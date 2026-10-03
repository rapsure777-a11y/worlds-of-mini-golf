using UnityEngine;

namespace Gamebreak.MiniGolf.Editor.Art
{
    /// <summary>
    /// The Tropical Adventure colour palette: one small texture (8 columns x 16 rows) that every kit mesh
    /// samples through its UVs, so the whole world shares one material and batches well.
    /// Gradient rows run dark to light left to right; meshes pick a position along a row per vertex.
    /// Row order matters: terrain blends seabed -> sand -> grass, and rocks blend grass caps into warm stone,
    /// so those rows are adjacent (grass/path edges pass briefly through the similar warm-stone row)
    /// (bilinear filtering between neighbouring rows gives smooth transitions).
    /// </summary>
    public static class Palette
    {
        public const int Columns = 8, Rows = 16, CellPx = 16;

        public enum Row
        {
            Seabed = 0, Sand = 1, Grass = 2, RockWarm = 3, Path = 4, Leaf = 5, Jungle = 6, Trunk = 7,
            RockGrey = 8, Wood = 9, Thatch = 10, Stone = 11, Fruit = 12, Cloud = 13, Flowers = 14, Accents = 15,
        }

        // Solid colours in the Flowers and Accents rows.
        public enum Flower { Hibiscus = 0, Pink = 1, Orange = 2, Yellow = 3, White = 4, Purple = 5, Blue = 6, Magenta = 7 }
        public enum Accent { White = 0, DarkBrown = 1, Red = 2, Teal = 3, Gold = 4, Cream = 5, Coral = 6, Sky = 7 }

        static readonly (Color dark, Color light)[] k_Gradients =
        {
            (C(0.55f, 0.72f, 0.62f), C(0.93f, 0.90f, 0.70f)), // Seabed: deep teal-sand -> pale
            (C(0.90f, 0.70f, 0.40f), C(1.00f, 0.88f, 0.58f)), // Sand: wet -> dry (warm gold)
            (C(0.16f, 0.48f, 0.12f), C(0.58f, 0.88f, 0.26f)), // Grass
            (C(0.45f, 0.24f, 0.15f), C(0.98f, 0.76f, 0.50f)), // Rock warm (orange sandstone)
            (C(0.56f, 0.38f, 0.22f), C(0.86f, 0.68f, 0.44f)), // Path / dirt
            (C(0.06f, 0.40f, 0.14f), C(0.50f, 0.92f, 0.24f)), // Leaf (palms, bushes)
            (C(0.03f, 0.22f, 0.11f), C(0.22f, 0.62f, 0.22f)), // Jungle (dark undergrowth, moss)
            (C(0.30f, 0.18f, 0.10f), C(0.78f, 0.58f, 0.34f)), // Trunk
            (C(0.26f, 0.29f, 0.31f), C(0.74f, 0.75f, 0.70f)), // Rock grey
            (C(0.36f, 0.19f, 0.09f), C(0.86f, 0.58f, 0.30f)), // Wood
            (C(0.52f, 0.36f, 0.15f), C(0.98f, 0.84f, 0.48f)), // Thatch
            (C(0.33f, 0.37f, 0.31f), C(0.82f, 0.82f, 0.70f)), // Stone ruins
            (C(0.24f, 0.15f, 0.07f), C(0.60f, 0.78f, 0.20f)), // Fruit / coconut
            (C(0.74f, 0.82f, 0.94f), C(1.00f, 1.00f, 1.00f)), // Cloud
        };

        static readonly Color[] k_Flowers =
        {
            C(0.95f, 0.12f, 0.18f), C(1.00f, 0.45f, 0.70f), C(1.00f, 0.55f, 0.10f), C(1.00f, 0.86f, 0.15f),
            C(1.00f, 0.98f, 0.92f), C(0.62f, 0.30f, 0.90f), C(0.20f, 0.55f, 1.00f), C(0.90f, 0.15f, 0.65f),
        };

        static readonly Color[] k_Accents =
        {
            C(1.00f, 1.00f, 1.00f), C(0.16f, 0.10f, 0.06f), C(0.92f, 0.20f, 0.14f), C(0.10f, 0.70f, 0.72f),
            C(1.00f, 0.78f, 0.20f), C(1.00f, 0.95f, 0.82f), C(1.00f, 0.50f, 0.40f), C(0.45f, 0.78f, 1.00f),
        };

        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        /// <summary>UV for a point t (0 = dark, 1 = light) along a gradient row.</summary>
        public static Vector2 UV(Row row, float t)
        {
            float u = (0.5f + Mathf.Clamp01(t) * (Columns - 1)) / Columns;
            return new Vector2(u, RowV(row));
        }

        /// <summary>UV for a solid swatch in the Flowers or Accents rows.</summary>
        public static Vector2 Solid(Row row, int index) => new Vector2((index + 0.5f) / Columns, RowV(row));
        public static Vector2 FlowerUV(Flower f) => Solid(Row.Flowers, (int)f);
        /// <summary>Fractional swatch index: blends smoothly between two neighbouring solid swatches.</summary>
        public static Vector2 SolidBlend(Row row, float index) => new Vector2((index + 0.5f) / Columns, RowV(row));
        public static Vector2 AccentUV(Accent a) => Solid(Row.Accents, (int)a);

        static float RowV(Row row) => 1f - ((int)row + 0.5f) / Rows;

        public static Texture2D Build()
        {
            var tex = new Texture2D(Columns * CellPx, Rows * CellPx, TextureFormat.RGBA32, false, false);
            for (int row = 0; row < Rows; row++)
            for (int col = 0; col < Columns; col++)
            {
                Color c;
                if (row < k_Gradients.Length)
                {
                    var g = k_Gradients[row];
                    c = Color.Lerp(g.dark, g.light, col / (float)(Columns - 1));
                }
                else if (row == (int)Row.Flowers) c = k_Flowers[col];
                else c = k_Accents[col];
                int y0 = (Rows - 1 - row) * CellPx, x0 = col * CellPx;
                for (int y = 0; y < CellPx; y++)
                for (int x = 0; x < CellPx; x++)
                    tex.SetPixel(x0 + x, y0 + y, c);
            }
            tex.Apply();
            return tex;
        }
    }
}
