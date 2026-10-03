using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamebreak.MiniGolf.Tests
{
    /// <summary>Putter cases that must NOT strike, or strike only once.</summary>
    public class PutterEdgeTests
    {
        static readonly Quaternion FaceForward = Quaternion.LookRotation(Vector3.down, Vector3.left);

        static GreenLayout Lane()
        {
            var l = new GreenLayout();
            l.Area(-0.6f, 0f, 1.2f, 8f);
            return l;
        }

        static Putter MakePutter(GolfBall ball)
        {
            var p = new GameObject("Putter").AddComponent<Putter>();
            p.Ball = ball;
            p.SetAdjustments(0.85f, 0f, 0f);
            p.transform.rotation = FaceForward;
            return p;
        }

        /// <summary>Moves the head along z from start to end at speed, one step per frame.</summary>
        static IEnumerator Sweep(Putter p, Vector3 ball, float startZ, float endZ, float speed)
        {
            float z = startZ, dir = Mathf.Sign(endZ - startZ);
            p.transform.position = new Vector3(ball.x, ball.y + 0.85f, z);
            p.ResetTracking();
            yield return null;
            while ((endZ - z) * dir > 0f)
            {
                z += dir * speed * Time.deltaTime;
                p.transform.position = new Vector3(ball.x, ball.y + 0.85f, z);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator SlowNudge_BelowThreshold_DoesNotStrike()
        {
            Time.timeScale = 1f;
            using var bed = new TestBed(Lane(), new Vector2(0f, 1f));
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            var p = MakePutter(bed.ball);
            Vector3 b = bed.ball.Position;
            yield return Sweep(p, b, b.z - 0.15f, b.z + 0.1f, 0.02f); // 2 cm/s
            Object.Destroy(p.gameObject);
            Assert.AreEqual(0, bed.hole.Strokes);
        }

        [UnityTest]
        public IEnumerator FollowThrough_CountsOneStroke()
        {
            Time.timeScale = 1f;
            using var bed = new TestBed(Lane(), new Vector2(0f, 1f));
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            var p = MakePutter(bed.ball);
            Vector3 b = bed.ball.Position;
            // Slow ball, fast follow-through that chases it.
            yield return Sweep(p, b, b.z - 0.3f, b.z + 0.6f, 1.2f);
            Object.Destroy(p.gameObject);
            Assert.AreEqual(1, bed.hole.Strokes, "follow-through should not double-hit");
        }

        [UnityTest]
        public IEnumerator Backswing_FromInFront_StrikesBackwards()
        {
            Time.timeScale = 1f;
            using var bed = new TestBed(Lane(), new Vector2(0f, 2f));
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            var p = MakePutter(bed.ball);
            Vector3 b = bed.ball.Position;
            // Head starts in front of the ball and swings back through it: two-faced head hits it backwards.
            yield return Sweep(p, b, b.z + 0.3f, b.z - 0.3f, 1.0f);
            Object.Destroy(p.gameObject);
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(1, bed.hole.Strokes);
            Assert.Less(bed.ball.Velocity.z, 0f);
        }

        [UnityTest]
        public IEnumerator HeadSpawnedOverlappingBall_DoesNotStrike()
        {
            Time.timeScale = 1f;
            using var bed = new TestBed(Lane(), new Vector2(0f, 1f));
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            var p = MakePutter(bed.ball);
            Vector3 b = bed.ball.Position;
            // Teleport-style: head appears inside the ball, then moves away slowly.
            yield return Sweep(p, b, b.z, b.z - 0.2f, 0.5f);
            Object.Destroy(p.gameObject);
            Assert.AreEqual(0, bed.hole.Strokes);
        }

        [UnityTest]
        public IEnumerator HardSwing_ClampedToMaxBallSpeed()
        {
            Time.timeScale = 1f;
            using var bed = new TestBed(Lane(), new Vector2(0f, 1f));
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            var p = MakePutter(bed.ball);
            Vector3 b = bed.ball.Position;
            yield return Sweep(p, b, b.z - 0.5f, b.z + 0.3f, 12f);
            Object.Destroy(p.gameObject);
            Assert.AreEqual(1, bed.hole.Strokes, "very fast swing must still register (swept test)");
            Assert.LessOrEqual(bed.ball.Velocity.magnitude, bed.tuning.maxBallSpeed + 0.01f);
        }
    }
}
