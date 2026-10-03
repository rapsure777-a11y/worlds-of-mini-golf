using System.Collections.Generic;
using Gamebreak.MiniGolf.XR;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace Gamebreak.MiniGolf.Tests
{
    /// <summary>
    /// The Unity OpenXR provider writes each controller's state as the profile's actions in order:
    /// binary = 1 byte, anything 4 bytes or larger aligned to 4, poses expand to
    /// isTracked(1) trackingState(4) position(12) rotation(16) velocity(12) angularVelocity(12).
    /// A layout whose offsets disagree reads garbage (the first Frame build read hand position from the
    /// middle of the rotation, so the putter swung erratically). These tests pin the layout down.
    /// </summary>
    public class FrameLayoutTests
    {
        enum F { Bin, Ax1, Ax2, Pose }

        static Dictionary<string, uint> NativeOffsets(params (string name, F type)[] actions)
        {
            var map = new Dictionary<string, uint>();
            uint offset = 0;
            void Add(string name, uint size)
            {
                if (size >= 4 && offset % 4 != 0) offset += 4 - offset % 4;
                map[name] = offset;
                offset += size;
            }
            foreach (var (name, type) in actions)
            {
                switch (type)
                {
                    case F.Bin: Add(name, 1); break;
                    case F.Ax1: Add(name, 4); break;
                    case F.Ax2: Add(name, 8); break;
                    case F.Pose:
                        Add(name + ".isTracked", 1); Add(name + ".trackingState", 4);
                        Add(name + ".position", 12); Add(name + ".rotation", 16);
                        Add(name + ".velocity", 12); Add(name + ".angularVelocity", 12);
                        break;
                }
            }
            return map;
        }

        [Test]
        public void Model_ReproducesUnityIndexProfileOffsets()
        {
            // Action order from Unity's ValveIndexControllerProfile.RegisterActionMapsWithRuntime.
            var m = NativeOffsets(("system", F.Bin), ("systemTouched", F.Bin), ("primaryButton", F.Bin), ("primaryTouched", F.Bin),
                ("secondaryButton", F.Bin), ("secondaryTouched", F.Bin), ("grip", F.Ax1), ("gripPressed", F.Bin), ("gripForce", F.Ax1),
                ("trigger", F.Ax1), ("triggerPressed", F.Bin), ("triggerTouched", F.Bin), ("thumbstick", F.Ax2), ("thumbstickClicked", F.Bin),
                ("thumbstickTouched", F.Bin), ("trackpad", F.Ax2), ("trackpadForce", F.Ax1), ("trackpadTouched", F.Bin),
                ("devicePose", F.Pose), ("pointer", F.Pose));
            // Unity's hand-written offsets for the Index layout (known to work on hardware).
            Assert.AreEqual(53u, m["devicePose.isTracked"]);
            Assert.AreEqual(56u, m["devicePose.trackingState"]);
            Assert.AreEqual(60u, m["devicePose.position"]);
            Assert.AreEqual(72u, m["devicePose.rotation"]);
            Assert.AreEqual(120u, m["pointer.position"]);
            Assert.AreEqual(132u, m["pointer.rotation"]);
        }

        [Test]
        public void SteamFrameLayout_MatchesNativeStateLayout()
        {
            // Must mirror the action order in SteamFrameControllerProfile.RegisterActionMapsWithRuntime.
            var m = NativeOffsets(("primaryButton", F.Bin), ("secondaryButton", F.Bin), ("xButton", F.Bin), ("yButton", F.Bin),
                ("dpadLeft", F.Bin), ("dpadRight", F.Bin), ("menu", F.Bin), ("grip", F.Ax1), ("gripPressed", F.Bin),
                ("trigger", F.Ax1), ("triggerPressed", F.Bin), ("thumbstick", F.Ax2), ("thumbstickClicked", F.Bin),
                ("devicePose", F.Pose), ("pointer", F.Pose));

            InputSystem.RegisterLayout<SteamFrameControllerProfile.SteamFrameController>();
            var device = InputSystem.AddDevice<SteamFrameControllerProfile.SteamFrameController>();
            try
            {
                uint Off(string control) => device[control].stateBlock.byteOffset - device.stateBlock.byteOffset;
                // Controls named exactly like an action get their offset from the runtime's generated
                // layout, so they only need to exist under that name.
                foreach (var name in new[] { "primaryButton", "secondaryButton", "xButton", "yButton", "dpadLeft", "dpadRight",
                             "menu", "grip", "gripPressed", "trigger", "triggerPressed", "thumbstick", "thumbstickClicked",
                             "devicePose", "pointer", "haptic" })
                    Assert.IsNotNull(device.TryGetChildControl(name), $"no control named after action '{name}'");
                // These have no action of their own; their hand-written offsets must point into the pose data.
                Assert.AreEqual(m["devicePose.isTracked"], Off("isTracked"), "isTracked");
                Assert.AreEqual(m["devicePose.trackingState"], Off("trackingState"), "trackingState");
                Assert.AreEqual(m["devicePose.position"], Off("devicePosition"), "devicePosition");
                Assert.AreEqual(m["devicePose.rotation"], Off("deviceRotation"), "deviceRotation");
                Assert.AreEqual(m["pointer.position"], Off("pointerPosition"), "pointerPosition");
                Assert.AreEqual(m["pointer.rotation"], Off("pointerRotation"), "pointerRotation");
            }
            finally
            {
                InputSystem.RemoveDevice(device);
            }
        }
    }
}
