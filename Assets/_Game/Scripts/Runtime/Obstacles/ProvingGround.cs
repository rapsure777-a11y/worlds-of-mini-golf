using UnityEngine;

namespace Gamebreak.MiniGolf
{
    public enum ProvingPreset { Easy, Default, Hard }

    /// <summary>
    /// Layout of the waterwheel demo hole around a <see cref="WaterwheelCarrierSpec"/>: a flat intake lane ending in a dock under the wheel,
    /// and an elevated exit channel that slopes down to a cup. Hole space: x across, z along the lane, floor of the intake lane at y = 0.
    /// </summary>
    [System.Serializable]
    public class WaterwheelHoleSpec
    {
        public WaterwheelCarrierSpec wheel = new WaterwheelCarrierSpec();
        public float laneWidth = 0.7f;
        [Tooltip("Distance from the tee end of the lane to the wheel's axle (the dock is under the axle).")]
        public float wheelZ = 3.0f;
        [Tooltip("Downhill slope of the exit channel. The ball keeps rolling above about 0.08.")]
        [Range(0.04f, 0.16f)] public float exitSlope = 0.09f;
        public float exitLength = 2.8f;
        public float railHeight = 0.12f;
        public float teeZ = 0.5f;

        static float Snap(float v) => Mathf.Round(v * 10f) / 10f;

        public float WheelZ => Snap(wheelZ);
        public float LaneWidth => Mathf.Max(0.4f, Snap(laneWidth));
        /// <summary>Axle height: the lowest pocket sits one ball radius above the intake floor.</summary>
        public Vector3 WheelCentre(float ballRadius) => new Vector3(0f, wheel.pocketRadius + ballRadius + TrayClearance - TroughDepth, WheelZ);
        /// <summary>The dock floor dips this far under the axle, sloping up at <see cref="TroughSlope"/> each way, so a ball in the dock rolls to the
        /// pick-up spot and waits there (the slope is above the rest-hold limit, so it cannot stop part-way). The wheel is lowered by the same amount.</summary>
        public const float TroughDepth = 0.054f, TroughSlope = 0.09f;
        /// <summary>Dock floor height along the lane (hole space): flat, then a shallow trough centred under the axle.</summary>
        public float DockFloor(float z) => -TroughDepth + TroughSlope * Mathf.Min(Mathf.Abs(z - WheelZ), TroughDepth / TroughSlope);
        /// <summary>The wheel sits this much higher so the tray floors pass above the dock floor instead of through it.</summary>
        public const float TrayClearance = 0.045f;
        /// <summary>Height of the curb that closes the dock: above the ball's radius (0.0214) so the ball cannot roll over it, below the trays' lowest edge.</summary>
        public const float DockCurbHeight = 0.026f;
        [Tooltip("Width of the dock under the wheel: wide enough that the wheel's arms and rims clear the rails.")]
        public float dockWidth = 1.3f;
        [Tooltip("Length of the wide dock, ending just past the axle. The end of the dock has no rail, so a ball that overshoots falls off (one stroke).")]
        public float dockLength = 0.8f;
        public float DockWidth => Mathf.Max(LaneWidth + 0.2f, Snap(dockWidth));
        public float DockStartZ => Snap(WheelZ + 0.1f - Mathf.Max(0.6f, dockLength));
        public float DockEndZ => WheelZ + 0.1f;
        public Vector3 ReleasePoint(float ballRadius)
        {
            Vector3 c = WheelCentre(ballRadius), r = wheel.ReleasePointLocal;
            return new Vector3(0f, c.y + r.y, c.z + r.x);
        }
        public float ChannelStartZ(float ballRadius) => Mathf.Max(WheelZ + 0.2f, Snap(ReleasePoint(ballRadius).z - 0.15f));
        public float ChannelEndZ(float ballRadius) => ChannelStartZ(ballRadius) + Mathf.Max(1.2f, Snap(exitLength));
        public float CupZ(float ballRadius) => ChannelEndZ(ballRadius) - 0.6f;
        public float ChannelFloorAtRelease(float ballRadius) => ReleasePoint(ballRadius).y - ballRadius - 0.002f;

