using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Gamebreak.MiniGolf
{
    /// <summary>Hole, par and stroke count shown above the off-hand, plus a result banner.</summary>
    public class WristDisplay : MonoBehaviour
    {
        [SerializeField] CourseController course;
        [SerializeField] VRRig rig;

        Text m_Text;
        Transform m_Canvas;
        string m_Banner;
        float m_BannerUntil;

        public void Configure(CourseController c, VRRig r) { course = c; rig = r; }

        void Awake()
        {
            var canvas = WorldText.CreateCanvas("WristCanvas", transform, new Vector2(260, 120), new Color(0.05f, 0.1f, 0.15f, 0.75f));
            m_Canvas = canvas.transform;
            m_Text = WorldText.CreateText(canvas.transform, "Text", 26, TextAnchor.MiddleCenter, Color.white);
        }

        void OnEnable() { if (course) course.HoleFinished += OnFinished; }
        void OnDisable() { if (course) course.HoleFinished -= OnFinished; }

        void OnFinished(CourseController c, HoleController h)
        {
            m_Banner = Scorecard.ResultName(h.Strokes, h.Par);
            m_BannerUntil = Time.time + 3f;
        }

        void LateUpdate()
        {
            var hand = rig ? rig.OffHand : null;
            if (hand && rig.XRActive)
            {
                m_Canvas.position = hand.position + Vector3.up * 0.09f;
                Vector3 toHead = rig.Head.transform.position - m_Canvas.position;
                if (toHead.sqrMagnitude > 1e-4f) m_Canvas.rotation = Quaternion.LookRotation(-toHead);
            }
            else if (rig)
            {
                // Desktop: pin to the upper right of the view.
                var cam = rig.Head.transform;
                m_Canvas.position = cam.position + cam.forward * 0.6f + cam.right * 0.3f + cam.up * 0.17f;
                m_Canvas.rotation = cam.rotation;
            }

            var hole = course ? course.Current : null;
            if (!hole) { m_Text.text = ""; return; }
            var sb = new StringBuilder();
            sb.Append("<b>Hole ").Append(hole.HoleNumber).Append("</b>  Par ").Append(hole.Par).Append('\n');
            sb.Append("Strokes <b>").Append(hole.Strokes).Append("</b>");
            if (Time.time < m_BannerUntil) sb.Append("\n<color=#FFD54A><b>").Append(m_Banner).Append("</b></color>");
            m_Text.text = sb.ToString();
        }
    }
}
