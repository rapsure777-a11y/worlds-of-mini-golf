using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// Hole 5, "The Sun Stair": a ziggurat of three terraces climbed to a cup on the upper one. Everything is a single rectangle-union green with a
    /// height function (steps are steep cells, which act as walls: a ball that rolls off a terrace edge simply lands on the terrace below, so a miss
    /// is recoverable), plus <see cref="BankWall"/> pieces for rails along ramps and angled kickers, and one launch jump built from the proven
    /// ramp, open lip and landing logic.
    ///
    /// Plan (x across, z up the stair, units metres, defaults):
    ///   lower terrace (z 0..2.8, height 0)    left lane = ramp 1 (z 2.4..4.8) up to the middle terrace (height 0.18),
    ///   middle terrace (z 2.8..7.2)           a short landing, then the launch ramp (z 5.6..6.6, lip 0.44) over a 0.5 m gap,
    ///   upper terrace (z 7.1..10.3, 0.36)     right lane = ramp 2 (z 4.7..7.1) up to the upper terrace; the cup sits in a shallow sun dish.
    /// Safe route: tee, ramp 1 to the landing, across the middle terrace (a corner kicker turns the ball up ramp 2), up ramp 2, putt in.
    /// Aggressive route: from the landing, a firm putt up the launch ramp flies the gap onto the upper terrace, skipping ramp 2 (about 3.6 m/s);
    /// a very hard tee putt carries through ramp 1, the landing and the jump in one stroke. A short jump drops onto the middle terrace.
    /// </summary>
    [System.Serializable]
    public class SunStairSpec
    {
        [Header("Layout")]
        public float width = 5.0f;
        [Tooltip("Width of each ramp lane. Ramp 1 hugs the left wall, ramp 2 the right.")]
        public float laneWidth = 1.0f;
        [Tooltip("Rise of each terrace. Keep the ramps under about 7.8% so a ball can rest on them and be struck again.")]
        public float step = 0.18f;
        public float rampRun = 2.4f;
        [Tooltip("Ramp 1 starts this far up the lower terrace; the tee is 1.2 m before it.")]
        public float rampFootZ = 2.4f;
        public float landingLength = 0.8f;
        [Header("Jump")]
        public float jumpRun = 1.0f;
        public float gap = 0.5f;
        [Tooltip("How far the upper terrace's edge sits below the jump's lip. More = longer, more forgiving flight.")]
        public float padDrop = 0.08f;
        [Header("Upper terrace")]
        public float upperLength = 3.2f;
        [Tooltip("Radius of the shallow dish round the cup (the sun disc). 0 = a flat terrace.")]
        public float dishRadius = 1.2f;
        public float dishSlope = 0.09f;
        [Tooltip("Flat apron round the cup. 0.5 keeps the whole cup tile and a ball put 0.4 m before the cup on level ground.")]
        public float cupApron = 0.5f;
        [Header("Rails and kickers")]
        public float railHeight = 0.12f;
        public float wallHeight = 0.16f;
        public float kickerLeg = 0.9f;

        static float Snap(float v) => Mathf.Round(v * 10f) / 10f;

        public float Width => Snap(width);
        public float Lane => Snap(laneWidth);
        public float Left => -Width * 0.5f;
        public float Right => Width * 0.5f;
        /// <summary>x where the left lane ends and the middle terrace begins (and the mirror on the right).</summary>
        public float LeftLaneEdge => Left + Lane;
        public float RightLaneEdge => Right - Lane;
        public float RampRun => Snap(rampRun);
        public float RampFootZ => Snap(rampFootZ);
        public float DeckFrontZ => RampFootZ + 0.4f;
        public float RampTopZ => RampFootZ + RampRun;
        public float LandingEndZ => RampTopZ + Snap(landingLength);
        public float JumpRun => Snap(jumpRun);
        public float LipZ => LandingEndZ + JumpRun;
        public float Gap => Mathf.Max(0.2f, Snap(gap));
        public float UpperFrontZ => LipZ + Gap;
        public float UpperEndZ => UpperFrontZ + Snap(upperLength);
        public float Ramp2FootZ => UpperFrontZ - RampRun;
        public float MiddleHeight => step;
        public float UpperHeight => 2f * step;
        /// <summary>Height rise of the launch ramp: the middle terrace up to the lip, so the lip is <see cref="padDrop"/> above the upper terrace.</summary>
        public float JumpRise => step + padDrop;
        public float LipHeight => MiddleHeight + JumpRise;
        public float JumpAngleDegrees => Mathf.Atan(JumpRise / JumpRun) * Mathf.Rad2Deg;
        public float RampSlope => step / RampRun;
        public Vector2 Tee => new Vector2((Left + LeftLaneEdge) * 0.5f, 1.2f);
        public Vector2 Cup => new Vector2(0.4f, Snap(UpperFrontZ + (UpperEndZ - UpperFrontZ) * 0.6f));
        /// <summary>Total height of the climb, for dressing and tests.</summary>
        public float TopHeight => UpperHeight;

        const float Tol = 0.05f;   // vertices on a boundary belong to the higher-numbered side, so lanes keep their full width

        /// <summary>Surface height at hole-space (x, z).</summary>
        public float Height(float x, float z)
        {
            float t2 = MiddleHeight, t3 = UpperHeight;
            if (z >= UpperFrontZ - Tol) return UpperTerrace(x, z);
            bool left = x < LeftLaneEdge + Tol, right = x > RightLaneEdge - Tol;
            if (left)
            {
                if (z < RampFootZ - Tol) return 0f;
                if (z < RampTopZ) return t2 * Mathf.Clamp01((z - RampFootZ) / RampRun);
                if (z < LandingEndZ) return t2;
                if (z <= LipZ + Tol) return t2 + JumpRise * Mathf.Clamp01((z - LandingEndZ) / JumpRun);
                return t2;                                   // the gap floor: a short jump lands here, on the middle terrace
            }
            if (right)
            {
                if (z < DeckFrontZ - Tol) return 0f;
                if (z < Ramp2FootZ) return t2;
                return t2 + step * Mathf.Clamp01((z - Ramp2FootZ) / RampRun);
            }
            return z < DeckFrontZ - Tol ? 0f : t2;           // the middle of the stair: the lower floor, then the middle terrace
        }

        float UpperTerrace(float x, float z)
        {
            float t3 = UpperHeight;
            if (dishRadius <= cupApron) return t3;
            Vector2 c = Cup;
            float d = Mathf.Sqrt((x - c.x) * (x - c.x) + (z - c.y) * (z - c.y));
            if (d >= dishRadius) return t3;
            return t3 - dishSlope * (dishRadius - Mathf.Max(d, cupApron));
        }

        public GreenLayout BuildLayout()
        {
            var l = new GreenLayout { wallHeight = railHeight };
            l.Area(Left, 0f, Width, UpperEndZ);
            l.cup = Cup;
            l.height = Height;
            return l;
        }

        /// <summary>The hole's footprint, for the terrain plateau and the foliage keep-out.</summary>
        public Rect Footprint => new Rect(Left, 0f, Width, UpperEndZ);

        Vector3 P(float x, float z) => new Vector3(x, Height(x, z), z);

        /// <summary>Wall pieces with their end points (hole space), also used by tests. Rails that follow a ramp, then angled kickers.</summary>
        public System.Collections.Generic.List<(string name, Vector3 a, Vector3 b)> WallPlan()
        {
            var w = new System.Collections.Generic.List<(string, Vector3, Vector3)>();
            // Rails on the open (inner) sides of the ramps, where the neighbouring terrace is not higher than the ramp.
            w.Add(("Ramp1Rail", P(LeftLaneEdge, RampFootZ), P(LeftLaneEdge, DeckFrontZ)));
            w.Add(("JumpRail", P(LeftLaneEdge, LandingEndZ), P(LeftLaneEdge, LipZ)));
            w.Add(("Ramp2Rail", P(RightLaneEdge, Ramp2FootZ), P(RightLaneEdge, UpperFrontZ - 0.1f)));
            // Corner kicker at the foot of ramp 2: a ball crossing the middle terrace to the right turns up the lane.
            float kz = DeckFrontZ + 0.4f;
            w.Add(("Ramp2Kicker", P(RightLaneEdge, kz), P(Right, kz + Lane)));
            // Kickers in the back corners of the upper terrace turn a ball that arrives along the edge toward the cup.
            float cz = Cup.y - 0.4f, leg = Snap(kickerLeg);
            w.Add(("UpperKickerLeft", P(Left, cz), P(Left + leg, cz + leg)));
            w.Add(("UpperKickerRight", P(Right, cz), P(Right - leg, cz + leg)));
            return w;
        }

        public SunStairSpec Clone() => (SunStairSpec)MemberwiseClone();

        /// <summary>Human-readable problems with this combination (empty when it will work).</summary>
        public string Validate()
        {
            if (RampSlope > 0.12f) return "ramps are too steep to climb with a normal putt";
            if (RampTopZ + 0.1f > LandingEndZ) return "no landing between ramp 1 and the jump";
            if (Ramp2FootZ < DeckFrontZ + Lane + 0.4f) return "ramp 2 starts too close to the front of the middle terrace (no room for its kicker)";
            if (Gap > 1.2f) return "the gap is too wide to jump";
            if (Cup.y > UpperEndZ - 0.8f) return "cup too close to the back of the upper terrace";
            return "";
        }

        /// <summary>
        /// Builds everything that is not the green itself under <paramref name="root"/> (hole space): the angled walls and rails, and a low lip on the upper
        /// terrace's open front edge so a ball that rolls back off it stays up there. Returns the cup (found in the green already built).
        /// </summary>
        public Cup BuildPieces(Transform root, ProvingMaterials mats)
        {
            var walls = new GameObject("SunStairWalls").transform;
            walls.SetParent(root, false);
            foreach (var (name, a, b) in WallPlan())
                BankWall.Segment(walls, a, b, mats.wall, wallHeight, BankWall.DefaultThickness, BankWall.DefaultSkirt, name);
            // Low lip along the upper terrace's front edge (left of ramp 2): the jump flies over it, a rolling ball does not.
            float lipLen = RightLaneEdge - Left;
            float y = UpperHeight + 0.01f;
            ProvingKit.Box("UpperLip", walls, new Vector3(Left + lipLen * 0.5f, y, UpperFrontZ + 0.03f), Quaternion.identity,
                new Vector3(lipLen, 0.02f, 0.04f), mats.wall, true);
            return root.GetComponentInChildren<Cup>();
        }
    }
}
