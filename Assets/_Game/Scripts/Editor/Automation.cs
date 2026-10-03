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

        static void Build(string path, bool xr)
        {
            var xrSettings = UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            bool previous = xrSettings && xrSettings.InitManagerOnStart;
            if (xrSettings) { xrSettings.InitManagerOnStart = xr; EditorUtility.SetDirty(xrSettings); AssetDatabase.SaveAssets(); }
            try
            {
                var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = path,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None,
                });
                var s = report.summary;
                Debug.Log($"[Gamebreak] Build {s.result} (xr={xr}): {s.totalErrors} errors, {s.totalSize / (1024 * 1024)} MB, {s.totalTime.TotalSeconds:F0}s -> {path}");
                if (Application.isBatchMode && s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
            }
            finally
            {
                if (xrSettings) { xrSettings.InitManagerOnStart = previous; EditorUtility.SetDirty(xrSettings); AssetDatabase.SaveAssets(); }
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
            var cup = Object.FindFirstObjectByType<Cup>();
            var tee = Object.FindFirstObjectByType<HoleController>().Tee;
            var shots = new (string name, Vector3 pos, Vector3 look)[]
            {
                ("overview", new Vector3(4.5f, 4.5f, -3.5f), new Vector3(0f, 0f, 3.5f)),
                ("tee_view", tee.position + new Vector3(-0.4f, 1.6f, -1.0f), cup.transform.position),
                ("cup_close", cup.transform.position + new Vector3(0.25f, 0.25f, -0.35f), cup.transform.position),
            };
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
