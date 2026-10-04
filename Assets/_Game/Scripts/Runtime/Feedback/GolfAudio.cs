using System;
using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// Independent music and sound-effect volume settings, remembered between sessions (PlayerPrefs).
    /// The default music level is 0.55 (Andrew's choice). Desktop keys: F5/F6 music -/+, F7/F8 effects -/+
    /// (handled by <see cref="MusicDirector"/>); VR settings UI is a later decision. World-agnostic.
    /// </summary>
    public static class GolfAudio
    {
        public const float DefaultMusic = 0.55f, DefaultSfx = 1f;
        const string MusicKey = "gb.musicVolume", SfxKey = "gb.sfxVolume";

        public static event Action Changed;

        static float s_Music = -1f, s_Sfx = -1f;

        public static float MusicVolume
        {
            get { if (s_Music < 0f) s_Music = PlayerPrefs.GetFloat(MusicKey, DefaultMusic); return s_Music; }
            set { s_Music = Mathf.Clamp01(value); PlayerPrefs.SetFloat(MusicKey, s_Music); Changed?.Invoke(); }
        }

        public static float SfxVolume
        {
            get { if (s_Sfx < 0f) s_Sfx = PlayerPrefs.GetFloat(SfxKey, DefaultSfx); return s_Sfx; }
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
