using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static Gamebreak.MiniGolf.Editor.Art.TropicalWorld;

namespace Gamebreak.MiniGolf.Editor.Art
{
    /// <summary>
    /// Graphics Pass 3 environment dressing: layered foliage per biome, rope-and-post fences, braziers and banners, glowing lava, sea of cloud. Runs after the hand dressing
    /// and before the obstruction scan, entirely through <see cref="Gp3.Run"/>. Everything is decoration without colliders and stays out of the greens through the
    /// dressers' keep-outs plus extra margins around tees and cups. No hole geometry is read for change, only for clearance.
    /// </summary>
    public static class Gp3Dressing
    {
        sealed class Biome
        {
            public string name; public Dresser dresser; public Vector2 centre; public float radius; public ScatterRecipe recipe; public int seed; public float minHeight = 0.35f;
        }

        static readonly string[] Palms = { "HeroPalm_0", "HeroPalm_1", "HeroPalm_2" };

        public static void Dress(List<HoleDefinition> defs, List<HoleFrame> frames, Dresser start, Dresser jungle, Dresser temple, Dresser volcanic, Dresser summit, Transform root)
        {
            var gp = new GameObject("Gp3Dressing").transform;
            gp.SetParent(root, false);
            var biomes = new List<Biome>();
            if (start != null) biomes.Add(new Biome { name = "Start", dresser = start, centre = start.Island.centre, radius = start.Island.radius * 0.92f, seed = 301, recipe = Recipe(16, Palms, 0.45f) });
            if (jungle != null) biomes.Add(new Biome { name = "Jungle", dresser = jungle, centre = jungle.Island.centre, radius = jungle.Island.radius * 0.95f, seed = 302, recipe = Recipe(26, Palms, 0.5f, dense: true) });
            if (temple != null) biomes.Add(new Biome { name = "Temple", dresser = temple, centre = temple.Island.centre, radius = temple.Island.radius * 0.9f, seed = 303, recipe = Recipe(10, Palms, 0.25f, flowers: true) });
            if (volcanic != null) biomes.Add(new Biome { name = "Volcanic", dresser = volcanic, centre = volcanic.Island.centre, radius = volcanic.Island.radius * 0.85f, seed = 304, recipe = Sparse(6) });
            if (summit != null) biomes.Add(new Biome { name = "Summit", dresser = summit, centre = summit.Island.centre, radius = summit.Island.radius * 0.8f, seed = 305, recipe = Recipe(7, new string[0], 0f, flowers: true) });

            foreach (var b in biomes) Gp3.Run($"foliage {b.name}", () => Foliage(b, defs, frames, gp));
            for (int i = 0; i < defs.Count; i++)
            {
                int n = defs[i].number; var f = frames[i]; var def = defs[i];
                var dr = n <= 2 ? start : n <= 4 ? jungle : n <= 6 ? temple : n <= 8 ? volcanic : summit;
                if (dr == null) continue;
                bool shrine = n == 5 || n == 6;
                Gp3.Run($"rail coping hole {n}", () => RailCoping(dr, def, f, gp, n));
                if (n != 9) Gp3.Run($"fence hole {n}", () => Fence(dr, def, f, gp, shrine, n));   // Hole 9 keeps its rail-less identity and a clear Altar: no fence there
            }
            Gp3.Run("lava re-skin", () => ReskinLava(root));
            if (summit != null) Gp3.Run("cloud sea", () => CloudSea(summit, gp));
        }

        // ------------------------------------------------------------------ foliage

        static ScatterRecipe Recipe(int clusters, string[] canopy, float canopyChance, bool dense = false, bool flowers = false) => new ScatterRecipe
        {
            canopy = canopy,
            understory = new[] { "BigLeaf0", "BigLeaf1", "Banana0", "LeafBush0", "LeafBush1", "LeafBush2", "Fern0" },
            ground = new[] { "GrassClump0", "GrassClump1", "Fern0" },
            accents = new[] { "FlowerShrub0", "FlowerShrub1", "FlowerShrub2" },
            clusters = clusters, canopyChance = canopyChance,
            accentChance = flowers ? 0.3f : 0.14f,
            emptiness = dense ? 0.2f : 0.32f,
            minSeparation = dense ? 2.6f : 3.4f,
        };

        static ScatterRecipe Sparse(int clusters) => new ScatterRecipe
        {
            canopy = new string[0], understory = new[] { "Fern0", "LeafBush2" }, ground = new[] { "GrassClump1" }, accents = new string[0],
            clusters = clusters, emptiness = 0.5f, minSeparation = 5f, itemsPerCluster = new Vector2Int(3, 7),
        };

