using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Gamebreak.MiniGolf.Tests
{
    /// <summary>Graphics Pass 3 planning maths (foliage clumps, rope fences) and the biome atmospheres: deterministic, no scene needed.</summary>
    public class Gp3PlannerTests
    {
        static ScatterRecipe Recipe() => new ScatterRecipe
        {
            canopy = new[] { "Palm0", "Palm1", "Palm2" },
            understory = new[] { "BigLeaf0", "BigLeaf1", "Banana0", "Fern0", "LeafBush0", "LeafBush1" },
            ground = new[] { "GrassClump0", "GrassClump1", "Fern0" },
            accents = new[] { "FlowerShrub0", "FlowerShrub1", "FlowerShrub2" },
            clusters = 14,
        };

        // ------------------------------------------------------------------ foliage

        [Test]
        public void Scatter_IsDeterministic_AndRespectsTheKeepOut()
        {
            System.Func<Vector2, bool> allowed = p => !(Mathf.Abs(p.x) < 4f && Mathf.Abs(p.y) < 3f) && p.x > -20f;   // a green in the middle and a cliff edge
            var a = ScatterPlanner.Plan(7, Vector2.zero, 18f, Recipe(), allowed);
            var b = ScatterPlanner.Plan(7, Vector2.zero, 18f, Recipe(), allowed);
            Assert.Greater(a.Count, 40, "a real planting, not a token one");
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++) { Assert.AreEqual(a[i].position, b[i].position); Assert.AreEqual(a[i].species, b[i].species); }
            foreach (var it in a) Assert.IsTrue(allowed(it.position), $"{it.species} planted in a keep-out at {it.position}");
            var c = ScatterPlanner.Plan(8, Vector2.zero, 18f, Recipe(), allowed);
            Assert.IsFalse(a.Select(x => x.position).SequenceEqual(c.Select(x => x.position)), "a different seed plants differently");
        }

        [Test]
        public void Scatter_UsesAllLayers_ManyFamilies_AndVariesScaleAndRotation()
        {
            var items = ScatterPlanner.Plan(11, Vector2.zero, 20f, Recipe(), null);
            Assert.GreaterOrEqual(items.Select(i => i.layer).Distinct().Count(), 4, "canopy, understory, ground cover and accents");
            Assert.GreaterOrEqual(items.Select(i => i.species).Distinct().Count(), 8, "many plant families, not one repeated plant");
            Assert.Greater(items.Max(i => i.scale) - items.Min(i => i.scale), 0.5f, "scale variation");
            Assert.Greater(items.Max(i => i.yaw) - items.Min(i => i.yaw), 300f, "rotation variation");
            foreach (var layer in items.GroupBy(i => i.layer))
                Assert.That(layer.All(i => i.scale > 0.5f && i.scale < 1.6f), $"layer {layer.Key} scales stay sensible");
        }

        [Test]
        public void Scatter_LeavesNegativeSpace_BetweenClumps()
        {
            var r = Recipe(); r.minSeparation = 4f; r.emptiness = 0.35f; r.clusters = 10; r.clusterRadius = new Vector2(1.5f, 2.5f);
            var items = ScatterPlanner.Plan(3, Vector2.zero, 24f, r, null);
            // Sample a grid over the disc: a good share of it must be farther than 1.5 m from every plant.
            int bare = 0, total = 0;
            for (float x = -22f; x <= 22f; x += 1.5f)
            for (float y = -22f; y <= 22f; y += 1.5f)
            {
                var p = new Vector2(x, y);
                if (p.magnitude > 22f) continue;
                total++;
                if (!items.Any(i => (i.position - p).sqrMagnitude < 1.5f * 1.5f)) bare++;
            }
            Assert.Greater(bare / (float)total, 0.45f, "planting is clumped with open ground between, not an even carpet");
        }

        [Test]
        public void Scatter_ClumpsAreNotEvenlySpaced()
        {
            var items = ScatterPlanner.Plan(5, Vector2.zero, 20f, Recipe(), null);
            var nearest = items.Select(a => items.Where(b => !b.Equals(a)).Min(b => (b.position - a.position).magnitude)).ToList();
            float mean = nearest.Average(), sd = Mathf.Sqrt(nearest.Average(d => (d - mean) * (d - mean)));
            Assert.Greater(sd / mean, 0.35f, "nearest-neighbour distances vary a lot: clustered, not a grid");
        }

        [Test]
        public void ScatterNoise_StaysInRange_AndVaries()
        {
            float min = 1f, max = 0f;
            for (float x = -30f; x < 30f; x += 1.7f) for (float y = -30f; y < 30f; y += 1.7f) { float n = ScatterPlanner.Noise(new Vector2(x, y), 9f, 1); min = Mathf.Min(min, n); max = Mathf.Max(max, n); Assert.That(n, Is.InRange(0f, 1f)); }
            Assert.Greater(max - min, 0.5f);
        }

        // ------------------------------------------------------------------ fences

        [Test]
        public void Fence_PostsStandOutsideTheGreen_AtTheOutset_AndNeverInsideIt()
        {
            foreach (var h in TropicalCourse.Holes())
            {
                var posts = FencePlanner.Posts(h.layout, 0.3f, 2.4f, 1.4f);
                Assert.Greater(posts.Count, 0, $"hole {h.number} has a fence");
                foreach (var p in posts)
                {
                    foreach (var r in h.layout.areas)
                        Assert.IsFalse(p.position.x > r.xMin + 0.02f && p.position.x < r.xMax - 0.02f && p.position.y > r.yMin + 0.02f && p.position.y < r.yMax - 0.02f, $"hole {h.number}: post {p.position} is on the green");
                    float best = h.layout.areas.Min(r => Mathf.Max(Mathf.Max(r.xMin - p.position.x, p.position.x - r.xMax), Mathf.Max(r.yMin - p.position.y, p.position.y - r.yMax)));
                    Assert.That(best, Is.InRange(0.2f, 0.45f), $"hole {h.number}: post {p.position} is {best:F2} m from the rail");
                }
            }
        }

        [Test]
        public void Fence_SpacesPostsAlongRuns_AndHonoursTheKeepOut()
        {
            var l = new GreenLayout();
            l.Area(0f, 0f, 6f, 1.2f);
            var all = FencePlanner.Posts(l, 0.3f, 2.4f, 1.4f);
            Assert.GreaterOrEqual(all.Count, 6, "corners and the long runs");
            var long1 = all.Where(p => p.outward == Vector2.down).OrderBy(p => p.position.x).ToList();
            Assert.GreaterOrEqual(long1.Count, 3);
            for (int i = 1; i < long1.Count; i++) Assert.That(long1[i].position.x - long1[i - 1].position.x, Is.InRange(1.5f, 3.2f), "even-ish post spacing along a run");
            var keep = FencePlanner.Posts(l, 0.3f, 2.4f, 1.4f, p => Vector2.Distance(p, new Vector2(0f, -0.3f)) > 2f);
            Assert.Less(keep.Count, all.Count, "posts near the tee are dropped");
            Assert.IsTrue(keep.All(p => Vector2.Distance(p.position, new Vector2(0f, -0.3f)) > 2f));
        }

        [Test]
        public void Fence_AJoinBetweenTwoAreas_GetsNoPosts()
        {
            var l = new GreenLayout();
            l.Area(0f, 0f, 2f, 4f);
            l.Area(2f, 1f, 3f, 2f);   // shares the x = 2 edge from z 1..3
            var posts = FencePlanner.Posts(l, 0.3f, 2.4f, 0.8f);
            Assert.IsFalse(posts.Any(p => p.position.x > 1.9f && p.position.x < 2.5f && p.position.y > 1.2f && p.position.y < 2.8f), "no post across the open join");
        }

        // ------------------------------------------------------------------ atmosphere

        [Test]
        public void Atmosphere_EveryClusterHasItsOwnMood()
        {
            var ids = TropicalCourse.Clusters().Select(c => c.id).ToList();
            CollectionAssert.AreEquivalent(new[] { "start", "jungle", "temple", "volcanic", "summit" }, ids);
            var presets = ids.Select(BiomeAtmospheres.For).ToList();
            foreach (var p in presets)
            {
                Assert.That(p.fogDensity, Is.InRange(0.002f, 0.012f), $"{p.clusterId}: mist stays readable in VR");
                Assert.That(p.sunIntensity, Is.InRange(1.0f, 2.1f), $"{p.clusterId}: sun intensity stays sane");
                Assert.That(p.sunEuler.x, Is.InRange(8f, 70f), $"{p.clusterId}: the sun is above the horizon");
            }
            Assert.AreEqual(presets.Count, presets.Select(p => p.sunColor.ToString()).Distinct().Count(), "every biome has its own sun colour");
            Assert.Less(BiomeAtmospheres.For("summit").sunEuler.x, BiomeAtmospheres.For("start").sunEuler.x, "the summit is golden hour: a lower sun than the opening");
            Assert.Greater(BiomeAtmospheres.For("jungle").fogDensity, BiomeAtmospheres.For("start").fogDensity, "the jungle is misty");
            Assert.Greater(BiomeAtmospheres.For("summit").sunColor.r, BiomeAtmospheres.For("summit").sunColor.b + 0.3f, "a warm sunset sun");
            Assert.AreEqual("start", BiomeAtmospheres.For("nonsense").clusterId, "unknown ids fall back to the Starting Island");
        }

        [Test]
        public void Atmosphere_HolesMapToTheirBiome_AndBlendingIsSmooth()
        {
            var expected = new[] { "start", "start", "jungle", "jungle", "temple", "temple", "volcanic", "volcanic", "summit" };
            for (int h = 1; h <= 9; h++) Assert.AreEqual(expected[h - 1], BiomeAtmospheres.ForHole(h).clusterId, $"hole {h}");
            var a = BiomeAtmospheres.For("start"); var b = BiomeAtmospheres.For("summit");
            Assert.AreEqual(a.fogDensity, AtmospherePreset.Blend(a, b, 0f).fogDensity, 1e-6f);
            Assert.AreEqual(b.fogDensity, AtmospherePreset.Blend(a, b, 1f).fogDensity, 1e-6f);
            float mid = AtmospherePreset.Blend(a, b, 0.5f).fogDensity;
            Assert.That(mid, Is.InRange(Mathf.Min(a.fogDensity, b.fogDensity), Mathf.Max(a.fogDensity, b.fogDensity)));
            float prev = a.sunIntensity;
            for (float t = 0f; t <= 1.001f; t += 0.1f) { float v = AtmospherePreset.Blend(a, b, t).sunIntensity; Assert.That(Mathf.Abs(v - prev), Is.LessThan(0.1f), "no jumps"); prev = v; }
        }
    }
}
