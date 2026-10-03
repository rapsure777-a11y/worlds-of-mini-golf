using UnityEngine;

namespace Gamebreak.MiniGolf.Editor.Art
{
    /// <summary>Deterministic noise helpers for the art generators.</summary>
    public static class Noise
    {
        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        static float Fade(float t) => t * t * (3f - 2f * t);

        /// <summary>Value noise in [0,1]; with period > 0 it tiles every 'period' lattice cells.</summary>
        public static float Value(float x, float y, int seed, int period = 0)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = Fade(x - x0), fy = Fade(y - y0);
            int x1 = x0 + 1, y1 = y0 + 1;
            if (period > 0)
            {
                x0 = Mod(x0, period); x1 = Mod(x1, period); y0 = Mod(y0, period); y1 = Mod(y1, period);
            }
            float a = Hash(x0, y0, seed), b = Hash(x1, y0, seed), c = Hash(x0, y1, seed), d = Hash(x1, y1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>Fractal value noise in roughly [0,1].</summary>
        public static float Fbm(float x, float y, int seed, int octaves = 4, int period = 0)
        {
            float sum = 0f, amp = 0.5f, norm = 0f, f = 1f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Value(x * f, y * f, seed + i * 31, period > 0 ? period * (int)f : 0) * amp;
                norm += amp; amp *= 0.5f; f *= 2f;
            }
            return sum / norm;
        }

        /// <summary>3D-ish noise for displacing meshes (two 2D lookups blended).</summary>
        public static float Value3(Vector3 p, int seed) =>
            (Value(p.x + p.z * 0.7f, p.y, seed) + Value(p.y + 17.3f, p.z - p.x * 0.5f, seed + 7)) * 0.5f;

        static int Mod(int a, int m) => ((a % m) + m) % m;
    }
}