        public GreenLayout BuildLayout(float ballRadius)
        {
            float zr = ReleasePoint(ballRadius).z, yf = ChannelFloorAtRelease(ballRadius);
            float z0 = ChannelStartZ(ballRadius), z1 = ChannelEndZ(ballRadius), cupZ = CupZ(ballRadius);
            float split = WheelZ + 0.15f;
            var l = new GreenLayout { wallHeight = railHeight };
            l.Area(-LaneWidth * 0.5f, 0f, LaneWidth, DockStartZ);             // intake lane
            l.Area(-DockWidth * 0.5f, DockStartZ, DockWidth, DockEndZ - DockStartZ);   // wide dock under the wheel
            l.openEdges.Add(new Rect(-DockWidth * 0.5f - 0.1f, DockEndZ - 0.005f, DockWidth + 0.2f, 0.01f));   // no rail at the dock's end: nothing in the buckets' way
            l.Area(-LaneWidth * 0.5f, z0, LaneWidth, z1 - z0);                // elevated exit channel
            l.cup = new Vector2(0f, cupZ);
            l.height = (x, z) => z < split ? DockFloor(z) : yf - exitSlope * Mathf.Clamp(z - zr, -0.5f, cupZ - 0.45f - zr);
            return l;
        }

        /// <summary>Human-readable problems with this combination (empty when it will work).</summary>
        public string Validate(float ballRadius)
        {
            float z0 = ChannelStartZ(ballRadius), zr = ReleasePoint(ballRadius).z;
            if (zr < z0 + 0.08f) return "release point is not over the exit channel: raise releaseAngle or move the channel start";
            if (zr > ChannelEndZ(ballRadius) - 1f) return "release point is too close to the end of the channel";
            if (wheel.releaseSpeed < 0.3f) return "releaseSpeed is too low for the ball to roll away from the bucket";
            return "";
        }
    }

    /// <summary>
    /// "Jump into the Bowl": a lane, a launch ramp and a short gap, then the roulette bowl with a notch cut in its wall where the ball lands.
    /// The bowl is offset sideways so the ball arrives at an angle (it orbits instead of crossing the middle). Hole space: x across the lane,
    /// z along it, approach level y = 0.
    /// </summary>
    [System.Serializable]
    public class JumpBowlSpec
    {
        public LaunchRampSpec ramp = new LaunchRampSpec();
        public RouletteBowlSpec bowl = new RouletteBowlSpec();
        [Tooltip("Sideways offset of the bowl centre from the lane. 0 = the ball enters head-on; larger = a more tangential entry.")]
        public float entryOffsetX = 0.6f;
        [Tooltip("Half-width of the notch in the bowl's wall, degrees.")]
        public float entryHalfAngle = 24f;
        [Header("Optional drop on the lane before the ramp (the valley hole)")]
        [Tooltip("How far the lane drops before the run-up to the ramp. 0 = a flat lane.")]
        public float dropHeight = 0f;
        public float dropStartZ = 1.4f, dropEndZ = 4.0f;

        public float EntryDz => Mathf.Sqrt(Mathf.Max(0.01f, bowl.radius * bowl.radius - entryOffsetX * entryOffsetX));
        /// <summary>Lane level (before the ramp's own rise) at z: 0 on the flat, down to -<see cref="dropHeight"/> after the drop.</summary>
        public float LaneBase(float z) => Slopes.RampZ(z, dropStartZ, dropEndZ, 0f, -dropHeight);
        /// <summary>Lane surface height: the drop plus the ramp's rise.</summary>
        public float LaneHeight(float x, float z) => LaneBase(z) + ramp.Height(x, z);
        /// <summary>The bowl's local origin in hole space: its shelf edge sits <see cref="LaunchRampSpec.padDrop"/> below the ramp lip, like the plain landing pad.</summary>
        public Vector3 BowlOrigin => new Vector3(entryOffsetX, LaneBase(ramp.LipZ) + ramp.LipHeight - ramp.padDrop - bowl.Height(bowl.radius), ramp.LipZ + ramp.Gap + EntryDz);
        /// <summary>The ground the bowl occupies (hole space XZ): for the terrain plateau and the foliage keep-out.</summary>
        public Rect BowlFootprint
        {
            get { var o = BowlOrigin; float r = bowl.radius + 0.4f; return new Rect(o.x - r, o.z - r, 2f * r, 2f * r); }
        }
        /// <summary>The lane as a green: run-up (with the drop) and ramp, an open lip, no cup. The bowl is added by <see cref="ProvingGround.BuildJumpBowlPieces"/>.</summary>
        public GreenLayout LaneLayout()
        {
            var layout = new GreenLayout { wallHeight = ramp.railHeight };
            layout.Area(-ramp.Width * 0.5f, 0f, ramp.Width, ramp.LipZ);
            layout.openEdges.Add(new Rect(-ramp.Width * 0.5f - 0.1f, ramp.LipZ - 0.005f, ramp.Width + 0.2f, 0.01f));
            layout.height = LaneHeight;
            return layout;
        }
        public RouletteBowlSpec BowlForHole()
        {
            var b = bowl.Clone();
            b.entryAngleDegrees = Mathf.Atan2(-EntryDz, -entryOffsetX) * Mathf.Rad2Deg;
            b.entryHalfWidthDegrees = entryHalfAngle;
            return b;
        }
    }

