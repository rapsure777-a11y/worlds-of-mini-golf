using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Gamebreak.MiniGolf.Editor.Art
{
    /// <summary>
    /// Builds the Tropical Adventure world around the holes: island terrain, ocean, sky, lighting and
    /// set dressing. Per-hole dressing lives in Dress{N} methods using hole-local coordinates, so each
    /// hole's look is easy to tune; the kit, island and helpers are shared by all holes.
    /// </summary>
    public static class TropicalWorld
    {
        /// <summary>Hole-local frame: x = across the lane (+x = right of the tee), z = down the lane.</summary>
        public readonly struct HoleFrame
        {
            public readonly Vector3 origin; public readonly float yaw;
            public HoleFrame(HoleDefinition d) { origin = d.origin; yaw = d.yaw; }
            public Vector3 L(float x, float z) => origin + Quaternion.Euler(0f, yaw, 0f) * new Vector3(x, 0f, z);
            public Vector2 L2(float x, float z) { var p = L(x, z); return new Vector2(p.x, p.z); }
            public float Yaw(float localYaw) => yaw + localYaw;
        }

        public static void Build(TropicalKit kit, List<HoleDefinition> defs, Transform parent)
        {
            var island = new IslandGen { centre = Vector2.zero, radius = 34f, seed = 7 };
            var frames = new List<HoleFrame>();
            foreach (var d in defs) frames.Add(new HoleFrame(d));

            // Flatten every hole site (layout bounds + walk-around margin) to its green's ground height.
            for (int i = 0; i < defs.Count; i++)
            {
                var (centre, half) = LayoutBounds(defs[i].layout);
                var f = frames[i];
                island.zones.Add(new IslandGen.Zone
                {
                    centre = f.L2(centre.x, centre.y), halfSize = half + new Vector2(1.2f, 1.6f), yaw = f.yaw,
                    height = defs[i].origin.y - TropicalCourse.GreenElevation - 0.06f, feather = 3.5f,
                });
            }
            // Path from each cup to the next tee.
            for (int i = 0; i + 1 < defs.Count; i++)
            {
                var a = frames[i].L2(defs[i].layout.cup.Value.x, defs[i].layout.cup.Value.y + 1.2f);
                var b = frames[i + 1].L2(defs[i + 1].tee.x, defs[i + 1].tee.y - 1.2f);
                AddPath(island, a, b, 0.8f, Mathf.Min(defs[i].origin.y, defs[i + 1].origin.y) - TropicalCourse.GreenElevation - 0.08f);
            }
            // Raised backdrop behind Hole 1 for the cliffs.
            if (defs.Count > 0) island.mounds.Add((frames[0].L2(-8.5f, 0.5f), 7f, 1.6f));
            // Central jungle mountain: the island's landmark and backdrop from every hole.
            island.mounds.Add((new Vector2(-2f, 9f), 17f, 6.5f));
            island.mounds.Add((new Vector2(-12f, 1f), 9f, 2.6f));

            var world = new GameObject("World").transform;
            world.SetParent(parent, false);
            BuildTerrainAndSea(kit, island, world);
            SetupLighting(kit);

            var dresser = new Dresser(kit, island, new GameObject("Dressing").transform);
            dresser.Root.SetParent(world, false);
            for (int i = 0; i < defs.Count; i++)
            {
                var (centre, half) = LayoutBounds(defs[i].layout);
                // Generous clearance: holes get hand-placed dressing; random island cover stays back.
                dresser.KeepOut(frames[i].L2(centre.x, centre.y), half + new Vector2(0.9f, 1.2f), frames[i].yaw, 2.6f);
            }
            foreach (var z in island.zones) if (z.path) dresser.KeepOut(z.centre, z.halfSize, z.yaw, 0.2f);

            if (defs.Count > 0) DressHole1(dresser, frames[0], defs[0]);
            if (defs.Count > 1) DressHole2(dresser, frames[1], defs[1]);
            DressIsland(dresser);
            BuildHorizon(kit, island, world);
            RemoveObstructions(dresser.Root);
        }

        /// <summary>
        /// Safety net: scan every green on a 20 cm grid and remove any dressing collider that sits on or
        /// above the putting surface (it would block or wrongly penalise the ball).
        /// </summary>
        static void RemoveObstructions(Transform dressingRoot)
        {
            Physics.SyncTransforms();
            var offenders = new HashSet<GameObject>();
            foreach (var green in Object.FindObjectsByType<PlayableSurface>(FindObjectsSortMode.None))
            {
                var col = green.GetComponent<Collider>();
                var b = col.bounds;
                for (float x = b.min.x; x <= b.max.x; x += 0.2f)
                for (float z = b.min.z; z <= b.max.z; z += 0.2f)
                {
                    var hits = Physics.RaycastAll(new Vector3(x, b.max.y + 30f, z), Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore);
                    float greenY = float.NaN;
                    foreach (var h in hits) if (h.collider == col) greenY = h.point.y;
                    if (float.IsNaN(greenY)) continue;
                    foreach (var h in hits)
                        if (h.collider.transform.IsChildOf(dressingRoot) && h.point.y > greenY - 0.02f)
                            offenders.Add(h.collider.gameObject);
                }
            }
            foreach (var go in offenders)
            {
                Debug.LogWarning($"[Gamebreak] Removed dressing '{go.name}' at {go.transform.position:F1}: it overlapped a green.");
                Object.DestroyImmediate(go);
            }
        }

        // ------------------------------------------------------------------ terrain, sea, sky

        static void BuildTerrainAndSea(TropicalKit kit, IslandGen island, Transform world)
        {
            var terrainMesh = island.BuildTerrain();
            SaveMesh(terrainMesh, "IslandTerrain");
            var terrain = new GameObject("IslandTerrain");
            terrain.transform.SetParent(world, false);
            terrain.AddComponent<MeshFilter>().sharedMesh = terrainMesh;
            var tr = terrain.AddComponent<MeshRenderer>();
            tr.sharedMaterial = kit.Terrain;
            tr.shadowCastingMode = ShadowCastingMode.On;
            terrain.AddComponent<MeshCollider>().sharedMesh = terrainMesh;
            terrain.AddComponent<OutOfBoundsSurface>();
            GameObjectUtility.SetStaticEditorFlags(terrain, StaticEditorFlags.BatchingStatic);

            var oceanMesh = island.BuildOcean();
            SaveMesh(oceanMesh, "Ocean");
            var ocean = new GameObject("Ocean");
            ocean.transform.SetParent(world, false);
            ocean.AddComponent<MeshFilter>().sharedMesh = oceanMesh;
            var orr = ocean.AddComponent<MeshRenderer>();
            orr.sharedMaterial = kit.Water;
            orr.shadowCastingMode = ShadowCastingMode.Off;
        }

        static void SaveMesh(Mesh mesh, string name)
        {
            string path = $"{TropicalKit.Root}/Meshes/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
        }

        static void SetupLighting(TropicalKit kit)
        {
            var sun = Object.FindFirstObjectByType<Light>();
            if (sun)
            {
                sun.color = new Color(1f, 0.93f, 0.8f);
                sun.intensity = 1.55f;
                sun.transform.rotation = Quaternion.Euler(40f, 205f, 0f);
                sun.shadowStrength = 1f;
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.48f, 0.64f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.66f, 0.62f);
            RenderSettings.ambientGroundColor = new Color(0.42f, 0.36f, 0.27f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.72f, 0.86f, 0.96f);
            RenderSettings.fogDensity = 0.0045f;
            RenderSettings.skybox = kit.Sky;
            DynamicGI.UpdateEnvironment();
        }

        static void BuildHorizon(TropicalKit kit, IslandGen island, Transform world)
        {
            var horizon = new GameObject("Horizon").transform;
            horizon.SetParent(world, false);
            var rnd = new System.Random(31);
            // Distant islands, mostly visible from the southern (Hole 1) side.
            float[] angles = { 200f, 245f, 290f, 330f, 120f };
            for (int i = 0; i < angles.Length; i++)
            {
                float a = angles[i] * Mathf.Deg2Rad, dist = Dresser.Range(rnd, 150f, 240f);
                var go = new GameObject("DistantIsland");
                go.transform.SetParent(horizon, false);
                go.transform.position = new Vector3(Mathf.Cos(a) * dist, -1.5f, Mathf.Sin(a) * dist);
                go.transform.rotation = Quaternion.Euler(0f, Dresser.Range(rnd, 0f, 360f), 0f);
                go.transform.localScale = Vector3.one * Dresser.Range(rnd, 26f, 48f);
                go.AddComponent<MeshFilter>().sharedMesh = kit["DistantIsland"];
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = kit.Solid;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
            for (int i = 0; i < 14; i++)
            {
                float a = Dresser.Range(rnd, 0f, Mathf.PI * 2f), dist = Dresser.Range(rnd, 170f, 280f);
                var go = new GameObject("Cloud");
                go.transform.SetParent(horizon, false);
                go.transform.position = new Vector3(Mathf.Cos(a) * dist, Dresser.Range(rnd, 45f, 85f), Mathf.Sin(a) * dist);
                go.transform.rotation = Quaternion.LookRotation(-go.transform.position.normalized.WithY(0f)) * Quaternion.Euler(0f, Dresser.Range(rnd, -30f, 30f), 0f);
                go.transform.localScale = Vector3.one * Dresser.Range(rnd, 0.8f, 1.5f);
                go.AddComponent<MeshFilter>().sharedMesh = kit[$"Cloud{rnd.Next(0, 4)}"];
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = kit.Cloud;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        static Vector3 WithY(this Vector3 v, float y) { v.y = y; return v; }

        // ------------------------------------------------------------------ holes

        static void DressHole1(Dresser d, HoleFrame f, HoleDefinition def)
        {
            var root = new GameObject("Hole01_Dressing").transform;
            root.SetParent(d.Root, false);
            Vector3 start = f.L(0f, -0.3f);
            float FaceStart(Vector3 p) => Quaternion.LookRotation((p - start).WithY(0f)).eulerAngles.y;

            // Clubhouse tiki hut at the start, bar facing the tee.
            var hutPos = f.L(-4.4f, -1.6f);
            d.Place("TikiHut", d.Kit.Solid, hutPos, FaceStart(hutPos), 1f, collider: true, parent: root);
            d.Place("Barrel", d.Kit.Solid, f.L(-2.6f, -2.6f), 20f, 1f, collider: true, parent: root);
            d.Place("Crate0", d.Kit.Solid, f.L(-2.7f, -3.4f), 12f, 1f, collider: true, parent: root);
            d.Place("Crate1", d.Kit.Solid, f.L(-2.7f, -3.4f) + Vector3.up * 0.6f, 40f, 1f, snap: false, parent: root);
            d.Place("Barrel", d.Kit.Solid, f.L(1.8f, -1.9f), 70f, 0.9f, collider: true, parent: root);

            // Welcome / controls board and the hole sign.
            var signPos = f.L(-1.7f, -1.1f);
            var sign = d.Place("SignLarge", d.Kit.Solid, signPos, FaceStart(signPos), 1f, collider: true, parent: root);
            AddSignText(sign.transform, new Vector3(0f, 0.75f + 0.475f, -0.032f), new Vector2(1.4f, 0.88f), WelcomeText, 30);
            var holeSignPos = f.L(1.6f, 1.0f);
            var holeSign = d.Place("SignSmall", d.Kit.Solid, holeSignPos, FaceStart(holeSignPos), 1f, parent: root);
            AddSignText(holeSign.transform, new Vector3(0f, 0.65f + 0.225f, -0.032f), new Vector2(0.76f, 0.42f),
                $"<size=46><b>HOLE {def.number}</b></size>\n{def.name}  ·  Par {def.par}", 34);

            // Tiki torches lining the lane (set back from the start so they frame the view).
            foreach (var z in new[] { 1.8f, 4.5f, 7.3f })
            {
                d.Torch(f.L(-1.3f, z));
                d.Torch(f.L(1.3f, z));
            }

            // Pier from the start out over the lagoon: extend 6 m sections until it ends over water.
            float pierX0 = 1.7f, pierZ = -1.4f;
            var pierStart = f.L(pierX0, pierZ);
            float deckY = d.Ground(pierStart.x, pierStart.z) - 0.18f;
            float waterAt = 30f;
            for (float x = pierX0; x < pierX0 + 30f; x += 0.5f)
            {
                var p = f.L(x, pierZ);
                if (d.Ground(p.x, p.z) < -0.25f) { waterAt = x - pierX0; break; }
            }
            int sections = Mathf.Clamp(Mathf.CeilToInt((waterAt + 3f) / 6f), 1, 4);
            for (int s = 0; s < sections; s++)
            {
                var p = f.L(pierX0 + s * 6f, pierZ);
                d.Place("Boardwalk6", d.Kit.Solid, new Vector3(p.x, deckY, p.z), f.Yaw(90f), 1f, snap: false, collider: true, parent: root);
            }
            float pierLen = sections * 6f;
            var pierEnd = f.L(pierX0 + pierLen - 0.4f, pierZ);
            var side = Quaternion.Euler(0f, f.yaw, 0f) * Vector3.forward;
            d.Torch(new Vector3(pierEnd.x, deckY + 0.35f, pierEnd.z) + side * 0.55f);
            d.Torch(new Vector3(pierEnd.x, deckY + 0.35f, pierEnd.z) - side * 0.55f);
            var cratePos = f.L(pierX0 + pierLen - 1.4f, pierZ + 0.35f);
            d.Place("Crate1", d.Kit.Solid, new Vector3(cratePos.x, deckY + 0.35f, cratePos.z), 25f, 1f, snap: false, parent: root);
            var mid = f.L(pierX0 + pierLen * 0.5f, pierZ);
            d.KeepOut(new Vector2(mid.x, mid.z), new Vector2(0.9f, pierLen * 0.5f), f.Yaw(90f), 0.2f);

            // Palms: a row along the beach leaning toward the sea, a few inland.
            // Fewer, well-spaced palms so the lane stays visible; seaward ones lean out over the beach.
            var palms = new (float x, float z, int v, float lean)[]
            {
                (3.1f, 1.6f, 2, 95f), (3.9f, 5.0f, 3, 80f), (3.0f, 8.6f, 1, 105f),
                (-3.3f, 3.2f, 1, -80f), (-3.8f, 7.0f, 2, -110f), (-6.6f, -3.2f, 3, 200f),
            };
            foreach (var p in palms) d.Palm(p.v, f.L(p.x, p.z), f.Yaw(p.lean), 1f);

            // Sandstone cliffs and boulders form the inland backdrop.
            // Backdrop stays on Hole 1's own inland side (local z < 5); Hole 2 sits further along.
            d.Place("Cliff1", d.Kit.Solid, f.L(-7.6f, 0.6f), f.Yaw(15f), 1f, sink: 0.3f, collider: true, outOfBounds: true, parent: root);
            d.Place("Cliff0", d.Kit.Solid, f.L(-10.4f, 1.4f), f.Yaw(-30f), 1f, sink: 0.3f, collider: true, outOfBounds: true, parent: root);
            d.Place("Cliff2", d.Kit.Solid, f.L(-6.2f, -4.6f), f.Yaw(70f), 1f, sink: 0.3f, collider: true, outOfBounds: true, parent: root);
            d.Place("RockLarge0", d.Kit.Solid, f.L(-5.6f, 0.6f), f.Yaw(30f), 1f, sink: 0.35f, collider: true, outOfBounds: true, parent: root);
            d.Place("RockMedium0", d.Kit.Solid, f.L(-4.2f, 4.4f), f.Yaw(80f), 1f, sink: 0.3f, collider: true, outOfBounds: true, parent: root);
            d.Place("RockMedium1", d.Kit.Solid, f.L(5.2f, 4.6f), f.Yaw(10f), 0.9f, sink: 0.45f, collider: true, outOfBounds: true, parent: root);
            var rnd = new System.Random(101);
            for (int i = 0; i < 9; i++)
            {
                float x = Dresser.Range(rnd, 2.0f, 7.0f), z = Dresser.Range(rnd, -2.5f, 10f);
                var p = f.L(x, z);
                if (!d.IsFree(new Vector2(p.x, p.z))) continue;
                d.Place(rnd.NextDouble() < 0.5 ? "RockSmall0" : "RockSmall1", d.Kit.Solid, p, Dresser.Range(rnd, 0f, 360f), Dresser.Range(rnd, 0.7f, 1.4f), sink: 0.12f, parent: root);
            }

            // Lush planting along the inland rail, lighter beach planting on the sea side.
            string[] inland = { "BigLeaf1", "FlowerBush4", "Bush2", "FlowerBush0", "BigLeaf0", "FlowerBush6", "Bush3", "FlowerBush2", "BigLeaf2" };
            for (int i = 0; i < 9; i++)
            {
                float z = -0.2f + i * 1.0f;
                var p = f.L(-1.55f - (i % 2) * 0.35f, z);
                d.Place(inland[i], inland[i].StartsWith("Bush") || inland[i].StartsWith("Flower") ? d.Kit.Foliage : d.Kit.Foliage, p, Dresser.Range(rnd, 0f, 360f), Dresser.Range(rnd, 0.85f, 1.15f), parent: root);
            }
            foreach (var (x, z, m) in new[] { (1.5f, 2.6f, "FlowerBush3"), (1.7f, 3.4f, "FlowerBush5"), (1.6f, 5.6f, "Bush1"), (1.5f, 7.6f, "FlowerBush0"), (-1.0f, -2.4f, "FlowerBush1"), (0.9f, -2.3f, "FlowerBush6"), (-0.4f, -2.8f, "Bush0") })
                d.Place(m, d.Kit.Foliage, f.L(x, z), Dresser.Range(rnd, 0f, 360f), 1f, parent: root);
            for (int i = 0; i < 40; i++)
            {
                float sideSign = rnd.NextDouble() < 0.5 ? -1f : 1f;
                float x = sideSign * Dresser.Range(rnd, 0.95f, 2.6f), z = Dresser.Range(rnd, -2.5f, 8.5f);
                var p = f.L(x, z);
                if (Mathf.Abs(x) < 1.05f && z > -0.5f && z < 7.4f) continue;
                d.Place($"Grass{rnd.Next(0, 3)}", d.Kit.Foliage, p, Dresser.Range(rnd, 0f, 360f), Dresser.Range(rnd, 0.8f, 1.3f), shadows: false, parent: root);
            }
            // Rope fence along the dune line on the sea side.
            d.Place("RopeFence3", d.Kit.Solid, f.L(1.95f, 1.6f), f.Yaw(0f), 1f, parent: root);
        }

        static void DressHole2(Dresser d, HoleFrame f, HoleDefinition def)
        {
            var root = new GameObject("Hole02_Dressing").transform;
            root.SetParent(d.Root, false);
            Vector3 start = f.L(0f, -0.3f);
            var holeSignPos = f.L(1.15f, 0.15f);
            var holeSign = d.Place("SignSmall", d.Kit.Solid, holeSignPos, Quaternion.LookRotation((holeSignPos - start).WithY(0f)).eulerAngles.y, 1f, parent: root);
            AddSignText(holeSign.transform, new Vector3(0f, 0.65f + 0.225f, -0.032f), new Vector2(0.76f, 0.42f),
                $"<size=46><b>HOLE {def.number}</b></size>\n{def.name}  ·  Par {def.par}", 34);
            foreach (var (x, z, v) in new[] { (1.6f, 1.0f, 1), (1.8f, 4.2f, 3), (-1.4f, 1.8f, 2), (-4.6f, 3.0f, 0), (-2.2f, 6.4f, 2) })
                d.Palm(v, f.L(x, z), f.Yaw(Dresser.Range(new System.Random(x.GetHashCode()), 0f, 360f)), 1f);
            d.Torch(f.L(-1.05f, 0.6f));
            d.Torch(f.L(1.05f, 0.6f));
        }

        /// <summary>General island cover away from the holes.</summary>
        static void DressIsland(Dresser d)
        {
            var root = new GameObject("Island_Dressing").transform;
            root.SetParent(d.Root, false);
            var rnd = new System.Random(2026);
            var c = d.Island.centre;
            float r = d.Island.radius;
            d.Scatter(rnd, c, r * 0.95f, 46, (q, p) => { d.Palm(q.Next(0, 4), new Vector3(p.x, 0f, p.y), Dresser.Range(q, 0f, 360f), Dresser.Range(q, 0.85f, 1.15f)); return true; }, minHeight: 0.15f);
            d.Scatter(rnd, c, r * 0.9f, 60, (q, p) => { d.Place($"Bush{q.Next(0, 4)}", d.Kit.Foliage, new Vector3(p.x, 0f, p.y), Dresser.Range(q, 0f, 360f), Dresser.Range(q, 0.8f, 1.4f), parent: root); return true; }, minHeight: 0.3f);
            d.Scatter(rnd, c, r * 0.85f, 30, (q, p) => { d.Place($"BigLeaf{q.Next(0, 3)}", d.Kit.Foliage, new Vector3(p.x, 0f, p.y), Dresser.Range(q, 0f, 360f), Dresser.Range(q, 0.9f, 1.4f), parent: root); return true; }, minHeight: 0.35f);
            d.Scatter(rnd, c, r * 0.85f, 34, (q, p) => { d.Place($"FlowerBush{q.Next(0, 7)}", d.Kit.Foliage, new Vector3(p.x, 0f, p.y), Dresser.Range(q, 0f, 360f), Dresser.Range(q, 0.9f, 1.3f), parent: root); return true; }, minHeight: 0.35f);
            d.Scatter(rnd, c, r * 0.95f, 140, (q, p) => { d.Place($"Grass{q.Next(0, 3)}", d.Kit.Foliage, new Vector3(p.x, 0f, p.y), Dresser.Range(q, 0f, 360f), Dresser.Range(q, 0.8f, 1.4f), shadows: false, parent: root); return true; }, minHeight: 0.3f);
            d.Scatter(rnd, c, r * 1.02f, 26, (q, p) =>
            {
                string m = q.NextDouble() < 0.6 ? $"RockSmall{q.Next(0, 2)}" : $"RockMedium{q.Next(0, 2)}";
                d.Place(m, d.Kit.Solid, new Vector3(p.x, 0f, p.y), Dresser.Range(q, 0f, 360f), Dresser.Range(q, 0.7f, 1.5f), sink: 0.25f, collider: m.StartsWith("RockMedium"), outOfBounds: true, parent: root);
                return true;
            }, minHeight: -0.4f);
            d.Scatter(rnd, c, r * 0.6f, 4, (q, p) =>
            {
                d.Place($"Cliff{q.Next(0, 3)}", d.Kit.Solid, new Vector3(p.x, 0f, p.y), Dresser.Range(q, 0f, 360f), Dresser.Range(q, 0.8f, 1.2f), sink: 0.3f, collider: true, outOfBounds: true, parent: root);
                return true;
            }, minHeight: 0.6f);
            // Sandstone crags on the mountain slopes.
            d.Scatter(rnd, new Vector2(-2f, 9f), 12f, 4, (q, p) =>
            {
                d.Place($"Cliff{q.Next(0, 3)}", d.Kit.Solid, new Vector3(p.x, 0f, p.y), Dresser.Range(q, 0f, 360f), Dresser.Range(q, 0.9f, 1.5f), sink: 0.4f, collider: true, outOfBounds: true, parent: root);
                return true;
            }, minHeight: 2.2f);
            DressCoast(d, root);
        }

        /// <summary>Boulders and crags along the shoreline, some standing in the shallows.</summary>
        static void DressCoast(Dresser d, Transform root)
        {
            var rnd = new System.Random(77);
            for (float a = 0f; a < 360f; a += Dresser.Range(rnd, 9f, 20f))
            {
                var dir = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                // Walk outward to the waterline.
                Vector2 p = d.Island.centre;
                for (float r = 10f; r < 60f; r += 0.5f)
                {
                    p = d.Island.centre + dir * r;
                    if (d.Ground(p.x, p.y) < 0.05f) break;
                }
                p += dir * Dresser.Range(rnd, -1.5f, 2.5f);
                if (!d.IsFree(p)) continue;
                string m = rnd.NextDouble() < 0.25 ? "Cliff2" : rnd.NextDouble() < 0.5 ? "RockLarge0" : (rnd.NextDouble() < 0.5 ? "RockLarge1" : "RockMedium1");
                d.Place(m, d.Kit.Solid, new Vector3(p.x, 0f, p.y), Dresser.Range(rnd, 0f, 360f), Dresser.Range(rnd, 0.7f, 1.3f), sink: 0.5f, collider: true, outOfBounds: true, parent: root);
            }
        }

        // ------------------------------------------------------------------ helpers

        const string WelcomeText =
            "<size=46><b>WORLDS OF MINI GOLF</b></size>\n<size=36><i>Tropical Adventure</i></size>\n\n" +
            "<b>Putt:</b> swing through the ball   <b>A:</b> go to ball\n" +
            "<b>B:</b> ball back to last shot spot   <b>X:</b> scorecard\n" +
            "<b>Y (hold):</b> swap hands   <b>Stick:</b> teleport / turn\n" +
            "<b>Left grip:</b> drag to move   <b>Right grip + stick:</b> putter\n" +
            "<b>Look at your free wrist</b> for your score";

        static void AddSignText(Transform sign, Vector3 localPos, Vector2 sizeMetres, string text, int fontSize)
        {
            var canvas = WorldText.CreateCanvas("SignText", sign, sizeMetres * 1000f, new Color(0, 0, 0, 0));
            canvas.transform.localPosition = localPos;
            canvas.transform.localRotation = Quaternion.identity;
            var t = WorldText.CreateText(canvas.transform, "Text", fontSize, TextAnchor.MiddleCenter, new Color(0.22f, 0.11f, 0.04f));
            t.text = text;
            t.lineSpacing = 1.05f;
        }

        static (Vector2 centre, Vector2 half) LayoutBounds(GreenLayout l)
        {
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            foreach (var a in l.areas)
            {
                minX = Mathf.Min(minX, a.xMin); minZ = Mathf.Min(minZ, a.yMin);
                maxX = Mathf.Max(maxX, a.xMax); maxZ = Mathf.Max(maxZ, a.yMax);
            }
            return (new Vector2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f), new Vector2((maxX - minX) * 0.5f, (maxZ - minZ) * 0.5f));
        }

        static void AddPath(IslandGen island, Vector2 a, Vector2 b, float width, float height)
        {
            Vector2 d = b - a;
            float yaw = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
            island.zones.Add(new IslandGen.Zone
            {
                centre = (a + b) * 0.5f, halfSize = new Vector2(width * 0.5f, d.magnitude * 0.5f), yaw = yaw,
                height = height, feather = 1.2f, path = true,
            });
        }
    }
}
