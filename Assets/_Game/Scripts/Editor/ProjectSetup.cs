using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace Gamebreak.MiniGolf.Editor
{
    /// <summary>
    /// One-click (or batch-mode) project configuration for the PCVR target:
    /// Windows x64, D3D11, Linear, URP, OpenXR with Single Pass Instanced.
    /// </summary>
    public static class ProjectSetup
    {
        static readonly HashSet<string> k_Profiles = new HashSet<string>
        {
            "ValveIndexControllerProfile",
            "OculusTouchControllerProfile",
            "MetaQuestTouchPlusControllerProfile",
            "MetaQuestTouchProControllerProfile",
            "HTCViveControllerProfile",
            "KHRSimpleControllerProfile",
            "HPReverbG2ControllerProfile",
            "SteamFrameControllerProfile", // ours: native Steam Frame buttons instead of Touch emulation
        };

        [MenuItem("Gamebreak/Setup/Configure Project for PCVR")]
        public static void ConfigureAll()
        {
            ConfigurePlayer();
            ConfigureQuality();
            ConfigureXR();
            AssetDatabase.SaveAssets();
            Debug.Log("[Gamebreak] Project configured for PCVR (D3D11, Linear, OpenXR SPI).");
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Gamebreak Labs";
            PlayerSettings.productName = "Worlds of Mini Golf";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.runInBackground = true;
            PlayerSettings.enableFrameTimingStats = true; // CPU/GPU frame times in the session log
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
        }

        public const string BalancedAssetPath = "Assets/Settings/PC_RPAsset.asset";       // the PC quality level's asset
        public const string LeanAssetPath = "Assets/Settings/PC_Lean_RPAsset.asset";
        public const string RichAssetPath = "Assets/Settings/PC_Rich_RPAsset.asset";

        /// <summary>
        /// Creates (if missing) and configures the three URP assets that <see cref="QualityPreset"/> swaps between.
        /// VR budget at 120 Hz is 8.3 ms for two 2160x2160 views. The URP template's PC settings (SSAO, depth + opaque
        /// copies, HDR, 4 soft cascades) measured 6.5 ms median / 10.8 ms p99 GPU on a greybox scene, so SteamVR halved
        /// the frame rate. Hence a lean preset (the old settings) stays available and each costly feature is introduced
        /// by a separate tier so it can be benchmarked on its own (Tools/benchmark.ps1 -Quality lean|balanced|rich).
        /// </summary>
        public static void EnsureQualityAssets()
        {
            var balanced = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(BalancedAssetPath);
            if (!balanced) { Debug.LogError($"[Gamebreak] {BalancedAssetPath} not found; cannot create quality presets."); return; }
            foreach (var path in new[] { LeanAssetPath, RichAssetPath })
                if (!File.Exists(path)) AssetDatabase.CopyAsset(BalancedAssetPath, path);
            ApplyLevel(balanced, QualityLevel.Balanced);
            ApplyLevel(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(LeanAssetPath), QualityLevel.Lean);
            ApplyLevel(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(RichAssetPath), QualityLevel.Rich);
            AssetDatabase.SaveAssets();
        }

        static void ApplyLevel(UniversalRenderPipelineAsset asset, QualityLevel level)
        {
            if (!asset) return;
            asset.msaaSampleCount = 4;
            asset.renderScale = 1f;
            switch (level)
            {
                case QualityLevel.Lean:      // the pre-Pass-2 configuration: measured flat 8.33 ms at 120 Hz
                    asset.shadowDistance = 25f; asset.shadowCascadeCount = 2;
                    asset.supportsHDR = false; asset.supportsCameraDepthTexture = false; asset.supportsCameraOpaqueTexture = false;
                    break;
                case QualityLevel.Balanced:  // + depth/opaque copies for real water depth, refraction and soft mist
                    asset.shadowDistance = 30f; asset.shadowCascadeCount = 3;
                    asset.supportsHDR = false; asset.supportsCameraDepthTexture = true; asset.supportsCameraOpaqueTexture = true;
                    break;
                default:                     // + HDR colour buffer (tonemap/bloom) and 4 longer cascades
                    asset.shadowDistance = 40f; asset.shadowCascadeCount = 4;
                    asset.supportsHDR = true; asset.supportsCameraDepthTexture = true; asset.supportsCameraOpaqueTexture = true;
                    break;
            }
            var so = new SerializedObject(asset);
            var soft = so.FindProperty("m_SoftShadowQuality");
            if (soft != null) soft.intValue = level == QualityLevel.Rich ? 2 : 1; // Medium (2) / Low (1); the template used High (3)
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        static void ConfigureQuality()
        {
            EnsureQualityAssets();
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (!asset || !asset.name.StartsWith("PC")) continue;
                // Screen-space ambient occlusion is a full-screen pass per eye; disable it for VR (shared PC renderer).
                foreach (var r in asset.rendererDataList)
                {
                    if (!r) continue;
                    foreach (var feature in r.rendererFeatures)
                    {
                        if (!feature || !feature.GetType().Name.Contains("AmbientOcclusion")) continue;
                        feature.SetActive(false);
                        EditorUtility.SetDirty(feature);
                        Debug.Log($"[Gamebreak] Disabled renderer feature {feature.name} on {r.name}");
                    }
                    EditorUtility.SetDirty(r);
                }
            }
            // Make the PC quality level the default for Windows.
            var names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] != "PC") continue;
                QualitySettings.SetQualityLevel(i, true);
                var so = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
                var perPlatform = so.FindProperty("m_PerPlatformDefaultQuality");
                if (perPlatform != null)
                {
                    for (int p = 0; p < perPlatform.arraySize; p++)
                    {
                        var entry = perPlatform.GetArrayElementAtIndex(p);
                        if (entry.FindPropertyRelative("first").stringValue == "Standalone")
                            entry.FindPropertyRelative("second").intValue = i;
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        static void ConfigureXR()
        {
            const BuildTargetGroup group = BuildTargetGroup.Standalone;

            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey, out XRGeneralSettingsPerBuildTarget perTarget);
            if (!perTarget)
            {
                Directory.CreateDirectory("Assets/XR");
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, perTarget, true);
            }

            var settings = perTarget.SettingsForBuildTarget(group);
            if (!settings)
            {
                settings = ScriptableObject.CreateInstance<XRGeneralSettings>();
                settings.name = "Standalone Settings";
                perTarget.SetSettingsForBuildTarget(group, settings);
                AssetDatabase.AddObjectToAsset(settings, perTarget);
            }
            if (!settings.Manager)
            {
                var manager = ScriptableObject.CreateInstance<XRManagerSettings>();
                manager.name = "Standalone Providers";
                AssetDatabase.AddObjectToAsset(manager, perTarget);
                settings.Manager = manager;
            }
            settings.InitManagerOnStart = true;
            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(perTarget);

            bool assigned = XRPackageMetadataStore.AssignLoader(settings.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", group);
            Debug.Log($"[Gamebreak] OpenXR loader assigned: {assigned}");

            // Pick up project-defined features such as SteamFrameControllerProfile.
            UnityEditor.XR.OpenXR.Features.FeatureHelpers.RefreshFeatures(group);
            var oxr = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            if (oxr)
            {
                oxr.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
                oxr.depthSubmissionMode = OpenXRSettings.DepthSubmissionMode.Depth24Bit;
                foreach (var feature in oxr.GetFeatures<OpenXRInteractionFeature>())
                {
                    bool on = k_Profiles.Contains(feature.GetType().Name);
                    feature.enabled = on;
                    if (on) Debug.Log($"[Gamebreak] OpenXR interaction profile enabled: {feature.GetType().Name}");
                }
                EditorUtility.SetDirty(oxr);
            }
            else Debug.LogError("[Gamebreak] OpenXR settings not found for Standalone.");
        }
    }
}
