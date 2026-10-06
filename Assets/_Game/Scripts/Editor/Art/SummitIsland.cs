using UnityEditor;
using UnityEngine;
using HoleFrame = Gamebreak.MiniGolf.Editor.Art.TropicalWorld.HoleFrame;

namespace Gamebreak.MiniGolf.Editor.Art
{
    /// <summary>
    /// Summit Sanctuary island (cluster "summit", Hole 9). Functional stage dressing only (Graphics Pass 3 comes after all nine holes play): a levelled plateau,
    /// plain stone masonry that makes the tiers and the Sky Bridge read as a ridge over a drop, a stepped sanctuary with pillars and an arch behind the Altar, a
    /// tall summit cliff, waterfalls into the chasm, a cloud bank under the bridge, torches, warm lights and a low sun disc as the sunset cue. Everything here is
    /// decorative: no colliders, nothing on a green. No particles, no vegetation, no final sky treatment.
    /// </summary>
    public static class SummitIsland
    {
        public static IslandGen CreateIsland()
        {
            var c = TropicalCourse.SummitCentre;
            var g = new IslandGen { centre = c, radius = 24f, seed = 53, hillHeight = 4.5f, extent = 34f, splat = false, skipDeepSea = true };
            LoadMaterials();
            g.terrainMaterial = Mat("Summit_Ground", new Color(0.30f, 0.29f, 0.28f), 0.05f);   // bare mountain rock all round the course (the lawn splat made it a grassy platform)
            g.mounds.Add((c + new Vector2(8f, 14f), 11f, 8.0f));     // the summit cliff behind the sanctuary
            g.mounds.Add((c + new Vector2(-14f, 4f), 8f, 3.5f));
            return g;
        }

        public static void ConfigureTerrain(IslandGen island, HoleFrame f, HoleDefinition def)
        {
            var (centre, half) = TropicalWorld.LayoutBounds(def);
            float baseY = def.origin.y - TropicalCourse.GreenElevation - 0.06f;
            island.zones.Add(new IslandGen.Zone
            {
                centre = f.L2(centre.x, centre.y), halfSize = half + new Vector2(3.5f, 3.5f), yaw = f.yaw,
                height = baseY - 0.25f, feather = 10f, plateau = true,
            });
        }

        static Material s_Stone, s_StoneDark, s_Water, s_Cloud, s_Gold, s_Sun;

        static Material Mat(string name, Color color, float smoothness = 0.12f, float emission = 0f)
        {
            string path = $"{TropicalKit.Root}/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (!shader) shader = Shader.Find("Standard");
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            if (emission > 0f && m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * emission); }
            EditorUtility.SetDirty(m);
            return m;
        }

        static void LoadMaterials()
        {
            s_Stone = Mat("Summit_Stone", new Color(0.82f, 0.78f, 0.7f));
            s_StoneDark = Mat("Summit_StoneDark", new Color(0.55f, 0.52f, 0.48f));
            s_Water = Mat("Summit_Water", new Color(0.55f, 0.85f, 0.95f), 0.7f, 0.5f);
            s_Cloud = Mat("Summit_Cloud", new Color(0.97f, 0.95f, 0.95f), 0.1f, 0.35f);
            s_Gold = Mat("Summit_Gold", new Color(0.97f, 0.78f, 0.3f), 0.5f, 0.8f);
            s_Sun = Mat("Summit_Sun", new Color(1f, 0.62f, 0.25f), 0.2f, 2.5f);
            if (Gp3Materials.Ready) { s_Stone = Gp3Materials.Limestone; s_StoneDark = Gp3Materials.Sandstone; }
        }

        static GameObject Block(Transform root, HoleFrame f, string name, float x, float z, float sizeX, float sizeZ, float y0, float y1, Material mat, float yawOffset = 0f)
        {
            if (y1 - y0 < 0.01f) return null;
            var go = ChamferMesh.Block(name, root, f.L(x, z) + Vector3.up * ((y0 + y1) * 0.5f), Quaternion.Euler(0f, f.yaw + yawOffset, 0f), new Vector3(sizeX, y1 - y0, sizeZ), Mathf.Min(0.04f, Mathf.Min(sizeX, y1 - y0, sizeZ) * 0.2f), mat, 1.6f);
            return go;
        }

