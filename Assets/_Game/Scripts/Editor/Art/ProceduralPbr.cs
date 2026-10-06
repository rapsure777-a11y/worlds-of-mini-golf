using System;

namespace Gamebreak.MiniGolf.Editor.Art
{
    /// <summary>
    /// Pure-maths procedural PBR texture sets for Graphics Pass 3 (no UnityEngine dependency, so the cloud tooling can run it and write previews).
    /// Every generator returns a <see cref="PbrSet"/>: albedo, a height field (turned into a normal map), occlusion, smoothness and an optional glow mask.
    /// Tileable sets use integer noise periods so their edges match. Deterministic (seeded). Conventions match the Blender sets under Art/Generated:
    /// <c>name_albedo</c>, <c>name_normal</c>, <c>name_mask</c> (G = occlusion, A = smoothness), plus <c>name_glow</c> where there is emission.
    /// </summary>
    public sealed class PbrSet
    {
        public readonly int Size;
        public readonly float[] R, G, B;        // albedo, linear-ish 0..1 (sRGB values as painted)
        public readonly float[] Height;         // 0..1
        public readonly float[] Occlusion;      // 0..1 (1 = open)
        public readonly float[] Smooth;         // 0..1
        public float[] Glow;                    // optional emission mask 0..1

        public PbrSet(int size)
        {
            Size = size;
            int n = size * size;
            R = new float[n]; G = new float[n]; B = new float[n]; Height = new float[n]; Occlusion = new float[n]; Smooth = new float[n];
            for (int i = 0; i < n; i++) { Occlusion[i] = 1f; Smooth[i] = 0.3f; }
        }

        public int Idx(int x, int y) => y * Size + x;

        /// <summary>RGBA8 albedo (opaque).</summary>
        public byte[] AlbedoBytes()
        {
            var b = new byte[Size * Size * 4];
            for (int i = 0; i < Size * Size; i++) { b[i * 4] = To8(R[i]); b[i * 4 + 1] = To8(G[i]); b[i * 4 + 2] = To8(B[i]); b[i * 4 + 3] = 255; }
            return b;
        }

        /// <summary>RGBA8 tangent-space normal map from the height field (wrapping at the edges when <paramref name="wrap"/>).</summary>
        public byte[] NormalBytes(float strength, bool wrap = true)
        {
            var b = new byte[Size * Size * 4];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float hl = H(x - 1, y, wrap), hr = H(x + 1, y, wrap), hd = H(x, y - 1, wrap), hu = H(x, y + 1, wrap);
                float nx = -(hr - hl) * strength, ny = -(hu - hd) * strength, nz = 1f;
                float len = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                int i = Idx(x, y) * 4;
                b[i] = To8(nx / len * 0.5f + 0.5f); b[i + 1] = To8(ny / len * 0.5f + 0.5f); b[i + 2] = To8(nz / len * 0.5f + 0.5f); b[i + 3] = 255;
            }
            return b;
        }

        float H(int x, int y, bool wrap)
        {
            if (wrap) { x = ((x % Size) + Size) % Size; y = ((y % Size) + Size) % Size; }
            else { x = Math.Min(Math.Max(x, 0), Size - 1); y = Math.Min(Math.Max(y, 0), Size - 1); }
            return Height[y * Size + x];
        }

        /// <summary>RGBA8 mask: R = 1, G = occlusion, B = 1, A = smoothness.</summary>
        public byte[] MaskBytes()
        {
            var b = new byte[Size * Size * 4];
            for (int i = 0; i < Size * Size; i++) { b[i * 4] = 255; b[i * 4 + 1] = To8(Occlusion[i]); b[i * 4 + 2] = 255; b[i * 4 + 3] = To8(Smooth[i]); }
            return b;
        }

        /// <summary>RGBA8 greyscale glow mask (or null when the set has none).</summary>
        public byte[] GlowBytes()
        {
            if (Glow == null) return null;
            var b = new byte[Size * Size * 4];
            for (int i = 0; i < Size * Size; i++) { byte v = To8(Glow[i]); b[i * 4] = v; b[i * 4 + 1] = v; b[i * 4 + 2] = v; b[i * 4 + 3] = 255; }
            return b;
        }

