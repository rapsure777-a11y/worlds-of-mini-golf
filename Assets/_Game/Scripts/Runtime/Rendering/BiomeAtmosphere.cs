using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gamebreak.MiniGolf
{
    /// <summary>Lighting, fog, ambient and sky colours for one biome (Graphics Pass 3).</summary>
    [Serializable]
    public struct AtmospherePreset
    {
        public string clusterId;
        public Color sunColor;
        public float sunIntensity;
        public Vector3 sunEuler;
        public Color ambientSky, ambientEquator, ambientGround;
        public Color fogColor;
        public float fogDensity;
        public Color skyTop, skyHorizon, skyBottom, skySun;
        public float skySunSize, skySunGlow;

        public static AtmospherePreset Blend(AtmospherePreset a, AtmospherePreset b, float t)
        {
            t = Mathf.Clamp01(t);
            return new AtmospherePreset
            {
                clusterId = t < 0.5f ? a.clusterId : b.clusterId,
                sunColor = Color.Lerp(a.sunColor, b.sunColor, t),
                sunIntensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, t),
                sunEuler = new Vector3(Mathf.LerpAngle(a.sunEuler.x, b.sunEuler.x, t), Mathf.LerpAngle(a.sunEuler.y, b.sunEuler.y, t), Mathf.LerpAngle(a.sunEuler.z, b.sunEuler.z, t)),
                ambientSky = Color.Lerp(a.ambientSky, b.ambientSky, t), ambientEquator = Color.Lerp(a.ambientEquator, b.ambientEquator, t), ambientGround = Color.Lerp(a.ambientGround, b.ambientGround, t),
                fogColor = Color.Lerp(a.fogColor, b.fogColor, t), fogDensity = Mathf.Lerp(a.fogDensity, b.fogDensity, t),
                skyTop = Color.Lerp(a.skyTop, b.skyTop, t), skyHorizon = Color.Lerp(a.skyHorizon, b.skyHorizon, t), skyBottom = Color.Lerp(a.skyBottom, b.skyBottom, t),
                skySun = Color.Lerp(a.skySun, b.skySun, t), skySunSize = Mathf.Lerp(a.skySunSize, b.skySunSize, t), skySunGlow = Mathf.Lerp(a.skySunGlow, b.skySunGlow, t),
            };
        }
    }

    /// <summary>The five biome moods of Tropical Adventure. One world, one sun: only its colour, height and the mist change from island to island.</summary>
    public static class BiomeAtmospheres
    {
        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        /// <summary>Preset for a cluster id ("start", "jungle", "temple", "volcanic", "summit"); unknown ids get the Starting Island's.</summary>
        public static AtmospherePreset For(string clusterId)
        {
            switch (clusterId)
            {
                case TropicalCourse.JungleCluster:
                    return new AtmospherePreset
                    {
                        clusterId = clusterId, sunColor = C(0.96f, 1.0f, 0.84f), sunIntensity = 1.3f, sunEuler = new Vector3(58f, 190f, 0f),
                        ambientSky = C(0.42f, 0.66f, 0.74f), ambientEquator = C(0.36f, 0.56f, 0.46f), ambientGround = C(0.22f, 0.30f, 0.20f),
                        fogColor = C(0.58f, 0.82f, 0.80f), fogDensity = 0.0085f,
                        skyTop = C(0.20f, 0.50f, 0.86f), skyHorizon = C(0.62f, 0.86f, 0.88f), skyBottom = C(0.42f, 0.68f, 0.70f), skySun = C(1f, 0.98f, 0.8f), skySunSize = 0.012f, skySunGlow = 0.45f,
                    };
                case TropicalCourse.TempleCluster:
                    return new AtmospherePreset
                    {
                        clusterId = clusterId, sunColor = C(1.0f, 0.82f, 0.54f), sunIntensity = 1.75f, sunEuler = new Vector3(30f, 212f, 0f),
                        ambientSky = C(0.62f, 0.62f, 0.86f), ambientEquator = C(0.72f, 0.58f, 0.42f), ambientGround = C(0.46f, 0.34f, 0.22f),
                        fogColor = C(0.98f, 0.82f, 0.62f), fogDensity = 0.0050f,
                        skyTop = C(0.26f, 0.50f, 0.92f), skyHorizon = C(1.0f, 0.82f, 0.58f), skyBottom = C(0.82f, 0.66f, 0.50f), skySun = C(1f, 0.86f, 0.55f), skySunSize = 0.018f, skySunGlow = 0.85f,
                    };
                case TropicalCourse.VolcanicCluster:
                    return new AtmospherePreset
                    {
                        clusterId = clusterId, sunColor = C(1.0f, 0.62f, 0.36f), sunIntensity = 1.35f, sunEuler = new Vector3(22f, 250f, 0f),
                        ambientSky = C(0.56f, 0.38f, 0.40f), ambientEquator = C(0.56f, 0.30f, 0.22f), ambientGround = C(0.30f, 0.12f, 0.08f),
                        fogColor = C(0.56f, 0.30f, 0.22f), fogDensity = 0.0085f,
                        skyTop = C(0.20f, 0.18f, 0.38f), skyHorizon = C(1.0f, 0.50f, 0.26f), skyBottom = C(0.36f, 0.13f, 0.09f), skySun = C(1f, 0.55f, 0.25f), skySunSize = 0.022f, skySunGlow = 1.0f,
                    };
                case TropicalCourse.SummitCluster:
                    return new AtmospherePreset
                    {
                        clusterId = clusterId, sunColor = C(1.0f, 0.68f, 0.36f), sunIntensity = 1.8f, sunEuler = new Vector3(12f, 265f, 0f),
                        ambientSky = C(0.60f, 0.50f, 0.78f), ambientEquator = C(0.86f, 0.56f, 0.40f), ambientGround = C(0.50f, 0.36f, 0.30f),
                        fogColor = C(1.0f, 0.72f, 0.60f), fogDensity = 0.0055f,
                        skyTop = C(0.30f, 0.30f, 0.70f), skyHorizon = C(1.0f, 0.60f, 0.30f), skyBottom = C(0.96f, 0.58f, 0.48f), skySun = C(1f, 0.70f, 0.32f), skySunSize = 0.04f, skySunGlow = 1.3f,
                    };
                default:
                    return new AtmospherePreset
                    {
                        clusterId = TropicalCourse.StartCluster, sunColor = C(1.0f, 0.93f, 0.78f), sunIntensity = 1.6f, sunEuler = new Vector3(42f, 205f, 0f),
                        ambientSky = C(0.50f, 0.68f, 0.95f), ambientEquator = C(0.58f, 0.70f, 0.62f), ambientGround = C(0.44f, 0.38f, 0.28f),
                        fogColor = C(0.70f, 0.88f, 0.97f), fogDensity = 0.0040f,
                        skyTop = C(0.14f, 0.45f, 0.96f), skyHorizon = C(0.72f, 0.90f, 1.0f), skyBottom = C(0.50f, 0.78f, 0.92f), skySun = C(1f, 0.93f, 0.78f), skySunSize = 0.012f, skySunGlow = 0.55f,
                    };
            }
        }

        public static AtmospherePreset ForHole(int holeNumber)
        {
            var c = TropicalCourse.ClusterOf(holeNumber);
            return For(c != null ? c.id : TropicalCourse.StartCluster);
        }
    }

    /// <summary>
    /// Moves the world's mood with the player: when a hole starts, the sun colour and height, ambient light, fog and sky colours ease (about two seconds) toward that hole's
    /// biome. Purely visual and cheap: it touches <see cref="RenderSettings"/>, the one directional light and a runtime copy of the sky material, never gameplay.
    /// </summary>
    public class BiomeAtmosphere : MonoBehaviour
    {
        [SerializeField] CourseController course;
        [SerializeField] Light sun;
        [SerializeField] Material skySource;
        [SerializeField] float blendSeconds = 2.2f;

        Material m_Sky;
        AtmospherePreset m_From, m_To, m_Current;
        float m_T = 1f;

        public AtmospherePreset Current => m_Current;
        public AtmospherePreset Target => m_To;
        public bool IsBlending => m_T < 1f;

        public void Configure(CourseController c, Light sunLight, Material sky)
        {
            course = c; sun = sunLight; skySource = sky;
        }

        void OnEnable()
        {
            if (skySource && !m_Sky) { m_Sky = new Material(skySource) { name = skySource.name + " (runtime)" }; RenderSettings.skybox = m_Sky; }
            if (course) course.HoleStarted += OnHoleStarted;
            m_To = m_Current = m_From = BiomeAtmospheres.For(TropicalCourse.StartCluster);
            if (course && course.Current) SetTarget(BiomeAtmospheres.ForHole(course.Current.HoleNumber), true);
            else Apply(m_Current);
        }

        void OnDisable()
        {
            if (course) course.HoleStarted -= OnHoleStarted;
            if (m_Sky) { Destroy(m_Sky); m_Sky = null; }
        }

        void OnHoleStarted(CourseController c, HoleController hole) => SetTarget(BiomeAtmospheres.ForHole(hole.HoleNumber), false);

        /// <summary>Eases toward <paramref name="preset"/> (or jumps there at once).</summary>
        public void SetTarget(AtmospherePreset preset, bool immediate)
        {
            if (preset.clusterId == m_To.clusterId && !immediate) return;
            m_From = m_Current; m_To = preset;
            m_T = immediate ? 1f : 0f;
            if (immediate) { m_Current = preset; Apply(m_Current); }
        }

        void Update()
        {
            if (m_T >= 1f) return;
            m_T = Mathf.Min(1f, m_T + Time.unscaledDeltaTime / Mathf.Max(0.1f, blendSeconds));
            float s = m_T * m_T * (3f - 2f * m_T);
            m_Current = AtmospherePreset.Blend(m_From, m_To, s);
            Apply(m_Current);
        }

        /// <summary>Editor capture and previews: applies a preset to the scene settings without a running component (the sky is a throw-away copy, never saved).</summary>
        public static void ApplyPreview(AtmospherePreset p, Light sun, ref Material skyCopy)
        {
            if (sun) { sun.color = p.sunColor; sun.intensity = p.sunIntensity; sun.transform.rotation = Quaternion.Euler(p.sunEuler); }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = p.ambientSky; RenderSettings.ambientEquatorColor = p.ambientEquator; RenderSettings.ambientGroundColor = p.ambientGround;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogColor = p.fogColor; RenderSettings.fogDensity = p.fogDensity;
            if (!skyCopy && RenderSettings.skybox) skyCopy = new Material(RenderSettings.skybox) { name = "PreviewSky" };
            if (skyCopy)
            {
                skyCopy.SetColor("_TopColor", p.skyTop); skyCopy.SetColor("_HorizonColor", p.skyHorizon); skyCopy.SetColor("_BottomColor", p.skyBottom);
                skyCopy.SetColor("_SunColor", p.skySun); skyCopy.SetFloat("_SunSize", p.skySunSize); skyCopy.SetFloat("_SunGlow", p.skySunGlow);
                RenderSettings.skybox = skyCopy;
            }
        }

        void Apply(AtmospherePreset p)
        {
            if (sun)
            {
                sun.color = p.sunColor; sun.intensity = p.sunIntensity;
                sun.transform.rotation = Quaternion.Euler(p.sunEuler);
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = p.ambientSky; RenderSettings.ambientEquatorColor = p.ambientEquator; RenderSettings.ambientGroundColor = p.ambientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = p.fogColor; RenderSettings.fogDensity = p.fogDensity;
            if (m_Sky)
            {
                m_Sky.SetColor("_TopColor", p.skyTop); m_Sky.SetColor("_HorizonColor", p.skyHorizon); m_Sky.SetColor("_BottomColor", p.skyBottom);
                m_Sky.SetColor("_SunColor", p.skySun); m_Sky.SetFloat("_SunSize", p.skySunSize); m_Sky.SetFloat("_SunGlow", p.skySunGlow);
            }
        }
    }
}
