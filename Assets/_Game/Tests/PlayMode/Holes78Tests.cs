using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Gamebreak.MiniGolf.Tests
{
    /// <summary>
    /// Holes 7 (Lava Falls) and 8 (Caldera Run), the vent transport and the bowl's gate notch. Layout, spec and logic tests are deterministic; the physics tests
    /// measure the real ball and assert relationships and generous windows, and log the real outcome tables so the numbers can be retuned in Unity.
    /// </summary>
    public class Holes78Tests
    {
        [SetUp] public void SetUp() => Time.timeScale = 3f;
        [TearDown] public void TearDown() => Time.timeScale = 1f;

        static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float end = Time.time + timeoutSeconds;
            while (!condition() && Time.time < end) yield return new WaitForFixedUpdate();
        }

        static IEnumerator Steps(int n) { for (int i = 0; i < n; i++) yield return new WaitForFixedUpdate(); }

        static HoleDefinition Def(int number) => TropicalCourse.Holes().Find(h => h.number == number);

        /// <summary>One production hole at the origin, built with the real factory (no theme), a ball and a hole controller.</summary>
        class HoleBed : IDisposable
        {
            public readonly GolfTuning tuning;
            public readonly GolfBall ball;
            public readonly HoleController hole;
            public readonly GreenLayout layout;
            readonly GameObject m_Root;

            public HoleBed(int number)
            {
                tuning = ScriptableObject.CreateInstance<GolfTuning>();
                GolfPhysicsBootstrap.Apply(tuning);
                m_Root = new GameObject($"Hole{number}Bed");
                var ballGo = new GameObject("Ball");
                ballGo.transform.SetParent(m_Root.transform);
                ballGo.AddComponent<Rigidbody>();
                ballGo.AddComponent<SphereCollider>();
                ball = ballGo.AddComponent<GolfBall>();
                ball.SetTuning(tuning);
                var src = Def(number);
                layout = src.layout;
                var def = new HoleDefinition { number = number, name = src.name, par = src.par, layout = src.layout, tee = src.tee, origin = Vector3.zero, yaw = 0f, cluster = src.cluster, buildExtras = src.buildExtras };
                hole = HoleFactory.Build(def, null, tuning, m_Root.transform, ball);
                hole.BeginHole(ball);
                Physics.SyncTransforms();
            }

            public Vector3 Surface(float x, float z) => new Vector3(x, layout.Height(x, z) + ball.Radius + 0.002f, z);
            public VentTransport Vent => hole.GetComponentInChildren<VentTransport>();
            public Transform Root => m_Root.transform;
            public void Dispose() { Object.Destroy(m_Root); Object.Destroy(tuning); }
        }

        /// <summary>Strike speed that rolls a ball <paramref name="distance"/> metres on the flat (the game's rolling model).</summary>
        static float SpeedForDistance(float distance, GolfTuning t)
        {
            float a0 = t.rollingDeceleration, k = t.speedDrag;
            float lo = 0.1f, hi = 8f;
            for (int i = 0; i < 40; i++)
            {
                float v = (lo + hi) * 0.5f;
                float d = (v - a0 / k * Mathf.Log(1f + k * v / a0)) / k;
                if (d < distance) lo = v; else hi = v;
            }
            return (lo + hi) * 0.5f;
        }

        /// <summary>On the bowl's shelf or cone (not on the stone approach that touches its rim, which stands 2 cm higher).</summary>
        static bool InBowl(Vector3 p, CalderaRunSpec s) =>
            Vector2.Distance(new Vector2(p.x, p.z), s.BowlCentre) < s.BowlRadius + 0.05f && p.y < s.GateLevel + 0.012f;

        static float RollOut(float speed, GolfTuning t)
        {
            float a0 = t.rollingDeceleration, k = t.speedDrag;
            return (speed - a0 / k * Mathf.Log(1f + k * speed / a0)) / k;
        }

        // ------------------------------------------------------------------ the Volcanic Island data and the course so far

        [Test]
        public void VolcanicCluster_HasHolesSevenAndEight_OnAnIslandOfItsOwn()
        {
            var clusters = TropicalCourse.Clusters();
            var volcanic = clusters.Find(c => c.id == TropicalCourse.VolcanicCluster);
            Assert.IsNotNull(volcanic);
            CollectionAssert.AreEqual(new[] { 7, 8 }, volcanic.holes);
            Assert.Greater(volcanic.radius, 10f);
            foreach (var other in clusters)
                if (other != volcanic && other.radius > 0f)
                    Assert.Greater(Vector2.Distance(other.centre, volcanic.centre), other.radius + volcanic.radius + 8f, $"the Volcanic Island overlaps {other.id}");
            foreach (var n in new[] { 7, 8 })
            {
                var d = Def(n);
                Assert.AreEqual(TropicalCourse.VolcanicCluster, d.cluster);
                var rects = d.layout.areas.Concat(d.extraAreas).ToList();
                float minX = rects.Min(a => a.xMin), maxX = rects.Max(a => a.xMax), minZ = rects.Min(a => a.yMin), maxZ = rects.Max(a => a.yMax);
                var world = new Vector2(d.origin.x + (minX + maxX) * 0.5f, d.origin.z + (minZ + maxZ) * 0.5f);
                Assert.Less(Vector2.Distance(world, volcanic.centre), volcanic.radius - 4f, $"hole {n} is not safely inside the island");
                foreach (var r in rects)
                    foreach (var corner in new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMax) })
                        Assert.Less(Vector2.Distance(new Vector2(d.origin.x + corner.x, d.origin.z + corner.y), volcanic.centre), volcanic.radius - 1f, $"hole {n} reaches the island's edge");
            }
        }

        [Test]
        public void Course_HasNineConsecutiveHoles_AndHolesSevenAndEightDoNotOverlap()
        {
            var holes = TropicalCourse.Holes();
            CollectionAssert.AreEqual(Enumerable.Range(1, 9).ToArray(), holes.Select(h => h.number).ToArray(), "holes 1..9 in order");
            Rect World(HoleDefinition d)
            {
                var rects = d.layout.areas.Concat(d.extraAreas).ToList();
                return Rect.MinMaxRect(d.origin.x + rects.Min(r => r.xMin), d.origin.z + rects.Min(r => r.yMin), d.origin.x + rects.Max(r => r.xMax), d.origin.z + rects.Max(r => r.yMax));
            }
            Assert.IsFalse(World(Def(7)).Overlaps(World(Def(8))), "holes 7 and 8 must not overlap in the world");
            foreach (var h in holes.Where(h => h.number >= 7))
            {
                Assert.IsTrue(h.par >= 3 && h.par <= 5);
                Assert.IsNotNull(h.buildExtras, $"hole {h.number} is a composite hole");
            }
            foreach (var c in TropicalCourse.Clusters())
                for (int i = 0; i + 1 < c.holes.Length; i++) Assert.AreEqual(c.holes[i] + 1, c.holes[i + 1]);
        }

        // ------------------------------------------------------------------ Hole 7 layout

        [Test]
        public void Hole7_Definition_IsAParFourWithAVentAndLava()
        {
            var d = Def(7);
            Assert.IsNotNull(d);
            Assert.AreEqual("Lava Falls", d.name);
            Assert.AreEqual(4, d.par);
            Assert.AreEqual(TropicalCourse.VolcanicCluster, d.cluster);
            var s = TropicalCourse.Hole7Spec();
            Assert.AreEqual("", s.Validate());
            Assert.IsTrue(d.layout.cup.HasValue, "the cup is on the final green of the layout");
            Assert.That(d.layout.cup.Value, Is.EqualTo(s.Cup));
            Assert.That(d.tee.y, Is.LessThan(s.PadLength), "the tee is on Tier 1's pad");
            Assert.GreaterOrEqual(d.layout.openEdges.Count, 3, "lava edges have no rail: causeway, Tier 2's corner, Tier 3's west edge");
            Assert.GreaterOrEqual(s.LavaPlan().Count, 3, "a lava lake, a lava river and a lava pool");
            Assert.IsNotNull(d.buildExtras);
        }

        [Test]
        public void Hole7_HeightsFormThreeTiers_WithAGentleRestableCauseway()
        {
            var s = TropicalCourse.Hole7Spec();
            var l = Def(7).layout;
            Assert.That(l.Height(0f, 1f), Is.EqualTo(0f).Within(1e-4f), "Tier 1");
            Assert.That(l.Height(1.0f, s.RampEndZ + 0.4f), Is.EqualTo(s.Tier2Height).Within(1e-3f), "Tier 2");
            Assert.That(l.Height(1.0f, s.Tier3FrontZ + 1f), Is.EqualTo(s.Tier3Height).Within(1e-4f), "Tier 3");
            Assert.Greater(s.Tier2Height, 0.15f);
            Assert.Greater(s.Tier3Height - s.Tier2Height, 0.4f, "the vent genuinely lifts the ball");
            float x = s.Mouth.x, prev = -1f;
            for (float z = s.CrossEndZ + 0.1f; z <= s.RampEndZ; z += 0.1f)
            {
                float h = l.Height(x, z);
                Assert.GreaterOrEqual(h, prev - 1e-4f, $"the causeway only climbs (z {z:F1})");
                if (prev >= 0f) Assert.LessOrEqual(h - prev, 0.1f * s.CausewaySlope * 1.2f + 1e-4f, $"gentle at z {z:F1}");
                prev = h;
            }
            Assert.That(s.CausewaySlope, Is.InRange(0.05f, 0.0785f), "a ball can rest on the causeway and be struck again");
            Assert.That(Mathf.Sin(Mathf.Atan(s.CausewaySlope)), Is.LessThan(0.0785f));
        }

        [Test]
        public void Hole7_VentMouth_IsADishSteeperThanRestHold_WithItsLowPointAtTheMouth()
        {
            var s = TropicalCourse.Hole7Spec();
            var l = Def(7).layout;
            Vector2 m = s.Mouth;
            float centre = l.Height(m.x, m.y);
            Assert.That(centre, Is.EqualTo(s.Tier2Height - s.dishDepth).Within(1e-4f));
            Assert.That(l.Height(m.x + s.dishRadius + 0.1f, m.y), Is.EqualTo(s.Tier2Height).Within(1e-4f), "flat Tier 2 outside the dish");
            float slope = s.dishDepth / s.dishRadius;
            Assert.Greater(slope, 0.0785f, "a ball cannot rest on the dish's slope: it can only rest at the mouth");
            // On the 0.1 m grid the neighbouring vertices keep that slope.
            Assert.Greater((l.Height(m.x + 0.1f, m.y) - centre) / 0.1f, 0.0785f);
            Assert.Greater((l.Height(m.x, m.y + 0.1f) - centre) / 0.1f, 0.0785f);
            Assert.That(m.y, Is.LessThan(s.Tier2EndZ - 0.5f), "room behind the mouth for a rebound");
        }

        [Test]
        public void Hole7_LavaSitsWhereTheOpenEdgesAre_AndTheTiersAreSeparatedByTheRiver()
        {
            var s = TropicalCourse.Hole7Spec();
            var l = Def(7).layout;
            var lava = s.LavaPlan();
            var lake = lava.First(v => v.name == "LavaLake"); var river = lava.First(v => v.name == "LavaRiver"); var pool = lava.First(v => v.name == "LavaPool");
            Assert.That(lake.x1, Is.EqualTo(s.CausewayWestX).Within(1e-4f), "the lake lies along the causeway's open west edge");
            Assert.LessOrEqual(lake.z0, s.CrossEndZ + 0.1f);
            Assert.GreaterOrEqual(lake.z1, s.RampEndZ, "and reaches Tier 2's open corner");
            Assert.That(pool.x1, Is.EqualTo(s.Tier3WestX).Within(1e-4f), "the pool lies off Tier 3's open west edge");
            Assert.That(river.z0, Is.EqualTo(s.Tier2EndZ).Within(1e-4f));
            Assert.That(river.z1, Is.EqualTo(s.Tier3FrontZ).Within(1e-4f), "the river separates Tier 2 from Tier 3: the vent is the only way across");
            foreach (var v in lava)
            {
                Assert.Less(v.top, s.Tier3Height - 0.05f, $"{v.name} lies below the floor beside it");
                Assert.That(v.x1, Is.GreaterThan(v.x0)); Assert.That(v.z1, Is.GreaterThan(v.z0));
            }
            // Tier 1 and Tier 2 are not joined to Tier 3 by any layout area.
            Assert.IsFalse(l.areas.Any(a => a.yMin < s.Tier2EndZ - 0.01f && a.yMax > s.Tier3FrontZ + 0.01f), "no green bridges the river");
            // The cup's approach is a real putting section.
            Assert.GreaterOrEqual(s.Tier3EndZ - s.Tier3FrontZ, 4f);
            Assert.GreaterOrEqual(s.CrossEastX - s.Tier3WestX, 3f);
        }

        [Test]
        public void Hole7_BendBank_TurnsATeePuttEast()
        {
            var s = TropicalCourse.Hole7Spec();
            var bank = s.WallPlan().First(w => w.name == "BendBank");
            var n = BankWall.FaceNormal(new Vector2(bank.a.x, bank.a.z), new Vector2(bank.b.x, bank.b.z), new Vector2(0f, 1f));
            var o = BankWall.Reflect(Vector2.up, n);
            Assert.Greater(o.x, 0.6f, "a ball struck up the tee pad is sent east along the cross lane");
            Assert.Greater(o.magnitude, 0.6f, "and keeps most of its pace");
            Assert.That(bank.a.x, Is.EqualTo(-s.PadHalf).Within(1e-3f)); Assert.That(bank.b.x, Is.EqualTo(s.PadHalf).Within(1e-3f), "the bank spans the whole pad width");
        }

        [Test]
        public void Hole7_RidePath_RunsFromTheMouthToTheExit_OverTheRiver()
        {
            var s = TropicalCourse.Hole7Spec();
            float r = GolfTuning.Default.ballRadius;
            var path = s.RidePath(r);
            Assert.GreaterOrEqual(path.Length, 20);
            Assert.That(path[0].x, Is.EqualTo(s.Mouth.x).Within(1e-3f)); Assert.That(path[0].z, Is.EqualTo(s.Mouth.y).Within(1e-3f));
            Assert.That(path[0].y, Is.EqualTo(s.Tier2Height - s.dishDepth + r).Within(1e-3f), "starts with the ball resting in the mouth");
            var last = path[path.Length - 1];
            Assert.That(last.x, Is.EqualTo(s.ExitXZ.x).Within(1e-3f)); Assert.That(last.z, Is.EqualTo(s.ExitXZ.y).Within(1e-3f));
            Assert.That(last.y, Is.EqualTo(s.Tier3Height + r + 0.004f).Within(1e-3f), "ends just above Tier 3's floor");
            Assert.That(path.Max(p => p.y), Is.GreaterThan(1.0f), "the chute climbs high over the river so the ride is seen");
            Assert.That(path.Where(p => p.z > s.Tier2EndZ + 0.5f && p.z < s.Tier3FrontZ - 0.5f).All(p => p.y > s.Tier2Height + 0.5f), "above the lava all the way across");
            for (int i = 1; i < path.Length; i++) Assert.GreaterOrEqual(path[i].z, path[i - 1].z - 0.02f, "the ride never doubles back");
            for (int i = 1; i < path.Length; i++) Assert.Less(Vector3.Distance(path[i], path[i - 1]), 0.5f, "a smooth path, with no jumps");
            Assert.That(last.x, Is.EqualTo(s.Mouth.x).Within(1e-3f), "the exit is in line with the mouth, so the chute reads as one straight run");
        }

        [Test]
        public void Hole7_ExitRollOut_LeavesARealPutt_NotInLineWithTheCup()
        {
            var s = TropicalCourse.Hole7Spec();
            var t = ScriptableObject.CreateInstance<GolfTuning>();
            try
            {
                GolfPhysicsBootstrap.Apply(t);
                float roll = RollOut(s.vent.exitSpeed, t);
                Assert.That(roll, Is.InRange(2.0f, 4.0f), "the ball rolls a believable distance after leaving the exit");
                var stop = new Vector2(s.ExitXZ.x, s.ExitXZ.y + roll);
                Assert.Less(stop.y, s.Tier3EndZ - 0.5f, "it does not hit the back rail");
                float dist = Vector2.Distance(stop, s.Cup);
                Assert.That(dist, Is.InRange(1.2f, 3.5f), "it rests a putt away from the cup");
                Assert.Greater(Mathf.Abs(s.ExitXZ.x - s.Cup.x), 0.8f, "the exit line misses the cup, so riding the vent cannot hole the ball");
                Assert.That(s.vent.exitSpeed, Is.InRange(1.2f, t.maxBallSpeed));
            }
            finally { Object.DestroyImmediate(t); }
        }

        // ------------------------------------------------------------------ VentTransport logic (no physics)

        [Test]
        public void Vent_PathPoint_FollowsArcLength()
        {
            var pts = new[] { Vector3.zero, new Vector3(0f, 0f, 1f), new Vector3(0f, 0f, 3f) };
            Assert.AreEqual(3f, VentTransport.PathLength(pts), 1e-5f);
            Assert.That(VentTransport.PathPoint(pts, 0f), Is.EqualTo(Vector3.zero));
            Assert.That(VentTransport.PathPoint(pts, 1f), Is.EqualTo(new Vector3(0f, 0f, 3f)));
            Assert.That(VentTransport.PathPoint(pts, 0.5f).z, Is.EqualTo(1.5f).Within(1e-5f), "halfway by length is not halfway by points");
            Assert.That(VentTransport.PathPoint(pts, 2f).z, Is.EqualTo(3f).Within(1e-5f), "clamped");
            Assert.That(VentTransport.PathPoint(pts, -1f), Is.EqualTo(Vector3.zero), "clamped");
            Assert.That(VentTransport.PathPoint(new Vector3[0], 0.5f), Is.EqualTo(Vector3.zero));
            Assert.That(VentTransport.PathPoint(new[] { Vector3.one }, 0.5f), Is.EqualTo(Vector3.one));
            Assert.That(VentTransport.PathPoint(new[] { Vector3.one, Vector3.one }, 0.5f), Is.EqualTo(Vector3.one), "a zero-length path does not divide by zero");
        }

        [Test]
        public void Vent_Ease_IsMonotone_AndStartsAndEndsGently()
        {
            float prev = -1f;
            for (float t = 0f; t <= 1.0001f; t += 0.05f)
            {
                float e = VentTransport.Ease(t);
                Assert.GreaterOrEqual(e, prev); prev = e;
            }
            Assert.AreEqual(0f, VentTransport.Ease(0f)); Assert.AreEqual(1f, VentTransport.Ease(1f)); Assert.AreEqual(0.5f, VentTransport.Ease(0.5f), 1e-5f);
            Assert.Less(VentTransport.Ease(0.1f), 0.1f, "slow start");
            Assert.Greater(VentTransport.Ease(0.9f), 0.9f, "slow finish");
        }

        [Test]
        public void Vent_Smooth_PassesThroughItsControlPoints()
        {
            var c = new[] { Vector3.zero, new Vector3(0f, 1f, 1f), new Vector3(0f, 1.5f, 3f), new Vector3(0f, 0.5f, 5f) };
            var p = VentTransport.Smooth(c, 8);
            Assert.AreEqual(3 * 8 + 1, p.Length);
            Assert.That(p[0], Is.EqualTo(c[0])); Assert.That(p[p.Length - 1], Is.EqualTo(c[3]));
            Assert.That(Vector3.Distance(p[8], c[1]), Is.LessThan(1e-4f)); Assert.That(Vector3.Distance(p[16], c[2]), Is.LessThan(1e-4f));
            var two = c.Take(2).ToArray();
            Assert.AreSame(two, VentTransport.Smooth(two, 8), "two points are returned unchanged (nothing to smooth)");
        }

        [Test]
        public void Vent_Timing_IsShortButDeliberate()
        {
            var v = TropicalCourse.Hole7Spec().vent;
            Assert.GreaterOrEqual(v.dwellSeconds, 0.3f, "a visible charge, so the ride is clearly deliberate");
            Assert.LessOrEqual(v.captureBlendSeconds + v.dwellSeconds + v.travelSeconds, 3.5f, "no long dead waits");
            Assert.GreaterOrEqual(v.travelSeconds, 1.0f, "slow enough to follow with the eyes");
            Assert.Greater(v.maxHoldSeconds, v.captureBlendSeconds + v.dwellSeconds + v.travelSeconds + 2f, "the safety timeout never cuts a normal ride");
            Assert.Greater(v.recaptureLockoutSeconds, 0.5f);
            Assert.Greater(v.maxCaptureSpeed, 1.0f); Assert.Less(v.maxCaptureSpeed, 3.5f);
        }

        // ------------------------------------------------------------------ Hole 7 play: the vent

        [UnityTest]
        public IEnumerator Hole7_Vent_CapturesCarriesAndReleases_WithNoExtraStroke()
        {
            var s = TropicalCourse.Hole7Spec();
            using var b = new HoleBed(7);
            var vent = b.Vent;
            Assert.IsNotNull(vent, "the hole contains the vent");
            int returns = 0; b.hole.BallReturned += (h, oob) => returns++;
            yield return Steps(5);
            Vector2 m = s.Mouth;
            b.ball.PlaceAt(b.Surface(m.x, m.y - 1.0f));
            yield return Steps(5);
            b.ball.Strike(new Vector3(0f, 0f, SpeedForDistance(1.2f, b.tuning)));   // a gentle putt that rolls into the mouth

            var seen = new List<VentTransport.Phase>();
            float tCapture = -1f, maxY = 0f, lastZ = -99f; bool monotone = true;
            float end = Time.time + 30f;
            while (Time.time < end && vent.ReleaseCount < 1)
            {
                yield return new WaitForFixedUpdate();
                var st = vent.State;
                if (seen.Count == 0 || seen[seen.Count - 1] != st) seen.Add(st);
                if (st != VentTransport.Phase.Idle && tCapture < 0f) tCapture = Time.time;
                if (st == VentTransport.Phase.Travelling)
                {
                    Assert.IsTrue(b.ball.IsHeld, "the ball is held (kinematic) during the ride");
                    maxY = Mathf.Max(maxY, b.ball.Position.y);
                    if (b.ball.Position.z < lastZ - 0.02f) monotone = false;
                    lastZ = b.ball.Position.z;
                }
            }
            float rideSeconds = Time.time - tCapture;
            Debug.Log($"[Test] hole 7 vent: phases {string.Join(">", seen)}, ride {rideSeconds:F2} s, max height {maxY:F2}, strokes {b.hole.Strokes}");
            Assert.AreEqual(1, vent.CaptureCount, "captured once");
            Assert.AreEqual(1, vent.ReleaseCount, "released once");
            int iSettle = seen.IndexOf(VentTransport.Phase.Settling), iCharge = seen.IndexOf(VentTransport.Phase.Charging), iTravel = seen.IndexOf(VentTransport.Phase.Travelling);
            Assert.That(iSettle >= 0 && iSettle < iCharge && iCharge < iTravel, $"settle, charge, travel in order, saw {string.Join(">", seen)}");
            Assert.That(rideSeconds, Is.LessThan(vent.TotalSeconds + 0.6f), "no excessive waiting");
            Assert.That(rideSeconds, Is.GreaterThan(vent.TotalSeconds - 0.3f));
            Assert.Greater(maxY, 1.0f, "the ball is carried high over the river");
            Assert.IsTrue(monotone, "the ball only ever moves forward along the chute");
            Assert.IsFalse(b.ball.IsHeld, "released");
            Assert.AreEqual(1, b.hole.Strokes, "the putt into the mouth is the only stroke: the ride costs none");
            Assert.AreEqual(0, returns, "and nothing was out of bounds");
            yield return WaitUntil(() => b.ball.IsAtRest || b.hole.IsComplete, 30f);
            yield return Steps(10);
            Vector3 p = b.ball.Position;
            Assert.AreEqual(1, b.hole.Strokes, "settling on Tier 3 costs nothing either");
            Assert.IsTrue(b.ball.InPlay);
            Assert.That(p.y, Is.EqualTo(s.Tier3Height + b.ball.Radius).Within(0.05f), $"the ball rests on Tier 3, at {p}");
            Assert.That(p.z, Is.GreaterThan(s.Tier3FrontZ + 1.5f), "it rolled out after leaving the exit");
            Assert.That(Vector2.Distance(new Vector2(p.x, p.z), s.Cup), Is.GreaterThan(1.0f), "a real putt remains");
            Assert.That(Vector3.Distance(b.hole.LastRestPosition, p), Is.LessThan(0.05f), "the rest on Tier 3 is the new last rest spot");
        }

        [UnityTest]
        public IEnumerator Hole7_Vent_ReleasesAtTheExit_WithRealVelocity_ThenOrdinaryPhysics()
        {
            var s = TropicalCourse.Hole7Spec();
            using var b = new HoleBed(7);
            var vent = b.Vent;
            yield return Steps(5);
            b.ball.PlaceAt(b.Surface(s.Mouth.x, s.Mouth.y));
            yield return WaitUntil(() => vent.ReleaseCount >= 1, 30f);
            Assert.AreEqual(1, vent.ReleaseCount, "a ball resting in the mouth is captured and carried");
            yield return Steps(3);
            Vector3 v = b.ball.Velocity, p = b.ball.Position;
            Debug.Log($"[Test] hole 7 exit: position {p}, velocity {v} ({v.magnitude:F2} m/s)");
            Assert.IsFalse(b.ball.Body.isKinematic, "physics resumes normally");
            Assert.That(v.magnitude, Is.InRange(s.vent.exitSpeed * 0.75f, s.vent.exitSpeed * 1.05f), "leaves with the exit speed");
            Assert.Greater(v.z, v.magnitude * 0.95f, "straight up the green");
            Assert.That(p.z, Is.InRange(s.ExitXZ.y - 0.1f, s.ExitXZ.y + 0.3f), "at the exit, not somewhere else");
            Assert.AreEqual(0, b.hole.Strokes, "no stroke was ever played: the ride is not a stroke");
            float z0 = p.z;
            yield return WaitUntil(() => b.ball.IsAtRest, 30f);
            float rolled = b.ball.Position.z - z0;
            Assert.That(rolled, Is.InRange(1.8f, 3.6f), $"it rolled out {rolled:F2} m on Tier 3");
            Assert.That(b.ball.Position.z, Is.LessThan(s.Tier3EndZ - 0.3f));
            Assert.AreEqual(0, b.hole.Strokes);
        }

        [UnityTest]
        public IEnumerator Hole7_Vent_AFastBallIsNotCaptured_AndIsNeverTrapped()
        {
            var s = TropicalCourse.Hole7Spec();
            using var b = new HoleBed(7);
            var vent = b.Vent;
            yield return Steps(5);
            b.ball.PlaceAt(b.Surface(s.Mouth.x, s.Mouth.y - 1.2f));
            yield return Steps(5);
            b.ball.Strike(new Vector3(0f, 0f, 5.0f));
            yield return WaitUntil(() => vent.RejectedCount >= 1 || vent.CaptureCount >= 1, 3f);
            Assert.GreaterOrEqual(vent.RejectedCount, 1, "a fast ball over the mouth is rejected");
            Assert.AreEqual(0, vent.CaptureCount, "it is not captured on the fast pass");
            Assert.IsFalse(b.ball.IsHeld);
            // Whatever happens next (a rebound off the back wall for a slower pass, or a rest on the pad) the ball is never lost or trapped.
            yield return WaitUntil(() => b.ball.IsAtRest && !b.ball.IsHeld && vent.State == VentTransport.Phase.Idle, 40f);
            Debug.Log($"[Test] hole 7 fast ball: captures {vent.CaptureCount}, rejected {vent.RejectedCount}, ball {b.ball.Position}, strokes {b.hole.Strokes}");
            Assert.IsTrue(b.ball.InPlay);
            Assert.IsFalse(b.ball.IsHeld);
            Assert.AreEqual(1, b.hole.Strokes, "a fast putt costs no penalty: the pad's back wall keeps it on the course");
        }

        [UnityTest]
        public IEnumerator Hole7_Vent_ResetDuringTheRide_CancelsCleanly_AndTheNextAttemptIsCarried()
        {
            var s = TropicalCourse.Hole7Spec();
            using var b = new HoleBed(7);
            var vent = b.Vent;
            yield return Steps(5);
            Vector3 start = b.Surface(s.Mouth.x, s.Mouth.y - 1.0f);
            b.ball.PlaceAt(start);
            yield return Steps(5);
            b.ball.Strike(new Vector3(0f, 0f, SpeedForDistance(1.2f, b.tuning)));
            yield return WaitUntil(() => vent.State == VentTransport.Phase.Travelling, 20f);
            Assert.AreEqual(VentTransport.Phase.Travelling, vent.State);
            Assert.IsTrue(b.ball.IsHeld);
            b.hole.RequestReset();                       // the player resets mid-ride
            yield return Steps(4);
            Assert.IsFalse(b.ball.IsHeld, "the reset cancelled the hold");
            Assert.IsFalse(b.ball.Body.isKinematic, "and the ball is a normal ball again");
            Assert.AreEqual(VentTransport.Phase.Idle, vent.State, "the vent forgot the ball");
            Assert.AreEqual(0, vent.ReleaseCount, "the abandoned ride never released");
            Assert.That(Vector3.Distance(b.ball.Position, b.hole.LastRestPosition), Is.LessThan(0.1f), "back where the shot was played from");
            Assert.AreEqual(1, b.hole.Strokes, "the shot still counts, no extra penalty");
            // The next attempt works: putt in again and ride.
            yield return Steps(10);
            b.ball.Strike(new Vector3(0f, 0f, SpeedForDistance(1.2f, b.tuning)));
            yield return WaitUntil(() => vent.ReleaseCount >= 1, 30f);
            Assert.AreEqual(1, vent.ReleaseCount);
            Assert.AreEqual(2, vent.CaptureCount);
            Assert.AreEqual(2, b.hole.Strokes);
        }

        [UnityTest]
        public IEnumerator Hole7_Vent_SafetyTimeout_ReleasesABallThatWouldBeHeldTooLong()
        {
            var s = TropicalCourse.Hole7Spec();
            using var b = new HoleBed(7);
            var vent = b.Vent;
            vent.Spec.maxHoldSeconds = 0.8f;
            yield return Steps(5);
            b.ball.PlaceAt(b.Surface(s.Mouth.x, s.Mouth.y));
            yield return WaitUntil(() => vent.CaptureCount >= 1, 5f);
            Assert.AreEqual(1, vent.CaptureCount);
            yield return WaitUntil(() => vent.ReleaseCount >= 1, 5f);
            Assert.AreEqual(1, vent.ReleaseCount, "released by the timeout, still at the exit");
            Assert.IsFalse(b.ball.IsHeld);
            Assert.That(b.ball.Position.z, Is.GreaterThan(s.Tier3FrontZ - 0.2f), "the timeout releases at the exit, not mid-air over the river");
        }

        [UnityTest]
        public IEnumerator Hole7_Vent_AfterTheExit_TheIntakeIgnoresTheBallBriefly()
        {
            var s = TropicalCourse.Hole7Spec();
            using var b = new HoleBed(7);
            var vent = b.Vent;
            yield return Steps(5);
            b.ball.PlaceAt(b.Surface(s.Mouth.x, s.Mouth.y));
            yield return WaitUntil(() => vent.ReleaseCount >= 1, 30f);
            b.ball.PlaceAt(b.Surface(s.Mouth.x, s.Mouth.y));     // straight back to the mouth
            yield return Steps(30);                              // 0.25 s of game time
            Assert.AreEqual(1, vent.CaptureCount, "locked out right after an exit");
            yield return WaitUntil(() => vent.CaptureCount >= 2, s.vent.recaptureLockoutSeconds + 2f);
            Assert.AreEqual(2, vent.CaptureCount, "and captured again once the lockout is over");
        }

        // ------------------------------------------------------------------ Hole 7 play: tiers and lava

        [UnityTest]
        public IEnumerator Hole7_TeePutt_BanksEastAlongTheCrossLane_WithoutPenalty()
        {
            var s = TropicalCourse.Hole7Spec();
            var table = new System.Text.StringBuilder("[Test] hole 7 tee putt (strike m/s -> rest)\n");
            foreach (float v in new[] { 1.8f, 2.4f, 3.0f })
            {
                using var b = new HoleBed(7);
                yield return Steps(5);
                b.ball.Strike(new Vector3(0f, 0f, v));
                yield return Steps(3);
                yield return WaitUntil(() => b.ball.IsAtRest || b.hole.Strokes > 1, 20f);
                yield return Steps(15);
                Vector3 p = b.ball.Position;
                table.AppendLine($"  {v:F1} -> ({p.x:F2}, {p.y:F2}, {p.z:F2}) strokes {b.hole.Strokes}");
                Assert.AreEqual(1, b.hole.Strokes, $"{v} m/s: the tee putt must never cost a penalty");
                Assert.IsTrue(b.ball.InPlay);
                Assert.That(p.y, Is.LessThan(0.08f), "still on Tier 1");
                if (v >= 2.4f) Assert.Greater(p.x, 0.4f, $"{v} m/s: the corner bank sent the ball east, at {p}");
            }
            Debug.Log(table.ToString());
        }

        [UnityTest]
        public IEnumerator Hole7_Causeway_HoldsABall_AndAFirmPuttClimbsToTierTwo()
        {
            var s = TropicalCourse.Hole7Spec();
            using (var hold = new HoleBed(7))
            {
                yield return Steps(5);
                var start = hold.Surface(s.Mouth.x, s.CrossEndZ + s.CausewayRun * 0.5f);
                hold.ball.PlaceAt(start);
                yield return WaitUntil(() => false, 2.0f);
                Assert.That(Vector3.Distance(hold.ball.Position, start), Is.LessThan(0.08f), "a ball resting on the causeway stays there (under the rest-hold slope)");
                Assert.IsTrue(hold.ball.InPlay);
            }
            var table = new System.Text.StringBuilder("[Test] hole 7 causeway from its foot (strike m/s -> rest)\n");
            int reached = 0;
            foreach (float v in new[] { 2.4f, 3.2f, 3.6f, 4.0f })
            {
                using var b = new HoleBed(7);
                var vent = b.Vent;
                yield return Steps(5);
                b.ball.PlaceAt(b.Surface(s.Mouth.x, s.CrossEndZ + 0.3f));
                yield return Steps(5);
                b.ball.Strike(new Vector3(0f, 0f, v));
                yield return Steps(3);
                yield return WaitUntil(() => (b.ball.IsAtRest && !b.ball.IsHeld) || b.hole.Strokes > 1, 40f);
                yield return Steps(15);
                Vector3 p = b.ball.Position;
                table.AppendLine($"  {v:F1} -> ({p.x:F2}, {p.y:F2}, {p.z:F2}) captures {vent.CaptureCount} strokes {b.hole.Strokes}");
                Assert.AreEqual(1, b.hole.Strokes, $"{v} m/s up the causeway cost a penalty");
                if (p.y > s.Tier2Height - 0.05f) reached++;
            }
            Debug.Log(table.ToString());
            Assert.GreaterOrEqual(reached, 2, "a firm putt climbs to Tier 2 (or rides on to Tier 3): a usable window");
        }

        [UnityTest]
        public IEnumerator Hole7_LavaLake_CostsOneStroke_AndReturnsTheBallToItsLastRestSpot()
        {
            var s = TropicalCourse.Hole7Spec();
            using var b = new HoleBed(7);
            int returns = 0; bool oob = false;
            b.hole.BallReturned += (h, o) => { returns++; oob = o; };
            yield return Steps(5);
            Vector3 rest = b.hole.LastRestPosition;
            b.ball.PlaceAt(b.Surface(s.CausewayWestX + 0.3f, s.CrossEndZ + 1.2f));
            yield return Steps(5);
            b.ball.Strike(new Vector3(-1.4f, 0f, 0f));       // off the causeway's open west edge
            yield return WaitUntil(() => returns >= 1, 12f);
            yield return Steps(5);
            Assert.AreEqual(1, returns); Assert.IsTrue(oob, "the lava is out of bounds");
            Assert.AreEqual(2, b.hole.Strokes, "the stroke plus one penalty stroke");
            Assert.IsTrue(b.ball.InPlay);
            Assert.That(Vector3.Distance(b.ball.Position, b.hole.LastRestPosition), Is.LessThan(0.05f), "back at the last rest spot, not stranded in the lava");
            Assert.That(b.hole.LastRestPosition, Is.EqualTo(rest), "(the tee: nothing had come to rest since)");
        }

        [UnityTest]
        public IEnumerator Hole7_TierTwoCorner_AndTierThreeWestEdge_AreLava()
        {
            var s = TropicalCourse.Hole7Spec();
            using (var b = new HoleBed(7))
            {
                int returns = 0; b.hole.BallReturned += (h, o) => returns++;
                yield return Steps(5);
                b.ball.PlaceAt(b.Surface(s.Tier2WestX + 0.5f, s.RampEndZ + 0.4f));
                yield return Steps(5);
                b.ball.Strike(new Vector3(0f, 0f, -1.2f));       // south, off Tier 2's open south-west edge
                yield return WaitUntil(() => returns >= 1, 12f);
                Assert.AreEqual(1, returns, "Tier 2's south-west edge is lava");
                Assert.AreEqual(2, b.hole.Strokes);
            }
            using (var b = new HoleBed(7))
            {
                int returns = 0; b.hole.BallReturned += (h, o) => returns++;
                yield return Steps(5);
                var w = s.Tier3WestEdge();
                b.ball.PlaceAt(b.Surface(s.Tier3WestX + 0.3f, (w.z0 + w.z1) * 0.5f));
                yield return Steps(5);
                b.ball.Strike(new Vector3(-1.4f, 0f, 0f));
                yield return WaitUntil(() => returns >= 1, 12f);
                Assert.AreEqual(1, returns, "Tier 3's west edge is lava");
                Assert.AreEqual(2, b.hole.Strokes);
                Assert.IsTrue(b.ball.InPlay);
            }
        }

        [UnityTest]
        public IEnumerator Hole7_CompletionPath_TeeToCup_ByRealPutting_AndTheVent()
        {
            var s = TropicalCourse.Hole7Spec();
            using var b = new HoleBed(7);
            var vent = b.Vent;
            int returns = 0; b.hole.BallReturned += (h, o) => returns++;
            yield return Steps(5);
            int strokes = 0;
            // 1. The tee putt banks east.
            b.ball.Strike(new Vector3(0f, 0f, 2.6f)); strokes++;
            yield return Steps(3);
            yield return WaitUntil(() => b.ball.IsAtRest, 20f);
            yield return Steps(10);
            Assert.Greater(b.ball.Position.x, 0.3f, "the bank turned the tee putt east");
            // 2. Up the causeway (from its foot) with a firm putt, until the ball is on Tier 2 or has ridden to Tier 3.
            bool up = false;
            for (int attempt = 0; attempt < 3 && !up; attempt++)
            {
                b.ball.PlaceAt(b.Surface(s.Mouth.x, s.CrossEndZ + 0.3f));
                yield return Steps(5);
                b.ball.Strike(new Vector3(0f, 0f, new[] { 3.4f, 3.7f, 3.1f }[attempt])); strokes++;
                yield return Steps(3);
                yield return WaitUntil(() => (b.ball.IsAtRest && !b.ball.IsHeld) || b.hole.Strokes > strokes, 40f);
                yield return Steps(15);
                up = b.ball.Position.y > s.Tier2Height - 0.05f;
            }
            Assert.IsTrue(up, $"the causeway was not climbed, ball at {b.ball.Position}");
            // 3. On Tier 2 (not yet carried): a gentle putt into the mouth.
            if (vent.ReleaseCount == 0)
            {
                b.ball.PlaceAt(b.Surface(s.Mouth.x, s.Mouth.y - 1.0f));
                yield return Steps(5);
                b.ball.Strike(new Vector3(0f, 0f, SpeedForDistance(1.2f, b.tuning))); strokes++;
                yield return WaitUntil(() => vent.ReleaseCount >= 1, 30f);
            }
            Assert.GreaterOrEqual(vent.ReleaseCount, 1, "the vent carried the ball");
            yield return WaitUntil(() => b.ball.IsAtRest && !b.ball.IsHeld, 30f);
            yield return Steps(10);
            Assert.That(b.ball.Position.y, Is.EqualTo(s.Tier3Height + b.ball.Radius).Within(0.05f), $"on Tier 3, at {b.ball.Position}");
            // 4. The final green: aimed putts at the cup.
            for (int i = 0; i < 5 && !b.hole.IsComplete; i++)
            {
                Vector3 cup = b.hole.Cup.transform.position, d = cup - b.ball.Position; d.y = 0f;
                b.ball.Strike(d.normalized * Mathf.Clamp(SpeedForDistance(d.magnitude + 0.2f, b.tuning), 0.6f, 3.2f)); strokes++;
                yield return Steps(3);
                yield return WaitUntil(() => b.hole.IsComplete || b.ball.IsAtRest || b.hole.Strokes > strokes, 15f);
                yield return Steps(10);
                if (b.hole.Strokes > strokes) break;          // a lava edge: stop and report
            }
            Debug.Log($"[Test] hole 7 completion: strokes played {strokes}, hole strokes {b.hole.Strokes}, complete {b.hole.IsComplete}, returns {returns}");
            Assert.IsTrue(b.hole.IsComplete, $"the hole was not completed, ball at {b.ball.Position}");
            Assert.AreEqual(strokes, b.hole.Strokes, "no penalties and no strokes added by the vent");
        }

        [UnityTest]
        public IEnumerator Hole7_Cup_CanBeHoled_FromTierThree()
        {
            var s = TropicalCourse.Hole7Spec();
            using var b = new HoleBed(7);
            yield return Steps(5);
            var cup = b.hole.Cup.transform.position;
            b.ball.PlaceAt(new Vector3(cup.x, cup.y + b.ball.Radius + 0.002f, cup.z - 0.4f));
            yield return Steps(5);
            Vector3 d = cup - b.ball.Position; d.y = 0f;
            b.ball.Strike(d.normalized * 1.1f);
            yield return WaitUntil(() => b.hole.IsComplete, 8f);
            Assert.IsTrue(b.hole.IsComplete, $"not holed, ball at {b.ball.Position}, cup {cup}");
        }

        // ------------------------------------------------------------------ the bowl's gate notch (regression + new)

        [Test]
        public void BowlSpec_InNotch_DefaultsToNone_AndMatchesTheEntryNotchAndGate()
        {
            var plain = new RouletteBowlSpec();
            for (float a = -180f; a <= 180f; a += 15f) Assert.IsFalse(plain.InNotch(a), "a plain bowl has a closed wall all the way round");
            var entryOnly = new RouletteBowlSpec { entryAngleDegrees = -110f, entryHalfWidthDegrees = 26f };
            Assert.IsTrue(entryOnly.InNotch(-110f)); Assert.IsTrue(entryOnly.InNotch(-90f)); Assert.IsFalse(entryOnly.InNotch(-60f)); Assert.IsFalse(entryOnly.InNotch(150f), "the gate is off while its width is 0");
            var both = new RouletteBowlSpec { entryAngleDegrees = -110f, entryHalfWidthDegrees = 26f, gateAngleDegrees = 157f, gateHalfWidthDegrees = 15f };
            Assert.IsTrue(both.InNotch(157f)); Assert.IsTrue(both.InNotch(170f)); Assert.IsFalse(both.InNotch(120f));
            Assert.IsTrue(both.InNotch(-100f));
            Assert.IsTrue(both.InNotch(-203f), "angles wrap round (-203 = 157)");
            Assert.AreEqual(0.02f, RouletteBowlSpec.NotchCurbRise, 1e-6f, "the curb height the proven jump bowl was tuned with");
        }

        [Test]
        public void Bowl_Rebuild_MakesCurbsOnlyInTheNotches()
        {
            var tuning = ScriptableObject.CreateInstance<GolfTuning>();
            try
            {
                foreach (bool withGate in new[] { false, true })
                {
                    var spec = new RouletteBowlSpec { entryAngleDegrees = -110f, entryHalfWidthDegrees = 26f, segments = 96 };
                    if (withGate) { spec.gateAngleDegrees = 157f; spec.gateHalfWidthDegrees = 15f; }
                    var go = new GameObject("BowlTest");
                    try
                    {
                        var bowl = go.AddComponent<RouletteBowl>();
                        bowl.Configure(spec, tuning, new ProvingMaterials());
                        bowl.Rebuild();
                        var curbs = go.GetComponentsInChildren<Transform>().Count(t => t.name == "EntryCurb");
                        var segs = go.GetComponentsInChildren<Transform>().Count(t => t.name == "WallSegment");
                        int expected = 0;
                        for (int i = 0; i < 96; i++) if (spec.InNotch(360f * i / 96f)) expected++;
                        Assert.AreEqual(expected, curbs, withGate ? "curbs in both notches" : "curbs only in the entry notch");
                        Assert.AreEqual(96, curbs + segs, "every wall position is either a wall or a curb");
                        Assert.Greater(curbs, withGate ? 20 : 10);
                        Assert.IsNotNull(bowl.Cup);
                    }
                    finally { Object.DestroyImmediate(go); }
                }
            }
            finally { Object.DestroyImmediate(tuning); }
        }

        // ------------------------------------------------------------------ Hole 8 layout

        [Test]
        public void Hole8_Bowl_HasALowInnerRing_ThatIsOpenOnTheGateSide()
        {
            var b = TropicalCourse.Hole8Spec().Jump.bowl;
            Assert.Greater(b.ringHeight, 0f, "an inner ring round the cup (Andrew: a blocker to get over)");
            Assert.Less(b.ringHeight, 0.02f, "lower than a ball's radius, so a firm putt can climb it");
            Assert.Greater(b.ringOpeningHalfWidthDegrees, 30f, "the gate side is open");
            Assert.AreEqual(b.gateAngleDegrees, b.ringOpeningAngleDegrees, 0.01f, "the opening is centred on the gate");
            Assert.Greater(b.ringRadius, b.apronRadius + 0.3f, "the ring stands well out from the cup");
            Assert.Less(b.ringRadius, b.ShelfInnerRadius, "the ring is on the cone, inside the shelf");
        }

        [Test]
        public void Hole8_Definition_IsAParThreeWithTheJumpAndAGate()
        {
            var d = Def(8);
            Assert.IsNotNull(d);
            Assert.AreEqual("Caldera Run", d.name);
            Assert.AreEqual(3, d.par);
            Assert.AreEqual(TropicalCourse.VolcanicCluster, d.cluster);
            var s = TropicalCourse.Hole8Spec();
            Assert.AreEqual("", s.Validate());
            Assert.IsFalse(d.layout.cup.HasValue, "the cup is the bowl's, built by the hook");
            Assert.IsNotNull(d.buildExtras);
            Assert.GreaterOrEqual(d.extraAreas.Count, 2, "the bowl and the pit under the gap");
            Assert.That(d.tee, Is.EqualTo(s.Tee));
            Assert.That(s.Jump.bowl.gateHalfWidthDegrees, Is.GreaterThan(5f), "the bowl has a gate");
            Assert.That(s.Jump.bowl.entryHalfWidthDegrees, Is.EqualTo(0f), "(the jump notch is set when the bowl is built for the hole)");
            Assert.That(s.Jump.BowlForHole().entryHalfWidthDegrees, Is.GreaterThan(5f), "and a jump notch");
            Assert.That(s.BowlRadius, Is.GreaterThanOrEqualTo(1.5f), "a modest bowl (Andrew asked for a smaller circle)");
            Assert.That(d.layout.openEdges.Count, Is.GreaterThanOrEqualTo(2), "the jump lip and the gate lane's end are open");
        }

        [Test]
        public void Hole8_GateGeometry_IsFlushTangentialAndSeparateFromTheJumpNotch()
        {
            var s = TropicalCourse.Hole8Spec();
            Vector2 bc = s.BowlCentre;
            float r = s.GateArcRadius;
            Assert.That(s.GateLaneEndX, Is.LessThan(s.GateArcX(s.GateLaneZ0) - 0.05f));
            Assert.That(s.GateLaneEndX, Is.LessThan(s.GateArcX(s.GateLaneZ1) - 0.05f), "the lane stops short of the rim everywhere across its width");
            Assert.That(s.GateLaneEndX, Is.GreaterThan(s.ColumnEastX + 0.5f), "and is long enough to roll along");
            Assert.That(s.GateLaneEndX * 10f, Is.EqualTo(Mathf.Round(s.GateLaneEndX * 10f)).Within(1e-3f), "on the green's grid");
            // The arc points really lie on the notch curb's radius.
            foreach (float z in new[] { s.GateLaneZ0, (s.GateLaneZ0 + s.GateLaneZ1) * 0.5f, s.GateLaneZ1 })
                Assert.That(Vector2.Distance(new Vector2(s.GateArcX(z), z), bc), Is.EqualTo(r).Within(1e-3f));
            // Level with the curb's top (2 cm above the rim shelf), i.e. the ball rolls on, then steps down onto the shelf.
            var j = s.Jump;
            Assert.That(s.GateLevel, Is.EqualTo(j.BowlOrigin.y + j.bowl.Height(j.bowl.radius) + 0.02f).Within(1e-4f));
            // The gate is on the side of the bowl away from the jump's landing.
            var (angle, half) = s.GateArc();
            float jumpAngle = j.BowlForHole().entryAngleDegrees;
            Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(angle, jumpAngle)), half + j.entryHalfAngle + 10f, "the two notches do not overlap");
            // Every point of the lane's width falls inside the notch.
            foreach (float z in new[] { s.GateLaneZ0, s.GateLaneZ1 })
            {
                float a = Mathf.Atan2(z - bc.y, s.GateArcX(z) - bc.x) * Mathf.Rad2Deg;
                Assert.Less(Mathf.Abs(Mathf.DeltaAngle(a, angle)), half, "the lane's edges are inside the gate notch");
            }
            // Offset entry: the lane's centre line is well off the bowl's centre (tangential, like the jump), and it curves the same way as the jump (clockwise from above).
            float zc = (s.GateLaneZ0 + s.GateLaneZ1) * 0.5f;
            float v = zc - bc.y;
            Assert.Greater(v / s.BowlRadius, 0.3f, "an offset entry: the ball does not cross the bowl's middle");
            Assert.Greater(v, 0f);
            Vector2 gateIn = Vector2.right;                                   // the gate lane runs +x
            Vector2 jumpIn = Vector2.up;                                      // the jump runs +z
            float gateTurn = gateIn.x * (bc.y - zc) - gateIn.y * (bc.x - s.GateArcX(zc));
            float jumpTurn = jumpIn.x * (bc.y - (s.jump.ramp.LipZ + s.jump.ramp.Gap)) - jumpIn.y * (bc.x - 0f);
            Assert.AreEqual(Mathf.Sign(gateTurn), Mathf.Sign(jumpTurn), "both entries orbit the same way round");
        }

        [Test]
        public void Hole8_RouteHeights_ClimbGentlyToTheGateLevel()
        {
            var s = TropicalCourse.Hole8Spec();
            var l = Def(8).layout;
            float gl = s.GateLevel;
            Assert.That(gl, Is.InRange(0.05f, 0.25f), "a short, honest climb");
            Assert.That(l.Height(-2.0f, 1.0f), Is.EqualTo(0f).Within(1e-4f), "the crater floor is level with the tee");
            Assert.That(l.Height(0f, 1f), Is.EqualTo(0f).Within(1e-4f), "tee lane");
            float cx = (s.WestX + s.ColumnEastX) * 0.5f, prev = -1f;
            for (float z = s.routeZ1; z <= s.GateLaneZ1; z += 0.1f)
            {
                float h = l.Height(cx, z);
                Assert.GreaterOrEqual(h, prev - 1e-4f, $"the climb only rises (z {z:F1})");
                Assert.LessOrEqual(h - Mathf.Max(prev, 0f), 0.1f * 0.0785f, $"never steeper than the rest-hold slope (z {z:F1})");
                prev = h;
            }
            Assert.That(l.Height(cx, s.GateLaneZ0 + 0.3f), Is.EqualTo(gl).Within(1e-4f), "the top of the climb is the gate level");
            Assert.That(l.Height(s.ColumnEastX + 0.4f, s.GateLaneZ0 + 0.3f), Is.EqualTo(gl).Within(1e-4f), "the gate lane is flat at the gate level");
            Assert.That(l.Height(s.GateLaneEndX - 0.1f, s.GateLaneZ1 - 0.1f), Is.EqualTo(gl).Within(1e-4f), "right up to its end");
            Assert.That(gl / (s.climbEndZ - s.climbStartZ), Is.LessThan(0.0785f), "a ball can rest on the climb");
            // The tee lane is the proven jump lane: flat run-up, then the ramp.
            var j = s.Jump;
            Assert.That(l.Height(0f, j.ramp.LipZ), Is.EqualTo(j.ramp.LipHeight).Within(1e-3f));
        }

        [Test]
        public void Hole8_Banks_TurnTheBallNorthUpTheClimb_ThenEastIntoTheGateLane()
        {
            var s = TropicalCourse.Hole8Spec();
            var south = s.WallPlan().First(w => w.name == "CraterBankSouth");
            var n1 = BankWall.FaceNormal(new Vector2(south.a.x, south.a.z), new Vector2(south.b.x, south.b.z), new Vector2(-1.5f, 1.0f));
            var o1 = BankWall.Reflect(Vector2.left, n1);
            Assert.Greater(o1.y, Mathf.Abs(o1.x) * 3f, "a ball putted west from the tee is turned up the climb");
            var north = s.WallPlan().First(w => w.name == "CraterBankNorth");
            var n2 = BankWall.FaceNormal(new Vector2(north.a.x, north.a.z), new Vector2(north.b.x, north.b.z), new Vector2(-2.9f, 4.0f));
            var o2 = BankWall.Reflect(Vector2.up, n2);
            Assert.Greater(o2.x, Mathf.Abs(o2.y) * 3f, "a ball coming up the climb is turned east into the gate lane");
            foreach (var w in s.WallPlan())
            {
                Assert.That(w.a.x, Is.InRange(s.WestX - 0.01f, s.ColumnEastX + 0.01f), w.name); Assert.That(w.b.x, Is.InRange(s.WestX - 0.01f, s.ColumnEastX + 0.01f), w.name);
            }
        }

        [Test]
        public void Hole8_ApproachMesh_IsFlat_FlushWithTheLane_AndMeetsTheBowlOnItsArc()
        {
            var s = TropicalCourse.Hole8Spec();
            var mesh = s.BuildApproachMesh();
            try
            {
                var v = mesh.vertices; var t = mesh.triangles;
                Assert.GreaterOrEqual(v.Length, 20); Assert.AreEqual(0, t.Length % 3);
                foreach (var p in v) Assert.That(p.y, Is.EqualTo(s.GateLevel).Within(1e-4f), "flat at the gate level");
                Assert.That(v.Min(p => p.x), Is.EqualTo(s.GateLaneEndX).Within(1e-4f), "starts exactly at the lane's end");
                Assert.That(v.Min(p => p.z), Is.EqualTo(s.GateLaneZ0).Within(1e-4f)); Assert.That(v.Max(p => p.z), Is.EqualTo(s.GateLaneZ1).Within(1e-4f), "as wide as the lane");
                foreach (var p in v.Where(p => p.x > s.GateLaneEndX + 1e-3f))
                    Assert.That(Vector2.Distance(new Vector2(p.x, p.z), s.BowlCentre), Is.EqualTo(s.GateArcRadius).Within(1e-3f), "the far edge follows the bowl's curb");
                for (int i = 0; i < t.Length; i += 3)
                    Assert.Greater(Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]).y, 0f, "every triangle faces up");
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void Hole8_GateRails_RunAlongTheApproachEdges_AndEndOnTheBowlsCurb()
        {
            var s = TropicalCourse.Hole8Spec();
            var rails = s.GateRails();
            Assert.AreEqual(2, rails.Count);
            foreach (var (name, a, b) in rails)
            {
                Assert.That(a.y, Is.EqualTo(s.GateLevel).Within(1e-4f), name); Assert.That(b.y, Is.EqualTo(s.GateLevel).Within(1e-4f), name);
                Assert.That(a.z, Is.EqualTo(b.z).Within(1e-4f), $"{name} runs straight along the lane");
                Assert.That(a.x, Is.LessThan(s.GateLaneEndX), $"{name} overlaps the lane's own rail");
                Assert.That(Vector2.Distance(new Vector2(b.x - 0.02f, b.z), s.BowlCentre), Is.EqualTo(s.GateArcRadius).Within(2e-3f), $"{name} ends on the curb's arc");
            }
            Assert.Less(rails[0].a.z, s.GateLaneZ0, "south rail outside the lane"); Assert.Greater(rails[1].a.z, s.GateLaneZ1, "north rail outside the lane");
        }

        [Test]
        public void Hole8_ExtraAreas_CoverTheBowlAndThePit()
        {
            var s = TropicalCourse.Hole8Spec();
            var areas = s.ExtraAreas();
            Assert.IsTrue(areas.Any(a => a.Contains(s.BowlCentre)), "the bowl's ground");
            Vector2 pit = new Vector2(0f, s.jump.ramp.LipZ + s.jump.ramp.Gap * 0.5f);
            Assert.IsTrue(areas.Any(a => a.Contains(pit)), "the pit under the gap");
            Assert.AreEqual("", s.Validate());
            // A spec with the gate on top of the jump notch is rejected.
            var bad = TropicalCourse.Hole8Spec(); bad.gateOffset = 0.3f;
            Assert.AreNotEqual("", bad.Validate(), "a gate lane that crosses the bowl's middle is not a tangential entry");
        }

        // ------------------------------------------------------------------ Hole 8 play: the caldera route

        [UnityTest]
        public IEnumerator Hole8_TeePuttWest_IsBankedUpTheClimb_WithoutPenalty()
        {
            var s = TropicalCourse.Hole8Spec();
            var table = new System.Text.StringBuilder("[Test] hole 8 tee putt west (strike m/s -> rest)\n");
            foreach (float v in new[] { 2.4f, 3.4f, 4.2f })
            {
                using var b = new HoleBed(8);
                yield return Steps(5);
                b.ball.Strike(new Vector3(-v, 0f, 0f));
                yield return Steps(3);
                yield return WaitUntil(() => b.ball.IsAtRest || b.hole.Strokes > 1, 25f);
                yield return Steps(15);
                Vector3 p = b.ball.Position;
                table.AppendLine($"  {v:F1} -> ({p.x:F2}, {p.y:F2}, {p.z:F2}) strokes {b.hole.Strokes}");
                Assert.AreEqual(1, b.hole.Strokes, $"{v} m/s: the route has no penalty for a plain putt");
                Assert.IsTrue(b.ball.InPlay);
                if (Mathf.Approximately(v, 3.4f)) { Assert.Less(p.x, s.ColumnEastX + 0.1f, $"{v} m/s: in the column, at {p}"); Assert.Greater(p.z, 2.4f, "the bank turned it north"); }
            }
            Debug.Log(table.ToString());
        }

        [UnityTest]
        public IEnumerator Hole8_ClimbHoldsABall_SoAShortPuttCanBeStruckAgain()
        {
            var s = TropicalCourse.Hole8Spec();
            using var b = new HoleBed(8);
            yield return Steps(5);
            var start = b.Surface((s.WestX + s.ColumnEastX) * 0.5f, (s.climbStartZ + s.climbEndZ) * 0.5f);
            b.ball.PlaceAt(start);
            yield return WaitUntil(() => false, 2.0f);
            Assert.That(Vector3.Distance(b.ball.Position, start), Is.LessThan(0.08f), "a ball resting on the climb stays there");
        }

        [UnityTest]
        public IEnumerator Hole8_GateLane_RollsTheBallIntoTheBowl_AndTheBowlKeepsIt()
        {
            var s = TropicalCourse.Hole8Spec();
            float R = s.BowlRadius;
            var table = new System.Text.StringBuilder("[Test] hole 8 rolling into the bowl through the gate (strike m/s from the lane -> outcome)\n");
            int inBowl = 0;
            foreach (float v in new[] { 1.0f, 1.5f, 2.0f, 2.5f, 3.0f, 3.5f, 4.0f })
            {
                using var b = new HoleBed(8);
                int returns = 0; b.hole.BallReturned += (h, o) => returns++;
                yield return Steps(5);
                float zc = (s.GateLaneZ0 + s.GateLaneZ1) * 0.5f;
                b.ball.PlaceAt(b.Surface(s.GateLaneEndX - 0.6f, zc));
                yield return Steps(5);
                b.ball.Strike(new Vector3(v, 0f, 0f));
                yield return Steps(3);
                yield return WaitUntil(() => b.ball.IsAtRest || b.hole.IsComplete || b.hole.Strokes > 1, 40f);
                yield return Steps(15);
                Vector3 p = b.ball.Position;
                float r = Vector2.Distance(new Vector2(p.x, p.z), s.BowlCentre);
                bool inb = b.hole.IsComplete || InBowl(p, s);
                string outcome = b.hole.IsComplete ? "HOLED" : inb ? $"in the bowl, r {r:F2}" : "outside the bowl (on the approach)";
                table.AppendLine($"  {v:F1} -> {outcome} (strokes {b.hole.Strokes})");
                Assert.AreEqual(1, b.hole.Strokes, $"{v} m/s: rolling in through the gate costs no penalty");
                Assert.AreEqual(0, returns);
                Assert.IsTrue(b.ball.InPlay || b.hole.IsComplete);
                if (inb) inBowl++;
                if (v >= 1.5f) Assert.IsTrue(inb, $"{v} m/s: the ball must end in the bowl, ended at {p}");
            }
            Debug.Log(table.ToString());
            Assert.GreaterOrEqual(inBowl, 6, "the gate is a reliable entry across a wide band of speeds");
        }

        [UnityTest]
        public IEnumerator Hole8_Bowl_HasNoInvisibleForce_ABallOnTheShelfStaysPut()
        {
            var s = TropicalCourse.Hole8Spec();
            using var b = new HoleBed(8);
            yield return Steps(5);
            var j = s.Jump;
            float rr = j.bowl.radius - j.bowl.shelfWidth * 0.5f;
            var (angle, half) = s.GateArc();
            float a = (angle + 180f) * Mathf.Deg2Rad;                  // the far side from the gate
            Vector2 c = s.BowlCentre;
            var start = new Vector3(c.x + Mathf.Cos(a) * rr, j.BowlOrigin.y + j.bowl.Height(rr) + b.ball.Radius + 0.002f, c.y + Mathf.Sin(a) * rr);
            b.ball.PlaceAt(start);
            yield return WaitUntil(() => false, 3.0f);
            Assert.That(Vector3.Distance(b.ball.Position, start), Is.LessThan(0.06f), "on the gentle rim shelf nothing pulls or steers the ball: it stays where it is put");
            Assert.IsTrue(b.ball.InPlay);
            Assert.AreEqual(0, b.hole.Strokes);
        }

        [UnityTest]
        public IEnumerator Hole8_Bowl_ABallStruckBackOutThroughTheGate_DoesNotLeave()
        {
            var s = TropicalCourse.Hole8Spec();
            using var b = new HoleBed(8);
            int returns = 0; b.hole.BallReturned += (h, o) => returns++;
            yield return Steps(5);
            var j = s.Jump;
            float rr = j.bowl.radius - j.bowl.shelfWidth * 0.5f;
            var (angle, half) = s.GateArc();
            float a = angle * Mathf.Deg2Rad;
            Vector2 c = s.BowlCentre;
            var start = new Vector3(c.x + Mathf.Cos(a) * rr, j.BowlOrigin.y + j.bowl.Height(rr) + b.ball.Radius + 0.002f, c.y + Mathf.Sin(a) * rr);
            b.ball.PlaceAt(start);
            yield return Steps(5);
            b.ball.Strike(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.5f);     // straight out at the gate
            yield return WaitUntil(() => b.ball.IsAtRest || b.hole.IsComplete || b.hole.Strokes > 1, 40f);
            yield return Steps(15);
            Vector3 p = b.ball.Position;
            float r = Vector2.Distance(new Vector2(p.x, p.z), c);
            Assert.AreEqual(0, returns, "no out-of-bounds return");
            Assert.IsTrue(b.hole.IsComplete || InBowl(p, s), $"the curb keeps the ball in the bowl, it is at {p} (r {r:F2})");
        }

        [UnityTest]
        public IEnumerator Hole8_Cup_CanBeHoled_FromTheBowl()
        {
            using var b = new HoleBed(8);
            yield return Steps(5);
            var cup = b.hole.Cup.transform.position;
            b.ball.PlaceAt(new Vector3(cup.x, cup.y + b.ball.Radius + 0.002f, cup.z - 0.4f));
            yield return Steps(5);
            Vector3 d = cup - b.ball.Position; d.y = 0f;
            b.ball.Strike(d.normalized * 1.1f);
            yield return WaitUntil(() => b.hole.IsComplete, 8f);
            Assert.IsTrue(b.hole.IsComplete, $"not holed, ball at {b.ball.Position}, cup {cup}");
        }

        [UnityTest]
        public IEnumerator Hole8_NormalRoute_CompletesByRealPutting_NoShortcutNeeded()
        {
            var s = TropicalCourse.Hole8Spec();
            float R = s.BowlRadius;
            using var b = new HoleBed(8);
            int returns = 0; b.hole.BallReturned += (h, o) => returns++;
            yield return Steps(5);
            int strokes = 0;
            // 1. West from the tee: the bank turns the ball up the climb.
            b.ball.Strike(new Vector3(-3.6f, 0f, 0f)); strokes++;
            yield return Steps(3);
            yield return WaitUntil(() => b.ball.IsAtRest, 25f);
            yield return Steps(10);
            Assert.IsTrue(b.ball.Position.x < s.ColumnEastX + 0.1f || InBowl(b.ball.Position, s), $"on the climb, at {b.ball.Position}");
            // 2. North up the climb (from its foot), through the north bank and the gate lane, into the bowl.
            bool inBowl = false;
            for (int attempt = 0; attempt < 3 && !inBowl && !b.hole.IsComplete; attempt++)
            {
                b.ball.PlaceAt(b.Surface((s.WestX + s.ColumnEastX) * 0.5f, s.climbStartZ));
                yield return Steps(5);
                b.ball.Strike(new Vector3(0f, 0f, new[] { 4.2f, 4.8f, 3.8f }[attempt])); strokes++;
                yield return Steps(3);
                yield return WaitUntil(() => b.ball.IsAtRest || b.hole.IsComplete || b.hole.Strokes > strokes, 40f);
                yield return Steps(15);
                inBowl = b.hole.IsComplete || InBowl(b.ball.Position, s);
                Debug.Log($"[Test] hole 8 route, attempt {attempt}: ball {b.ball.Position}, in bowl {inBowl}");
            }
            Assert.IsTrue(inBowl, $"the route did not reach the bowl, ball at {b.ball.Position}");
            // 3. The bowl: aimed putts at the cup.
            for (int i = 0; i < 6 && !b.hole.IsComplete; i++)
            {
                Vector3 cup = b.hole.Cup.transform.position, d = cup - b.ball.Position; d.y = 0f;
                // A ball outside the inner ring must be struck firmly enough to climb it (that is its job); inside, a soft putt.
                bool outside = Vector2.Distance(new Vector2(b.ball.Position.x, b.ball.Position.z), s.BowlCentre) > s.jump.bowl.ringRadius + 0.05f;
                b.ball.Strike(d.normalized * Mathf.Clamp(SpeedForDistance(d.magnitude, b.tuning), outside ? 1.6f : 0.5f, 2.4f)); strokes++;
                yield return Steps(3);
                yield return WaitUntil(() => b.hole.IsComplete || b.ball.IsAtRest || b.hole.Strokes > strokes, 40f);
                yield return Steps(10);
            }
            Debug.Log($"[Test] hole 8 normal route: strokes played {strokes}, hole strokes {b.hole.Strokes}, complete {b.hole.IsComplete}, returns {returns}");
            Assert.IsTrue(b.hole.IsComplete, $"the hole was not completed, ball at {b.ball.Position}");
            Assert.AreEqual(0, returns, "the normal route never leaves the course");
            Assert.AreEqual(strokes, b.hole.Strokes, "no penalty strokes");
        }

        // ------------------------------------------------------------------ Hole 8 play: the shortcut

        [UnityTest]
        public IEnumerator Hole8_Shortcut_FirmTeePuttJumpsIntoTheBowl_TangentiallyAndStays_ASoftOneDoesNot()
        {
            var s = TropicalCourse.Hole8Spec();
            float R = s.BowlRadius;
            var j = s.Jump;
            var table = new System.Text.StringBuilder("[Test] hole 8 shortcut from the tee (strike m/s -> outcome)\n");
            var landed = new List<float>(); var punished = new List<float>(); var rolledBack = new List<float>();
            foreach (float v in new[] { 3.0f, 3.4f, 3.8f, 4.2f, 4.6f, 5.0f, 5.6f })
            {
                using var b = new HoleBed(8);
                int returns = 0; b.hole.BallReturned += (h, o) => returns++;
                yield return Steps(5);
                b.ball.Strike(new Vector3(0f, 0f, v));
                int air = 0; Vector3 firstLanding = Vector3.zero; bool wasAir = false;
                float tStart = Time.time, end = tStart + 40f;
                while (Time.time < end)
                {
                    yield return new WaitForFixedUpdate();
                    bool flying = !b.ball.IsGrounded && b.ball.Position.z > j.ramp.LipZ - 0.05f && b.ball.Position.y > -0.2f;
                    if (flying) { air++; wasAir = true; }
                    else if (wasAir && firstLanding == Vector3.zero) firstLanding = b.ball.Position;
                    if ((b.ball.IsAtRest && Time.time > tStart + 1.0f) || b.hole.IsComplete || returns > 0) break;
                }
                if (returns > 0) yield return WaitUntil(() => b.ball.IsAtRest, 5f);
                yield return Steps(15);
                Vector3 p = b.ball.Position;
                float r = Vector2.Distance(new Vector2(p.x, p.z), s.BowlCentre);
                bool inBowl = b.hole.IsComplete || InBowl(p, s);
                string outcome = inBowl ? (b.hole.IsComplete ? "HOLED from the jump" : $"in the bowl, r {r:F2}") : returns > 0 ? "pit: penalty, returned" : "rolled back / short";
                table.AppendLine($"  {v:F1} -> {outcome} (airborne steps {air}, strokes {b.hole.Strokes})");
                Assert.IsTrue(b.ball.InPlay || b.hole.IsComplete);
                Assert.IsFalse(b.ball.IsHeld);
                if (inBowl)
                {
                    landed.Add(v);
                    Assert.AreEqual(1, b.hole.Strokes, $"{v} m/s: a good jump costs the one stroke");
                    Assert.AreEqual(0, returns);
                    Assert.Greater(air, 5, "the jump is real flight");
                    if (firstLanding != Vector3.zero)
                        Assert.That(Vector2.Distance(new Vector2(firstLanding.x, firstLanding.z), s.BowlCentre), Is.GreaterThan(R * 0.6f), "it lands on the rim shelf, not in the middle: a tangential entry");
                }
                else if (returns > 0)
                {
                    punished.Add(v);
                    Assert.AreEqual(2, b.hole.Strokes, "a failed jump costs one penalty stroke");
                    Assert.That(Vector3.Distance(b.ball.Position, b.hole.LastRestPosition), Is.LessThan(0.05f), "and goes back to the last rest spot");
                }
                else rolledBack.Add(v);
            }
            Debug.Log(table.ToString());
            Assert.GreaterOrEqual(landed.Count, 2, "firm putts must jump into the bowl: a usable window");
            Assert.GreaterOrEqual(punished.Count + rolledBack.Count, 2, "soft putts must not make it, and must be recoverable");
            Assert.Less(punished.Concat(rolledBack).Min(), landed.Min(), "failure comes below the landing speeds");
        }

        [UnityTest]
        public IEnumerator Hole8_Shortcut_AfterAFailedJump_TheNextAttemptStillWorks()
        {
            var s = TropicalCourse.Hole8Spec();
            using var b = new HoleBed(8);
            int returns = 0; b.hole.BallReturned += (h, o) => returns++;
            yield return Steps(5);
            // A deliberately short jump: just enough speed to leave the lip.
            b.ball.Strike(new Vector3(0f, 0f, 3.6f));
            yield return WaitUntil(() => returns >= 1 || b.ball.IsAtRest, 30f);
            yield return Steps(20);
            Assert.IsTrue(b.ball.InPlay);
            Assert.IsFalse(b.ball.IsHeld);
            int strokesAfterFirst = b.hole.Strokes;
            // Whatever the outcome (pit and return, or a roll back), the ball is playable and the route is open: a plain putt west still works.
            b.ball.PlaceAt(b.Surface(0f, s.Tee.y));
            yield return Steps(10);
            b.ball.Strike(new Vector3(-3.4f, 0f, 0f));
            yield return WaitUntil(() => b.ball.IsAtRest, 25f);
            Assert.IsTrue(b.ball.InPlay);
            Assert.AreEqual(strokesAfterFirst + 1, b.hole.Strokes, "the follow-up putt is an ordinary stroke");
        }
    }
}
