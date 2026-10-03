using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// Built-player self test. Run the exe with <c>-smoketest</c>: it stands at the ball, swings the
    /// real putter through it (scripted hand motion, same strike code as VR), waits for the result,
    /// saves screenshots and a report to SmokeTest/ next to the exe, then quits (exit 0 = pass).
    /// </summary>
    [DefaultExecutionOrder(150)] // after the rig (-100), before the putter samples the hand (200)
    public class SmokeTest : MonoBehaviour
    {
        const float HeadSpeed = 2.4f;

        string m_Dir;
        readonly StringBuilder m_Report = new StringBuilder();
        int m_Errors;
        float m_StrikeSpeed = -1f;
        double m_FrameMsSum;
        int m_Frames;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-smoketest") < 0) return;
            var go = new GameObject("SmokeTest");
            DontDestroyOnLoad(go);
            go.AddComponent<SmokeTest>();
        }

        void Awake()
        {
            m_Dir = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "SmokeTest");
            Directory.CreateDirectory(m_Dir);
            Application.logMessageReceived += OnLog;
        }

        void OnDestroy() => Application.logMessageReceived -= OnLog;

        void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error)
            {
                m_Errors++;
                m_Report.AppendLine($"LOG {type}: {msg}");
            }
        }

        Putter m_Putter;
        Transform m_Hand;
        Vector3 m_Face, m_Ground;
        float m_S;
        bool m_Swinging;

        void Update()
        {
            m_FrameMsSum += Time.unscaledDeltaTime * 1000.0;
            m_Frames++;
            if (!m_Swinging) return;
            m_S += HeadSpeed * Time.deltaTime;
            bool done = m_S >= 0.25f;
            VRRig.PlaceHead(m_Putter, m_Hand, m_Ground + m_Face * m_S + Vector3.up * (done ? 0.15f : 0.003f), m_Face);
            if (done) m_Swinging = false;
        }

        IEnumerator Start()
        {
            Line($"Smoke test {DateTime.Now:yyyy-MM-dd HH:mm:ss}  Unity {Application.unityVersion}  GPU {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType})");
            yield return new WaitForSecondsRealtime(2f);
            yield return Shot("01_start");

            var rig = FindFirstObjectByType<VRRig>();
            var course = FindFirstObjectByType<CourseController>();
            var hole = course ? course.Current : null;
            Line($"XR active: {(rig ? rig.XRActive : false)}  course: {(course ? course.CourseName : "MISSING")}  hole: {(hole ? hole.HoleNumber : -1)}");
            if (!rig || !hole || !rig.Putter) { Line("FAIL: scene wiring"); Finish(false); yield break; }

            var putter = rig.Putter;
            var ball = hole.Ball;
            putter.StruckBall += (p, s) => m_StrikeSpeed = s;
            rig.DesktopMouseControl = false;
            rig.TeleportToBall();
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("02_at_ball");

            // Swing: lower the head 30 cm behind the ball, sweep through toward the cup. The hand is moved
            // in Update (which runs before the putter samples it), like real tracking data each frame.
            m_Face = hole.Cup.transform.position - ball.Position; m_Face.y = 0f; m_Face.Normalize();
            m_Ground = ball.Position - Vector3.up * ball.Radius;
            m_Putter = putter;
            m_Hand = rig.DominantHand;
            m_S = -0.3f;
            VRRig.PlaceHead(putter, m_Hand, m_Ground + m_Face * m_S + Vector3.up * 0.003f, m_Face);
            putter.ResetTracking();
            for (int i = 0; i < 10; i++) yield return null;
            m_Swinging = true;
            while (m_Swinging) yield return null;
            Line($"Strike: {(m_StrikeSpeed > 0 ? $"{m_StrikeSpeed:F2} m/s" : "NONE")}  strokes {hole.Strokes}");

            float end = Time.realtimeSinceStartup + 12f;
            while (!hole.IsComplete && Time.realtimeSinceStartup < end) yield return null;
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot("03_result");

            Vector3 rest = ball.Position;
            Line($"Holed: {hole.IsComplete}  strokes {hole.Strokes}  ball {rest:F3}  cup {hole.Cup.transform.position:F3}");
            Line($"Average frame {m_FrameMsSum / Math.Max(1, m_Frames):F2} ms over {m_Frames} frames (desktop window, not VR)");
            Finish(m_StrikeSpeed > 0f && hole.IsComplete && hole.Strokes == 1 && m_Errors == 0);
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(m_Dir, name + ".png"), tex.EncodeToPNG());
            Destroy(tex);
        }

        void Line(string s)
        {
            m_Report.AppendLine(s);
            Debug.Log("[SmokeTest] " + s);
        }

        void Finish(bool pass)
        {
            Line(pass ? "RESULT: PASS" : $"RESULT: FAIL ({m_Errors} errors logged)");
            File.WriteAllText(Path.Combine(m_Dir, "report.txt"), m_Report.ToString());
            Application.Quit(pass ? 0 : 1);
        }
    }
}
