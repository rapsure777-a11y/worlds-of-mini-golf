using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Gamebreak.MiniGolf.Tests
{
    /// <summary>A throwaway green with a ball and hole, built at runtime with the real course code.</summary>
    class TestBed : IDisposable
    {
        public readonly GolfTuning tuning;
        public readonly GolfBall ball;
        public readonly HoleController hole;
        readonly GameObject m_Root;

        public TestBed(GreenLayout layout, Vector2 tee)
        {
            tuning = ScriptableObject.CreateInstance<GolfTuning>();
            GolfPhysicsBootstrap.Apply(tuning);
            m_Root = new GameObject("TestBed");
            var ballGo = new GameObject("Ball");
            ballGo.transform.SetParent(m_Root.transform);
            ballGo.AddComponent<Rigidbody>();
            ballGo.AddComponent<SphereCollider>();
            ball = ballGo.AddComponent<GolfBall>();
            ball.SetTuning(tuning);
            var def = new HoleDefinition { number = 1, name = "Test", par = 2, layout = layout, tee = tee, origin = Vector3.zero };
            hole = HoleFactory.Build(def, null, tuning, m_Root.transform, ball);
            hole.BeginHole(ball);
            Physics.SyncTransforms();
        }

        public void Dispose()
        {
            Object.Destroy(m_Root);
            Object.Destroy(tuning);
        }
    }

    public class GolfPhysicsTests
    {
        static IEnumerator WaitUntil(Func<bool> condition, float timeout)
        {
            float end = Time.time + timeout;
            while (!condition() && Time.time < end) yield return new WaitForFixedUpdate();
        }

        static IEnumerator Settle(int steps = 5)
        {
            for (int i = 0; i < steps; i++) yield return new WaitForFixedUpdate();
        }

        static GreenLayout Flat(float width, float length, Vector2? cup = null)
        {
            var l = new GreenLayout();
            l.Area(-width / 2f, 0f, width, length);
            l.cup = cup;
            return l;
        }

        [SetUp] public void SetUp() => Time.timeScale = 3f;
        [TearDown] public void TearDown() => Time.timeScale = 1f;

        [UnityTest]
        public IEnumerator RollOut_MatchesRollingModel()
        {
            using var bed = new TestBed(Flat(1.2f, 12f), new Vector2(0f, 1f));
            yield return Settle();
            Vector3 start = bed.ball.Position;
            bed.ball.Strike(new Vector3(0f, 0f, 2f));
            yield return new WaitForFixedUpdate();
            yield return WaitUntil(() => bed.ball.IsAtRest, 15f);
            Assert.IsTrue(bed.ball.IsAtRest, "ball never came to rest");
            float d = Vector3.Distance(start, bed.ball.Position);
            // Analytic roll-out for a = a0 + k v from 2 m/s with default tuning is ~3.06 m.
            float a0 = bed.tuning.rollingDeceleration, k = bed.tuning.speedDrag, v = 2f;
            float expected = (v - a0 / k * Mathf.Log(1f + k * v / a0)) / k;
            Debug.Log($"[Test] roll-out {d:F3} m, expected {expected:F3} m");
            Assert.That(d, Is.InRange(expected * 0.9f, expected * 1.1f));
            Assert.AreEqual(1, bed.hole.Strokes);
        }

        [UnityTest]
        public IEnumerator PuttAtCup_IsHoled()
        {
            using var bed = new TestBed(Flat(1.2f, 5f, new Vector2(0f, 3f)), new Vector2(0f, 0.5f));
            yield return Settle();
            Vector3 cup = bed.hole.Cup.transform.position;
            Vector3 dir = cup - bed.ball.Position; dir.y = 0f;
            bed.ball.Strike(dir.normalized * 2.0f);
            yield return WaitUntil(() => bed.hole.IsComplete, 8f);
            Assert.IsTrue(bed.hole.IsComplete, $"not holed, ball at {bed.ball.Position}");
            Assert.AreEqual(1, bed.hole.Strokes);
        }

        [UnityTest]
        public IEnumerator FastPutt_SkipsOverCup()
        {
            using var bed = new TestBed(Flat(1.2f, 8f, new Vector2(0f, 3f)), new Vector2(0f, 0.5f));
            yield return Settle();
            Vector3 cup = bed.hole.Cup.transform.position;
            Vector3 dir = cup - bed.ball.Position; dir.y = 0f;
            bed.ball.Strike(dir.normalized * 5f);
            var trace = new System.Text.StringBuilder();
            float end = Time.time + 4f;
            int step = 0;
            while (!(bed.ball.Position.z > cup.z + 0.5f || bed.hole.IsComplete) && Time.time < end)
            {
                yield return new WaitForFixedUpdate();
                if (step++ % 4 == 0) trace.AppendLine($"{step} {bed.ball.Position:F4} {bed.ball.Velocity:F3} inPlay {bed.ball.InPlay}");
            }
            Debug.Log("[Test] fast putt trace\n" + trace);
            Assert.IsFalse(bed.hole.IsComplete, "a 5 m/s putt should not drop");
            Assert.Greater(bed.ball.Position.z, cup.z + 0.5f);
        }

        [UnityTest]
        public IEnumerator WallRebound_UsesRestitution()
        {
            using var bed = new TestBed(Flat(1.2f, 3f), new Vector2(0f, 2f));
            yield return Settle();
            bed.ball.Strike(new Vector3(0f, 0f, 3f));
            float vIn = 0f, vOut = 0f;
            bed.ball.HitWall += (b, impact) => vIn = impact;
            float end = Time.time + 3f;
            while (Time.time < end)
            {
                yield return new WaitForFixedUpdate();
                if (vIn > 0f) { vOut = -bed.ball.Velocity.z; break; }
            }
            float ratio = vOut / Mathf.Max(vIn, 1e-3f);
            Debug.Log($"[Test] rebound in {vIn:F2} out {vOut:F2} ratio {ratio:F2}");
            Assert.That(ratio, Is.InRange(bed.tuning.wallBounciness - 0.08f, bed.tuning.wallBounciness + 0.05f));
        }

        [UnityTest]
        public IEnumerator OutOfBounds_ReturnsBallWithPenalty()
        {
            using var bed = new TestBed(Flat(1.2f, 6f), new Vector2(0f, 0.5f));
            yield return Settle();
            bed.ball.Strike(new Vector3(0f, 0f, 1f));
            yield return new WaitForFixedUpdate();
            yield return WaitUntil(() => bed.ball.IsAtRest, 6f);
            Vector3 rest = bed.ball.Position;
            bed.ball.PlaceAt(new Vector3(0f, -10f, 0f)); // fell off the world
            yield return WaitUntil(() => (bed.ball.Position - rest).sqrMagnitude < 1e-4f, 3f);
            Assert.Less((bed.ball.Position - rest).magnitude, 0.01f, "ball not returned to last rest spot");
            Assert.AreEqual(1 + bed.tuning.outOfBoundsPenalty, bed.hole.Strokes);
        }

        [UnityTest]
        public IEnumerator GentleSlope_HoldsBall_SteepSlope_RollsIt()
        {
            var gentle = Flat(1.2f, 4f); gentle.height = (x, z) => -z * 0.02f;
            using (var bed = new TestBed(gentle, new Vector2(0f, 2f)))
            {
                yield return Settle(10);
                Vector3 p = bed.ball.Position;
                yield return new WaitForSeconds(1f);
                Assert.Less((bed.ball.Position - p).magnitude, 0.01f, "ball crept on a 2% slope");
            }
            var steep = Flat(1.2f, 4f); steep.height = (x, z) => -z * 0.12f;
            using (var bed = new TestBed(steep, new Vector2(0f, 1f)))
            {
                yield return Settle(10);
                Vector3 p = bed.ball.Position;
                yield return new WaitForSeconds(1f);
                Assert.Greater((bed.ball.Position - p).magnitude, 0.05f, "ball stuck on a 12% slope");
            }
        }
    }

    public class PutterTests
    {
        [UnityTest]
        public IEnumerator Swing_StrikesBallAlongFace()
        {
            Time.timeScale = 1f;
            using var bed = new TestBed(FlatLane(), new Vector2(0f, 1f));
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();

            var putter = new GameObject("Putter").AddComponent<Putter>();
            putter.Ball = bed.ball;
            putter.SetAdjustments(0.85f, 0f, 0f);
            // Shaft straight down, face normal (+X local) pointing down the lane (+Z world).
            putter.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.left);

            Vector3 ballPos = bed.ball.Position;
            float speed = 1.5f;
            float z = ballPos.z - 0.35f;
            float end = Time.time + 2f;
            while (Time.time < end && bed.hole.Strokes == 0)
            {
                z += speed * Time.deltaTime;
                putter.transform.position = new Vector3(ballPos.x, ballPos.y + 0.85f, z);
                yield return null;
            }
            Object.Destroy(putter.gameObject);

            Assert.AreEqual(1, bed.hole.Strokes, "putter never struck the ball");
            yield return new WaitForFixedUpdate();
            Vector3 v = bed.ball.Velocity;
            Debug.Log($"[Test] putter 1.5 m/s -> ball {v.magnitude:F2} m/s dir {v.normalized}");
            Assert.Greater(v.z, 0f);
            Assert.Less(Mathf.Abs(v.x), 0.05f * v.magnitude);
            Assert.That(v.magnitude, Is.InRange(speed * 1.35f * 0.75f, speed * 1.35f * 1.25f));
        }

        [UnityTest]
        public IEnumerator OpenFace_PushesBallOffLine()
        {
            Time.timeScale = 1f;
            using var bed = new TestBed(FlatLane(), new Vector2(0f, 1f));
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();

            var putter = new GameObject("Putter").AddComponent<Putter>();
            putter.Ball = bed.ball;
            putter.SetAdjustments(0.85f, 0f, 0f);
            // Face turned 10 degrees to the right (toward +X).
            putter.transform.rotation = Quaternion.Euler(0f, 10f, 0f) * Quaternion.LookRotation(Vector3.down, Vector3.left);

            Vector3 ballPos = bed.ball.Position;
            float z = ballPos.z - 0.35f;
            float end = Time.time + 2f;
            while (Time.time < end && bed.hole.Strokes == 0)
            {
                z += 1.5f * Time.deltaTime;
                putter.transform.position = new Vector3(ballPos.x, ballPos.y + 0.85f, z);
                yield return null;
            }
            Object.Destroy(putter.gameObject);
            yield return new WaitForFixedUpdate();

            Vector3 v = bed.ball.Velocity;
            float angle = Vector3.SignedAngle(Vector3.forward, new Vector3(v.x, 0f, v.z), Vector3.up);
            Debug.Log($"[Test] 10 deg open face -> launch angle {angle:F1} deg");
            Assert.AreEqual(1, bed.hole.Strokes);
            Assert.That(angle, Is.InRange(6f, 11f));
        }

        static GreenLayout FlatLane()
        {
            var l = new GreenLayout();
            l.Area(-0.6f, 0f, 1.2f, 8f);
            return l;
        }
    }
}


