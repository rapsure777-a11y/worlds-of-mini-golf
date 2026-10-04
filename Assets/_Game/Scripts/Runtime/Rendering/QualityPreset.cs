using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gamebreak.MiniGolf
{
    public enum QualityLevel { Lean = 0, Balanced = 1, Rich = 2 }

    /// <summary>
    /// Runtime graphics presets. World-agnostic: it only swaps URP pipeline assets, toggles camera
    /// post-processing and sets the global <c>_GB_WATER_DEPTH</c> shader keyword.
    /// <list type="bullet">
    /// <item><b>Lean</b>: the original VR budget (no depth/opaque copies, no HDR, 2 cascades). Water uses its baked fallback.</item>
    /// <item><b>Balanced</b>: depth + opaque textures (real water depth/refraction, soft particles), 3 cascades, no post.</item>
    /// <item><b>Rich</b>: Balanced + HDR + tonemapping/bloom/colour grade + 4 cascades.</item>
    /// </list>
    /// Choose with <c>-quality lean|balanced|rich</c> on the command line, the <c>gb.quality</c> PlayerPrefs key,
    /// or F9 (cycle) on the desktop keyboard. The active preset is written to the session log.
    /// </summary>
    public class QualityPreset : MonoBehaviour
    {
        public const string WaterDepthKeyword = "_GB_WATER_DEPTH";
        const string PrefKey = "gb.quality";

        [SerializeField] UniversalRenderPipelineAsset leanAsset, balancedAsset, richAsset;
        [SerializeField] QualityLevel defaultLevel = QualityLevel.Balanced;
        [SerializeField] Camera targetCamera;
        [SerializeField] Volume postVolume;

        public static QualityLevel Current { get; private set; } = QualityLevel.Balanced;
        public static string CurrentName => Current.ToString();

        public void Configure(UniversalRenderPipelineAsset lean, UniversalRenderPipelineAsset balanced, UniversalRenderPipelineAsset rich,
            QualityLevel level, Camera cam, Volume post)
        {
            leanAsset = lean; balancedAsset = balanced; richAsset = rich;
            defaultLevel = level; targetCamera = cam; postVolume = post;
        }

        void Awake() => Apply(Resolve());

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f9Key.wasPressedThisFrame)
                Apply((QualityLevel)(((int)Current + 1) % 3), save: true);
        }

        QualityLevel Resolve()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-quality" && Enum.TryParse(args[i + 1], true, out QualityLevel fromArg)) return fromArg;
            int saved = PlayerPrefs.GetInt(PrefKey, -1);
            return saved >= 0 && saved <= 2 ? (QualityLevel)saved : defaultLevel;
        }

        public void Apply(QualityLevel level, bool save = false)
        {
            var asset = level == QualityLevel.Lean ? leanAsset : level == QualityLevel.Rich ? richAsset : balancedAsset;
            if (asset) QualitySettings.renderPipeline = asset;
            Current = level;
            SetWaterDepth(level != QualityLevel.Lean);

            bool post = level == QualityLevel.Rich;
            if (!targetCamera) targetCamera = Camera.main;
            if (targetCamera) targetCamera.GetUniversalAdditionalCameraData().renderPostProcessing = post;
            if (postVolume) postVolume.enabled = post;
            if (save) { PlayerPrefs.SetInt(PrefKey, (int)level); PlayerPrefs.Save(); }
            Debug.Log($"[Gamebreak] Quality preset: {level} (asset {(asset ? asset.name : "none")}, post {post})");
        }

        /// <summary>The water shader reads the depth/opaque textures only when this keyword is on.</summary>
        public static void SetWaterDepth(bool on)
        {
            if (on) Shader.EnableKeyword(WaterDepthKeyword); else Shader.DisableKeyword(WaterDepthKeyword);
        }
    }
}
