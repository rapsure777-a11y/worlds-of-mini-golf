using System;
using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// Tracked putter. It follows a hand transform exactly (no physics lag, no collisions with the
    /// course) and strikes the ball through a swept test of the head against the ball each frame.
    ///
    /// Frame layout (local to the hand after the angle offset):
    ///   +Z  runs down the shaft from the hand to the head
    ///   +X  is the face normal (the head is two-faced, so -X also strikes)
    ///   +Y  runs heel to toe
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class Putter : MonoBehaviour
    {
        const string PrefLength = "putter.length";
        const string PrefAngle = "putter.angle";
        const string PrefTwist = "putter.twist";

        [SerializeField] GolfTuning tuning;
        [Tooltip("Tracked controller (grip pose) the putter follows.")]
        [SerializeField] Transform hand;
        [SerializeField] GolfBall ball;
        [SerializeField] Material shaftMaterial;
        [SerializeField] Material headMaterial;
        [SerializeField] Material gripMaterial;

        [Tooltip("Hand to head-centre distance, metres.")]
        [SerializeField] float length = -1f;
        [Tooltip("Pitch of the shaft relative to the controller, degrees.")]
        [SerializeField] float angleOffset = 0f;
        [Tooltip("Rotation of the head about the shaft, degrees.")]
        [SerializeField] float headTwist = 0f;
        [SerializeField] bool loadSavedAdjustments = true;

        public GolfTuning Tuning => tuning ? tuning : GolfTuning.Default;
        public Transform Hand { get => hand; set => hand = value; }
        public GolfBall Ball { get => ball; set => ball = value; }
        public float Length => length;
        public float AngleOffset => angleOffset;
        public float HeadTwist => headTwist;
        public Vector3 HeadPosition { get; private set; }
        public Quaternion HeadRotation { get; private set; }
        public Vector3 HeadVelocity { get; private set; }

        /// <summary>Raised after the putter launches the ball. Argument is the ball speed.</summary>
        public event Action<Putter, float> StruckBall;

        Transform m_Pivot, m_Shaft, m_Head, m_Grip;

        // Head history for velocity smoothing.
        const int k_History = 9;
        readonly Vector3[] m_PosHistory = new Vector3[k_History];
        readonly float[] m_TimeHistory = new float[k_History];
        int m_HistoryCount, m_HistoryIndex;

        Vector3 m_PrevHeadPos;
        Quaternion m_PrevHeadRot = Quaternion.identity;
        Vector3 m_PrevBallPos;
        bool m_HasPrev;
        bool m_Overlapping;
        float m_LastHitTime = -10f;

        public void Configure(GolfTuning t, Transform handTransform, GolfBall golfBall, Material shaft, Material head, Material grip)
        {
            tuning = t; hand = handTransform; ball = golfBall;
            shaftMaterial = shaft; headMaterial = head; gripMaterial = grip;
        }

        void Awake()
        {
            var t = Tuning;
            if (length <= 0f) length = t.defaultPutterLength;
            if (loadSavedAdjustments)
            {
                length = PlayerPrefs.GetFloat(PrefLength, length);
                angleOffset = PlayerPrefs.GetFloat(PrefAngle, angleOffset);
                headTwist = PlayerPrefs.GetFloat(PrefTwist, headTwist);
            }
            BuildVisual();
        }

        void BuildVisual()
        {
            m_Pivot = new GameObject("Pivot").transform;
            m_Pivot.SetParent(transform, false);
            m_Shaft = MakePart(PrimitiveType.Cylinder, "Shaft", shaftMaterial);
            m_Grip = MakePart(PrimitiveType.Cylinder, "Grip", gripMaterial);
            m_Head = MakePart(PrimitiveType.Cube, "Head", headMaterial);
            LayoutVisual();
        }

        Transform MakePart(PrimitiveType type, string name, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
            var r = go.GetComponent<MeshRenderer>();
            if (mat) r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            go.transform.SetParent(m_Pivot, false);
            return go.transform;
        }

        void LayoutVisual()
        {
            var t = Tuning;
            m_Pivot.localRotation = Quaternion.Euler(angleOffset, 0f, 0f);
            float shaftLen = Mathf.Max(0.05f, length - t.headSize.z * 0.5f);
            m_Shaft.localPosition = new Vector3(0f, 0f, shaftLen * 0.5f);
            m_Shaft.localRotation = Quaternion.Euler(90f, 0f, 0f);
            m_Shaft.localScale = new Vector3(0.012f, shaftLen * 0.5f, 0.012f);
            m_Grip.localPosition = new Vector3(0f, 0f, 0.02f);
            m_Grip.localRotation = Quaternion.Euler(90f, 0f, 0f);
            m_Grip.localScale = new Vector3(0.024f, 0.08f, 0.024f);
            m_Head.localPosition = new Vector3(0f, 0f, length);
            m_Head.localRotation = Quaternion.AngleAxis(headTwist, Vector3.forward);
            m_Head.localScale = t.headSize;
        }

        public void SetAdjustments(float newLength, float newAngle, float newTwist)
        {
            length = newLength; angleOffset = newAngle; headTwist = newTwist;
            LayoutVisual();
        }

        public void AdjustLength(float delta)
        {
            var t = Tuning;
            length = Mathf.Clamp(length + delta, t.minPutterLength, t.maxPutterLength);
            LayoutVisual();
        }

        public void AdjustAngle(float deltaDegrees)
        {
            angleOffset = Mathf.Clamp(angleOffset + deltaDegrees, -80f, 80f);
            LayoutVisual();
        }

        public void AdjustTwist(float deltaDegrees)
        {
            headTwist = Mathf.Repeat(headTwist + deltaDegrees + 180f, 360f) - 180f;
            LayoutVisual();
        }

        public void SaveAdjustments()
        {
            PlayerPrefs.SetFloat(PrefLength, length);
            PlayerPrefs.SetFloat(PrefAngle, angleOffset);
            PlayerPrefs.SetFloat(PrefTwist, headTwist);
            PlayerPrefs.Save();
        }

        /// <summary>Forget motion history, e.g. after the player teleports or swaps hands.</summary>
        public void ResetTracking()
        {
            m_HasPrev = false;
            m_HistoryCount = 0;
            m_Overlapping = true; // require the head to clear the ball before the next strike
        }

        public void SetVisible(bool visible)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
        }

        /// <summary>Head movement in one frame above this is treated as a tracking jump, not a swing (metres).</summary>
        const float MaxHeadJump = 0.3f;
        /// <summary>Head speeds above this are tracking glitches; no human putting stroke gets close (m/s).</summary>
        const float MaxHumanHeadSpeed = 15f;

        void Update() => Step(Time.time);

        /// <summary>Follow the hand and run strike detection. Public so tests can drive it.</summary>
        public void Step(float time)
        {
            if (hand)
            {
                transform.SetPositionAndRotation(hand.position, hand.rotation);
            }
            LayoutVisual();

            Quaternion headRot = m_Head.rotation;
            Vector3 headPos = m_Head.position;
            HeadPosition = headPos;
            HeadRotation = headRot;

            // A jump no swing can produce (tracking just acquired, controller woke up, rig teleport)
            // must not sweep through the ball: restart tracking from the new pose instead.
            if (m_HasPrev && (headPos - m_PrevHeadPos).sqrMagnitude > MaxHeadJump * MaxHeadJump)
            {
                ResetTracking();
                PushHistory(headPos, time);
                HeadVelocity = Vector3.zero;
                m_PrevHeadPos = headPos;
                m_PrevHeadRot = headRot;
                m_PrevBallPos = ball ? ball.Position : Vector3.zero;
                m_HasPrev = true;
                return;
            }

            PushHistory(headPos, time);
            HeadVelocity = EstimateVelocity();

            if (ball && ball.InPlay && m_HasPrev)
                DetectStrike(headPos, headRot, time);

            m_PrevHeadPos = headPos;
            m_PrevHeadRot = headRot;
            m_PrevBallPos = ball ? ball.Position : Vector3.zero;
            m_HasPrev = true;
        }

        void PushHistory(Vector3 p, float time)
        {
            m_PosHistory[m_HistoryIndex] = p;
            m_TimeHistory[m_HistoryIndex] = time;
            m_HistoryIndex = (m_HistoryIndex + 1) % k_History;
            m_HistoryCount = Mathf.Min(m_HistoryCount + 1, k_History);
        }

        Vector3 EstimateVelocity()
        {
            int frames = Mathf.Clamp(Tuning.velocitySmoothingFrames, 1, k_History - 1);
            if (m_HistoryCount < 2) return Vector3.zero;
            frames = Mathf.Min(frames, m_HistoryCount - 1);
            int newest = (m_HistoryIndex - 1 + k_History) % k_History;
            int oldest = (newest - frames + k_History) % k_History;
            float dt = m_TimeHistory[newest] - m_TimeHistory[oldest];
            if (dt <= 1e-5f) return HeadVelocity;
            return (m_PosHistory[newest] - m_PosHistory[oldest]) / dt;
        }

        void DetectStrike(Vector3 headPos, Quaternion headRot, float time)
        {
            var t = Tuning;
            float r = ball.Radius;
            Vector3 half = t.headSize * 0.5f + new Vector3(r, r, r);

            Vector3 p0 = Quaternion.Inverse(m_PrevHeadRot) * (m_PrevBallPos - m_PrevHeadPos);
            Vector3 p1 = Quaternion.Inverse(headRot) * (ball.Position - headPos);

            bool insideNow = Inside(p1, half);
            bool insideBefore = Inside(p0, half);
            if (m_Overlapping)
            {
                // Head is still passing through the ball from a previous strike or a reset.
                if (!insideNow && !insideBefore) m_Overlapping = false;
                return;
            }
            if (insideBefore) { m_Overlapping = true; return; }
            if (time - m_LastHitTime < t.hitCooldown) return;

            if (!SegmentEntersBox(p0, p1, half, out int axis, out float sign)) return;

            Vector3 localNormal = Vector3.zero;
            localNormal[axis] = sign;
            Vector3 n = headRot * localNormal; // points from head toward ball

            Vector3 ground = ball.IsGrounded ? ball.GroundNormal : Vector3.up;
            Vector3 nFlat = n - Vector3.Dot(n, ground) * ground;
            if (nFlat.magnitude < 0.3f) return; // touched the top or bottom of the head
            nFlat.Normalize();

            if (HeadVelocity.sqrMagnitude > MaxHumanHeadSpeed * MaxHumanHeadSpeed) { m_Overlapping = true; return; }

            Vector3 vRel = HeadVelocity - ball.Velocity;
            float vn = Vector3.Dot(vRel, nFlat);
            if (vn < t.minHitSpeed) { m_Overlapping = true; return; }

            Vector3 path = HeadVelocity - Vector3.Dot(HeadVelocity, ground) * ground;
            Vector3 pathDir = (path.sqrMagnitude > 1e-6f && Vector3.Dot(path, nFlat) > 0f) ? path.normalized : nFlat;
            Vector3 dir = Vector3.Slerp(pathDir, nFlat, t.faceAngleWeight).normalized;

            float speed = vn * t.energyTransfer;
            Vector3 v = dir * speed;
            v -= Vector3.Dot(v, ground) * ground;

            m_LastHitTime = time;
            m_Overlapping = true;
            ball.Strike(v);
            StruckBall?.Invoke(this, v.magnitude);
        }

        static bool Inside(Vector3 p, Vector3 half) =>
            Mathf.Abs(p.x) < half.x && Mathf.Abs(p.y) < half.y && Mathf.Abs(p.z) < half.z;

        /// <summary>Slab test. Returns the axis and side of the face the segment enters through.</summary>
        static bool SegmentEntersBox(Vector3 p0, Vector3 p1, Vector3 half, out int axis, out float sign)
        {
            axis = -1; sign = 0f;
            float tEnter = 0f, tExit = 1f;
            Vector3 d = p1 - p0;
            for (int i = 0; i < 3; i++)
            {
                if (Mathf.Abs(d[i]) < 1e-9f)
                {
                    if (p0[i] < -half[i] || p0[i] > half[i]) return false;
                    continue;
                }
                float inv = 1f / d[i];
                float tA = (-half[i] - p0[i]) * inv;
                float tB = (half[i] - p0[i]) * inv;
                float s = -Mathf.Sign(d[i]); // entering through the face opposite the motion
                if (tA > tB) { (tA, tB) = (tB, tA); }
                if (tA > tEnter) { tEnter = tA; axis = i; sign = s; }
                if (tB < tExit) tExit = tB;
                if (tEnter > tExit) return false;
            }
            return axis >= 0 && tEnter <= 1f;
        }
    }
}
