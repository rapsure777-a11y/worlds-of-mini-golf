using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// Hole 6, "The Waterwheel Mill": the proven waterwheel as one feature of a real hole. Plan (hole space, x across, z along):
    ///   tee pad -> intake lane (0.7 m) -> dock under the wheel -> the wheel lifts the ball -> elevated aqueduct (the channel) -> final green with the cup.
    /// Beside the lane runs the <b>mill road</b>, a long gentle ramp (under the rest-hold slope, so a ball can stop on it and be struck again) that
    /// climbs to the same green: the conventional route for a player who would rather not use the wheel, and a way round if it ever misbehaves. It costs
    /// a few extra strokes. Transport by the wheel costs none (see <see cref="WaterwheelCarrier"/>).
    /// The wheel itself is the unchanged proven mechanism: this spec only chooses its position, bucket count and speed, and how it is surrounded.
    /// </summary>
    [System.Serializable]
    public class MillHoleSpec
    {
        public WaterwheelHoleSpec wheel = NewWheel();
        [Header("Tee pad and the mill road")]
        [Tooltip("Length of the flat tee pad. The road starts rising where the pad ends (or later, if its slope needs it).")]
        public float padLength = 2.0f;
        [Tooltip("How far the pad extends to the right of the lane's centre line.")]
        public float padRight = 0.8f;
        public float roadWidth = 1.0f;
        [Tooltip("Slope of the mill road. Under about 0.0785 a ball can rest on it and be struck again.")]
        [Range(0.04f, 0.0785f)] public float roadSlope = 0.075f;
        public float teeZ = 0.6f;

        static float Snap(float v) => Mathf.Round(v * 10f) / 10f;

        /// <summary>The proven wheel with the production choices: a bucket every 2 s (little waiting), a 3 m aqueduct, a 2 m x 3 m final green.</summary>
        public static WaterwheelHoleSpec NewWheel()
        {
            var w = new WaterwheelHoleSpec { wheelZ = 4.0f, exitLength = 3.0f, greenWidth = 2.0f, greenLength = 3.0f };
            w.wheel.bucketCount = 6;
            w.wheel.periodSeconds = 12f;
            return w;
        }

        public float PadLength => Snap(padLength);
        public float RoadWidth => Mathf.Max(0.8f, Snap(roadWidth));
        /// <summary>The road's inner edge touches the final green's left edge.</summary>
        public float RoadInnerX => -wheel.GreenWidth * 0.5f;
        public float RoadOuterX => RoadInnerX - RoadWidth;
        public Vector2 Tee => new Vector2(0f, teeZ);
        public float GreenStartZ(float r) => wheel.ChannelEndZ(r);
        public float GreenEndZ(float r) => wheel.ChannelEndZ(r) + wheel.GreenLength;
        /// <summary>Height of the final green (the end of the aqueduct).</summary>
        public float GreenHeight(float r) => wheel.ChannelHeight(wheel.ChannelEndZ(r), r);
        /// <summary>The road runs to the back of the green.</summary>
        public float RoadEndZ(float r) => GreenEndZ(r);
        /// <summary>Where the road starts rising so that, at <see cref="roadSlope"/>, it reaches the green's height at <see cref="RoadEndZ"/> (never before the pad ends).</summary>
        public float RoadRiseStartZ(float r) => Mathf.Max(PadLength, Snap(RoadEndZ(r) - GreenHeight(r) / roadSlope));
        public float RoadHeight(float z, float r) => Mathf.Min(GreenHeight(r), Mathf.Max(0f, (z - RoadRiseStartZ(r)) * roadSlope));
        public Vector2 Cup(float r) => new Vector2(0f, wheel.CupZ(r));

        public GreenLayout BuildLayout(float ballRadius)
        {
            var l = new GreenLayout { wallHeight = wheel.railHeight };
            l.Area(RoadOuterX, 0f, padRight - RoadOuterX, PadLength);                   // tee pad: the road's foot, the lane's start
            var wheelHeight = wheel.AddToLayout(l, ballRadius);
            float rs = PadLength, re = RoadEndZ(ballRadius);
            l.Area(RoadOuterX, rs, RoadWidth, re - rs);                                 // the mill road, ending level with the green's back
            l.cup = Cup(ballRadius);
            float roadInner = RoadInnerX;
            float greenStart = GreenStartZ(ballRadius);
            l.height = (x, z) =>
            {
                if (x < roadInner - 0.05f) return RoadHeight(z, ballRadius);                               // the road
                if (x < roadInner + 0.05f && z < greenStart - 0.05f) return RoadHeight(z, ballRadius);    // the road's own right-edge vertices
                return wheelHeight(x, z);
            };
            return l;
        }

        public MillHoleSpec Clone() { var c = (MillHoleSpec)MemberwiseClone(); c.wheel = wheel.Clone(); return c; }

        /// <summary>The hole's footprint, for the terrain plateau and the foliage keep-out.</summary>
        public Rect Footprint(float ballRadius)
        {
            float x0 = RoadOuterX, x1 = Mathf.Max(padRight, Mathf.Max(wheel.DockWidth, wheel.GreenWidth) * 0.5f);
            return new Rect(x0, 0f, x1 - x0, GreenEndZ(ballRadius));
        }

        /// <summary>Human-readable problems with this combination (empty when it will work).</summary>
        public string Validate(float ballRadius)
        {
            string w = wheel.Validate(ballRadius);
            if (w.Length > 0) return w;
            if (!wheel.HasGreen) return "the mill needs a final green";
            if (roadSlope > 0.0785f) return "the road is steeper than the rest-hold slope";
            if (RoadHeight(RoadEndZ(ballRadius), ballRadius) < GreenHeight(ballRadius) - 0.03f) return "the road does not reach the green's height";
            return "";
        }

        /// <summary>
        /// The corner kicker at the top of the mill road: a ball struck up the road and carrying pace to the back rail is turned right onto the flat green
        /// instead of rebounding down the whole road (a 7% slope lets a rolling ball run a long way downhill). From (x, z) to (x, z), hole space.
        /// </summary>
        public (Vector3 a, Vector3 b) RoadTopKicker(float ballRadius)
        {
            float zEnd = RoadEndZ(ballRadius), y = GreenHeight(ballRadius);
            return (new Vector3(RoadOuterX, y, zEnd - 0.9f), new Vector3(RoadInnerX + 0.1f, y, zEnd));
        }

        /// <summary>Builds the wheel, curb and supports (the proven pieces) and the road's top kicker under <paramref name="root"/>; returns the cup found in the green.</summary>
        public Cup BuildPieces(Transform root, GolfTuning tuning, ProvingMaterials mats)
        {
            ProvingGround.BuildWheelPieces(root, tuning, null, mats, wheel);
            var k = RoadTopKicker(tuning.ballRadius);
            BankWall.Segment(root, k.a, k.b, mats.wall, BankWall.DefaultHeight, BankWall.DefaultThickness, BankWall.DefaultSkirt, "RoadTopKicker");
            return root.GetComponentInChildren<Cup>();
        }
    }
}
