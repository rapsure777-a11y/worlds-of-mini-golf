using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// Writes a plain-text play log for every session to
    /// %USERPROFILE%\AppData\LocalLow\Gamebreak Labs\Worlds of Mini Golf\Sessions\.
    /// Records the XR device and controller layouts, every strike, hole results, putter settings
    /// and frame-time statistics, so headset sessions can be analysed afterwards.
    /// </summary>
    public class SessionLog : MonoBehaviour
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        [SerializeField] CourseController course;
        [SerializeField] VRRig rig;

        StreamWriter m_Out;
        Putter m_Putter;
        readonly List<float> m_FrameMs = new List<float>(4096);
        int m_Hitches;
        float m_NextFlush, m_NextFrameReport;

        public void Configure(CourseController c, VRRig r) { course = c; rig = r; }

        void Start()
        {
            try
            {
                string dir = Path.Combine(Application.persistentDataPath, "Sessions");
                Directory.CreateDirectory(dir);
                m_Out = new StreamWriter(Path.Combine(dir, $"session_{DateTime.Now:yyyyMMdd_HHmmss}.txt")) { AutoFlush = false };
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SessionLog] disabled: " + e.Message);
                enabled = false;
                return;
            }

            W($"Worlds of Mini Golf session {DateTime.Now:yyyy-MM-dd HH:mm:ss}  build {Application.version}  Unity {Application.unityVersion}");
            W($"GPU {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType})  CPU {SystemInfo.processorType}");
            W($"XR active {XRSettings.isDeviceActive}  device '{XRSettings.loadedDeviceName}'  stereo {XRSettings.stereoRenderingMode}  eye {XRSettings.eyeTextureWidth}x{XRSettings.eyeTextureHeight}  refresh {RefreshRate():F1} Hz");
            LogDevices();
            InputSystem.onDeviceChange += OnDeviceChange;

            m_Putter = rig ? rig.Putter : null;
            if (m_Putter)
            {
                m_Putter.StruckBall += OnStrike;
                W($"Putter length {F(m_Putter.Length)} angle {F(m_Putter.AngleOffset)} twist {F(m_Putter.HeadTwist)}  {(rig.LeftHanded ? "left" : "right")}-handed");
            }
            if (course)
            {
                course.HoleStarted += (c, h) => W($"HOLE {h.HoleNumber} start (par {h.Par})");
                course.HoleFinished += (c, h) => W($"HOLE {h.HoleNumber} finished in {h.Strokes} ({Scorecard.ResultName(h.Strokes, h.Par)})");
                course.CourseFinished += c => W($"COURSE finished: {c.Card.TotalStrokes} strokes, par {c.Card.TotalParPlayed}");
            }
            foreach (var hole in FindObjectsByType<HoleController>(FindObjectsSortMode.None))
                hole.BallReturned += (h, oob) => W(oob ? $"  out of bounds -> strokes {h.Strokes}" : "  ball reset by player");
        }

        void OnDestroy()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
            if (m_Putter) m_Putter.StruckBall -= OnStrike;
            if (m_Out == null) return;
            ReportFrames();
            W("Session end");
            m_Out.Flush();
            m_Out.Dispose();
            m_Out = null;
        }

        void OnApplicationQuit() => m_Out?.Flush();

        void LogDevices()
        {
            foreach (var d in InputSystem.devices)
                if (d is UnityEngine.InputSystem.XR.XRController || d is UnityEngine.InputSystem.XR.XRHMD)
                    W($"Input device: {d.layout} '{d.displayName}' usages [{string.Join(",", d.usages)}]");
        }

        void OnDeviceChange(UnityEngine.InputSystem.InputDevice d, InputDeviceChange change)
        {
            if (d is UnityEngine.InputSystem.XR.XRController || d is UnityEngine.InputSystem.XR.XRHMD)
                W($"Input device {change}: {d.layout} '{d.displayName}' usages [{string.Join(",", d.usages)}]");
        }

        void OnStrike(Putter p, float ballSpeed)
        {
            var hole = HoleController.Active;
            Vector3 head = p.HeadVelocity;
            Vector3 headFlat = new Vector3(head.x, 0f, head.z);
            Vector3 face = p.HeadRotation * Vector3.right; face.y = 0f;
            float faceVsPath = headFlat.sqrMagnitude > 1e-4f ? Vector3.SignedAngle(headFlat, face * Mathf.Sign(Vector3.Dot(face, headFlat)), Vector3.up) : 0f;
            string toCup = "";
            if (hole && hole.Cup && hole.Ball)
            {
                Vector3 d = hole.Cup.transform.position - hole.Ball.Position; d.y = 0f;
                Vector3 v = hole.Ball.Velocity; v.y = 0f;
                toCup = $" dist {F(d.magnitude)} m  aim error {F(Vector3.SignedAngle(d, v, Vector3.up))} deg";
            }
            W($"  STRIKE hole {(hole ? hole.HoleNumber : 0)} stroke {(hole ? hole.Strokes : 0)}: head {F(head.magnitude)} m/s (vertical {F(head.y)})  ball {F(ballSpeed)} m/s  face-vs-path {F(faceVsPath)} deg{toCup}");
        }

        void Update()
        {
            if (m_Out == null) return;
            if (Time.realtimeSinceStartup < 5f) return; // skip start-up (XR runtime handshake, scene load)
            float ms = Time.unscaledDeltaTime * 1000f;
            m_FrameMs.Add(ms);
            float hz = RefreshRate();
            float budget = hz > 1f ? 1000f / hz : 16.7f;
            if (ms > budget * 1.5f) m_Hitches++;
            if (Time.unscaledTime > m_NextFrameReport) { m_NextFrameReport = Time.unscaledTime + 60f; ReportFrames(); }
            if (Time.unscaledTime > m_NextFlush) { m_NextFlush = Time.unscaledTime + 5f; m_Out.Flush(); }
        }

        void ReportFrames()
        {
            if (m_FrameMs.Count < 10) return;
            m_FrameMs.Sort();
            float P(float q) => m_FrameMs[Mathf.Clamp((int)(q * (m_FrameMs.Count - 1)), 0, m_FrameMs.Count - 1)];
            W($"FRAMES n={m_FrameMs.Count} median {F(P(0.5f))} ms  p95 {F(P(0.95f))} ms  p99 {F(P(0.99f))} ms  max {F(P(1f))} ms  hitches(>1.5x budget) {m_Hitches}");
            if (m_Putter) W($"Putter now: length {F(m_Putter.Length)} angle {F(m_Putter.AngleOffset)} twist {F(m_Putter.HeadTwist)}");
            m_FrameMs.Clear();
            m_Hitches = 0;
        }

        static readonly List<XRDisplaySubsystem> s_Displays = new List<XRDisplaySubsystem>();

        static float RefreshRate()
        {
            SubsystemManager.GetSubsystems(s_Displays);
            foreach (var d in s_Displays)
                if (d.running && d.TryGetDisplayRefreshRate(out float hz)) return hz;
            return 0f;
        }

        void W(string s) => m_Out?.WriteLine($"{Time.realtimeSinceStartup,8:F1}s  {s}");
        static string F(float f) => f.ToString("F2", Inv);
    }
}
