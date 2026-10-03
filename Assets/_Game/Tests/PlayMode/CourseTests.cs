using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Gamebreak.MiniGolf.Tests
{
    /// <summary>Rules, scoring, progression and cup edge cases.</summary>
    public class CourseTests
    {
        static IEnumerator WaitUntil(Func<bool> condition, float timeout)
        {
            float end = Time.time + timeout;
            while (!condition() && Time.time < end) yield return new WaitForFixedUpdate();
        }

        static IEnumerator Steps(int n)
        {
            for (int i = 0; i < n; i++) yield return new WaitForFixedUpdate();
        }

        static Vector3 Toward(GolfBall ball, Vector3 target, float speed)
        {
            Vector3 d = target - ball.Position; d.y = 0f;
            return d.normalized * speed;
        }

        [SetUp] public void SetUp() => Time.timeScale = 3f;
        [TearDown] public void TearDown() => Time.timeScale = 1f;

        /// <summary>Two straight holes side by side sharing one ball, run by a CourseController.</summary>
        class TwoHoleCourse : IDisposable
        {
            public readonly GolfTuning tuning;
            public readonly GolfBall ball;
            public readonly CourseController course;
            readonly GameObject m_Root;

            public TwoHoleCourse()
            {
                tuning = ScriptableObject.CreateInstance<GolfTuning>();
                GolfPhysicsBootstrap.Apply(tuning);
                m_Root = new GameObject("TwoHoleCourse");
                var ballGo = new GameObject("Ball");
                ballGo.transform.SetParent(m_Root.transform);
                ballGo.AddComponent<Rigidbody>();
                ballGo.AddComponent<SphereCollider>();
                ball = ballGo.AddComponent<GolfBall>();
                ball.SetTuning(tuning);
                var holes = new HoleController[2];
                for (int i = 0; i < 2; i++)
                {
                    var l = new GreenLayout();
                    l.Area(-0.6f, 0f, 1.2f, 4f);
                    l.cup = new Vector2(0f, 2.5f);
                    var def = new HoleDefinition { number = i + 1, name = "H" + (i + 1), par = 2 + i, layout = l, tee = new Vector2(0f, 0.5f), origin = new Vector3(i * 5f, 0f, 0f) };
                    holes[i] = HoleFactory.Build(def, null, tuning, m_Root.transform, ball);
                }
                var go = new GameObject("Course");
                go.transform.SetParent(m_Root.transform);
                course = go.AddComponent<CourseController>();
                course.StartOnAwake = false;
                course.AdvanceDelay = 0.5f;
                course.Configure("Test", holes, ball, null);
                course.StartCourse();
            }

            public void Dispose()
            {
                Object.Destroy(m_Root);
                Object.Destroy(tuning);
            }
        }

        [UnityTest]
        public IEnumerator Course_RecordsScoresAndAdvances()
        {
            using var c = new TwoHoleCourse();
            yield return Steps(5);
            Assert.AreEqual(0, c.course.CurrentIndex);

            // Hole 1: miss short, then hole out. 2 strokes.
            c.ball.Strike(Toward(c.ball, c.course.Current.Cup.transform.position, 1.0f));
            yield return Steps(1);
            yield return WaitUntil(() => c.ball.IsAtRest, 8f);
            Assert.IsFalse(c.course.Current.IsComplete);
            c.ball.Strike(Toward(c.ball, c.course.Current.Cup.transform.position, 1.6f));
            yield return WaitUntil(() => c.course.Current.IsComplete, 8f);
            Assert.IsTrue(c.course.Current.IsComplete, "hole 1 not completed");
            Assert.AreEqual(2, c.course.Card.strokes[0]);

            // Advances to hole 2 and puts the ball on its tee.
            yield return WaitUntil(() => c.course.CurrentIndex == 1, 3f);
            Assert.AreEqual(1, c.course.CurrentIndex);
            Assert.Less(Vector3.Distance(c.ball.Position, c.course.Current.TeePosition), 0.01f);
            Assert.AreEqual(0, c.course.Current.Strokes);

            // Hole 2: hole in one.
            yield return Steps(3);
            c.ball.Strike(Toward(c.ball, c.course.Current.Cup.transform.position, 1.9f));
            yield return WaitUntil(() => c.course.Finished, 10f);
            Assert.IsTrue(c.course.Finished, "course did not finish");
            Assert.AreEqual(1, c.course.Card.strokes[1]);
            Assert.AreEqual(3, c.course.Card.TotalStrokes);
            Assert.AreEqual(5, c.course.Card.TotalParPlayed);
        }

        [UnityTest]
        public IEnumerator RestartDuringAdvanceDelay_DoesNotSkipHole()
        {
            using var c = new TwoHoleCourse();
            c.course.AdvanceDelay = 1.5f;
            yield return Steps(5);
            c.ball.Strike(Toward(c.ball, c.course.Current.Cup.transform.position, 1.9f));
            yield return WaitUntil(() => c.course.Current.IsComplete, 8f);
            c.course.RestartHole();
            yield return new WaitForSeconds(2.5f);
            Assert.AreEqual(0, c.course.CurrentIndex, "pending advance fired after restart");
            Assert.IsFalse(c.course.Current.IsComplete);
        }

        [UnityTest]
        public IEnumerator StrokeLimit_EndsHole()
        {
            using var c = new TwoHoleCourse();
            c.tuning.strokeLimit = 2;
            yield return Steps(5);
            var hole = c.course.Current;
            for (int i = 0; i < 2; i++)
            {
                c.ball.Strike(new Vector3(0.3f, 0f, -0.1f)); // away from the cup
                yield return Steps(1);
                yield return WaitUntil(() => c.ball.IsAtRest || hole.IsComplete, 6f);
            }
            Assert.IsTrue(hole.IsComplete, "hole should end at the stroke limit");
            Assert.AreEqual(2, c.course.Card.strokes[0]);
        }

        [UnityTest]
        public IEnumerator PlayerReset_ReturnsBallWithoutPenalty()
        {
            using var c = new TwoHoleCourse();
            yield return Steps(5);
            var hole = c.course.Current;
            Vector3 tee = c.ball.Position;
            c.ball.Strike(new Vector3(0f, 0f, 0.8f));
            yield return Steps(10);
            hole.RequestReset(); // while still rolling: back to the last rest spot (the tee)
            yield return Steps(3);
            Assert.Less(Vector3.Distance(c.ball.Position, tee), 0.01f);
            Assert.AreEqual(1, hole.Strokes);
            Assert.IsTrue(c.ball.InPlay);
        }

        [UnityTest]
        public IEnumerator PlayerReset_AfterBallStops_ReturnsToLastShotSpot()
        {
            using var c = new TwoHoleCourse();
            yield return Steps(5);
            var hole = c.course.Current;
            Vector3 tee = c.ball.Position;
            c.ball.Strike(new Vector3(0.1f, 0f, 1.0f));
            yield return Steps(1);
            yield return WaitUntil(() => c.ball.IsAtRest, 8f);
            Vector3 firstRest = c.ball.Position;
            Assert.Greater(Vector3.Distance(firstRest, tee), 0.3f);

            hole.RequestReset(); // ball is at rest: must still visibly return, to where it was hit from
            yield return Steps(3);
            Assert.Less(Vector3.Distance(c.ball.Position, tee), 0.01f, "should return to the shot's start (the tee)");
            Assert.AreEqual(1, hole.Strokes, "the shot still counts, no extra penalty");

            // Second shot from the tee, then reset: back to the tee again, not to the first rest spot.
            c.ball.Strike(new Vector3(-0.1f, 0f, 0.6f));
            yield return Steps(1);
            yield return WaitUntil(() => c.ball.IsAtRest, 8f);
            hole.RequestReset();
            yield return Steps(3);
            Assert.Less(Vector3.Distance(c.ball.Position, tee), 0.01f);
            Assert.AreEqual(2, hole.Strokes);
        }

        [UnityTest]
        public IEnumerator SlowBallAtCupEdge_Drops()
        {
            using var c = new TwoHoleCourse();
            yield return Steps(5);
            var hole = c.course.Current;
            Vector3 cup = hole.Cup.transform.position;
            // Off-centre by 3 cm, dying speed: should catch the edge and fall in.
            c.ball.PlaceAt(new Vector3(cup.x + 0.03f, c.ball.Position.y, cup.z - 0.4f));
            yield return Steps(3);
            c.ball.Strike(new Vector3(0f, 0f, 0.75f));
            yield return WaitUntil(() => hole.IsComplete || (c.ball.IsAtRest && c.ball.Position.z > cup.z - 0.39f), 6f);
            Assert.IsTrue(hole.IsComplete, $"edge putt did not drop, ball at {c.ball.Position - cup}");
        }

        [UnityTest]
        public IEnumerator GlancingFastBall_LipsOut()
        {
            using var c = new TwoHoleCourse();
            yield return Steps(5);
            var hole = c.course.Current;
            Vector3 cup = hole.Cup.transform.position;
            c.ball.PlaceAt(new Vector3(cup.x + 0.045f, c.ball.Position.y, cup.z - 0.6f));
            yield return Steps(3);
            c.ball.Strike(new Vector3(0f, 0f, 2.6f));
            yield return WaitUntil(() => hole.IsComplete || c.ball.Position.z > cup.z + 0.4f, 4f);
            Assert.IsFalse(hole.IsComplete, "fast glancing ball should not drop");
        }

        [UnityTest]
        public IEnumerator AngledRailHit_ReflectsAngle()
        {
            using var bed = new TestBed(Flat(), new Vector2(0f, 1f));
            yield return Steps(5);
            // 45 degrees into the +X rail.
            var v = new Vector3(1.5f, 0f, 1.5f);
            float impact = 0f;
            bed.ball.HitWall += (b, s) => impact = s;
            bed.ball.Strike(v);
            yield return WaitUntil(() => impact > 0f, 3f);
            yield return Steps(1);
            Vector3 after = bed.ball.Velocity;
            float angleOut = Vector3.SignedAngle(Vector3.forward, new Vector3(after.x, 0f, after.z), Vector3.up);
            Debug.Log($"[Test] 45° rail hit: out {after:F2}, angle {angleOut:F1}, impact {impact:F2}");
            Assert.Less(after.x, 0f, "x should reverse");
            Assert.Greater(after.z, 0f, "z should continue");
            // Restitution 0.72 on the normal and 0.94 on the tangent => about -37° off the rail.
            Assert.That(angleOut, Is.InRange(-45f, -30f));
        }

        [UnityTest]
        public IEnumerator BallOnRampClimbsAndRollsBack()
        {
            var l = Flat();
            l.height = (x, z) => Slopes.RampZ(z, 2f, 3f, 0f, 0.15f);
            using var bed = new TestBed(l, new Vector2(0f, 1f));
            yield return Steps(5);
            bed.ball.Strike(new Vector3(0f, 0f, 1.3f)); // not enough to crest 15 cm
            float maxZ = 0f;
            float end = Time.time + 6f;
            while (Time.time < end && !(bed.ball.IsAtRest && maxZ > 2f))
            {
                maxZ = Mathf.Max(maxZ, bed.ball.Position.z);
                yield return new WaitForFixedUpdate();
            }
            Debug.Log($"[Test] ramp: max z {maxZ:F2}, rest z {bed.ball.Position.z:F2}");
            Assert.Greater(maxZ, 2.1f, "ball should climb onto the ramp");
            Assert.Less(maxZ, 3f, "ball should not crest the ramp");
            Assert.Less(bed.ball.Position.z, 2.05f, "ball should roll back down");
        }

        [UnityTest]
        public IEnumerator BallStoppingOnRailTop_IsOutOfBounds()
        {
            using var bed = new TestBed(Flat(), new Vector2(0f, 1f));
            yield return Steps(5);
            // Rail top: inner face at x = 0.6, 8 cm thick, 9 cm high.
            var railTop = new Vector3(0.64f, 0.09f + bed.ball.Radius + 0.001f, 3f);
            bed.ball.PlaceAt(railTop);
            bed.ball.Strike(new Vector3(0f, 0f, 0.05f)); // tiny nudge so it settles and "stops" up there
            yield return WaitUntil(() => bed.hole.Strokes >= 2, 4f);
            Assert.AreEqual(1 + bed.tuning.outOfBoundsPenalty, bed.hole.Strokes, "resting on a rail should cost a penalty");
            yield return WaitUntil(() => bed.ball.InPlay, 2f);
            Assert.Less(Vector3.Distance(bed.ball.Position, bed.hole.TeePosition), 0.01f, "ball should return to last legal spot");
        }

        [UnityTest]
        public IEnumerator StuckBall_IsForceStopped()
        {
            // A bowl the ball can rock in forever is approximated by a very long, slow roll.
            var l = Flat();
            l.Area(-0.6f, 0f, 1.2f, 40f);
            using var bed = new TestBed(l, new Vector2(0f, 1f));
            bed.tuning.rollingDeceleration = 0.005f;
            bed.tuning.speedDrag = 0f;
            yield return Steps(5);
            bed.ball.Strike(new Vector3(0f, 0f, 0.25f));
            yield return Steps(1);
            yield return WaitUntil(() => bed.ball.IsAtRest, 30f);
            Assert.IsTrue(bed.ball.IsAtRest, "safeguard should stop a ball that keeps creeping");
        }

        [UnityTest]
        public IEnumerator MaxSpeedRailHits_NeverTunnel([Values(0f, 30f, 60f, 80f)] float angle)
        {
            using var bed = new TestBed(Flat(), new Vector2(0f, 3f));
            yield return Steps(5);
            // Max-speed shot at the +Z end rail (0 = straight on, 80 = nearly parallel).
            var dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            bed.ball.Strike(dir * bed.tuning.maxBallSpeed);
            float end = Time.time + 4f;
            bool escaped = false;
            while (Time.time < end && !bed.ball.IsAtRest)
            {
                var p = bed.ball.Position;
                if (Mathf.Abs(p.x) > 0.6f || p.z < 0f || p.z > 6f || p.y < -0.05f || p.y > 0.3f) { escaped = true; break; }
                yield return new WaitForFixedUpdate();
            }
            Assert.IsFalse(escaped, $"ball left the green at {bed.ball.Position} (angle {angle})");
            Assert.AreEqual(1, bed.hole.Strokes, "no out-of-bounds penalty expected");
        }

        [UnityTest]
        public IEnumerator DoglegCorner_BallStaysInPlay()
        {
            // Same shape as Hole 2: lane up +Z, cross lane to the left at the far end.
            var l = new GreenLayout();
            l.Area(-0.6f, 0f, 1.2f, 5.2f);
            l.Area(-3.6f, 4.0f, 4.2f, 1.2f);
            using var bed = new TestBed(l, new Vector2(0f, 0.6f));
            yield return Steps(5);
            int wallHits = 0;
            bed.ball.HitWall += (b, s) => wallHits++;
            // Firm shot into the far corner, then off the rails into the cross lane.
            bed.ball.Strike(new Vector3(-0.25f, 0f, 1f).normalized * 6f);
            float end = Time.time + 15f;
            while (Time.time < end && !bed.ball.IsAtRest)
            {
                Assert.IsTrue(bed.ball.InPlay, "ball went out of bounds");
                yield return new WaitForFixedUpdate();
            }
            Assert.IsTrue(bed.ball.IsAtRest);
            Assert.AreEqual(1, bed.hole.Strokes);
            Assert.Greater(wallHits, 0);
            Debug.Log($"[Test] dogleg: rest at {bed.ball.Position:F2} after {wallHits} rail hits");
        }

        static GreenLayout Flat()
        {
            var l = new GreenLayout();
            l.Area(-0.6f, 0f, 1.2f, 6f);
            return l;
        }
    }

    /// <summary>Loads the generated Tropical scene and plays it, catching wiring errors.</summary>
    public class SceneIntegrationTests
    {
        [UnityTest]
        public IEnumerator TropicalScene_LoadsAndHoleOneIsPlayable()
        {
            Time.timeScale = 3f;
            SceneManager.LoadScene("TropicalAdventure");
            yield return null;
            yield return null;
            var course = Object.FindFirstObjectByType<CourseController>();
            Assert.IsNotNull(course, "no CourseController in scene");
            Assert.AreEqual(0, course.CurrentIndex, "course did not start on hole 1");
            var hole = course.Current;
            var ball = hole.Ball;
            Assert.IsNotNull(hole.Cup);
            Assert.Less(Vector3.Distance(ball.Position, hole.TeePosition), 0.02f, "ball not on tee");
            var rig = Object.FindFirstObjectByType<VRRig>();
            Assert.IsNotNull(rig);
            Assert.IsNotNull(rig.Putter);

            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
            Vector3 d = hole.Cup.transform.position - ball.Position; d.y = 0f;
            // Speed chosen to reach the cup over the rise with some pace left.
            ball.Strike(d.normalized * 3.2f);
            float end = Time.time + 12f;
            while (!hole.IsComplete && Time.time < end) yield return new WaitForFixedUpdate();
            Assert.IsTrue(hole.IsComplete, $"hole 1 not holed, ball at {ball.Position}, cup {hole.Cup.transform.position}");
            Assert.AreEqual(1, course.Card.strokes[0]);
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator TropicalScene_ProgressesToHoleTwoAndFinishes()
        {
            Time.timeScale = 3f;
            SceneManager.LoadScene("TropicalAdventure");
            yield return null;
            yield return null;
            var course = Object.FindFirstObjectByType<CourseController>();
            Assert.GreaterOrEqual(course.Holes.Length, 2, "scene should have at least two holes");
            course.AdvanceDelay = 0.5f;

            for (int h = 0; h < course.Holes.Length; h++)
            {
                var hole = course.Current;
                Assert.AreEqual(h, course.CurrentIndex);
                var ball = hole.Ball;
                for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
                // Drop the ball 40 cm short of the cup on the line from the tee and roll it in.
                Vector3 cup = hole.Cup.transform.position;
                Vector3 back = hole.TeePosition - cup; back.y = 0f;
                if (h == 1) back = Vector3.right; // dogleg: approach along the cross lane
                Vector3 start = cup + back.normalized * 0.4f;
                start.y = cup.y + ball.Radius + 0.002f;
                ball.PlaceAt(start);
                for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
                Vector3 d = cup - ball.Position; d.y = 0f;
                ball.Strike(d.normalized * 1.1f);
                float end = Time.time + 8f;
                while (!hole.IsComplete && Time.time < end) yield return new WaitForFixedUpdate();
                Assert.IsTrue(hole.IsComplete, $"hole {h + 1} not holed, ball {ball.Position}, cup {cup}");
                if (h + 1 < course.Holes.Length)
                {
                    end = Time.time + 4f;
                    while (course.CurrentIndex == h && Time.time < end) yield return null;
                }
            }
            float fin = Time.time + 4f;
            while (!course.Finished && Time.time < fin) yield return null;
            Assert.IsTrue(course.Finished);
            Time.timeScale = 1f;
        }
    }
}
