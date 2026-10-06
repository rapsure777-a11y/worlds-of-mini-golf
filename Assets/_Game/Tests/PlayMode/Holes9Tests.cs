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
    /// Hole 9 (Summit Sanctuary), the finale: layout and spec tests are deterministic; the physics tests measure the real ball, assert relationships and generous
    /// windows, and log the real outcome tables so the numbers can be retuned in Unity. Also the nine-hole course run and the end-of-course scorecard.
    /// </summary>
    public class Holes9Tests
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
            public Transform Root => m_Root.transform;
            public void Dispose() { Object.Destroy(m_Root); Object.Destroy(tuning); }
        }

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

        class Tally { public int strokes; }

        /// <summary>Places the ball, strikes it, waits for it to stop. Returns nothing; the caller reads the ball and the tally.</summary>
        static IEnumerator Putt(HoleBed b, Tally t, Vector3? from, Vector3 velocity)
        {
            if (from.HasValue) { b.ball.PlaceAt(from.Value); yield return Steps(5); }
            b.ball.Strike(velocity); t.strokes++;
            yield return Steps(3);
            yield return WaitUntil(() => (b.ball.IsAtRest && !b.ball.IsHeld) || b.hole.IsComplete || b.hole.Strokes > t.strokes, 40f);
            // A penalty (out of bounds): wait for the ball to be returned to play before going on, so a pending return cannot move it later.
            if (b.hole.Strokes > t.strokes && !b.hole.IsComplete) { yield return WaitUntil(() => b.ball.InPlay, 10f); t.strokes = b.hole.Strokes; }
            yield return Steps(15);
        }

        static bool OnOverlookOrBeyond(Vector3 p, SummitSanctuarySpec s) => p.z > s.OverlookZ + 0.05f && p.y > s.OverlookHeight - 0.05f && p.x < s.OverlookEastX + 0.05f
            || p.z > s.ClimbEndZ;

        // ------------------------------------------------------------------ data and layout

        [Test]
        public void Hole9_Definition_IsAParFiveFinale_WithNoBowl()
        {
            var d = Def(9);
            Assert.IsNotNull(d);
            Assert.AreEqual("Summit Sanctuary", d.name);
            Assert.AreEqual(5, d.par);
            Assert.AreEqual(TropicalCourse.SummitCluster, d.cluster);
            var s = TropicalCourse.Hole9Spec();
            Assert.AreEqual("", s.Validate());
            Assert.IsTrue(d.layout.cup.HasValue, "the cup is part of the layout (on the Altar)");
            Assert.That(d.layout.cup.Value, Is.EqualTo(s.Cup));
            Assert.IsNotNull(d.buildExtras);
            Assert.GreaterOrEqual(d.layout.openEdges.Count, 6, "ramp lip, Overlook edge, both chasm sides and both Sky Bridge sides");
            // The final mechanic is not a bowl: nothing in the built hole is a RouletteBowl.
            var tuning = ScriptableObject.CreateInstance<GolfTuning>();
            var root = new GameObject("Hole9NoBowl");
            try
            {
                var ballGo = new GameObject("Ball"); ballGo.transform.SetParent(root.transform);
                ballGo.AddComponent<Rigidbody>(); ballGo.AddComponent<SphereCollider>();
                var ball = ballGo.AddComponent<GolfBall>();
                var def = new HoleDefinition { number = 9, name = d.name, par = d.par, layout = d.layout, tee = d.tee, origin = Vector3.zero, buildExtras = d.buildExtras };
                HoleFactory.Build(def, null, tuning, root.transform, ball);
                Assert.IsNull(root.GetComponentInChildren<RouletteBowl>(), "Hole 9 has no bowl of any kind");
                Assert.AreEqual(1, root.GetComponentsInChildren<Cup>().Length, "one cup");
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(tuning); }
        }

        [Test]
        public void SummitCluster_HasHoleNine_OnAnIslandOfItsOwn_AndTheHighestGround()
        {
            var clusters = TropicalCourse.Clusters();
            var summit = clusters.Find(c => c.id == TropicalCourse.SummitCluster);
            Assert.IsNotNull(summit);
            CollectionAssert.AreEqual(new[] { 9 }, summit.holes);
            Assert.Greater(summit.radius, 10f);
            foreach (var other in clusters)
                if (other != summit && other.radius > 0f)
                    Assert.Greater(Vector2.Distance(other.centre, summit.centre), other.radius + summit.radius + 8f, $"the Summit island overlaps {other.id}");
            var d = Def(9);
            var rects = d.layout.areas.Concat(d.extraAreas).ToList();
            foreach (var r in rects)
                foreach (var corner in new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMax) })
                    Assert.Less(Vector2.Distance(new Vector2(d.origin.x + corner.x, d.origin.z + corner.y), summit.centre), summit.radius - 1f, "the hole reaches the island's edge");
            foreach (var h in TropicalCourse.Holes().Where(h => h.number < 9))
                Assert.Greater(d.origin.y, h.origin.y, $"Hole 9 stands higher than hole {h.number}: the highest point of the course");
        }

        [Test]
        public void Hole9_IsClearlyLongerThanAnAverageHole_AndTheHeroShortcutSavesTheDetour()
        {
            var s = TropicalCourse.Hole9Spec();
            // Safe route as a polyline of the putts' track.
            var safe = new[] { new Vector2(0f, 0.8f), new Vector2(0.5f, 5.8f), new Vector2(3.9f, 6.2f), new Vector2(3.9f, 12.6f), new Vector2(3.0f, 13.2f), new Vector2(-0.3f, 13.2f),
                new Vector2(-0.3f, s.ShrineRiseEndZ), new Vector2(0.0f, s.FinalZ0 + 0.5f), new Vector2(s.Cup.x, s.Cup.y) };
            var hero = new[] { new Vector2(0f, 0.8f), new Vector2(-0.3f, 5.9f), new Vector2(-0.3f, s.OverlookZ + 1.0f), new Vector2(-0.3f, s.ClimbEndZ + 0.8f),
                new Vector2(-0.3f, s.ShrineRiseEndZ), new Vector2(0.0f, s.FinalZ0 + 0.5f), new Vector2(s.Cup.x, s.Cup.y) };
            float Len(Vector2[] p) { float l = 0f; for (int i = 1; i < p.Length; i++) l += Vector2.Distance(p[i - 1], p[i]); return l; }
            float safeLen = Len(safe), heroLen = Len(hero);
            Debug.Log($"[Test] hole 9 route lengths: safe {safeLen:F1} m, hero {heroLen:F1} m");
            Assert.GreaterOrEqual(safeLen, 26f, "a long finale");
            Assert.GreaterOrEqual(safeLen - heroLen, 5f, "the hero shot saves a real distance (a stroke or more)");
            Assert.Greater(heroLen, 18f, "but the hero route is still a full golf hole, not a skip");
            // Longer than the average of the other holes' tee-to-cup spans (bounding extent along the longer side).
            float Extent(HoleDefinition h) { var r = h.layout.areas.Concat(h.extraAreas).ToList(); return Mathf.Max(r.Max(a => a.xMax) - r.Min(a => a.xMin), r.Max(a => a.yMax) - r.Min(a => a.yMin)); }
            float avg = TropicalCourse.Holes().Where(h => h.number < 9).Average(Extent);
            Assert.Greater(Extent(Def(9)), avg * 1.5f, "clearly longer than the average hole");
        }

        [Test]
        public void Hole9_Elevation_ClimbsStageByStage_AndEveryClimbIsRestable()
        {
            var s = TropicalCourse.Hole9Spec();
            var l = Def(9).layout;
            Assert.That(l.Height(0f, 0.8f), Is.EqualTo(0f).Within(1e-4f), "the tee is the lowest point");
            float prev = -1f;
            for (float z = s.riseStartZ; z <= s.ApproachEndZ; z += 0.1f) { float h = l.Height(0f, z); Assert.GreaterOrEqual(h, prev - 1e-4f); prev = h; }
            Assert.That(l.Height(0f, s.ApproachEndZ), Is.EqualTo(s.T1).Within(1e-4f), "Terrace 1");
            float cx = (s.ClimbWestX + s.ClimbEastX) * 0.5f; prev = -1f;
            for (float z = s.Terrace1EndZ; z <= s.ClimbEndZ; z += 0.1f)
            {
                float h = l.Height(cx, z);
                Assert.GreaterOrEqual(h, prev - 1e-4f, $"the Waterfall Climb only rises (z {z:F1})");
                if (prev >= 0f) Assert.LessOrEqual(h - prev, 0.1f * 0.0785f, $"restable at z {z:F1}");
                prev = h;
            }
            Assert.That(l.Height(cx, s.ClimbEndZ + 0.5f), Is.EqualTo(s.MergeHeight).Within(1e-4f), "the Merge Terrace");
            Assert.That(l.Height(0f, s.OverlookZ + 0.2f), Is.EqualTo(s.OverlookHeight).Within(1e-4f), "the Overlook, where the jump lands");
            Assert.That(l.Height(-0.3f, s.ShrineTopZ - 0.2f), Is.EqualTo(s.SummitLevel).Within(1e-4f), "the Shrine Lane's top");
            float bx = (s.LandingEndX + s.BridgeEndX) * 0.5f;
            Assert.That(l.Height(bx, (s.FinalZ0 + s.FinalZ1) * 0.5f), Is.InRange(s.SummitLevel + 0.01f, s.AltarHeight - 0.01f), "the Sky Bridge climbs");
            Assert.That(l.Height(s.Cup.x, s.Cup.y), Is.EqualTo(s.AltarHeight).Within(1e-4f), "the Altar is the highest ground");
            Assert.Greater(s.AltarHeight, s.MergeHeight); Assert.Greater(s.MergeHeight, s.T1);
            Assert.GreaterOrEqual(s.AltarHeight, 0.7f, "a genuine climb: stage after stage");
            Assert.That(s.ClimbSlope, Is.InRange(0.04f, 0.0785f)); Assert.That(s.BridgeSlope, Is.InRange(0.02f, 0.0785f));
            // Nothing in the layout is higher than the Altar.
            float max = 0f;
            foreach (var a in l.areas) for (float x = a.xMin; x <= a.xMax; x += 0.5f) for (float z = a.yMin; z <= a.yMax; z += 0.5f) max = Mathf.Max(max, l.Height(x, z));
            Assert.That(max, Is.LessThanOrEqualTo(s.AltarHeight + 1e-4f), "nothing on the hole is higher than the Altar (the jump's lip, 0.46, is below it)");
        }

        [Test]
        public void Hole9_Sightline_FromTheTee_ClearsEveryLevelUpToTheAltar()
        {
            var s = TropicalCourse.Hole9Spec();
            var l = Def(9).layout;
            var eye = new Vector3(0f, 1.6f, 0f);   // standing at the tee, eyes 1.6 m up (hole space; tee at height 0)
            var target = new Vector3(s.Cup.x, s.AltarHeight + 1.0f, s.Cup.y);
            for (float t = 0.02f; t < 1f; t += 0.02f)
            {
                var p = Vector3.Lerp(eye, target, t);
                bool inside = l.areas.Any(a => a.Contains(new Vector2(p.x, p.z)));
                if (inside) Assert.Greater(p.y, l.Height(p.x, p.z) + 0.3f, $"the view is blocked at ({p.x:F1}, {p.z:F1})");
            }
            Assert.Greater(s.AltarHeight, s.T1 * 2f, "the destination stands well above the player's start");
        }

        [Test]
        public void Hole9_JumpGeometry_UsesTheProvenSunStairNumbers()
        {
            var s = TropicalCourse.Hole9Spec();
            var l = Def(9).layout;
            Assert.That(l.Height(-0.3f, s.LipZ - 0.05f), Is.EqualTo(s.LipHeight).Within(0.02f), "the lip");
            Assert.That(s.LipHeight - s.OverlookHeight, Is.EqualTo(s.padDrop).Within(1e-4f), "the Overlook is padDrop below the lip: a landing, not a climb");
            Assert.That(s.Gap, Is.InRange(0.3f, 0.8f)); Assert.That(s.JumpAngleDegrees, Is.InRange(10f, 18f));
            var hz = s.HazardPlan().First(h => h.name == "GapPit");
            Assert.Less(hz.top, s.T1, "the pit lies below Terrace 1: a short jump falls out of bounds");
            Assert.LessOrEqual(hz.z0, s.LipZ); Assert.GreaterOrEqual(hz.z1, s.OverlookZ, "it spans the gap");
            Assert.That(s.RampEastX - s.RampWestX, Is.GreaterThanOrEqualTo(0.8f));
            Assert.That(-0.3f, Is.InRange(s.RampWestX, s.RampEastX), "the ramp is in line with the Shrine Lane's gate, so a good landing runs straight on");
            Assert.That(s.ShrineGateX, Is.InRange(s.RampWestX, s.RampEastX));
            Assert.IsTrue(Def(9).layout.openEdges.Any(r => Mathf.Abs(r.center.y - s.LipZ) < 0.02f), "the lip is an open edge");
        }

        [Test]
        public void Hole9_OpenEdges_LieOverOutOfBoundsDrops_AndTheSkyBridgeHasNoRails()
        {
            var s = TropicalCourse.Hole9Spec();
            var l = Def(9).layout;
            var hz = s.HazardPlan();
            var chasm = hz.First(h => h.name == "WaterfallChasm"); var abyss = hz.First(h => h.name == "Abyss");
            Assert.That(chasm.x0, Is.EqualTo(s.OverlookEastX).Within(1e-4f)); Assert.That(chasm.x1, Is.EqualTo(s.ClimbWestX).Within(1e-4f), "the chasm lies between the Overlook and the climb");
            Assert.That(abyss.x0, Is.EqualTo(s.LandingEndX).Within(1e-4f)); Assert.That(abyss.x1, Is.EqualTo(s.BridgeEndX).Within(1e-4f), "the abyss lies under the Sky Bridge only");
            Assert.Less(abyss.z0, s.FinalZ0); Assert.Greater(abyss.z1, s.FinalZ1);
            foreach (var h in hz) Assert.Less(h.top, s.T1 - 0.05f + (h.name == "GapPit" ? 0.1f : 0f), $"{h.name} lies below every surface beside it");
            // Open edges: both Sky Bridge sides along its whole length; the landing before it is railed (its edges are not open).
            Assert.IsTrue(l.openEdges.Any(r => Mathf.Abs(r.center.y - s.FinalZ0) < 0.02f && r.xMin <= s.LandingEndX + 1e-3f && r.xMax >= s.BridgeEndX - 1e-3f), "south side");
            Assert.IsTrue(l.openEdges.Any(r => Mathf.Abs(r.center.y - s.FinalZ1) < 0.02f && r.xMin <= s.LandingEndX + 1e-3f && r.xMax >= s.BridgeEndX - 1e-3f), "north side");
            Assert.IsFalse(l.openEdges.Any(r => Mathf.Abs(r.center.y - s.FinalZ0) < 0.02f && r.xMin < s.ShrineEastX + 0.1f), "the Landing keeps its rails");
            Assert.That(s.BridgeEndX - s.LandingEndX, Is.GreaterThanOrEqualTo(3f), "a long exposed putt");
            Assert.That(s.FinalZ1 - s.FinalZ0, Is.InRange(0.8f, 1.2f), "narrow but fair");
        }

        [Test]
        public void Hole9_Kickers_TurnTheBallTheWayTheRouteNeeds()
        {
            var s = TropicalCourse.Hole9Spec();
            var plan = s.WallPlan();
            Vector2 Turn(string name, Vector2 dir, Vector2 from)
            {
                var w = plan.First(x => x.name == name);
                var n = BankWall.FaceNormal(new Vector2(w.a.x, w.a.z), new Vector2(w.b.x, w.b.z), from);
                return BankWall.Reflect(dir, n);
            }
            var east = Turn("EastKicker", Vector2.right, new Vector2(2.0f, 5.6f));
            Assert.Greater(east.y, Mathf.Abs(east.x) * 3f, "an eastbound putt on Terrace 1 is turned north, up the climb");
            var corner = Turn("CornerKicker", Vector2.up, new Vector2(3.9f, 12.0f));
            Assert.Less(corner.x, -Mathf.Abs(corner.y) * 3f, "the ball arriving up the climb is turned west along the Merge Terrace");
            var shrine = Turn("ShrineKicker", Vector2.up, new Vector2(-0.3f, 16.5f));
            Assert.Greater(shrine.x, Mathf.Abs(shrine.y) * 3f, "a ball up the Shrine Lane is turned east onto the Landing");
            var west = plan.First(x => x.name == "ShrineStubWest"); var eastStub = plan.First(x => x.name == "ShrineStubEast");
            Assert.That(eastStub.b.x - west.b.x, Is.EqualTo(s.shrineGateWidth).Within(1e-3f), "the shrine gate is 0.7 m wide");
            Assert.That((west.b.x + eastStub.b.x) * 0.5f, Is.EqualTo(s.ShrineGateX).Within(1e-3f));
            foreach (var w in plan) { Assert.That(w.a.x, Is.InRange(s.Left - 0.01f, s.Terrace1EastX + 0.01f), w.name); Assert.That(w.b.z, Is.InRange(0f, s.ShrineTopZ + 0.01f), w.name); }
        }

        [Test]
        public void Hole9_Cup_SitsOnAFlatAltar_WithRoomRoundIt()
        {
            var s = TropicalCourse.Hole9Spec();
            var l = Def(9).layout;
            var c = s.Cup;
            Assert.That(c.x, Is.InRange(s.AltarWestX + 0.8f, s.AltarEastX - 0.6f)); Assert.That(c.y, Is.InRange(s.AltarZ0 + 0.6f, s.AltarZ1 - 0.6f));
            foreach (var (dx, dz) in new[] { (-0.5f, 0f), (0.5f, 0f), (0f, -0.5f), (0f, 0.5f), (-0.4f, -0.4f) })
                Assert.That(l.Height(c.x + dx, c.y + dz), Is.EqualTo(s.AltarHeight).Within(1e-4f), "the Altar is flat all round the cup (including 0.4 m behind it, as the progression test needs)");
            Assert.That(c.y, Is.EqualTo((s.FinalZ0 + s.FinalZ1) * 0.5f).Within(0.31f), "the cup is on the Sky Bridge's line: a straight, honest final putt");
        }

        [Test]
        public void Holes1To8_AreUnchangedByHoleNine()
        {
            var holes = TropicalCourse.Holes();
            var expected = new (int n, string name, int par)[] { (1, "Beach Warm-up", 2), (2, "Palm Corner", 3), (3, "Jungle Crossing", 4), (4, "Tiki Twister", 3),
                (5, "The Sun Stair", 3), (6, "The Waterwheel Mill", 3), (7, "Lava Falls", 4), (8, "Caldera Run", 3) };
            foreach (var (n, name, par) in expected)
            {
                var h = holes.Find(x => x.number == n);
                Assert.AreEqual(name, h.name); Assert.AreEqual(par, h.par);
            }
            Assert.AreEqual("", TropicalCourse.Hole5Spec().Validate());
            Assert.AreEqual("", TropicalCourse.Hole7Spec().Validate());
            Assert.AreEqual("", TropicalCourse.Hole8Spec().Validate());
            Assert.AreEqual(new Vector3(114f, 2.4f, 32f), holes.Find(x => x.number == 7).origin);
            Assert.AreEqual(new Vector3(129f, 2.6f, 40f), holes.Find(x => x.number == 8).origin);
            // The new hole does not overlap the Volcanic Island's holes in the world.
            Rect World(HoleDefinition d) { var r = d.layout.areas.Concat(d.extraAreas).ToList(); return Rect.MinMaxRect(d.origin.x + r.Min(a => a.xMin), d.origin.z + r.Min(a => a.yMin), d.origin.x + r.Max(a => a.xMax), d.origin.z + r.Max(a => a.yMax)); }
            foreach (var n in new[] { 7, 8, 5, 6 }) Assert.IsFalse(World(holes.Find(x => x.number == 9)).Overlaps(World(holes.Find(x => x.number == n))), $"hole 9 overlaps hole {n}");
        }

        // ------------------------------------------------------------------ the safe route, stage by stage

        [UnityTest]
        public IEnumerator Hole9_Approach_TeePuttReachesTerraceOne_WithoutPenalty_AndABallRestsOnTheSlope()
        {
            var s = TropicalCourse.Hole9Spec();
            var table = new System.Text.StringBuilder("[Test] hole 9 tee putt up the approach (strike m/s -> rest)\n");
            int reached = 0;
            foreach (float v in new[] { 2.4f, 3.0f, 3.4f, 3.8f })
            {
                using var b = new HoleBed(9);
                var t = new Tally();
                yield return Steps(5);
                yield return Putt(b, t, null, new Vector3(0f, 0f, v));
                Vector3 p = b.ball.Position;
                table.AppendLine($"  {v:F1} -> ({p.x:F2}, {p.y:F2}, {p.z:F2}) strokes {b.hole.Strokes}");
                Assert.AreEqual(1, b.hole.Strokes, $"{v} m/s: the opening never costs a penalty");
                if (p.z > s.ApproachEndZ - 0.1f && p.y > s.T1 - 0.04f) reached++;
            }
            Debug.Log(table.ToString());
            Assert.GreaterOrEqual(reached, 1, "a firm tee putt reaches Terrace 1");
            using (var hold = new HoleBed(9))
            {
                yield return Steps(5);
                var start = hold.Surface(0f, 3.0f);
                hold.ball.PlaceAt(start);
                yield return WaitUntil(() => false, 2.0f);
                Assert.That(Vector3.Distance(hold.ball.Position, start), Is.LessThan(0.08f), "a ball resting on the approach stays there");
            }
        }

        [UnityTest]
        public IEnumerator Hole9_EastKicker_TurnsATerraceOnePuttNorth_IntoTheClimb()
        {
            var s = TropicalCourse.Hole9Spec();
            var table = new System.Text.StringBuilder("[Test] hole 9 Terrace 1 east putt from (0.5, 5.8) (strike m/s -> rest)\n");
            int turned = 0;
            foreach (float v in new[] { 2.6f, 3.0f, 3.4f })
            {
                using var b = new HoleBed(9);
                var t = new Tally();
                yield return Steps(5);
                yield return Putt(b, t, b.Surface(0.5f, 5.8f), new Vector3(v, 0f, 0f));
                Vector3 p = b.ball.Position;
                table.AppendLine($"  {v:F1} -> ({p.x:F2}, {p.y:F2}, {p.z:F2}) strokes {b.hole.Strokes}");
                Assert.AreEqual(1, b.hole.Strokes, "no penalty on Terrace 1");
                if (p.x > s.ClimbWestX - 0.1f && p.z > s.Terrace1EndZ - 0.1f) turned++;
            }
            Debug.Log(table.ToString());
            Assert.GreaterOrEqual(turned, 1, "the kicker turns an eastbound putt up the climb");
        }

        [UnityTest]
        public IEnumerator Hole9_WaterfallClimb_SoftPuttsStay_FirmPuttsReachTheMergeTerrace()
        {
            var s = TropicalCourse.Hole9Spec();
            var table = new System.Text.StringBuilder("[Test] hole 9 Waterfall Climb from its foot (strike m/s -> rest)\n");
            int reached = 0; bool softStayed = false;
            foreach (float v in new[] { 2.2f, 3.4f, 4.0f, 4.6f, 5.2f })
            {
                using var b = new HoleBed(9);
                var t = new Tally();
                yield return Steps(5);
                float cx = (s.ClimbWestX + s.ClimbEastX) * 0.5f;
                yield return Putt(b, t, b.Surface(cx, s.Terrace1EndZ + 0.3f), new Vector3(0f, 0f, v));
                Vector3 p = b.ball.Position;
                table.AppendLine($"  {v:F1} -> ({p.x:F2}, {p.y:F2}, {p.z:F2}) strokes {b.hole.Strokes}");
                Assert.AreEqual(1, b.hole.Strokes, $"{v} m/s up the climb cost a penalty");
                if (p.z > s.ClimbEndZ - 0.05f) reached++;
                if (v < 3f && p.z < s.ClimbEndZ - 0.5f && p.y > s.T1 - 0.02f) softStayed = true;
            }
            Debug.Log(table.ToString());
            Assert.GreaterOrEqual(reached, 2, "firm putts reach the Merge Terrace: a usable window");
            Assert.IsTrue(softStayed, "a soft putt stays on the climb (it can be struck again)");
        }

        [UnityTest]
        public IEnumerator Hole9_ChasmEdge_ACrookedPuttOffTheClimb_CostsOneStroke_AndReturnsTheBall()
        {
            var s = TropicalCourse.Hole9Spec();
            using var b = new HoleBed(9);
            int returns = 0; bool oob = false;
            b.hole.BallReturned += (h, o) => { returns++; oob = o; };
            yield return Steps(5);
            b.ball.PlaceAt(b.Surface(s.ClimbWestX + 0.3f, (s.Terrace1EndZ + s.ClimbEndZ) * 0.5f));
            yield return Steps(5);
            b.ball.Strike(new Vector3(-1.4f, 0f, 0f));
            yield return WaitUntil(() => returns >= 1, 12f);
            yield return Steps(5);
            Assert.AreEqual(1, returns); Assert.IsTrue(oob, "the waterfall chasm is out of bounds");
            Assert.AreEqual(2, b.hole.Strokes, "the stroke plus one penalty stroke");
            Assert.IsTrue(b.ball.InPlay);
            Assert.That(Vector3.Distance(b.ball.Position, b.hole.LastRestPosition), Is.LessThan(0.05f), "back at the last rest spot, never stranded");
        }

        [UnityTest]
        public IEnumerator Hole9_CornerKicker_TurnsTheClimbBallWestAlongTheMergeTerrace()
        {
            var s = TropicalCourse.Hole9Spec();
            using var b = new HoleBed(9);
            var t = new Tally();
            yield return Steps(5);
            yield return Putt(b, t, b.Surface((s.ClimbWestX + s.ClimbEastX) * 0.5f, s.ClimbEndZ - 0.8f), new Vector3(0f, 0f, 2.4f));
            Vector3 p = b.ball.Position;
            Debug.Log($"[Test] corner kicker: rest {p}");
            Assert.AreEqual(1, b.hole.Strokes);
            Assert.Greater(p.z, s.ClimbEndZ - 0.05f, "it reached the Merge Terrace");
            Assert.Less(p.x, s.ClimbWestX - 0.2f, "and was turned west by the kicker");
        }

        [UnityTest]
        public IEnumerator Hole9_ShrineLane_AGoodPuttThreadsTheGate_AndTheKickerDeliversTheLanding()
        {
            var s = TropicalCourse.Hole9Spec();
            using (var b = new HoleBed(9))
            {
                var t = new Tally();
                yield return Steps(5);
                yield return Putt(b, t, b.Surface(s.ShrineGateX, s.MergeEndZ - 0.5f), new Vector3(0f, 0f, 2.4f));
                Vector3 p = b.ball.Position;
                Debug.Log($"[Test] shrine gate: rest {p}");
                Assert.AreEqual(1, b.hole.Strokes);
                Assert.Greater(p.z, s.shrineGateZ + 0.2f, "a ball aimed through the 0.7 m gate passes it");
                Assert.Less(p.x, s.ShrineEastX + 0.05f);
            }
            var table = new System.Text.StringBuilder("[Test] hole 9 up the Shrine Lane into the kicker (strike m/s -> rest)\n");
            int landed = 0;
            foreach (float v in new[] { 2.4f, 2.9f, 3.4f })
            {
                using var b = new HoleBed(9);
                var t = new Tally();
                yield return Steps(5);
                yield return Putt(b, t, b.Surface(s.ShrineGateX, s.shrineGateZ + 0.8f), new Vector3(0f, 0f, v));
                Vector3 p = b.ball.Position;
                table.AppendLine($"  {v:F1} -> ({p.x:F2}, {p.y:F2}, {p.z:F2}) strokes {b.hole.Strokes}");
                Assert.AreEqual(1, b.hole.Strokes, $"{v} m/s: the Shrine Lane never costs a penalty (the Landing keeps its rails)");
                if (p.x > s.ShrineEastX + 0.2f) landed++;
            }
            Debug.Log(table.ToString());
            Assert.GreaterOrEqual(landed, 1, "a firm putt up the lane is turned east onto the Landing");
        }

        // ------------------------------------------------------------------ the finale: the Sky Bridge and the cup

        [UnityTest]
        public IEnumerator Hole9_SkyBridge_StraightPuttsReachTheAltar_AndTheCupIsHolable()
        {
            var s = TropicalCourse.Hole9Spec();
            float zc = (s.FinalZ0 + s.FinalZ1) * 0.5f;
            var table = new System.Text.StringBuilder("[Test] hole 9 Sky Bridge putt from the Landing (strike m/s -> outcome)\n");
            int onAltar = 0, holed = 0, short_ = 0;
            foreach (float v in new[] { 1.8f, 2.4f, 2.8f, 3.2f, 3.6f })
            {
                using var b = new HoleBed(9);
                var t = new Tally();
                yield return Steps(5);
                Vector3 cup = b.hole.Cup.transform.position, from = b.Surface(1.0f, zc);
                Vector3 dir = cup - from; dir.y = 0f;
                yield return Putt(b, t, from, dir.normalized * v);
                Vector3 p = b.ball.Position;
                string outcome = b.hole.IsComplete ? "HOLED" : p.x >= s.AltarWestX - 0.05f ? "altar" : "short, on the bridge";
                table.AppendLine($"  {v:F1} -> {outcome} ({p.x:F2}, {p.z:F2}) strokes {b.hole.Strokes}");
                Assert.AreEqual(1, b.hole.Strokes, $"{v} m/s straight down the bridge: no penalty");
                Assert.IsTrue(b.ball.InPlay || b.hole.IsComplete);
                if (b.hole.IsComplete) holed++; else if (p.x >= s.AltarWestX - 0.05f) onAltar++; else short_++;
            }
            Debug.Log(table.ToString());
            Assert.GreaterOrEqual(onAltar + holed, 2, "a firm straight putt crosses the bridge");
            Assert.GreaterOrEqual(short_, 1, "a soft putt stops on the bridge: it can be struck again");
            Assert.GreaterOrEqual(onAltar, 1, "a good putt usually leaves a real final putt rather than a hole out");
        }

        [UnityTest]
        public IEnumerator Hole9_Finale_NoPuttLeavesTheBallOutOfPlay()
        {
            var s = TropicalCourse.Hole9Spec();
            float zc = (s.FinalZ0 + s.FinalZ1) * 0.5f;
            var log = new System.Text.StringBuilder("[Test] hole 9 finale escape sweep (from x, strike m/s, yaw deg -> rest)\n");
            int escaped = 0;
            foreach (float x0 in new[] { 0.8f, 1.5f, 3.0f, 4.5f, 5.6f })
            foreach (float v in new[] { 2.0f, 3.1f, 4.0f, 5.5f, 8.0f })
            foreach (float yaw in new[] { -4f, 0f, 4f })
            {
                using var b = new HoleBed(9);
                var t = new Tally();
                yield return Steps(5);
                Vector3 cup = b.hole.Cup.transform.position, from = b.Surface(x0, zc);
                Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
                yield return Putt(b, t, from, dir * v);
                Vector3 p = b.ball.Position;
                if (!(b.ball.InPlay || b.hole.IsComplete)) escaped++;   // a hard putt may rebound far back down the course or fall (penalty); it must always be playable
                log.AppendLine($"  {x0:F1}, {v:F1}, {yaw:F0} -> ({p.x:F2}, {p.y:F2}, {p.z:F2}) strokes {b.hole.Strokes} holed {b.hole.IsComplete} inPlay {b.ball.InPlay}");
            }
            Debug.Log(log.ToString());
            Assert.AreEqual(0, escaped, "every ball stays in play or is returned (never lost behind the back wall)");
        }

        [UnityTest]
        public IEnumerator Hole9_SkyBridge_ACrookedPutt_FallsIntoTheAbyss_WithAFairRecovery()
        {
            var s = TropicalCourse.Hole9Spec();
            float zc = (s.FinalZ0 + s.FinalZ1) * 0.5f;
            using var b = new HoleBed(9);
            int returns = 0; bool oob = false;
            b.hole.BallReturned += (h, o) => { returns++; oob = o; };
            yield return Steps(5);
            b.ball.PlaceAt(b.Surface(1.2f, zc));
            yield return Steps(5);
            float a = 25f * Mathf.Deg2Rad;
            b.ball.Strike(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 2.6f);      // 25 degrees off the bridge's line
            yield return WaitUntil(() => returns >= 1, 15f);
            yield return Steps(5);
            Assert.AreEqual(1, returns, "the ball left the bridge");
            Assert.IsTrue(oob);
            Assert.AreEqual(2, b.hole.Strokes, "one stroke plus one penalty");
            Assert.IsTrue(b.ball.InPlay);
            Assert.That(Vector3.Distance(b.ball.Position, b.hole.LastRestPosition), Is.LessThan(0.05f), "back where it can be putted again");
            // And it can be putted again straight away.
            b.ball.Strike(new Vector3(2.8f, 0f, 0f));
            yield return Steps(3);
            yield return WaitUntil(() => b.ball.IsAtRest || b.hole.IsComplete, 20f);
            Assert.AreEqual(3, b.hole.Strokes);
        }

        [UnityTest]
        public IEnumerator Hole9_Cup_CanBeHoled_FromTheAltar()
        {
            using var b = new HoleBed(9);
            yield return Steps(5);
            var cup = b.hole.Cup.transform.position;
            b.ball.PlaceAt(new Vector3(cup.x, cup.y + b.ball.Radius + 0.002f, cup.z - 0.4f));
            yield return Steps(5);
            Vector3 d = cup - b.ball.Position; d.y = 0f;
            b.ball.Strike(d.normalized * 1.1f);
            yield return WaitUntil(() => b.hole.IsComplete, 8f);
            Assert.IsTrue(b.hole.IsComplete, $"not holed, ball at {b.ball.Position}, cup {cup}");
        }

        // ------------------------------------------------------------------ the hero shortcut

        [UnityTest]
        public IEnumerator Hole9_HeroJump_FirmPuttsLandOnTheOverlook_SoftOnesFailFairly()
        {
            var s = TropicalCourse.Hole9Spec();
            var table = new System.Text.StringBuilder("[Test] hole 9 hero jump from Terrace 1 (strike m/s -> outcome)\n");
            var landed = new List<float>(); var failed = new List<float>();
            foreach (float v in new[] { 2.8f, 3.2f, 3.6f, 4.0f, 4.4f, 4.8f, 5.4f })
            {
                using var b = new HoleBed(9);
                int returns = 0; b.hole.BallReturned += (h, o) => returns++;
                var t = new Tally();
                yield return Steps(5);
                b.ball.PlaceAt(b.Surface(-0.3f, s.Terrace1EndZ - 0.7f));
                yield return Steps(5);
                b.ball.Strike(new Vector3(0f, 0f, v)); t.strokes++;
                int air = 0; float tStart = Time.time, end = tStart + 40f;
                while (Time.time < end)
                {
                    yield return new WaitForFixedUpdate();
                    if (!b.ball.IsGrounded && b.ball.Position.z > s.LipZ - 0.05f && b.ball.Position.y > -0.2f) air++;
                    if ((b.ball.IsAtRest && Time.time > tStart + 1.0f) || b.hole.IsComplete || returns > 0) break;
                }
                if (returns > 0) yield return WaitUntil(() => b.ball.IsAtRest, 8f);
                yield return Steps(15);
                Vector3 p = b.ball.Position;
                bool ok = OnOverlookOrBeyond(p, s);
                string outcome = ok ? "lands beyond the gap" : returns > 0 ? "pit: penalty, returned" : "rolled back / short";
                table.AppendLine($"  {v:F1} -> {outcome} ({p.x:F2}, {p.y:F2}, {p.z:F2}) airborne steps {air}, strokes {b.hole.Strokes}");
                Assert.IsTrue(b.ball.InPlay || b.hole.IsComplete); Assert.IsFalse(b.ball.IsHeld);
                if (ok) { landed.Add(v); Assert.AreEqual(1, b.hole.Strokes, $"{v} m/s: a good jump costs the one stroke"); Assert.Greater(air, 4, "the jump is real flight"); }
                else
                {
                    failed.Add(v);
                    if (returns > 0) { Assert.AreEqual(2, b.hole.Strokes, "a failed jump costs one penalty stroke"); Assert.That(Vector3.Distance(b.ball.Position, b.hole.LastRestPosition), Is.LessThan(0.05f), "and goes back to the last rest spot"); }
                    else Assert.AreEqual(1, b.hole.Strokes, "a soft putt that rolls back costs nothing");
                }
            }
            Debug.Log(table.ToString());
            Assert.GreaterOrEqual(landed.Count, 2, "firm putts must clear the gap: a usable window");
            Assert.GreaterOrEqual(failed.Count, 2, "soft putts must not make it, and must be recoverable");
            Assert.Less(failed.Min(), landed.Min(), "failure comes below the landing speeds");
        }

        [UnityTest]
        public IEnumerator Hole9_FailedJump_LeavesTheSafeRouteOpen()
        {
            var s = TropicalCourse.Hole9Spec();
            using var b = new HoleBed(9);
            int returns = 0; b.hole.BallReturned += (h, o) => returns++;
            var t = new Tally();
            yield return Steps(5);
            yield return Putt(b, t, b.Surface(-0.3f, s.Terrace1EndZ - 0.7f), new Vector3(0f, 0f, 3.3f));    // a deliberately marginal jump
            Assert.IsTrue(b.ball.InPlay); Assert.IsFalse(b.ball.IsHeld);
            int before = b.hole.Strokes;
            // Whatever happened, the safe route is still there: east, up the climb.
            yield return Putt(b, t, b.Surface(0.5f, 5.8f), new Vector3(3.0f, 0f, 0f));
            Assert.AreEqual(before + 1, b.hole.Strokes, "the next putt is an ordinary stroke");
            Assert.IsTrue(b.ball.InPlay);
        }

        // ------------------------------------------------------------------ whole routes

        static IEnumerator FromTheShrineLane(HoleBed b, Tally t, SummitSanctuarySpec s)
        {
            // Up the Shrine Lane to the Landing.
            for (int i = 0; i < 3 && !(b.ball.Position.x > s.ShrineEastX + 0.2f && b.ball.Position.z > s.FinalZ0 - 0.1f); i++)
                yield return Putt(b, t, i == 0 ? b.Surface(s.ShrineGateX, s.shrineGateZ + 0.8f) : (Vector3?)null, new Vector3(0f, 0f, new[] { 2.9f, 2.6f, 3.2f }[i]));
            // The Sky Bridge and the Altar: aimed putts at the cup.
            float zc = (s.FinalZ0 + s.FinalZ1) * 0.5f;
            if (b.ball.Position.x < s.ShrineEastX + 0.2f) b.ball.PlaceAt(b.Surface(1.0f, zc));
            for (int i = 0; i < 5 && !b.hole.IsComplete; i++)
            {
                Vector3 cup = b.hole.Cup.transform.position, d = cup - b.ball.Position; d.y = 0f;
                float speed = b.ball.Position.x < s.AltarWestX ? Mathf.Clamp(SpeedForDistance(d.magnitude + 0.15f, b.tuning), 0.6f, 3.4f) : Mathf.Clamp(SpeedForDistance(d.magnitude + 0.15f, b.tuning), 0.5f, 2.5f);
                yield return Putt(b, t, null, d.normalized * speed);
            }
        }

        [UnityTest]
        public IEnumerator Hole9_SafeRoute_CompletesByRealPutting_NoShortcutNeeded()
        {
            var s = TropicalCourse.Hole9Spec();
            using var b = new HoleBed(9);
            int returns = 0; b.hole.BallReturned += (h, o) => returns++;
            var t = new Tally();
            yield return Steps(5);
            yield return Putt(b, t, null, new Vector3(0f, 0f, 3.4f));                                     // 1. up the approach
            for (int i = 0; i < 3 && !(b.ball.Position.x > s.ClimbWestX - 0.1f && b.ball.Position.z > s.Terrace1EndZ - 0.1f); i++)
                yield return Putt(b, t, b.Surface(0.5f, 5.8f), new Vector3(new[] { 3.0f, 3.4f, 2.6f }[i], 0f, 0f));      // 2. east, the kicker turns it up the climb
            for (int i = 0; i < 3 && b.ball.Position.z < s.ClimbEndZ; i++)
                yield return Putt(b, t, b.Surface((s.ClimbWestX + s.ClimbEastX) * 0.5f, s.Terrace1EndZ + 0.4f), new Vector3(0f, 0f, new[] { 4.4f, 4.8f, 4.0f }[i]));   // 3. the climb
            Assert.Greater(b.ball.Position.z, s.ClimbEndZ - 0.05f, $"the climb was not made, ball at {b.ball.Position}");
            yield return Putt(b, t, b.Surface(s.ShrineGateX, s.MergeEndZ - 0.5f), new Vector3(0f, 0f, 2.6f));    // 4. across the Merge Terrace into the Shrine Lane
            yield return FromTheShrineLane(b, t, s);                                                      // 5-6. the Landing, the Sky Bridge, the cup
            Debug.Log($"[Test] hole 9 safe route: strokes played {t.strokes}, hole strokes {b.hole.Strokes}, complete {b.hole.IsComplete}, returns {returns}");
            Assert.IsTrue(b.hole.IsComplete, $"the hole was not completed, ball at {b.ball.Position}");
            Assert.AreEqual(0, returns, "no out-of-bounds returns on the safe route");
            Assert.AreEqual(t.strokes, b.hole.Strokes, "no penalty strokes");
            Assert.GreaterOrEqual(t.strokes, 5, "the finale is a five-stroke hole: no hole in one by accident");
        }

        [UnityTest]
        public IEnumerator Hole9_HeroRoute_CompletesInFewerStrokesThanTheSafeRoute()
        {
            var s = TropicalCourse.Hole9Spec();
            using var b = new HoleBed(9);
            int returns = 0; b.hole.BallReturned += (h, o) => returns++;
            var t = new Tally();
            yield return Steps(5);
            yield return Putt(b, t, null, new Vector3(0f, 0f, 3.4f));                                     // 1. up the approach
            bool over = false;
            for (int i = 0; i < 4 && !over; i++)
            {
                yield return Putt(b, t, b.Surface(-0.3f, s.Terrace1EndZ - 0.7f), new Vector3(0f, 0f, new[] { 4.4f, 4.0f, 4.8f, 3.8f }[i]));   // 2. the hero jump
                over = OnOverlookOrBeyond(b.ball.Position, s);
            }
            Assert.IsTrue(over, $"no jump landed, ball at {b.ball.Position}");
            if (b.ball.Position.z < s.ClimbEndZ)
                yield return Putt(b, t, b.Surface(-0.3f, s.OverlookZ + 1.5f), new Vector3(0f, 0f, 3.0f));  // 3. north across the Overlook
            if (b.ball.Position.z < s.MergeEndZ)
                yield return Putt(b, t, b.Surface(s.ShrineGateX, s.ClimbEndZ + 0.3f), new Vector3(0f, 0f, 2.6f));   // 4. into the Shrine Lane
            yield return FromTheShrineLane(b, t, s);
            Debug.Log($"[Test] hole 9 hero route: strokes played {t.strokes}, hole strokes {b.hole.Strokes}, complete {b.hole.IsComplete}");
            Assert.IsTrue(b.hole.IsComplete, $"the hole was not completed, ball at {b.ball.Position}");
            Assert.AreEqual(t.strokes, b.hole.Strokes, "the strokes played are the strokes counted");
            Assert.LessOrEqual(t.strokes, 8, "the hero route is a sensible hole");
        }

        // ------------------------------------------------------------------ the whole course

        [UnityTest]
        public IEnumerator Course_AllNineHoles_ProgressInOrder_AndTheScorecardEndsFinished()
        {
            var tuning = ScriptableObject.CreateInstance<GolfTuning>();
            GolfPhysicsBootstrap.Apply(tuning);
            var root = new GameObject("NineHoleCourse");
            try
            {
                var ballGo = new GameObject("Ball");
                ballGo.transform.SetParent(root.transform);
                ballGo.AddComponent<Rigidbody>(); ballGo.AddComponent<SphereCollider>();
                var ball = ballGo.AddComponent<GolfBall>();
                ball.SetTuning(tuning);
                var defs = TropicalCourse.Holes();
                var holes = defs.Select(d => HoleFactory.Build(d, null, tuning, root.transform, ball)).ToArray();
                Physics.SyncTransforms();
                var course = root.AddComponent<CourseController>();
                course.Configure("Tropical Adventure", holes, ball, null);
                course.StartOnAwake = false;
                course.AdvanceDelay = 0.5f;
                int finishedEvents = 0; course.CourseFinished += c => finishedEvents++;
                course.StartCourse(0);
                Assert.AreEqual(9, course.Holes.Length);
                Assert.AreEqual(9, course.Card.par.Length);
                Assert.AreEqual(5, course.Card.par[8], "Hole 9 is par 5");
                for (int h = 0; h < 9; h++)
                {
                    Assert.AreEqual(h, course.CurrentIndex);
                    var hole = course.Current;
                    yield return Steps(5);
                    Vector3 cup = hole.Cup.transform.position;
                    Vector3 back = hole.TeePosition - cup; back.y = 0f;
                    if (h == 1) back = Vector3.right;
                    if (h >= 2) back = hole.transform.TransformDirection(Vector3.back);
                    Vector3 start = cup + back.normalized * 0.4f;
                    start.y = cup.y + ball.Radius + 0.002f;
                    ball.PlaceAt(start);
                    yield return Steps(5);
                    Vector3 d = cup - ball.Position; d.y = 0f;
                    ball.Strike(d.normalized * 1.1f);
                    yield return WaitUntil(() => hole.IsComplete, 10f);
                    Assert.IsTrue(hole.IsComplete, $"hole {h + 1} not holed, ball {ball.Position}, cup {cup}");
                    if (h < 8) yield return WaitUntil(() => course.CurrentIndex != h, 4f);
                }
                yield return WaitUntil(() => course.Finished, 4f);
                Assert.IsTrue(course.Finished, "the course ends after Hole 9");
                Assert.AreEqual(1, finishedEvents, "CourseFinished fires once");
                Assert.IsTrue(course.Card.strokes.All(x => x > 0), "every hole is on the scorecard");
                Assert.AreEqual(course.Card.strokes.Sum(), course.Card.TotalStrokes);
                Assert.AreEqual(defs.Sum(x => x.par), course.Card.TotalParPlayed, "the par of all nine holes has been played");
                Assert.AreEqual(8, course.CurrentIndex, "the last hole stays current at the end");
            }
            finally { Object.Destroy(root); Object.Destroy(tuning); }
        }
    }
}
