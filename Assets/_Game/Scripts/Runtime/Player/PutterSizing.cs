using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// Walkabout-style automatic putter length. Estimates the player's standing eye height from a
    /// high percentile of observed head heights (so leaning over a putt doesn't shrink it), sizes the
    /// putter from that, and keeps the player's manual grip+stick adjustment as an offset on top.
    /// </summary>
    public class PutterSizing
    {
        const string PrefAuto = "putter.auto";
        const string PrefOffset = "putter.lengthOffset";
        const float MinEye = 0.9f, MaxEye = 2.2f, BinSize = 0.01f;
        const float Percentile = 0.9f;
        const int MinSamples = 90; // about 1 s at 90 Hz before the first estimate

        readonly int[] m_Bins = new int[Mathf.CeilToInt((MaxEye - MinEye) / BinSize) + 1];
        int m_Samples;

        public bool Auto { get; private set; }
        public float Offset { get; private set; }
        /// <summary>Estimated standing eye height, or 0 before enough samples.</summary>
        public float EyeHeight { get; private set; }

        public PutterSizing()
        {
            Auto = PlayerPrefs.GetInt(PrefAuto, 1) == 1;
            Offset = PlayerPrefs.GetFloat(PrefOffset, 0f);
        }

        public static float LengthFor(float eyeHeight, float ratio, float offset) => eyeHeight * ratio + offset;

        /// <summary>Feed the head's height above the floor once per frame.</summary>
        public void AddSample(float headHeight)
        {
            if (headHeight < MinEye || headHeight > MaxEye) return;
            m_Bins[Mathf.Clamp(Mathf.RoundToInt((headHeight - MinEye) / BinSize), 0, m_Bins.Length - 1)]++;
            m_Samples++;
            if (m_Samples < MinSamples) return;
            int target = Mathf.CeilToInt(m_Samples * Percentile), acc = 0;
            for (int i = 0; i < m_Bins.Length; i++)
            {
                acc += m_Bins[i];
                if (acc >= target) { EyeHeight = MinEye + i * BinSize; break; }
            }
        }

        /// <summary>Desired putter length, or null if auto-sizing is off or not calibrated yet.</summary>
        public float? TargetLength(GolfTuning tuning)
        {
            if (!Auto || EyeHeight <= 0f) return null;
            return Mathf.Clamp(LengthFor(EyeHeight, tuning.autoLengthRatio, Offset), tuning.minPutterLength, tuning.maxPutterLength);
        }

        public void AddOffset(float delta) => Offset = Mathf.Clamp(Offset + delta, -0.5f, 0.5f);

        public void Save()
        {
            PlayerPrefs.SetInt(PrefAuto, Auto ? 1 : 0);
            PlayerPrefs.SetFloat(PrefOffset, Offset);
            PlayerPrefs.Save();
        }
    }
}
