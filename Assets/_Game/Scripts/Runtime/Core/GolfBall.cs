using System;
using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// Golf ball with a hand-written rolling model on top of PhysX contacts.
    ///
    /// PhysX has no rolling resistance and treats a frictionless sphere as sliding,
    /// so this script owns the ball's velocity integration:
    ///  - gravity on slopes is scaled by 5/7 (solid sphere rolling without slipping),
    ///  - a constant rolling deceleration plus a little speed drag slows it,
    ///  - rolling resistance holds the ball still on gentle slopes,
    ///  - wall rebounds are computed from the pre-contact velocity so they are
    ///    consistent regardless of CCD or the PhysX bounce threshold.
    /// PhysX only resolves penetration. All colliders use zero friction and zero bounce.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    [DefaultExecutionOrder(-50)]
    public class GolfBall : MonoBehaviour
    {
        const float GroundMinNormalY = 0.6f;

        [SerializeField] GolfTuning tuning;
        [Tooltip("Optional child mesh rotated to show rolling. The rigidbody itself never rotates.")]
        [SerializeField] Transform visual;

        public GolfTuning Tuning => tuning ? tuning : GolfTuning.Default;
        public Rigidbody Body { get; private set; }
        public float Radius => Tuning.ballRadius;
        public bool IsGrounded { get; private set; }
        public Vector3 GroundNormal { get; private set; } = Vector3.up;
        public bool IsAtRest { get; private set; } = true;
        /// <summary>False while the ball is holed or being reset; it then ignores strikes.</summary>
        public bool InPlay { get; set; } = true;
        public Vector3 Velocity => Body.linearVelocity;
        public Vector3 Position => Body.position;

        public event Action<GolfBall> Stopped;
        public event Action<GolfBall, Vector3> Struck;
        /// <summary>Raised on a wall rebound with the impact speed along the wall normal.</summary>
        public event Action<GolfBall, float> HitWall;

        public static bool DebugContacts;

        int m_GroundContacts;
        Vector3 m_GroundNormalSum;
        int m_WallContacts;
        Vector3 m_WallNormalSum;
        Vector3 m_PreStepVelocity;
        float m_SlowTimer;
        Vector3 m_StepStartPosition;

        public void SetTuning(GolfTuning t)
        {
            tuning = t;
            if (Body) Configure();
        }

        public void SetVisual(Transform v) => visual = v;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Configure();
        }

        void Configure()
        {
            var t = Tuning;
            Body.mass = t.ballMass;
            Body.useGravity = false;
            Body.linearDamping = 0f;
            Body.angularDamping = 0f;
            Body.constraints = RigidbodyConstraints.FreezeRotation;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.sleepThreshold = 0f; // we detect rest ourselves; sleeping would stop contact callbacks
            Body.maxDepenetrationVelocity = 0.5f;

            var col = GetComponent<SphereCollider>();
            col.radius = t.ballRadius;
            col.contactOffset = 0.002f;
            col.sharedMaterial = GolfMaterials.Ball;
        }

        void FixedUpdate()
        {
            var t = Tuning;
            float dt = Time.fixedDeltaTime;

            IsGrounded = m_GroundContacts > 0;
            GroundNormal = IsGrounded ? m_GroundNormalSum.normalized : Vector3.up;
            bool touchedWall = m_WallContacts > 0;
            Vector3 wallNormal = touchedWall ? m_WallNormalSum.normalized : Vector3.zero;
            m_GroundContacts = 0; m_GroundNormalSum = Vector3.zero;
            m_WallContacts = 0; m_WallNormalSum = Vector3.zero;

            if (Body.isKinematic) return;
            m_StepStartPosition = Body.position;

            Vector3 v = Body.linearVelocity;

            // Wall rebound from the velocity we had before PhysX resolved the contact.
            if (touchedWall)
            {
                float vn = Vector3.Dot(m_PreStepVelocity, wallNormal);
                if (vn < -0.02f)
                {
                    Vector3 tangent = m_PreStepVelocity - vn * wallNormal;
                    v = tangent * t.wallTangentKeep - vn * t.wallBounciness * wallNormal;
                    // Keep whatever the ground contact did to the vertical part.
                    if (IsGrounded) v -= Vector3.Dot(v, GroundNormal) * GroundNormal;
                    HitWall?.Invoke(this, -vn);
                }
            }

            Vector3 g = Physics.gravity;
            if (IsGrounded)
            {
                Vector3 n = GroundNormal;
                Vector3 gN = Vector3.Dot(g, n) * n;
                Vector3 gT = g - gN;
                Vector3 vN = Vector3.Dot(v, n) * n;
                Vector3 vT = v - vN;
                Vector3 slopeAccel = gT * (5f / 7f);
                float speed = vT.magnitude;

                if (speed < t.restSpeed && slopeAccel.magnitude <= t.rollingDeceleration)
                {
                    vT = Vector3.zero; // rolling resistance holds it on a gentle slope
                }
                else
                {
                    vT += slopeAccel * dt;
                    float s = vT.magnitude;
                    float drop = (t.rollingDeceleration + t.speedDrag * s) * dt;
                    vT = s > drop ? vT * (1f - drop / s) : Vector3.zero;
                }
                // Separating velocity is kept so the ball can roll over edges and leave ramps.
                if (Vector3.Dot(vN, n) < 0f) vN = Vector3.zero;
                v = vT + vN + gN * dt;
                // A rigid, inelastic cup lip turns forward speed into a big vertical kick. Real rims are
                // rounded and soft, so cap how much upward speed a single contact can add.
                float lift = v.y - m_PreStepVelocity.y;
                if (lift > t.maxContactLift) v.y = m_PreStepVelocity.y + t.maxContactLift;
                m_SurfaceSpeed = vT.magnitude;
            }
            else
            {
                v += g * dt;
                m_SurfaceSpeed = v.magnitude;
            }

            if (v.sqrMagnitude > t.maxBallSpeed * t.maxBallSpeed) v = v.normalized * t.maxBallSpeed;

            Body.linearVelocity = v;
            m_PreStepVelocity = v;
            UpdateRest(dt, t);
        }

        float m_SurfaceSpeed;

        /// <summary>Speed along the ground (ignores the small push that keeps the ball on the surface).</summary>
        public float SurfaceSpeed => m_SurfaceSpeed;

        void UpdateRest(float dt, GolfTuning t)
        {
            float speed = m_SurfaceSpeed;
            if (IsAtRest)
            {
                if (speed > t.restSpeed * 2f) { IsAtRest = false; m_SlowTimer = 0f; }
                return;
            }
            if (InPlay && IsGrounded && speed < t.restSpeed)
            {
                m_SlowTimer += dt;
                if (m_SlowTimer >= t.restTime)
                {
                    IsAtRest = true;
                    m_SurfaceSpeed = 0f;
                    Body.linearVelocity = Vector3.zero;
                    m_PreStepVelocity = Vector3.zero;
                    Stopped?.Invoke(this);
                }
            }
            else m_SlowTimer = 0f;
        }

        void OnCollisionEnter(Collision c) => GatherContacts(c);
        void OnCollisionStay(Collision c) => GatherContacts(c);

        void GatherContacts(Collision c)
        {
            if (c.collider.isTrigger) return;
            Vector3 center = Body.position;
            for (int i = 0; i < c.contactCount; i++)
            {
                var cp = c.GetContact(i);
                // PhysX builds contacts from the pose at the start of the step, so the true sphere
                // normal is (start-of-step centre - contact point). This is exact on mesh edges such
                // as the cup rim, where PhysX may report the neighbouring face normal instead.
                Vector3 n = m_StepStartPosition - cp.point;
                if (n.sqrMagnitude > 1e-10f) n.Normalize();
                else
                {
                    n = cp.normal;
                    if (Vector3.Dot(n, center - cp.point) < 0f) n = -n;
                }
                if (DebugContacts) Debug.Log($"[Contact] {c.collider.name} n {cp.normal:F3} -> {n:F3} pt {cp.point:F4} ctr {center:F4} sep {cp.separation:F4}");
                if (n.y >= GroundMinNormalY) { m_GroundContacts++; m_GroundNormalSum += n; }
                else { m_WallContacts++; m_WallNormalSum += n; }
            }
        }

        void Update()
        {
            if (!visual) return;
            Vector3 v = Body.linearVelocity;
            Vector3 axis = Vector3.Cross(GroundNormal, v);
            float mag = axis.magnitude;
            if (mag < 1e-5f) return;
            float angle = v.magnitude * Time.deltaTime / Radius * Mathf.Rad2Deg;
            visual.Rotate(axis / mag, angle, Space.World);
        }

        /// <summary>Launch the ball. Counts as a stroke for whoever listens to <see cref="Struck"/>.</summary>
        public void Strike(Vector3 velocity)
        {
            if (!InPlay) return;
            float max = Tuning.maxBallSpeed;
            if (velocity.sqrMagnitude > max * max) velocity = velocity.normalized * max;
            Body.linearVelocity = velocity;
            m_PreStepVelocity = velocity;
            m_SurfaceSpeed = velocity.magnitude;
            IsAtRest = false;
            m_SlowTimer = 0f;
            Struck?.Invoke(this, velocity);
        }

        /// <summary>Teleport the ball to a resting position (tee, reset, etc.).</summary>
        public void PlaceAt(Vector3 position)
        {
            Body.position = position;
            transform.position = position;
            Body.linearVelocity = Vector3.zero;
            m_PreStepVelocity = Vector3.zero;
            m_StepStartPosition = position;
            m_SurfaceSpeed = 0f;
            m_SlowTimer = 0f;
            IsAtRest = true;
            InPlay = true;
        }
    }
}
