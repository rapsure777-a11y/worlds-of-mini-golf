using System.IO;
using UnityEditor;
using UnityEngine;

namespace Gamebreak.MiniGolf.Editor.Art
{
    /// <summary>Generates the kit's textures as PNG assets (palette, grain, water normals, foam).</summary>
    public static class TextureGen
    {
        public const string Dir = "Assets/_Game/Art/Textures";

        public static Texture2D Palette() => Save(Art.Palette.Build(), "TropicalPalette", t =>
        {
            t.filterMode = FilterMode.Bilinear;
            t.wrapMode = TextureWrapMode.Clamp;
            t.mipmapEnabled = false;
            t.textureCompression = TextureImporterCompression.Uncompressed;
        });

        /// <summary>Tileable grayscale grain centred on 0.5, used by the triplanar detail.</summary>
        public static Texture2D Grain()
        {
            const int size = 256, period = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size * period, v = y / (float)size * period;
                float n = Noise.Fbm(u, v, 11, 4, period) * 0.75f + Noise.Value(u * 4f, v * 4f, 5, period * 4) * 0.25f;
                float g = Mathf.Clamp01(0.5f + (n - 0.5f) * 1.6f);
                tex.SetPixel(x, y, new Color(g, g, g, 1f));
            }
            tex.Apply();
            return Save(tex, "Grain", t => { t.wrapMode = TextureWrapMode.Repeat; t.sRGBTexture = false; });
        }

        /// <summary>Tileable water normal map from a noise height field.</summary>
        public static Texture2D WaterNormal()
        {
            const int size = 256, period = 8;
            var h = new float[size, size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                h[x, y] = Noise.Fbm(x / (float)size * period, y / (float)size * period, 23, 3, period);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = h[(x + 1) % size, y] - h[(x - 1 + size) % size, y];
                float dy = h[x, (y + 1) % size] - h[x, (y - 1 + size) % size];
                var n = new Vector3(-dx * 12f, -dy * 12f, 1f).normalized;
                tex.SetPixel(x, y, new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f));
            }
            tex.Apply();
            return Save(tex, "WaterNormal", t => { t.textureType = TextureImporterType.NormalMap; t.wrapMode = TextureWrapMode.Repeat; });
        }

        public static Texture2D Foam()
        {
            const int size = 128, period = 8;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = Noise.Fbm(x / (float)size * period, y / (float)size * period, 41, 3, period);
                tex.SetPixel(x, y, new Color(n, n, n, 1f));
            }
            tex.Apply();
            return Save(tex, "FoamNoise", t => { t.wrapMode = TextureWrapMode.Repeat; t.sRGBTexture = false; });
        }

        static Texture2D Save(Texture2D tex, string name, System.Action<TextureImporter> configure)
        {
            Directory.CreateDirectory(Dir);
            string path = $"{Dir}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            configure(importer);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