        static byte To8(float v) => (byte)(Math.Min(Math.Max(v, 0f), 1f) * 255f + 0.5f);
    }

    /// <summary>The Graphics Pass 3 surface generators.</summary>
    public static class ProceduralPbr
    {
        // ------------------------------------------------------------------ noise (tileable)

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
        static float Lerp(float a, float b, float t) => a + (b - a) * t;
        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        static float Smooth(float e0, float e1, float x) { float t = Clamp01((x - e0) / (e1 - e0)); return t * t * (3f - 2f * t); }
        static int Mod(int a, int m) => ((a % m) + m) % m;

        /// <summary>Value noise in [0,1] over (x, y) lattice cells; tiles every <paramref name="px"/> x <paramref name="py"/> cells.</summary>
        public static float Value(float x, float y, int seed, int px, int py)
        {
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            float fx = Fade(x - x0), fy = Fade(y - y0);
            int x1 = Mod(x0 + 1, px), y1 = Mod(y0 + 1, py);
            x0 = Mod(x0, px); y0 = Mod(y0, py);
            return Lerp(Lerp(Hash(x0, y0, seed), Hash(x1, y0, seed), fx), Lerp(Hash(x0, y1, seed), Hash(x1, y1, seed), fx), fy);
        }

        /// <summary>Fractal value noise in about [0,1] for u, v in 0..1 over the tile, at <paramref name="cellsX"/> x <paramref name="cellsY"/> base cells.</summary>
        public static float Fbm(float u, float v, int cellsX, int cellsY, int seed, int octaves)
        {
            float sum = 0f, amp = 0.5f, norm = 0f; int fx = cellsX, fy = cellsY;
            for (int i = 0; i < octaves; i++)
            {
                sum += Value(u * fx, v * fy, seed + i * 31, fx, fy) * amp;
                norm += amp; amp *= 0.5f; fx *= 2; fy *= 2;
            }
            return sum / norm;
        }

        /// <summary>Tileable cellular noise: distance to the nearest and second-nearest feature point (in cell units) and the nearest cell's id hash (0..1).</summary>
        static void Worley(float u, float v, int cells, int seed, out float f1, out float f2, out float id)
        {
            float x = u * cells, y = v * cells;
            int cx = (int)Math.Floor(x), cy = (int)Math.Floor(y);
            f1 = 9f; f2 = 9f; id = 0f;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = cx + dx, ny = cy + dy;
                int wx = Mod(nx, cells), wy = Mod(ny, cells);
                float px = nx + 0.15f + 0.7f * Hash(wx, wy, seed), py = ny + 0.15f + 0.7f * Hash(wx, wy, seed + 101);
                float d = (float)Math.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
                if (d < f1) { f2 = f1; f1 = d; id = Hash(wx, wy, seed + 202); }
                else if (d < f2) f2 = d;
            }
        }

        static void Colour(PbrSet s, int i, float r, float g, float b) { s.R[i] = r; s.G[i] = g; s.B[i] = b; }
        static void Mix(PbrSet s, int i, float[] a, float[] b, float t)
        {
            s.R[i] = Lerp(a[0], b[0], t); s.G[i] = Lerp(a[1], b[1], t); s.B[i] = Lerp(a[2], b[2], t);
        }

        // ------------------------------------------------------------------ putting turf

        /// <summary>
        /// Fine mowed turf, one tile = 1 m of green. Dense short blades stretched along one direction (a directional grain), a faint mowing stripe, soft clumps and
        /// slow colour drift. Restrained on purpose: low contrast and a very gentle height field so the ball line stays readable and nothing shimmers.
        /// </summary>
        public static PbrSet Turf(int size = 512, int seed = 5)
        {
            var s = new PbrSet(size);
            float[] dark = { 0.10f, 0.36f, 0.06f }, light = { 0.30f, 0.68f, 0.13f }, dry = { 0.40f, 0.60f, 0.14f };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float blade = Fbm(u, v, 220, 70, seed, 3);                      // fine strands, stretched along u
                float fine = Hash(x, y, seed + 9);                              // per-texel pepper
                float clump = Fbm(u, v, 14, 14, seed + 3, 3);
                float drift = Fbm(u, v, 3, 3, seed + 7, 2);
                float stripe = 0.5f + 0.5f * (float)Math.Sin(v * Math.PI * 4f); // two soft mowing bands per metre
                float t = Clamp01(0.08f + (blade - 0.5f) * 0.9f + 0.34f + (fine - 0.5f) * 0.22f + (clump - 0.5f) * 0.55f + (stripe - 0.5f) * 0.12f);
                int i = s.Idx(x, y);
                Mix(s, i, dark, light, t);
                float dryAmt = Smooth(0.58f, 0.85f, drift) * 0.30f;
                s.R[i] = Lerp(s.R[i], dry[0], dryAmt); s.G[i] = Lerp(s.G[i], dry[1], dryAmt); s.B[i] = Lerp(s.B[i], dry[2], dryAmt);
                s.Height[i] = blade * 0.55f + fine * 0.15f + clump * 0.30f;
                s.Occlusion[i] = 0.78f + 0.22f * Clamp01(blade * 0.7f + fine * 0.3f);
                s.Smooth[i] = 0.55f + 0.35f * stripe;                          // the mowing bands catch light differently (the mask multiplies a low base)
            }
            return s;
        }

        // ------------------------------------------------------------------ stone

        /// <summary>Warm carved sandstone, laid in ashlar courses (tile = 1 m, four courses): mortar joints, per-block tint, weathering, chips.</summary>
        public static PbrSet TempleSandstone(int size = 512, int seed = 21)
        {
            var s = new PbrSet(size);
            float[] a = { 0.68f, 0.50f, 0.26f }, b = { 0.93f, 0.76f, 0.44f }, dirt = { 0.38f, 0.28f, 0.14f };
            const int courses = 4;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                int row = (int)Math.Floor(v * courses);
                float rowV = v * courses - row;
                int blocksInRow = 2 + (Hash(row, 0, seed) > 0.5f ? 1 : 0);          // 2 or 3 blocks per row (tiles because it depends only on the row)
                float off = Hash(row, 1, seed) * 0.5f;
                float bu = (u + off) * blocksInRow;
                int col = (int)Math.Floor(bu);
                float colU = bu - col;
                float edge = Math.Min(Math.Min(rowV, 1f - rowV) * courses * 0.55f, Math.Min(colU, 1f - colU) * blocksInRow * 0.55f);  // 0 at the joint
                float mortar = 1f - Smooth(0.012f, 0.045f, edge);
                float bandTint = Hash(Mod(col, blocksInRow) + row * 7, row, seed + 5);
                float grain = Fbm(u, v, 24, 24, seed + 2, 4);
                float weather = Fbm(u, v, 5, 5, seed + 8, 3);
                float chip = Smooth(0.72f, 0.9f, Fbm(u, v, 40, 40, seed + 11, 2)) * (1f - mortar);
                float speck = Hash(x, y, seed + 17);
                float streak = Fbm(u, v, 14, 2, seed + 19, 3);
                float t = Clamp01(0.12f + bandTint * 0.55f + (grain - 0.5f) * 0.9f + 0.28f + (speck - 0.5f) * 0.10f - streak * 0.18f);
                int i = s.Idx(x, y);
                Mix(s, i, a, b, t);
                float dirtAmt = Clamp01(weather * 0.8f + mortar * 0.9f + chip * 0.3f + streak * 0.25f);
                s.R[i] = Lerp(s.R[i], dirt[0], dirtAmt * 0.6f); s.G[i] = Lerp(s.G[i], dirt[1], dirtAmt * 0.6f); s.B[i] = Lerp(s.B[i], dirt[2], dirtAmt * 0.6f);
                s.Height[i] = Clamp01(0.62f + grain * 0.28f - mortar * 0.55f - chip * 0.25f);
                s.Occlusion[i] = Clamp01(1f - mortar * 0.55f - chip * 0.15f);
                s.Smooth[i] = Clamp01(0.30f + grain * 0.12f - mortar * 0.1f);
            }
            return s;
        }

        /// <summary>Pale tropical limestone cliff rock: horizontal strata, pitting, erosion streaks. Tile = 2 m. <paramref name="wet"/> darkens and glosses it (waterfall spray zones).</summary>
        public static PbrSet Limestone(int size = 512, int seed = 33, bool wet = false)
        {
            var s = new PbrSet(size);
            float[] a = { 0.60f, 0.57f, 0.50f }, b = { 0.86f, 0.83f, 0.74f }, moss = { 0.22f, 0.36f, 0.16f };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float strata = Fbm(u, v, 2, 14, seed, 3);
                float grain = Fbm(u, v, 20, 20, seed + 4, 4);
                float pit = Smooth(0.68f, 0.82f, Fbm(u, v, 32, 32, seed + 6, 2));
                float streak = Fbm(u, v, 14, 2, seed + 9, 3);                      // vertical erosion streaks
                float band = 0.5f + 0.5f * (float)Math.Sin((v * 9f + strata * 2.2f) * Math.PI * 2f);
                float t = Clamp01(0.05f + (strata - 0.5f) * 0.9f + 0.3f + band * 0.22f + (grain - 0.5f) * 0.55f + 0.2f - pit * 0.3f);
                int i = s.Idx(x, y);
                Mix(s, i, a, b, t);
                float damp = wet ? 0.55f + streak * 0.25f : Smooth(0.7f, 1.0f, streak) * 0.10f;
                float mossAmt = wet ? Smooth(0.55f, 0.85f, Fbm(u, v, 8, 8, seed + 13, 3)) * 0.55f : 0f;
                s.R[i] = Lerp(s.R[i], s.R[i] * (1f - damp * 0.55f), 1f); s.G[i] = s.G[i] * (1f - damp * 0.5f); s.B[i] = s.B[i] * (1f - damp * 0.5f);
                s.R[i] = Lerp(s.R[i], moss[0], mossAmt); s.G[i] = Lerp(s.G[i], moss[1], mossAmt); s.B[i] = Lerp(s.B[i], moss[2], mossAmt);
                s.Height[i] = Clamp01(0.45f + band * 0.25f + strata * 0.25f + grain * 0.3f - pit * 0.35f);
                s.Occlusion[i] = Clamp01(1f - pit * 0.5f);
                s.Smooth[i] = Clamp01((wet ? 0.62f : 0.22f) + grain * 0.1f - pit * 0.1f);
            }
            return s;
        }

        /// <summary>Dark volcanic basalt, tile = 2 m: dark fractured plates with glowing cracks (the glow mask). Cracks are narrow and sparse so the rock stays dark and the lava stays a feature.</summary>
        public static PbrSet Basalt(int size = 512, int seed = 47)
        {
            var s = new PbrSet(size) { Glow = new float[size * size] };
            float[] a = { 0.065f, 0.062f, 0.07f }, b = { 0.24f, 0.22f, 0.21f };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float grain = Fbm(u, v, 22, 22, seed, 4);
                float rough = Hash(x, y, seed + 5);
                float wu = u + (Fbm(u, v, 6, 6, seed + 2, 2) - 0.5f) * 0.06f, wv = v + (Fbm(u, v, 6, 6, seed + 3, 2) - 0.5f) * 0.06f;
                float f1, f2, id;
                Worley(wu - (float)Math.Floor(wu), wv - (float)Math.Floor(wv), 9, seed + 11, out f1, out f2, out id);   // polygonal plates (columns seen from above)
                float edge = f2 - f1;
                float crack = 1f - Smooth(0.012f, 0.075f, edge);
                float hot = Smooth(0.62f, 0.82f, id) * Smooth(0.35f, 0.65f, Fbm(u, v, 3, 3, seed + 9, 2));   // only some plates are hot at their rims
                float plateTint = id;
                int i = s.Idx(x, y);
                Mix(s, i, a, b, Clamp01(0.10f + plateTint * 0.35f + (grain - 0.5f) * 0.7f + 0.2f + (rough - 0.5f) * 0.10f));
                s.R[i] = Lerp(s.R[i], 0.015f, crack * 0.85f); s.G[i] = Lerp(s.G[i], 0.012f, crack * 0.85f); s.B[i] = Lerp(s.B[i], 0.012f, crack * 0.85f);
                s.Height[i] = Clamp01(0.55f + grain * 0.3f + plateTint * 0.1f - crack * 0.55f);
                s.Occlusion[i] = Clamp01(1f - crack * 0.6f);
                s.Smooth[i] = Clamp01(0.26f + grain * 0.12f - crack * 0.1f);
                s.Glow[i] = crack * hot * Smooth(0.0f, 0.5f, crack);
            }
            return s;
        }


        // ------------------------------------------------------------------ rails, reliefs, lava

        /// <summary>
        /// Chunky tan-brown stone blocks (the rim round every green in the art direction): rounded, slightly uneven blocks in a running bond, rough pitted faces, dark grout.
        /// Tile = 1 m: eight courses of 12.5 cm, blocks about 25 cm long. Used with continuous world-space rail UVs.
        /// </summary>
        public static PbrSet StoneBlocks(int size = 512, int seed = 91)
        {
            var s = new PbrSet(size);
            float[] dark = { 0.46f, 0.30f, 0.17f }, light = { 0.84f, 0.64f, 0.40f }, grout = { 0.17f, 0.12f, 0.08f };
            const int courses = 8, perRow = 4;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                int row = (int)Math.Floor(v * courses);
                float rv = v * courses - row;
                float bu = u * perRow + (row % 2 == 0 ? 0f : 0.5f);
                int col = (int)Math.Floor(bu);
                float cu = bu - col;
                int cid = Mod(col, perRow);
                // Distance to the brick edge in metres (blocks are 25 x 12.5 cm), wobbled so the blocks are not perfect rectangles.
                float wob = (Fbm(u, v, 24, 24, seed + 4, 2) - 0.5f) * 0.006f;
                float edge = Math.Min(Math.Min(cu, 1f - cu) * 0.25f, Math.Min(rv, 1f - rv) * 0.125f) + wob;
                float round = Smooth(0.004f, 0.011f, edge);                        // 0 in the grout, 1 on the face
                float shoulderH = Smooth(0.004f, 0.026f, edge);                    // the rounded shoulder (height only)
                float tint = Hash(cid + row * 13, row, seed);
                float rough = Fbm(u, v, 40, 40, seed + 2, 4);
                float pit = Smooth(0.70f, 0.86f, Fbm(u, v, 60, 60, seed + 6, 2));
                float speck = Hash(x, y, seed + 9);
                float t = Clamp01(0.15f + tint * 0.5f + (rough - 0.5f) * 0.8f + 0.25f - pit * 0.35f + (speck - 0.5f) * 0.12f);
                int i = s.Idx(x, y);
                Mix(s, i, dark, light, t);
                float g = 1f - round;
                s.R[i] = Lerp(s.R[i], grout[0], g); s.G[i] = Lerp(s.G[i], grout[1], g); s.B[i] = Lerp(s.B[i], grout[2], g);
                s.Height[i] = Clamp01(shoulderH * 0.7f + rough * 0.28f - pit * 0.2f);
                s.Occlusion[i] = Clamp01(0.5f + 0.5f * shoulderH - pit * 0.25f);
                s.Smooth[i] = Clamp01(0.20f + rough * 0.08f - pit * 0.1f);
            }
            return s;
        }

        /// <summary>
        /// A square carved sun relief (not tileable): a raised golden disc with an inner ring, sixteen rays and a stepped frame on warm sandstone. Placed as a plaque on
        /// temple and sanctuary masonry.
        /// </summary>
        public static PbrSet SunRelief(int size = 256, int seed = 111)
        {
            var s = new PbrSet(size);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size * 2f - 1f, v = y / (float)size * 2f - 1f;
                float r = (float)Math.Sqrt(u * u + v * v), ang = (float)Math.Atan2(v, u);
                float disc = 1f - Smooth(0.27f, 0.30f, r);
                float ringIn = 1f - Smooth(0.0f, 0.03f, Math.Abs(r - 0.17f));
                float ray = 0.5f + 0.5f * (float)Math.Cos(ang * 16f);
                float rays = Smooth(0.45f, 0.75f, ray) * Smooth(0.31f, 0.34f, r) * (1f - Smooth(0.52f, 0.60f, r - ray * 0.04f));
                float frame = Math.Max(Math.Abs(u), Math.Abs(v));
                float rim = Smooth(0.82f, 0.86f, frame) * (1f - Smooth(0.92f, 0.95f, frame));
                float grain = Fbm((u + 1f) * 0.5f, (v + 1f) * 0.5f, 18, 18, seed, 4);
                float raised = Math.Max(Math.Max(disc, rays), Math.Max(rim, ringIn * 0.6f));
                float h = Clamp01(0.30f + raised * 0.5f + (grain - 0.5f) * 0.12f);
                int i = s.Idx(x, y);
                float tone = Clamp01(0.55f + raised * 0.28f + (grain - 0.5f) * 0.3f);
                Colour(s, i, 0.60f + tone * 0.38f, 0.44f + tone * 0.32f, 0.22f + tone * 0.2f);
                s.Height[i] = h;
                s.Occlusion[i] = Clamp01(0.55f + raised * 0.45f);
                s.Smooth[i] = 0.25f + raised * 0.1f;
            }
            return s;
        }

        /// <summary>
        /// Molten lava, tile = 4 m: dark cooled crust plates divided by a glowing network of orange-yellow cracks that wanders and pulses in width. The glow mask drives
        /// emission; the albedo already carries the hot colours, so the surface reads as lava even where the emission is clamped.
        /// </summary>
        public static PbrSet Lava(int size = 512, int seed = 131)
        {
            // Mostly MOLTEN surface (deep red -> orange -> yellow, flowing in streaks along v), with dark crust plates floating on it as breakup (about a fifth of the area),
            // each plate rimmed by white-hot lava. Emission follows the molten fraction, so the whole river glows and the crust reads dark against it.
            var s = new PbrSet(size) { Glow = new float[size * size] };
            float[] deep = { 0.62f, 0.07f, 0.01f }, orange = { 1.0f, 0.38f, 0.04f }, yellow = { 1.0f, 0.80f, 0.20f }, white = { 1.0f, 0.95f, 0.62f };
            float[] crust = { 0.07f, 0.03f, 0.025f }, crustHi = { 0.22f, 0.08f, 0.05f };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                // Flow: noise stretched along v (few cells across u, many along v) and domain-warped, so streaks run the same way everywhere.
                float flowA = Fbm(u, v, 3, 9, seed + 2, 4), flowB = Fbm(u, v, 5, 14, seed + 9, 3), flowC = Fbm(u, v, 2, 5, seed + 13, 3);
                float wu = u + (flowC - 0.5f) * 0.22f, wv = v + (flowB - 0.5f) * 0.10f;
                float f1, f2, id;
                Worley(wu - (float)Math.Floor(wu), wv - (float)Math.Floor(wv), 5, seed + 11, out f1, out f2, out id);
                float edge = f2 - f1;
                // Crust plates: about one cell in five, the rest is open lava; a plate fades to molten at its rim.
                bool plateCell = id < 0.30f;
                float plate = plateCell ? Smooth(0.03f, 0.14f, edge) : 0f;
                float rim = plateCell ? 1f - Smooth(0.0f, 0.07f, edge - 0.03f) : 0f;
                // Molten colour field: slow large-scale variation plus streaks.
                float heat = Clamp01(-0.12f + flowA * 0.85f + (flowB - 0.5f) * 0.55f);
                float vein = Smooth(0.58f, 0.92f, flowB) * 0.5f;
                int i = s.Idx(x, y);
                float[] c1 = deep, c2 = orange;
                float t1 = Clamp01(heat * 1.15f);
                float r = Lerp(c1[0], c2[0], t1), g = Lerp(c1[1], c2[1], t1), b = Lerp(c1[2], c2[2], t1);
                float ty = Clamp01(Math.Max(vein * 0.8f, Smooth(0.62f, 0.98f, heat)));
                r = Lerp(r, yellow[0], ty); g = Lerp(g, yellow[1], ty); b = Lerp(b, yellow[2], ty);
                float tw = Smooth(0.82f, 1.0f, ty * Clamp01(heat + 0.2f));
                r = Lerp(r, white[0], tw * 0.35f); g = Lerp(g, white[1], tw * 0.35f); b = Lerp(b, white[2], tw * 0.35f);
                // Hot rim round each crust plate, then the dark crust itself.
                r = Lerp(r, orange[0], rim * 0.8f); g = Lerp(g, orange[1] + 0.2f, rim * 0.8f); b = Lerp(b, orange[2], rim * 0.55f);
                float cp = Fbm(u, v, 14, 14, seed + 5, 4);
                float cr = Lerp(crust[0], crustHi[0], cp), cg = Lerp(crust[1], crustHi[1], cp), cb = Lerp(crust[2], crustHi[2], cp);
                r = Lerp(r, cr, plate); g = Lerp(g, cg, plate); b = Lerp(b, cb, plate);
                s.R[i] = r; s.G[i] = g; s.B[i] = b;
                s.Height[i] = Clamp01(0.45f + plate * 0.4f + flowA * 0.1f);
                s.Occlusion[i] = 1f;
                s.Smooth[i] = 0.35f + (1f - plate) * 0.25f;
                s.Glow[i] = Clamp01((1f - plate) * (0.35f + heat * 0.85f));
            }
            return s;
        }

        // ------------------------------------------------------------------ rope, metal, golf

        /// <summary>Twisted three-strand rope (tile runs along u; 1 tile = 0.25 m of rope).</summary>
        public static PbrSet Rope(int size = 256, int seed = 61)
        {
            var s = new PbrSet(size);
            float[] a = { 0.42f, 0.32f, 0.20f }, b = { 0.72f, 0.60f, 0.40f };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float phase = (u * 3f + v * 1f) * (float)Math.PI * 2f;               // diagonal strands
                float strand = 0.5f + 0.5f * (float)Math.Sin(phase * 1f);
                float fibre = Value(u * 64f, v * 16f, seed, 64, 16);
                float t = Clamp01(strand * 0.65f + fibre * 0.3f);
                int i = s.Idx(x, y);
                Mix(s, i, a, b, t);
                s.Height[i] = Clamp01(strand * 0.85f + fibre * 0.15f);
                s.Occlusion[i] = 0.55f + 0.45f * strand;
                s.Smooth[i] = 0.15f;
            }
            return s;
        }

        /// <summary>Brushed steel: very fine anisotropic lines along u, tiny roughness variation. Tile 8 cm.</summary>
        public static PbrSet BrushedMetal(int size = 256, int seed = 71)
        {
            var s = new PbrSet(size);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float line = Value(u * 4f, v * 220f, seed, 4, 220);
                float fine = Value(u * 16f, v * 110f, seed + 3, 16, 110);
                float low = Fbm(u, v, 3, 3, seed + 5, 2);
                float g = Clamp01(0.62f + (line - 0.5f) * 0.10f + (fine - 0.5f) * 0.06f + (low - 0.5f) * 0.05f);
                int i = s.Idx(x, y);
                Colour(s, i, g, g * 1.01f, g * 1.03f);
                s.Height[i] = 0.5f + (line - 0.5f) * 0.25f;
                s.Occlusion[i] = 1f;
                s.Smooth[i] = Clamp01(0.62f + (low - 0.5f) * 0.25f - (line - 0.5f) * 0.12f);
            }
            return s;
        }

        /// <summary>
        /// The cup liner as seen from above: not tileable. u runs round the wall, v runs from the rim (1, top of the texture) down to the floor (0). A bright
        /// rolled rim, a satin white-grey plastic wall with fine vertical ribs and a soft ambient-occlusion fall-off to a dark floor (the readable interior shadow).
        /// </summary>
        public static PbrSet CupLiner(int size = 256)
        {
            var s = new PbrSet(size);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;                     // v = 1 at the rim
                float depth = 1f - v;                                               // 0 at the rim, 1 at the floor
                float rib = 0.5f;                                                   // no vertical detail: the pit mesh wraps its UVs once, and any u-pattern would smear across that one segment
                float seam = 0f;
                float fall = Clamp01((depth - 0.05f) / 0.9f);
                float light = Lerp(0.88f, 0.10f, fall * fall);                      // quick fall-off with depth
                float rimBand = Smooth(0.93f, 0.985f, v) * (1f - Smooth(0.985f, 1f, v));   // bright rolled lip just below the top
                float g = Clamp01(light * (0.94f + rib * 0.06f) + rimBand * 0.12f - seam * 0.05f);
                int i = s.Idx(x, y);
                Colour(s, i, g * 0.96f, g * 0.97f, g);
                s.Height[i] = 0.5f + rib * 0.1f + rimBand * 0.3f;
                s.Occlusion[i] = Clamp01(1f - fall * 0.65f);
                s.Smooth[i] = Clamp01(0.55f - fall * 0.25f + rimBand * 0.2f);
            }
            return s;
        }

        /// <summary>
        /// Golf-ball dimples as a normal-map height field: a hex lattice of round dimples (tile covers 20 x 10 dimples). Albedo is white; the ball material tints it.
        /// Mapped on a UV sphere, so the pattern compresses toward the poles (fine for a 4 cm ball).
        /// </summary>
        public static PbrSet BallDimples(int size = 512)
        {
            var s = new PbrSet(size);
            const int cols = 20, rows = 10;
            float cw = 1f / cols, ch = 1f / rows;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float best = 1f;
                for (int dr = -1; dr <= 1; dr++)
                for (int dc = -1; dc <= 1; dc++)
                {
                    int r = (int)Math.Floor(v / ch) + dr, c = (int)Math.Floor(u / cw) + dc;
                    float cx = (c + 0.5f + ((r & 1) == 0 ? 0f : 0.5f)) * cw, cy = (r + 0.5f) * ch;
                    // wrap-around for the tile edges
                    for (int wu = -1; wu <= 1; wu += 1)
                    {
                        float dx = (u - (cx + wu)) / cw, dy = (v - cy) / ch;
                        float d = (float)Math.Sqrt(dx * dx + dy * dy);
                        if (d < best) best = d;
                    }
                }
                float dimple = 1f - Smooth(0.28f, 0.46f, best);                  // 1 inside a dimple
                int i = s.Idx(x, y);
                Colour(s, i, 0.97f, 0.97f, 0.96f);
                s.Height[i] = 1f - dimple * 0.9f;
                s.Occlusion[i] = 1f - dimple * 0.25f;
                s.Smooth[i] = 0.62f - dimple * 0.1f;
            }
            return s;
        }

        /// <summary>Woven banner cloth (tile = 12 cm weave) with a faint vertical fold gradient; dyed by the material colour.</summary>
        public static PbrSet Cloth(int size = 256, int seed = 83)
        {
            var s = new PbrSet(size);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float warp = 0.5f + 0.5f * (float)Math.Sin(u * Math.PI * 2f * 48f), weft = 0.5f + 0.5f * (float)Math.Sin(v * Math.PI * 2f * 48f);
                float weave = warp * weft * 0.5f + (1f - warp) * (1f - weft) * 0.5f;
                float noise = Fbm(u, v, 24, 24, seed, 3);
                float g = Clamp01(0.68f + weave * 0.18f + (noise - 0.5f) * 0.12f);
                int i = s.Idx(x, y);
                Colour(s, i, g, g, g);
                s.Height[i] = weave;
                s.Occlusion[i] = 0.8f + weave * 0.2f;
                s.Smooth[i] = 0.12f;
            }
            return s;
        }
    }
}