        static void Foliage(Biome b, List<HoleDefinition> defs, List<HoleFrame> frames, Transform gp)
        {
            var d = b.dresser;
            var holder = new GameObject($"Foliage_{b.name}").transform; holder.SetParent(gp, false);
            // Extra clearance on top of the dresser's keep-outs: no planting within 2 m of any tee or cup, so the standing places stay open.
            var standing = new List<Vector2>();
            for (int i = 0; i < defs.Count; i++)
            {
                standing.Add(frames[i].L2(defs[i].tee.x, defs[i].tee.y));
                if (defs[i].layout.cup.HasValue) standing.Add(frames[i].L2(defs[i].layout.cup.Value.x, defs[i].layout.cup.Value.y));
            }
            bool Allowed(Vector2 p)
            {
                if (!d.IsFree(p) || d.Ground(p.x, p.y) < b.minHeight) return false;
                foreach (var s in standing) if ((s - p).sqrMagnitude < 4f) return false;
                return true;
            }
            var items = ScatterPlanner.Plan(b.seed, b.centre, b.radius, b.recipe, Allowed);
            foreach (var it in items)
            {
                var pos = new Vector3(it.position.x, 0f, it.position.y);
                if (it.layer == 0)
                {
                    string palm = it.species;
                    if (d.HasModel(palm)) d.Model(palm, pos, it.yaw, it.scale, sink: 0.06f, parent: holder, cull: 0.006f);
                }
                else d.Leaf(it.species, pos, it.yaw, it.scale, shadows: it.layer == 1 && it.scale > 1.1f, parent: holder, cull: it.layer == 2 ? 0.014f : 0.01f);
            }
        }

        // ------------------------------------------------------------------ fences, braziers, banners

        static Mesh s_Cyl, s_Cube;
        static Mesh Cylinder() => s_Cyl ? s_Cyl : s_Cyl = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        static Mesh Cube() => s_Cube ? s_Cube : s_Cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

        static GameObject Part(Dresser d, Mesh mesh, string name, Material mat, Vector3 pos, Vector3 scale, Quaternion rot, Transform parent, bool shadows = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return go;
        }

        /// <summary>
        /// Graphics Pass 3B: dresses the course rails so they read as built masonry instead of a flat strip: a chamfered coping stone on top of the wall, laid in short
        /// stones with joints, and a taller cut-stone post at every run end and corner. Visual only: the wall mesh and its collider are untouched, everything sits on top of
        /// the existing wall (never inside the playing area), and open edges (lips, jumps) get nothing.
        /// </summary>
        static void RailCoping(Dresser d, HoleDefinition def, HoleFrame f, Transform gp, int number)
        {
            var stoneMat = Gp3Materials.Ready ? (Gp3Materials.Limestone ? Gp3Materials.Limestone : Gp3Materials.Sandstone) : null;
            if (!stoneMat) return;
            var l = def.layout;
            var holder = new GameObject($"RailCoping_Hole{number}").transform; holder.SetParent(gp, false);
            float th = l.wallThickness, wh = l.wallHeight, step = l.cell;
            var rects = l.areas;
            bool Covered(Vector2 p) { foreach (var r in rects) if (p.x > r.xMin + 1e-4f && p.x < r.xMax - 1e-4f && p.y > r.yMin + 1e-4f && p.y < r.yMax - 1e-4f) return true; return false; }
            bool IsOpen(Vector2 p) { foreach (var r in l.openEdges) if (p.x >= r.xMin - 1e-3f && p.x <= r.xMax + 1e-3f && p.y >= r.yMin - 1e-3f && p.y <= r.yMax + 1e-3f) return true; return false; }
            Vector3 W(Vector2 p, float up) { var w = f.L2(p.x, p.y); return new Vector3(w.x, f.origin.y + l.Height(p.x, p.y) + up, w.y); }
            var placed = new HashSet<Vector2Int>();
            foreach (var r in rects)
            {
                var sides = new[]
                {
                    (new Vector2(r.xMin, r.yMin), Vector2.right, Vector2.down, r.width),
                    (new Vector2(r.xMin, r.yMax), Vector2.right, Vector2.up, r.width),
                    (new Vector2(r.xMin, r.yMin), Vector2.up, Vector2.left, r.height),
                    (new Vector2(r.xMax, r.yMin), Vector2.up, Vector2.right, r.height),
                };
                foreach (var (origin, dir, outward, length) in sides)
                {
                    int n = Mathf.Max(1, Mathf.RoundToInt(length / step));
                    float runStart = -1f;
                    for (int i = 0; i <= n; i++)
                    {
                        float s = Mathf.Min(length, i * step);
                        Vector2 mid = origin + dir * Mathf.Min(length, (i + 0.5f) * step) + outward * 0.03f;
                        Vector2 onEdge = origin + dir * Mathf.Min(length, (i + 0.5f) * step);
                        bool open = i < n && !Covered(mid) && !IsOpen(onEdge);
                        if (open && runStart < 0f) runStart = i * step;
                        if ((!open || i == n) && runStart >= 0f)
                        {
                            float runEnd = s;
                            if (runEnd - runStart > 0.05f) CopingRun(d, f, holder, l, origin, dir, outward, runStart, runEnd, th, wh, stoneMat, W, placed);
                            runStart = -1f;
                        }
                    }
                }
            }
        }

