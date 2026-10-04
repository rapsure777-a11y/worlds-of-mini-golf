using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// Celebrates holing out by how the score compares with par: a confetti burst from the cup, a floating label ("BIRDIE!") that
    /// faces the player, a short synthesised fanfare and a haptic pulse. Bigger scores get bigger effects (hole in one is the
    /// biggest); par gets a small sparkle and bogey or worse only a quiet label. Listens to <see cref="CourseController.HoleFinished"/>,
    /// so golf code never references it. Everything is synthesised at runtime, so there are no asset dependencies beyond the
    /// particle material the scene builder assigns.
    /// </summary>
    public class ScoreCelebration : MonoBehaviour
    {
        public enum Tier { None = 0, Bogey, Par, Birdie, Eagle, Albatross, HoleInOne }

        [SerializeField] CourseController course;
        [SerializeField] VRRig rig;
        [SerializeField] Material particleMaterial;

        /// <summary>Tier of the most recent celebration, for tests and the session log.</summary>
        public Tier LastTier { get; private set; }
        public int Count { get; private set; }
        public GameObject LastLabel { get; private set; }
        public ParticleSystem LastBurst { get; private set; }

        const float LabelSeconds = 2.6f;

        public void Configure(CourseController c, VRRig r, Material particles)
        {
            course = c; rig = r; particleMaterial = particles;
        }

        void OnEnable() { if (course) course.HoleFinished += OnHoleFinished; }
        void OnDisable() { if (course) course.HoleFinished -= OnHoleFinished; }

        void OnHoleFinished(CourseController c, HoleController hole)
        {
            if (!hole || !hole.Cup) return;
            Celebrate(TierFor(hole.Strokes, hole.Par), hole.Strokes - hole.Par, hole.Cup.transform.position);
        }

        // ------------------------------------------------------------------ scoring (pure, tested)

        public static Tier TierFor(int strokes, int par)
        {
            if (strokes <= 0) return Tier.None;
            if (strokes == 1) return Tier.HoleInOne;
            int d = strokes - par;
            if (d <= -3) return Tier.Albatross;
            if (d == -2) return Tier.Eagle;
            if (d == -1) return Tier.Birdie;
            if (d == 0) return Tier.Par;
            return Tier.Bogey;
        }

        public static string LabelFor(Tier tier, int overPar)
        {
            switch (tier)
            {
                case Tier.HoleInOne: return "HOLE IN ONE!";
                case Tier.Albatross: return "ALBATROSS!";
                case Tier.Eagle: return "EAGLE!";
                case Tier.Birdie: return "BIRDIE!";
                case Tier.Par: return "PAR";
                case Tier.Bogey: return overPar == 1 ? "Bogey" : overPar == 2 ? "Double Bogey" : "+" + overPar;
                default: return "";
            }
        }

        static Color ColorFor(Tier t)
        {
            switch (t)
            {
                case Tier.HoleInOne: return new Color(1f, 0.82f, 0.2f);
                case Tier.Albatross: return new Color(1f, 0.55f, 0.95f);
                case Tier.Eagle: return new Color(1f, 0.6f, 0.2f);
                case Tier.Birdie: return new Color(0.45f, 0.95f, 0.5f);
                case Tier.Par: return new Color(0.75f, 0.9f, 1f);
                default: return new Color(0.85f, 0.85f, 0.85f);
            }
        }

        static int ConfettiFor(Tier t)
        {
            switch (t)
            {
                case Tier.HoleInOne: return 220;
                case Tier.Albatross: return 170;
                case Tier.Eagle: return 120;
                case Tier.Birdie: return 70;
                case Tier.Par: return 16;
                default: return 0;
            }
        }

        // ------------------------------------------------------------------ the celebration

        /// <summary>Play the effects for a tier at a world position (the cup). Public so tests and other tools can trigger them.</summary>
        public void Celebrate(Tier tier, int overPar, Vector3 at)
        {
            if (tier == Tier.None) return;
            LastTier = tier; Count++;
            int n = ConfettiFor(tier);
            if (n > 0) LastBurst = SpawnBurst(tier, n, at);
            LastLabel = SpawnLabel(tier, overPar, at);
            if (tier >= Tier.Par)
            {
                float vol = GolfAudio.SfxVolume * (tier == Tier.Par ? 0.5f : 0.9f);
                if (vol > 0.001f) AudioSource.PlayClipAtPoint(Fanfare(tier), at + Vector3.up * 0.5f, vol);
                if (rig) rig.Haptic(tier >= Tier.Eagle ? 0.9f : 0.6f, tier >= Tier.Eagle ? 0.35f : 0.2f);
            }
        }

        ParticleSystem SpawnBurst(Tier tier, int count, Vector3 at)
        {
            var go = new GameObject("ScoreBurst_" + tier);
            go.transform.position = at + Vector3.up * 0.05f;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 1f; main.loop = false; main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.5f);
            float power = tier >= Tier.Eagle ? 1.25f : 1f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f * power, 6f * power);
            main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.1f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0.55f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            main.startColor = new ParticleSystem.MinMaxGradient(ConfettiColors()) { mode = ParticleSystemGradientMode.RandomColor };
            var em = ps.emission; em.enabled = true; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = tier >= Tier.Eagle ? 42f : 32f; sh.radius = 0.08f;
            sh.rotation = new Vector3(-90f, 0f, 0f); // cones emit along +Z by default; point it up
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
            var col = ps.colorOverLifetime; col.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.65f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(fade);
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.55f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            if (particleMaterial) r.sharedMaterial = particleMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            Destroy(go, 5f);
            return ps;
        }

        static Gradient ConfettiColors()
        {
            var g = new Gradient();
            g.mode = GradientMode.Fixed;
            g.SetKeys(new[]
            {
                new GradientColorKey(new Color(1f, 0.82f, 0.2f), 0.00f), new GradientColorKey(new Color(1f, 0.4f, 0.45f), 0.17f),
                new GradientColorKey(new Color(0.4f, 0.9f, 0.5f), 0.34f), new GradientColorKey(new Color(0.35f, 0.75f, 1f), 0.51f),
                new GradientColorKey(new Color(0.85f, 0.5f, 1f), 0.68f), new GradientColorKey(new Color(1f, 0.6f, 0.2f), 0.85f),
            }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        GameObject SpawnLabel(Tier tier, int overPar, Vector3 at)
        {
            var root = new GameObject("ScoreLabel_" + tier);
            root.transform.position = at + Vector3.up * 0.55f;
            var canvas = WorldText.CreateCanvas("Canvas", root.transform, new Vector2(1500f, 360f), new Color(0, 0, 0, 0));
            bool big = tier >= Tier.Birdie;
            var text = WorldText.CreateText(canvas.transform, "Text", big ? 190 : 110, TextAnchor.MiddleCenter, ColorFor(tier));
            text.fontStyle = FontStyle.Bold;
            text.text = LabelFor(tier, overPar);
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.08f, 0.05f, 0.02f, 0.9f);
            outline.effectDistance = new Vector2(7f, -7f);
            var group = canvas.gameObject.AddComponent<CanvasGroup>();
            StartCoroutine(AnimateLabel(root.transform, group, tier));
            return root;
        }

        IEnumerator AnimateLabel(Transform t, CanvasGroup group, Tier tier)
        {
            Vector3 from = t.position;
            float rise = tier >= Tier.Birdie ? 1.1f : 0.5f;
            for (float time = 0f; time < LabelSeconds && t; time += Time.deltaTime)
            {
                float k = time / LabelSeconds;
                // Pop in with a small overshoot, settle, then drift up and fade out.
                float pop = time < 0.45f ? Mathf.Sin(Mathf.Clamp01(time / 0.45f) * Mathf.PI * 0.5f) * 1.15f : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((time - 0.45f) / 0.3f));
                if (time < 0.45f) pop = Mathf.Lerp(0.2f, 1.18f, 1f - Mathf.Pow(1f - time / 0.45f, 3f));
                t.localScale = Vector3.one * pop;
                t.position = from + Vector3.up * (rise * (1f - Mathf.Pow(1f - k, 2f)));
                group.alpha = k < 0.72f ? 1f : Mathf.Clamp01((1f - k) / 0.28f);
                var cam = rig && rig.Head ? rig.Head.transform : (Camera.main ? Camera.main.transform : null);
                if (cam)
                {
                    Vector3 away = t.position - cam.position; away.y = 0f;
                    if (away.sqrMagnitude > 0.001f) t.rotation = Quaternion.LookRotation(away);
                }
                yield return null;
            }
            if (t) Destroy(t.gameObject);
        }

        // ------------------------------------------------------------------ fanfare (synthesised, cached per tier)

        static readonly System.Collections.Generic.Dictionary<Tier, AudioClip> s_Clips = new System.Collections.Generic.Dictionary<Tier, AudioClip>();

        static AudioClip Fanfare(Tier tier)
        {
            if (s_Clips.TryGetValue(tier, out var cached) && cached) return cached;
            // Major-scale arpeggio from C5; more notes (and a held chord) for bigger scores.
            float[] semis = { 0, 4, 7, 12, 16, 19, 24 };
            int notes = tier == Tier.Par ? 1 : tier == Tier.Birdie ? 4 : tier == Tier.Eagle ? 5 : 7;
            bool chord = tier >= Tier.Eagle;
            const int rate = 44100;
            float step = tier == Tier.Par ? 0.2f : 0.095f;
            float tail = chord ? 1.4f : 0.7f;
            int n = Mathf.CeilToInt((step * notes + tail) * rate);
            var data = new float[n];
            for (int k = 0; k < notes; k++)
            {
                float f = 523.25f * Mathf.Pow(2f, semis[k] / 12f);
                int start = Mathf.RoundToInt(k * step * rate);
                bool last = k == notes - 1;
                float len = last ? tail : 0.32f;
                for (int i = 0; i < Mathf.Min((int)(len * rate), n - start); i++)
                {
                    float t = i / (float)rate;
                    float env = Mathf.Clamp01(t * 300f) * Mathf.Exp(-t * (last ? 2.2f : 7f));
                    float s = Mathf.Sin(2f * Mathf.PI * f * t) + 0.35f * Mathf.Sin(2f * Mathf.PI * f * 2f * t) + 0.15f * Mathf.Sin(2f * Mathf.PI * f * 3f * t);
                    data[start + i] += s * env * 0.22f;
                }
            }
            if (chord)
            {
                // A held major chord under the last note.
                int start = Mathf.RoundToInt((notes - 1) * step * rate);
                foreach (float semi in new[] { -12f, -8f, -5f })
                {
                    float f = 523.25f * Mathf.Pow(2f, semi / 12f);
                    for (int i = 0; i < n - start; i++)
                    {
                        float t = i / (float)rate;
                        data[start + i] += Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Clamp01(t * 80f) * Mathf.Exp(-t * 1.8f) * 0.12f;
                    }
                }
            }
            var clip = AudioClip.Create("score_" + tier, n, 1, rate, false);
            clip.SetData(data, 0);
            s_Clips[tier] = clip;
            return clip;
        }
    }
}