    /// <summary>
    /// Builds the proving-ground demonstration holes at runtime, with the real hole code (<see cref="HoleController"/>, <see cref="Cup"/>,
    /// <see cref="GolfBall"/>). Used by the editor scene builder and by the PlayMode tests, so what is tested is what is demonstrated.
    /// </summary>
    public static class ProvingGround
    {
        public static LaunchRampSpec LaunchSpec(ProvingPreset p)
        {
            var s = new LaunchRampSpec();
            if (p == ProvingPreset.Easy) { s.gap = 0.2f; s.padWidth = 1.8f; s.padDrop = 0.08f; s.rampAngleDegrees = 12f; }
            else if (p == ProvingPreset.Hard) { s.gap = 0.5f; s.padWidth = 1.2f; s.padDrop = 0.04f; s.rampAngleDegrees = 16f; }
            return s;
        }

        public static WaterwheelHoleSpec WheelSpec(ProvingPreset p)
        {
            var s = new WaterwheelHoleSpec();
            if (p == ProvingPreset.Easy) { s.wheel.periodSeconds = 10f; s.wheel.bucketCount = 5; s.wheel.captureRadius = 0.14f; s.wheel.maxCaptureSpeed = 2.0f; }
            else if (p == ProvingPreset.Hard) { s.wheel.periodSeconds = 18f; s.wheel.bucketCount = 3; s.wheel.captureRadius = 0.09f; s.wheel.maxCaptureSpeed = 1.2f; }
            return s;
        }

        public static RouletteBowlSpec BowlSpec(ProvingPreset p)
        {
            var s = new RouletteBowlSpec();
            if (p == ProvingPreset.Easy) { s.coneSlope = 0.105f; s.shelfSlope = 0.07f; }
            else if (p == ProvingPreset.Hard) { s.coneSlope = 0.095f; s.shelfSlope = 0.06f; }
            return s;
        }

        static GameObject NewRoot(Transform parent, string name, Vector3 position)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            return root;
        }

        public static HoleController BuildLaunchHole(Transform parent, GolfTuning tuning, GolfBall ball, ProvingMaterials mats, LaunchRampSpec spec,
            Vector3 position, int number = 1)
        {
            var root = NewRoot(parent, $"Hole{number:00}_LaunchRamp", position);
            var ramp = root.AddComponent<LaunchRamp>();
            ramp.Configure(spec, tuning, mats);
            ramp.Rebuild();
            return ProvingKit.AttachHole(root, number, 2, new Vector3(spec.Tee.x, spec.Height(spec.Tee.x, spec.Tee.y), spec.Tee.y), ramp.Cup, tuning, ball, mats.tee);
        }

        public static HoleController BuildWaterwheelHole(Transform parent, GolfTuning tuning, GolfBall ball, ProvingMaterials mats, WaterwheelHoleSpec spec,
            Vector3 position, int number = 2)
        {
            float r = tuning.ballRadius;
            var root = NewRoot(parent, $"Hole{number:00}_Waterwheel", position);
            var layout = spec.BuildLayout(r);
            var green = CourseGeometry.CreateGreen("WaterwheelGreen", layout, tuning, root.transform, mats.green, mats.cup, mats.wall, mats.flag, null, out Cup cup);

            Vector3 centre = spec.WheelCentre(r);
            var wheelGo = new GameObject("Waterwheel");
            wheelGo.transform.SetParent(root.transform, false);
            wheelGo.transform.localPosition = centre;
            wheelGo.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);      // carrier +X (the ball's travel direction) -> hole +Z
            var carrier = wheelGo.AddComponent<WaterwheelCarrier>();
            carrier.Configure(spec.wheel, ball, mats);

            // A low curb closes the dock: taller than the ball's radius so the ball cannot roll over it, lower than the bucket trays so
            // nothing clips. (A full-height end rail sits right in the buckets' path.)
            float curbTop = spec.DockFloor(spec.DockEndZ) + WaterwheelHoleSpec.DockCurbHeight;
            ProvingKit.Box("DockCurb", root.transform, new Vector3(0f, (curbTop - 0.2f) * 0.5f, spec.DockEndZ + 0.03f), Quaternion.identity,
                new Vector3(spec.DockWidth, curbTop + 0.2f, 0.06f), mats.wall, true);

