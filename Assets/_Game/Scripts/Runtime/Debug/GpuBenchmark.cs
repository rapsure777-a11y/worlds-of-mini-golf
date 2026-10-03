using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// Built-player GPU benchmark. Run the exe with <c>-benchmark</c>: the main camera renders fixed
    /// Hole 1 viewpoints offscreen at 4320x2160 (about the pixel count of both Steam Frame eyes) and the
    /// GPU frame time is measured with FrameTimingManager. Results go to Benchmark/ next to the exe.
    /// This approximates, but does not replace, measurement in the headset.
    /// </summary>
    public class GpuBenchmark : MonoBehaviour
    {
        const int Width = 4320, Height = 2160, WarmupFrames = 60, MeasureFrames = 240;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-benchmark") < 0) return;
            var go = new GameObject("GpuBenchmark");
            DontDestroyOnLoad(go);
            go.AddComponent<GpuBenchmark>();
        }

        readonly FrameTiming[] m_Timing = new FrameTiming[1];

        IEnumerator Start()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            yield return new WaitForSecondsRealtime(2f);

            var course = FindFirstObjectByType<CourseController>();
            var rig = FindFirstObjectByType<VRRig>();
            var cam = rig ? rig.Head : Camera.main;
            if (rig) rig.DesktopMouseControl = false;
            foreach (var overlay in FindObjectsByType<DebugOverlay>(FindObjectsSortMode.None)) overlay.enabled = false;
            var hole = course ? course.Current : null;

            // 4x MSAA and HDR-capable format to match the VR build's pipeline settings.
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.DefaultHDR) { antiAliasing = 4, name = "BenchmarkTarget" };
            cam.targetTexture = rt;
            cam.fieldOfView = 100f;

            var views = new List<(string name, Vector3 pos, Vector3 look)>();
            if (hole)
            {
                Vector3 tee = hole.Tee.position, cup = hole.Cup.transform.position;
                Vector3 line = cup - tee; line.y = 0f; line.Normalize();
                Vector3 right = Vector3.Cross(Vector3.up, line);
                views.Add(("tee", hole.PlayerStart.position + Vector3.up * 1.65f, tee + line * 4f));
                views.Add(("from_cup", cup + line * 1.6f + Vector3.up * 1.5f, tee));
                views.Add(("jungle_side", (tee + cup) * 0.5f - right * 6f + Vector3.up * 2.4f, (tee + cup) * 0.5f));
                views.Add(("sea_side", (tee + cup) * 0.5f + right * 7f + Vector3.up * 2.2f, (tee + cup) * 0.5f));
            }

            var sb = new StringBuilder();
            sb.AppendLine($"GPU benchmark {DateTime.Now:yyyy-MM-dd HH:mm}  {SystemInfo.graphicsDeviceName}  {Width}x{Height} offscreen, FOV 100 (approx. both Steam Frame eyes)");
            var all = new List<float>();
            foreach (var v in views)
            {
                cam.transform.SetPositionAndRotation(v.pos, Quaternion.LookRotation(v.look - v.pos));
                for (int i = 0; i < WarmupFrames; i++) yield return null;
                var gpu = new List<float>(); var cpu = new List<float>();
                for (int i = 0; i < MeasureFrames; i++)
                {
                    yield return null;
                    FrameTimingManager.CaptureFrameTimings();
                    if (FrameTimingManager.GetLatestTimings(1, m_Timing) > 0)
                    {
                        if (m_Timing[0].gpuFrameTime > 0 && m_Timing[0].gpuFrameTime < 500) gpu.Add((float)m_Timing[0].gpuFrameTime);
                        cpu.Add((float)m_Timing[0].cpuMainThreadFrameTime);
                    }
                }
                all.AddRange(gpu);
                sb.AppendLine($"  {v.name,-12} GPU {Stats(gpu)}   CPU main {Stats(cpu)}");
            }
            sb.AppendLine($"  ALL          GPU {Stats(all)}");
            sb.AppendLine("Budget reference: 120 Hz = 8.33 ms, 90 Hz = 11.1 ms, 72 Hz = 13.9 ms (VR adds compositor/streaming overhead).");
            string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Benchmark");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "benchmark.txt"), sb.ToString());
            Debug.Log("[Benchmark]\n" + sb);
            cam.targetTexture = null;
            Application.Quit(0);
        }

        static string Stats(List<float> v)
        {
            if (v.Count == 0) return "n/a";
            v.Sort();
            float P(float q) => v[Mathf.Clamp((int)(q * (v.Count - 1)), 0, v.Count - 1)];
            var inv = CultureInfo.InvariantCulture;
            return $"median {P(0.5f).ToString("F2", inv)}  p95 {P(0.95f).ToString("F2", inv)} ms";
        }
    }
}
