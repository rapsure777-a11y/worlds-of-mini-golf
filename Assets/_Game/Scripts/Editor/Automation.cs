using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Gamebreak.MiniGolf.Editor
{
    /// <summary>Batch-mode entry points: Unity.exe -batchmode -quit -executeMethod Gamebreak.MiniGolf.Editor.Automation.X</summary>
    public static class Automation
    {
        public const string PlayerPath = "Builds/Windows/WorldsOfMiniGolf.exe";
        public const string DesktopPlayerPath = "Builds/Desktop/WorldsOfMiniGolf_Desktop.exe";

        public static void Setup()
        {
            ProjectSetup.ConfigureAll();
            SceneBuilder.BuildTropicalScene();
        }

        [MenuItem("Gamebreak/Build Windows Player (PCVR)")]
        public static void BuildPlayer() => Build(PlayerPath, true);

        /// <summary>Same game with OpenXR not started at launch: opens straight into desktop debug mode, never starts SteamVR.</summary>
        [MenuItem("Gamebreak/Build Desktop Debug Player")]
        public static void BuildDesktopPlayer() => Build(DesktopPlayerPath, false);

        /// <summary>
        /// Every component script must live in a file named after its class. The editor tolerates a
        /// mismatch but player builds cannot resolve the script, which corrupts scene data (level0)
        /// and crashes on load. Returns the offending script paths.
        /// </summary>
        public static string[] FindUnresolvableComponentScripts()
        {
            var bad = new System.Collections.Generic.List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/_Game/Scripts" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(p);
                if (!script) continue;
                string text = script.text;
                bool declaresComponent = System.Text.RegularExpressions.Regex.IsMatch(text, @"class\s+\w+\s*:\s*(MonoBehaviour|ScriptableObject)\b");
                if (declaresComponent && script.GetClass() == null) bad.Add(p);
            }
            return bad.ToArray();
        }

        static void Build(string path, bool xr)
        {
            var bad = FindUnresolvableComponentScripts();
            if (bad.Length > 0)
            {
                Debug.LogError("[Gamebreak] Build blocked: component class name does not match file name in: " + string.Join(", ", bad));
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            // Desktop build: remove the OpenXR loader entirely. Toggling InitManagerOnStart is not enough,
            // because the OpenXR plugin still pre-initialises from boot.config and tries to reach SteamVR.
            const string Loader = "UnityEngine.XR.OpenXR.OpenXRLoader";
            var xrSettings = UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (xrSettings && !xr)
            {
                UnityEditor.XR.Management.Metadata.XRPackageMetadataStore.RemoveLoader(xrSettings.Manager, Loader, BuildTargetGroup.Standalone);
                AssetDatabase.SaveAssets();
            }
            try
            {
                var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = path,
                    target = BuildTarget.StandaloneWindows64,
                    // The two players differ only in XR start-up; a shared incremental cache produced a
                    // corrupt level0 when switching between them, so always build clean.
                    options = BuildOptions.CleanBuildCache,
                });
                var s = report.summary;
                Debug.Log($"[Gamebreak] Build {s.result} (xr={xr}): {s.totalErrors} errors, {s.totalSize / (1024 * 1024)} MB, {s.totalTime.TotalSeconds:F0}s -> {path}");
                if (Application.isBatchMode && s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
            }
            finally
            {
                if (xrSettings && !xr)
                {
                    bool restored = UnityEditor.XR.Management.Metadata.XRPackageMetadataStore.AssignLoader(xrSettings.Manager, Loader, BuildTargetGroup.Standalone);
                    AssetDatabase.SaveAssets();
                    if (!restored) Debug.LogError("[Gamebreak] Failed to restore the OpenXR loader after the desktop build. Run Gamebreak/Setup/Configure Project for PCVR.");
                }
            }
        }

        public static void SetupAndBuild()
        {
            Setup();
            BuildPlayer();
        }

        /// <summary>Art iteration loop: rebuild the world and capture review screenshots in one editor run.</summary>
        public static void SetupAndCapture()
        {
            Setup();
            CaptureScreenshots();
            SceneStats();
        }

        /// <summary>Rendering budget summary of the built scene, written to the log and Logs/scene-stats.txt.</summary>
        [MenuItem("Gamebreak/Scene Stats")]
        public static void SceneStats()
        {
            var path = EditorBuildSettings.scenes[0].path;
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path);
            long verts = 0, tris = 0, shadowTris = 0;
            int renderers = 0, staticRenderers = 0;
            var materials = new System.Collections.Generic.HashSet<Material>();
            var meshes = new System.Collections.Generic.HashSet<Mesh>();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var mf = r.GetComponent<MeshFilter>();
                if (!mf || !mf.sharedMesh || !r.enabled) continue;
                if (r.name.StartsWith("LOD")) continue; // simplified LOD children (HeroKit rock LODs): count LOD0 only, i.e. worst case at close range
                renderers++;
                if (GameObjectUtility.AreStaticEditorFlagsSet(r.gameObject, StaticEditorFlags.BatchingStatic)) staticRenderers++;
                var m = mf.sharedMesh;
                meshes.Add(m);
                verts += m.vertexCount;
                long t = 0;
                for (int s = 0; s < m.subMeshCount; s++) t += m.GetIndexCount(s) / 3;
                tris += t;
                if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off) shadowTris += t;
                foreach (var mat in r.sharedMaterials) if (mat) materials.Add(mat);
            }
            string report = $"Scene stats: {renderers} mesh renderers ({staticRenderers} static-batched), {meshes.Count} unique meshes, " +
                            $"{materials.Count} materials, {verts / 1000}k vertices, {tris / 1000}k triangles ({shadowTris / 1000}k shadow-casting)";
            System.IO.Directory.CreateDirectory("Logs");
            System.IO.File.WriteAllText("Logs/scene-stats.txt", report + "\n");
            Debug.Log("[Gamebreak] " + report);
        }

        public static void SetupAndBuildAll()
        {
            Setup();
            BuildPlayer();
            BuildDesktopPlayer();
        }

        /// <summary>
        /// Edit-mode captures do not run QualityPreset, so select the preset here: env var GB_QUALITY = lean | balanced | rich
        /// (default balanced). Swaps the editor's active URP asset and the water-depth keyword to match the runtime.
        /// </summary>
        static QualityLevel CaptureQuality()
        {
            var level = System.Enum.TryParse(System.Environment.GetEnvironmentVariable("GB_QUALITY"), true, out QualityLevel l) ? l : QualityLevel.Balanced;
            string path = level == QualityLevel.Lean ? ProjectSetup.LeanAssetPath : level == QualityLevel.Rich ? ProjectSetup.RichAssetPath : ProjectSetup.BalancedAssetPath;
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (asset) QualitySettings.renderPipeline = asset;
            QualityPreset.SetWaterDepth(level != QualityLevel.Lean);
            Debug.Log($"[Gamebreak] Capture quality preset: {level}");
            return level;
        }

        /// <summary>Renders review shots of the first scene to Screenshots/. Needs a graphics device (no -nographics).</summary>
        [MenuItem("Gamebreak/Capture Review Screenshots")]
        public static void CaptureScreenshots()
        {
            var path = EditorBuildSettings.scenes[0].path;
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path);
            System.IO.Directory.CreateDirectory("Screenshots");
            var shots = new System.Collections.Generic.List<(string name, Vector3 pos, Vector3 look)>();
            var holes = Object.FindObjectsByType<HoleController>(FindObjectsSortMode.None).OrderBy(h => h.HoleNumber).ToList();
            Vector3 centroid = Vector3.zero;
            foreach (var h in holes) centroid += (h.Tee.position + h.Cup.transform.position) * 0.5f;
            centroid /= Mathf.Max(1, holes.Count);
            shots.Add(("world_overview", centroid + new Vector3(-26f, 22f, -30f), centroid));
            foreach (var h in holes)
            {
                var cupPos = h.Cup.transform.position;
                var tee = h.Tee.position;
                Vector3 line = cupPos - tee; line.y = 0f; line.Normalize();
                Vector3 right = Vector3.Cross(Vector3.up, line);
                Vector3 mid = (tee + cupPos) * 0.5f;
                string n = $"hole{h.HoleNumber:00}";
                shots.Add(($"{n}_a_tee", h.PlayerStart.position + Vector3.up * 1.65f, tee + line * 4f + Vector3.down * 0.3f));
                shots.Add(($"{n}_b_aerial", mid - line * 7f + right * 5f + Vector3.up * 8f, mid));
                shots.Add(($"{n}_c_side_right", mid + right * 7f + Vector3.up * 2.2f - line * 1f, mid + Vector3.up * 0.4f));
                shots.Add(($"{n}_d_side_left", mid - right * 6f + Vector3.up * 2.4f + line * 1f, mid + Vector3.up * 0.4f));
                shots.Add(($"{n}_e_from_cup", cupPos + line * 1.6f + Vector3.up * 1.5f, tee + Vector3.up * 0.3f));
                shots.Add(($"{n}_f_cup", cupPos + new Vector3(0.25f, 0.25f, -0.35f), cupPos));
                if (h.HoleNumber == 3)
                {
                    // Jungle Crossing: the bridge from the side, and from the elbow looking across it (hole-local coordinates).
                    var t = h.transform;
                    shots.Add(($"{n}_g_bridge_side", t.TransformPoint(new Vector3(5.2f, 1.7f, -3.2f)), t.TransformPoint(new Vector3(5.2f, 0.1f, 4.6f))));
                    shots.Add(($"{n}_h_bridge_from_elbow", t.TransformPoint(new Vector3(1.6f, 1.5f, 4.6f)), t.TransformPoint(new Vector3(8f, 0.4f, 4.6f))));
                    shots.Add(($"{n}_i_ravine_below", t.TransformPoint(new Vector3(5.2f, -0.2f, -1.5f)), t.TransformPoint(new Vector3(5.2f, 1.6f, 4.6f))));
                }
            }
            // Archipelago: both islands and the channel pier from the air.
            shots.Add(("archipelago_aerial", new Vector3(30f, 70f, -120f), new Vector3(32f, 0f, -38f)));
            shots.Add(("archipelago_from_start_island", new Vector3(14f, 3.2f, -22f), new Vector3(58f, 2f, -60f)));
            var level = CaptureQuality();
            var go = new GameObject("ReviewCamera");
            var cam = go.AddComponent<Camera>();
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = level == QualityLevel.Rich;
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.01f;
            var rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            foreach (var s in shots)
            {
                go.transform.position = s.pos;
                go.transform.LookAt(s.look);
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                System.IO.File.WriteAllBytes($"Screenshots/{s.name}.png", tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(go);
            Debug.Log("[Gamebreak] Screenshots written to Screenshots/");
        }
    }
}