            // Decorative supports under the elevated channel (no colliders).
            float yf = spec.ChannelFloorAtRelease(r), z0 = spec.ChannelStartZ(r), z1 = spec.ChannelEndZ(r);
            foreach (float z in new[] { z0 + 0.3f, z1 - 0.3f })
            {
                float top = yf - spec.exitSlope * (z - spec.ReleasePoint(r).z) - 0.03f;
                ProvingKit.Box("ChannelSupport", root.transform, new Vector3(0f, (top - 0.6f) * 0.5f, z), Quaternion.identity,
                    new Vector3(spec.LaneWidth + 0.1f, top + 0.6f, 0.18f), mats.wood, false);
            }
            return ProvingKit.AttachHole(root, number, 2, new Vector3(0f, 0f, spec.teeZ), cup, tuning, ball, mats.tee);
        }

        public static HoleController BuildRouletteHole(Transform parent, GolfTuning tuning, GolfBall ball, ProvingMaterials mats, RouletteBowlSpec spec,
            Vector3 position, int number = 3)
        {
            var root = NewRoot(parent, $"Hole{number:00}_RouletteBowl", position);
            var bowl = root.AddComponent<RouletteBowl>();
            bowl.Configure(spec, tuning, mats);
            bowl.Rebuild();
            return ProvingKit.AttachHole(root, number, 2, spec.TeeLocal, bowl.Cup, tuning, ball, mats.tee);
        }

        public static JumpBowlSpec JumpBowlSpecFor(ProvingPreset p) => new JumpBowlSpec { ramp = LaunchSpec(p), bowl = BowlSpec(p) };

        public static HoleController BuildJumpBowlHole(Transform parent, GolfTuning tuning, GolfBall ball, ProvingMaterials mats, JumpBowlSpec spec,
            Vector3 position, int number = 4)
        {
            var root = NewRoot(parent, $"Hole{number:00}_JumpIntoBowl", position);
            var r = spec.ramp;
            CourseGeometry.CreateGreen("JumpLane", spec.LaneLayout(), tuning, root.transform, mats.green, mats.cup, mats.wall, mats.flag, null, out Cup unused);
            Cup cup = BuildJumpBowlPieces(root.transform, tuning, mats, spec);
            return ProvingKit.AttachHole(root, number, 3, new Vector3(0f, spec.LaneHeight(0f, r.teeZ), r.teeZ), cup, tuning, ball, mats.tee);
        }

        /// <summary>The bowl (with its entry notch) and the pit under the gap, as children of <paramref name="root"/> (hole space). Returns the bowl's cup.</summary>
        public static Cup BuildJumpBowlPieces(Transform root, GolfTuning tuning, ProvingMaterials mats, JumpBowlSpec spec)
        {
            var r = spec.ramp;
            var bowlGo = new GameObject("Bowl");
            bowlGo.transform.SetParent(root, false);
            bowlGo.transform.localPosition = spec.BowlOrigin;
            var bowl = bowlGo.AddComponent<RouletteBowl>();
            bowl.Configure(spec.BowlForHole(), tuning, mats);
            bowl.Rebuild();

            // The pit under the gap: touching it is out of bounds (a short jump), as on the plain launch hole.
            float lipLevel = spec.LaneBase(r.LipZ);
            ProvingKit.Box("GapFloor", root, new Vector3(0f, lipLevel - r.gapDepth - 0.05f, r.LipZ + (r.Gap + 1.0f) * 0.5f), Quaternion.identity,
                new Vector3(r.Width + 1.6f, 0.1f, r.Gap + 1.0f), mats.wood, true, false, true);
            return bowl.Cup;
        }

        /// <summary>The demonstration holes in a row, plus a distant out-of-bounds ground slab.</summary>
        public static HoleController[] BuildAll(Transform parent, GolfTuning tuning, GolfBall ball, ProvingMaterials mats, ProvingPreset preset, Material groundMat = null)
        {
            var holes = new[]
            {
                BuildLaunchHole(parent, tuning, ball, mats, LaunchSpec(preset), new Vector3(0f, 0f, 0f), 1),
                BuildWaterwheelHole(parent, tuning, ball, mats, WheelSpec(preset), new Vector3(6f, 0f, 0f), 2),
                BuildRouletteHole(parent, tuning, ball, mats, BowlSpec(preset), new Vector3(14f, 0f, 0f), 3),
                BuildJumpBowlHole(parent, tuning, ball, mats, JumpBowlSpecFor(preset), new Vector3(22f, 0f, 0f), 4),
            };
            ProvingKit.Box("Ground", parent, new Vector3(7f, -0.65f, 3f), Quaternion.identity, new Vector3(40f, 0.1f, 24f), groundMat, true, false, true);
            return holes;
        }
    }
}
