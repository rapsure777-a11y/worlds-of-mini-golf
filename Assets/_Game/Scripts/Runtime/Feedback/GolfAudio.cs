using System;
using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// Independent music and sound-effect volume settings, remembered between sessions (PlayerPrefs).
    /// <see cref="MusicScale"/> multiplies the world's music level (0.55 by default, Andrew's choice), so the default of 1 keeps that
    /// level and up to 1.8 raises it to full volume; <see cref="SfxVolume"/> scales every synthesised effect (default 1).
    /// Desktop keys, handled by <see cref="MusicPlayer"/>: F5/F6 music -/+, F7/F8 effects -/+. A VR settings UI is a later decision.
    /// World-agnostic.
    /// </summary>
    public static class GolfAudio
    {
        public const float DefaultMusicScale = 1f, DefaultSfx = 1f, MaxMusicScale = 1.8f;
        const string MusicKey = "gb.musicScale", SfxKey = "gb.sfxVolume";

        public static event Action Changed;

        static float s_Music = -1f, s_Sfx = -1f;

        public static float MusicScale
        {
            get { if (s_Music < 0f) s_Music = Mathf.Clamp(PlayerPrefs.GetFloat(MusicKey, DefaultMusicScale), 0f, MaxMusicScale); return s_Music; }
            set { s_Music = Mathf.Clamp(value, 0f, MaxMusicScale); PlayerPrefs.SetFloat(MusicKey, s_Music); Changed?.Invoke(); }
        }

        public static float SfxVolume
        {
            get { if (s_Sfx < 0f) s_Sfx = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, DefaultSfx)); return s_Sfx; }
            set { s_Sfx = Mathf.Clamp01(value); PlayerPrefs.SetFloat(SfxKey, s_Sfx); Changed?.Invoke(); }
        }

        /// <summary>Forget cached values and saved preferences, back to the defaults (tests).</summary>
        public static void ResetToDefaults()
        {
            PlayerPrefs.DeleteKey(MusicKey); PlayerPrefs.DeleteKey(SfxKey);
            s_Music = s_Sfx = -1f;
        }
    }
}
