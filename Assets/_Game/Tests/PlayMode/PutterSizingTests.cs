using NUnit.Framework;
using UnityEngine;

namespace Gamebreak.MiniGolf.Tests
{
    public class PutterSizingTests
    {
        [SetUp] public void SetUp() { PlayerPrefs.DeleteKey("putter.auto"); PlayerPrefs.DeleteKey("putter.lengthOffset"); }

        [Test]
        public void EyeHeight_IgnoresTimeSpentBentOverPutts()
        {
            var s = new PutterSizing();
            // 70% of frames bent over a putt (1.35 m), 30% standing (1.65 m).
            for (int i = 0; i < 1000; i++) s.AddSample(i % 10 < 7 ? 1.35f : 1.65f);
            // The 90th percentile lands in the standing samples.
            Assert.AreEqual(1.65f, s.EyeHeight, 0.011f);
        }

        [Test]
        public void TargetLength_IsAbout20PercentLongerThanOldDefault()
        {
            var tuning = ScriptableObject.CreateInstance<GolfTuning>();
            var s = new PutterSizing();
            for (int i = 0; i < 200; i++) s.AddSample(1.65f);
            float len = s.TargetLength(tuning).Value;
            Debug.Log($"[Test] auto putter length at 1.65 m eye height: {len:F3} m");
            Assert.AreEqual(tuning.defaultPutterLength * 1.2f, len, 0.02f);
            Object.DestroyImmediate(tuning);
        }

        [Test]
        public void NoEstimate_UntilEnoughSamples_AndOutliersIgnored()
        {
            var tuning = ScriptableObject.CreateInstance<GolfTuning>();
            var s = new PutterSizing();
            for (int i = 0; i < 50; i++) s.AddSample(1.7f);
            for (int i = 0; i < 500; i++) s.AddSample(0f); // untracked headset reports 0
            Assert.IsNull(s.TargetLength(tuning));
            Object.DestroyImmediate(tuning);
        }

        [Test]
        public void ManualOffset_AddsOnTop()
        {
            var tuning = ScriptableObject.CreateInstance<GolfTuning>();
            var s = new PutterSizing();
            for (int i = 0; i < 200; i++) s.AddSample(1.65f);
            float before = s.TargetLength(tuning).Value;
            s.AddOffset(0.05f);
            Assert.AreEqual(before + 0.05f, s.TargetLength(tuning).Value, 1e-4f);
            Object.DestroyImmediate(tuning);
        }
    }
}
