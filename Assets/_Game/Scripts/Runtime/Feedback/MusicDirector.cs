using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gamebreak.MiniGolf
{
    /// <summary>One cluster's music: which holes it plays on and the clip (null until Andrew's file is imported).</summary>
    [Serializable]
    public class MusicEntry
    {
        public string clusterId;
        public int[] holes = Array.Empty<int>();
        public AudioClip clip;
        [Range(0f, 1.5f)] public float gain = 1f;
    }

    /// <summary>
    /// Cluster-based world music. Music belongs to an island cluster, not a hole: it keeps playing across holes of the same
    /// cluster and crossfades (two sources, ping-pong) when a new hole belongs to a different cluster. A cluster whose clip is
    /// missing keeps the current music playing and logs one warning, so checkpoints work before every track exists.
    /// Level = <see cref="GolfAudio.MusicVolume"/> (default 0.55) x the entry's gain. Reusable by every world.
    /// </summary>
    public class MusicDirector : MonoBehaviour
    {
        [SerializeField] MusicEntry[] entries = Array.Empty<MusicEntry>();
        [SerializeField] AudioClip fallbackClip;
        [SerializeField] float crossfadeSeconds = 3.5f;
        [SerializeField] float startFadeSeconds = 4f;

        public string CurrentClusterId { get; private set; }
        /// <summary>The source currently carrying the music (fading in or at full level).</summary>
        public AudioSource ActiveSource => m_Active >= 0 ? m_Sources[m_Active] : null;
        public int PlayingSourceCount { get { int n = 0; foreach (var s in m_Sources) if (s && s.isPlaying) n++; return n; } }
        public MusicEntry[] Entries => entries;

        readonly AudioSource[] m_Sources = new AudioSource[2];
        readonly float[] m_Target = new float[2];   // 0..1 share of the current music level
        readonly float[] m_Share = new float[2];
        readonly float[] m_Rate = new float[2];     // share per second
        readonly float[] m_Gain = new float[2];
        int m_Active = -1;
        CourseController m_Course;
        readonly System.Collections.Generic.HashSet<string> m_Warned = new System.Collections.Generic.HashSet<string>();

        public void Configure(CourseController course, MusicEntry[] musicEntries, AudioClip fallback)
        {
            m_Course = course; entries = musicEntries ?? Array.Empty<MusicEntry>(); fallbackClip = fallback;
        }

        void Awake()
        {
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject($"MusicSource{i}");
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.loop = true; s.spatialBlend = 0f; s.priority = 0; s.playOnAwake = false; s.volume = 0f;
                m_Sources[i] = s;
            }
        }

        void Start()
        {
            if (!m_Course) m_Course = FindFirstObjectByType<CourseController>();
            if (m_Course)
            {
                m_Course.HoleStarted += OnHoleStarted;
                if (m_Course.Current) OnHoleStarted(m_Course, m_Course.Current);
            }
            if (m_Active < 0) PlayFallback();
        }

        void OnDestroy()
        {
            if (m_Course) m_Course.HoleStarted -= OnHoleStarted;
        }

        void OnHoleStarted(CourseController c, HoleController hole) => NotifyHole(hole.HoleNumber);

        /// <summary>A hole is starting: switch cluster music if its cluster differs from the current one.</summary>
        public void NotifyHole(int holeNumber)
        {
            var entry = Find(holeNumber);
            if (entry == null) return;                       // unknown hole: keep what is playing
            if (entry.clusterId == CurrentClusterId) return; // same cluster: continuous playback
            if (!entry.clip)
            {
                if (m_Warned.Add(entry.clusterId))
                    Debug.LogWarning($"[Gamebreak] Music for cluster '{entry.clusterId}' is missing (import it under Assets/_Game/Audio/Music); keeping the current music.");
                return;
            }
            CrossfadeTo(entry);
        }

        public MusicEntry Find(int holeNumber)
        {
            foreach (var e in entries) if (e != null && Array.IndexOf(e.holes, holeNumber) >= 0) return e;
            return null;
        }

        void PlayFallback()
        {
            if (!fallbackClip) return;
            Begin(fallbackClip, 1f, startFadeSeconds);
        }

        void CrossfadeTo(MusicEntry entry)
        {
            CurrentClusterId = entry.clusterId;
            float seconds = m_Active < 0 ? startFadeSeconds : crossfadeSeconds;
            Begin(entry.clip, entry.gain, seconds);
        }

        void Begin(AudioClip clip, float gain, float seconds)
        {
            int next = m_Active < 0 ? 0 : 1 - m_Active;
            // The incoming source may still be fading out from an earlier change: reuse it from the start.
            var src = m_Sources[next];
            src.Stop();
            src.clip = clip;
            src.time = 0f;
            m_Share[next] = 0f; m_Target[next] = 1f; m_Gain[next] = gain;
            m_Rate[next] = 1f / Mathf.Max(0.05f, seconds);
            src.volume = 0f;
            src.Play();
            if (m_Active >= 0) { m_Target[m_Active] = 0f; m_Rate[m_Active] = 1f / Mathf.Max(0.05f, seconds); }
            m_Active = next;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.f5Key.wasPressedThisFrame) GolfAudio.MusicVolume -= 0.05f;
                if (kb.f6Key.wasPressedThisFrame) GolfAudio.MusicVolume += 0.05f;
                if (kb.f7Key.wasPressedThisFrame) GolfAudio.SfxVolume -= 0.1f;
                if (kb.f8Key.wasPressedThisFrame) GolfAudio.SfxVolume += 0.1f;
            }
            float master = GolfAudio.MusicVolume;
            for (int i = 0; i < 2; i++)
            {
                var s = m_Sources[i];
                if (!s || !s.isPlaying) continue;
                m_Share[i] = Mathf.MoveTowards(m_Share[i], m_Target[i], m_Rate[i] * Time.unscaledDeltaTime);
                s.volume = Mathf.Clamp01(m_Share[i] * master * m_Gain[i]);
                if (m_Target[i] <= 0f && m_Share[i] <= 0f) s.Stop();
            }
        }
    }
}
