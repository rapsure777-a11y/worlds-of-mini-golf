using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// Wrist "watch" on the off-hand: hole, par, strokes and the last result. It sits on the wrist just
    /// behind the controller, turns to face the player, and fades in only when the player raises the
    /// wrist and looks at it (playtest feedback: a panel floating over the hand ended up near the ground
    /// and was hard to read). In desktop debug mode it is pinned to the corner of the view.
    /// </summary>
    public class WristDisplay : MonoBehaviour
    {
        const float LookDot = 0.9f;       // how directly the player must look at the watch (cos ~25 deg)
        const float MaxViewDistance = 0.9f;
        const float FadeSpeed = 8f;
        /// <summary>Wrist position in grip-pose space: behind the grip toward the forearm, slightly up.</summary>
        static readonly Vector3 WristOffset = new Vector3(0f, 0.025f, -0.1f);

        [SerializeField] CourseController course;
        [SerializeField] VRRig rig;

        Text m_Text;
        Transform m_Canvas;
        CanvasGroup m_Group;
        string m_Banner;
        float m_BannerUntil;

        public void Configure(CourseController c, VRRig r) { course = c; rig = r; }

        void Awake()
        {
            var canvas = WorldText.CreateCanvas("WristCanvas", transform, new Vector2(240, 120), new Color(0.04f, 0.07f, 0.12f, 0.92f));
            m_Canvas = canvas.transform;
            m_Canvas.localScale = Vector3.one * 0.0007f; // about 17 x 8 cm, watch-sized
            m_Group = canvas.gameObject.AddComponent<CanvasGroup>();
            m_Text = WorldText.CreateText(canvas.transform, "Text", 30, TextAnchor.MiddleCenter, Color.white);
            m_Text.lineSpacing = 0.95f;
        }

        void OnEnable() { if (course) course.HoleFinished += OnFinished; }
        void OnDisable() { if (course) course.HoleFinished -= OnFinished; }

        void OnFinished(CourseController c, HoleController h)
        {
            m_Banner = Scorecard.ResultName(h.Strokes, h.Par);
            m_BannerUntil = Time.time + 4f;
        }

        void LateUpdate()
        {
            var hand = rig ? rig.OffHand : null;
            float targetAlpha = 1f;
            if (hand && rig.XRActive)
            {
                var head = rig.Head.transform;
                Vector3 pos = hand.TransformPoint(WristOffset);
                Vector3 toWatch = pos - head.position;
                m_Canvas.position = pos;
                if (toWatch.sqrMagnitude > 1e-4f) m_Canvas.rotation = Quaternion.LookRotation(toWatch, head.up);
                bool looking = toWatch.magnitude < MaxViewDistance && Vector3.Dot(head.forward, toWatch.normalized) > LookDot;
                targetAlpha = looking ? 1f : 0f;
            }
            else if (rig)
            {
                // Desktop: pin to the upper right of the view.
                var cam = rig.Head.transform;
                m_Canvas.position = cam.position + cam.forward * 0.6f + cam.right * 0.3f + cam.up * 0.17f;
                m_Canvas.rotation = cam.rotation;
                m_Canvas.localScale = Vector3.one * 0.001f;
            }
            m_Group.alpha = Mathf.MoveTowards(m_Group.alpha, targetAlpha, FadeSpeed * Time.deltaTime);

            var hole = course ? course.Current : null;
            if (!hole) { m_Text.text = ""; return; }
            var sb = new StringBuilder();
            sb.Append("<b>Hole ").Append(hole.HoleNumber).Append("</b>  par ").Append(hole.Par).Append('\n');
            sb.Append("<size=40><b>").Append(hole.Strokes).Append("</b></size> ").Append(hole.Strokes == 1 ? "stroke" : "strokes");
            if (Time.time < m_BannerUntil) sb.Append("\n<color=#FFD54A><b>").Append(m_Banner).Append("</b></color>");
            m_Text.text = sb.ToString();
        }
    }
}
