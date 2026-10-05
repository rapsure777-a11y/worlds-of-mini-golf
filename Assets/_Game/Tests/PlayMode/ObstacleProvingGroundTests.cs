using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Gamebreak.MiniGolf.Tests
{
    /// <summary>A throwaway scene root with a ball, for the proving-ground holes (built with the same code the demo scene uses).</summary>
    class ProvingBed : IDisposable
    {
        public readonly GolfTuning tuning;
        public readonly GolfBall ball;
        public readonly GameObject root;
        public readonly ProvingMaterials mats = new ProvingMaterials();
        public HoleController hole;

        public ProvingBed()
        {
            tuning = ScriptableObject.CreateInstance<GolfTuning>();
            GolfPhysicsBootstrap.Apply(tuning);
            root = new GameObject("ProvingBed");
            var ballGo = new GameObject("Ball");
            ballGo.transform.SetParent(root.transform);
            ballGo.AddComponent<Rigidbody>();
            ballGo.AddComponent<SphereCollider>();
            ball = ballGo.AddComponent<GolfBall>();
            ball.SetTuning(tuning);
        }

        public HoleController Begin(HoleController h)
        {
            hole = h;
            hole.BeginHole(ball);
            Physics.SyncTransforms();
            return h;
        }

        public void Dispose()
        {
            Object.Destroy(root);
            Object.Destroy(tuning);
        }
    }

    /// <summary>
    /// Tests for the three proving-ground mechanics: the launch ramp (open lip, real airborne arc, forgiving landing), the waterwheel carrier
    /// (hold and release without strokes, out-of-bounds or ball corruption) and the roulette bowl (a sloped bowl with no scripted pull).
    /// Where a number depends on tuning, the test measures the real ball and asserts relationships (monotonic, ballistic, energy), so a
    /// retune does not invalidate it. Outcome tables are logged so Local can see the actual windows.
    /// </summary>
    public class ObstacleProvingGroundTests
    {
        [SetUp] public void SetUp() => Time.timeScale = 3f;
        [TearDown] public void TearDown() => Time.timeScale = 1f;

        static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float end = Time.time + timeoutSeconds;
            while (!condition() && Time.time < end) yield return new WaitForFixedUpdate();
        }

        static IEnumerator Steps(int n)
        {
            for (int i = 0; i < n; i++) yield return new WaitForFixedUpdate();
        }

        // ------------------------------------------------------------------ geometry and regression

        [Test]
        public void ExistingHoles_HaveNoOpenEdges()
        {
            foreach (var def in TropicalCourse.Holes())
                Assert.AreEqual(0, def.layout.openEdges.Count, $"hole {def.number} must be unchanged by the open-edge feature");
        }

        [Test]
        public void OpenEdges_RemoveTheRailAtTheLip_ButKeepASkirt()
        {
            var spec = new LaunchRampSpec();
            float inner = spec.Width * 0.5f - 0.15f;
            float TopAtLip(GreenLayout layout)
            {
                var mesh = CourseGeometry.BuildWalls(layout);
                float top = float.MinValue; bool anyBelow = false;
                foreach (var v in mesh.vertices)
                {
                    if (Mathf.Abs(v.z - spec.LipZ) > 1e-3f || Mathf.Abs(v.x) > inner) continue;
                    top = Mathf.Max(top, v.y);
                    if (v.y < spec.LipHeight - 0.1f) anyBelow = true;
                }
                Assert.IsTrue(anyBelow || layout.openEdges.Count == 0, "an open edge should have a skirt below the lip");
                return top;
            }
            float openTop = TopAtLip(spec.BuildLayout());
            var closed = spec.BuildLayout(); closed.openEdges.Clear();
            float closedTop = TopAtLip(closed);
            Assert.That(openTop, Is.LessThanOrEqualTo(spec.LipHeight + 0.001f), "no rail may stand along the open lip");
            Assert.That(closedTop, Is.GreaterThanOrEqualTo(spec.LipHeight + spec.railHeight - 0.001f), "the same edge normally has a rail");
        }

        [Test]
        public void PresetSpecs_AreSelfConsistent()
        {
            float r = GolfTuning.Default.ballRadius;
            foreach (ProvingPreset p in Enum.GetValues(typeof(ProvingPreset)))
            {
                Assert.AreEqual("", ProvingGround.WheelSpec(p).Validate(r), $"waterwheel {p}");
                var launch = ProvingGround.LaunchSpec(p);
                Assert.Greater(launch.PadStartZ, launch.LipZ, $"launch {p}: the landing platform must be beyond the lip");
                Assert.Greater(launch.dishSlope, 0.08f, $"launch {p}: a ball must not be able to rest on the dish slope");
                var bowl = ProvingGround.BowlSpec(p);
                Assert.Greater(Mathf.Sin(Mathf.Atan(bowl.coneSlope)), 0.0785f, $"bowl {p}: ball must not rest on the cone");
                Assert.Less(Mathf.Sin(Mathf.Atan(bowl.shelfSlope)), 0.0785f, $"bowl {p}: ball should be able to rest on the rim shelf");
            }
        }

        // ------------------------------------------------------------------ ball hold API

        static HoleController FlatHole(ProvingBed bed)
        {
            var l = new GreenLayout();
            l.Area(-0.6f, 0f, 1.2f, 8f);
            var def = new HoleDefinition { number = 1, name = "Flat", par = 2, layout = l, tee = new Vector2(0f, 1f), origin = Vector3.zero };
            return HoleFactory.Build(def, null, bed.tuning, bed.root.transform, bed.ball);
        }

        [UnityTest]
        public IEnumerator Hold_FreezesBall_CountsNoStroke_RaisesNoStop_AndReleasesCleanly()
        {
            using var bed = new ProvingBed();
            bed.Begin(FlatHole(bed));
            int stopped = 0;
            bed.ball.Stopped += _ => stopped++;
            yield return Steps(5);
            bed.ball.Strike(new Vector3(0f, 0f, 2f));
            yield return Steps(10);
            Assert.AreEqual(1, bed.hole.Strokes);
            Assert.IsTrue(bed.ball.TryHold(this));
            Assert.IsFalse(bed.ball.TryHold(new object()), "a held ball cannot be taken twice");
            Assert.IsTrue(bed.ball.IsHeld);
            Assert.IsTrue(bed.ball.Body.isKinematic);
            Vector3 frozen = bed.ball.Position;
            bed.ball.Strike(new Vector3(0f, 0f, 3f));
            yield return Steps(60);
            Assert.AreEqual(1, bed.hole.Strokes, "a strike on a held ball must be ignored");
            Assert.That(Vector3.Distance(bed.ball.Position, frozen), Is.LessThan(1e-3f), "a held ball does not roll");
            Assert.AreEqual(0, stopped, "holding is not stopping");
            bed.ball.EndHold(new Vector3(0f, 0f, 1f));
            Assert.IsFalse(bed.ball.IsHeld);
            Assert.IsFalse(bed.ball.Body.isKinematic);
            yield return Steps(2);
            Assert.That(bed.ball.Velocity.z, Is.InRange(0.7f, 1.05f), "released with the requested velocity, then ordinary rolling");
            yield return WaitUntil(() => bed.ball.IsAtRest, 15f);
            Assert.IsTrue(bed.ball.IsAtRest);
            Assert.AreEqual(1, bed.hole.Strokes, "release must not count as a stroke");
            Assert.AreEqual(1, stopped, "the ordinary stop event fires once the released ball settles");
        }

        [UnityTest]
        public IEnumerator Hold_IsCancelledByPlaceAt()
        {
            using var bed = new ProvingBed();
            bed.Begin(FlatHole(bed));
            yield return Steps(5);
            Assert.IsTrue(bed.ball.TryHold(this));
            Vector3 tee = bed.hole.TeePosition;
            bed.ball.PlaceAt(tee);
            Assert.IsFalse(bed.ball.IsHeld);
            Assert.IsFalse(bed.ball.Body.isKinematic);
            Assert.IsTrue(bed.ball.InPlay);
            yield return Steps(5);
            Assert.That(Vector3.Distance(bed.ball.Position, tee), Is.LessThan(0.03f));
        }

        [UnityTest]
        public IEnumerator Hold_IsNotTreatedAsStuck_ByTheHoleController()
        {
            using var bed = new ProvingBed();
            bed.Begin(FlatHole(bed));
            yield return Steps(5);
            bed.ball.Strike(new Vector3(0f, 0f, 1.0f));
            yield return Steps(5);
            Assert.IsTrue(bed.ball.TryHold(this));
            // The stuck-ball safeguard stops a slow ball after 20 s; a held ball must be exempt.
            yield return WaitUntil(() => false, 23f);
            Assert.IsTrue(bed.ball.IsHeld, "the safeguard must not take a held ball");
            Assert.IsTrue(bed.ball.Body.isKinematic);
            Assert.AreEqual(1, bed.hole.Strokes);
        }

        [Test]
        public void TryHold_Refuses_AnOutOfPlayBall()
        {
            using var bed = new ProvingBed();
            bed.Begin(FlatHole(bed));
            bed.ball.InPlay = false;
            Assert.IsFalse(bed.ball.TryHold(this));
            Assert.IsFalse(bed.ball.IsHeld);
        }

        // ------------------------------------------------------------------ A. launch ramp

        class LaunchResult
        {
            public readonly List<Vector3> air = new List<Vector3>();   // positions of the longest unbroken airborne run past the lip
            public int strokes;
            public bool reachedPad, rolledBack, holed;
            public float apexY;
            public Vector3 rest;
        }

        static HoleController BuildLaunch(ProvingBed bed, LaunchRampSpec spec) =>
            bed.Begin(ProvingGround.BuildLaunchHole(bed.root.transform, bed.tuning, bed.ball, bed.mats, spec, Vector3.zero));

        static IEnumerator Fly(ProvingBed bed, LaunchRampSpec spec, float speed, LaunchResult res, float timeout = 20f)
        {
            yield return Steps(5);
            bed.ball.Strike(Vector3.forward * speed);
            var run = new List<Vector3>();
            float end = Time.time + timeout;
            while (Time.time < end)
            {
                yield return new WaitForFixedUpdate();
                var b = bed.ball;
                if (b.InPlay && !b.IsGrounded && !b.IsHeld && b.Position.z > spec.RampStartZ + 0.1f)
                {
                    run.Add(b.Position);
                    res.apexY = Mathf.Max(res.apexY, b.Position.y);
                }
                else
                {
                    if (run.Count > res.air.Count) { res.air.Clear(); res.air.AddRange(run); }
                    run.Clear();
                }
                if (b.InPlay && b.IsGrounded && b.Position.z > spec.PadStartZ + 0.05f) res.reachedPad = true;
                if (bed.hole.Strokes >= 2 || bed.hole.IsComplete || (b.IsAtRest && b.InPlay)) break;
            }
            if (run.Count > res.air.Count) { res.air.Clear(); res.air.AddRange(run); }
            res.strokes = bed.hole.Strokes;
            res.holed = bed.hole.IsComplete;
            res.rest = bed.ball.Position;
            res.rolledBack = !res.reachedPad && res.strokes == 1 && !res.holed;
        }

        /// <summary>Mean second difference (acceleration) of one coordinate over a flight, ignoring two samples at each end (take-off and landing steps).</summary>
        static float MeanSecondDifference(List<Vector3> p, Func<Vector3, float> axis)
        {
            float dt = Time.fixedDeltaTime, sum = 0f; int n = 0;
            for (int i = 3; i < p.Count - 3; i++) { sum += (axis(p[i + 1]) - 2f * axis(p[i]) + axis(p[i - 1])) / (dt * dt); n++; }
            return n > 0 ? sum / n : float.NaN;
        }

        [UnityTest]
        public IEnumerator Launch_BallFliesARealBallisticArc_AndLandsOnThePad()
        {
            var spec = new LaunchRampSpec();
            using var bed = new ProvingBed();
            BuildLaunch(bed, spec);
            var res = new LaunchResult();
            yield return Fly(bed, spec, 4.0f, res);
            Debug.Log($"[Test] launch 4.0 m/s: airborne {res.air.Count} steps, apex y {res.apexY:F3}, strokes {res.strokes}, rest {res.rest}");
            Assert.GreaterOrEqual(res.air.Count, 12, "the ball must leave the surface for a real flight (about 0.2 s)");
            Assert.IsFalse(bed.ball.Body.isKinematic, "the jump is physics, not a scripted move");
            // Gravity: second difference of height over the flight equals -g. Horizontal speed is constant (no drag in the air).
            Assert.That(MeanSecondDifference(res.air, v => v.y), Is.InRange(-10.4f, -9.2f), "vertical motion must follow gravity");
            Assert.That(Mathf.Abs(MeanSecondDifference(res.air, v => v.z)), Is.LessThan(0.6f), "no horizontal push or drag in flight");
            Assert.Greater(res.apexY, spec.LipHeight + bed.tuning.ballRadius + 0.005f, "the ball rises above the lip before it falls");
            Assert.IsTrue(res.reachedPad, "it lands on the landing platform");
            Assert.AreEqual(1, res.strokes, "a clean jump costs no penalty");
            Assert.IsTrue(res.holed || res.rest.z > spec.PadStartZ, "and ends on the platform or in the cup");
        }

        [UnityTest]
        public IEnumerator Launch_SpeedSweep_RollsBack_ThenFallsInTheGap_ThenLands()
        {
            var spec = new LaunchRampSpec();
            float[] speeds = { 1.6f, 2.0f, 2.4f, 2.8f, 3.2f, 3.6f, 4.0f, 4.6f, 5.2f };
            var table = new System.Text.StringBuilder("[Test] launch sweep (strike m/s -> outcome)\n");
            var landed = new List<float>(); var gap = new List<float>(); var back = new List<float>();
            foreach (float s in speeds)
            {
                using var bed = new ProvingBed();
                BuildLaunch(bed, spec);
                var res = new LaunchResult();
                yield return Fly(bed, spec, s, res);
                string o = res.reachedPad && res.strokes == 1 ? "LANDED" : res.reachedPad ? "landed, then lost" : res.strokes >= 2 ? "short: gap/out of bounds" : "rolled back";
                table.AppendLine($"  {s:F1} -> {o} (air steps {res.air.Count}, strokes {res.strokes})");
                if (res.reachedPad && res.strokes == 1) landed.Add(s); else if (!res.reachedPad && res.strokes >= 2) gap.Add(s); else if (!res.reachedPad) back.Add(s);
            }
            Debug.Log(table.ToString());
            Assert.GreaterOrEqual(landed.Count, 3, "a forgiving window: several strike speeds must land");
            Assert.GreaterOrEqual(landed.Max() - landed.Min(), 1.0f, "the landing window must span at least 1 m/s of strike speed");
            Assert.GreaterOrEqual(gap.Count, 1, "a short jump must fall in the gap and be out of bounds");
            Assert.IsTrue(back.Contains(1.6f), "a soft putt just rolls back down the ramp, no penalty");
            Assert.Less(gap.Max(), landed.Min() + 0.01f, "falling short happens below the landing speeds, not above");
        }

        [UnityTest]
        public IEnumerator Launch_ShortJump_ReturnsToLastRestSpot_WithOnePenalty()
        {
            var spec = new LaunchRampSpec();
            for (float s = 2.0f; s <= 3.6f; s += 0.1f)
            {
                using var bed = new ProvingBed();
                BuildLaunch(bed, spec);
                var res = new LaunchResult();
                yield return Fly(bed, spec, s, res);
                if (res.strokes < 2) continue;
                yield return WaitUntil(() => (bed.ball.Position - bed.hole.TeePosition).magnitude < 0.05f, 4f);
                Debug.Log($"[Test] short jump at {s:F1} m/s -> strokes {bed.hole.Strokes}, ball {bed.ball.Position}, tee {bed.hole.TeePosition}");
                Assert.AreEqual(1 + bed.tuning.outOfBoundsPenalty, bed.hole.Strokes, "one stroke plus one penalty, counted once");
                Assert.That(Vector3.Distance(bed.ball.Position, bed.hole.TeePosition), Is.LessThan(0.05f), "back at the last rest spot");
                Assert.IsTrue(bed.ball.InPlay);
                Assert.IsFalse(bed.ball.IsHeld);
                yield break;
            }
            Assert.Fail("no strike speed between 2.0 and 3.6 m/s fell into the gap");
        }

        [UnityTest]
        public IEnumerator Launch_RepeatedAttempts_KeepTheBallStateConsistent()
        {
            var spec = new LaunchRampSpec();
            using var bed = new ProvingBed();
            BuildLaunch(bed, spec);
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                var res = new LaunchResult();
                yield return Fly(bed, spec, 4.0f, res);
                Assert.IsTrue(res.reachedPad, $"attempt {attempt} should land");
                Assert.IsFalse(float.IsNaN(bed.ball.Position.x + bed.ball.Position.y + bed.ball.Position.z));
                Assert.IsFalse(bed.ball.Body.isKinematic);
                if (bed.hole.IsComplete) bed.hole.BeginHole(bed.ball); else bed.hole.RequestReset();
                yield return Steps(5);
            }
        }

        [UnityTest]
        public IEnumerator Launch_RampAngleIsTunable_SteeperHopsHigher()
        {
            var results = new Dictionary<float, float>();
            foreach (float angle in new[] { 10f, 20f })
            {
                var spec = new LaunchRampSpec { rampAngleDegrees = angle, gap = 0.2f };
                using var bed = new ProvingBed();
                BuildLaunch(bed, spec);
                var res = new LaunchResult();
                yield return Fly(bed, spec, 4.2f, res);
                results[angle] = res.apexY - (spec.LipHeight + bed.tuning.ballRadius);
                Debug.Log($"[Test] ramp {angle} deg at 4.2 m/s: apex {results[angle] * 100f:F1} cm above the lip, airborne steps {res.air.Count}");
            }
            Assert.Greater(results[20f], results[10f] + 0.01f, "a steeper ramp must hop visibly higher");
        }

        // ------------------------------------------------------------------ B. waterwheel carrier

        static HoleController BuildWheel(ProvingBed bed, WaterwheelHoleSpec spec, out WaterwheelCarrier carrier)
        {
            var hole = ProvingGround.BuildWaterwheelHole(bed.root.transform, bed.tuning, bed.ball, bed.mats, spec, Vector3.zero);
            carrier = hole.GetComponentInChildren<WaterwheelCarrier>();
            return bed.Begin(hole);
        }

        [Test]
        public void Wheel_DefaultLayout_ReleaseIsOverTheExitChannel()
        {
            float r = GolfTuning.Default.ballRadius;
            var spec = new WaterwheelHoleSpec();
            Assert.AreEqual("", spec.Validate(r));
            Assert.Greater(spec.ReleasePoint(r).y, 0.6f, "the carrier must actually lift the ball");
            Assert.Greater(spec.ReleasePoint(r).z, spec.ChannelStartZ(r), "release is beyond the start of the channel");
        }

        [UnityTest]
        public IEnumerator Wheel_CapturesCarriesAndReleases_WithoutExtraStrokesOrOutOfBounds()
        {
            var spec = new WaterwheelHoleSpec();
            using var bed = new ProvingBed();
            BuildWheel(bed, spec, out var carrier);
            Vector3 releasedAt = Vector3.zero; bool released = false;
            carrier.Released += (c, b) => { released = true; releasedAt = b.Position; };
            int returns = 0;
            bed.hole.BallReturned += (h, oob) => returns++;
            yield return Steps(5);
            bed.ball.Strike(Vector3.forward * 2.0f);

            yield return WaitUntil(() => carrier.CaptureCount >= 1, 40f);
            Assert.GreaterOrEqual(carrier.CaptureCount, 1, "the ball should be scooped by a bucket");
            Assert.IsTrue(bed.ball.IsHeld);
            Assert.AreSame(carrier, bed.ball.Holder);
            Assert.IsTrue(bed.ball.Body.isKinematic);
            Assert.AreEqual(1, bed.hole.Strokes, "capture is not a stroke");
            float y0 = bed.ball.Position.y;

            yield return Steps(360);   // three seconds of carry: well up the wheel, well before the release
            Assert.IsTrue(bed.ball.IsHeld, "still being carried");
            Assert.Greater(bed.ball.Position.y, y0 + 0.15f, "carried upward");
            Assert.AreEqual(1, bed.hole.Strokes);
            Assert.IsTrue(bed.ball.InPlay);

            yield return WaitUntil(() => carrier.ReleaseCount >= 1, 40f);
            Assert.GreaterOrEqual(carrier.ReleaseCount, 1);
            Assert.IsTrue(released);
            Vector3 expected = bed.hole.transform.TransformPoint(spec.ReleasePoint(bed.tuning.ballRadius));
            Assert.That(Vector3.Distance(releasedAt, expected), Is.LessThan(0.2f), "released at the bucket's tip-out point, over the channel");
            Assert.IsFalse(bed.ball.IsHeld);
            Assert.IsFalse(bed.ball.Body.isKinematic);
            Assert.IsNull(carrier.HeldBall);
            Assert.Greater(bed.ball.Position.y, 0.6f, "the ball is now on the elevated channel");

            yield return WaitUntil(() => bed.hole.IsComplete || bed.ball.IsAtRest, 40f);
            Debug.Log($"[Test] wheel: holed {bed.hole.IsComplete}, ball {bed.ball.Position}, strokes {bed.hole.Strokes}");
            Assert.AreEqual(1, bed.hole.Strokes, "no stroke was added by the carry");
            Assert.AreEqual(0, returns, "the carry never triggered an out-of-bounds return");
            Assert.IsTrue(bed.hole.IsComplete || bed.ball.Position.y > 0.6f, "holed, or resting on the channel");
        }

        [UnityTest]
        [NUnit.Framework.Timeout(900000)]
        public IEnumerator Wheel_PuttSweep_ForgivingWindow()
        {
            // Realistic putts from the tee: speeds 1.4-2.6 m/s at three small aim errors. Logs where each ball ends up and whether the
            // wheel scooped it (a resting ball gets 9 s (two buckets) for a bucket to come by: the dock is a waiting room).
            var table = new System.Text.StringBuilder("[Test] wheel putt sweep (strike m/s, yaw deg -> outcome)\n");
            int scooped = 0, total = 0;
            for (float s = 1.4f; s <= 2.61f; s += 0.4f)
            {
                foreach (float yaw in new[] { -3f, 0f, 3f })
                {
                    using var bed = new ProvingBed();
                    BuildWheel(bed, new WaterwheelHoleSpec(), out var carrier);
                    yield return Steps(5);
                    bed.ball.Strike(Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * s);
                    yield return WaitUntil(() => carrier.CaptureCount >= 1 || bed.hole.Strokes >= 2 || bed.ball.IsAtRest, 20f);
                    bool restedFirst = bed.ball.IsAtRest && carrier.CaptureCount == 0;
                    Vector3 rest = bed.ball.Position;
                    if (restedFirst) yield return WaitUntil(() => carrier.CaptureCount >= 1 || bed.hole.Strokes >= 2, 9f);
                    total++;
                    string o = carrier.CaptureCount >= 1 ? (restedFirst ? "scooped after waiting" : "scooped on the way") :
                               bed.hole.Strokes >= 2 ? "fell off (penalty)" : $"rests at x {rest.x:F2} z {rest.z:F2}, not scooped";
                    if (carrier.CaptureCount >= 1) scooped++;
                    table.AppendLine($"  {s:F1}, {yaw:+0;-0;0} -> {o}" + (restedFirst ? $" (rested at x {rest.x:F2} z {rest.z:F2})" : ""));
                }
            }
            Debug.Log(table.ToString());
            Assert.GreaterOrEqual(scooped, total * 3 / 4, "every putt from 1.8 m/s up, even 3 degrees off line, must be scooped (only the soft 1.4 m/s putts stop short)");
        }

        [UnityTest]
        public IEnumerator Wheel_TooFastBall_IsNotCaptured_AndASecondAttemptSucceeds()
        {
            using var bed = new ProvingBed();
            BuildWheel(bed, new WaterwheelHoleSpec(), out var carrier);
            yield return Steps(5);
            bed.ball.Strike(Vector3.forward * 4.0f);
            yield return WaitUntil(() => bed.ball.IsAtRest, 40f);
            Assert.AreEqual(0, carrier.CaptureCount, "a hard putt bounces off the dock instead of being scooped");
            Assert.IsFalse(bed.ball.IsHeld);
            Assert.IsFalse(bed.ball.Body.isKinematic);
            Assert.AreEqual(1, bed.hole.Strokes);

            bed.hole.RequestReset();                 // back to the tee, no penalty, ball state intact
            yield return Steps(5);
            Assert.AreEqual(1, bed.hole.Strokes);
            bed.ball.Strike(Vector3.forward * 2.0f);
            yield return WaitUntil(() => carrier.ReleaseCount >= 1, 60f);
            Assert.GreaterOrEqual(carrier.CaptureCount, 1, "the second, gentler attempt is scooped");
            Assert.GreaterOrEqual(carrier.ReleaseCount, 1);
            Assert.AreEqual(2, bed.hole.Strokes, "two strikes, nothing else");
        }

        [UnityTest]
        public IEnumerator Wheel_ResetWhileHeld_CancelsTheHoldCleanly_AndTheNextAttemptWorks()
        {
            using var bed = new ProvingBed();
            BuildWheel(bed, new WaterwheelHoleSpec(), out var carrier);
            yield return Steps(5);
            bed.ball.Strike(Vector3.forward * 2.0f);
            yield return WaitUntil(() => carrier.CaptureCount >= 1, 40f);
            Assert.IsTrue(bed.ball.IsHeld);
            yield return Steps(40);
            bed.hole.RequestReset();
            Assert.IsFalse(bed.ball.IsHeld, "a reset must cancel the hold at once");
            Assert.IsFalse(bed.ball.Body.isKinematic);
            Assert.IsTrue(bed.ball.InPlay);
            Assert.That(Vector3.Distance(bed.ball.Position, bed.hole.TeePosition), Is.LessThan(0.05f));
            yield return Steps(3);
            Assert.IsNull(carrier.HeldBall, "the carrier forgets the ball");
            Assert.AreEqual(1, bed.hole.Strokes, "a reset adds no penalty");

            bed.ball.Strike(Vector3.forward * 2.0f);
            yield return WaitUntil(() => carrier.CaptureCount >= 2, 60f);
            Assert.GreaterOrEqual(carrier.CaptureCount, 2, "the wheel tolerates repeated attempts");
        }

        [UnityTest]
        public IEnumerator Wheel_OutOfBoundsWhileHeld_PenalisesOnce_AndReturnsTheBall()
        {
            using var bed = new ProvingBed();
            BuildWheel(bed, new WaterwheelHoleSpec(), out var carrier);
            yield return Steps(5);
            bed.ball.Strike(Vector3.forward * 2.0f);
            yield return WaitUntil(() => carrier.CaptureCount >= 1, 40f);
            Assert.IsTrue(bed.ball.IsHeld);
            bed.hole.BallOutOfBounds(bed.ball);
            yield return WaitUntil(() => (bed.ball.Position - bed.hole.LastRestPosition).magnitude < 0.05f && !bed.ball.IsHeld, 5f);
            Assert.IsFalse(bed.ball.IsHeld);
            Assert.IsFalse(bed.ball.Body.isKinematic);
            Assert.IsTrue(bed.ball.InPlay);
            Assert.AreEqual(1 + bed.tuning.outOfBoundsPenalty, bed.hole.Strokes, "exactly one penalty");
        }

        [UnityTest]
        public IEnumerator Wheel_ReleasedBall_IsOrdinaryPhysics()
        {
            using var bed = new ProvingBed();
            BuildWheel(bed, new WaterwheelHoleSpec(), out var carrier);
            yield return Steps(5);
            bed.ball.Strike(Vector3.forward * 2.0f);
            yield return WaitUntil(() => carrier.ReleaseCount >= 1, 60f);
            Assert.GreaterOrEqual(carrier.ReleaseCount, 1);
            yield return Steps(3);
            Assert.IsFalse(bed.ball.Body.isKinematic);
            float s = bed.ball.Velocity.magnitude;
            Assert.That(s, Is.InRange(0.4f, 1.6f), "leaves the bucket at about the configured release speed");
            Assert.IsFalse(bed.ball.IsHeld);
        }

        [UnityTest]
        public IEnumerator Wheel_PeriodIsTunable_ShorterPeriodCarriesFaster()
        {
            var carry = new Dictionary<float, float>();
            foreach (float period in new[] { 7f, 14f })
            {
                var spec = new WaterwheelHoleSpec(); spec.wheel.periodSeconds = period;
                using var bed = new ProvingBed();
                BuildWheel(bed, spec, out var carrier);
                float tCapture = -1f, tRelease = -1f;
                carrier.Captured += (c, b) => tCapture = Time.time;
                carrier.Released += (c, b) => tRelease = Time.time;
                yield return Steps(5);
                bed.ball.Strike(Vector3.forward * 2.0f);
                yield return WaitUntil(() => carrier.ReleaseCount >= 1, 60f);
                Assert.GreaterOrEqual(carrier.ReleaseCount, 1, $"period {period}");
                carry[period] = tRelease - tCapture;
                Debug.Log($"[Test] wheel period {period} s: carried for {carry[period]:F2} s");
            }
            float ratio = carry[14f] / carry[7f];
            Assert.That(ratio, Is.InRange(1.6f, 2.5f), "carry time scales with the wheel period");
        }

        // ------------------------------------------------------------------ C. roulette bowl

        static HoleController BuildBowl(ProvingBed bed, RouletteBowlSpec spec) =>
            bed.Begin(ProvingGround.BuildRouletteHole(bed.root.transform, bed.tuning, bed.ball, bed.mats, spec, Vector3.zero));

        [Test]
        public void Bowl_CupIsTheLowestPoint_AndTheSurfaceNeverDipsBelowTheRim()
        {
            var spec = new RouletteBowlSpec();
            var mesh = RouletteBowl.BuildMesh(spec, GolfTuning.Default.cupRadius, GolfTuning.Default.cupDepth);
            float cupR = GolfTuning.Default.cupRadius, minOutside = float.MaxValue, minInside = float.MaxValue;
            foreach (var v in mesh.vertices)
            {
                float r = Mathf.Sqrt(v.x * v.x + v.z * v.z);
                if (r > cupR + 1e-3f) minOutside = Mathf.Min(minOutside, v.y); else minInside = Mathf.Min(minInside, v.y);
            }
            Assert.That(minInside, Is.EqualTo(-GolfTuning.Default.cupDepth).Within(1e-4f), "the cup pit is the deepest point");
            Assert.GreaterOrEqual(minOutside, -1e-4f, "no part of the bowl is lower than the cup rim");
            float prev = -1f;
            for (float r = 0f; r <= spec.radius; r += 0.02f)
            {
                float h = spec.Height(r);
                Assert.GreaterOrEqual(h, prev - 1e-6f, "the surface only rises away from the cup");
                prev = h;
            }
        }

        [UnityTest]
        public IEnumerator Bowl_BallPlacedOnTheCone_RollsDownToward_TheCup()
        {
            var spec = new RouletteBowlSpec();
            using var bed = new ProvingBed();
            BuildBowl(bed, spec);
            yield return Steps(5);
            float r0 = 1.0f;
            float y = spec.Height(r0) + bed.tuning.ballRadius * 1.05f;
            bed.ball.PlaceAt(new Vector3(r0, y, 0f));
            yield return WaitUntil(() => false, 3f);
            float r1 = new Vector2(bed.ball.Position.x, bed.ball.Position.z).magnitude;
            Debug.Log($"[Test] bowl: ball released at r {r0:F2} m is at r {r1:F2} m after 3 s (holed {bed.hole.IsComplete})");
            Assert.IsTrue(bed.hole.IsComplete || r1 < r0 - 0.2f, "a ball cannot rest on the cone; the bowl is genuinely sloped");
        }

        [UnityTest]
        public IEnumerator Bowl_BallOnTheRimShelf_CanRest_SoItCanBeStruckAgain()
        {
            var spec = new RouletteBowlSpec();
            using var bed = new ProvingBed();
            BuildBowl(bed, spec);
            yield return Steps(5);
            Vector3 tee = bed.hole.TeePosition;
            yield return WaitUntil(() => false, 2f);
            Assert.That(Vector3.Distance(bed.ball.Position, tee), Is.LessThan(0.05f), "the tee is on the rim shelf and the ball stays there");
            Assert.IsTrue(bed.ball.IsAtRest);
        }

        [UnityTest]
        public IEnumerator Bowl_StruckAlongTheRim_OrbitsAndSettles_WithNoEnergyGain_AndNeverRestsOnTheCone()
        {
            var spec = new RouletteBowlSpec();
            using var bed = new ProvingBed();
            var hole = BuildBowl(bed, spec);
            float g = Mathf.Abs(Physics.gravity.y), k = 5f / 7f * g;
            float apron = spec.apronRadius, shelfIn = spec.ShelfInnerRadius;
            int holed = 0, onApron = 0, onShelf = 0, total = 0;
            var log = new System.Text.StringBuilder("[Test] bowl strikes (m/s, yaw offset deg -> outcome)\n");
            foreach (float speed in new[] { 1.5f, 2.5f })
            foreach (float yaw in new[] { -4f, 0f, 4f, 8f })
            {
                hole.BeginHole(bed.ball);
                yield return Steps(5);
                Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;     // tangent at the tee, rotated a little outward (+) or inward (-)
                bed.ball.Strike(dir * speed);
                float E0 = 0.5f * speed * speed + k * (bed.ball.Position.y - bed.tuning.ballRadius);
                float worst = 0f, end = Time.time + 40f;
                while (Time.time < end && !hole.IsComplete)
                {
                    yield return new WaitForFixedUpdate();
                    float E = 0.5f * bed.ball.Velocity.sqrMagnitude + k * (bed.ball.Position.y - bed.tuning.ballRadius);
                    worst = Mathf.Max(worst, E - E0);
                    if (bed.ball.IsAtRest && bed.ball.InPlay) break;
                }
                float r = new Vector2(bed.ball.Position.x, bed.ball.Position.z).magnitude;
                string outcome;
                if (hole.IsComplete) { holed++; outcome = "holed"; }
                else if (r <= apron + 0.06f) { onApron++; outcome = $"apron r={r:F2}"; }
                else if (r >= shelfIn - 0.06f) { onShelf++; outcome = $"rim shelf r={r:F2}"; }
                else outcome = $"DEAD ZONE r={r:F2}";
                log.AppendLine($"  {speed:F1}, {yaw:F0} -> {outcome} (max energy gain {worst:F3} J/kg, strokes {hole.Strokes})");
                total++;
                Assert.That(worst, Is.LessThan(0.3f), "mechanical energy must never rise: nothing pulls the ball toward the cup");
                Assert.IsFalse(outcome.StartsWith("DEAD"), $"a ball must not come to rest on the cone ({speed} m/s, yaw {yaw}): {outcome}");
            }
            Debug.Log(log + $"holed {holed}, apron {onApron}, shelf {onShelf} of {total}");
            Assert.GreaterOrEqual(holed + onApron, 1, "the cup is reachable: some strokes end holed or on the apron");
        }

        [Test]
        public void Bowl_HasNoScriptedBehaviour()
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var name in new[] { "Update", "FixedUpdate", "LateUpdate", "OnTriggerStay", "OnTriggerEnter", "OnCollisionStay", "OnCollisionEnter" })
                Assert.IsNull(typeof(RouletteBowl).GetMethod(name, flags), $"RouletteBowl must not implement {name}: it only builds geometry");
            using var bed = new ProvingBed();
            var hole = BuildBowl(bed, new RouletteBowlSpec());
            Assert.AreEqual(0, hole.GetComponentInChildren<RouletteBowl>().GetComponentsInChildren<Rigidbody>(true).Length, "no rigidbody drives the bowl");
        }

        [Test]
        public void Bowl_Presets_Build_WithACup_AndATeeOnTheRimShelf()
        {
            foreach (ProvingPreset p in Enum.GetValues(typeof(ProvingPreset)))
            {
                using var bed = new ProvingBed();
                var spec = ProvingGround.BowlSpec(p);
                var hole = BuildBowl(bed, spec);
                Assert.IsNotNull(hole.Cup, $"{p}: bowl needs a cup");
                float r = new Vector2(hole.TeePosition.x, hole.TeePosition.z).magnitude;
                Assert.That(r, Is.InRange(spec.ShelfInnerRadius, spec.radius), $"{p}: the tee is on the rim shelf");
            }
        }

        [Test]
        public void BuildAll_ProducesThreeHoles_WithCups()
        {
            using var bed = new ProvingBed();
            var holes = ProvingGround.BuildAll(bed.root.transform, bed.tuning, bed.ball, bed.mats, ProvingPreset.Default);
            Assert.AreEqual(3, holes.Length);
            foreach (var h in holes) Assert.IsNotNull(h.Cup);
        }
    }
}
