using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gamebreak.MiniGolf.Editor.Art
{
    /// <summary>
    /// Placement helpers for dressing a world from the kit: ground snapping, hole-relative coordinates,
    /// avoidance of greens/paths/water, and seeded scattering. Everything placed is static for batching.
    /// </summary>
    public class Dresser
    {
        public readonly TropicalKit Kit;
        /// <summary>Blender-generated hero models, PBR materials and leaf-card foliage.</summary>
        public readonly HeroKit Hero;
        public readonly IslandGen Island;
        public readonly Transform Root;
        readonly List<(Vector2 c, Vector2 half, float yaw, float margin)> m_Keepout = new List<(Vector2, Vector2, float, float)>();

        public Dresser(TropicalKit kit, HeroKit hero, IslandGen island, Transform root)
        {
            Kit = kit; Hero = hero; Island = island; Root = root;
        }

        /// <summary>Keep scatter away from an oriented rectangle (greens, paths, decks).</summary>
        public void KeepOut(Vector2 centre, Vector2 half, float yaw, float margin) => m_Keepout.Add((centre, half, yaw, margin));

        public bool IsFree(Vector2 p)
        {
            foreach (var k in m_Keepout)
            {
                Vector2 d = p - k.c;
                float rad = k.yaw * Mathf.Deg2Rad; // inverse of Unity's yaw rotation
                var local = new Vector2(d.x * Mathf.Cos(rad) - d.y * Mathf.Sin(rad), d.x * Mathf.Sin(rad) + d.y * Mathf.Cos(rad));
                if (Mathf.Abs(local.x) < k.half.x + k.margin && Mathf.Abs(local.y) < k.half.y + k.margin) return false;
            }
            return true;
        }

        public float Ground(float x, float z) => Island.Height(x, z);

        public GameObject Place(string mesh, Material mat, Vector3 pos, float yaw = 0f, float scale = 1f,
            bool snap = true, float sink = 0.04f, bool shadows = true, bool collider = false, bool outOfBounds = false,
            Transform parent = null, Vector3? tilt = null)
            => PlaceMesh(Kit[mesh], mesh, mat, pos, yaw, scale, snap, sink, shadows, collider, outOfBounds, parent, tilt);

        /// <summary>Place a leaf-card foliage mesh from the hero kit (LeafBush*, FlowerShrub*, BigLeaf*, Banana0, Fern0, GrassClump*).</summary>
        public GameObject Leaf(string heroMesh, Vector3 pos, float yaw = 0f, float scale = 1f, bool shadows = true,
            float sink = 0.03f, Transform parent = null)
            => PlaceMesh(Hero.Foliage(heroMesh), heroMesh, Hero.Leaves, pos, yaw, scale, true, sink, shadows, false, false, parent, null);

        /// <summary>Place a Blender hero model, snapped to the ground unless <paramref name="snap"/> is false.</summary>
        public GameObject Model(string model, Vector3 pos, float yaw = 0f, float scale = 1f, bool snap = true, float sink = 0.05f,
            bool colliders = false, bool outOfBounds = false, bool shadows = true, Transform parent = null)
        {
            if (snap) pos.y = Ground(pos.x, pos.z) - sink * scale;
            return Hero.Instantiate(model, parent ? parent : Root, pos, yaw, scale, colliders, outOfBounds, shadows);
        }

        public GameObject PlaceMesh(Mesh mesh, string name, Material mat, Vector3 pos, float yaw = 0f, float scale = 1f,
            bool snap = true, float sink = 0.04f, bool shadows = true, bool collider = false, bool outOfBounds = false,
            Transform parent = null, Vector3? tilt = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent ? parent : Root, false);
            if (snap) pos.y = Ground(pos.x, pos.z) - sink * scale;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(tilt ?? Vector3.zero) * Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (collider)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                if (outOfBounds) go.AddComponent<OutOfBoundsSurface>();
            }
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return go;
        }

        /// <summary>Palm = trunk (solid) + crown (foliage), sharing one parent.</summary>
        public GameObject Palm(int variant, Vector3 pos, float yaw, float scale = 1f)
        {
            var trunk = Place($"PalmTrunk{variant}", Kit.Solid, pos, yaw, scale, sink: 0.05f);
            Place($"PalmCrown{variant}", Kit.Foliage, trunk.transform.position, yaw, scale, snap: false, parent: trunk.transform.parent);
            trunk.name = $"Palm{variant}";
            return trunk;
        }

        public GameObject Torch(Vector3 pos)
        {
            var t = Place("TikiTorch", Kit.Solid, pos, 0f, 1f, sink: 0.02f);
            Place("TikiFlame", Kit.Emissive, t.transform.position, 0f, 1f, snap: false, shadows: false);
            return t;
        }

        /// <summary>Seeded scatter in a disc; places only where free, on land and within a height range.</summary>
        public int Scatter(System.Random rnd, Vector2 centre, float radius, int count, Func<System.Random, Vector2, bool> place,
            float minHeight = 0.12f, float maxHeight = 50f, int maxTries = 8)
        {
            int placed = 0;
            for (int i = 0; i < count; i++)
            {
                for (int tryN = 0; tryN < maxTries; tryN++)
                {
                    float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                    float r = Mathf.Sqrt((float)rnd.NextDouble()) * radius;
                    var p = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    float h = Ground(p.x, p.y);
                    if (h < minHeight || h > maxHeight || !IsFree(p)) continue;
                    if (place(rnd, p)) { placed++; break; }
                }
            }
            return placed;
        }

        public static float Range(System.Random rnd, float a, float b) => a + (float)rnd.NextDouble() * (b - a);
    }
}
