using System;
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
            /// <summary>If set, ground height as a function of world XZ (e.g. a hole's height function) instead of the constant <see cref="height"/>.</summary>
            public Func<Vector2, float> heightFn;
            /// <summary>Broad plateau under a hole: applied before ravine channels so channels cut through it. Other zones apply after.</summary>
            public bool plateau;
        }

        /// <summary>A ravine / stream cut: a trench between two world XZ points with a flat-ish floor and rounded walls.</summary>
        public struct Channel
        {
            public Vector2 a, b;
            public float halfWidth;   // distance from the axis at which the walls reach the surrounding ground
            public float depth;       // carve at the floor (m below the surrounding ground)
            public float floorFraction; // 0..1 share of halfWidth that is flat floor (default 0.35)
            public float endTaper;    // distance over which the cut fades out at both ends (m)
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
        /// <summary>Carved depressions (world XZ, radius, depth) for pools; applied after the mounds.</summary>
        public readonly List<(Vector2 c, float r, float depth)> basins = new List<(Vector2, float, float)>();
        public readonly List<Channel> channels = new List<Channel>();
        /// <summary>Skip terrain cells that are flat deep sea floor (no triangles, no collider). Used for secondary islands so seabeds of neighbouring islands never overlap.</summary>
        public bool skipDeepSea;
        /// <summary>Optional terrain material for this island (the Volcanic Island's basalt); null = the shared tropical splat material.</summary>
        public Material terrainMaterial;

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
            foreach (var b in basins)
            {
                float t = 1f - Mathf.Clamp01(Vector2.Distance(new Vector2(x, z), b.c) / b.r);
                h -= b.depth * t * t * (3f - 2f * t);
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

        /// <summary>Strength (0..1) of the strongest channel cut at this point, 1 on the floor.</summary>
        public float ChannelWeight(float x, float z) => ChannelProfile(x, z, false);

        float CutDepth(float x, float z) => ChannelProfile(x, z, true);

        /// <summary>Strongest channel profile at a point: 0..1 weight, or metres of cut when <paramref name="scaleByDepth"/>.</summary>
        float ChannelProfile(float x, float z, bool scaleByDepth)
        {
            float best = 0f;
            var p = new Vector2(x, z);
            foreach (var c in channels)
            {
                Vector2 ab = c.b - c.a;
                float len = ab.magnitude;
                if (len < 1e-3f) continue;
                float t = Mathf.Clamp01(Vector2.Dot(p - c.a, ab) / (len * len));
                float dist = Vector2.Distance(p, c.a + ab * t);
                float u = 1f - Mathf.Clamp01(dist / c.halfWidth);                 // 1 on the axis, 0 at the rim
                float floorT = Mathf.Clamp(c.floorFraction <= 0f ? 0.35f : c.floorFraction, 0.05f, 0.95f);
                float profile = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / (1f - floorT)));
                float along = t * len;
                float taper = c.endTaper <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(along, len - along) / c.endTaper));
                best = Mathf.Max(best, profile * taper * (scaleByDepth ? c.depth : 1f));
            }
            return best;
        }

        public float Height(float x, float z)
        {
            float h = Natural(x, z);
            foreach (var zn in zones)
            {
                if (zn.path || !zn.plateau) continue;
                float w = ZoneWeight(zn, x, z);
                if (w > 0f) h = Mathf.Lerp(h, zn.heightFn != null ? zn.heightFn(new Vector2(x, z)) : zn.height, w);
            }
            if (channels.Count > 0) h -= CutDepth(x, z);
            foreach (var zn in zones)
            {
                if (zn.path || zn.plateau) continue;
                float w = ZoneWeight(zn, x, z);
                if (w > 0f) h = Mathf.Lerp(h, zn.heightFn != null ? zn.heightFn(new Vector2(x, z)) : zn.height, w);
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

        /// <summary>When true, terrain vertex colours carry TerrainSplat weights instead of palette UVs.</summary>
        public bool splat;
        [Tooltip("0..1: how much of the flat ground outside the holes is painted as bare rock (a mountain top, not a lawn).")]
        public float rockBias = 0f;

        /// <summary>
        /// Splat weights for Gamebreak/TerrainSplat: R sand, G lawn, B rock, A path. Soft, noisy transitions;
        /// steep slopes become exposed sandstone; underwater stays sand (the shader tints the seabed).
        /// </summary>
        Color Splat(float x, float z, float h, Vector3 n)
        {
            float jitter = Noise.Fbm(x * 0.35f, z * 0.35f, seed + 9, 2);
            float s = CoastParam(x, z);
            float hole = HoleZone(x, z);
            float sandLine = 0.32f + (jitter - 0.5f) * 0.35f;
            float sand = h < 0f ? 1f : Mathf.Clamp01((sandLine + 0.12f - h) / 0.24f) * Mathf.Clamp01((s - 0.58f) / 0.08f);
            sand *= 1f - Mathf.Clamp01((hole - (0.1f + jitter * 0.5f)) / 0.15f);
            float pathW = 0f;
            foreach (var zn in zones) if (zn.path) pathW = Mathf.Max(pathW, ZoneWeight(zn, x, z));
            float path = Mathf.Clamp01((pathW - 0.35f + (jitter - 0.5f) * 0.3f) / 0.3f);
            float rock = Mathf.Clamp01((0.86f - n.y) / 0.12f + (Noise.Fbm(x * 0.2f, z * 0.2f, seed + 31, 2) - 0.5f) * 0.6f);
            rock *= 1f - hole;
            if (rockBias > 0f) rock = Mathf.Max(rock, rockBias * Mathf.Clamp01(0.6f + (Noise.Fbm(x * 0.13f, z * 0.13f, seed + 47, 3) - 0.5f) * 1.6f) * (1f - 0.35f * hole));
            // Pool basins: sandy bed under the water, no rock or path.
            float pool = 0f;
            foreach (var b in basins) pool = Mathf.Max(pool, 1f - Mathf.Clamp01(Vector2.Distance(new Vector2(x, z), b.c) / (b.r * 1.2f)));
            pool = Mathf.Clamp01(pool * 2.5f);
            // Ravine floors: sandy/pebbly bed (the walls turn to rock through the slope rule).
            float bed = channels.Count > 0 ? Mathf.Clamp01((ChannelWeight(x, z) - 0.85f) / 0.1f) : 0f;
            pool = Mathf.Max(pool, bed);
            rock *= 1f - pool; path *= 1f - pool; sand = Mathf.Max(sand, pool);
            // Priority: rock, then path, then sand; lawn takes the remainder.
            float rest = 1f;
            rock = Mathf.Min(rock, rest); rest -= rock;
            path = Mathf.Min(path, rest); rest -= path;
            sand = Mathf.Min(sand, rest); rest -= sand;
            return new Color(sand, Mathf.Max(0f, rest), rock, path);
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
                if (splat)
                    mb.Vertex(new Vector3(x, heights[i, j], z), nrm, new Vector2(x, z), Splat(x, z, heights[i, j], nrm));
                else
                {
                    var (uv, c) = Paint(x, z, heights[i, j], nrm);
                    mb.Vertex(new Vector3(x, heights[i, j], z), nrm, uv, c);
                }
            }
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                if (skipDeepSea && Mathf.Max(Mathf.Max(heights[i, j], heights[i + 1, j]), Mathf.Max(heights[i, j + 1], heights[i + 1, j + 1])) < -3.3f) continue;
                int a = j * (n + 1) + i, b = a + 1, c = a + n + 1, d = c + 1;
                mb.Triangle(a, c, d);
                mb.Triangle(a, d, b);
            }
            return mb.ToMesh("IslandTerrain");
        }

        /// <summary>Ocean disc around this island; vertex colour R = how far from shore (0..1).</summary>
        public Mesh BuildOcean(float outerRadius = 420f, int rings = 48, int sectors = 96)
            => BuildOcean(centre, outerRadius, rings, sectors, 2.2f, null);

        /// <summary>
        /// Ocean disc around any centre (e.g. the middle of an archipelago). <paramref name="depthAt"/> returns the water depth
        /// (m, 0 or less = land) at a world XZ point, or a large value in open sea; null uses this island's height inside its extent.
        /// Vertex colour R = depth / 2.2 m (0 at the shore, 1 = deep). Radial spacing grows with distance (exponent).
        /// </summary>
        public Mesh BuildOcean(Vector2 oceanCentre, float outerRadius, int rings, int sectors, float exponent, Func<float, float, float> depthAt)
        {
            var mb = new MeshBuilder();
            for (int r = 0; r <= rings; r++)
            {
                float rad = outerRadius * Mathf.Pow(r / (float)rings, exponent);
                for (int s = 0; s <= sectors; s++)
                {
                    float a = s / (float)sectors * Mathf.PI * 2f;
                    float x = oceanCentre.x + Mathf.Cos(a) * rad, z = oceanCentre.y + Mathf.Sin(a) * rad;
                    float depth = depthAt != null ? depthAt(x, z)
                        : Mathf.Abs(x - centre.x) < extent && Mathf.Abs(z - centre.y) < extent ? -Height(x, z) : 4f;
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
