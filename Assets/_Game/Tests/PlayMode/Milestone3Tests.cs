using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Gamebreak.MiniGolf.Tests
{
    /// <summary>Milestone 3: island clusters, cluster music, and Hole 3 "Jungle Crossing" (layout, bridge physics, scene wiring).</summary>
    public class Milestone3Tests
    {
        [SetUp] public void SetUp() { Time.timeScale = 3f; GolfAudio.ResetToDefaults(); }
        [TearDown] public void TearDown() { Time.timeScale = 1f; GolfAudio.ResetToDefaults(); }

        static IEnumerator WaitUntil(Func<bool> condition, float timeout)
        {
            float end = Time.time + timeout;
            while (!condition() && Time.time < end) yield return new WaitForFixedUpdate();
        }

        static IEnumerator Steps(int n) { for (int i = 0; i < n; i++) yield return new WaitForFixedUpdate(); }

        // ------------------------------------------------------------------ cluster data

        [Test]
        public void Clusters_CoverEveryHoleExactlyOnce_AndMatchHoleTags()
        {
            var holes = TropicalCourse.Holes();
            var clusters = TropicalCourse.Clusters();
            var ids = new HashSet<string>();
            foreach (var c in clusters)
            {
                Assert.IsTrue(ids.Add(c.id), $"duplicate cluster id {c.id}");
                Assert.IsNotEmpty(c.holes, $"cluster {c.id} has no holes");
                Assert.IsFalse(string.IsNullOrEmpty(c.musicName), $"cluster {c.id} has no music name");
                Assert.LessOrEqual(c.holes.Length, 3, $"cluster {c.id} has more than three holes");
            }
            foreach (var h in holes)
            {
                int owners = 0;
                foreach (var c in clusters) if (c.Contains(h.number)) { owners++; Assert.AreEqual(c.id, h.cluster, $"hole {h.number} tag"); }
                Assert.AreEqual(1, owners, $"hole {h.number} must belong to exactly one cluster");
            }
            for (int i = 0; i < holes.Count; i++) Assert.AreEqual(i + 1, holes[i].number, "holes must be numbered in play order");
            // Holes of one cluster are consecutive (no returning to an island).
            foreach (var c in clusters)
                for (int i = 0; i + 1 < c.holes.Length; i++) Assert.AreEqual(c.holes[i] + 1, c.holes[i + 1], $"cluster {c.id} holes not consecutive");
        }

        [Test]
        public void Clusters_ArePlayedInOrder_WithNoReturnToAnEarlierIsland()
        {
            string last = null; var seen = new HashSet<string>();
            foreach (var h in TropicalCourse.Holes())
            {
                if (h.cluster != last) { Assert.IsTrue(seen.Add(h.cluster), $"hole {h.number} returns to island '{h.cluster}'"); last = h.cluster; }
            }
        }

        [Test]
        public void MusicFiles_UseTheDocumentedInProjectNames()
        {
            var expected = new Dictionary<string, string>
            {
                [TropicalCourse.StartCluster] = "Assets/_Game/Audio/Music/IslandExploration.ogg",
                [TropicalCourse.JungleCluster] = "Assets/_Game/Audio/Music/JungleTheme.ogg",
            };
            foreach (var c in TropicalCourse.Clusters())
                if (expected.TryGetValue(c.id, out var path)) Assert.AreEqual(path, c.MusicAssetPath, c.id);
        }

        // ------------------------------------------------------------------ Hole 3 layout

        static HoleDefinition Hole3() => TropicalCourse.Holes().Find(h => h.number == 3);

        /// <summary>Flood fill over the layout's 10 cm grid from the tee to the cup.</summary>
        static bool Connected(GreenLayout l, Vector2 from, Vector2 to, out int cells)
        {
            const float step = 0.1f;
            bool Inside(int i, int j)
            {
                var p = new Vector2(i * step + step * 0.5f, j * step + step * 0.5f);
                foreach (var r in l.areas) if (r.Contains(p)) return true;
                return false;
            }
            var start = new Vector2Int(Mathf.FloorToInt(from.x / step), Mathf.FloorToInt(from.y / step));
            var goal = new Vector2Int(Mathf.FloorToInt(to.x / step), Mathf.FloorToInt(to.y / step));
            var seen = new HashSet<Vector2Int> { start };
            var queue = new Queue<Vector2Int>(); queue.Enqueue(start);
            cells = 0;
            while (queue.Count > 0)
            {
                var c = queue.Dequeue(); cells++;
                if (c == goal) return true;
                foreach (var d in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
                {
                    var n = c + d;
                    if (!seen.Contains(n) && Inside(n.x, n.y)) { seen.Add(n); queue.Enqueue(n); }
                }
            }
            return false;
        }

        [Test]
        public void Hole3_IsAParThreeWithAConnectedLaneFromTeeToCup()
        {
            var def = Hole3();
            Assert.IsNotNull(def, "hole 3 is not defined");
            Assert.AreEqual(3, def.par);
            Assert.AreEqual(TropicalCourse.JungleCluster, def.cluster);
            Assert.IsTrue(def.layout.cup.HasValue);
            Assert.IsTrue(Connected(def.layout, def.tee, def.layout.cup.Value, out _), "tee and cup are not connected by playable surface");
        }

        [Test]
        public void Hole3_Bridge_IsPartOfTheSurface_AndWideEnoughForVR()
        {
            var l = Hole3().layout;
            Assert.AreEqual(1, l.deckAreas.Count, "expected one bridge deck");
            var deck = l.deckAreas[0];
            Assert.GreaterOrEqual(Mathf.Min(deck.width, deck.height), 1.2f - 1e-4f, "bridge narrower than Hole 1's lane");
            // The deck must lie wholly inside the playable areas (it is the putting surface, not decoration).
            foreach (var corner in new[] { new Vector2(deck.xMin + 0.01f, deck.yMin + 0.01f), new Vector2(deck.xMax - 0.01f, deck.yMax - 0.01f), deck.center })
            {
                bool inside = false;
                foreach (var r in l.areas) if (r.Contains(corner)) inside = true;
                Assert.IsTrue(inside, $"deck point {corner} is not playable");
            }
            // Walkable on both sides: the deck touches playable area at both ends.
            Assert.IsTrue(Connected(l, new Vector2(deck.xMin - 0.3f, deck.center.y), new Vector2(deck.xMax + 0.3f, deck.center.y), out _), "bridge does not link its two banks");
        }

        [Test]
        public void Hole3_LaneHeightHasNoCliffs()
        {
            var l = Hole3().layout;
            float maxStep = 0f;
            for (float x = -0.5f; x < 10f; x += 0.1f)
            for (float z = 0.05f; z < 9.2f; z += 0.1f)
            {
                float h = l.Height(x, z);
                maxStep = Mathf.Max(maxStep, Mathf.Abs(l.Height(x + 0.1f, z) - h), Mathf.Abs(l.Height(x, z + 0.1f) - h));
            }
            // 10 cm cells: a 0.04 m step is a 40% slope, far beyond the hump (about 19%) and the cross-slope (2.5%).
            Assert.Less(maxStep, 0.04f, $"height jump of {maxStep:F3} m between neighbouring cells");
        }

        [Test]
        public void Hole3_Bridge_HumpIsModest()
        {
            var l = Hole3().layout;
            float crest = l.Height(5.2f, 4.6f), bank = l.Height(2.5f, 4.6f);
            Assert.That(crest - bank, Is.InRange(0.08f, 0.25f), "the bridge hump should be a gentle rise a soft putt can still cross");
        }

        [Test]
        public void Hole3_FinalLane_LeansTowardTheEastRail_ButTheCupTileIsLevelEnough()
        {
            var l = Hole3().layout;
            float west = l.Height(8.9f, 8.0f), east = l.Height(9.9f, 8.0f);
            Assert.Less(east, west, "the final lane should lean toward the east rail");
            Assert.Less(west - east, 0.05f, "cross-slope should stay under about 5%");
        }

        // ------------------------------------------------------------------ Hole 3 physics (isolated)

        /// <summary>Hole 3 built alone at the origin (hole-local = world coordinates), with a ball and no scenery.</summary>
        class Hole3Bed : IDisposable
        {
            public readonly GolfTuning tuning;
            public readonly GolfBall ball;
            public readonly HoleController hole;
            public readonly GreenLayout layout;
            readonly GameObject m_Root;

            public Hole3Bed()
            {
                tuning = ScriptableObject.CreateInstance<GolfTuning>();
                GolfPhysicsBootstrap.Apply(tuning);
                m_Root = new GameObject("Hole3Bed");
                var ballGo = new GameObject("Ball");
                ballGo.transform.SetParent(m_Root.transform);
                ballGo.AddComponent<Rigidbody>();
                ballGo.AddComponent<SphereCollider>();
                ball = ballGo.AddComponent<GolfBall>();
                ball.SetTuning(tuning);
                var src = Hole3();
                layout = src.layout;
                var def = new HoleDefinition { number = 3, name = src.name, par = src.par, layout = src.layout, tee = src.tee, origin = Vector3.zero, yaw = 0f, cluster = src.cluster };
                hole = HoleFactory.Build(def, null, tuning, m_Root.transform, ball);
                hole.BeginHole(ball);
            }

            public Vector3 Surface(float x, float z) => new Vector3(x, layout.Height(x, z) + ball.Radius + 0.002f, z);

            public void Dispose() { Object.Destroy(m_Root); Object.Destroy(tuning); }
        }

        [UnityTest]
        public IEnumerator Hole3_Bridge_CanBeCrossedWithAFirmPutt()
        {
            using var b = new Hole3Bed();
            yield return Steps(5);
            b.ball.PlaceAt(b.Surface(2.8f, 4.6f));
            yield return Steps(5);
            b.ball.Strike(new Vector3(2.6f, 0f, 0f));
            yield return WaitUntil(() => b.ball.Position.x > 7.4f, 6f);
            Assert.Greater(b.ball.Position.x, 7.4f, $"ball did not reach the landing pad, at {b.ball.Position}");
            Assert.AreEqual(1, b.hole.Strokes, "the crossing should not have cost a penalty stroke");
            Assert.IsTrue(b.ball.InPlay);
        }

        [UnityTest]
        public IEnumerator Hole3_Bridge_RailsKeepAnAngledPuttOnTheDeck()
        {
            foreach (float angle in new[] { -14f, 14f })
            {
                using var b = new Hole3Bed();
                yield return Steps(5);
                b.ball.PlaceAt(b.Surface(2.8f, 4.6f));
                yield return Steps(5);
                b.ball.Strike(Quaternion.Euler(0f, angle, 0f) * new Vector3(3.2f, 0f, 0f));
                yield return Steps(3);
                yield return WaitUntil(() => b.ball.Position.x > 7.2f || b.ball.IsAtRest, 7f);
                Assert.AreEqual(1, b.hole.Strokes, $"angled putt ({angle} deg) left the course and cost a stroke");
                Assert.Greater(b.ball.Position.x, 3.2f, $"ball ({angle} deg) went backwards, at {b.ball.Position}");
                var z = b.ball.Position.z;
                Assert.That(z, Is.InRange(3.4f, 6.5f), $"ball ({angle} deg) ended outside the corridor, z {z:F2}");
            }
        }

        [UnityTest]
        public IEnumerator Hole3_SoftPutt_RollsBackOffTheHump_WithoutLeavingTheCourse()
        {
            using var b = new Hole3Bed();
            yield return Steps(5);
            b.ball.PlaceAt(b.Surface(3.1f, 4.6f));
            yield return Steps(5);
            b.ball.Strike(new Vector3(1.0f, 0f, 0f)); // too soft to crest (needs about 1.6 m/s)
            yield return Steps(3);
            yield return WaitUntil(() => b.ball.IsAtRest, 8f);
            yield return Steps(20);
            Assert.AreEqual(1, b.hole.Strokes, "a soft putt must not cost a penalty");
            Assert.Less(b.ball.Position.x, 6.0f, "a 1.0 m/s putt should not make it over the crest");
            Assert.IsTrue(b.ball.InPlay);
        }

        [UnityTest]
        public IEnumerator Hole3_FinalLane_CupCanBeHoled()
        {
            using var b = new Hole3Bed();
            yield return Steps(5);
            var cup = b.hole.Cup.transform.position;
            b.ball.PlaceAt(new Vector3(cup.x, b.layout.Height(cup.x, cup.z - 0.4f) + b.ball.Radius + 0.002f, cup.z - 0.4f));
            yield return Steps(5);
            Vector3 d = cup - b.ball.Position; d.y = 0f;
            b.ball.Strike(d.normalized * 1.1f);
            yield return WaitUntil(() => b.hole.IsComplete, 8f);
            Assert.IsTrue(b.hole.IsComplete, $"cup not holed, ball at {b.ball.Position}, cup {cup}");
        }

        [UnityTest]
        public IEnumerator Hole3_BankShotOffTheLaneRail_EntersTheFinalLane()
        {
            using var b = new Hole3Bed();
            yield return Steps(5);
            // From the landing pad the cup is not in line: mirror it across the lane's east rail (x = 10) and aim there.
            b.ball.PlaceAt(b.Surface(7.5f, 4.4f));
            yield return Steps(5);
            Vector3 aim = new Vector3(10.6f, 0f, 8.5f) - b.ball.Position; aim.y = 0f;
            b.ball.Strike(aim.normalized * 2.4f);
            yield return Steps(3);
            yield return WaitUntil(() => b.ball.IsAtRest || b.hole.IsComplete, 8f);
            Assert.AreEqual(1, b.hole.Strokes);
            Assert.That(b.hole.IsComplete || b.ball.Position.z > 6.5f, $"the bank shot did not reach the final lane, ball at {b.ball.Position}");
        }

        // ------------------------------------------------------------------ music director

        static AudioClip Tone(string name) => AudioClip.Create(name, 44100 * 4, 1, 44100, false);

        static MusicDirector NewDirector(MusicEntry[] entries, AudioClip fallback = null)
        {
            var go = new GameObject("TestMusic");
            var d = go.AddComponent<MusicDirector>();
            d.Configure(null, entries, fallback);
            return d;
        }

        [UnityTest]
        public IEnumerator Music_StaysContinuousWithinACluster_AndCrossfadesBetweenClusters()
        {
            var a = Tone("a"); var b = Tone("b");
            var d = NewDirector(new[]
            {
                new MusicEntry { clusterId = "one", holes = new[] { 1, 2 }, clip = a },
                new MusicEntry { clusterId = "two", holes = new[] { 3 }, clip = b },
            });
            yield return null;
            d.NotifyHole(1);
            var first = d.ActiveSource;
            Assert.AreSame(a, first.clip);
            yield return new WaitForSecondsRealtime(0.3f);
            float t = first.time;
            d.NotifyHole(2); // same cluster: nothing happens
            Assert.AreSame(first, d.ActiveSource, "same-cluster hole must not switch sources");
            Assert.GreaterOrEqual(first.time, t, "same-cluster hole must not restart the track");
            Assert.AreEqual(1, d.PlayingSourceCount);

            d.NotifyHole(3); // new cluster: crossfade
            Assert.AreNotSame(first, d.ActiveSource);
            Assert.AreSame(b, d.ActiveSource.clip);
            Assert.AreEqual(2, d.PlayingSourceCount, "both tracks should overlap during the crossfade");
            yield return new WaitForSecondsRealtime(4.2f);
            Assert.AreEqual(1, d.PlayingSourceCount, "the old track should stop after the fade");
            Assert.IsTrue(d.ActiveSource.loop);
            Assert.AreEqual(GolfAudio.DefaultMusic, d.ActiveSource.volume, 0.02f, "settled level should be the default music volume");
            Object.Destroy(d.gameObject);
        }

        [UnityTest]
        public IEnumerator Music_MissingClip_KeepsPlayingTheCurrentTrack_AndWarnsOnce()
        {
            var a = Tone("a");
            var d = NewDirector(new[]
            {
                new MusicEntry { clusterId = "one", holes = new[] { 1 }, clip = a },
                new MusicEntry { clusterId = "later", holes = new[] { 3 }, clip = null },
            });
            yield return null;
            d.NotifyHole(1);
            var src = d.ActiveSource;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Music for cluster 'later' is missing"));
            d.NotifyHole(3);
            d.NotifyHole(3); // no second warning
            Assert.AreSame(src, d.ActiveSource);
            Assert.IsTrue(src.isPlaying);
            Assert.AreEqual("one", d.CurrentClusterId);
            Object.Destroy(d.gameObject);
        }

        [UnityTest]
        public IEnumerator Music_FollowsTheLiveMusicVolume()
        {
            var d = NewDirector(new[] { new MusicEntry { clusterId = "one", holes = new[] { 1 }, clip = Tone("a"), gain = 1f } });
            yield return null;
            d.NotifyHole(1);
            yield return new WaitForSecondsRealtime(4.3f);
            GolfAudio.MusicVolume = 0.2f;
            yield return null; yield return null;
            Assert.AreEqual(0.2f, d.ActiveSource.volume, 0.02f);
            Object.Destroy(d.gameObject);
        }

        [Test]
        public void GolfAudio_MusicAndEffectsVolumesAreIndependentAndClamped()
        {
            Assert.AreEqual(0.55f, GolfAudio.MusicVolume, 1e-4f, "default music volume");
            GolfAudio.SfxVolume = 0.25f;
            Assert.AreEqual(0.55f, GolfAudio.MusicVolume, 1e-4f, "changing effects must not change music");
            GolfAudio.MusicVolume = 3f;
            Assert.AreEqual(1f, GolfAudio.MusicVolume, 1e-4f);
            Assert.AreEqual(0.25f, GolfAudio.SfxVolume, 1e-4f);
            GolfAudio.SfxVolume = -1f;
            Assert.AreEqual(0f, GolfAudio.SfxVolume, 1e-4f);
        }

        // ------------------------------------------------------------------ the generated scene

        [UnityTest]
        public IEnumerator TropicalScene_HasJungleIsland_WithHoleThreeOnABridge()
        {
            SceneManager.LoadScene("TropicalAdventure");
            yield return null;
            yield return new WaitForFixedUpdate();
            var course = Object.FindFirstObjectByType<CourseController>();
            Assert.GreaterOrEqual(course.Holes.Length, 3);
            var hole3 = course.Holes[2];
            Assert.AreEqual(3, hole3.HoleNumber);
            Assert.AreEqual(3, hole3.Par);
            var surface = hole3.GetComponentInChildren<PlayableSurface>();
            var renderer = surface.GetComponent<MeshRenderer>();
            Assert.AreEqual(3, renderer.sharedMaterials.Length, "the surface should carry the extra bridge-deck material");
            Assert.IsNotNull(renderer.sharedMaterials[2]);
            Assert.IsNotNull(GameObject.Find("IslandTerrain2"), "no Jungle Island terrain");

            // The deck floats over a ravine: terrain beneath the bridge centre is far below the surface.
            var t = hole3.transform;
            Vector3 deckCentre = t.TransformPoint(new Vector3(5.2f, 0.1f, 4.6f));
            var hits = Physics.RaycastAll(deckCentre + Vector3.up * 40f, Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore);
            float deckY = float.NaN, groundY = float.NaN;
            foreach (var h in hits)
            {
                if (h.collider.GetComponent<PlayableSurface>()) deckY = h.point.y;
                else if (h.collider.name == "IslandTerrain2") groundY = h.point.y;
            }
            Assert.IsFalse(float.IsNaN(deckY), "no deck under the bridge centre");
            Assert.IsFalse(float.IsNaN(groundY), "no terrain under the bridge");
            Assert.Greater(deckY - groundY, 2.0f, $"expected at least 2 m of ravine under the bridge, got {deckY - groundY:F2} m");
        }

        [UnityTest]
        public IEnumerator TropicalScene_MusicDirectorKnowsEveryCluster()
        {
            SceneManager.LoadScene("TropicalAdventure");
            yield return null;
            var director = Object.FindFirstObjectByType<MusicDirector>();
            Assert.IsNotNull(director);
            foreach (var c in TropicalCourse.Clusters())
            {
                foreach (int h in c.holes)
                {
                    var e = director.Find(h);
                    Assert.IsNotNull(e, $"no music entry for hole {h}");
                    Assert.AreEqual(c.id, e.clusterId);
                }
            }
        }
    }
}
