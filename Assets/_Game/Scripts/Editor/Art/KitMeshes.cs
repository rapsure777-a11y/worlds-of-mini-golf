using System.Collections.Generic;
using UnityEngine;
using R = Gamebreak.MiniGolf.Editor.Art.Palette.Row;

namespace Gamebreak.MiniGolf.Editor.Art
{
    /// <summary>
    /// Generators for the reusable Tropical kit. All meshes are original, deterministic (seeded) and
    /// coloured through the shared palette. Vertex alpha = wind weight (0 rigid .. 1 free).
    /// Units are metres; pivots sit on the ground at the base of the object.
    /// </summary>
    public static class KitMeshes
    {
        static Color W(float wind) => new Color(1f, 1f, 1f, wind);
        static Color Shade(float s, float wind = 0f) => new Color(s, s, s, wind);

        // ---------------------------------------------------------------- vegetation

        /// <summary>Stylised coconut palm: segmented banded trunk, drooping serrated fronds, coconuts.</summary>
        public static (Mesh trunk, Mesh crown) Palm(int seed, float height)
        {
            var rnd = new System.Random(seed);
            float Rf(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

            // Trunk: gentle lean with an S-curve, chunky segment rings.
            float lean = Rf(0.25f, 0.45f) * height, wobble = Rf(-0.12f, 0.12f) * height;
            int rings = 30, segments = Mathf.RoundToInt(height * 3.2f);
            var path = new List<Vector3>();
            for (int i = 0; i < rings; i++)
            {
                float t = i / (float)(rings - 1);
                path.Add(new Vector3(lean * Mathf.Pow(t, 1.8f), t * height, wobble * Mathf.Sin(t * Mathf.PI)));
            }
            var tb = new MeshBuilder();
            tb.Tube(path, 9,
                t => Mathf.Lerp(0.2f, 0.11f, t) * (t < 0.06f ? 1.35f - t * 5f : 1f),
                (t, a) => Palette.UV(R.Trunk, 0.2f + 0.65f * Mathf.Repeat(t * segments, 1f)),
                t => W(t * t * 0.25f),
                capEnd: true,
                radiusJitter: (t, a) => 1f + 0.16f * Mathf.Pow(Mathf.Abs(Mathf.Sin(t * segments * Mathf.PI)), 4f));

            // Crown.
            var cb = new MeshBuilder();
            Vector3 top = path[rings - 1];
            int fronds = rnd.Next(8, 11);
            float yaw0 = Rf(0f, 360f);
            for (int f = 0; f < fronds; f++)
            {
                float yaw = yaw0 + f * 360f / fronds + Rf(-12f, 12f);
                float len = Rf(1.9f, 2.6f) * Mathf.Clamp(height / 4f, 0.7f, 1.3f);
                float rise = Rf(0.25f, 0.55f);
                float droop = Rf(0.9f, 1.4f);
                var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                var spine = new List<Vector3>();
                for (int i = 0; i <= 18; i++)
                {
                    float t = i / 18f;
                    float y = rise * t * len - droop * t * t * len * 0.75f;
                    spine.Add(top + dir * (t * len) + Vector3.up * y);
                }
                float shade = f % 2 == 0 ? 0f : 0.12f;
                cb.Ribbon(spine, Vector3.Cross(Vector3.up, dir),
                    t => 0.62f * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * 1.05f + 0.02f)), 0.7f) * (0.72f + 0.28f * Mathf.Abs(Mathf.Sin(t * 34f))),
                    t => Palette.UV(R.Leaf, 0.35f + shade + 0.45f * t),
                    t => W(0.25f + 0.75f * t),
                    t => 0.55f);
            }
            // Crown bulb and coconuts.
            cb.With(Matrix4x4.TRS(top + Vector3.down * 0.05f, Quaternion.identity, Vector3.one * 0.22f), () =>
                cb.Blob(1, d => 1f, (p, n) => (Palette.UV(R.Trunk, 0.55f), W(0.15f))));
            int nuts = rnd.Next(2, 5);
            for (int i = 0; i < nuts; i++)
            {
                var o = Quaternion.Euler(0f, yaw0 + i * 360f / nuts + Rf(-20f, 20f), 0f) * new Vector3(0f, 0f, 0.17f);
                cb.With(Matrix4x4.TRS(top + o + Vector3.down * 0.2f, Quaternion.identity, Vector3.one * 0.11f), () =>
                    cb.Blob(1, d => 1f, (p, n) => (Palette.UV(R.Fruit, 0.25f + 0.2f * (p.y + 1f)), W(0.15f))));
            }
            return (tb.ToMesh($"PalmTrunk_{seed}"), cb.ToMesh($"PalmCrown_{seed}"));
        }

        /// <summary>Rounded leafy bush made of a few soft lumps.</summary>
        public static Mesh Bush(int seed, float size, R leafRow = R.Leaf)
        {
            var rnd = new System.Random(seed);
            float Rf(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var mb = new MeshBuilder();
            int lumps = rnd.Next(4, 7);
            for (int i = 0; i < lumps; i++)
            {
                float ang = i / (float)lumps * Mathf.PI * 2f + Rf(-0.3f, 0.3f);
                float r = i == 0 ? 0f : Rf(0.25f, 0.45f) * size;
                float s = (i == 0 ? 0.62f : Rf(0.38f, 0.5f)) * size;
                var c = new Vector3(Mathf.Cos(ang) * r, s * 0.75f, Mathf.Sin(ang) * r);
                int lumpSeed = seed * 13 + i;
                mb.With(Matrix4x4.TRS(c, Quaternion.identity, new Vector3(s, s * 0.85f, s)), () =>
                    mb.Blob(2, d => 1f + 0.18f * (Noise.Value3(d * 2.2f, lumpSeed) - 0.5f) * 2f,
                        (p, n) => (Palette.UV(leafRow, Mathf.Clamp01(0.2f + 0.65f * (n.y * 0.5f + 0.5f) + 0.15f * (Noise.Value3(p * 3f, lumpSeed) - 0.5f))),
                                   W(0.12f * Mathf.Clamp01(p.y + 0.5f)))));
            }
            return mb.ToMesh($"Bush_{seed}");
        }

        /// <summary>Big-leafed jungle plant (elephant-ear / monstera feel).</summary>
        public static Mesh BigLeafPlant(int seed, float size)
        {
            var rnd = new System.Random(seed);
            float Rf(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var mb = new MeshBuilder();
            int leaves = rnd.Next(6, 10);
            for (int i = 0; i < leaves; i++)
            {
                float yaw = i * 360f / leaves + Rf(-15f, 15f);
                var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                float len = Rf(0.6f, 1.0f) * size, rise = Rf(0.9f, 1.4f), droop = Rf(0.9f, 1.5f);
                var spine = new List<Vector3>();
                for (int k = 0; k <= 10; k++)
                {
                    float t = k / 10f;
                    spine.Add(dir * (t * len * 0.9f) + Vector3.up * (rise * t * len - droop * t * t * len * 0.8f + 0.05f));
                }
                bool dark = rnd.NextDouble() < 0.5;
                mb.Ribbon(spine, Vector3.Cross(Vector3.up, dir),
                    t => 0.55f * len * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Clamp01(t * 0.95f + 0.03f)), 0.6f),
                    t => Palette.UV(dark ? R.Jungle : R.Leaf, 0.35f + 0.5f * t),
                    t => W(0.2f + 0.8f * t),
                    t => 0.25f);
            }
            return mb.ToMesh($"BigLeaf_{seed}");
        }

        /// <summary>Grass tuft of curved blades.</summary>
        public static Mesh GrassTuft(int seed, float height)
        {
            var rnd = new System.Random(seed);
            float Rf(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var mb = new MeshBuilder();
            int blades = rnd.Next(7, 12);
            for (int i = 0; i < blades; i++)
            {
                float yaw = Rf(0f, 360f);
                var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                var basePos = new Vector3(Rf(-0.06f, 0.06f), 0f, Rf(-0.06f, 0.06f));
                float h = height * Rf(0.6f, 1.1f), bend = Rf(0.2f, 0.5f);
                var spine = new List<Vector3>();
                for (int k = 0; k <= 4; k++)
                {
                    float t = k / 4f;
                    spine.Add(basePos + dir * (bend * t * t * h) + Vector3.up * (t * h));
                }
                mb.Ribbon(spine, Vector3.Cross(Vector3.up, dir), t => 0.035f * (1f - t * 0.9f),
                    t => Palette.UV(R.Grass, 0.3f + 0.65f * t), t => W(t), null);
            }
            return mb.ToMesh($"Grass_{seed}");
        }

        /// <summary>Flowering shrub: leafy lumps dotted with blossoms of one colour.</summary>
        public static Mesh FlowerBush(int seed, float size, Palette.Flower flower)
        {
            var rnd = new System.Random(seed);
            float Rf(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var mb = new MeshBuilder();
            var bushMesh = Bush(seed, size);
            Append(mb, bushMesh, Matrix4x4.identity);
            Object.DestroyImmediate(bushMesh);
            int blossoms = rnd.Next(9, 15);
            for (int i = 0; i < blossoms; i++)
            {
                // On the outside of the foliage (the bush's lumps reach ~0.9 x size from its centre).
                var d = new Vector3(Rf(-1f, 1f), Rf(0.15f, 1f), Rf(-1f, 1f)).normalized;
                var pos = new Vector3(0f, 0.45f * size, 0f) + d * (0.82f * size);
                var rot = Quaternion.LookRotation(d) * Quaternion.Euler(90f, 0f, 0f);
                mb.With(Matrix4x4.TRS(pos, rot, Vector3.one * 0.13f * size), () =>
                {
                    for (int p = 0; p < 5; p++)
                    {
                        var pd = Quaternion.Euler(0f, p * 72f, 0f) * Vector3.forward;
                        var spine = new List<Vector3> { Vector3.zero, pd * 0.6f + Vector3.up * 0.2f, pd * 1.1f + Vector3.up * 0.35f };
                        mb.Ribbon(spine, Vector3.Cross(Vector3.up, pd), t => 0.9f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t * 0.9f + 0.1f)),
                            t => Palette.FlowerUV(flower), t => W(0.3f), null);
                    }
                    mb.With(Matrix4x4.TRS(Vector3.up * 0.1f, Quaternion.identity, Vector3.one * 0.18f), () =>
                        mb.Blob(0, x => 1f, (q, n) => (Palette.FlowerUV(Palette.Flower.Yellow), W(0.3f))));
                });
            }
            return mb.ToMesh($"FlowerBush_{seed}_{flower}");
        }

        // ---------------------------------------------------------------- rocks

        /// <summary>Chunky faceted boulder in warm sandstone (or grey), optional grassy top.</summary>
        public static Mesh Rock(int seed, Vector3 scale, bool grey = false, bool grassTop = false)
        {
            var mb = new MeshBuilder();
            R row = grey ? R.RockGrey : R.RockWarm;
            // Small pebbles stay faceted; larger rocks are smooth, chunky and banded, and a grassy cap
            // blends into warm stone (neighbouring palette rows). Grey stone keeps a hard cap edge.
            bool small = scale.magnitude < 0.8f;
            bool smoothCap = grassTop && !grey;
            mb.With(Matrix4x4.Scale(scale), () =>
                mb.Blob(small ? 2 : 3,
                    d => (1f + 0.36f * (Noise.Value3(d * 1.2f + Vector3.one * seed, seed) - 0.5f) * 2f
                             + 0.08f * (Noise.Value3(d * 3.5f, seed + 3) - 0.5f) * 2f) * (d.y < -0.2f ? 0.75f : 1f),
                    (p, n) =>
                    {
                        if (grassTop && n.y > 0.7f && p.y > 0.1f)
                            return (Palette.UV(R.Grass, 0.4f + 0.45f * Noise.Value3(p * 3f, seed + 1)), W(0f));
                        float band = Mathf.Repeat(p.y * 1.6f + Noise.Value3(p * 1.5f, seed) * 0.35f, 1f);
                        float light = Mathf.Clamp01(0.3f + 0.4f * (band < 0.5f ? 0.2f : 0.8f) + 0.25f * (n.y * 0.5f + 0.5f));
                        return (Palette.UV(row, light), W(0f));
                    },
                    faceted: small || (grassTop && !smoothCap),
                    warp: v => new Vector3(v.x, Mathf.Max(v.y, -0.55f), v.z)));
            return mb.ToMesh($"Rock_{seed}{(grey ? "_grey" : "")}{(grassTop ? "_grass" : "")}");
        }

        /// <summary>Tall layered sandstone formation: stacked chunky tiers, strata bands, grassy cap.</summary>
        public static Mesh CliffStack(int seed, float height, float width)
        {
            var rnd = new System.Random(seed);
            float Rf(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var mb = new MeshBuilder();
            int tiers = Mathf.Max(2, Mathf.RoundToInt(height / 1.4f));
            float y = 0f;
            Vector3 drift = Vector3.zero;
            for (int i = 0; i < tiers; i++)
            {
                float t = i / (float)(tiers - 1);
                float th = height / tiers * Rf(1.15f, 1.45f);
                float w = width * Mathf.Lerp(1f, 0.62f, t) * Rf(0.9f, 1.1f);
                drift += new Vector3(Rf(-0.15f, 0.15f), 0f, Rf(-0.15f, 0.15f)) * width;
                int s = seed * 7 + i;
                bool cap = i == tiers - 1;
                mb.With(Matrix4x4.TRS(drift + Vector3.up * (y + th * 0.45f), Quaternion.Euler(0f, Rf(0f, 360f), 0f), new Vector3(w * 0.5f, th * 0.62f, w * 0.5f * Rf(0.8f, 1.1f))), () =>
                    mb.Blob(3,
                        d => (1f + 0.28f * (Noise.Value3(d * 1.3f + Vector3.one * s, s) - 0.5f) * 2f
                                 + 0.07f * (Noise.Value3(d * 4f, s + 5) - 0.5f) * 2f),
                        (p, n) =>
                        {
                            // Grassy top tier blends into the stone; tiers alternate light/dark strata.
                            if (cap && n.y > 0.55f && p.y > 0.2f) return (Palette.UV(R.Grass, 0.4f + 0.45f * Noise.Value3(p * 3f, s)), W(0f));
                            float strata = (i % 2 == 0 ? 0.62f : 0.42f) + 0.12f * Mathf.Sign(Mathf.Sin((p.y + 1f) * 5f));
                            float light = Mathf.Clamp01(strata + 0.2f * (n.y * 0.5f + 0.5f) - 0.1f);
                            return (Palette.UV(R.RockWarm, light), W(0f));
                        },
                        faceted: false,
                        warp: v => new Vector3(v.x * (1f + 0.25f * Mathf.Clamp01(v.y)), v.y, v.z * (1f + 0.25f * Mathf.Clamp01(v.y)))));
                y += th * 0.82f;
            }
            return mb.ToMesh($"Cliff_{seed}");
        }

        // ---------------------------------------------------------------- structures

        /// <summary>Tiki hut on stilts with a layered thatch roof. Footprint ~3 x 3 m.</summary>
        public static Mesh TikiHut(int seed)
        {
            var mb = new MeshBuilder();
            Color solid = W(0f);
            // Stilts and deck.
            foreach (var c in new[] { new Vector2(-1.2f, -1.2f), new Vector2(1.2f, -1.2f), new Vector2(-1.2f, 1.2f), new Vector2(1.2f, 1.2f) })
            {
                mb.With(Matrix4x4.Translate(new Vector3(c.x, 0f, c.y)), () =>
                    mb.Lathe(new[] { new Vector2(0.11f, 0f), new Vector2(0.09f, 1.2f), new Vector2(0.09f, 2.6f), new Vector2(0.0f, 2.62f) }, 7,
                        t => Palette.UV(R.Trunk, 0.35f + 0.3f * t), t => solid));
            }
            for (int i = 0; i < 9; i++)
            {
                float z = -1.35f + i * 0.34f;
                mb.Box(new Vector3(0f, 0.55f, z), new Vector3(3.0f, 0.07f, 0.3f), Palette.UV(R.Wood, i % 2 == 0 ? 0.55f : 0.7f), solid);
            }
            // Bar counter on the front.
            mb.Box(new Vector3(0f, 1.05f, -1.35f), new Vector3(2.6f, 0.08f, 0.35f), Palette.UV(R.Wood, 0.8f), solid);
            for (int i = 0; i < 6; i++)
                mb.Box(new Vector3(-1.1f + i * 0.44f, 0.8f, -1.4f), new Vector3(0.1f, 0.45f, 0.1f), Palette.UV(R.Trunk, 0.45f), solid);
            // Roof: three stacked cones with ragged eaves.
            var rnd = new System.Random(seed);
            float[] radii = { 2.5f, 1.9f, 1.25f };
            float[] heights = { 2.35f, 2.85f, 3.3f };
            for (int i = 0; i < 3; i++)
            {
                float r = radii[i], h = heights[i];
                int s = seed + i;
                mb.Lathe(new[] { new Vector2(r, h), new Vector2(r * 0.55f, h + 0.55f), new Vector2(0.02f, h + 1.0f - i * 0.12f) }, 18,
                    t => Palette.UV(R.Thatch, 0.35f + 0.45f * t + i * 0.05f), t => W(0.05f * t),
                    (t, a) => t < 0.01f ? 1f + 0.08f * Mathf.Sin(a * 9f + s) + 0.06f * Mathf.Sin(a * 23f) : 1f);
                // Underside so the roof isn't see-through from below.
                mb.Lathe(new[] { new Vector2(0.02f, h + 0.3f), new Vector2(r * 0.98f, h + 0.01f) }, 18,
                    t => Palette.UV(R.Thatch, 0.25f), t => solid);
            }
            return mb.ToMesh($"TikiHut_{seed}");
        }

        /// <summary>Straight boardwalk section along +Z with posts and rope rails.</summary>
        public static Mesh Boardwalk(int seed, float length, float width = 1.4f)
        {
            var rnd = new System.Random(seed);
            var mb = new MeshBuilder();
            Color solid = W(0f);
            int planks = Mathf.Max(1, Mathf.RoundToInt(length / 0.22f));
            for (int i = 0; i < planks; i++)
            {
                float z = (i + 0.5f) * length / planks;
                float tilt = ((float)rnd.NextDouble() - 0.5f) * 2f;
                mb.With(Matrix4x4.TRS(new Vector3(0f, 0.32f, z), Quaternion.Euler(tilt, ((float)rnd.NextDouble() - 0.5f) * 3f, 0f), Vector3.one), () =>
                    mb.Box(Vector3.zero, new Vector3(width, 0.06f, length / planks - 0.025f), Palette.UV(R.Wood, 0.5f + (float)rnd.NextDouble() * 0.35f), solid));
            }
            foreach (float x in new[] { -width * 0.4f, width * 0.4f })
                mb.Box(new Vector3(x, 0.22f, length * 0.5f), new Vector3(0.12f, 0.14f, length), Palette.UV(R.Wood, 0.3f), solid);
            int posts = Mathf.Max(2, Mathf.RoundToInt(length / 1.6f) + 1);
            var tops = new List<Vector3>[2] { new List<Vector3>(), new List<Vector3>() };
            for (int i = 0; i < posts; i++)
            {
                float z = i * length / (posts - 1);
                for (int side = 0; side < 2; side++)
                {
                    float x = (side == 0 ? -1f : 1f) * (width * 0.5f + 0.02f);
                    mb.With(Matrix4x4.Translate(new Vector3(x, 0f, z)), () =>
                        mb.Lathe(new[] { new Vector2(0.075f, -0.3f), new Vector2(0.07f, 0.95f), new Vector2(0.085f, 1.0f), new Vector2(0f, 1.05f) }, 7,
                            t => Palette.UV(R.Trunk, 0.4f + 0.3f * t), t => solid));
                    tops[side].Add(new Vector3(x, 0.92f, z));
                }
            }
            for (int side = 0; side < 2; side++)
                for (int i = 0; i < posts - 1; i++)
                {
                    var a = tops[side][i]; var b = tops[side][i + 1];
                    var rope = new List<Vector3>();
                    for (int k = 0; k <= 8; k++) { float t = k / 8f; rope.Add(Vector3.Lerp(a, b, t) + Vector3.down * 0.12f * Mathf.Sin(Mathf.PI * t)); }
                    mb.Tube(rope, 5, t => 0.022f, (t, u) => Palette.UV(R.Thatch, 0.6f), t => W(0.05f), capEnd: false);
                }
            return mb.ToMesh($"Boardwalk_{seed}_{length:F1}");
        }

        /// <summary>Bamboo tiki torch (pole + bowl). The flame is a separate emissive mesh.</summary>
        public static (Mesh torch, Mesh flame) TikiTorch(int seed)
        {
            var mb = new MeshBuilder();
            var profile = new List<Vector2>();
            for (int i = 0; i <= 12; i++)
            {
                float y = i / 12f * 1.55f;
                profile.Add(new Vector2(i % 3 == 0 ? 0.045f : 0.038f, y));
            }
            mb.Lathe(profile, 7, t => Palette.UV(R.Thatch, 0.45f + 0.35f * Mathf.Repeat(t * 4f, 1f)), t => W(0f));
            mb.Lathe(new[] { new Vector2(0.05f, 1.5f), new Vector2(0.13f, 1.62f), new Vector2(0.15f, 1.75f), new Vector2(0.11f, 1.76f), new Vector2(0.0f, 1.7f) }, 10,
                t => Palette.UV(R.Wood, 0.3f), t => W(0f));
            // Flame: a tall central tongue and two smaller ones, orange at the base to yellow at the tips.
            var fb = new MeshBuilder();
            var tongues = new[] { (new Vector3(0f, 1.8f, 0f), 1f), (new Vector3(0.045f, 1.78f, 0.02f), 0.65f), (new Vector3(-0.04f, 1.78f, -0.03f), 0.7f) };
            foreach (var (pos, k) in tongues)
            {
                fb.With(Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(0.055f, 0.16f, 0.055f) * k), () =>
                    fb.Blob(2, d => 1f, (p, n) =>
                        (Palette.SolidBlend(R.Flowers, 2f + Mathf.Clamp01(p.y * 0.5f + 0.5f)), W(0.35f + 0.65f * Mathf.Clamp01(p.y))),
                        warp: v =>
                        {
                            // Teardrop: narrow to a point at the top, rounded bottom.
                            float taper = v.y > 0f ? Mathf.Lerp(1f, 0.05f, Mathf.Pow(v.y, 0.8f)) : 1f;
                            return new Vector3(v.x * taper, v.y > 0f ? v.y * 1.4f : v.y * 0.6f, v.z * taper);
                        }));
            }
            return (mb.ToMesh($"TikiTorch_{seed}"), fb.ToMesh($"TikiFlame_{seed}"));
        }

        public static Mesh Crate(int seed, float size)
        {
            var mb = new MeshBuilder();
            Color solid = W(0f);
            float s = size;
            mb.Box(new Vector3(0f, s * 0.5f, 0f), Vector3.one * s * 0.94f, Palette.UV(R.Wood, 0.62f), solid);
            // Frame battens on each vertical edge and around top/bottom.
            float b = s * 0.12f;
            foreach (var x in new[] { -1f, 1f })
            foreach (var z in new[] { -1f, 1f })
                mb.Box(new Vector3(x * (s * 0.5f - b * 0.4f), s * 0.5f, z * (s * 0.5f - b * 0.4f)), new Vector3(b, s, b), Palette.UV(R.Wood, 0.38f), solid);
            foreach (var y in new[] { b * 0.5f, s - b * 0.5f })
            {
                mb.Box(new Vector3(0f, y, s * 0.49f), new Vector3(s, b, b * 0.6f), Palette.UV(R.Wood, 0.38f), solid);
                mb.Box(new Vector3(0f, y, -s * 0.49f), new Vector3(s, b, b * 0.6f), Palette.UV(R.Wood, 0.38f), solid);
                mb.Box(new Vector3(s * 0.49f, y, 0f), new Vector3(b * 0.6f, b, s), Palette.UV(R.Wood, 0.38f), solid);
                mb.Box(new Vector3(-s * 0.49f, y, 0f), new Vector3(b * 0.6f, b, s), Palette.UV(R.Wood, 0.38f), solid);
            }
            return mb.ToMesh($"Crate_{seed}");
        }

        public static Mesh Barrel(int seed)
        {
            var mb = new MeshBuilder();
            var profile = new List<Vector2>();
            for (int i = 0; i <= 10; i++)
            {
                float t = i / 10f;
                profile.Add(new Vector2(0.27f + 0.06f * Mathf.Sin(Mathf.PI * t), t * 0.85f));
            }
            profile.Add(new Vector2(0f, 0.85f));
            mb.Lathe(profile, 14, t => Palette.UV(R.Wood, (t > 0.12f && t < 0.2f) || (t > 0.78f && t < 0.86f) ? 0.12f : 0.6f), t => W(0f),
                (t, a) => 1f + 0.015f * Mathf.Sign(Mathf.Sin(a * 14f)));
            return mb.ToMesh($"Barrel_{seed}");
        }

        /// <summary>Wooden signboard on two posts; text is added in the scene as a world-space canvas.</summary>
        public static Mesh Signboard(float width, float height, float postHeight)
        {
            var mb = new MeshBuilder();
            Color solid = W(0f);
            foreach (var x in new[] { -width * 0.42f, width * 0.42f })
                mb.With(Matrix4x4.Translate(new Vector3(x, 0f, 0.04f)), () =>
                    mb.Lathe(new[] { new Vector2(0.06f, 0f), new Vector2(0.055f, postHeight + height), new Vector2(0f, postHeight + height + 0.06f) }, 7,
                        t => Palette.UV(R.Trunk, 0.45f), t => solid));
            int planks = Mathf.Max(2, Mathf.RoundToInt(height / 0.16f));
            for (int i = 0; i < planks; i++)
            {
                float y = postHeight + (i + 0.5f) * height / planks;
                mb.Box(new Vector3(0f, y, 0f), new Vector3(width, height / planks - 0.012f, 0.05f), Palette.UV(R.Wood, i % 2 == 0 ? 0.55f : 0.65f), solid);
            }
            return mb.ToMesh($"Signboard_{width:F1}x{height:F1}");
        }

        /// <summary>Low rope fence segment along +Z (posts every ~1.2 m).</summary>
        public static Mesh RopeFence(int seed, float length)
        {
            var mb = new MeshBuilder();
            int posts = Mathf.Max(2, Mathf.RoundToInt(length / 1.2f) + 1);
            var tops = new List<Vector3>();
            for (int i = 0; i < posts; i++)
            {
                float z = i * length / (posts - 1);
                mb.With(Matrix4x4.Translate(new Vector3(0f, 0f, z)), () =>
                    mb.Lathe(new[] { new Vector2(0.06f, -0.1f), new Vector2(0.055f, 0.55f), new Vector2(0.07f, 0.6f), new Vector2(0f, 0.66f) }, 7,
                        t => Palette.UV(R.Trunk, 0.45f), t => W(0f)));
                tops.Add(new Vector3(0f, 0.5f, z));
            }
            for (int i = 0; i < posts - 1; i++)
            {
                var rope = new List<Vector3>();
                for (int k = 0; k <= 8; k++) { float t = k / 8f; rope.Add(Vector3.Lerp(tops[i], tops[i + 1], t) + Vector3.down * 0.1f * Mathf.Sin(Mathf.PI * t)); }
                mb.Tube(rope, 5, t => 0.02f, (t, u) => Palette.UV(R.Thatch, 0.6f), t => W(0.05f), capEnd: false);
            }
            return mb.ToMesh($"RopeFence_{seed}_{length:F1}");
        }

        // ---------------------------------------------------------------- sky

        public static Mesh Cloud(int seed, float size)
        {
            var rnd = new System.Random(seed);
            float Rf(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var mb = new MeshBuilder();
            int puffs = rnd.Next(5, 9);
            for (int i = 0; i < puffs; i++)
            {
                float x = (i / (float)(puffs - 1) - 0.5f) * size * 1.6f + Rf(-0.1f, 0.1f) * size;
                float r = size * Rf(0.3f, 0.5f) * (1f - Mathf.Abs(x / size) * 0.6f);
                var c = new Vector3(x, r * 0.35f, Rf(-0.2f, 0.2f) * size);
                mb.With(Matrix4x4.TRS(c, Quaternion.identity, new Vector3(r, r * 0.8f, r)), () =>
                    mb.Blob(1, d => 1f, (p, n) => (Palette.UV(R.Cloud, 0.35f + 0.65f * Mathf.Clamp01(n.y * 0.5f + 0.6f)), W(0f)),
                        warp: v => new Vector3(v.x, Mathf.Max(v.y, -0.35f), v.z)));
            }
            return mb.ToMesh($"Cloud_{seed}");
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>Copy an existing mesh into a builder under a transform.</summary>
        public static void Append(MeshBuilder mb, Mesh mesh, Matrix4x4 xf)
        {
            var v = mesh.vertices; var n = mesh.normals; var uv = mesh.uv; var c = mesh.colors; var t = mesh.triangles;
            int[] map = new int[v.Length];
            mb.With(xf, () =>
            {
                for (int i = 0; i < v.Length; i++) map[i] = mb.Vertex(v[i], n[i], uv[i], c.Length > 0 ? c[i] : Color.white);
            });
            for (int i = 0; i < t.Length; i += 3) mb.Triangle(map[t[i]], map[t[i + 1]], map[t[i + 2]]);
        }
    }
}
