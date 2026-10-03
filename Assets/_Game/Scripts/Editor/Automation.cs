using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

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

        public static void SetupAndBuildAll()
        {
            Setup();
            BuildPlayer();
            BuildDesktopPlayer();
        }

        /// <summary>Renders review shots of the first scene to Screenshots/. Needs a graphics device (no -nographics).</summary>
        [MenuItem("Gamebreak/Capture Review Screenshots")]
        public static void CaptureScreenshots()
        {
            var path = EditorBuildSettings.scenes[0].path;
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path);
            System.IO.Directory.CreateDirectory("Screenshots");
            var shots = new System.Collections.Generic.List<(string name, Vector3 pos, Vector3 look)>
            {
                ("overview", new Vector3(5.5f, 7f, -5.5f), new Vector3(-2f, 0f, 3.5f)),
            };
            var holes = Object.FindObjectsByType<HoleController>(FindObjectsSortMode.None).OrderBy(h => h.HoleNumber);
            var sign = GameObject.Find("ControlsSign");
            if (sign)
            {
                var start = holes.First().PlayerStart.position + Vector3.up * 1.6f;
                shots.Add(("controls_sign", start, sign.transform.position + Vector3.up * 1.35f));
            }
            foreach (var h in holes)
            {
                var cupPos = h.Cup.transform.position;
                var tee = h.Tee.position;
                Vector3 line = cupPos - tee; line.y = 0f; line.Normalize();
                shots.Add(($"hole{h.HoleNumber:00}_tee", tee - line * 1.0f + Vector3.up * 1.6f, tee + line * 3f));
                shots.Add(($"hole{h.HoleNumber:00}_above", (tee + cupPos) * 0.5f + new Vector3(0f, 6f, -2.5f), (tee + cupPos) * 0.5f));
                shots.Add(($"hole{h.HoleNumber:00}_cup", cupPos + new Vector3(0.25f, 0.25f, -0.35f), cupPos));
            }
            var go = new GameObject("ReviewCamera");
            var cam = go.AddComponent<Camera>();
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
