using System.Collections;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamebreak.MiniGolf.Tests
{
    /// <summary>Trace helpers for tuning. Run with -testCategory Diagnostic; they always pass.</summary>
    public class DiagnosticTests
    {
        static IEnumerator Trace(TestBed bed, Vector3 strike, int steps, int every)
        {
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            bed.ball.HitWall += (b, s) => Debug.Log($"[Trace] WALL hit {s:F3} at {b.Position:F4}");
            bed.ball.Stopped += b => Debug.Log($"[Trace] STOPPED at {b.Position:F4} t={Time.time:F2}");
            bed.ball.Strike(strike);
            var sb = new StringBuilder();
            for (int i = 0; i < steps; i++)
            {
                yield return new WaitForFixedUpdate();
                if (i % every == 0)
                    sb.AppendLine($"{i} pos {bed.ball.Position:F4} vel {bed.ball.Velocity:F3} g {bed.ball.IsGrounded} rest {bed.ball.IsAtRest} inPlay {bed.ball.InPlay}");
            }
            Debug.Log("[Trace]\n" + sb);
        }

        [UnityTest, Category("Diagnostic")]
        public IEnumerator TraceFastPutt()
        {
            Time.timeScale = 3f;
            var l = new GreenLayout();
            l.Area(-0.6f, 0f, 1.2f, 8f);
            l.cup = new Vector2(0f, 3f);
            using var bed = new TestBed(l, new Vector2(0f, 0.5f));
            yield return Trace(bed, new Vector3(0f, 0f, 5f), 90, 3);
            Time.timeScale = 1f;
            Assert.Pass();
        }

        [UnityTest, Category("Diagnostic")]
        public IEnumerator TraceRollOut()
        {
            Time.timeScale = 3f;
            var l = new GreenLayout();
            l.Area(-0.6f, 0f, 1.2f, 12f);
            using var bed = new TestBed(l, new Vector2(0f, 1f));
            yield return Trace(bed, new Vector3(0f, 0f, 2f), 500, 25);
            Time.timeScale = 1f;
            Assert.Pass();
        }
    }
}