        static void CopingRun(Dresser d, HoleFrame f, Transform holder, GreenLayout l, Vector2 origin, Vector2 dir, Vector2 outward, float s0, float s1, float th, float wh,
            Material mat, System.Func<Vector2, float, Vector3> W, HashSet<Vector2Int> placed)
        {
            float len = s1 - s0;
            int stones = Mathf.Max(1, Mathf.RoundToInt(len / 0.55f));
            float sl = len / stones;
            Vector2 off = outward * (th * 0.5f);
            for (int k = 0; k < stones; k++)
            {
                float a = s0 + k * sl, b = a + sl;
                Vector2 pa = origin + dir * a + off, pb = origin + dir * b + off;
                Vector3 wa = W(pa, wh + 0.012f), wb = W(pb, wh + 0.012f);
                Vector3 axis = wb - wa;
                if (axis.sqrMagnitude < 1e-6f) continue;
                var key = new Vector2Int(Mathf.RoundToInt((wa.x + wb.x) * 50f), Mathf.RoundToInt((wa.z + wb.z) * 50f));
                if (!placed.Add(key)) continue;
                float jitter = 0.012f * ((k * 7 + Mathf.RoundToInt(origin.x * 10f + origin.y * 10f)) % 3);
                var go = ChamferMesh.Block("Coping", holder, (wa + wb) * 0.5f + Vector3.up * (0.014f + jitter * 0.5f), Quaternion.LookRotation(axis.normalized, Vector3.up),
                    new Vector3(th + 0.05f, 0.032f + jitter, axis.magnitude - 0.018f), 0.011f, mat, 1.1f);
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            }
            // A cut-stone post with a capped head at each end of the run (the wall's own end, standing proud of it).
            foreach (float sEnd in new[] { s0, s1 })
            {
                Vector2 p = origin + dir * sEnd + outward * (th * 0.5f);
                Vector3 w = W(p, 0f);
                var key = new Vector2Int(Mathf.RoundToInt(w.x * 50f) + 100000, Mathf.RoundToInt(w.z * 50f));
                if (!placed.Add(key)) continue;
                float ph = wh + 0.075f, pw = th + 0.075f;
                var rot = Quaternion.Euler(0f, f.yaw, 0f);
                var post = ChamferMesh.Block("RailPost", holder, w + Vector3.up * (ph * 0.5f - 0.02f), rot, new Vector3(pw, ph, pw), 0.014f, mat, 1.1f);
                var cap = ChamferMesh.Block("RailPostCap", holder, w + Vector3.up * (ph - 0.005f), rot, new Vector3(pw + 0.035f, 0.035f, pw + 0.035f), 0.012f, mat, 1.1f);
                GameObjectUtility.SetStaticEditorFlags(post, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
                GameObjectUtility.SetStaticEditorFlags(cap, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            }
        }

        static void Fence(Dresser d, HoleDefinition def, HoleFrame f, Transform gp, bool shrine, int number)
        {
            if (!Gp3Materials.Ready || !Gp3Materials.Rope || !Gp3Materials.WoodDark) return;
            var holder = new GameObject($"Fence_Hole{number}").transform; holder.SetParent(gp, false);
            Vector2 tee = def.tee, cup = def.layout.cup ?? def.tee;
            // No posts within 1.4 m of the tee or cup (stand-and-aim places) and none beyond the island's land.
            bool Allowed(Vector2 local)
            {
                if ((local - tee).sqrMagnitude < 1.4f * 1.4f || (local - cup).sqrMagnitude < 1.4f * 1.4f) return false;
                var w = f.L2(local.x, local.y);
                return d.Ground(w.x, w.y) > 0.25f;
            }
            var posts = FencePlanner.Posts(def.layout, 0.32f, 2.4f, 1.4f, Allowed);
            var byRun = new Dictionary<int, List<Vector3>>();
            int corner = 0;
            foreach (var p in posts)
            {
                var w = f.L2(p.position.x, p.position.y);
                float y = d.Ground(w.x, w.y);
                var pos = new Vector3(w.x, y, w.y);
                float h = 0.62f;
                bool lamp = shrine && p.corner && (corner++ % 2 == 0);
                if (lamp) Brazier(d, holder, pos);
                else Part(d, Cylinder(), "Post", Gp3Materials.WoodDark, pos + Vector3.up * (h * 0.5f - 0.05f), new Vector3(0.09f, h * 0.5f + 0.025f, 0.09f), Quaternion.identity, holder);
                if (!byRun.TryGetValue(p.run, out var list)) byRun[p.run] = list = new List<Vector3>();
                list.Add(pos + Vector3.up * (lamp ? 0.62f : h - 0.1f));
            }
            // Rope between neighbouring posts of the same run, with a slight sag (three short segments).
            foreach (var kv in byRun)
            {
                var l = kv.Value;
                for (int i = 0; i + 1 < l.Count; i++) Rope(d, holder, l[i], l[i + 1]);
            }
        }

        static void Rope(Dresser d, Transform parent, Vector3 a, Vector3 b)
        {
            const int segs = 4;
            Vector3 prev = a;
            for (int s = 1; s <= segs; s++)
            {
                float t = s / (float)segs;
                var p = Vector3.Lerp(a, b, t);
                p.y -= Mathf.Sin(t * Mathf.PI) * 0.08f;
                var dir = p - prev;
                float len = dir.magnitude;
                if (len > 0.01f)
                    Part(d, Cylinder(), "Rope", Gp3Materials.Rope, (prev + p) * 0.5f, new Vector3(0.025f, len * 0.5f, 0.025f), Quaternion.FromToRotation(Vector3.up, dir.normalized), parent);
                prev = p;
            }
        }

        static void Brazier(Dresser d, Transform parent, Vector3 pos)
        {
            if (Gp3Materials.Brazier) Part(d, Cylinder(), "BrazierStem", Gp3Materials.Brazier, pos + Vector3.up * 0.32f, new Vector3(0.09f, 0.32f, 0.09f), Quaternion.identity, parent, true);
            if (Gp3Materials.Brazier) Part(d, Cylinder(), "BrazierBowl", Gp3Materials.Brazier, pos + Vector3.up * 0.68f, new Vector3(0.28f, 0.05f, 0.28f), Quaternion.identity, parent);
            if (Gp3Materials.Ember) Part(d, Cylinder(), "BrazierEmber", Gp3Materials.Ember, pos + Vector3.up * 0.74f, new Vector3(0.22f, 0.02f, 0.22f), Quaternion.identity, parent);
        }

        // ------------------------------------------------------------------ lava and clouds

        /// <summary>Swaps the volcanic island's lava boxes and falls (named Volcanic_Lava*) for the generated emissive lava, so the glow cracks read as molten.</summary>
        static void ReskinLava(Transform root)
        {
            if (!Gp3Materials.Ready || !Gp3Materials.Lava) return;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))   // the lava boxes live under the holes, not the dressing root
            {
                var m = r.sharedMaterial;
                if (m && (m.name == "Volcanic_Lava" || m.name == "Volcanic_LavaFall")) r.sharedMaterial = Gp3Materials.Lava;
            }
        }

