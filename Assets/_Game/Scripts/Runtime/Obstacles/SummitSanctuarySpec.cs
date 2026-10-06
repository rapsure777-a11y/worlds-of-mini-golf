using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// Hole 9, "Summit Sanctuary": the finale. One rectangle-union green with a height function (like Holes 5 and 8), <see cref="BankWall"/> kickers, the proven
    /// launch jump (open lip, low pad lip, pit) and open edges over out-of-bounds drops. No bowl and no new mechanics: the finale is a long, exposed putt.
    /// Plan (x across, z up the mountain; metres; defaults):
    ///
    ///   1 Summit Approach   a broad 2 m lane (z 0..5) rising 0.20 m at 5%: the sanctuary is straight ahead.
    ///   2 Sanctuary Climb   Terrace 1 (z 5..6.6); the safe route goes east, a corner kicker turns the ball north up the Waterfall Climb (x 3.4..4.4, z 6.6..12.5,
    ///                       5% to 0.50 m, open west side over the waterfall chasm), a kicker turns it west onto the Merge Terrace (z 12.5..13.9).
    ///   3 Hero Shortcut     from Terrace 1 a launch ramp (14.6 degrees, as on the Sun Stair) jumps a 0.5 m gap onto the Overlook (x -1..2.4, z 8.1..12.5), the
    ///                       straight line north that skips the whole detour. Short = the pit (+1 stroke, back to the last rest spot); soft = rolls back down.
    ///   4 Final Approach    the Shrine Lane (x -1..0.4, z 13.9..19): a 0.7 m shrine gate to thread, a calm 3% rise, a kicker that turns the ball east at the top
    ///                       onto the Landing, a railed pad where the ball can rest and be struck again.
    ///   5 The Sky Bridge    the finale: a 1 m wide, 3.2 m long ridge climbing 0.10 m, with NO rails, open to the abyss on both sides (out of bounds: +1 stroke and
    ///                       back to the last rest spot), ending at the Altar (a 2 x 2 m platform) and the cup. A straight, honest putt over a cliff.
    ///
    /// Both routes join on the Merge Terrace and use the Shrine Lane and the Sky Bridge, so the hero shot saves the detour (about 9 m, normally a stroke or more),
    /// not the finale.
    /// </summary>
    [System.Serializable]
    public class SummitSanctuarySpec
    {
        [Header("Approach and Terrace 1")]
        public float width = 2.0f;
        public float approachLength = 5.0f;
        [Tooltip("The approach starts rising this far up the lane (the tee is on the flat before it).")]
        public float riseStartZ = 1.2f;
        public float terrace1Length = 1.6f;
        public float terrace1EastX = 4.4f;
        public float terrace1Height = 0.20f;
        [Header("Hero jump (proven Sun Stair numbers)")]
        public float jumpRun = 1.0f;
        public float jumpRampWidth = 1.0f;
        public float gap = 0.5f;
        [Tooltip("The Overlook's near edge sits this far below the jump's lip.")]
        public float padDrop = 0.08f;
        [Tooltip("The Overlook is this much higher than Terrace 1 where the jump lands.")]
        public float step = 0.18f;
        [Header("Climb, Merge Terrace, Shrine Lane")]
        public float climbWidth = 1.0f;
        public float climbEndZ = 12.5f;
        public float mergeHeight = 0.50f;
        public float mergeLength = 1.4f;
        public float chasmWidth = 1.0f;
        public float shrineLaneEastX = 0.4f;
        public float shrineGateZ = 15.8f;
        public float shrineGateWidth = 0.7f;
        [Tooltip("Height of the Shrine Lane's top and the Landing.")]
        public float summitLevel = 0.62f;
        public float shrineLaneTopZ = 19.0f;
        [Header("Landing, Sky Bridge, Altar")]
        public float laneWidth = 1.0f;
        public float landingLength = 1.4f;
        public float bridgeLength = 3.2f;
        public float altarHeight = 0.72f;
        public float altarSize = 2.0f;
        [Tooltip("Cup distance from the Altar's near (west) edge, and its offset from the lane's centre line.")]
        public float cupFromAltarEdge = 1.2f;
        public float cupOffsetZ = 0.0f;
        [Header("Walls")]
        public float railHeight = 0.12f;
        public float wallHeight = 0.16f;

        static float Snap(float v) => Mathf.Round(v * 10f) / 10f;

        const float Tol = 0.05f;   // vertices on a boundary belong to the higher-numbered side, so lanes keep their full width

        // ---------------- geometry (hole space)

        public float Width => Snap(width);
        public float Left => -Width * 0.5f;
        public float ApproachEndZ => Snap(approachLength);
        public float Terrace1EndZ => ApproachEndZ + Snap(terrace1Length);
        public float Terrace1EastX => Snap(terrace1EastX);
        public float T1 => terrace1Height;
        public float JumpRise => step + padDrop;
        public float LipHeight => T1 + JumpRise;
        public float JumpAngleDegrees => Mathf.Atan(JumpRise / Snap(jumpRun)) * Mathf.Rad2Deg;
        public float RampWestX => -0.8f;
        public float RampEastX => RampWestX + Snap(jumpRampWidth);
        public float LipZ => Terrace1EndZ + Snap(jumpRun);
        public float Gap => Mathf.Max(0.2f, Snap(gap));
        public float OverlookZ => LipZ + Gap;
        public float OverlookHeight => T1 + step;
        public float ClimbEndZ => Snap(climbEndZ);
        public float ClimbEastX => Terrace1EastX;
        public float ClimbWestX => ClimbEastX - Snap(climbWidth);
        public float OverlookEastX => ClimbWestX - Snap(chasmWidth);
        public float MergeHeight => mergeHeight;
        public float MergeEndZ => ClimbEndZ + Snap(mergeLength);
        public float ClimbSlope => (MergeHeight - T1) / (ClimbEndZ - Terrace1EndZ);
        public float OverlookSlope => (MergeHeight - OverlookHeight) / (ClimbEndZ - (OverlookZ + 1.0f));
        public float ShrineWestX => Left;
        public float ShrineEastX => Snap(shrineLaneEastX);
        /// <summary>Centre x of the shrine gate's opening.</summary>
        public float ShrineGateX => (ShrineWestX + ShrineEastX) * 0.5f;
        public float SummitLevel => summitLevel;
        public float ShrineTopZ => Snap(shrineLaneTopZ);
        /// <summary>Where the Shrine Lane finishes rising (one kicker length below its top).</summary>
        public float ShrineRiseEndZ => ShrineTopZ - (ShrineEastX - ShrineWestX);
        /// <summary>The final lane (Landing, Sky Bridge) runs east along z [FinalZ0, FinalZ1], the Shrine Lane's top.</summary>
        public float FinalZ1 => ShrineTopZ;
        public float FinalZ0 => ShrineTopZ - Snap(laneWidth);
        public float LandingEndX => ShrineEastX + Snap(landingLength);
        public float BridgeEndX => LandingEndX + Snap(bridgeLength);
        public float AltarWestX => BridgeEndX;
        public float AltarEastX => AltarWestX + Snap(altarSize);
        public float AltarZ0 => (FinalZ0 + FinalZ1) * 0.5f - Snap(altarSize) * 0.5f;
        public float AltarZ1 => AltarZ0 + Snap(altarSize);
        public float AltarHeight => altarHeight;
        public float BridgeSlope => (AltarHeight - SummitLevel) / (BridgeEndX - LandingEndX);
        public Vector2 Cup => new Vector2(Snap(AltarWestX + cupFromAltarEdge), Snap((FinalZ0 + FinalZ1) * 0.5f + cupOffsetZ));
        public Vector2 Tee => new Vector2(0f, 0.8f);

        /// <summary>Surface height at hole-space (x, z).</summary>
        public float Height(float x, float z)
        {
            if (z >= MergeEndZ - Tol)
            {
                if (x >= AltarWestX - Tol) return AltarHeight;                                                  // the Altar
                if (x >= ShrineEastX - Tol && z >= FinalZ0 - Tol)                                              // the Landing and the Sky Bridge
                    return SummitLevel + (AltarHeight - SummitLevel) * Mathf.Clamp01((x - LandingEndX) / Mathf.Max(0.1f, BridgeEndX - LandingEndX));
                return MergeHeight + (SummitLevel - MergeHeight) * Mathf.Clamp01((z - MergeEndZ) / Mathf.Max(0.1f, ShrineRiseEndZ - MergeEndZ));   // the Shrine Lane
            }
            if (z >= ClimbEndZ - Tol) return MergeHeight;                                                       // the Merge Terrace
            if (x >= ClimbWestX - Tol && z >= Terrace1EndZ - Tol)                                               // the Waterfall Climb
                return T1 + (MergeHeight - T1) * Mathf.Clamp01((z - Terrace1EndZ) / (ClimbEndZ - Terrace1EndZ));
            if (z >= OverlookZ - Tol)                                                                           // the Overlook: flat where the jump lands, then a gentle rise
                return OverlookHeight + (MergeHeight - OverlookHeight) * Mathf.Clamp01((z - (OverlookZ + 1.0f)) / (ClimbEndZ - (OverlookZ + 1.0f)));
            if (z >= Terrace1EndZ - Tol) return T1 + JumpRise * Mathf.Clamp01((z - Terrace1EndZ) / Snap(jumpRun));   // the launch ramp
            if (z >= ApproachEndZ - Tol) return T1;                                                             // Terrace 1
            return T1 * Mathf.Clamp01((z - riseStartZ) / Mathf.Max(0.1f, ApproachEndZ - riseStartZ));          // the Summit Approach
        }

        public GreenLayout BuildLayout()
        {
            var l = new GreenLayout { wallHeight = railHeight };
            l.Area(Left, 0f, Width, ApproachEndZ);                                                              // 1 the approach
            l.Area(Left, ApproachEndZ, Terrace1EastX - Left, Terrace1EndZ - ApproachEndZ);                      // Terrace 1
            l.Area(RampWestX, Terrace1EndZ, RampEastX - RampWestX, LipZ - Terrace1EndZ);                        // the launch ramp
            l.Area(Left, OverlookZ, OverlookEastX - Left, ClimbEndZ - OverlookZ);                               // 3 the Overlook
            l.Area(ClimbWestX, Terrace1EndZ, ClimbEastX - ClimbWestX, ClimbEndZ - Terrace1EndZ);                // 2 the Waterfall Climb
            l.Area(Left, ClimbEndZ, Terrace1EastX - Left, MergeEndZ - ClimbEndZ);                               // the Merge Terrace
            l.Area(Left, MergeEndZ, ShrineEastX - Left, ShrineTopZ - MergeEndZ);                                // 4 the Shrine Lane
            l.Area(ShrineEastX, FinalZ0, BridgeEndX - ShrineEastX, FinalZ1 - FinalZ0);                          // the Landing and the Sky Bridge
            l.Area(AltarWestX, AltarZ0, AltarEastX - AltarWestX, AltarZ1 - AltarZ0);                            // 5 the Altar
            // Open edges (no rail): the ramp's lip, the Overlook's near edge, both sides of the waterfall chasm, and both sides of the Sky Bridge.
            l.openEdges.Add(new Rect(RampWestX - 0.1f, LipZ - 0.005f, RampEastX - RampWestX + 0.2f, 0.01f));
            l.openEdges.Add(new Rect(Left - 0.1f, OverlookZ - 0.005f, OverlookEastX - Left + 0.2f, 0.01f));
            l.openEdges.Add(new Rect(OverlookEastX - 0.005f, OverlookZ, 0.01f, ClimbEndZ - OverlookZ));
            l.openEdges.Add(new Rect(ClimbWestX - 0.005f, Terrace1EndZ, 0.01f, ClimbEndZ - Terrace1EndZ));
            l.openEdges.Add(new Rect(LandingEndX, FinalZ0 - 0.005f, BridgeEndX - LandingEndX, 0.01f));
            l.openEdges.Add(new Rect(LandingEndX, FinalZ1 - 0.005f, BridgeEndX - LandingEndX, 0.01f));
            l.cup = Cup;
            l.height = Height;
            return l;
        }

        Vector3 P(float x, float z) => new Vector3(x, Height(x, z), z);

        /// <summary>Angled walls with their end points (hole space), also used by tests.</summary>
        public List<(string name, Vector3 a, Vector3 b)> WallPlan()
        {
            float cw = Snap(climbWidth), tw = Terrace1EastX - 3.0f, kl = ShrineEastX - ShrineWestX;
            float zg = Snap(shrineGateZ), half = shrineGateWidth * 0.5f, gateX = ShrineGateX;
            return new List<(string, Vector3, Vector3)>
            {
                ("EastKicker", P(3.0f, ApproachEndZ), P(Terrace1EastX, ApproachEndZ + tw)),                       // turns an eastbound putt north into the climb
                ("CornerKicker", P(ClimbEastX, MergeEndZ - cw), P(ClimbWestX, MergeEndZ)),                        // turns the climb's ball west along the Merge Terrace
                ("ShrineStubWest", P(ShrineWestX, zg), P(gateX - half, zg)),
                ("ShrineStubEast", P(ShrineEastX, zg), P(gateX + half, zg)),
                ("ShrineKicker", P(ShrineWestX, ShrineTopZ - kl), P(ShrineEastX, ShrineTopZ)),                    // turns the ball east onto the Landing
            };
        }

        /// <summary>The unplayable drops (out-of-bounds boxes): the waterfall chasm, the pit under the jump's gap, and the abyss under the Sky Bridge.</summary>
        public List<(string name, float x0, float x1, float z0, float z1, float top)> HazardPlan() => new List<(string, float, float, float, float, float)>
        {
            ("WaterfallChasm", OverlookEastX, ClimbWestX, Terrace1EndZ, ClimbEndZ, -0.05f),
            ("GapPit", Left - 0.2f, OverlookEastX + 0.2f, LipZ - 0.1f, OverlookZ + 0.8f, LipHeight - 0.30f),
            ("Abyss", LandingEndX, BridgeEndX, FinalZ0 - 0.6f, FinalZ1 + 0.6f, -0.30f),
        };

        /// <summary>The ground the hole occupies beyond its green rectangles: the pit, the chasm and the abyss (terrain plateau and foliage keep-out).</summary>
        public List<Rect> ExtraAreas() => HazardPlan().Select(h => new Rect(h.x0, h.z0, h.x1 - h.x0, h.z1 - h.z0)).ToList();

        public SummitSanctuarySpec Clone() => (SummitSanctuarySpec)MemberwiseClone();

        /// <summary>Human-readable problems with this combination (empty when it will work).</summary>
        public string Validate()
        {
            if (ClimbSlope > 0.0785f) return "the Waterfall Climb is steeper than the rest-hold slope";
            if (OverlookSlope > 0.0785f || OverlookSlope < 0f) return "the Overlook's rise is not gentle";
            if (T1 / (ApproachEndZ - riseStartZ) > 0.0785f) return "the approach is steeper than the rest-hold slope";
            if ((SummitLevel - MergeHeight) / (ShrineRiseEndZ - MergeEndZ) > 0.0785f) return "the Shrine Lane is steeper than the rest-hold slope";
            if (BridgeSlope > 0.0785f || BridgeSlope < 0f) return "the Sky Bridge is not a gentle rise";
            if (JumpAngleDegrees < 10f || JumpAngleDegrees > 18f) return "the jump angle is outside the proven range";
            if (Gap > 0.8f) return "the gap is too wide to jump";
            if (OverlookEastX - Left < 2.5f) return "the Overlook is too narrow to land on";
            if (ClimbWestX - OverlookEastX < 0.6f) return "no chasm between the Overlook and the climb";
            if (MergeEndZ + 1.0f > ShrineRiseEndZ) return "the Shrine Lane has no room to rise";
            if (Snap(shrineGateZ) < MergeEndZ + 0.8f || Snap(shrineGateZ) > ShrineRiseEndZ) return "the shrine gate is not on the lane";
            if (BridgeEndX - LandingEndX < 3f) return "the Sky Bridge is too short to be the finale";
            if (Cup.x - AltarWestX < 0.8f || AltarEastX - Cup.x < 0.6f) return "the cup needs flat Altar all round it";
            return "";
        }

        /// <summary>Builds everything that is not the green itself under <paramref name="root"/> (hole space). Returns the cup (found in the green).</summary>
        public Cup BuildPieces(Transform root, GolfTuning tuning, ProvingMaterials mats)
        {
            var walls = new GameObject("SummitWalls").transform;
            walls.SetParent(root, false);
            foreach (var (name, a, b) in WallPlan())
                BankWall.Segment(walls, a, b, mats.wall, wallHeight, BankWall.DefaultThickness, BankWall.DefaultSkirt, name);
            // Low lip along the Overlook's open near edge: the jump flies over it, a ball rolling back does not (as on the launch pad).
            float lipLen = OverlookEastX - Left;
            ProvingKit.Box("OverlookLip", walls, new Vector3(Left + lipLen * 0.5f, OverlookHeight + 0.01f, OverlookZ + 0.03f), Quaternion.identity,
                new Vector3(lipLen, 0.02f, 0.04f), mats.wall, true);

            var hazards = new GameObject("Hazards").transform;
            hazards.SetParent(root, false);
            foreach (var (name, x0, x1, z0, z1, top) in HazardPlan())
                ProvingKit.Box(name, hazards, new Vector3((x0 + x1) * 0.5f, top - 0.25f, (z0 + z1) * 0.5f), Quaternion.identity,
                    new Vector3(x1 - x0, 0.5f, z1 - z0), mats.wood, true, false, true);
            return root.GetComponentInChildren<Cup>();
        }
    }
}
