using System;
using System.Collections.Generic;
using System.Linq;
using Gamebreak.MiniGolf;

namespace UnityEngine
{
    public struct Vector2 : IEquatable<Vector2>
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 right => new Vector2(1, 0); public static Vector2 up => new Vector2(0, 1); public static Vector2 down => new Vector2(0, -1); public static Vector2 left => new Vector2(-1, 0); public static Vector2 zero => new Vector2(0, 0);
        public float sqrMagnitude => x * x + y * y; public float magnitude => (float)Math.Sqrt(sqrMagnitude);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator *(Vector2 a, float f) => new Vector2(a.x * f, a.y * f);
        public bool Equals(Vector2 o) => x == o.x && y == o.y;
        public static bool operator ==(Vector2 a, Vector2 b) => a.Equals(b); public static bool operator !=(Vector2 a, Vector2 b) => !a.Equals(b);
        public override bool Equals(object o) => o is Vector2 v && Equals(v); public override int GetHashCode() => x.GetHashCode() ^ y.GetHashCode();
    }
    public struct Vector2Int { public int x, y; public Vector2Int(int x, int y) { this.x = x; this.y = y; } }
    public struct Rect { public float xMin, yMin, width, height; public Rect(float x, float y, float w, float h) { xMin = x; yMin = y; width = w; height = h; } public float xMax => xMin + width; public float yMax => yMin + height; }
    public static class Mathf
    {
        public static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v; public static float Lerp(float a, float b, float t) => a + (b - a) * t;
        public static float Sqrt(float v) => (float)Math.Sqrt(v); public static float Cos(float v) => (float)Math.Cos(v); public static float Sin(float v) => (float)Math.Sin(v); public static float Pow(float a, float b) => (float)Math.Pow(a, b);
        public const float PI = 3.14159265f; public static int FloorToInt(float v) => (int)Math.Floor(v); public static int RoundToInt(float v) => (int)Math.Round(v);
        public static int Max(int a, int b) => a > b ? a : b; public static float Min(float a, float b) => a < b ? a : b;
    }
}
namespace Gamebreak.MiniGolf { public class GreenLayout { public float cell = 0.1f; public readonly List<UnityEngine.Rect> areas = new List<UnityEngine.Rect>(); public GreenLayout Area(float x, float z, float w, float l) { areas.Add(new UnityEngine.Rect(x, z, w, l)); return this; } } }

static class P
{
    static void Main()
    {
        ScatterRecipe Recipe() => new ScatterRecipe { canopy = new[] { "Palm0", "Palm1", "Palm2" }, understory = new[] { "BigLeaf0", "BigLeaf1", "Banana0", "Fern0", "LeafBush0", "LeafBush1" }, ground = new[] { "GrassClump0", "GrassClump1", "Fern0" }, accents = new[] { "FlowerShrub0", "FlowerShrub1", "FlowerShrub2" }, clusters = 14 };
        Func<UnityEngine.Vector2, bool> allowed = p => !(Math.Abs(p.x) < 4f && Math.Abs(p.y) < 3f) && p.x > -20f;
        var a = ScatterPlanner.Plan(7, UnityEngine.Vector2.zero, 18f, Recipe(), allowed);
        Console.WriteLine($"scatter(7): {a.Count} items, species {a.Select(i => i.species).Distinct().Count()}, layers {a.Select(i => i.layer).Distinct().Count()}, allowed-all {a.All(i => allowed(i.position))}");
        foreach (int seed in new[] { 11, 3, 5, 21 })
        {
            var it = ScatterPlanner.Plan(seed, UnityEngine.Vector2.zero, 20f, Recipe(), null);
            var nearest = it.Select(x => it.Where(y => !(y.position == x.position && y.species == x.species && y.yaw == x.yaw)).Min(y => (y.position - x.position).magnitude)).ToList();
            float mean = nearest.Average(), sd = (float)Math.Sqrt(nearest.Average(d => (d - mean) * (d - mean)));
            Console.WriteLine($"seed {seed}: {it.Count} items, species {it.Select(i => i.species).Distinct().Count()}, layers {it.Select(i => i.layer).Distinct().Count()}, scale range {it.Max(i => i.scale) - it.Min(i => i.scale):F2}, yaw range {it.Max(i => i.yaw) - it.Min(i => i.yaw):F0}, nn CV {sd / mean:F2}");
        }
        var r = Recipe(); r.minSeparation = 4f; r.emptiness = 0.35f; r.clusters = 10; r.clusterRadius = new UnityEngine.Vector2(1.5f, 2.5f);
        foreach (int seed in new[] { 3, 4, 5 })
        {
            var items = ScatterPlanner.Plan(seed, UnityEngine.Vector2.zero, 24f, r, null);
            int bare = 0, total = 0;
            for (float x = -22f; x <= 22f; x += 1.5f) for (float y = -22f; y <= 22f; y += 1.5f) { var p = new UnityEngine.Vector2(x, y); if (p.magnitude > 22f) continue; total++; if (!items.Any(i => (i.position - p).sqrMagnitude < 2.25f)) bare++; }
            Console.WriteLine($"negative space seed {seed}: {items.Count} items, bare share {bare / (float)total:F2}");
        }
        var l = new GreenLayout(); l.Area(0f, 0f, 6f, 1.2f);
        var all = FencePlanner.Posts(l, 0.3f, 2.4f, 1.4f);
        Console.WriteLine($"fence single lane: {all.Count} posts: " + string.Join(" ", all.Select(p => $"({p.position.x:F1},{p.position.y:F1})")));
        var l2 = new GreenLayout(); l2.Area(0f, 0f, 2f, 4f); l2.Area(2f, 1f, 3f, 2f);
        var p2 = FencePlanner.Posts(l2, 0.3f, 2.4f, 0.8f);
        Console.WriteLine($"fence L join: {p2.Count} posts; any across join: {p2.Any(p => p.position.x > 1.9f && p.position.x < 2.5f && p.position.y > 1.2f && p.position.y < 2.8f)}");
    }
}
