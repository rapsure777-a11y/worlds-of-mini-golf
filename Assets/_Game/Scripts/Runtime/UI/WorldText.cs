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

        /// <summary>Text in a fixed rectangle (canvas units, origin top-left).</summary>
        public static Text CreateCell(Transform parent, string name, Rect r, int fontSize, Color color)
        {
            var t = CreateText(parent, name, fontSize, TextAnchor.MiddleCenter, color);
            var rt = (RectTransform)t.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(r.x, -r.y);
            rt.sizeDelta = new Vector2(r.width, r.height);
            return t;
        }

        static void Stretch(RectTransform rt, float pad = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(pad, pad); rt.offsetMax = new Vector2(-pad, -pad);
        }
    }
}
