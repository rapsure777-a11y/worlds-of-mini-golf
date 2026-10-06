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
        /// <param name="cull">Cull the cards once smaller than this fraction of the screen height (0 = never); leaf cards are alpha-tested overdraw.</param>
        public GameObject Leaf(string heroMesh, Vector3 pos, float yaw = 0f, float scale = 1f, bool shadows = true,
            float sink = 0.03f, Transform parent = null, float cull = 0.012f)
        {
            var go = PlaceMesh(Hero.Foliage(heroMesh), heroMesh, Hero.Leaves, pos, yaw, scale, true, sink, shadows, false, false, parent, null);
            if (cull > 0f) HeroKit.AddCull(go, cull);
            return go;
        }

        /// <summary>Place a Blender hero model, snapped to the ground unless <paramref name="snap"/> is false.</summary>
        readonly HashSet<string> m_MissingWarned = new HashSet<string>();

        /// <summary>True if the generated FBX exists. New Blender assets are optional until Local Claude has run their scripts.</summary>
        public bool HasModel(string model)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>($"{HeroKit.ModelDir}/{model}.fbx")) return true;
            if (m_MissingWarned.Add(model)) Debug.LogWarning($"[Gamebreak] Hero model '{model}' not found; skipped (run Tools/Blender scripts, see Docs/MILESTONE3_HANDOFF.md).");
            return false;
        }

        /// <param name="seat">After placing, re-seat the model so its lowest vertices sit just under the ground everywhere (no floating edges, no over-burial).</param>
        /// <param name="lods">Rock LODs + distance culling (rock models only).</param>
        /// <param name="yStretch">Vertical stretch applied after placement (boulders read as flat slabs at 1).</param>
        public GameObject Model(string model, Vector3 pos, float yaw = 0f, float scale = 1f, bool snap = true, float sink = 0.05f,
            bool colliders = false, bool outOfBounds = false, bool shadows = true, Transform parent = null,
            bool seat = false, bool lods = false, float lodScale = 1f, int colliderLod = 0, float yStretch = 1f, float seatExtraSink = 0.15f, float cull = 0f)
        {
            if (snap) pos.y = Ground(pos.x, pos.z) - sink * scale;
            var go = Hero.Instantiate(model, parent ? parent : Root, pos, yaw, scale, colliders, outOfBounds, shadows, lods, lodScale, colliderLod);
            if (!Mathf.Approximately(yStretch, 1f)) go.transform.localScale = new Vector3(scale, scale * yStretch, scale);
            if (seat) Seat(go, seatExtraSink);
            if (cull > 0f && !go.GetComponent<LODGroup>()) HeroKit.AddCull(go, cull);
            return go;
        }

        /// <summary>
        /// Gives a placed hero model a stone footing so it never floats: a chamfered block spanning the model's footprint (its renderer bounds, inset), from below the lowest
        /// ground under it up into the model's base. Visual only. Use for solid, axis-aligned buildings (never for gates or arches the player passes under).
        /// </summary>
        public GameObject Footing(GameObject model, Transform parent, Material mat, float inset = 0.06f, float overlap = 0.35f)
        {
            if (!model || !mat) return null;
            var rs = model.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return null;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            float lowest = float.MaxValue;
            for (int i = 0; i <= 4; i++) for (int j = 0; j <= 4; j++)
                lowest = Mathf.Min(lowest, Ground(Mathf.Lerp(b.min.x, b.max.x, i / 4f), Mathf.Lerp(b.min.z, b.max.z, j / 4f)));
            float bottom = lowest - 0.5f, top = b.min.y + overlap;
            if (top <= bottom + 0.05f) return null;
            var size = new Vector3(b.size.x - 2f * inset, top - bottom, b.size.z - 2f * inset);
            return ChamferMesh.Block(model.name + "_Footing", parent, new Vector3(b.center.x, (top + bottom) * 0.5f, b.center.z), Quaternion.identity, size, 0.04f, mat, 1.6f);
        }

        /// <summary>
        /// Blends a built structure into the terrain: a ring of rock-kit stones and foliage tight round its footing (retaining-wall rubble, ferns and bushes growing in the cracks),
        /// leaving the side that faces <paramref name="front"/> open (stairs, doors). Visual only.
        /// </summary>
        public void Grounded(GameObject model, Transform parent, Material rockMat, Vector3 front, int seed, float reach = 0.55f)
        {
            if (!model) return;
            var rs = model.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            var rnd = new System.Random(seed);
            front.y = 0f; if (front.sqrMagnitude > 0.01f) front.Normalize();
            float perim = 2f * (b.size.x + b.size.z);
            int count = Mathf.Clamp(Mathf.RoundToInt(perim / 1.15f), 4, 40);
            string[] shrubs = { "LeafBush0", "LeafBush1", "Fern0", "FlowerShrub0", "BigLeaf0", "FlowerShrub2" };
            for (int i = 0; i < count; i++)
            {
                float t = (i + (float)rnd.NextDouble() * 0.6f) / count * perim, x, z;
                float w = b.size.x, dpt = b.size.z;
                Vector2 outDir;
                if (t < w) { x = b.min.x + t; z = b.min.z; outDir = new Vector2(0, -1); }
                else if (t < w + dpt) { x = b.max.x; z = b.min.z + (t - w); outDir = new Vector2(1, 0); }
                else if (t < 2 * w + dpt) { x = b.max.x - (t - w - dpt); z = b.max.z; outDir = new Vector2(0, 1); }
                else { x = b.min.x; z = b.max.z - (t - 2 * w - dpt); outDir = new Vector2(-1, 0); }
                if (front.sqrMagnitude > 0.01f && Vector2.Dot(outDir, new Vector2(front.x, front.z)) > 0.5f) continue;
                var p = new Vector3(x + outDir.x * reach * (0.6f + (float)rnd.NextDouble()), 0f, z + outDir.y * reach * (0.6f + (float)rnd.NextDouble()));
                if (!IsFree(new Vector2(p.x, p.z)) || Ground(p.x, p.z) < 0.15f) continue;
                if (i % 2 == 0 && HasModel("HeroRock_C"))
                    Crag(i % 4 == 0 ? "HeroRock_C" : (i % 3 == 0 ? "HeroRock_A" : "HeroStone_C"), p, (float)rnd.NextDouble() * 360f, 0.55f + (float)rnd.NextDouble() * 0.6f, rockMat, parent, lods: false, extraSink: 0.12f);
                else
                    Leaf(shrubs[rnd.Next(shrubs.Length)], p, (float)rnd.NextDouble() * 360f, 0.9f + (float)rnd.NextDouble() * 0.6f, parent: parent);
            }
        }

        /// <summary>A saved variant of a triplanar rock material with its own moss/grass cap coverage and optional tint (cached by name under the kit's Materials folder).</summary>
        public static Material RockVariant(Material src, string name, float topCoverage, Color? tint = null)
        {
            if (!src) return null;
            string path = $"{TropicalKit.Root}/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(src); AssetDatabase.CreateAsset(m, path); }
            else m.CopyPropertiesFromMaterial(src);
            m.SetFloat("_TopCoverage", topCoverage);
            if (tint.HasValue && m.HasProperty("_Tint")) m.SetColor("_Tint", tint.Value);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// A piece of the Graphics Pass 3B rock kit (HeroCliff_*, HeroRock_*, HeroStone_*, HeroSpire_*, HeroBasalt_*, HeroCrater_*), seated on the ground and re-skinned with
        /// <paramref name="rockMaterial"/> (triplanar limestone / sandstone / basalt). Visual only.
        /// </summary>
        public GameObject Crag(string model, Vector3 pos, float yaw, float scale, Material rockMaterial, Transform parent, bool lods = true, float yStretch = 1f, float extraSink = 0.1f, bool shadows = true, bool ground = true)
        {
            if (!HasModel(model)) return null;
            var go = Model(model, pos, yaw, scale, snap: ground, sink: 0.1f, parent: parent, seat: ground, lods: lods, yStretch: yStretch, seatExtraSink: extraSink, shadows: shadows);
            if (rockMaterial)
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
                    if (r.sharedMaterial == Hero.Rock) r.sharedMaterial = rockMaterial;
            return go;
        }

        /// <summary>Hero rock by size class: "small" / "medium" boulder clusters (scaled down) or "large" mesas. Cheap by default: LODs, no shadows for small.</summary>
        public GameObject Rock(string kind, Vector3 pos, float yaw, float scale, Transform parent, bool collider = false, bool outOfBounds = false, int variant = 0)
        {
            bool large = kind == "large";
            string model = large ? $"HeroMesa_{variant & 1}" : $"HeroBoulders_{variant & 1}";
            float s = large ? scale : scale * (kind == "small" ? 0.33f : 0.55f);
            return Model(model, pos, yaw, s, snap: true, sink: 0.1f, colliders: collider, outOfBounds: outOfBounds, shadows: large || kind == "medium",
                parent: parent, seat: true, lods: true, colliderLod: collider ? 1 : 0, yStretch: large ? 1f : 1.4f, seatExtraSink: 0.12f);
        }

        /// <summary>
        /// Moves a model vertically so that every vertex in its lowest 35 cm is at or just below the ground beneath it.
        /// Fixes cliffs and boulders hovering on slopes (flat base over curved terrain) and slabs buried too deep. The shift is clamped.
        /// </summary>
        public float Seat(GameObject go, float extraSink = 0.15f, float maxShift = 1.6f)
        {
            float shift = float.MaxValue;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (!mf.sharedMesh || mf.name.StartsWith("LOD")) continue;
                var verts = mf.sharedMesh.vertices;
                var m = mf.transform.localToWorldMatrix;
                float minY = float.MaxValue;
                for (int i = 0; i < verts.Length; i += 2) minY = Mathf.Min(minY, m.MultiplyPoint3x4(verts[i]).y);
                for (int i = 0; i < verts.Length; i += 2)
                {
                    var w = m.MultiplyPoint3x4(verts[i]);
                    if (w.y > minY + 0.35f) continue;
                    shift = Mathf.Min(shift, Ground(w.x, w.z) - w.y);
                }
            }
            if (shift == float.MaxValue) return 0f;
            shift = Mathf.Clamp(shift - extraSink, -maxShift, maxShift);
            go.transform.position += Vector3.up * shift;
            return shift;
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
