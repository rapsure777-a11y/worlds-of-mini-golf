using System.Collections.Generic;
using UnityEngine;
using R = Gamebreak.MiniGolf.Editor.Art.Palette.Row;

namespace Gamebreak.MiniGolf.Editor.Art
{
    /// <summary>
    /// Island terrain and ocean. The height function is analytic so set dressing can query ground
    /// height anywhere. Hole sites are flattened via zones so greens sit level on the ground.
    /// Sea level is y = 0.
    /// </summary>
    public class IslandGen
    {
        public struct Zone
        {
            public Vector2 centre;   // world XZ
            public Vector2 halfSize; // local half extents
            public float yaw;        // degrees
            public float height;     // ground height inside
            public float feather;    // blend distance outside (m)
            public bool path;        // paint as dirt path instead of grass
        }

        public Vector2 centre = new Vector2(0f, 0f);
        public float radius = 34f;
        public float hillHeight = 3.2f;
        public int seed = 7;
        public float extent = 50f;   // terrain half size
        public float cell = 0.5f;
        public readonly List<Zone> zones = new List<Zone>();
        /// <summary>Extra raised mounds (world XZ, radius, height) for backdrops.</summary>
        public readonly List<(Vector2 c, float r, float h)> mounds = new List<(Vector2, float, float)>();

        /// <summary>Normalised distance to the coast (1 at the shoreline), with a noisy outline.</summary>
        public float CoastParam(float x, float z)
        {
            Vector2 d = new Vector2(x, z) - centre;
            float ang = Mathf.Atan2(d.y, d.x);
            float wobble = (Noise.Fbm(Mathf.Cos(ang) * 1.6f + 10f, Mathf.Sin(ang) * 1.6f + 10f, seed, 3) - 0.5f) * 2f;
            float r = radius * (1f + 0.16f * wobble);
            return d.magnitude / r;
        }

