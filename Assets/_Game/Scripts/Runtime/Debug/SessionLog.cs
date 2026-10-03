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
            W($"Quality preset {QualityPreset.CurrentName}");
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
                hole.BallReturned += (h, oob) => W(oob ? $"  out of bounds -> strokes {h.Strokes}" : "  ball returned to last shot spot (player)");
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

        // Per-report-window performance samples.
        readonly List<float> m_CpuMs = new List<float>(4096);
        readonly List<float> m_GpuMs = new List<float>(4096);
        readonly List<float> m_AppGpuMs = new List<float>(4096);
        readonly List<float> m_CompGpuMs = new List<float>(4096);
        readonly Dictionary<int, int> m_RefreshHist = new Dictionary<int, int>();
        int m_DroppedStart = -1, m_PresentStart = -1;
        readonly FrameTiming[] m_Timing = new FrameTiming[1];

        void Update()
        {
            if (m_Out == null) return;
            if (Time.realtimeSinceStartup < 5f) return; // skip start-up (XR runtime handshake, scene load)
            float ms = Time.unscaledDeltaTime * 1000f;
            m_FrameMs.Add(ms);
            float hz = RefreshRate();
            int hzKey = Mathf.RoundToInt(hz);
            m_RefreshHist[hzKey] = m_RefreshHist.TryGetValue(hzKey, out int c) ? c + 1 : 1;
            float budget = hz > 1f ? 1000f / hz : 16.7f;
            if (ms > budget * 1.5f) m_Hitches++;

            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, m_Timing) > 0)
            {
                m_CpuMs.Add((float)m_Timing[0].cpuMainThreadFrameTime);
                if (m_Timing[0].gpuFrameTime > 0) m_GpuMs.Add((float)m_Timing[0].gpuFrameTime);
            }
            var display = ActiveDisplay();
            if (display != null)
            {
                // The runtime's own measurements, comparable across sessions (Unity's GPU timer can
                // include waits on the XR swapchain).
                if (display.TryGetAppGPUTimeLastFrame(out float appGpu) && appGpu > 0f) m_AppGpuMs.Add(appGpu);
                if (display.TryGetCompositorGPUTimeLastFrame(out float compGpu) && compGpu > 0f) m_CompGpuMs.Add(compGpu);
                if (m_DroppedStart < 0 && display.TryGetDroppedFrameCount(out int d0)) m_DroppedStart = d0;
                if (m_PresentStart < 0 && display.TryGetFramePresentCount(out int p0)) m_PresentStart = p0;
            }

            if (Time.unscaledTime > m_NextFrameReport) { m_NextFrameReport = Time.unscaledTime + 60f; ReportFrames(); }
            if (Time.unscaledTime > m_NextFlush) { m_NextFlush = Time.unscaledTime + 5f; m_Out.Flush(); }
        }

        void ReportFrames()
        {
            if (m_FrameMs.Count < 10) return;
            W($"FRAMES n={m_FrameMs.Count} delta {Pct(m_FrameMs)}  over-budget {m_Hitches}");
            if (m_CpuMs.Count > 0) W($"  CPU main thread {Pct(m_CpuMs)}");
            if (m_GpuMs.Count > 0) W($"  GPU (Unity timer; NOT valid in VR, tracks the frame interval) {Pct(m_GpuMs)}");
            if (m_AppGpuMs.Count > 0) W($"  GPU (runtime, app) {Pct(m_AppGpuMs)}");
            if (m_CompGpuMs.Count > 0) W($"  GPU (runtime, compositor) {Pct(m_CompGpuMs)}");
            float hzNow = RefreshRate();
            if (hzNow > 1f)
            {
                // Pacing at the refresh rate the runtime reports: share of frames delivered at full rate vs half rate (reprojection/ASW territory).
                float budgetNow = 1000f / hzNow; int fullRate = 0, halfRate = 0;
                foreach (float f in m_FrameMs) { if (f <= budgetNow * 1.15f) fullRate++; else if (f <= budgetNow * 2.3f) halfRate++; }
                W($"  pacing at {hzNow:F0} Hz: full rate {100f * fullRate / m_FrameMs.Count:F0}%  half rate {100f * halfRate / m_FrameMs.Count:F0}%  worse {100f * (m_FrameMs.Count - fullRate - halfRate) / m_FrameMs.Count:F0}%  (preset {QualityPreset.CurrentName})");
            }
            W($"  render: eye {XRSettings.eyeTextureWidth}x{XRSettings.eyeTextureHeight} scale {XRSettings.eyeTextureResolutionScale:F2}");
            var sb = new StringBuilder("  refresh rate seen:");
            foreach (var kv in m_RefreshHist) sb.Append($" {kv.Key} Hz x{kv.Value}");
            W(sb.ToString());
            var display = ActiveDisplay();
            if (display != null && display.TryGetDroppedFrameCount(out int dropped) && display.TryGetFramePresentCount(out int presents) && m_DroppedStart >= 0)
                W($"  compositor: dropped {dropped - m_DroppedStart}  presented {presents - m_PresentStart}");
            if (m_Putter) W($"Putter now: length {F(m_Putter.Length)} angle {F(m_Putter.AngleOffset)} twist {F(m_Putter.HeadTwist)}" + (rig ? $"  auto {rig.Sizing.Auto} eye height {F(rig.Sizing.EyeHeight)} offset {F(rig.Sizing.Offset)}" : ""));
            m_FrameMs.Clear(); m_CpuMs.Clear(); m_GpuMs.Clear(); m_AppGpuMs.Clear(); m_CompGpuMs.Clear(); m_RefreshHist.Clear();
            m_Hitches = 0;
            m_DroppedStart = m_PresentStart = -1;
        }

        static string Pct(List<float> v)
        {
            v.Sort();
            float P(float q) => v[Mathf.Clamp((int)(q * (v.Count - 1)), 0, v.Count - 1)];
            return $"median {F(P(0.5f))} p95 {F(P(0.95f))} p99 {F(P(0.99f))} max {F(P(1f))} ms";
        }

        static XRDisplaySubsystem ActiveDisplay()
        {
            SubsystemManager.GetSubsystems(s_Displays);
            foreach (var d in s_Displays) if (d.running) return d;
            return null;
        }

        // Every button press with the exact control that fired, to verify mappings on new controllers.
        InputAction[] m_ButtonLog;

        void OnEnable()
        {
            var paths = new[] { "{PrimaryButton}", "{SecondaryButton}", "{FrameX}", "{FrameY}", "{DpadLeft}", "{DpadRight}", "{MenuButton}", "{Primary2DAxisClick}", "{GripButton}", "{TriggerButton}" };
            var list = new List<InputAction>();
            foreach (var hand in new[] { "LeftHand", "RightHand" })
            foreach (var p in paths)
            {
                var a = new InputAction(type: InputActionType.Button, binding: $"<XRController>{{{hand}}}/{p}");
                string label = $"{hand} {p.Trim('{', '}')}";
                a.performed += ctx => W($"  BUTTON {label} ({ctx.control.path})");
                a.Enable();
                list.Add(a);
            }
            m_ButtonLog = list.ToArray();
        }

        void OnDisable()
        {
            if (m_ButtonLog == null) return;
            foreach (var a in m_ButtonLog) { a.Disable(); a.Dispose(); }
            m_ButtonLog = null;
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