        static void HoleSign(Dresser d, Transform root, HoleFrame f, HoleDefinition def, float x, float z)
        {
            Vector3 start = f.L(0f, -0.3f);
            var pos = f.L(x, z);
            var sign = d.Place("SignSmall", d.Kit.Solid, pos, Quaternion.LookRotation((pos - start).WithY(0f)).eulerAngles.y, 1f, parent: root);
            TropicalWorld.AddSignText(sign.transform, new Vector3(0f, 0.65f + 0.225f, -0.032f), new Vector2(0.76f, 0.42f),
                $"<size=46><b>HOLE {def.number}</b></size>\n{def.name}  ·  Par {def.par}", 34);
        }

        /// <summary>A hero model placed in hole space (x, z on the ground plane, y a hole-space height); faceLocalYaw 270 turns the front to face -x.</summary>
        static GameObject HeroAt(Dresser d, Transform root, HoleFrame f, string model, float x, float z, float y, float faceLocalYaw, float scale = 1f)
        {
            if (!d.HasModel(model)) return null;
            var pos = f.L(x, z); pos.y = f.origin.y + y;
            return d.Model(model, pos, f.Yaw(faceLocalYaw), scale, snap: false, parent: root);
        }

        static void KeepOuts(Dresser d, HoleFrame f, HoleDefinition def)
        {
            foreach (var r in def.layout.areas) d.KeepOut(f.L2(r.center.x, r.center.y), r.size * 0.5f, f.yaw, 1.2f);
            foreach (var r in def.extraAreas) d.KeepOut(f.L2(r.center.x, r.center.y), r.size * 0.5f, f.yaw, 0.8f);
        }

        static void SkinHazards(int holeNumber)
        {
            foreach (var hole in Object.FindObjectsByType<HoleController>(FindObjectsSortMode.None))
            {
                if (hole.HoleNumber != holeNumber) continue;
                foreach (var r in hole.GetComponentsInChildren<MeshRenderer>(true))
                    if (r.transform.parent && r.transform.parent.name == "Hazards")
                        r.sharedMaterial = r.name == "Abyss" ? s_Cloud : s_Water;
            }
        }

