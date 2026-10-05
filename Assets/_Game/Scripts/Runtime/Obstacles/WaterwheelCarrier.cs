using System;
using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>Tunable numbers for a waterwheel carrier. Wheel plane is the carrier's local XY plane, the axle is local Z.</summary>
    [Serializable]
    public class WaterwheelCarrierSpec
    {
        [Header("Wheel")]
        [Tooltip("Radius at which a ball sits in a bucket (ball centre).")]
        public float pocketRadius = 0.5f;
        [Range(2, 8)] public int bucketCount = 4;
        [Tooltip("Seconds for one full turn. The wheel always turns; it never waits for the ball.")]
        public float periodSeconds = 14f;
        [Tooltip("Off: counter-clockwise in the local XY plane (buckets rise on the +X side, move toward +X at the bottom).")]
        public bool reverse = false;
        [Tooltip("Angle of bucket 0 at start, degrees from local +X toward +Y.")]
        public float startAngleDegrees = -150f;
        [Tooltip("Half the width of a bucket and of the wheel (visual).")]
        public float wheelHalfWidth = 0.5f;

        [Header("Capture")]
        [Tooltip("A ball whose centre is this close to an empty bucket pocket can be scooped.")]
        public float captureRadius = 0.11f;
        [Tooltip("Ball speed relative to the bucket above which the ball is too fast to be scooped (it bounces off the dock instead).")]
        public float maxCaptureSpeed = 1.6f;
        [Tooltip("Seconds the ball takes to settle into the bucket after capture.")]
        public float captureBlendSeconds = 0.15f;

        [Header("Release")]
        [Tooltip("The held ball is released when its bucket reaches this angle (degrees from local +X toward +Y).")]
        public float releaseAngleDegrees = 50f;
        [Tooltip("Direction the bucket tips the ball, in the carrier's local space (XY plane).")]
        public Vector3 releaseDirectionLocal = new Vector3(1f, -0.08f, 0f);
        [Tooltip("Speed the ball leaves the bucket with. After release the ball is ordinary physics.")]
        public float releaseSpeed = 0.9f;
        [Tooltip("Safety net: a ball held this long is released regardless (e.g. a stopped wheel).")]
        public float maxHoldSeconds = 30f;

        public float OmegaDegPerSecond => periodSeconds > 0.01f ? 360f / periodSeconds : 0f;
        /// <summary>Pocket position at the release angle, in the carrier's local space.</summary>
        public Vector3 ReleasePointLocal
        {
            get { float a = releaseAngleDegrees * Mathf.Deg2Rad; return new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * pocketRadius; }
        }
        public WaterwheelCarrierSpec Clone() => (WaterwheelCarrierSpec)MemberwiseClone();
    }

    /// <summary>
    /// A waterwheel that scoops the golf ball into a moving bucket, carries it upward and tips it onto an elevated channel.
    ///
    /// Transport is a *hold*, not physics: the ball is made kinematic through <see cref="GolfBall.TryHold"/>, follows its bucket pocket,
    /// and is handed back with <see cref="GolfBall.EndHold"/>. A hold counts no stroke, raises no rest/stop event and is not out of bounds;
    /// <see cref="GolfBall.PlaceAt"/> (reset, hole change, out-of-bounds return) always cancels it, so repeated and abandoned attempts
    /// cannot leave the ball in a bad state. A ball that is too fast, or arrives with no bucket in reach, is simply not captured and
    /// stays an ordinary rolling ball. Everything before capture and after release is the existing ball physics.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class WaterwheelCarrier : MonoBehaviour
    {
        [SerializeField] WaterwheelCarrierSpec spec = new WaterwheelCarrierSpec();
        [SerializeField] GolfBall ball;
        [SerializeField] ProvingMaterials materials = new ProvingMaterials();

        float m_Angle;               // angle of bucket 0, degrees
        GolfBall m_Held;
        int m_Bucket;
        Vector3 m_StartPos;
        float m_BlendT, m_Remaining, m_HeldTime, m_NoCaptureTime;
        /// <summary>After a release the carrier ignores the ball for this long, so it rolls clear of the bucket.</summary>
        const float RecaptureLockoutSeconds = 1.5f;
        Transform m_Visual;
        bool m_Initialised;

        public WaterwheelCarrierSpec Spec => spec;
        public GolfBall HeldBall => m_Held;
        public int CaptureCount { get; private set; }
        public int ReleaseCount { get; private set; }
        /// <summary>Balls that reached a bucket but were too fast to be scooped.</summary>
        public int RejectedCount { get; private set; }
        public float Direction => spec.reverse ? -1f : 1f;

        public event Action<WaterwheelCarrier, GolfBall> Captured;
        public event Action<WaterwheelCarrier, GolfBall> Released;

        public void Configure(WaterwheelCarrierSpec s, GolfBall b, ProvingMaterials m)
        {
            spec = s ?? new WaterwheelCarrierSpec(); ball = b; materials = m ?? new ProvingMaterials();
            m_Initialised = true;
            m_Angle = Mathf.Repeat(spec.startAngleDegrees, 360f);
            BuildVisual();
        }

        /// <summary>Overrides the ball to watch (default: the active hole's ball).</summary>
        public void SetBall(GolfBall b) => ball = b;

        /// <summary>Sets the wheel to a known phase (angle of bucket 0). Used by tests and by the scene builder.</summary>
        public void SetPhase(float bucketZeroAngleDegrees)
        {
            m_Angle = Mathf.Repeat(bucketZeroAngleDegrees, 360f);
            ApplyVisual(0f);
        }

        void Awake() => Init();

        void Init()
        {
            if (m_Initialised) return;
            m_Initialised = true;
            m_Angle = Mathf.Repeat(spec.startAngleDegrees, 360f);
            BuildVisual();
        }

        /// <summary>Angle of bucket <paramref name="i"/> in degrees (from local +X toward +Y).</summary>
        public float BucketAngle(int i) => Mathf.Repeat(m_Angle + i * 360f / spec.bucketCount, 360f);

        public Vector3 PocketLocal(int i)
        {
            float a = BucketAngle(i) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * spec.pocketRadius;
        }

        public Vector3 PocketWorld(int i) => transform.TransformPoint(PocketLocal(i));

        Vector3 PocketVelocityWorld(int i)
        {
            float a = BucketAngle(i) * Mathf.Deg2Rad;
            float w = Direction * spec.OmegaDegPerSecond * Mathf.Deg2Rad * spec.pocketRadius;
            return transform.TransformVector(new Vector3(-Mathf.Sin(a), Mathf.Cos(a), 0f) * w);
        }

        GolfBall ResolveBall()
        {
            if (ball) return ball;
            var hole = HoleController.Active;
            return hole ? hole.Ball : null;
        }

        void FixedUpdate()
        {
            Init();
            float dt = Time.fixedDeltaTime;
            m_Angle = Mathf.Repeat(m_Angle + Direction * spec.OmegaDegPerSecond * dt, 360f);

            if (m_Held)
            {
                // Someone else ended the hold (reset, out-of-bounds return, hole change): forget the ball.
                if (!m_Held.IsHeld || !ReferenceEquals(m_Held.Holder, this)) { m_Held = null; }
                else { CarryStep(dt); return; }
            }
            else m_Held = null;

            var b = ResolveBall();
            if (b && b.InPlay && !b.IsHeld && m_NoCaptureTime <= 0f) TryCapture(b);
            if (m_NoCaptureTime > 0f) m_NoCaptureTime -= dt;
        }

        void TryCapture(GolfBall b)
        {
            int best = -1;
            float bestD = spec.captureRadius;
            for (int i = 0; i < spec.bucketCount; i++)
            {
                float d = Vector3.Distance(b.Position, PocketWorld(i));
                if (d <= bestD) { bestD = d; best = i; }
            }
            if (best < 0) return;
            if ((b.Velocity - PocketVelocityWorld(best)).magnitude > spec.maxCaptureSpeed) { RejectedCount++; return; }
            if (!b.TryHold(this)) return;
            m_Held = b;
            m_Bucket = best;
            m_StartPos = b.Position;
            m_BlendT = 0f;
            m_HeldTime = 0f;
            m_Remaining = Mathf.Repeat(Direction * (spec.releaseAngleDegrees - BucketAngle(best)), 360f);
            CaptureCount++;
            Captured?.Invoke(this, b);
        }

        void CarryStep(float dt)
        {
            m_HeldTime += dt;
            m_BlendT = Mathf.Min(1f, m_BlendT + dt / Mathf.Max(1e-3f, spec.captureBlendSeconds));
            float s = m_BlendT * m_BlendT * (3f - 2f * m_BlendT);
            m_Held.MoveHeld(Vector3.Lerp(m_StartPos, PocketWorld(m_Bucket), s));
            m_Remaining -= spec.OmegaDegPerSecond * dt;
            if ((m_Remaining <= 0f && m_BlendT >= 1f) || m_HeldTime >= spec.maxHoldSeconds) Release();
        }

        void Release()
        {
            var b = m_Held;
            m_Held = null;
            Vector3 dir = transform.TransformDirection(spec.releaseDirectionLocal.sqrMagnitude > 1e-6f ? spec.releaseDirectionLocal.normalized : Vector3.right);
            b.EndHold(dir * spec.releaseSpeed);
            m_NoCaptureTime = RecaptureLockoutSeconds;   // the ball is still inside its old bucket's reach: do not scoop it straight back up
            ReleaseCount++;
            Released?.Invoke(this, b);
        }

        void OnDisable()
        {
            if (m_Held && m_Held.IsHeld && ReferenceEquals(m_Held.Holder, this)) m_Held.EndHold(Vector3.zero);
            m_Held = null;
        }

        void Update()
        {
            float frac = Mathf.Clamp(Time.time - Time.fixedTime, 0f, Time.fixedDeltaTime);
            ApplyVisual(Direction * spec.OmegaDegPerSecond * frac);
        }

        void ApplyVisual(float extraDegrees)
        {
            if (m_Visual) m_Visual.localRotation = Quaternion.Euler(0f, 0f, m_Angle + extraDegrees);
        }

        // ---------------- visuals (decorative only: no colliders)

        /// <summary>Rebuilds the wheel's look after editing <see cref="Spec"/> (radius, bucket count, width). Timing and capture settings apply live.</summary>
        [ContextMenu("Rebuild visual")]
        public void RebuildVisual() { m_Initialised = true; BuildVisual(); }

        void BuildVisual()
        {
            var old = transform.Find("WheelVisual");
            if (old) ProvingKit.Discard(old.gameObject);
            var pivot = new GameObject("WheelVisual").transform;
            pivot.SetParent(transform, false);
            m_Visual = pivot;
            Material wood = materials != null ? (materials.wood ? materials.wood : materials.wall) : null;
            float rimR = spec.pocketRadius + 0.11f, hw = spec.wheelHalfWidth, ballR = GolfTuning.Default.ballRadius;

            ProvingKit.AxialCylinder("Axle", pivot, Vector3.zero, Quaternion.identity, 0.06f, hw * 2f + 0.2f, wood);
            const int seg = 28;
            float chord = 2f * rimR * Mathf.Sin(Mathf.PI / seg) + 0.01f;
            for (int side = -1; side <= 1; side += 2)
            {
                for (int k = 0; k < seg; k++)
                {
                    float a = 360f * k / seg;
                    Vector3 p = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad), 0f) * rimR + Vector3.forward * (side * hw);
                    ProvingKit.Box("Rim", pivot, p, Quaternion.Euler(0f, 0f, a + 90f), new Vector3(chord, 0.05f, 0.05f), wood, false);
                }
                for (int k = 0; k < spec.bucketCount * 2; k++)
                {
                    float a = 360f * k / (spec.bucketCount * 2);
                    Vector3 p = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad), 0f) * (rimR * 0.5f) + Vector3.forward * (side * hw);
                    ProvingKit.Box("Spoke", pivot, p, Quaternion.Euler(0f, 0f, a), new Vector3(rimR, 0.03f, 0.03f), wood, false);
                }
            }
            float bw = hw * 0.62f;
            for (int i = 0; i < spec.bucketCount; i++)
            {
                float a = 360f * i / spec.bucketCount;
                Quaternion rot = Quaternion.Euler(0f, 0f, a);
                Vector3 radial = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad), 0f);
                Vector3 tang = new Vector3(-radial.y, radial.x, 0f);
                float inner = spec.pocketRadius + ballR;               // radius of the tray floor's inner face
                ProvingKit.Box("BucketFloor", pivot, radial * (inner + 0.012f), rot, new Vector3(0.024f, 0.16f, bw * 2f), wood, false);
                // A scoop: the wall on the leading side (the way the bucket travels) is left out so the ball can roll in;
                // the trailing wall is the one the ball rests against while it is carried up.
                float trail = -Direction;
                ProvingKit.Box("BucketBack", pivot, radial * (inner - 0.01f) + tang * (trail * 0.08f), rot, new Vector3(0.09f, 0.02f, bw * 2f), wood, false);
                for (int sgn = -1; sgn <= 1; sgn += 2)
                    ProvingKit.Box("BucketCheek", pivot, radial * (inner - 0.005f) + Vector3.forward * (sgn * (bw - 0.01f)), rot, new Vector3(0.07f, 0.17f, 0.02f), wood, false);
                for (int side = -1; side <= 1; side += 2)
                    ProvingKit.Box("BucketArm", pivot, radial * (inner + 0.012f) + Vector3.forward * (side * (bw + (hw - bw) * 0.5f)), rot,
                        new Vector3(0.04f, 0.04f, hw - bw), wood, false);
            }
        }
    }
}
