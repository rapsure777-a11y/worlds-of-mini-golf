using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Gamebreak.MiniGolf
{
    /// <summary>Tiny helpers for building world-space UGUI text in code.</summary>
    public static class WorldText
    {
        static Font s_Font;
        public static Font Font => s_Font ? s_Font : (s_Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        /// <summary>Creates a world-space canvas. Size is in canvas units, 1000 units = 1 metre.</summary>
        public static Canvas CreateCanvas(string name, Transform parent, Vector2 size, Color background)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one * 0.001f;
            if (background.a > 0f)
            {
                var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
                bg.transform.SetParent(go.transform, false);
                Stretch((RectTransform)bg.transform);
                bg.GetComponent<Image>().color = background;
            }
            return canvas;
        }

        public static Text CreateText(Transform parent, string name, int fontSize, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform, 20f);
            var t = go.GetComponent<Text>();
            t.font = Font;
            t.fontSize = fontSize;
            t.alignment = anchor;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            return t;
        }

        static void Stretch(RectTransform rt, float pad = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(pad, pad); rt.offsetMax = new Vector2(-pad, -pad);
        }
    }

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
                // Desktop: pin to the corner of the view.
                var cam = rig.Head.transform;
                m_Canvas.position = cam.position + cam.forward * 0.6f + cam.right * -0.28f + cam.up * 0.17f;
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

    /// <summary>Nine-hole scorecard panel. The rig toggles it and keeps it in front of the player.</summary>
    public class ScorecardPanel : MonoBehaviour
    {
        [SerializeField] CourseController course;
        Text m_Text;

        public void Configure(CourseController c) => course = c;

        void Awake()
        {
            var canvas = WorldText.CreateCanvas("ScorecardCanvas", transform, new Vector2(900, 300), new Color(0.98f, 0.95f, 0.85f, 0.95f));
            m_Text = WorldText.CreateText(canvas.transform, "Text", 34, TextAnchor.MiddleCenter, new Color(0.15f, 0.12f, 0.1f));
        }

        void OnEnable()
        {
            if (!course) return;
            course.ScorecardChanged += Refresh;
            Refresh(course);
        }

        void OnDisable() { if (course) course.ScorecardChanged -= Refresh; }

        void Refresh(CourseController c)
        {
            if (c.Card == null) return;
            var card = c.Card;
            var sb = new StringBuilder();
            sb.Append("<b>").Append(c.CourseName).Append("</b>\n\n");
            sb.Append("Hole   ");
            for (int i = 0; i < card.par.Length; i++) sb.Append((i + 1).ToString().PadLeft(4));
            sb.Append("    Tot\nPar    ");
            int parTotal = 0;
            for (int i = 0; i < card.par.Length; i++) { sb.Append(card.par[i].ToString().PadLeft(4)); parTotal += card.par[i]; }
            sb.Append(parTotal.ToString().PadLeft(7)).Append("\nScore  ");
            for (int i = 0; i < card.strokes.Length; i++)
                sb.Append((card.strokes[i] > 0 ? card.strokes[i].ToString() : "-").PadLeft(4));
            sb.Append(card.TotalStrokes.ToString().PadLeft(7));
            if (c.Finished)
            {
                int diff = card.TotalStrokes - card.TotalParPlayed;
                sb.Append("\n\n<b>Course complete: ").Append(diff == 0 ? "even par" : (diff > 0 ? "+" + diff : diff.ToString())).Append("</b>");
            }
            m_Text.text = sb.ToString();
        }
    }
}
