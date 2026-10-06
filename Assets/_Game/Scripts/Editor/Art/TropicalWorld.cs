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
            /// <summary>World XZ to hole-local (x across the lane, z down the lane); inverse of <see cref="L2"/>.</summary>
            public Vector2 ToLocal2(Vector2 world)
            {
                Vector2 d = world - new Vector2(origin.x, origin.z);
                float rad = yaw * Mathf.Deg2Rad;
                return new Vector2(d.x * Mathf.Cos(rad) - d.y * Mathf.Sin(rad), d.x * Mathf.Sin(rad) + d.y * Mathf.Cos(rad));
            }
        }

        /// <summary>Hole 1 hero set-piece, in Hole 1's local frame (x across the lane, + = sea side; z down the lane).</summary>
        static readonly Vector2 CliffLocal = new Vector2(-9.5f, -2.5f);   // HeroCliffWall origin, front faces the lane (+x)
        static readonly Vector2 PoolLocal = new Vector2(-7.4f, 1.0f);     // plunge pool in front of the cliff
        const float PoolRadius = 2.0f, PoolDepth = 0.8f;
        const float CliffScale = 0.85f;
        /// <summary>Palm fronds lean toward Blender +X. Flip to -1 if the lean arrives mirrored after FBX import.</summary>
        const float PalmLeanSign = 1f;

        public static void Build(TropicalKit kit, HeroKit hero, List<HoleDefinition> defs, List<IslandCluster> clusters, Transform parent)
        {
            var frames = new List<HoleFrame>();
            foreach (var d in defs) frames.Add(new HoleFrame(d));
            bool IsJungle(HoleDefinition d) => d.cluster == TropicalCourse.JungleCluster;
            bool IsTemple(HoleDefinition d) => d.cluster == TropicalCourse.TempleCluster;
            bool IsVolcanic(HoleDefinition d) => d.cluster == TropicalCourse.VolcanicCluster;
            bool IsSummit(HoleDefinition d) => d.cluster == TropicalCourse.SummitCluster;
            bool OffStart(HoleDefinition d) => IsJungle(d) || IsTemple(d) || IsVolcanic(d) || IsSummit(d);   // holes that are not on the Starting Island

            // ---- Starting Island: holes 1-2 (unchanged from the approved visual baseline).
            var island = new IslandGen { centre = Vector2.zero, radius = 34f, seed = 7, splat = true };
            // Flatten every hole site (layout bounds + walk-around margin) to its green's ground height.
            for (int i = 0; i < defs.Count; i++)
            {
                if (OffStart(defs[i])) continue;
                var (centre, half) = LayoutBounds(defs[i]);
                var f = frames[i];
                island.zones.Add(new IslandGen.Zone
                {
                    centre = f.L2(centre.x, centre.y), halfSize = half + new Vector2(1.2f, 1.6f), yaw = f.yaw,
                    height = defs[i].origin.y - TropicalCourse.GreenElevation - 0.06f, feather = 3.5f,
                });
            }
            // Path from each cup to the next tee (within an island only; islands are linked by the pier and the hole transition).
            for (int i = 0; i + 1 < defs.Count; i++)
            {
                if (OffStart(defs[i]) || OffStart(defs[i + 1])) continue;
                var a = frames[i].L2(defs[i].layout.cup.Value.x, defs[i].layout.cup.Value.y + 1.2f);
                var b = frames[i + 1].L2(defs[i + 1].tee.x, defs[i + 1].tee.y - 1.2f);
                AddPath(island, a, b, 0.8f, Mathf.Min(defs[i].origin.y, defs[i + 1].origin.y) - TropicalCourse.GreenElevation - 0.08f);
            }
            // Raised backdrop behind Hole 1 for the cliffs.
            if (defs.Count > 0) island.mounds.Add((frames[0].L2(-8.5f, 0.5f), 7f, 1.6f));
            // Central jungle mountain: the island's landmark and backdrop from every hole.
            island.mounds.Add((new Vector2(-2f, 9f), 17f, 6.5f));
            island.mounds.Add((new Vector2(-12f, 1f), 9f, 2.6f));
            // Plunge pool carved into the backdrop mound (the water disc is added in DressHole1).
            if (defs.Count > 0) island.basins.Add((frames[0].L2(PoolLocal.x, PoolLocal.y), PoolRadius, PoolDepth));

            // ---- Jungle Island: holes 3-4 (only when the course has them).
            IslandGen jungle = null;
            for (int i = 0; i < defs.Count; i++)
            {
                if (!IsJungle(defs[i])) continue;
                if (jungle == null) jungle = JungleIsland.CreateIsland();
                JungleIsland.ConfigureTerrain(jungle, frames[i], defs[i]);
            }

            // ---- Temple Island: holes 5-6 (only when the course has them).
            IslandGen temple = null;
            for (int i = 0; i < defs.Count; i++)
            {
                if (!IsTemple(defs[i])) continue;
                if (temple == null) temple = TempleIsland.CreateIsland();
                TempleIsland.ConfigureTerrain(temple, frames[i], defs[i]);
            }

            // ---- Volcanic Island: holes 7-8 (only when the course has them).
            IslandGen volcanic = null;
            for (int i = 0; i < defs.Count; i++)
            {
                if (!IsVolcanic(defs[i])) continue;
                if (volcanic == null) volcanic = VolcanicIsland.CreateIsland();
                VolcanicIsland.ConfigureTerrain(volcanic, frames[i], defs[i]);
            }

            // ---- Summit Sanctuary island: hole 9 (only when the course has it).
            IslandGen summit = null;
            for (int i = 0; i < defs.Count; i++)
            {
                if (!IsSummit(defs[i])) continue;
                if (summit == null) summit = SummitIsland.CreateIsland();
                SummitIsland.ConfigureTerrain(summit, frames[i], defs[i]);
            }

            var world = new GameObject("World").transform;
            world.SetParent(parent, false);
            var islands = new List<IslandGen> { island };
            if (jungle != null) islands.Add(jungle);
            if (temple != null) islands.Add(temple);
            if (volcanic != null) islands.Add(volcanic);
            if (summit != null) islands.Add(summit);
            BuildTerrainAndSea(kit, hero, islands, world);
            SetupLighting(kit);

            // ---- Dressing: one Dresser per island (ground queries differ), one shared root so the obstruction scan covers everything.
            var dressingRoot = new GameObject("Dressing").transform;
            dressingRoot.SetParent(world, false);
            var dresser = new Dresser(kit, hero, island, dressingRoot);
            Dresser jungleDresser = jungle != null ? new Dresser(kit, hero, jungle, dressingRoot) : null;
            Dresser templeDresser = temple != null ? new Dresser(kit, hero, temple, dressingRoot) : null;
            Dresser volcanicDresser = volcanic != null ? new Dresser(kit, hero, volcanic, dressingRoot) : null;
            Dresser summitDresser = summit != null ? new Dresser(kit, hero, summit, dressingRoot) : null;
            for (int i = 0; i < defs.Count; i++)
            {
                var (centre, half) = LayoutBounds(defs[i]);
                var dr = IsJungle(defs[i]) ? jungleDresser : IsTemple(defs[i]) ? templeDresser : IsVolcanic(defs[i]) ? volcanicDresser : IsSummit(defs[i]) ? summitDresser : dresser;
                // Generous clearance: holes get hand-placed dressing; random island cover stays back.
                dr.KeepOut(frames[i].L2(centre.x, centre.y), half + new Vector2(0.9f, 1.2f), frames[i].yaw, 2.6f);
            }
            foreach (var z in island.zones) if (z.path) dresser.KeepOut(z.centre, z.halfSize, z.yaw, 0.2f);

            if (jungleDresser != null) JungleIsland.BuildPier(dresser, jungleDresser, dressingRoot);
            for (int i = 0; i < defs.Count; i++)
            {
                switch (defs[i].number)
                {
                    case 1: DressHole1(dresser, frames[i], defs[i]); break;
                    case 2: DressHole2(dresser, frames[i], defs[i]); break;
                    case 3: JungleIsland.DressHole3(jungleDresser, frames[i], defs[i]); break;
                    case 4: JungleIsland.DressHole4(jungleDresser, frames[i], defs[i]); break;
                    case 5: TempleIsland.DressHole5(templeDresser, frames[i], defs[i]); break;
                    case 6: TempleIsland.DressHole6(templeDresser, frames[i], defs[i]); break;
                    case 7: VolcanicIsland.DressHole7(volcanicDresser, frames[i], defs[i]); break;
                    case 8: VolcanicIsland.DressHole8(volcanicDresser, frames[i], defs[i]); break;
                    case 9: SummitIsland.DressHole9(summitDresser, frames[i], defs[i]); break;
                    default: Debug.LogWarning($"[Gamebreak] No dressing for hole {defs[i].number} yet."); break;
                }
            }
            DressIsland(dresser, defs.Count > 0 ? new Vector2(frames[0].origin.x, frames[0].origin.z) : Vector2.zero);
            if (jungleDresser != null)
                for (int i = 0; i < defs.Count; i++)
                    if (defs[i].number == 3) { JungleIsland.DressIsland(jungleDresser, frames[i]); break; }
            BuildHorizon(kit, island, world);
            RemoveObstructions(dressingRoot);
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

        static void BuildTerrainAndSea(TropicalKit kit, HeroKit hero, List<IslandGen> islands, Transform world)
        {
            for (int i = 0; i < islands.Count; i++)
            {
                string name = i == 0 ? "IslandTerrain" : $"IslandTerrain{i + 1}";   // the first keeps its historic name (probe, docs)
                var terrainMesh = islands[i].BuildTerrain();
                SaveMesh(terrainMesh, name);
                var terrain = new GameObject(name);
                terrain.transform.SetParent(world, false);
                terrain.AddComponent<MeshFilter>().sharedMesh = terrainMesh;
                var tr = terrain.AddComponent<MeshRenderer>();
                tr.sharedMaterial = islands[i].terrainMaterial ? islands[i].terrainMaterial : hero.Terrain; // TerrainSplat: vertex colour = layer weights (IslandGen.splat)
                tr.shadowCastingMode = ShadowCastingMode.On;
                terrain.AddComponent<MeshCollider>().sharedMesh = terrainMesh;
                terrain.AddComponent<OutOfBoundsSurface>();
                GameObjectUtility.SetStaticEditorFlags(terrain, StaticEditorFlags.BatchingStatic);
            }

            // One ocean for the whole archipelago, centred between the islands. Depth = the shallowest of the islands' own depths.
            Vector2 centre = Vector2.zero;
            foreach (var g in islands) centre += g.centre;
            centre /= islands.Count;
            float Depth(float x, float z)
            {
                float depth = 4f;
                foreach (var g in islands)
                    if (Mathf.Abs(x - g.centre.x) < g.extent && Mathf.Abs(z - g.centre.y) < g.extent)
                        depth = Mathf.Min(depth, -g.Height(x, z));
                return depth;
            }
            var oceanMesh = islands.Count == 1
                ? islands[0].BuildOcean()
                : islands[0].BuildOcean(centre, 480f, 110, 220, 1.7f, Depth);
            SaveMesh(oceanMesh, "Ocean");
            var ocean = new GameObject("Ocean");
            ocean.transform.SetParent(world, false);
            ocean.AddComponent<MeshFilter>().sharedMesh = oceanMesh;
            var orr = ocean.AddComponent<MeshRenderer>();
            orr.sharedMaterial = kit.Water;
            orr.shadowCastingMode = ShadowCastingMode.Off;
        }

        internal static void SaveMesh(Mesh mesh, string name)
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
            // (The 330 degree island moved to 30: the Temple Island now occupies that bearing at about 135 m. It then moved on to 75:
            // the Volcanic Island sits at about 128 m on the 20 degree bearing. It moved on to 160: the Summit island is at about 94 m on the 58 degree bearing.)
            float[] angles = { 200f, 245f, 290f, 160f, 120f };
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

        internal static Vector3 WithY(this Vector3 v, float y) { v.y = y; return v; }

        // ------------------------------------------------------------------ holes

        /// <summary>
        /// Hole 1 "Beach Warm-up". Composition, from the player's tee looking down the lane (left = inland, right = sea):
        /// foreground = sandstone rail, ground cover and shrubs; middle = bananas, ferns, boulders, hero palms and the tiki
        /// clubhouse; back = the terraced cliff wall with its waterfall and plunge pool, sandstone mesas behind it; lagoon side =
        /// beach palms and a sea arch standing in the water. Heroes come from Blender (HeroKit); the kit still supplies
        /// signs, torches, crates and the pier.
        /// </summary>
        /// <summary>
        /// Barrel / crate from the Blender hero props (HeroBarrel_0, HeroCrate_0/1), with a simple primitive collider so the ball
        /// does not catch on the stave grooves. Falls back to the procedural kit mesh when the FBX has not been generated.
        /// </summary>
        static GameObject HeroProp(Dresser d, string kitName, Vector3 pos, float yaw, float scale, bool collider, Transform parent)
        {
            string model = kitName == "Barrel" ? "HeroBarrel_0" : kitName == "Crate0" ? "HeroCrate_0" : "HeroCrate_1";
            if (!d.HasModel(model))
                return d.Place(kitName, d.Kit.Solid, pos, yaw, scale, snap: collider, collider: collider, parent: parent);
            bool snap = collider; // stacked / deck-placed props pass an explicit height
            var go = d.Model(model, pos, yaw, scale, snap: snap, sink: 0.02f, parent: parent);
            if (!collider || go == null) return go;
            if (kitName == "Barrel")
            {
                var cap = go.AddComponent<CapsuleCollider>();
                cap.radius = 0.31f; cap.height = 0.88f; cap.center = new Vector3(0f, 0.44f, 0f);
            }
            else
            {
                float s = kitName == "Crate0" ? 0.6f : 0.45f;
                var box = go.AddComponent<BoxCollider>();
                box.size = Vector3.one * s; box.center = new Vector3(0f, s * 0.5f, 0f);
            }
            return go;
        }

        static void DressHole1(Dresser d, HoleFrame f, HoleDefinition def)
        {
            var root = new GameObject("Hole01_Dressing").transform;
            root.SetParent(d.Root, false);
            Vector3 start = f.L(0f, -0.3f);
            float FaceStart(Vector3 p) => Quaternion.LookRotation((p - start).WithY(0f)).eulerAngles.y;
            var rnd = new System.Random(101);

            // Clubhouse site: behind the tee, well clear of the cliff, plunge pool/waterfall and Hole 2's tee (which lies at
            // roughly local x -5..-10, z 8..10). The first candidate whose whole deck is on dry land and out of the pool's way wins.
            var hutLocal = ChooseClubhouseSite(d, f, FaceStart);

            // Keep-outs for the set-piece volumes, registered first so every scatter below respects them.
            d.KeepOut(f.L2(hutLocal.x, hutLocal.y), new Vector2(3.0f, 3.0f), f.yaw, 0.4f);          // clubhouse deck (rotated footprint, conservative)
            d.KeepOut(f.L2(CliffLocal.x - 1.7f, CliffLocal.y), new Vector2(2.8f, 7.2f), f.yaw, 0.3f); // cliff wall volume
            d.KeepOut(f.L2(PoolLocal.x, PoolLocal.y), new Vector2(PoolRadius + 0.4f, PoolRadius + 0.4f), f.yaw, 0.2f);
            d.KeepOut(f.L2(-15.5f, 3f), new Vector2(3f, 3f), f.yaw, 0.5f);                          // mesas
            d.KeepOut(f.L2(-13.5f, -7.5f), new Vector2(3f, 3f), f.yaw, 0.5f);

            // Clubhouse tiki hut, front (model -Z) facing the tee. Deck is 4.6 x 3.6 m on 0.75 m stilts.
            var hutPos = f.L(hutLocal.x, hutLocal.y);
            d.Model("HeroTikiClubhouse", hutPos, FaceStart(hutPos), 1f, sink: 0.12f, colliders: true, parent: root);
            // Crates and barrel on the beach side of the tee (the old spot is under the new deck).
            HeroProp(d, "Barrel", f.L(0.9f, -2.7f), 20f, 1f, true, root);
            var bottomCrate = HeroProp(d, "Crate0", f.L(1.3f, -3.5f), 12f, 1f, true, root);
            // Stack the small crate on the big one's actual position (the terrain height differs from the hole's height there).
            var stackPos = bottomCrate.transform.position + Vector3.up * 0.607f;
            HeroProp(d, "Crate1", stackPos, 40f, 1f, false, root);
            HeroProp(d, "Barrel", f.L(2.3f, -3.0f), 70f, 0.9f, true, root); // the pier runs z -2.1..-0.7 from x 1.7: keep clear of the planks

            // Tiki poles beside the tee and the cup, and carved masks on stakes along the lane (they face the tee).
            Tiki(d, "HeroTikiPole_0", f.L(-2.8f, 0.9f), FaceStart(f.L(-2.8f, 0.9f)), root, 0.22f, 2.3f);
            Tiki(d, "HeroTikiPole_1", f.L(2.7f, 7.4f), FaceStart(f.L(2.7f, 7.4f)), root, 0.2f, 1.7f);
            Tiki(d, "HeroTikiMask_0", f.L(-1.9f, 3.3f), FaceStart(f.L(-1.9f, 3.3f)), root, 0.07f, 1.7f);
            Tiki(d, "HeroTikiMask_0", f.L(1.9f, 5.8f), FaceStart(f.L(1.9f, 5.8f)), root, 0.07f, 1.7f);

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
            HeroProp(d, "Crate1", new Vector3(cratePos.x, deckY + 0.58f, cratePos.z), 25f, 1f, false, root);
            var mid = f.L(pierX0 + pierLen * 0.5f, pierZ);
            d.KeepOut(new Vector2(mid.x, mid.z), new Vector2(0.9f, pierLen * 0.5f), f.Yaw(90f), 0.2f);

            // ---- Hero palms (3 Blender variants, 4.6 / 5.6 / 3.8 m). yawOff 0 leans toward the sea, 180 toward the jungle.
            var palms = new (float x, float z, int v, float yawOff, float s)[]
            {
                (3.5f, 1.4f, 2, 0f, 1f), (4.3f, 5.0f, 0, 15f, 1.05f), (3.7f, 8.8f, 1, -10f, 1f), (6.0f, 3.2f, 2, 25f, 1.1f),
                (-3.7f, 0.9f, 0, 180f, 1f), (-4.2f, 6.6f, 1, 165f, 1.05f), (-8.4f, -4.6f, 2, 200f, 1f),
            };
            foreach (var p in palms)
            {
                var pos = f.L(p.x, p.z);
                if (d.Ground(pos.x, pos.z) < 0.05f) continue; // would stand in the water
                float lean = f.yaw + p.yawOff + (PalmLeanSign < 0f ? 180f : 0f);
                d.Model($"HeroPalm_{p.v}", pos, lean, p.s, sink: 0.06f, parent: root, cull: 0.006f);
            }

            // ---- Backdrop: terraced cliff wall (16 m x 7 m, scaled), mesas behind it, boulders and the sea arch.
            var wallPos = f.L(CliffLocal.x, CliffLocal.y);
            // Model front is -Z; yaw -90 turns it toward local +x, i.e. facing the lane. Seated so the straight bottom edge never hovers over the mound's slope.
            var wall = d.Model("HeroCliffWall", wallPos, f.Yaw(-90f), CliffScale, sink: 0.35f, colliders: true, outOfBounds: true, parent: root,
                seat: true, lods: true, lodScale: 0.6f, seatExtraSink: 0.3f);
            // Mesas are skyline silhouettes: no colliders, no shadows, aggressive LODs.
            d.Model("HeroMesa_0", f.L(-15.5f, 3f), f.Yaw(25f), 1.05f, sink: 0.5f, shadows: false, parent: root, seat: true, lods: true, colliderLod: 0, seatExtraSink: 0.4f);
            d.Model("HeroMesa_1", f.L(-13.5f, -7.5f), f.Yaw(140f), 1f, sink: 0.5f, shadows: false, parent: root, seat: true, lods: true, seatExtraSink: 0.4f);
            // Boulders: stretched taller so they read as rounded stones rather than slabs, then seated on the ground.
            d.Model("HeroBoulders_0", f.L(-4.4f, -1.6f), f.Yaw(30f), 0.8f, sink: 0.1f, colliders: true, outOfBounds: true, parent: root, seat: true, lods: true, yStretch: 1.45f, seatExtraSink: 0.12f);
            d.Model("HeroBoulders_1", f.L(-3.9f, 3.3f), f.Yaw(-50f), 0.65f, sink: 0.1f, colliders: true, outOfBounds: true, parent: root, seat: true, lods: true, yStretch: 1.45f, seatExtraSink: 0.12f);
            d.Model("HeroBoulders_1", f.L(5.6f, 4.4f), f.Yaw(80f), 0.7f, sink: 0.1f, colliders: true, outOfBounds: true, parent: root, seat: true, lods: true, yStretch: 1.45f, seatExtraSink: 0.12f);
            PlaceSeaArch(d, f, root);

            // ---- Waterfall, plunge pool and mist.
            BuildWaterfall(d, f, root, wall);

            // ---- Layered jungle. Lane edge is at local x = +-0.6, so nothing taller than ground cover sits inside |x| < 1.2.
            bool Free(float x, float z, float minH = 0.12f)
            {
                var p = f.L(x, z);
                return d.IsFree(new Vector2(p.x, p.z)) && d.Ground(p.x, p.z) > minH;
            }
            float Yaw360() => Dresser.Range(rnd, 0f, 360f);
            // Layer 1: ground cover hugging the inland rail (and a thin dune strip on the beach side).
            for (int i = 0; i < 46; i++)
            {
                float x = -Dresser.Range(rnd, 1.25f, 2.7f), z = Dresser.Range(rnd, -2.5f, 8.6f);
                if (!Free(x, z)) continue;
                d.Leaf($"GrassClump{rnd.Next(0, 2)}", f.L(x, z), Yaw360(), Dresser.Range(rnd, 0.9f, 1.6f), shadows: false, parent: root, cull: 0.014f);
            }
            for (int i = 0; i < 22; i++)
            {
                float x = Dresser.Range(rnd, 1.35f, 3.0f), z = Dresser.Range(rnd, -0.5f, 9f);
                if (!Free(x, z, 0.22f)) continue;
                d.Leaf($"GrassClump{rnd.Next(0, 2)}", f.L(x, z), Yaw360(), Dresser.Range(rnd, 0.8f, 1.3f), shadows: false, parent: root);
            }
            // Layer 2: flowering shrubs and leaf bushes in a staggered band behind it.
            string[] shrubs = { "LeafBush0", "FlowerShrub0", "LeafBush1", "FlowerShrub1", "LeafBush2", "FlowerShrub2" };
            for (int i = 0; i < 12; i++)
            {
                float x = -(2.3f + (i % 2) * 0.55f + Dresser.Range(rnd, 0f, 0.3f)), z = -2.3f + i * 0.95f;
                if (!Free(x, z)) continue;
                d.Leaf(shrubs[i % shrubs.Length], f.L(x, z), Yaw360(), Dresser.Range(rnd, 1.0f, 1.4f), shadows: false, parent: root);
            }
            foreach (var (x, z, m) in new[] { (1.6f, 2.6f, "FlowerShrub1"), (1.9f, 5.6f, "LeafBush2"), (1.7f, 7.8f, "FlowerShrub0"), (-0.9f, -2.5f, "FlowerShrub2"), (0.9f, -2.4f, "FlowerShrub0") })
                if (Free(x, z, 0.2f)) d.Leaf(m, f.L(x, z), Yaw360(), 1f, shadows: false, parent: root);
            // Layer 3: bananas and big-leaf plants, taller and further back.
            string[] tall = { "Banana0", "BigLeaf1", "BigLeaf0", "Banana0", "BigLeaf1" };
            for (int i = 0; i < 9; i++)
            {
                float x = -Dresser.Range(rnd, 3.9f, 5.4f), z = -2.8f + i * 1.35f;
                if (!Free(x, z)) continue;
                d.Leaf(tall[i % tall.Length], f.L(x, z), Yaw360(), Dresser.Range(rnd, 0.85f, 1.2f), parent: root);
            }
            // Layer 4: ferns and shrubs filling the gap to the cliff (pool and boulders are keep-outs).
            for (int i = 0; i < 36; i++)
            {
                float x = -Dresser.Range(rnd, 4.2f, 8.6f), z = Dresser.Range(rnd, -4.5f, 5.0f);
                if (!Free(x, z)) continue;
                bool fern = rnd.NextDouble() < 0.6;
                d.Leaf(fern ? "Fern0" : shrubs[rnd.Next(0, shrubs.Length)], f.L(x, z), Yaw360(), Dresser.Range(rnd, 1.0f, fern ? 1.5f : 1.8f), shadows: false, parent: root);
            }
            // Layer 5: dense big foliage on the shoulders of the cliff and behind the clubhouse.
            for (int i = 0; i < 18; i++)
            {
                float x = -Dresser.Range(rnd, 6.5f, 12.5f), z = Dresser.Range(rnd, -9f, 5.5f);
                if (!Free(x, z, 0.2f)) continue;
                d.Leaf(rnd.NextDouble() < 0.5 ? "Banana0" : "LeafBush1", f.L(x, z), Yaw360(), Dresser.Range(rnd, 1.3f, 2.0f), shadows: false, parent: root, cull: 0.01f);
            }
            // Small stones along the beach side.
            for (int i = 0; i < 9; i++)
            {
                float x = Dresser.Range(rnd, 2.0f, 7.0f), z = Dresser.Range(rnd, -2.5f, 10f);
                var p = f.L(x, z);
                if (!d.IsFree(new Vector2(p.x, p.z))) continue;
                d.Rock("small", p, Yaw360(), Dresser.Range(rnd, 0.8f, 1.4f), root, variant: rnd.Next(0, 2));
            }
            // Rope fence along the dune line on the sea side.
            d.Place("RopeFence3", d.Kit.Solid, f.L(1.95f, 1.6f), f.Yaw(0f), 1f, parent: root);
        }

        /// <summary>
        /// Picks the clubhouse centre (hole-local x, z) behind the tee. A site is valid when every corner of the 5 x 4 m
        /// deck footprint is on dry land and free of keep-outs, is at least PoolRadius + 4 m from the plunge pool, and stays
        /// east of the cliff wall's volume. Falls back to the first candidate with a warning.
        /// </summary>
        static Vector2 ChooseClubhouseSite(Dresser d, HoleFrame f, System.Func<Vector3, float> faceStart)
        {
            var candidates = new[] { new Vector2(-2.6f, -6.2f), new Vector2(-3.4f, -6.4f), new Vector2(-1.8f, -6.8f), new Vector2(-4.0f, -6.0f), new Vector2(-0.8f, -6.8f) };
            var rot = Quaternion.Euler(0f, f.yaw, 0f);
            Vector3 right = rot * Vector3.right;
            foreach (var c in candidates)
            {
                var centre = f.L(c.x, c.y);
                var yawRot = Quaternion.Euler(0f, faceStart(centre), 0f);
                bool ok = (c - PoolLocal).magnitude > PoolRadius + 4f;
                foreach (var (sx, sz) in new[] { (1f, 1f), (1f, -1f), (-1f, 1f), (-1f, -1f), (0f, 0f) })
                {
                    var corner = centre + yawRot * new Vector3(sx * 2.5f, 0f, sz * 2.0f);
                    if (d.Ground(corner.x, corner.z) < 0.15f || !d.IsFree(new Vector2(corner.x, corner.z))) ok = false;
                    if (Vector3.Dot(corner - f.origin, right) < CliffLocal.x + 3f) ok = false;
                }
                if (ok) { Debug.Log($"[Gamebreak] Clubhouse site: hole-local ({c.x:F1}, {c.y:F1})."); return c; }
            }
            Debug.LogWarning("[Gamebreak] No clear clubhouse site found; using the first candidate. Check the layout.");
            return candidates[0];
        }

        /// <summary>Sea arch standing in the lagoon off Hole 1. Legs sink 1.2 m below the model origin; we seat them on the seabed.</summary>
        static void PlaceSeaArch(Dresser d, HoleFrame f, Transform root)
        {
            const float z = 3.5f;
            float x = 9f;
            for (float tx = 8f; tx < 26f; tx += 0.25f)
            {
                var c = f.L(tx, z);
                if (d.Ground(c.x, c.z) < -0.6f) { x = tx; break; }
            }
            // Model X (span axis) -> local z, so the arch is seen face-on from the lane. Legs at +-3.5 m along the span.
            var legA = f.L(x, z - 3.5f); var legB = f.L(x, z + 3.5f);
            float seabed = Mathf.Min(d.Ground(legA.x, legA.z), d.Ground(legB.x, legB.z));
            var pos = f.L(x, z);
            pos.y = seabed + 1.2f - 0.15f;
            // The arch is the fidelity standard for the level: LOD0 stays active out to a long distance (lodScale 0.45).
            d.Model("HeroSeaArch", pos, f.Yaw(90f), 1f, snap: false, shadows: false, parent: root, lods: true, lodScale: 0.45f);
        }

        // ------------------------------------------------------------------ waterfall

        /// <summary>
        /// Builds the sheet, plunge pool water and mist for Hole 1. The sheet follows the actual cliff surface: rays are cast
        /// from the lane side into the wall's MeshColliders, so it hugs whatever the Blender model really looks like.
        /// If the wall cannot be hit (collider or placement problem) a flat fallback plane is used and a warning is logged.
        /// </summary>
        static void BuildWaterfall(Dresser d, HoleFrame f, Transform root, GameObject wall)
        {
            var hero = d.Hero;
            var rot = Quaternion.Euler(0f, f.yaw, 0f);
            Vector3 right = rot * Vector3.right, fwd = rot * Vector3.forward;
            Vector3 P(float along, float lane, float up) => f.origin + right * along + fwd * lane + Vector3.up * up;

            // --- Pool water level: just under the lowest point of the rim so every bank stays above the surface.
            var pc = f.L2(PoolLocal.x, PoolLocal.y);
            float rim = float.MaxValue;
            for (int i = 0; i < 16; i++)
            {
                float a = i / 16f * Mathf.PI * 2f;
                rim = Mathf.Min(rim, d.Ground(pc.x + Mathf.Cos(a) * PoolRadius, pc.y + Mathf.Sin(a) * PoolRadius));
            }
            float waterY = rim - 0.06f;

            // --- Raycast the cliff face for the sheet's path: rows from the lip down to the pool.
            Physics.SyncTransforms();
            // Actual extents of the seated wall (renderer bounds), not the nominal model size.
            Bounds wb = default; bool wbSet = false;
            foreach (var wr in wall.GetComponentsInChildren<MeshRenderer>())
                if (!wr.name.StartsWith("LOD")) { if (wbSet) wb.Encapsulate(wr.bounds); else { wb = wr.bounds; wbSet = true; } }
            float baseY = wbSet ? wb.min.y : d.Ground(P(CliffLocal.x, CliffLocal.y, 0f).x, P(CliffLocal.x, CliffLocal.y, 0f).z);
            float topScan = wbSet ? wb.max.y + 0.2f : baseY + 7f * CliffScale + 0.6f;
            float lz = PoolLocal.y;             // sheet centre along the lane
            const float half0 = 0.45f, half1 = 0.8f;
            float CliffX(float hy, float hz)
            {
                var o = P(-4f, hz, hy);
                var hits = Physics.RaycastAll(o, -right, 8f, ~0, QueryTriggerInteraction.Ignore);
                float best = float.NaN;
                foreach (var h in hits)
                {
                    if (!h.collider.transform.IsChildOf(wall.transform)) continue;
                    float lx = Vector3.Dot(h.point - f.origin, right);
                    if (float.IsNaN(best) || lx > best) best = lx; // outermost surface toward the lane
                }
                return best;
            }
            // Lip: highest scan height where the wall is hit; the sheet starts a little below it.
            float lipY = float.NaN;
            for (float y = topScan; y > baseY + 1f; y -= 0.1f)
                if (!float.IsNaN(CliffX(y, lz))) { lipY = y - 0.25f; break; }
            bool fallback = float.IsNaN(lipY);
            if (fallback)
            {
                Debug.LogWarning("[Gamebreak] Waterfall: cliff wall not hit by raycasts; using a flat fallback sheet. Check HeroCliffWall placement/colliders.");
                lipY = baseY + (topScan - baseY) * 0.8f;
            }

            const int rows = 28;
            var xs = new float[rows + 1];
            for (int r = 0; r <= rows; r++)
            {
                float y = Mathf.Lerp(lipY, waterY, r / (float)rows);
                float best = float.NaN;
                if (!fallback)
                    foreach (var dz in new[] { -half1, 0f, half1 })
                    {
                        float v = CliffX(y, lz + dz);
                        if (!float.IsNaN(v) && (float.IsNaN(best) || v > best)) best = v;
                    }
                xs[r] = float.IsNaN(best) ? (r > 0 ? xs[r - 1] : CliffLocal.x + 0.5f) : best;
            }
            // Smooth the surface (max-filter, then average) so ledges do not make the sheet zig-zag.
            var sm = new float[rows + 1];
            for (int r = 0; r <= rows; r++)
            {
                float m = xs[r];
                for (int k = -2; k <= 2; k++) m = Mathf.Max(m, xs[Mathf.Clamp(r + k, 0, rows)]);
                sm[r] = m;
            }
            var mb = new MeshBuilder();
            var rowIdx = new int[rows + 1][];
            for (int r = 0; r <= rows; r++)
            {
                float t = r / (float)rows;
                float lx = sm[r] + 0.1f + 0.12f * t;           // stand off the rock; bows out toward the plunge
                float half = Mathf.Lerp(half0, half1, t * t);
                float y = Mathf.Lerp(lipY, waterY - 0.04f, t);
                rowIdx[r] = new int[3];
                for (int c = 0; c < 3; c++)
                {
                    float u = c * 0.5f;
                    float bow = (1f - Mathf.Abs(u - 0.5f) * 2f) * 0.05f;   // centre column slightly proud
                    rowIdx[r][c] = mb.Vertex(P(lx + bow, lz + (u - 0.5f) * 2f * half, y), right, new Vector2(u, t), Color.white);
                }
                if (r > 0)
                    for (int c = 0; c < 2; c++)
                        mb.Quad(rowIdx[r - 1][c], rowIdx[r][c], rowIdx[r][c + 1], rowIdx[r - 1][c + 1]);
            }
            var sheetMesh = mb.ToMesh("Waterfall");
            SaveMesh(sheetMesh, "Waterfall");
            var sheet = new GameObject("WaterfallSheet");
            sheet.transform.SetParent(root, false);
            sheet.AddComponent<MeshFilter>().sharedMesh = sheetMesh;
            var sr = sheet.AddComponent<MeshRenderer>();
            sr.sharedMaterial = hero.Waterfall;
            sr.shadowCastingMode = ShadowCastingMode.Off;
            sr.receiveShadows = false;
            sr.lightProbeUsage = LightProbeUsage.Off;
            sr.reflectionProbeUsage = ReflectionProbeUsage.Off;

            // --- Pool water disc. Vertex colour R = baked depth / 2.2 m, as for the ocean (drives the no-depth fallback).
            const int rings = 5, sectors = 32;
            var pb = new MeshBuilder();
            for (int r = 0; r <= rings; r++)
            for (int s = 0; s <= sectors; s++)
            {
                float rad = PoolRadius * 1.02f * r / rings, a = s / (float)sectors * Mathf.PI * 2f;
                float x = pc.x + Mathf.Cos(a) * rad, z = pc.y + Mathf.Sin(a) * rad;
                float depth = Mathf.Max(0f, waterY - d.Ground(x, z));
                pb.Vertex(new Vector3(x, waterY, z), Vector3.up, Vector2.zero, new Color(Mathf.Clamp01(depth / 2.2f), 0f, 0f, 1f));
            }
            for (int r = 0; r < rings; r++)
            for (int s = 0; s < sectors; s++)
            {
                int a = r * (sectors + 1) + s, b = a + 1, c = a + sectors + 1, e = c + 1;
                pb.Triangle(a, b, e);
                pb.Triangle(a, e, c);
            }
            var poolMesh = pb.ToMesh("PoolWater");
            SaveMesh(poolMesh, "PoolWater");
            var pool = new GameObject("PlungePool");
            pool.transform.SetParent(root, false);
            pool.AddComponent<MeshFilter>().sharedMesh = poolMesh;
            var pr = pool.AddComponent<MeshRenderer>();
            pr.sharedMaterial = hero.PoolWater;
            pr.shadowCastingMode = ShadowCastingMode.Off;
            pr.receiveShadows = false;
            pr.lightProbeUsage = LightProbeUsage.Off;
            pr.reflectionProbeUsage = ReflectionProbeUsage.Off;

            // --- Mist at the plunge: few, large, slow billboards (low overdraw for VR).
            var mist = new GameObject("WaterfallMist");
            mist.transform.SetParent(root, false);
            mist.transform.SetPositionAndRotation(P(sm[rows] + 0.5f, lz, waterY + 0.25f), rot);
            var ps = mist.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.prewarm = true; main.playOnAwake = true;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3f);
            main.startSpeed = 0.05f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.5f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(1f, 1f, 1f, 0.5f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 40;
            var em = ps.emission; em.enabled = true; em.rateOverTime = 14f;
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(1.0f, 0.15f, 1.6f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = 0.15f; vel.y = 0.35f; vel.z = 0f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var szo = ps.sizeOverLifetime; szo.enabled = true;
            szo.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.4f));
            var prn = mist.GetComponent<ParticleSystemRenderer>();
            prn.renderMode = ParticleSystemRenderMode.Billboard;
            prn.sharedMaterial = hero.Mist;
            prn.shadowCastingMode = ShadowCastingMode.Off;
            prn.receiveShadows = false;
            prn.lightProbeUsage = LightProbeUsage.Off;
            prn.reflectionProbeUsage = ReflectionProbeUsage.Off;
            Debug.Log($"[Gamebreak] Waterfall: lip y {lipY:F2}, pool water y {waterY:F2}, sheet {(fallback ? "FALLBACK plane" : "raycast-fitted")}.");
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
                d.Model($"HeroPalm_{v % 3}", f.L(x, z), f.Yaw(Dresser.Range(new System.Random(x.GetHashCode()), 0f, 360f)), 1f, sink: 0.06f, parent: root, cull: 0.006f);
            d.Torch(f.L(-1.05f, 0.6f));
            d.Torch(f.L(1.05f, 0.6f));

            // Tiki poles framing the dogleg corner and the cup, masks along the first lane.
            float Face(Vector3 p) => Quaternion.LookRotation((p - start).WithY(0f)).eulerAngles.y;
            foreach (var (x, z, model, r, h) in new[]
            {
                (2.6f, 2.8f, "HeroTikiPole_0", 0.22f, 2.3f), (-2.7f, 2.2f, "HeroTikiPole_1", 0.2f, 1.7f),
                (1.7f, 5.9f, "HeroTikiMask_0", 0.07f, 1.7f), (-4.2f, 5.9f, "HeroTikiMask_0", 0.07f, 1.7f),
            })
                Tiki(d, model, f.L(x, z), Face(f.L(x, z)), root, r, h);
        }

        /// <summary>
        /// A Blender tiki pole or mask (HeroTikiPole_0/1, HeroTikiMask_0) with a slim capsule collider. Positions are hand-picked beside the
        /// lanes (inside each hole's own clearance zone, so the random-cover check is not used); skipped with a warning only when the spot is
        /// underwater, and the green-overlap safety net still removes any prop that lands on a lane.
        /// </summary>
        internal static void Tiki(Dresser d, string model, Vector3 pos, float faceYaw, Transform parent, float radius, float height)
        {
            if (!d.HasModel(model)) return;
            if (d.Ground(pos.x, pos.z) < 0.15f)
            {
                Debug.LogWarning($"[Gamebreak] Tiki '{model}' skipped at {pos:F1}: ground is below water level.");
                return;
            }
            // The FBX export (forward -Z, up Y) puts a Blender -Y front on Unity +Z, so turn the model half a circle to face the viewer.
            var go = d.Model(model, pos, faceYaw + 180f, 1f, sink: 0.04f, parent: parent);
            if (go == null) return;
            var cap = go.AddComponent<CapsuleCollider>();
            cap.radius = radius; cap.height = height; cap.center = new Vector3(0f, height * 0.5f, 0f);
        }

        /// <summary>General island cover away from the holes.</summary>
        /// <summary>Palms near Hole 1 (what the player actually sees) are the Blender hero palms; far ones stay on the cheap kit palms.</summary>
        static void PalmAt(Dresser d, Transform root, Vector2 focus, int variant, Vector3 pos, float yaw, float scale)
        {
            if ((new Vector2(pos.x, pos.z) - focus).magnitude < HeroPalmRadius && d.Ground(pos.x, pos.z) > 0.05f)
                d.Model($"HeroPalm_{variant % 3}", pos, yaw, scale, sink: 0.06f, parent: root, cull: 0.006f);
            else d.Palm(variant, pos, yaw, scale);
        }

        const float HeroPalmRadius = 26f;

        static void DressIsland(Dresser d, Vector2 focus)
        {
            var root = new GameObject("Island_Dressing").transform;
            root.SetParent(d.Root, false);
            var rnd = new System.Random(2026);
            var c = d.Island.centre;
            float r = d.Island.radius;
            d.Scatter(rnd, c, r * 0.95f, 46, (q, p) => { PalmAt(d, root, focus, q.Next(0, 4), new Vector3(p.x, 0f, p.y), Dresser.Range(q, 0f, 360f), Dresser.Range(q, 0.85f, 1.15f)); return true; }, minHeight: 0.15f);
            d.Scatter(rnd, c, r * 0.9f, 60, (q, p) => { d.Place($"Bush{q.Next(0, 4)}", d.Kit.Foliage, new Vector3(p.x, 0f, p.y), Dresser.Range(q, 0f, 360f), Dresser.Range(q, 0.8f, 1.4f), parent: root); return true; }, minHeight: 0.3f);
            d.Scatter(rnd, c, r * 0.85f, 30, (q, p) => { d.Place($"BigLeaf{q.Next(0, 3)}", d.Kit.Foliage, new Vector3(p.x, 0f, p.y), Dresser.Range(q, 0f, 360f), Dresser.Range(q, 0.9f, 1.4f), parent: root); return true; }, minHeight: 0.35f);
            d.Scatter(rnd, c, r * 0.85f, 34, (q, p) => { d.Place($"FlowerBush{q.Next(0, 7)}", d.Kit.Foliage, new Vector3(p.x, 0f, p.y), Dresser.Range(q, 0f, 360f), Dresser.Range(q, 0.9f, 1.3f), parent: root); return true; }, minHeight: 0.35f);
            d.Scatter(rnd, c, r * 0.95f, 140, (q, p) => { d.Place($"Grass{q.Next(0, 3)}", d.Kit.Foliage, new Vector3(p.x, 0f, p.y), Dresser.Range(q, 0f, 360f), Dresser.Range(q, 0.8f, 1.4f), shadows: false, parent: root); return true; }, minHeight: 0.3f);
            d.Scatter(rnd, c, r * 1.02f, 26, (q, p) =>
            {
                bool small = q.NextDouble() < 0.6;
                d.Rock(small ? "small" : "medium", new Vector3(p.x, 0f, p.y), Dresser.Range(q, 0f, 360f), Dresser.Range(q, 0.7f, 1.5f), root,
                    collider: !small, outOfBounds: true, variant: q.Next(0, 2));
                return true;
            }, minHeight: -0.4f);
            d.Scatter(rnd, c, r * 0.6f, 4, (q, p) =>
            {
                d.Rock("large", new Vector3(p.x, 0f, p.y), Dresser.Range(q, 0f, 360f), Dresser.Range(q, 0.5f, 0.75f), root, collider: true, outOfBounds: true, variant: q.Next(0, 2));
                return true;
            }, minHeight: 0.6f);
            // Sandstone crags on the mountain slopes.
            d.Scatter(rnd, new Vector2(-2f, 9f), 12f, 4, (q, p) =>
            {
                d.Rock("large", new Vector3(p.x, 0f, p.y), Dresser.Range(q, 0f, 360f), Dresser.Range(q, 0.55f, 0.9f), root, collider: true, outOfBounds: true, variant: q.Next(0, 2));
                return true;
            }, minHeight: 2.2f);
            DressCoast(d, root);
        }

        /// <summary>Boulders and crags along the shoreline, some standing in the shallows.</summary>
        internal static void DressCoast(Dresser d, Transform root)
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
                bool large = rnd.NextDouble() < 0.6;
                d.Rock(large ? "large" : "medium", new Vector3(p.x, 0f, p.y), Dresser.Range(rnd, 0f, 360f),
                    large ? Dresser.Range(rnd, 0.4f, 0.7f) : Dresser.Range(rnd, 0.8f, 1.3f), root, collider: true, outOfBounds: true, variant: rnd.Next(0, 2));
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

        internal static void AddSignText(Transform sign, Vector3 localPos, Vector2 sizeMetres, string text, int fontSize)
        {
            var canvas = WorldText.CreateCanvas("SignText", sign, sizeMetres * 1000f, new Color(0, 0, 0, 0));
            canvas.transform.localPosition = localPos;
            canvas.transform.localRotation = Quaternion.identity;
            var t = WorldText.CreateText(canvas.transform, "Text", fontSize, TextAnchor.MiddleCenter, new Color(0.22f, 0.11f, 0.04f));
            t.text = text;
            t.lineSpacing = 1.05f;
        }

        /// <summary>Bounds of the lane and of any pieces hung off it (<see cref="HoleDefinition.extraAreas"/>, e.g. Hole 4's bowl).</summary>
        internal static (Vector2 centre, Vector2 half) LayoutBounds(HoleDefinition def) => LayoutBounds(def.layout, def.extraAreas);

        internal static (Vector2 centre, Vector2 half) LayoutBounds(GreenLayout l, IEnumerable<Rect> extra = null)
        {
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            void Grow(Rect a)
            {
                minX = Mathf.Min(minX, a.xMin); minZ = Mathf.Min(minZ, a.yMin);
                maxX = Mathf.Max(maxX, a.xMax); maxZ = Mathf.Max(maxZ, a.yMax);
            }
            foreach (var a in l.areas) Grow(a);
            if (extra != null) foreach (var a in extra) Grow(a);
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
