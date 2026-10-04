using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// Looping background music for a world (2D, quiet under the golf sounds), faded in at start.
    /// The clip and level come from the world's <see cref="WorldTheme"/>; golf code never touches it.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class MusicPlayer : MonoBehaviour
    {
        [SerializeField] AudioClip m_Clip;
        [SerializeField, Range(0f, 1f)] float m_Volume = 0.55f;
        [SerializeField] float m_FadeInSeconds = 4f;

        AudioSource m_Source;

        public void Configure(AudioClip clip, float volume)
        {
            m_Clip = clip;
            m_Volume = volume;
        }

        void Start()
        {
            if (!m_Clip) return;
            m_Source = GetComponent<AudioSource>();
            m_Source.clip = m_Clip;
            m_Source.loop = true;
            m_Source.spatialBlend = 0f;
            m_Source.priority = 0;
            m_Source.playOnAwake = false;
            m_Source.volume = 0f;
            m_Source.Play();
        }

        void Update()
        {
            if (!m_Source || m_Source.volume >= m_Volume) return;
            m_Source.volume = Mathf.Min(m_Volume, m_Source.volume + m_Volume * Time.unscaledDeltaTime / Mathf.Max(0.01f, m_FadeInSeconds));
        }
    }
}