        float Natural(float x, float z)
        {
            float s = CoastParam(x, z);
            float h;
            if (s >= 1f) h = -0.05f - Mathf.Min((s - 1f) * 9f, 3.5f);
            else
            {
                float beach = Mathf.SmoothStep(0f, 0.55f, Mathf.InverseLerp(1f, 0.8f, s));
                float inland = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f, 0.45f, s));
                float hills = (Noise.Fbm(x * 0.045f + 3f, z * 0.045f - 2f, seed + 3, 4) - 0.35f) * hillHeight;
                h = beach + inland * Mathf.Max(0f, hills);
            }
            foreach (var m in mounds)
            {
                float t = 1f - Mathf.Clamp01(Vector2.Distance(new Vector2(x, z), m.c) / m.r);
                h += m.h * t * t * (3f - 2f * t);
            }
            return h;
        }

        /// <summary>Zone influence (1 inside, fading to 0 over the feather distance).</summary>
        static float ZoneWeight(in Zone zn, float x, float z)
        {
            Vector2 d = new Vector2(x, z) - zn.centre;
            // Inverse of Unity's yaw rotation (world = Euler(0, yaw, 0) * local).
            float rad = zn.yaw * Mathf.Deg2Rad;
            Vector2 local = new Vector2(d.x * Mathf.Cos(rad) - d.y * Mathf.Sin(rad), d.x * Mathf.Sin(rad) + d.y * Mathf.Cos(rad));
            Vector2 outside = new Vector2(Mathf.Max(0f, Mathf.Abs(local.x) - zn.halfSize.x), Mathf.Max(0f, Mathf.Abs(local.y) - zn.halfSize.y));
            float dist = outside.magnitude;
            if (zn.feather <= 0f) return dist <= 0f ? 1f : 0f;
            float t = 1f - Mathf.Clamp01(dist / zn.feather);
            return t * t * (3f - 2f * t);
        }

        public float Height(float x, float z)
        {
            float h = Natural(x, z);
            foreach (var zn in zones)
            {
                if (zn.path) continue;
                float w = ZoneWeight(zn, x, z);
                if (w > 0f) h = Mathf.Lerp(h, zn.height, w);
            }
            foreach (var zn in zones)
            {
                if (!zn.path) continue;
                float w = ZoneWeight(zn, x, z);
                if (w > 0f) h = Mathf.Lerp(h, Mathf.Max(zn.height, h - 0.05f), w * 0.6f);
            }
            return h;
        }

        /// <summary>Strength of the strongest hole (non-path) zone at this point.</summary>
        float HoleZone(float x, float z)
        {
            float w = 0f;
            foreach (var zn in zones) if (!zn.path) w = Mathf.Max(w, ZoneWeight(zn, x, z));
            return w;
        }

        bool OnPath(float x, float z)
        {
            foreach (var zn in zones) if (zn.path && ZoneWeight(zn, x, z) > 0.55f) return true;
            return false;
        }

        (Vector2 uv, Color c) Paint(float x, float z, float h, Vector3 n)
        {
            float jitter = Noise.Fbm(x * 0.35f, z * 0.35f, seed + 9, 2);
            if (h < -0.03f) return (Palette.UV(R.Seabed, Mathf.Clamp01(1f + h / 3f) * 0.9f), Color.white);
            float sandLine = 0.32f + (jitter - 0.5f) * 0.35f;
            float s = CoastParam(x, z);
            // Hole sites read as lawns, not sand pads.
            if (h < sandLine && s > 0.62f && HoleZone(x, z) < 0.2f + jitter * 0.5f) return (Palette.UV(R.Sand, Mathf.Clamp01(0.15f + h / sandLine * 0.85f)), Color.white);
            if (OnPath(x, z)) return (Palette.UV(R.Path, 0.4f + 0.4f * jitter), Color.white);
            // Steeper ground a little darker.
            float steep = 1f - Mathf.Clamp01(n.y);
            float patch = Noise.Fbm(x * 0.08f, z * 0.08f, seed + 21, 3);
            return (Palette.UV(R.Grass, Mathf.Clamp01(0.12f + 0.55f * patch + 0.3f * jitter - steep * 0.8f)), Color.white);
        }

        public Mesh BuildTerrain()
        {
            int n = Mathf.CeilToInt(extent * 2f / cell);
            var mb = new MeshBuilder();
            var heights = new float[n + 1, n + 1];
            for (int j = 0; j <= n; j++)
            for (int i = 0; i <= n; i++)
                heights[i, j] = Height(centre.x - extent + i * cell, centre.y - extent + j * cell);
            for (int j = 0; j <= n; j++)
            for (int i = 0; i <= n; i++)
            {
                float x = centre.x - extent + i * cell, z = centre.y - extent + j * cell;
                float hx = heights[Mathf.Min(i + 1, n), j] - heights[Mathf.Max(i - 1, 0), j];
                float hz = heights[i, Mathf.Min(j + 1, n)] - heights[i, Mathf.Max(j - 1, 0)];
                var nrm = new Vector3(-hx, 2f * cell, -hz).normalized;
                var (uv, c) = Paint(x, z, heights[i, j], nrm);
                mb.Vertex(new Vector3(x, heights[i, j], z), nrm, uv, c);
            }
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                int a = j * (n + 1) + i, b = a + 1, c = a + n + 1, d = c + 1;
                mb.Triangle(a, c, d);
                mb.Triangle(a, d, b);
            }
            return mb.ToMesh("IslandTerrain");
        }

        /// <summary>Ocean disc around the island; vertex colour R = how far from shore (0..1).</summary>
        public Mesh BuildOcean(float outerRadius = 420f, int rings = 48, int sectors = 96)
        {
            var mb = new MeshBuilder();
            for (int r = 0; r <= rings; r++)
            {
                float rad = outerRadius * Mathf.Pow(r / (float)rings, 2.2f);
                for (int s = 0; s <= sectors; s++)
                {
                    float a = s / (float)sectors * Mathf.PI * 2f;
                    float x = centre.x + Mathf.Cos(a) * rad, z = centre.y + Mathf.Sin(a) * rad;
                    float depth = Mathf.Abs(x - centre.x) < extent && Mathf.Abs(z - centre.y) < extent ? -Height(x, z) : 4f;
                    float shore = Mathf.Clamp01(depth / 2.2f);
                    mb.Vertex(new Vector3(x, 0f, z), Vector3.up, Vector2.zero, new Color(shore, 0f, 0f, 1f));
                }
            }
            for (int r = 0; r < rings; r++)
            for (int s = 0; s < sectors; s++)
            {
                int a = r * (sectors + 1) + s, b = a + 1, c = a + sectors + 1, d = c + 1;
                // Polar grid: angle increases counter-clockwise from above, so this order faces up.
                mb.Triangle(a, b, d);
                mb.Triangle(a, d, c);
            }
            return mb.ToMesh("Ocean");
        }
    }
}
