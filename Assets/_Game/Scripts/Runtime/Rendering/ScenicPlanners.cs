using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>One planned plant: where, which species, how big, and which layer of the planting it belongs to.</summary>
    public struct ScatterItem
    {
        public Vector2 position;
        public string species;
        public int layer;          // 0 canopy, 1 understory, 2 ground cover, 3 accent (flowers)
        public float scale, yaw;
    }

    /// <summary>How a biome is planted: which species make up each layer and how the clumps are shaped.</summary>
    public class ScatterRecipe
    {
        public string[] canopy = new string[0], understory = new string[0], ground = new string[0], accents = new string[0];
        public int clusters = 12;
        public Vector2 clusterRadius = new Vector2(1.6f, 3.4f);
        public Vector2Int itemsPerCluster = new Vector2Int(8, 16);
        /// <summary>No two clumps closer than this (centre to centre): keeps negative space between them.</summary>
        public float minSeparation = 3.2f;
        /// <summary>Fraction (0..1) of the area the noise gate leaves unplanted, on top of the separation.</summary>
        public float emptiness = 0.30f;
        /// <summary>Chance that a mid-layer slot becomes a flower accent.</summary>
        public float accentChance = 0.14f;
        public float canopyChance = 0.40f;
    }

    /// <summary>
    /// Plans layered, clumped, uneven planting for a biome (Graphics Pass 3): clusters of plants with a canopy core, an understory ring and ground cover at the edge, a
    /// few dominant species per clump (so neighbours are not copies of each other), scale and rotation variation, and real gaps between clumps. Pure and deterministic:
    /// the editor turns the result into objects, tests check it. <paramref name="allowed"/> is the caller's keep-out (greens, standing places, shot corridors).
    /// </summary>
    public static class ScatterPlanner
    {
        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        /// <summary>Smooth value noise in [0,1] with a feature size of roughly <paramref name="featureSize"/> metres.</summary>
        public static float Noise(Vector2 p, float featureSize, int seed)
        {
            float x = p.x / featureSize, y = p.y / featureSize;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            float a = Hash(x0, y0, seed), b = Hash(x0 + 1, y0, seed), c = Hash(x0, y0 + 1, seed), d = Hash(x0 + 1, y0 + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        public static List<ScatterItem> Plan(int seed, Vector2 centre, float radius, ScatterRecipe r, Func<Vector2, bool> allowed)
        {
            var rnd = new System.Random(seed);
            float Rf(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var items = new List<ScatterItem>();
            var centres = new List<Vector2>();
            int attempts = r.clusters * 14;
            for (int i = 0; i < attempts && centres.Count < r.clusters; i++)
            {
                float a = Rf(0f, Mathf.PI * 2f), d = Mathf.Sqrt((float)rnd.NextDouble()) * radius;
                var p = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                if (allowed != null && !allowed(p)) continue;
                if (Noise(p, 9f, seed + 5) < r.emptiness) continue;                 // negative space: whole stretches of ground stay bare
                bool tooClose = false;
                foreach (var c in centres) if ((c - p).sqrMagnitude < r.minSeparation * r.minSeparation) { tooClose = true; break; }
                if (tooClose) continue;
                centres.Add(p);
            }

            string Pick(string[] set, string[] dominant)
            {
                if (set.Length == 0) return null;
                if (dominant != null && dominant.Length > 0 && rnd.NextDouble() < 0.7) return dominant[rnd.Next(dominant.Length)];
                return set[rnd.Next(set.Length)];
            }
            string[] Dominant(string[] set)
            {
                if (set.Length <= 2) return set;
                int n = 2 + rnd.Next(2);
                var d = new string[Math.Min(n, set.Length)];
                for (int k = 0; k < d.Length; k++) d[k] = set[rnd.Next(set.Length)];
                return d;
            }

            foreach (var c in centres)
            {
                float cr = Rf(r.clusterRadius.x, r.clusterRadius.y);
                int count = rnd.Next(r.itemsPerCluster.x, r.itemsPerCluster.y + 1);
                var domCanopy = Dominant(r.canopy); var domUnder = Dominant(r.understory); var domGround = Dominant(r.ground); var domAccent = Dominant(r.accents);
                for (int k = 0; k < count; k++)
                {
                    float a = Rf(0f, Mathf.PI * 2f), t = Mathf.Pow((float)rnd.NextDouble(), 0.75f);
                    var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (cr * t);
                    if (allowed != null && !allowed(p)) continue;
                    int layer; string species;
                    if (t < 0.35f && r.canopy.Length > 0 && rnd.NextDouble() < r.canopyChance) { layer = 0; species = Pick(r.canopy, domCanopy); }
                    else if (t < 0.8f)
                    {
                        if (r.accents.Length > 0 && rnd.NextDouble() < r.accentChance) { layer = 3; species = Pick(r.accents, domAccent); }
                        else { layer = 1; species = Pick(r.understory, domUnder); }
                    }
                    else { layer = 2; species = Pick(r.ground, domGround); }
                    if (species == null) continue;
                    float scale = layer == 0 ? Rf(0.85f, 1.25f) : layer == 1 ? Rf(0.7f, 1.45f) : layer == 2 ? Rf(0.6f, 1.25f) : Rf(0.8f, 1.3f);
                    items.Add(new ScatterItem { position = p, species = species, layer = layer, scale = scale, yaw = Rf(0f, 360f) });
                }
            }
            return items;
        }
    }

    /// <summary>
    /// Plans rope-and-post fences along the outside of a green's rails (Graphics Pass 3): posts at corners and every few metres along long straight runs, a little outside
    /// the rail, never inside the green. Pure and deterministic. <paramref name="allowed"/> keeps posts out of the putting, standing and teleport places.
    /// </summary>
    public static class FencePlanner
    {
        public struct Post { public Vector2 position, outward; public bool corner; public int run; }

        public static List<Post> Posts(GreenLayout l, float outset = 0.3f, float spacing = 2.4f, float minRun = 1.4f, Func<Vector2, bool> allowed = null)
        {
            var posts = new List<Post>();
            var rects = l.areas;
            bool Covered(Vector2 p) { foreach (var r in rects) if (p.x > r.xMin + 1e-4f && p.x < r.xMax - 1e-4f && p.y > r.yMin + 1e-4f && p.y < r.yMax - 1e-4f) return true; return false; }
            int run = 0;
            float step = l.cell;
            foreach (var r in rects)
            {
                // Four sides: (start, direction, outward normal, length).
                var sides = new[]
                {
                    (new Vector2(r.xMin, r.yMin), Vector2.right, Vector2.down, r.width),
                    (new Vector2(r.xMin, r.yMax), Vector2.right, Vector2.up, r.width),
                    (new Vector2(r.xMin, r.yMin), Vector2.up, Vector2.left, r.height),
                    (new Vector2(r.xMax, r.yMin), Vector2.up, Vector2.right, r.height),
                };
                foreach (var (origin, dir, normal, length) in sides)
                {
                    int n = Mathf.Max(1, Mathf.RoundToInt(length / step));
                    float runStart = -1f;
                    for (int i = 0; i <= n; i++)
                    {
                        float s = Mathf.Min(length, i * step);
                        Vector2 mid = origin + dir * Mathf.Min(length, (i + 0.5f) * step) + normal * 0.03f;
                        bool open = i < n && !Covered(mid);
                        if (open && runStart < 0f) runStart = i * step;
                        if ((!open || i == n) && runStart >= 0f)
                        {
                            float runEnd = s;
                            if (runEnd - runStart >= minRun) AddRun(posts, origin, dir, normal, runStart, runEnd, outset, spacing, ref run);
                            runStart = -1f;
                        }
                    }
                }
            }
            if (allowed != null) posts.RemoveAll(p => !allowed(p.position));
            // Two sides meeting at a corner each plant a post there: keep one of any pair closer than 35 cm.
            for (int i = posts.Count - 1; i >= 0; i--)
                for (int j = 0; j < i; j++)
                    if ((posts[i].position - posts[j].position).sqrMagnitude < 0.35f * 0.35f) { posts.RemoveAt(i); break; }
            return posts;
        }

        static void AddRun(List<Post> posts, Vector2 origin, Vector2 dir, Vector2 normal, float s0, float s1, float outset, float spacing, ref int run)
        {
            float len = s1 - s0;
            int segments = Mathf.Max(1, Mathf.RoundToInt(len / spacing));
            for (int k = 0; k <= segments; k++)
            {
                float s = Mathf.Lerp(s0, s1, k / (float)segments);
                posts.Add(new Post { position = origin + dir * s + normal * outset, outward = normal, corner = k == 0 || k == segments, run = run });
            }
            run++;
        }
    }
}
