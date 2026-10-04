using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// Sound and haptics for strikes, rail hits and holing out. Clips are synthesised at startup
    /// so the prototype has no audio asset dependencies; swap in recorded clips later.
    /// </summary>
    public class GolfFeedback : MonoBehaviour
    {
        [SerializeField] Putter putter;
        [SerializeField] GolfBall ball;
        [SerializeField] VRRig rig;
        [SerializeField] CourseController course;
        [SerializeField] AudioClip strikeClip, wallClip, cupClip;

        AudioSource m_BallSource;

        public void Configure(Putter p, GolfBall b, VRRig r, CourseController c)
        {
            putter = p; ball = b; rig = r; course = c;
        }

        void Awake()
        {
            if (!strikeClip) strikeClip = Synth("strike", 0.05f, 2400f, 0.004f, 0.5f);
            if (!wallClip) wallClip = Synth("wall", 0.07f, 900f, 0.008f, 0.6f);
            if (!cupClip) cupClip = SynthCup();
            m_BallSource = ball.gameObject.AddComponent<AudioSource>();
            m_BallSource.spatialBlend = 1f;
            m_BallSource.minDistance = 0.5f;
            m_BallSource.maxDistance = 25f;
            m_BallSource.playOnAwake = false;
        }

        AudioClip m_ReturnClip;

        void OnEnable()
        {
            if (putter) putter.StruckBall += OnStrike;
            if (ball) ball.HitWall += OnWall;
            if (course)
            {
                course.HoleFinished += OnHoleFinished;
                foreach (var h in course.Holes) if (h) h.BallReturned += OnBallReturned;
            }
        }

        void OnDisable()
        {
            if (putter) putter.StruckBall -= OnStrike;
            if (ball) ball.HitWall -= OnWall;
            if (course)
            {
                course.HoleFinished -= OnHoleFinished;
                foreach (var h in course.Holes) if (h) h.BallReturned -= OnBallReturned;
            }
        }

        void OnBallReturned(HoleController hole, bool outOfBounds)
        {
            if (!m_ReturnClip) m_ReturnClip = SynthReturn();
            m_BallSource.pitch = outOfBounds ? 0.8f : 1f;
            m_BallSource.PlayOneShot(m_ReturnClip, 0.6f * GolfAudio.SfxVolume);
            if (rig) rig.Haptic(0.3f, 0.05f);
        }

        /// <summary>Short, low "pop" for the ball reappearing (the earlier rising tone sounded like a whistle).</summary>
        static AudioClip SynthReturn()
        {
            const int rate = 44100;
            int n = (int)(rate * 0.09f);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float f = Mathf.Lerp(420f, 180f, t / 0.09f);
                phase += 2f * Mathf.PI * f / rate;
                data[i] = Mathf.Sin(phase) * Mathf.Exp(-t * 45f) * 0.5f;
            }
            var clip = AudioClip.Create("return", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void OnStrike(Putter p, float speed)
        {
            float k = Mathf.Clamp01(speed / 4f);
            m_BallSource.pitch = 0.9f + 0.3f * k;
            m_BallSource.PlayOneShot(strikeClip, (0.25f + 0.75f * k) * GolfAudio.SfxVolume);
            if (rig) rig.Haptic(0.25f + 0.75f * k, 0.03f + 0.05f * k);
        }

        void OnWall(GolfBall b, float impact)
        {
            if (impact < 0.05f) return;
            float k = Mathf.Clamp01(impact / 3f);
            m_BallSource.pitch = 0.85f + 0.3f * k;
            m_BallSource.PlayOneShot(wallClip, (0.15f + 0.85f * k) * GolfAudio.SfxVolume);
        }

        void OnHoleFinished(CourseController c, HoleController hole)
        {
            if (hole.Cup) AudioSource.PlayClipAtPoint(cupClip, hole.Cup.transform.position, GolfAudio.SfxVolume);
            if (rig) rig.Haptic(0.6f, 0.15f);
        }

        /// <summary>Short knock: decaying sine plus a burst of noise.</summary>
        static AudioClip Synth(string name, float seconds, float freq, float noiseTime, float noiseAmt)
        {
            const int rate = 44100;
            int n = Mathf.CeilToInt(seconds * rate);
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Exp(-t * 70f);
                float tone = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * freq * 1.7f * t) * 0.25f;
                float noise = t < noiseTime ? ((float)rng.NextDouble() * 2f - 1f) * noiseAmt : 0f;
                data[i] = (tone * env + noise) * 0.9f;
            }
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Ball rattling into the cup, then a bright chime.</summary>
        static AudioClip SynthCup()
        {
            const int rate = 44100;
            int n = rate;
            var data = new float[n];
            var rng = new System.Random(7);
            float[] knocks = { 0f, 0.07f, 0.12f, 0.15f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float s = 0f;
                foreach (var k in knocks)
                {
                    float dt = t - k;
                    if (dt >= 0f && dt < 0.05f) s += Mathf.Sin(2f * Mathf.PI * 1300f * dt) * Mathf.Exp(-dt * 90f) * 0.5f
                                                     + ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-dt * 400f) * 0.2f;
                }
                float c = t - 0.25f;
                if (c > 0f)
                {
                    float env = Mathf.Exp(-c * 4f) * 0.25f;
                    s += (Mathf.Sin(2f * Mathf.PI * 1046.5f * c) + Mathf.Sin(2f * Mathf.PI * 1318.5f * c) * 0.7f
                          + Mathf.Sin(2f * Mathf.PI * 1568f * c) * 0.5f) * env;
                }
                data[i] = s;
            }
            var clip = AudioClip.Create("cup", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
