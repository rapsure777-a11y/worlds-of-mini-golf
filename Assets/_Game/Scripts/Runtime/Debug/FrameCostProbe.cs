using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// VR frame-cost probe. In VR neither Unity's GPU timer nor the OpenXR GPU metric separates real GPU work from waiting
    /// on the compositor (the timer simply tracks the frame interval), so this measures cost by <b>ablation</b>: it turns one
    /// feature off at a time (shadows, MSAA, render scale, depth/opaque copies, foliage, rocks, water, terrain, ...) for a few
    /// seconds each and records how the delivered frame interval and the share of frames at full refresh respond. A feature whose
    /// removal moves the interval by milliseconds is where the cost is.
    ///
    /// Start it with <c>-probe</c> on the command line (VR or desktop; <c>Tools\vr-probe.ps1</c>) or F10 on a desktop keyboard.
    /// Stand still at the tee looking down the lane; the run takes about 3 minutes. Output: <c>Sessions/probe_*.txt</c> and Player.log.
    /// Only runs in a built player: it edits the live URP asset, which in the editor would persist.
    /// </summary>
    public class FrameCostProbe : MonoBehaviour
    {
        const float SettleSeconds = 3f, MeasureSeconds = 12f;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        struct Stage { public string name; public Action apply; }

        UniversalRenderPipelineAsset m_Asset;
        float m_Shadow, m_Scale; int m_Msaa; bool m_Depth, m_Opaque, m_Hdr;
        readonly List<Renderer> m_Hidden = new List<Renderer>();
        readonly List<(Renderer r, ShadowCastingMode mode)> m_ShadowOff = new List<(Renderer, ShadowCastingMode)>();
        bool m_Running;

        void Start()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-probe") >= 0) StartCoroutine(RunAfter(12f));
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f10Key.wasPressedThisFrame && !m_Running) StartCoroutine(RunAfter(1f));
        }

        IEnumerator RunAfter(float delay)
        {
            if (Application.isEditor) { Debug.LogWarning("[Probe] Built players only (it edits the live URP asset)."); yield break; }
            if (m_Running) yield break;
            m_Running = true;
            yield return new WaitForSecondsRealtime(delay);
            m_Asset = UniversalRenderPipeline.asset;
            if (!m_Asset) { Debug.LogWarning("[Probe] No URP asset."); m_Running = false; yield break; }
            m_Shadow = m_Asset.shadowDistance; m_Scale = m_Asset.renderScale; m_Msaa = m_Asset.msaaSampleCount;
            m_Depth = m_Asset.supportsCameraDepthTexture; m_Opaque = m_Asset.supportsCameraOpaqueTexture; m_Hdr = m_Asset.supportsHDR;

            var stages = new List<Stage>
            {
                new Stage { name = "baseline", apply = () => { } },
                new Stage { name = "shadows off (distance 0.1 m)", apply = () => m_Asset.shadowDistance = 0.1f },
                new Stage { name = "terrain does not cast shadows", apply = () => SetShadows(new[] { "IslandTerrain" }, false) },
                new Stage { name = "MSAA off", apply = () => m_Asset.msaaSampleCount = 1 },
                new Stage { name = "MSAA 2x", apply = () => m_Asset.msaaSampleCount = 2 },
                new Stage { name = "render scale 0.7", apply = () => m_Asset.renderScale = 0.7f },
                new Stage { name = "no depth/opaque copies (water fallback)", apply = () => { m_Asset.supportsCameraDepthTexture = false; m_Asset.supportsCameraOpaqueTexture = false; QualityPreset.SetWaterDepth(false); } },
                new Stage { name = "hide leaf cards (alpha-tested foliage)", apply = () => Hide(r => HasMat(r, "Hero_Leaves") || HasMat(r, "Kit_Foliage")) },
                new Stage { name = "hide hero rocks", apply = () => Hide(r => HasMat(r, "Hero_Rock")) },
                new Stage { name = "hide terrain", apply = () => Hide(r => r.name == "IslandTerrain") },
                new Stage { name = "hide water (ocean, pool, waterfall, mist)", apply = () => Hide(r => HasMat(r, "Kit_Water") || HasMat(r, "Hero_PoolWater") || HasMat(r, "Hero_Waterfall") || HasMat(r, "Hero_Mist")) },
                new Stage { name = "hide all dressing (green, rails, sky, ball only)", apply = () => Hide(r => InDressing(r.transform)) },
            };

            var sb = new StringBuilder();
            sb.AppendLine($"Frame-cost probe {DateTime.Now:yyyy-MM-dd HH:mm}  preset {QualityPreset.CurrentName}  {SystemInfo.graphicsDeviceName}");
            sb.AppendLine($"XR {XRSettings.isDeviceActive}  eye {XRSettings.eyeTextureWidth}x{XRSettings.eyeTextureHeight}  refresh {RefreshRate():F0} Hz   (stand still at the tee; lower interval / higher full-rate share = that feature was costing time)");
            sb.AppendLine($"{"stage",-52} {"interval med",12} {"p95",8} {"full-rate",10} {"half-rate",10}  frames");
            foreach (var st in stages)
            {
                Restore();
                st.apply();
                yield return new WaitForSecondsRealtime(SettleSeconds);
                var ms = new List<float>();
                float end = Time.realtimeSinceStartup + MeasureSeconds;
                while (Time.realtimeSinceStartup < end) { ms.Add(Time.unscaledDeltaTime * 1000f); yield return null; }
                float hz = RefreshRate(); float budget = hz > 1f ? 1000f / hz : 11.1f;
                int full = 0, half = 0;
                foreach (float f in ms) { if (f <= budget * 1.15f) full++; else if (f <= budget * 2.3f) half++; }
                ms.Sort();
                float P(float q) => ms.Count == 0 ? 0f : ms[Mathf.Clamp((int)(q * (ms.Count - 1)), 0, ms.Count - 1)];
                string line = $"{st.name,-52} {P(0.5f).ToString("F2", Inv),9} ms {P(0.95f).ToString("F2", Inv),5} ms {(100f * full / Math.Max(1, ms.Count)).ToString("F0", Inv),8} % {(100f * half / Math.Max(1, ms.Count)).ToString("F0", Inv),8} %  {ms.Count}";
                sb.AppendLine(line);
                Debug.Log("[Probe] " + line);
            }
            Restore();
            try
            {
                string dir = Path.Combine(Application.persistentDataPath, "Sessions");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, $"probe_{DateTime.Now:yyyyMMdd_HHmmss}.txt"), sb.ToString());
            }
            catch (Exception e) { Debug.LogWarning("[Probe] could not write report: " + e.Message); }
            Debug.Log("[Probe] done\n" + sb);
            m_Running = false;
        }

        static bool HasMat(Renderer r, string name)
        {
            foreach (var m in r.sharedMaterials) if (m && m.name == name) return true;
            return false;
        }

        static bool InDressing(Transform t)
        {
            for (; t != null; t = t.parent) if (t.name == "Dressing" || t.name == "Island_Dressing") return true;
            return false;
        }

        void Hide(Func<Renderer, bool> pick)
        {
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (r.enabled && pick(r)) { r.enabled = false; m_Hidden.Add(r); }
        }

        void SetShadows(string[] names, bool on)
        {
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (Array.IndexOf(names, r.name) >= 0) { m_ShadowOff.Add((r, r.shadowCastingMode)); r.shadowCastingMode = on ? ShadowCastingMode.On : ShadowCastingMode.Off; }
        }

        void Restore()
        {
            foreach (var r in m_Hidden) if (r) r.enabled = true;
            m_Hidden.Clear();
            foreach (var (r, mode) in m_ShadowOff) if (r) r.shadowCastingMode = mode;
            m_ShadowOff.Clear();
            if (!m_Asset) return;
            m_Asset.shadowDistance = m_Shadow; m_Asset.renderScale = m_Scale; m_Asset.msaaSampleCount = m_Msaa;
            m_Asset.supportsCameraDepthTexture = m_Depth; m_Asset.supportsCameraOpaqueTexture = m_Opaque; m_Asset.supportsHDR = m_Hdr;
            QualityPreset.SetWaterDepth(QualityPreset.Current != QualityLevel.Lean);
        }

        static readonly List<XRDisplaySubsystem> s_Displays = new List<XRDisplaySubsystem>();
        static float RefreshRate()
        {
            SubsystemManager.GetSubsystems(s_Displays);
            foreach (var d in s_Displays) if (d.running && d.TryGetDisplayRefreshRate(out float hz)) return hz;
            return 0f;
        }
    }
}