        static void Glow(Transform root, Vector3 world, float range, float intensity, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = world;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, 0.68f, 0.38f); l.range = range; l.intensity = intensity; l.shadows = LightShadows.None;
        }

        public static void DressHole9(Dresser d, HoleFrame f, HoleDefinition def)
        {
            LoadMaterials();
            var root = new GameObject("Hole09_Dressing").transform;
            root.SetParent(d.Root, false);
            KeepOuts(d, f, def);
            var s = TropicalCourse.Hole9Spec();
            SkinHazards(9);
            HoleSign(d, root, f, def, s.Left - 0.9f, 1.0f);

            // Masonry under every level so the climb reads as one stepped mountain (tops sit below the surfaces).
            Block(root, f, "Body_Approach", 0f, s.ApproachEndZ * 0.5f, s.Width - 0.1f, s.ApproachEndZ - 0.2f, -0.8f, -0.03f, s_StoneDark);
            Block(root, f, "Body_Terrace1", (s.Left + s.Terrace1EastX) * 0.5f, (s.ApproachEndZ + s.Terrace1EndZ) * 0.5f, s.Terrace1EastX - s.Left - 0.1f, s.Terrace1EndZ - s.ApproachEndZ - 0.2f, -0.8f, s.T1 - 0.08f, s_Stone);
            Block(root, f, "Body_Overlook", (s.Left + s.OverlookEastX) * 0.5f, (s.OverlookZ + s.ClimbEndZ) * 0.5f, s.OverlookEastX - s.Left - 0.1f, s.ClimbEndZ - s.OverlookZ - 0.2f, -0.8f, s.OverlookHeight - 0.1f, s_Stone);
            const int steps = 4;
            for (int k = 0; k < steps; k++)
            {
                float z0 = s.Terrace1EndZ + (s.ClimbEndZ - s.Terrace1EndZ) * k / steps, z1 = s.Terrace1EndZ + (s.ClimbEndZ - s.Terrace1EndZ) * (k + 1) / steps;
                Block(root, f, $"Body_Climb{k}", (s.ClimbWestX + s.ClimbEastX) * 0.5f, (z0 + z1) * 0.5f, s.ClimbEastX - s.ClimbWestX - 0.1f, z1 - z0 - 0.04f, -0.8f, s.T1 + (s.MergeHeight - s.T1) * k / steps - 0.03f, s_Stone);
            }
            Block(root, f, "Body_Merge", (s.Left + s.Terrace1EastX) * 0.5f, (s.ClimbEndZ + s.MergeEndZ) * 0.5f, s.Terrace1EastX - s.Left - 0.1f, s.MergeEndZ - s.ClimbEndZ - 0.2f, -0.8f, s.MergeHeight - 0.08f, s_Stone);
            Block(root, f, "Body_Shrine", (s.ShrineWestX + s.ShrineEastX) * 0.5f, (s.MergeEndZ + s.ShrineTopZ) * 0.5f, s.ShrineEastX - s.ShrineWestX - 0.1f, s.ShrineTopZ - s.MergeEndZ - 0.2f, -0.8f, s.MergeHeight - 0.08f, s_Stone);
            Block(root, f, "Body_Landing", (s.ShrineEastX + s.LandingEndX) * 0.5f, (s.FinalZ0 + s.FinalZ1) * 0.5f, s.LandingEndX - s.ShrineEastX - 0.1f, s.FinalZ1 - s.FinalZ0 - 0.1f, -0.8f, s.SummitLevel - 0.08f, s_Stone);
            // The Sky Bridge: a narrow stone spine, much narrower than the green, standing out of the cloud.
            Block(root, f, "Body_BridgeSpine", (s.LandingEndX + s.BridgeEndX) * 0.5f, (s.FinalZ0 + s.FinalZ1) * 0.5f, s.BridgeEndX - s.LandingEndX, 0.5f, -3.0f, s.SummitLevel - 0.1f, s_StoneDark);
            Block(root, f, "Body_Altar", (s.AltarWestX + s.AltarEastX) * 0.5f, (s.AltarZ0 + s.AltarZ1) * 0.5f, s.AltarEastX - s.AltarWestX - 0.1f, s.AltarZ1 - s.AltarZ0 - 0.1f, -3.0f, s.AltarHeight - 0.08f, s_Stone);

            // The sanctuary (Graphics Pass 3B): a modelled sun-gate temple behind the Altar and a ceremonial gate over the Landing. Both stand wholly beyond the playable
            // surfaces: the sanctuary's stair starts 0.25 m past the Altar's east rail, and the gate's piers stand outside the lane with the arch crown well above head height.
            float ax = (s.AltarWestX + s.AltarEastX) * 0.5f, az = (s.AltarZ0 + s.AltarZ1) * 0.5f, ay = s.AltarHeight;
            var sanctuary = HeroAt(d, root, f, "HeroSanctuary", s.AltarEastX + 0.25f + 1.65f, az, ay - 0.04f, 270f);
            d.Footing(sanctuary, root, s_StoneDark, 0.1f, 0.5f);
            d.Grounded(sanctuary, root, Gp3Materials.Ready ? Dresser.RockVariant(Gp3Materials.RockLimestone, "Summit_Rock", 0.88f) : null, sanctuary ? sanctuary.transform.forward : Vector3.zero, 14, 0.7f);
            HeroAt(d, root, f, "HeroSummitGate", s.LandingEndX, az, s.SummitLevel - 0.03f, 270f);

            // The summit cliff: tall, exposed rock beyond the sanctuary and along the east side of the mountain.
            // The summit massif (Graphics Pass 3B): the rock kit as layered cliffs behind the sanctuary and along the approach, spires rising out of the cloud sea.
            var rockMat = Gp3Materials.Ready ? Dresser.RockVariant(Gp3Materials.RockLimestone, "Summit_Rock", 0.88f) : null;
            var rr = new System.Random(905);
            string[] cliffs = { "HeroCliff_A", "HeroCliff_B", "HeroCliff_C" };
            for (int i = 0; i < 9; i++)   // the high backdrop behind the sanctuary (x beyond the stair and the pillars)
            {
                float zz = az - 9.5f + i * 2.35f + (float)(rr.NextDouble() - 0.5) * 0.8f, xx = s.AltarEastX + 9.6f + (float)rr.NextDouble() * 2.5f;
                d.Crag(cliffs[i % 3], f.L(xx, zz), (float)rr.NextDouble() * 360f, 1.5f + (float)rr.NextDouble() * 0.9f, rockMat, root, lods: true, extraSink: 0.2f);
            }
            for (int i = 0; i < 6; i++)   // a second, taller rank further back so the mountain climbs
                d.Crag(cliffs[(i + 1) % 3], f.L(s.AltarEastX + 14.5f + (float)rr.NextDouble() * 2f, az - 6f + i * 2.4f), (float)rr.NextDouble() * 360f, 2.3f + (float)rr.NextDouble() * 0.8f, rockMat, root, lods: true, extraSink: 0.3f);
            for (int i = 0; i < 6; i++)   // the west wall along the climb and the Shrine Lane
                d.Crag(cliffs[i % 3], f.L(s.Left - 5.6f - (float)rr.NextDouble() * 1.4f, 4.5f + i * 2.8f), (float)rr.NextDouble() * 360f, 1.0f + (float)rr.NextDouble() * 0.45f, rockMat, root, lods: true, extraSink: 0.2f);
            for (int i = 0; i < 14; i++)  // boulders and stones at the feet of the cliffs
            {
                float a = (float)rr.NextDouble();
                var pos = i % 2 == 0 ? f.L(s.Left - 2.2f - (float)rr.NextDouble() * 1.2f, 2f + a * 18f) : f.L(s.AltarEastX + 3.2f + (float)rr.NextDouble() * 1.5f, az - 7f + a * 14f);
                d.Crag(i % 3 == 0 ? "HeroRock_A" : i % 3 == 1 ? "HeroRock_C" : "HeroStone_C", pos, (float)rr.NextDouble() * 360f, 0.9f + (float)rr.NextDouble() * 0.7f, rockMat, root, lods: false);
            }
            // The ground round the summit course: bare rock, scree, ledges and hardy plants instead of lawn, so the platform is built into a mountain (kept clear of every green).
            bool Clear9(float lx, float lz, float margin)
            {
                foreach (var a in def.layout.areas) if (lx > a.xMin - margin && lx < a.xMax + margin && lz > a.yMin - margin && lz < a.yMax + margin) return false;
                foreach (var a in def.extraAreas) if (lx > a.xMin - margin && lx < a.xMax + margin && lz > a.yMin - margin && lz < a.yMax + margin) return false;
                return lx < s.AltarEastX - 0.2f || lx > s.AltarEastX + 3.6f;   // keep the sanctuary footprint free
            }
            string[] ground = { "HeroRock_A", "HeroRock_C", "HeroStone_C", "HeroRock_B", "HeroStone_A", "HeroCliff_C" };
            string[] plants = { "Fern0", "LeafBush2", "GrassClump1", "FlowerShrub2", "BigLeaf0" };
            int placedRocks = 0, tries = 0;
            while (placedRocks < 70 && tries++ < 900)
            {
                float lx = Mathf.Lerp(s.Left - 8.5f, s.AltarEastX + 8f, (float)rr.NextDouble()), lz = Mathf.Lerp(-4f, 24f, (float)rr.NextDouble());
                if (!Clear9(lx, lz, 1.6f)) continue;
                var wp = f.L(lx, lz);
                if (d.Ground(wp.x, wp.z) < 0.3f) continue;
                bool big = rr.NextDouble() < 0.15;
                if (big) d.Crag("HeroCliff_C", wp, (float)rr.NextDouble() * 360f, 0.45f + (float)rr.NextDouble() * 0.35f, rockMat, root, lods: true, extraSink: 0.25f);
                else d.Crag(ground[rr.Next(ground.Length - 1)], wp, (float)rr.NextDouble() * 360f, 0.6f + (float)rr.NextDouble() * 1.0f, rockMat, root, lods: false);
                placedRocks++;
                if (rr.NextDouble() < 0.5) d.Leaf(plants[rr.Next(plants.Length)], wp + new Vector3(0.5f, 0f, 0.3f), (float)rr.NextDouble() * 360f, 0.8f + (float)rr.NextDouble() * 0.5f, parent: root);
            }
            for (int i = 0; i < 7; i++)   // spires standing out of the cloud sea around the island
            {
                float ang = (i / 7f) * Mathf.PI * 2f + 0.4f, dist = 38f + (float)rr.NextDouble() * 14f;
                var c = d.Island.centre; var sp = new Vector3(c.x + Mathf.Cos(ang) * dist, -1.2f, c.y + Mathf.Sin(ang) * dist);
                if (TropicalCourse.Clusters().Exists(cl => cl.id != TropicalCourse.SummitCluster && Vector2.Distance(new Vector2(sp.x, sp.z), cl.centre) < cl.radius + 16f)) continue;
                var go = d.Crag(i % 2 == 0 ? "HeroSpire_A" : "HeroSpire_B", sp, (float)rr.NextDouble() * 360f, 1.2f + (float)rr.NextDouble() * 0.8f, rockMat, root, lods: true, ground: false);
            }

            // Water: a waterfall pouring into the chasm from the cliff face, a spill over its south end, and a cloud bank under the Sky Bridge.
            float chx = (s.OverlookEastX + s.ClimbWestX) * 0.5f;
            Block(root, f, "Waterfall_Head", chx, s.ClimbEndZ + 0.2f, 0.7f, 0.4f, -0.05f, 1.6f, s_Water);
            Block(root, f, "Waterfall_Spill", chx, s.Terrace1EndZ - 0.15f, 0.7f, 0.3f, -2.5f, -0.05f, s_Water);
            Block(root, f, "CloudBank", (s.LandingEndX + s.BridgeEndX) * 0.5f, (s.FinalZ0 + s.FinalZ1) * 0.5f, 5.0f, 4.0f, -1.5f, -0.35f, s_Cloud);
            Block(root, f, "CloudBankFar", s.BridgeEndX + 1.0f, az + 4.5f, 5.0f, 3.5f, -1.8f, -0.6f, s_Cloud);

            // The sunset cue: a low, warm sun disc over the sea beyond the Altar, and warm lights on the Altar, the bridge and the climb.
            var sun = ProvingKit.AxialCylinder("SunDisc", root, Vector3.zero, Quaternion.Euler(0f, f.yaw + 90f, 0f), 4.5f, 0.2f, s_Sun);
            sun.transform.position = f.L(s.AltarEastX + 45f, az - 6f) + Vector3.up * 6f;
            Glow(root, f.L(ax, az) + Vector3.up * 1.2f, 6f, 2.4f, "AltarGlow");
            Glow(root, f.L((s.LandingEndX + s.BridgeEndX) * 0.5f, (s.FinalZ0 + s.FinalZ1) * 0.5f) + Vector3.up * 1.0f, 5f, 1.6f, "BridgeGlow");
            Glow(root, f.L(s.ClimbEastX, (s.Terrace1EndZ + s.ClimbEndZ) * 0.5f) + Vector3.up * 1.0f, 5f, 1.2f, "ClimbGlow");

            foreach (float z in new[] { 1.5f, 4.0f }) { d.Torch(f.L(s.Left - 0.6f, z)); d.Torch(f.L(-s.Left + 0.6f, z)); }
            d.Torch(f.L(s.Terrace1EastX + 0.6f, s.Terrace1EndZ + 1.0f));
            d.Torch(f.L(s.Left - 0.6f, s.MergeEndZ + 0.6f));
            d.Torch(f.L(s.Left - 0.6f, s.ShrineTopZ - 0.6f));
        }
    }
}
