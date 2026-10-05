using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// Every number that shapes the launch ramp hole. Distances are metres in hole space (x across the lane, z along it) and are snapped
    /// to the 0.1 m green grid. Defaults come from the design sprint's flight maths for the real ball model: a firm putt of about
    /// 3.0 to 3.6 m/s clears a 0.3 m gap, a softer one falls short and returns with the out-of-bounds penalty, a harder one still lands on the pad.
    /// </summary>
    [System.Serializable]
    public class LaunchRampSpec
    {
        [Header("Approach and ramp")]
        [Tooltip("Flat run-up before the ramp starts (the tee sits 1 m before the ramp).")]
        public float approachLength = 1.4f;
        [Tooltip("Width of the lane and ramp. The open lip is this wide.")]
        public float width = 0.8f;
        [Tooltip("Horizontal length of the ramp.")]
        public float rampRun = 0.6f;
        [Tooltip("Launch angle. Steeper hops higher but costs more speed; flatter flies flatter and farther.")]
        [Range(6f, 30f)] public float rampAngleDegrees = 14f;

        [Header("Gap and landing platform")]
        [Tooltip("Gap between the ramp lip and the landing platform's near edge.")]
        public float gap = 0.3f;
        [Tooltip("How far the platform's near edge sits below the lip.")]
        public float padDrop = 0.06f;
        public float padWidth = 1.6f;
        public float padLength = 2.0f;
        [Tooltip("Cup distance from the platform's near edge.")]
        public float cupFromPadFront = 1.1f;
        [Tooltip("Platform is a shallow dish around the cup: slope toward the cup (keep above 0.08 so a ball cannot rest on it).")]
        [Range(0.05f, 0.2f)] public float dishSlope = 0.09f;
        [Tooltip("Flat apron radius around the cup.")]
        public float cupApron = 0.30f;

        [Header("Rails and hazard")]
        public float railHeight = 0.12f;
        [Tooltip("Low lip along the platform's near edge. Lower than the arc a landing ball flies, so landings clear it, but it stops a ball that rolls back toward the gap.")]
        public float padLipHeight = 0.02f;
        [Tooltip("Depth of the gap floor below the approach level. A ball that lands in the gap is out of bounds (+1 stroke, back to its last rest spot).")]
        public float gapDepth = 0.30f;
        [Tooltip("Tee distance from the lane's back rail.")]
        public float teeZ = 0.4f;

        static float Snap(float v) => Mathf.Round(v * 10f) / 10f;

        public float Width => Mathf.Max(0.4f, Snap(width));
        public float RampStartZ => Snap(approachLength);
        public float RampRun => Mathf.Max(0.2f, Snap(rampRun));
        public float LipZ => RampStartZ + RampRun;
        public float Gap => Mathf.Max(0.1f, Snap(gap));
        public float PadStartZ => LipZ + Gap;
        public float PadLength => Mathf.Max(1f, Snap(padLength));
        public float PadWidth => Mathf.Max(Width, Snap(padWidth));
        public float PadEndZ => PadStartZ + PadLength;
        public float CupZ => PadStartZ + Mathf.Clamp(Snap(cupFromPadFront), 0.6f, PadLength - 0.6f);
        public float Rise => RampRun * Mathf.Tan(rampAngleDegrees * Mathf.Deg2Rad);
        /// <summary>Surface height of the lip edge (hole space, approach level = 0).</summary>
        public float LipHeight => Rise;
        public Vector2 Tee => new Vector2(0f, teeZ);
        public Vector2 CupXZ => new Vector2(0f, CupZ);

        float Dish(float x, float z)
        {
            float d = Mathf.Sqrt(x * x + (z - CupZ) * (z - CupZ));
            return dishSlope * Mathf.Max(0f, d - cupApron);
        }

        /// <summary>Surface height at hole-space (x, z) for either piece (the ramp strip or the landing dish).</summary>
        public float Height(float x, float z)
        {
            if (z < PadStartZ - 0.05f)
            {
                float t = Mathf.Clamp(z - RampStartZ, 0f, RampRun);
                return t * Mathf.Tan(rampAngleDegrees * Mathf.Deg2Rad);
            }
            float baseH = LipHeight - padDrop - Dish(0f, PadStartZ);
            return baseH + Dish(x, z);
        }

        /// <summary>The two disjoint greens, their open edges and the cup, ready for <see cref="CourseGeometry.CreateGreen"/>.</summary>
        public GreenLayout BuildLayout()
        {
            var l = new GreenLayout { wallHeight = railHeight };
            l.Area(-Width * 0.5f, 0f, Width, LipZ);
            l.Area(-PadWidth * 0.5f, PadStartZ, PadWidth, PadLength);
            l.openEdges.Add(new Rect(-PadWidth * 0.5f - 0.1f, LipZ - 0.005f, PadWidth + 0.2f, 0.01f));
            l.openEdges.Add(new Rect(-PadWidth * 0.5f - 0.1f, PadStartZ - 0.005f, PadWidth + 0.2f, 0.01f));
            l.cup = CupXZ;
            l.height = Height;
            return l;
        }

        public LaunchRampSpec Clone() => (LaunchRampSpec)MemberwiseClone();
    }

    /// <summary>
    /// Adjustable launch ramp: a lane that rises to an open lip (no rail), a short gap, and a forgiving landing platform shaped like a
    /// shallow dish around the cup. The jump is the real <see cref="GolfBall"/> physics: the ball leaves the lip as a rigid body and
    /// follows a ballistic arc; nothing is scripted. Change <see cref="Spec"/> and call <see cref="Rebuild"/> (or use the context menu).
    /// </summary>
    public class LaunchRamp : MonoBehaviour
    {
        [SerializeField] LaunchRampSpec spec = new LaunchRampSpec();
        [SerializeField] GolfTuning tuning;
        [SerializeField] ProvingMaterials materials = new ProvingMaterials();

        const string GeometryName = "LaunchRampGeometry";

        public LaunchRampSpec Spec => spec;
        public Cup Cup { get; private set; }
        public GolfTuning Tuning => tuning ? tuning : GolfTuning.Default;
        public Transform Geometry { get; private set; }

        public void Configure(LaunchRampSpec s, GolfTuning t, ProvingMaterials m)
        {
            spec = s ?? new LaunchRampSpec(); tuning = t; materials = m ?? new ProvingMaterials();
        }

        [ContextMenu("Rebuild")]
        public void Rebuild()
        {
            var old = transform.Find(GeometryName);
            if (old) ProvingKit.Discard(old.gameObject);
            var layout = spec.BuildLayout();
            var root = CourseGeometry.CreateGreen(GeometryName, layout, Tuning, transform, materials.green, materials.cup, materials.wall, materials.flag,
                null, out Cup cup);
            Geometry = root.transform;
            Cup = cup;

            // Low lip on the landing platform's open near edge: keeps a rebounding ball from rolling back into the gap.
            float lipY = spec.Height(0f, spec.PadStartZ + 0.03f) + spec.padLipHeight * 0.5f;
            ProvingKit.Box("PadLip", root.transform, new Vector3(0f, lipY, spec.PadStartZ + 0.03f), Quaternion.identity,
                new Vector3(spec.PadWidth, spec.padLipHeight, 0.04f), materials.wall, true);

            // The pit under the gap: touching it is out of bounds. A short ball meets the skirts first, then lands here.
            float floorTop = -spec.gapDepth;
            ProvingKit.Box("GapFloor", root.transform, new Vector3(0f, floorTop - 0.05f, (spec.LipZ + spec.PadStartZ) * 0.5f), Quaternion.identity,
                new Vector3(spec.PadWidth + 0.4f, 0.1f, spec.Gap + 0.6f), materials.wood, true, false, true);

            // Rebuilt after the hole was assembled (tuning in the editor): point the hole at the new cup and tee.
            var hole = GetComponent<HoleController>();
            if (hole)
            {
                hole.Configure(hole.Tuning, hole.HoleNumber, hole.Par, hole.Tee, hole.PlayerStart, Cup, hole.Ball, transform.position.y - 3f);
                hole.Tee.localPosition = new Vector3(spec.Tee.x, spec.Height(spec.Tee.x, spec.Tee.y), spec.Tee.y);
                if (Application.isPlaying && HoleController.Active == hole) hole.BeginHole(hole.Ball); // re-hook the new cup
            }
        }
    }
}
