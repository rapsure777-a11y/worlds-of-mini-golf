using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// Cluster-based world music. Each island/area cluster (a range of holes) has one track:
    /// - holes in the same cluster keep the track playing without a restart;
    /// - entering a new cluster crossfades to its track;
    /// - each track loops by crossfading its tail into a fresh start, so loops are smooth even if the
    ///   recording's end and start don't match exactly.
    /// Two 2D AudioSources alternate (never more than two playing). A cluster without a clip keeps the
    /// current music and logs a warning once. Clusters come from the world's <see cref="WorldTheme"/>, and the
    /// player follows <see cref="CourseController.HoleStarted"/>, so golf code never references music.
    /// </summary>
    public class MusicPlayer : MonoBehaviour
    {
        [SerializeField] MusicCluster[] m_Clusters = new MusicCluster[0];
        [SerializeField] AudioClip m_Fallback;
        [SerializeField, Range(0f, 1f)] float m_Volume = 0.55f;
        [SerializeField] float m_FadeInSeconds = 4f;
        [SerializeField] float m_ClusterCrossfadeSeconds = 3f;
        [SerializeField] float m_LoopCrossfadeSeconds = 2.5f;
        [SerializeField] CourseController m_Course;

        readonly AudioSource[] m_Sources = new AudioSource[2];
        readonly float[] m_From = new float[2], m_To = new float[2];
        float m_FadeStart, m_FadeLength;
        int m_Active = -1;
        bool m_Started;

        /// <summary>Track currently fading in or playing, for tests and the session log.</summary>
        public AudioClip CurrentClip => m_Active >= 0 ? m_Sources[m_Active].clip : null;
        public AudioSource ActiveSource => m_Active >= 0 ? m_Sources[m_Active] : null;
        public int PlayingSourceCount { get { int n = 0; foreach (var s in m_Sources) if (s && s.isPlaying) n++; return n; } }

        /// <summary>Music level (0..1). Defaults to the world's level (0.55).</summary>
        public float Volume
        {
            get => m_Volume;
            set { m_Volume = Mathf.Clamp01(value); for (int i = 0; i < 2; i++) m_To[i] = i == m_Active ? m_Volume : 0f; }
        }

        public void Configure(MusicCluster[] clusters, AudioClip fallback, float volume, CourseController course)
        {
            m_Clusters = clusters ?? new MusicCluster[0];
            m_Fallback = fallback;
            m_Volume = volume;
            m_Course = course;
        }

        void Awake()
        {
            for (int i = 0; i < 2; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = false; // looping is done by crossfading between the two sources
                s.spatialBlend = 0f;
                s.priority = 0;
                s.volume = 0f;
                m_Sources[i] = s;
            }
        }

        void OnEnable() { if (m_Course) m_Course.HoleStarted += OnHoleStarted; }
        void OnDisable() { if (m_Course) m_Course.HoleStarted -= OnHoleStarted; }

        void Start()
        {
            if (m_Started) return;
            var current = m_Course ? m_Course.Current : null;
            PlayFor(current ? current.HoleNumber : 1, m_FadeInSeconds);
        }

        void OnHoleStarted(CourseController course, HoleController hole) => PlayFor(hole.HoleNumber, m_ClusterCrossfadeSeconds);

        /// <summary>Make the music right for this hole: no change within a cluster, crossfade into a new one.</summary>
        public void PlayFor(int holeNumber, float fadeSeconds)
        {
            var cluster = MusicCluster.For(m_Clusters, holeNumber);
            var clip = cluster != null ? cluster.clip : m_Fallback;
            if (!clip)
            {
                if (cluster != null) Debug.LogWarning($"[Gamebreak] Music cluster '{cluster.name}' (hole {holeNumber}) has no clip; keeping the current music.");
                if (m_Active < 0 && m_Fallback) clip = m_Fallback; else return;
            }
            m_Started = true;
            if (CurrentClip == clip) return; // same cluster: keep playing without a restart
            CrossfadeTo(clip, 0f, fadeSeconds);
        }

        void CrossfadeTo(AudioClip clip, float startTime, float seconds)
        {
            int next = m_Active < 0 ? 0 : 1 - m_Active;
            var s = m_Sources[next];
            s.Stop();
            s.clip = clip;
            s.time = Mathf.Clamp(startTime, 0f, Mathf.Max(0f, clip.length - 0.1f));
            s.volume = 0f;
            s.Play();
            for (int i = 0; i < 2; i++) { m_From[i] = m_Sources[i].volume; m_To[i] = i == next ? m_Volume : 0f; }
            m_FadeStart = Time.unscaledTime;
            m_FadeLength = Mathf.Max(0.01f, seconds);
            m_Active = next;
        }

        void Update()
        {
            if (m_Active < 0) return;
            float t = Mathf.Clamp01((Time.unscaledTime - m_FadeStart) / m_FadeLength);
            float eased = t * t * (3f - 2f * t);
            for (int i = 0; i < 2; i++)
            {
                var s = m_Sources[i];
                s.volume = Mathf.Lerp(m_From[i], m_To[i], eased);
                if (t >= 1f && i != m_Active && s.isPlaying) s.Stop();
            }
            // Loop: crossfade the tail of the active track into a fresh start of the same track.
            var a = m_Sources[m_Active];
            if (a.isPlaying && a.clip && a.clip.length > m_LoopCrossfadeSeconds * 4f && t >= 1f
                && a.time >= a.clip.length - m_LoopCrossfadeSeconds)
                CrossfadeTo(a.clip, 0f, m_LoopCrossfadeSeconds);
            else if (!a.isPlaying && a.clip && t >= 1f)
                CrossfadeTo(a.clip, 0f, 0.05f); // safety: a source that stopped unexpectedly restarts
        }
    }
}
