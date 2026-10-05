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
    /// Holes 5 (The Sun Stair) and 6 (The Waterwheel Mill) and the BankWall they use. Layout and spec tests are deterministic; the physics tests
    /// measure the real ball and assert relationships and generous windows, and log the real outcome tables so the numbers can be retuned.
    /// </summary>
    public class Holes56Tests
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
            public WaterwheelCarrier Carrier => hole.GetComponentInChildren<WaterwheelCarrier>();
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

        // ------------------------------------------------------------------ BankWall

        [Test]
        public void BankWall_Segment_LiesAlongTheLine_AndFollowsASlope()
        {
            var root = new GameObject("BankWallTest");
            try
            {
                var a = new Vector3(0f, 0.1f, 0f); var b = new Vector3(1f, 0.4f, 2f);
                var wall = BankWall.Segment(root.transform, a, b, null, 0.14f, 0.06f, 0.1f, "W");
                Assert.IsNotNull(wall);
                var box = wall.GetComponent<BoxCollider>();
                Assert.IsNotNull(box, "a bank wall is a functional collider");
                Vector3 dir = (b - a).normalized;
                Assert.That(Vector3.Angle(wall.transform.forward, dir), Is.LessThan(0.1f), "the wall runs from a to b, climbing with the slope");
                Assert.That(wall.transform.localScale.z, Is.EqualTo((b - a).magnitude + 0.06f).Within(1e-3f), "length covers the segment plus the corner overlap");
                Vector3 mid = (a + b) * 0.5f;
                Assert.That(Vector3.Distance(wall.transform.position, mid + wall.transform.up * ((0.14f - 0.1f) * 0.5f)), Is.LessThan(1e-3f));
                Assert.IsNull(wall.GetComponent<PlayableSurface>(), "a ball resting on a wall top is out of bounds like on any rail");
                Assert.IsNull(BankWall.Segment(root.transform, a, a, null), "a zero-length segment makes no wall");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void BankWall_Polyline_MakesOneSegmentPerEdge()
        {
            var root = new GameObject("BankWallTest");
            try
            {
                var group = BankWall.Polyline(root.transform, new[] { Vector3.zero, new Vector3(1f, 0f, 0f), new Vector3(2f, 0f, 1f), new Vector3(2f, 0f, 2f) }, null);
                Assert.AreEqual(3, group.transform.childCount);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void BankWall_Reflect_FollowsTheBallModel()
        {
            // A 45 degree hit leaves at about 37.5 degrees from the wall (the repo's own rebound test), and slower.
            var wallDir = new Vector2(1f, 1f).normalized;
            var n = BankWall.FaceNormal(Vector2.zero, wallDir, new Vector2(1f, -1f));
            var outDir = BankWall.Reflect(new Vector2(0f, 1f), n);
            float angle = Vector2.Angle(outDir, wallDir);
            Assert.That(angle, Is.InRange(36.5f, 38.5f));
            Assert.Less(outDir.magnitude, 1f);
        }

        [UnityTest]
        public IEnumerator BankWall_RealBall_ReboundsAsTheModelSays()
        {
            using var bed = new FlatBed();
            var wall = BankWall.Segment(bed.root.transform, new Vector3(-1f, 0f, 3f), new Vector3(1f, 0f, 5f), null, 0.2f);
            Assert.IsNotNull(wall);
            Physics.SyncTransforms();
            yield return Steps(5);
            bed.ball.PlaceAt(new Vector3(0.5f, 0.03f, 1.0f));
            yield return Steps(5);
            bed.ball.Strike(new Vector3(0f, 0f, 3f));
            bool hit = false;
            bed.ball.HitWall += (b, v) => hit = true;
            yield return WaitUntil(() => hit, 3f);
            Assert.IsTrue(hit, "the ball never reached the angled wall");
            yield return Steps(2);
            Vector3 v = bed.ball.Velocity;
            float fromWall = Vector2.Angle(new Vector2(v.x, v.z), new Vector2(1f, 1f));
            Debug.Log($"[Test] bank wall: outgoing velocity {v:F2}, {fromWall:F1} degrees from the wall (model 37.5)");
            Assert.That(fromWall, Is.InRange(32f, 43f));
            Assert.Greater(v.x, 0.5f, "the 45 degree wall turns a ball moving up the lane to the right");
            Assert.AreEqual(1, bed.hole.Strokes);
        }

        class FlatBed : IDisposable
        {
            public readonly GolfTuning tuning;
            public readonly GolfBall ball;
            public readonly HoleController hole;
            public readonly GameObject root;
            public FlatBed()
            {
                tuning = ScriptableObject.CreateInstance<GolfTuning>();
                GolfPhysicsBootstrap.Apply(tuning);
                root = new GameObject("FlatBed");
                var ballGo = new GameObject("Ball");
                ballGo.transform.SetParent(root.transform);
                ballGo.AddComponent<Rigidbody>();
                ballGo.AddComponent<SphereCollider>();
                ball = ballGo.AddComponent<GolfBall>();
                ball.SetTuning(tuning);
                var l = new GreenLayout();
                l.Area(-3f, 0f, 6f, 8f);
                var def = new HoleDefinition { number = 1, name = "Flat", par = 2, layout = l, tee = new Vector2(0f, 0.5f), origin = Vector3.zero };
                hole = HoleFactory.Build(def, null, tuning, root.transform, ball);
                hole.BeginHole(ball);
            }
            public void Dispose() { Object.Destroy(root); Object.Destroy(tuning); }
        }

        // ------------------------------------------------------------------ the Temple Island data

        [Test]
        public void TempleCluster_HasHolesFiveAndSix_OnAnIslandOfItsOwn()
        {
            var clusters = TropicalCourse.Clusters();
            var temple = clusters.Find(c => c.id == TropicalCourse.TempleCluster);
            Assert.IsNotNull(temple);
            CollectionAssert.AreEqual(new[] { 5, 6 }, temple.holes);
            Assert.Greater(temple.radius, 10f);
            foreach (var other in clusters)
                if (other != temple && other.radius > 0f)
                    Assert.Greater(Vector2.Distance(other.centre, temple.centre), other.radius + 15f, $"the Temple Island overlaps {other.id}");
            foreach (var n in new[] { 5, 6 })
            {
                var d = Def(n);
                Assert.AreEqual(TropicalCourse.TempleCluster, d.cluster);
                float minX = d.layout.areas.Min(a => a.xMin), maxX = d.layout.areas.Max(a => a.xMax), minZ = d.layout.areas.Min(a => a.yMin), maxZ = d.layout.areas.Max(a => a.yMax);
                var world = new Vector2(d.origin.x + (minX + maxX) * 0.5f, d.origin.z + (minZ + maxZ) * 0.5f);
                Assert.Less(Vector2.Distance(world, temple.centre), temple.radius - 4f, $"hole {n} is not safely inside the island");
            }
        }

        // ------------------------------------------------------------------ Hole 5 layout

        [Test]
        public void Hole5_Definition_IsAParFourOnTheTempleIsland()
        {
            var d = Def(5);
            Assert.IsNotNull(d);
            Assert.AreEqual("The Sun Stair", d.name);
            Assert.AreEqual(4, d.par);
            Assert.IsNotNull(d.buildExtras, "walls and kickers come from the build hook");
            Assert.IsTrue(d.layout.cup.HasValue);
            Assert.AreEqual(0, d.layout.deckAreas.Count);
            var s = TropicalCourse.Hole5Spec();
            Assert.AreEqual("", s.Validate());
            Assert.That(s.RampSlope, Is.InRange(0.05f, 0.0785f), "ramps are climbable and a ball can rest on them");
            Assert.That(d.tee.x, Is.InRange(s.Left, s.LeftLaneEdge), "the tee is in the left lane, in line with ramp 1");
        }

        [Test]
        public void Hole5_HeightsFormThreeTerraces_WithTheCupOnTheUpperOne()
        {
            var s = TropicalCourse.Hole5Spec();
            var l = Def(5).layout;
            Assert.That(l.Height(0f, 1f), Is.EqualTo(0f).Within(1e-4f), "lower terrace");
            Assert.That(l.Height(0f, s.DeckFrontZ + 1f), Is.EqualTo(s.MiddleHeight).Within(1e-4f), "middle terrace");
            Assert.That(l.Height(-2.0f, s.UpperFrontZ + 0.5f), Is.EqualTo(s.UpperHeight).Within(1e-4f), "upper terrace");
            Assert.Greater(s.UpperHeight, s.MiddleHeight);
            Assert.That(l.Height(-2.0f, (s.RampFootZ + s.RampTopZ) * 0.5f), Is.InRange(0.01f, s.MiddleHeight - 0.01f), "ramp 1 climbs");
            Assert.That(l.Height(2.0f, (s.Ramp2FootZ + s.UpperFrontZ - 0.1f) * 0.5f), Is.InRange(s.MiddleHeight + 0.01f, s.UpperHeight - 0.01f), "ramp 2 climbs");
            var cup = l.cup.Value;
            Assert.That(cup.y, Is.GreaterThan(s.UpperFrontZ + 1f), "the cup is well back on the upper terrace");
            Assert.LessOrEqual(l.Height(cup.x, cup.y), s.UpperHeight, "the cup sits in the sun dish, at or below the terrace");
            Assert.That(l.Height(cup.x + 0.4f, cup.y), Is.EqualTo(l.Height(cup.x, cup.y)).Within(1e-4f), "flat apron round the cup");
        }

        [Test]
        public void Hole5_JumpGeometry_LipIsAboveTheUpperTerrace_ByThePadDrop()
        {
            var s = TropicalCourse.Hole5Spec();
            var l = Def(5).layout;
            float lip = l.Height(-2.0f, s.LipZ);
            Assert.That(lip, Is.EqualTo(s.LipHeight).Within(1e-3f));
            Assert.That(lip - s.UpperHeight, Is.EqualTo(s.padDrop).Within(1e-3f), "the upper terrace is padDrop below the lip, so the jump lands slightly down");
            Assert.That(l.Height(-2.0f, s.LipZ + 0.2f), Is.EqualTo(s.MiddleHeight).Within(1e-3f), "short of the terrace the floor is the middle terrace, not a pit");
            Assert.That(s.UpperFrontZ - s.LipZ, Is.InRange(0.3f, 0.8f), "a short, fair gap");
            Assert.That(s.JumpAngleDegrees, Is.InRange(10f, 18f));
        }

        [Test]
        public void Hole5_HasNoCliffsOnTheMainClimbs_ExceptTheTerraceRisers()
        {
            // Every cell of the two ramps changes height smoothly (at most the ramp slope); the only big steps are the terrace fronts and the lip.
            var s = TropicalCourse.Hole5Spec();
            var l = Def(5).layout;
            for (float z = s.RampFootZ; z < s.RampTopZ; z += 0.1f)
                Assert.That(Mathf.Abs(l.Height(-2.0f, z + 0.1f) - l.Height(-2.0f, z)), Is.LessThan(0.1f * s.RampSlope * 1.2f + 1e-4f), $"ramp 1 at z {z:F1}");
            for (float z = s.Ramp2FootZ; z < s.UpperFrontZ - 0.2f; z += 0.1f)
                Assert.That(Mathf.Abs(l.Height(2.0f, z + 0.1f) - l.Height(2.0f, z)), Is.LessThan(0.1f * s.RampSlope * 1.2f + 1e-4f), $"ramp 2 at z {z:F1}");
        }

        [Test]
        public void Hole5_WallPlan_StaysInsideTheHole_AndKickersTurnTowardTheirLane()
        {
            var s = TropicalCourse.Hole5Spec();
            foreach (var (name, a, b) in s.WallPlan())
            {
                Assert.That(a.x, Is.InRange(s.Left - 0.01f, s.Right + 0.01f), name);
                Assert.That(b.x, Is.InRange(s.Left - 0.01f, s.Right + 0.01f), name);
                Assert.That(a.z, Is.InRange(0f, s.UpperEndZ), name);
                Assert.That(b.z, Is.InRange(0f, s.UpperEndZ), name);
            }
            // Ramp 2's kicker turns a ball crossing the middle terrace (moving +x) up the lane (+z).
            var k = s.WallPlan().First(w => w.name == "Ramp2Kicker");
            var n = BankWall.FaceNormal(new Vector2(k.a.x, k.a.z), new Vector2(k.b.x, k.b.z), new Vector2(k.a.x - 1f, k.a.z + 0.5f));
            var o = BankWall.Reflect(Vector2.right, n);
            Assert.Greater(o.y, Mathf.Abs(o.x) * 1.5f, "the kicker sends the ball mostly up the lane");
        }

        // ------------------------------------------------------------------ Hole 5 play

        [UnityTest]
        public IEnumerator Hole5_Ramp1_SoftPuttStaysOnTheRamp_FirmPuttReachesTheMiddleTerrace_NoPenalty()
        {
            var s = TropicalCourse.Hole5Spec();
            var table = new System.Text.StringBuilder("[Test] hole 5 ramp 1 from the tee (strike m/s -> where it ends)\n");
            var reached = new List<float>(); var stayed = new List<float>();
            foreach (float v in new[] { 1.6f, 2.2f, 2.8f, 3.2f, 3.6f, 4.0f, 4.6f })
            {
                using var b = new HoleBed(5);
                yield return Steps(5);
                b.ball.Strike(new Vector3(0f, 0f, v));
                yield return Steps(3);
                yield return WaitUntil(() => b.ball.IsAtRest || b.hole.Strokes > 1, 14f);
                yield return Steps(15);
                Vector3 p = b.ball.Position;
                bool onMiddle = p.y > s.MiddleHeight - 0.03f && p.z > s.RampTopZ - 0.05f;
                table.AppendLine($"  {v:F1} -> ({p.x:F2}, {p.y:F2}, {p.z:F2}) {(onMiddle ? "MIDDLE TERRACE" : "lower")} strokes {b.hole.Strokes}");
                Assert.AreEqual(1, b.hole.Strokes, $"{v} m/s cost a penalty: the stair must never punish a plain putt up ramp 1");
                if (onMiddle) reached.Add(v); else stayed.Add(v);
            }
            Debug.Log(table.ToString());
            Assert.IsTrue(stayed.Contains(1.6f), "a soft putt must not reach the middle terrace");
            Assert.GreaterOrEqual(reached.Count, 2, "a firm putt reaches the middle terrace: a usable window");
        }

        [UnityTest]
        public IEnumerator Hole5_RampsHoldABall_SoAMissedClimbCanBeStruckAgain()
        {
            var s = TropicalCourse.Hole5Spec();
            using var b = new HoleBed(5);
            yield return Steps(5);
            float zMid = (s.RampFootZ + s.RampTopZ) * 0.5f;
            var start = b.Surface(-2.0f, zMid);
            b.ball.PlaceAt(start);
            yield return WaitUntil(() => false, 2.0f);
            Assert.That(Vector3.Distance(b.ball.Position, start), Is.LessThan(0.08f), "a ball resting on ramp 1 stays there (the slope is under the rest-hold limit)");
            Assert.IsTrue(b.ball.InPlay);
        }

        [UnityTest]
        public IEnumerator Hole5_Jump_FromTheLanding_LandsOnTheUpperTerrace_OrFallsShortWithoutPenalty()
        {
            var s = TropicalCourse.Hole5Spec();
            var table = new System.Text.StringBuilder("[Test] hole 5 jump from the landing (strike m/s -> outcome)\n");
            var landed = new List<float>(); var shortOnes = new List<float>();
            foreach (float v in new[] { 2.8f, 3.2f, 3.6f, 4.0f, 4.4f, 5.0f })
            {
                using var b = new HoleBed(5);
                yield return Steps(5);
                b.ball.PlaceAt(b.Surface(-2.0f, s.LandingEndZ - 0.4f));
                yield return Steps(5);
                b.ball.Strike(new Vector3(0f, 0f, v));
                int air = 0, maxAir = 0;
                float end = Time.time + 16f;
                while (Time.time < end)
                {
                    yield return new WaitForFixedUpdate();
                    if (!b.ball.IsGrounded && b.ball.Position.z > s.LandingEndZ) { air++; maxAir = Mathf.Max(maxAir, air); } else air = 0;
                    if (b.ball.IsAtRest || b.hole.IsComplete || b.hole.Strokes > 1) break;
                }
                yield return Steps(10);
                Vector3 p = b.ball.Position;
                bool onUpper = b.hole.IsComplete || (p.z > s.UpperFrontZ && p.y > s.UpperHeight - 0.1f);
                table.AppendLine($"  {v:F1} -> ({p.x:F2}, {p.y:F2}, {p.z:F2}) {(onUpper ? "UPPER TERRACE" : "short/other")} airborne steps {maxAir}, strokes {b.hole.Strokes}");
                Assert.AreEqual(1, b.hole.Strokes, $"a jump at {v} m/s must not cost a penalty: a short jump lands on the middle terrace");
                Assert.IsTrue(b.ball.InPlay || b.hole.IsComplete);
                if (onUpper) landed.Add(v); else shortOnes.Add(v);
            }
            Debug.Log(table.ToString());
            Assert.GreaterOrEqual(landed.Count, 2, "firm putts must clear the gap and land on the upper terrace");
            Assert.GreaterOrEqual(shortOnes.Count, 1, "a soft putt must fall short (and stay in play)");
            Assert.Less(shortOnes.Where(v => v <= 4.4f).DefaultIfEmpty(0f).Max(), landed.Min() + 0.01f, "falling short happens below the landing speeds (a very hard putt may bounce back off the far wall)");
        }

        [UnityTest]
        public IEnumerator Hole5_Jump_LandingIsInPlay_AndTheBallFliesRealPhysics()
        {
            var s = TropicalCourse.Hole5Spec();
            using var b = new HoleBed(5);
            yield return Steps(5);
            b.ball.PlaceAt(b.Surface(-2.0f, s.LandingEndZ - 0.4f));
            yield return Steps(5);
            b.ball.Strike(new Vector3(0f, 0f, 4.4f));
            int air = 0;
            float end = Time.time + 12f;
            while (Time.time < end)
            {
                yield return new WaitForFixedUpdate();
                Assert.IsFalse(b.ball.Body.isKinematic, "the jump is the ball's own physics");
                if (!b.ball.IsGrounded && b.ball.Position.z > s.LipZ - 0.05f) air++;
                if (b.ball.IsAtRest) break;
            }
            Assert.GreaterOrEqual(air, 5, "the ball must actually leave the lip");
            Assert.AreEqual(1, b.hole.Strokes);
        }

        [UnityTest]
        public IEnumerator Hole5_MiddleTerraceEdge_ABallThatRollsOffLandsBelow_WithoutPenalty()
        {
            var s = TropicalCourse.Hole5Spec();
            using var b = new HoleBed(5);
            yield return Steps(5);
            b.ball.PlaceAt(b.Surface(0.2f, s.DeckFrontZ + 0.5f));
            yield return Steps(5);
            b.ball.Strike(new Vector3(0f, 0f, -1.2f));       // toward the terrace front, over the edge
            yield return WaitUntil(() => b.ball.IsAtRest || b.hole.Strokes > 1, 10f);
            yield return Steps(15);
            Assert.AreEqual(1, b.hole.Strokes, "falling off a terrace is a setback, not a penalty");
            Assert.That(b.ball.Position.y, Is.LessThan(0.08f), "it ended on the lower floor");
            Assert.IsTrue(b.ball.InPlay);
        }

        [UnityTest]
        public IEnumerator Hole5_Ramp2Kicker_TurnsAPuttAcrossTheMiddleTerraceUpTheLane()
        {
            var s = TropicalCourse.Hole5Spec();
            using var b = new HoleBed(5);
            yield return Steps(5);
            b.ball.PlaceAt(b.Surface(0.0f, s.DeckFrontZ + 0.9f));
            yield return Steps(5);
            Vector3 start = b.ball.Position;
            b.ball.Strike(new Vector3(2.4f, 0f, 0f));
            yield return Steps(3);
            yield return WaitUntil(() => b.ball.IsAtRest, 10f);
            yield return Steps(15);
            Vector3 p = b.ball.Position;
            Debug.Log($"[Test] ramp 2 kicker: start {start}, rest {p}");
            Assert.AreEqual(1, b.hole.Strokes);
            Assert.Greater(p.x, s.RightLaneEdge, "the ball ended in ramp 2's lane");
            Assert.Greater(p.z, start.z + 0.25f, "and was turned up the lane by the kicker");
        }

        [UnityTest]
        public IEnumerator Hole5_Ramp2_CanBeClimbed_FromItsFoot()
        {
            var s = TropicalCourse.Hole5Spec();
            using var b = new HoleBed(5);
            yield return Steps(5);
            b.ball.PlaceAt(b.Surface(2.0f, s.Ramp2FootZ - 0.7f));
            yield return Steps(5);
            b.ball.Strike(new Vector3(0f, 0f, 3.8f));
            yield return Steps(3);
            yield return WaitUntil(() => b.ball.IsAtRest || b.hole.Strokes > 1 || b.hole.IsComplete, 12f);
            yield return Steps(15);
            Assert.AreEqual(1, b.hole.Strokes);
            Assert.That(b.hole.IsComplete || b.ball.Position.y > s.UpperHeight - 0.08f, $"the ball did not reach the upper terrace, at {b.ball.Position}");
        }

        [UnityTest]
        public IEnumerator Hole5_UpperKicker_TurnsABallAlongTheEdgeTowardTheCup()
        {
            var s = TropicalCourse.Hole5Spec();
            using var b = new HoleBed(5);
            yield return Steps(5);
            b.ball.PlaceAt(b.Surface(s.Left + 0.35f, s.UpperFrontZ + 0.7f));
            yield return Steps(5);
            b.ball.Strike(new Vector3(0f, 0f, 2.2f));
            yield return Steps(3);
            yield return WaitUntil(() => b.ball.IsAtRest || b.hole.IsComplete, 10f);
            yield return Steps(15);
            Assert.AreEqual(1, b.hole.Strokes);
            Assert.That(b.hole.IsComplete || b.ball.Position.x > s.Left + 1.2f, $"the kicker should turn the ball inward, ended at {b.ball.Position}");
        }

        [UnityTest]
        public IEnumerator Hole5_Cup_CanBeHoled_FromTheUpperTerrace()
        {
            using var b = new HoleBed(5);
            yield return Steps(5);
            var cup = b.hole.Cup.transform.position;
            b.ball.PlaceAt(new Vector3(cup.x, cup.y + b.ball.Radius + 0.002f, cup.z - 0.4f));
            yield return Steps(5);
            Vector3 d = cup - b.ball.Position; d.y = 0f;
            b.ball.Strike(d.normalized * 1.1f);
            yield return WaitUntil(() => b.hole.IsComplete, 8f);
            Assert.IsTrue(b.hole.IsComplete, $"not holed, ball at {b.ball.Position}, cup {cup}");
        }

        // ------------------------------------------------------------------ Hole 6 layout

        [Test]
        public void Hole6_Definition_IsAParThreeWithTheProvenWheel()
        {
            var d = Def(6);
            Assert.IsNotNull(d);
            Assert.AreEqual("The Waterwheel Mill", d.name);
            Assert.AreEqual(3, d.par);
            Assert.AreEqual(TropicalCourse.TempleCluster, d.cluster);
            var s = TropicalCourse.Hole6Spec();
            float r = GolfTuning.Default.ballRadius;
            Assert.AreEqual("", s.Validate(r));
            Assert.IsTrue(s.wheel.HasGreen, "the wheel delivers to a real green, not just a cup in a channel");
            Assert.LessOrEqual(s.wheel.wheel.periodSeconds / s.wheel.wheel.bucketCount, 2.5f, "a bucket passes often enough that waiting stays short");
            Assert.IsTrue(d.layout.openEdges.Count >= 1, "the dock end is open (curb, no rail in the buckets' way)");
            Assert.AreEqual(s.Cup(r).y, d.layout.cup.Value.y, 1e-4f);
        }

        [Test]
        public void Hole6_MillRoad_IsGentle_RestableAndReachesTheGreen()
        {
            var s = TropicalCourse.Hole6Spec();
            float r = GolfTuning.Default.ballRadius;
            var l = Def(6).layout;
            float x = (s.RoadInnerX + s.RoadOuterX) * 0.5f, greenH = s.GreenHeight(r);
            float prev = -1f;
            for (float z = 0.2f; z <= s.RoadEndZ(r); z += 0.1f)
            {
                float h = l.Height(x, z);
                Assert.GreaterOrEqual(h, prev - 1e-4f, $"the road only climbs (z {z:F1})");
                prev = h;
            }
            Assert.That(prev, Is.EqualTo(greenH).Within(0.03f), "the road arrives level with the green");
            Assert.Less(Mathf.Sin(Mathf.Atan(s.roadSlope)), 0.0785f, "a ball can rest on the road");
            float aqueduct = s.wheel.ChannelHeight(s.wheel.ChannelStartZ(r) + 0.5f, r);
            Assert.Greater(aqueduct, greenH, "the aqueduct is higher than the green it delivers to: it runs downhill");
            Assert.Greater(aqueduct, 0.5f, "the wheel genuinely lifts the ball");
        }

        [Test]
        public void Hole6_Aqueduct_RunsDownhillToTheGreen_AndTheGreenIsFlat()
        {
            var s = TropicalCourse.Hole6Spec();
            float r = GolfTuning.Default.ballRadius;
            var l = Def(6).layout;
            float prev = float.MaxValue;
            for (float z = s.wheel.ChannelStartZ(r) + 0.1f; z < s.GreenStartZ(r); z += 0.1f)
            {
                float h = l.Height(0f, z);
                Assert.LessOrEqual(h, prev + 1e-4f, $"the aqueduct only descends (z {z:F1})");
                prev = h;
            }
            float g = s.GreenHeight(r);
            for (float z = s.GreenStartZ(r); z <= s.GreenEndZ(r); z += 0.2f)
                Assert.That(l.Height(0.5f, z), Is.EqualTo(g).Within(1e-3f), "final green is flat");
        }

        // ------------------------------------------------------------------ Hole 6 play

        [UnityTest]
        public IEnumerator Hole6_WheelRoute_CarriesTheBallToTheGreen_WithNoExtraStrokes()
        {
            var s = TropicalCourse.Hole6Spec();
            float r = GolfTuning.Default.ballRadius;
            using var b = new HoleBed(6);
            var carrier = b.Carrier;
            Assert.IsNotNull(carrier, "the hole must contain the waterwheel");
            int returns = 0;
            b.hole.BallReturned += (h, oob) => returns++;
            yield return Steps(5);
            // Putt up the lane so it rolls to the dock under the axle.
            float dist = s.wheel.WheelZ - 0.2f - b.ball.Position.z;
            b.ball.Strike(new Vector3(0f, 0f, SpeedForDistance(dist, b.tuning)));
            yield return WaitUntil(() => carrier.ReleaseCount >= 1, 45f);
            Debug.Log($"[Test] hole 6 wheel route: captures {carrier.CaptureCount}, releases {carrier.ReleaseCount}, ball {b.ball.Position}, strokes {b.hole.Strokes}");
            Assert.GreaterOrEqual(carrier.CaptureCount, 1, "the wheel should scoop the ball");
            Assert.GreaterOrEqual(carrier.ReleaseCount, 1);
            Assert.AreEqual(1, b.hole.Strokes, "the ride costs no stroke");
            yield return WaitUntil(() => b.hole.IsComplete || b.ball.IsAtRest, 40f);
            Assert.AreEqual(1, b.hole.Strokes);
            Assert.AreEqual(0, returns, "no out-of-bounds return during the whole ride");
            Assert.That(b.hole.IsComplete || (b.ball.Position.z > s.GreenStartZ(r) - 0.3f && b.ball.Position.y > s.GreenHeight(r) - 0.1f),
                $"the ball should end on the final green or in the cup, at {b.ball.Position}");
        }

        [UnityTest]
        public IEnumerator Hole6_AHardPutt_IsNotScooped_AndTheWheelStillWorksOnTheNextAttempt()
        {
            var s = TropicalCourse.Hole6Spec();
            using var b = new HoleBed(6);
            var carrier = b.Carrier;
            yield return Steps(5);
            b.ball.Strike(new Vector3(0f, 0f, 4.6f));
            yield return WaitUntil(() => b.ball.IsAtRest, 40f);
            Assert.AreEqual(0, carrier.CaptureCount, "a fast ball bounces off the dock curb");
            Assert.IsFalse(b.ball.IsHeld);
            Assert.AreEqual(1, b.hole.Strokes, "no penalty: the curb keeps it on the course");
            b.hole.RequestReset();
            yield return Steps(5);
            float dist = s.wheel.WheelZ - 0.2f - b.ball.Position.z;
            b.ball.Strike(new Vector3(0f, 0f, SpeedForDistance(dist, b.tuning)));
            yield return WaitUntil(() => carrier.ReleaseCount >= 1, 45f);
            Assert.GreaterOrEqual(carrier.ReleaseCount, 1, "the second attempt is carried");
            Assert.AreEqual(2, b.hole.Strokes);
        }

        [UnityTest]
        public IEnumerator Hole6_MillRoad_IsAConventionalRoute_ToTheGreen_WithoutTheWheel()
        {
            var s = TropicalCourse.Hole6Spec();
            float r = GolfTuning.Default.ballRadius;
            using var b = new HoleBed(6);
            yield return Steps(5);
            float x = (s.RoadInnerX + s.RoadOuterX) * 0.5f;
            b.ball.PlaceAt(b.Surface(x, s.PadLength - 0.3f));
            yield return Steps(5);
            int strokes = 0; bool onGreen = false;
            for (int i = 0; i < 6 && !onGreen && !b.hole.IsComplete; i++)
            {
                b.ball.Strike(new Vector3(0f, 0f, 3.0f));
                strokes++;
                yield return Steps(3);
                yield return WaitUntil(() => b.ball.IsAtRest || b.hole.IsComplete || b.hole.Strokes > strokes, 14f);
                yield return Steps(15);
                onGreen = b.ball.Position.z > s.GreenStartZ(r) && b.ball.Position.y > s.GreenHeight(r) - 0.05f;
                Debug.Log($"[Test] mill road stroke {strokes}: ball {b.ball.Position}");
            }
            Assert.AreEqual(strokes, b.hole.Strokes, "no penalty strokes on the road");
            Assert.IsTrue(onGreen || b.hole.IsComplete, $"six plain putts up the road should reach the green, ball at {b.ball.Position}");
            Assert.IsTrue(b.ball.InPlay || b.hole.IsComplete);
        }

        [UnityTest]
        public IEnumerator Hole6_Cup_CanBeHoled_FromTheGreen()
        {
            using var b = new HoleBed(6);
            yield return Steps(5);
            var cup = b.hole.Cup.transform.position;
            b.ball.PlaceAt(new Vector3(cup.x, cup.y + b.ball.Radius + 0.002f, cup.z - 0.4f));
            yield return Steps(5);
            Vector3 d = cup - b.ball.Position; d.y = 0f;
            b.ball.Strike(d.normalized * 1.1f);
            yield return WaitUntil(() => b.hole.IsComplete, 8f);
            Assert.IsTrue(b.hole.IsComplete, $"not holed, ball at {b.ball.Position}, cup {cup}");
        }
    }
}
