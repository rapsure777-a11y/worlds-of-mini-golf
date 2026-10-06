using System;
using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>Tunable numbers for a vent transport. All positions are in the transport's local space (for a hole, the hole's own space).</summary>
    [Serializable]
    public class VentTransportSpec
    {
        [Header("Intake")]
        [Tooltip("A ball whose centre is within this distance (horizontally) of the intake point can be captured.")]
        public float captureRadius = 0.15f;
        [Tooltip("Ball speed above which the ball is too fast to be captured: it rolls through (and usually rebounds back for a slower second pass).")]
        public float maxCaptureSpeed = 2.4f;
        [Tooltip("Seconds the ball takes to settle into the mouth after capture.")]
        public float captureBlendSeconds = 0.2f;
        [Tooltip("How far below its resting height the ball sinks into the mouth during the settle (the ball visibly drops in).")]
        public float sinkDepth = 0.03f;

        [Header("Transport")]
        [Tooltip("Pause between the ball dropping into the mouth and the ride starting: the vent charges (glow, rumble), so the ride is clearly deliberate.")]
        public float dwellSeconds = 0.6f;
        [Tooltip("Seconds the ride takes from the mouth to the exit (eased at both ends).")]
        public float travelSeconds = 1.6f;

        [Header("Exit")]
        [Tooltip("Speed the ball leaves the exit with. After the exit the ball is ordinary physics.")]
        public float exitSpeed = 2.0f;
        [Tooltip("Direction the ball leaves the exit in (local space, normalised on use; vertical part is ignored).")]
        public Vector3 exitDirection = Vector3.forward;
        [Tooltip("After an exit the intake ignores the ball for this long, so a ball that comes back to the mouth is not instantly re-captured.")]
        public float recaptureLockoutSeconds = 1.5f;
        [Tooltip("Safety net: a ball held this long is released at the exit regardless.")]
        public float maxHoldSeconds = 12f;

        public VentTransportSpec Clone() => (VentTransportSpec)MemberwiseClone();
    }

    /// <summary>
    /// A vent that takes the ball from an intake mouth to an exit somewhere else: capture, a short charge, a visible ride along a path, then the
    /// ball leaves the exit with real velocity and is ordinary physics again.
    ///
    /// Like <see cref="WaterwheelCarrier"/> the ride is a *hold*, not simulated physics: the ball is made kinematic through <see cref="GolfBall.TryHold"/>,
    /// moved along the path with <see cref="GolfBall.MoveHeld"/> and handed back with <see cref="GolfBall.EndHold"/>. A hold counts no stroke, raises no
    /// rest/stop event and is not out of bounds; <see cref="GolfBall.PlaceAt"/> (reset, hole change, out-of-bounds return) always cancels it, so an
    /// abandoned ride cannot leave the ball in a bad state. A ball that arrives too fast is simply not captured. Everything before the capture and
    /// after the exit is the existing ball physics. The component is geometry-free: the mouth, the chute and the cannon are dressing built by the hole.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class VentTransport : MonoBehaviour
    {
        public enum Phase { Idle, Settling, Charging, Travelling }

        [SerializeField] VentTransportSpec spec = new VentTransportSpec();
        [SerializeField] GolfBall ball;
        [SerializeField] Vector3[] path = new Vector3[0];

        GolfBall m_Held;
        Phase m_Phase;
        float m_PhaseTime, m_HeldTime, m_NoCaptureTime;
        Vector3 m_StartPos;
        bool m_WasInReach;

        public VentTransportSpec Spec => spec;
        /// <summary>The ride: path[0] is the ball centre resting in the mouth, the last point is the ball centre at the exit. Local space.</summary>
        public Vector3[] Path => path;
        public Phase State => m_Phase;
        public GolfBall HeldBall => m_Held;
        public int CaptureCount { get; private set; }
        public int ReleaseCount { get; private set; }
        /// <summary>Balls that reached the mouth but were too fast to be captured.</summary>
        public int RejectedCount { get; private set; }

        public event Action<VentTransport, GolfBall> Captured;
        public event Action<VentTransport, GolfBall> Released;

        public void Configure(VentTransportSpec s, Vector3[] ridePath, GolfBall b = null)
        {
            spec = s ?? new VentTransportSpec();
            path = ridePath ?? new Vector3[0];
            ball = b;
        }

        /// <summary>Overrides the ball to watch (default: the active hole's ball).</summary>
        public void SetBall(GolfBall b) => ball = b;

        public Vector3 IntakeLocal => path.Length > 0 ? path[0] : Vector3.zero;
        public Vector3 ExitLocal => path.Length > 0 ? path[path.Length - 1] : Vector3.zero;
        public Vector3 IntakeWorld => transform.TransformPoint(IntakeLocal);
        public Vector3 ExitWorld => transform.TransformPoint(ExitLocal);
        /// <summary>Total time from capture to the exit (settle + charge + ride).</summary>
        public float TotalSeconds => spec.captureBlendSeconds + spec.dwellSeconds + spec.travelSeconds;

        /// <summary>Unit horizontal exit direction in world space.</summary>
        public Vector3 ExitDirectionWorld
        {
            get
            {
                Vector3 d = spec.exitDirection; d.y = 0f;
                if (d.sqrMagnitude < 1e-6f) d = Vector3.forward;
                return transform.TransformDirection(d.normalized);
            }
        }

        /// <summary>The ball's velocity when it leaves the exit (world space).</summary>
        public Vector3 ExitVelocityWorld
        {
            get { Vector3 d = ExitDirectionWorld; d.y = 0f; return d.normalized * spec.exitSpeed; }
        }

        // ---------------- path maths (static, deterministic: used by the ride and by tests)

        /// <summary>Total length of a polyline.</summary>
        public static float PathLength(Vector3[] pts)
        {
            float len = 0f;
            for (int i = 0; i + 1 < pts.Length; i++) len += Vector3.Distance(pts[i], pts[i + 1]);
            return len;
        }

        /// <summary>Point at fraction <paramref name="t"/> (0..1) of the way along the polyline, by arc length.</summary>
        public static Vector3 PathPoint(Vector3[] pts, float t)
        {
            if (pts == null || pts.Length == 0) return Vector3.zero;
            if (pts.Length == 1) return pts[0];
            t = Mathf.Clamp01(t);
            float target = PathLength(pts) * t, run = 0f;
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                float seg = Vector3.Distance(pts[i], pts[i + 1]);
                if (run + seg >= target || i + 2 == pts.Length)
                    return Vector3.Lerp(pts[i], pts[i + 1], seg > 1e-6f ? Mathf.Clamp01((target - run) / seg) : 0f);
                run += seg;
            }
            return pts[pts.Length - 1];
        }

        /// <summary>Smooth ease in and out: the ride starts and ends gently.</summary>
        public static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        /// <summary>A smooth path through <paramref name="controlPoints"/> (a Catmull-Rom spline sampled <paramref name="samplesPerSegment"/> times per span).</summary>
        public static Vector3[] Smooth(Vector3[] controlPoints, int samplesPerSegment = 8)
        {
            if (controlPoints == null || controlPoints.Length < 3) return controlPoints;
            var outPts = new System.Collections.Generic.List<Vector3>();
            for (int i = 0; i + 1 < controlPoints.Length; i++)
            {
                Vector3 p0 = controlPoints[Mathf.Max(i - 1, 0)], p1 = controlPoints[i], p2 = controlPoints[i + 1], p3 = controlPoints[Mathf.Min(i + 2, controlPoints.Length - 1)];
                for (int s = 0; s < samplesPerSegment; s++)
                {
                    float t = s / (float)samplesPerSegment, t2 = t * t, t3 = t2 * t;
                    outPts.Add(0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            }
            outPts.Add(controlPoints[controlPoints.Length - 1]);
            return outPts.ToArray();
        }

        // ---------------- the state machine

        GolfBall ResolveBall()
        {
            if (ball) return ball;
            var hole = HoleController.Active;
            return hole ? hole.Ball : null;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (m_Held)
            {
                // Someone else ended the hold (reset, out-of-bounds return, hole change): forget the ball.
                if (!m_Held.IsHeld || !ReferenceEquals(m_Held.Holder, this)) Forget();
                else { Step(dt); return; }
            }
            if (m_NoCaptureTime > 0f) m_NoCaptureTime -= dt;
            var b = ResolveBall();
            if (b && b.InPlay && !b.IsHeld && m_NoCaptureTime <= 0f && path.Length >= 2) TryCapture(b);
        }

        void TryCapture(GolfBall b)
        {
            Vector3 mouth = IntakeWorld;
            Vector3 p = b.Position;
            float horizontal = Mathf.Sqrt((p.x - mouth.x) * (p.x - mouth.x) + (p.z - mouth.z) * (p.z - mouth.z));
            bool inReach = horizontal <= spec.captureRadius && Mathf.Abs(p.y - mouth.y) <= 0.1f;
            if (!inReach) { m_WasInReach = false; return; }
            if (b.Velocity.magnitude > spec.maxCaptureSpeed)
            {
                if (!m_WasInReach) RejectedCount++;    // counted once per pass
                m_WasInReach = true;
                return;
            }
            m_WasInReach = true;
            if (!b.TryHold(this)) return;
            m_Held = b;
            m_Phase = Phase.Settling;
            m_PhaseTime = 0f;
            m_HeldTime = 0f;
            m_StartPos = b.Position;
            CaptureCount++;
            Captured?.Invoke(this, b);
        }

        void Step(float dt)
        {
            m_HeldTime += dt;
            m_PhaseTime += dt;
            if (m_HeldTime >= spec.maxHoldSeconds) { Release(); return; }
            switch (m_Phase)
            {
                case Phase.Settling:
                {
                    float t = Ease(m_PhaseTime / Mathf.Max(1e-3f, spec.captureBlendSeconds));
                    Vector3 mouth = IntakeWorld + Vector3.down * spec.sinkDepth;
                    m_Held.MoveHeld(Vector3.Lerp(m_StartPos, mouth, t));
                    if (m_PhaseTime >= spec.captureBlendSeconds) { m_Phase = Phase.Charging; m_PhaseTime = 0f; }
                    break;
                }
                case Phase.Charging:
                    m_Held.MoveHeld(IntakeWorld + Vector3.down * spec.sinkDepth);
                    if (m_PhaseTime >= spec.dwellSeconds) { m_Phase = Phase.Travelling; m_PhaseTime = 0f; }
                    break;
                case Phase.Travelling:
                {
                    float u = Ease(m_PhaseTime / Mathf.Max(1e-3f, spec.travelSeconds));
                    // Ride from the sunk mouth position (blended in over the first part so there is no jump) to the exit.
                    Vector3 point = transform.TransformPoint(PathPoint(path, u));
                    float sink = spec.sinkDepth * Mathf.Clamp01(1f - u * 6f);
                    m_Held.MoveHeld(point + Vector3.down * sink);
                    if (m_PhaseTime >= spec.travelSeconds) Release();
                    break;
                }
            }
        }

        void Release()
        {
            var b = m_Held;
            Forget();
            // A pending MovePosition of a kinematic body is lost when it turns dynamic, so put the ball exactly on the exit first,
            // then hand it back with the exit velocity. EndHold counts no stroke and raises no Struck/Stopped event.
            Vector3 exit = ExitWorld;
            b.Body.position = exit;
            b.transform.position = exit;
            b.EndHold(ExitVelocityWorld);
            m_NoCaptureTime = spec.recaptureLockoutSeconds;
            ReleaseCount++;
            Released?.Invoke(this, b);
        }

        void Forget() { m_Held = null; m_Phase = Phase.Idle; m_PhaseTime = 0f; }

        void OnDisable()
        {
            if (m_Held && m_Held.IsHeld && ReferenceEquals(m_Held.Holder, this)) m_Held.EndHold(Vector3.zero);
            Forget();
        }
    }
}