        /// <summary>A sea of cloud far below the Summit island (decor, no collision): a few big flattened cloud meshes, ringed round it.</summary>
        static void CloudSea(Dresser d, Transform gp)
        {
            var holder = new GameObject("CloudSea").transform; holder.SetParent(gp, false);
            var rnd = new System.Random(909);
            var mat = Gp3Materials.Cloud2 ? Gp3Materials.Cloud2 : d.Kit.Cloud;
            var clusters = TropicalCourse.Clusters();
            for (int i = 0; i < 18; i++)
            {
                float a = i / 18f * Mathf.PI * 2f + Dresser.Range(rnd, -0.2f, 0.2f), dist = Dresser.Range(rnd, 56f, 80f);
                var pos = new Vector3(d.Island.centre.x + Mathf.Cos(a) * dist, Dresser.Range(rnd, -1.5f, 1.2f), d.Island.centre.y + Mathf.Sin(a) * dist);
                float s = Dresser.Range(rnd, 0.8f, 1.3f);   // the kit cloud meshes are already 18-36 m across: this is a scale, not metres
                float half = 18f * s;
                // Never let a cloud sit on (or swallow) another island: keep clear of every cluster (the Summit's own included) by its radius plus the cloud's.
                bool clear = true;
                foreach (var c in clusters)
                    if (Vector2.Distance(new Vector2(pos.x, pos.z), c.centre) < c.radius + half + 8f) { clear = false; break; }
                if (!clear) continue;
                var go = Part(d, d.Kit[$"Cloud{rnd.Next(0, 4)}"], "SeaCloud", mat, pos, new Vector3(s, s * 0.45f, s), Quaternion.Euler(0f, Dresser.Range(rnd, 0f, 360f), 0f), holder);
                go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }
    }
}
