using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// Procedural art for UI cards, generated once at runtime so no texture assets are needed: a soft dark banner, a radial glow
    /// and an ornamental divider (fading rule with a diamond and two dots). White with alpha: tint through the Image colour.
    /// Also picks an elegant serif for titles (an installed OS font, falling back to Unity's built-in).
    /// </summary>
    public static class TitleArt
    {
        /// <summary>Set false to force Unity's built-in font (e.g. if a machine has none of the serif fonts).</summary>
        public static bool UseOsFont = true;

        static Font s_Serif;
        static Sprite s_Banner, s_Glow, s_Divider;

        public static Font Serif
        {
            get
            {
                if (s_Serif) return s_Serif;
                if (UseOsFont)
                {
                    try { s_Serif = Font.CreateDynamicFontFromOSFont(new[] { "Georgia", "Palatino Linotype", "Book Antiqua", "Times New Roman" }, 64); }
                    catch (System.Exception) { s_Serif = null; }
                }
                return s_Serif ? s_Serif : (s_Serif = WorldText.Font);
            }
        }

        /// <summary>Wide soft-edged band, darkest in the middle: sits behind the title so it reads against bright sky.</summary>
        public static Sprite Banner => s_Banner ? s_Banner : (s_Banner = Make(512, 128, (u, v) =>
        {
            float h = Smooth(u / 0.2f) * Smooth((1f - u) / 0.2f);
            float vy = (v - 0.5f) / 0.34f;
            float vert = Mathf.Exp(-vy * vy * 1.4f);
            return 0.82f * h * vert;
        }));

        /// <summary>Radial glow, white at the centre fading to nothing: tint it with the area's accent colour.</summary>
        public static Sprite Glow => s_Glow ? s_Glow : (s_Glow = Make(128, 128, (u, v) =>
        {
            float d = Mathf.Clamp01(Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f);
            return Mathf.Pow(1f - d, 2.2f);
        }));

        /// <summary>A hairline rule that fades toward both ends with a diamond at the centre and a small dot either side of it.</summary>
        public static Sprite Divider => s_Divider ? s_Divider : (s_Divider = Make(512, 32, (u, v) =>
        {
            float x = (u - 0.5f) * 512f, y = (v - 0.5f) * 32f;
            float rule = Mathf.Abs(y) < 1.2f ? Mathf.Clamp01(1f - Mathf.Abs(x) / 245f) : 0f;
            float diamond = Mathf.Abs(x) + Mathf.Abs(y) * 1.4f < 12f ? 1f : 0f;
            float inner = Mathf.Abs(x) + Mathf.Abs(y) * 1.4f < 6f ? -0.35f : 0f;   // hollow centre
            float dot = Mathf.Min(Mathf.Sqrt((Mathf.Abs(x) - 34f) * (Mathf.Abs(x) - 34f) + y * y), 99f) < 3.2f ? 1f : 0f;
            return Mathf.Clamp01(Mathf.Max(rule * 0.85f, diamond + inner, dot));
        }));

        static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        static Sprite Make(int w, int h, System.Func<float, float, float> alpha)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha((x + 0.5f) / w, (y + 0.5f) / h)) * 255f);
                px[y * w + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            tex.name = "TitleArt";
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
