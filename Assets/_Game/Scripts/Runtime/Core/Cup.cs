using System;
using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// The cup is real geometry (a hole cut in the green, see <see cref="CourseGeometry"/>), so
    /// lip-outs and fast balls skipping over it come from physics. This component only decides
    /// when a ball has dropped far enough to count as holed.
    /// The transform sits at the centre of the cup opening, on the green surface.
    /// </summary>
    public class Cup : MonoBehaviour
    {
        [SerializeField] GolfTuning tuning;
        [Tooltip("Optional flag that hides when a ball is close so it never blocks the view.")]
        [SerializeField] GameObject flag;
        [SerializeField] float flagHideDistance = 0.8f;

        public GolfTuning Tuning => tuning ? tuning : GolfTuning.Default;
        public float Radius => Tuning.cupRadius;

        public event Action<Cup, GolfBall> BallHoled;

        GolfBall[] m_Balls = Array.Empty<GolfBall>();

        public void Configure(GolfTuning t, GameObject flagObject)
        {
            tuning = t; flag = flagObject;
        }

        public void Track(params GolfBall[] balls) => m_Balls = balls ?? Array.Empty<GolfBall>();

        public bool ContainsBall(GolfBall ball)
        {
            Vector3 local = transform.InverseTransformPoint(ball.Position);
            float horiz = new Vector2(local.x, local.z).magnitude;
            return horiz < Radius && local.y < -ball.Radius * 1.2f && local.y > -Tuning.cupDepth - 0.05f;
        }

        void FixedUpdate()
        {
            foreach (var b in m_Balls)
            {
                if (!b || !b.InPlay) continue;
                if (ContainsBall(b))
                {
                    b.InPlay = false;
                    BallHoled?.Invoke(this, b);
                }
            }
        }

        void Update()
        {
            if (!flag) return;
            bool near = false;
            foreach (var b in m_Balls)
                if (b && b.InPlay && (b.Position - transform.position).sqrMagnitude < flagHideDistance * flagHideDistance)
                    near = true;
            if (flag.activeSelf == near) flag.SetActive(!near);
        }
    }
}
