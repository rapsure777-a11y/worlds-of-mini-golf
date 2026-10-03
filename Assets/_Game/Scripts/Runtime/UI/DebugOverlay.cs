using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// Monitor-only debug panel (IMGUI does not render in the headset). Shows controls in desktop
    /// mode and live numbers useful while tuning or watching someone play in VR. F1 toggles it.
    /// </summary>
    public class DebugOverlay : MonoBehaviour
    {
        [SerializeField] CourseController course;
        [SerializeField] VRRig rig;
        [SerializeField] bool visible = true;

        float m_LastStrike, m_PeakBall, m_SmoothedDt = 1f / 60f;
        int m_Strikes;
        GUIStyle m_Style;
        Putter m_Putter;

        public void Configure(CourseController c, VRRig r) { course = c; rig = r; }

        void Start()
        {
            m_Putter = rig ? rig.Putter : null;
            if (m_Putter) m_Putter.StruckBall += OnStrike;
        }

        void OnDestroy()
        {
            if (m_Putter) m_Putter.StruckBall -= OnStrike;
        }

        void OnStrike(Putter p, float speed)
        {
            m_LastStrike = speed;
            m_Strikes++;
            m_PeakBall = 0f;
        }

        void Update()
        {
            m_SmoothedDt = Mathf.Lerp(m_SmoothedDt, Time.unscaledDeltaTime, 0.05f);
            var kb = Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame) visible = !visible;
            var hole = course ? course.Current : null;
            if (hole && hole.Ball) m_PeakBall = Mathf.Max(m_PeakBall, hole.Ball.SurfaceSpeed);
        }

        void OnGUI()
        {
            if (!visible) return;
            if (m_Style == null)
            {
                m_Style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14, richText = true, wordWrap = false };
                m_Style.normal.textColor = Color.white;
                m_Style.padding = new RectOffset(10, 10, 8, 8);
            }

            var sb = new System.Text.StringBuilder();
            bool xr = rig && rig.XRActive;
            sb.Append("<b>Worlds of Mini Golf</b>   ")
              .Append(xr ? "VR: " + XRSettings.loadedDeviceName : "<color=#ffd54a>Desktop debug</color>")
              .Append($"   {m_SmoothedDt * 1000f:F1} ms ({1f / Mathf.Max(m_SmoothedDt, 1e-4f):F0} fps)\n");

            var hole = course ? course.Current : null;
            if (hole)
            {
                sb.Append($"Hole {hole.HoleNumber}/{course.Holes.Length}  par {hole.Par}  strokes {hole.Strokes}")
                  .Append(hole.IsComplete ? "  <b>complete</b>" : "").Append('\n');
                var ball = hole.Ball;
                if (ball)
                {
                    float toCup = hole.Cup ? Vector3.Distance(ball.Position, hole.Cup.transform.position) : 0f;
                    sb.Append($"Ball {ball.SurfaceSpeed:F2} m/s  {(ball.IsAtRest ? "at rest" : ball.IsGrounded ? "rolling" : "airborne")}  to cup {toCup:F2} m\n");
                }
            }
            if (m_Strikes > 0) sb.Append($"Last strike {m_LastStrike:F2} m/s (peak roll {m_PeakBall:F2})  strikes {m_Strikes}\n");
            if (m_Putter)
                sb.Append($"Putter length {m_Putter.Length:F2} m  angle {m_Putter.AngleOffset:F0}°  twist {m_Putter.HeadTwist:F0}°  head {m_Putter.HeadVelocity.magnitude:F2} m/s\n");
            if (rig) sb.Append(rig.LeftHanded ? "Left-handed\n" : "Right-handed\n");
            if (!xr) sb.Append('\n').Append(VRRig.DesktopHelp);

            var content = new GUIContent(sb.ToString());
            Vector2 size = m_Style.CalcSize(content);
            GUI.Box(new Rect(10, 10, size.x, size.y), content, m_Style);
        }
    }
}
