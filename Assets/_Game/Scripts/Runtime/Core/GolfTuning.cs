using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// Every number that shapes the putting feel lives here, so tuning against
    /// Walkabout observations is a data change rather than a code change.
    /// </summary>
    [CreateAssetMenu(menuName = "Gamebreak/Golf Tuning", fileName = "GolfTuning")]
    public class GolfTuning : ScriptableObject
    {
        [Header("Simulation")]
        [Tooltip("Physics steps per second. Small fast balls need a high rate.")]
        public int physicsRate = 120;

        [Header("Ball")]
        public float ballRadius = 0.0225f;
        public float ballMass = 0.046f;
        [Tooltip("Constant rolling-resistance deceleration on flat green, m/s^2. Sets how far a putt runs.")]
        public float rollingDeceleration = 0.55f;
        [Tooltip("Extra speed-proportional drag, 1/s. Makes fast putts die a little quicker.")]
        public float speedDrag = 0.08f;
        [Tooltip("Below this speed the ball counts as stopping.")]
        public float restSpeed = 0.03f;
        [Tooltip("Seconds below rest speed before the ball is declared at rest.")]
        public float restTime = 0.25f;
        public float maxBallSpeed = 9f;
        [Tooltip("Coefficient of restitution against walls (0..1).")]
        [Range(0f, 1f)] public float wallBounciness = 0.72f;
        [Tooltip("Fraction of along-wall speed kept on a wall hit (rail friction).")]
        [Range(0f, 1f)] public float wallTangentKeep = 0.94f;
        [Tooltip("Most upward speed (m/s) one contact may add, e.g. a fast ball clipping the cup lip. Limits hops.")]
        public float maxContactLift = 0.5f;

        [Header("Putter strike")]
        [Tooltip("Ball speed = head speed along the face normal x this value.")]
        public float energyTransfer = 1.35f;
        [Tooltip("1 = ball leaves along the face normal; 0 = along the swing path.")]
        [Range(0f, 1f)] public float faceAngleWeight = 0.85f;
        [Tooltip("Head speeds below this do not strike the ball (m/s).")]
        public float minHitSpeed = 0.04f;
        [Tooltip("Seconds after a strike during which the putter cannot strike again.")]
        public float hitCooldown = 0.35f;
        [Tooltip("Frames averaged to estimate head velocity.")]
        [Range(1, 8)] public int velocitySmoothingFrames = 3;

        [Header("Putter shape")]
        public float defaultPutterLength = 0.85f;
        [Tooltip("Auto-sized putter length = standing eye height x this. 0.62 gives ~1.02 m at 1.65 m eye height " +
                 "(first Frame test: 0.85 m was about 20% short).")]
        public float autoLengthRatio = 0.62f;
        public float minPutterLength = 0.4f;
        public float maxPutterLength = 1.4f;
        public Vector3 headSize = new Vector3(0.03f, 0.12f, 0.035f); // face thickness, toe-heel, height

        [Header("Cup")]
        public float cupRadius = 0.054f;
        public float cupDepth = 0.11f;

        [Header("Rules")]
        public int strokeLimit = 10;
        public int outOfBoundsPenalty = 1;

        static GolfTuning s_Default;

        /// <summary>Fallback used when a component has no tuning asset assigned.</summary>
        public static GolfTuning Default
        {
            get
            {
                if (s_Default == null)
                {
                    s_Default = Resources.Load<GolfTuning>("GolfTuning");
                    if (s_Default == null) s_Default = CreateInstance<GolfTuning>();
                }
                return s_Default;
            }
        }
    }
}
