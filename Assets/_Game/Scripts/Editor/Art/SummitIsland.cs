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
            var g = new IslandGen { centre = c, radius = 24f, seed = 53, hillHeight = 4.5f, extent = 34f, splat = true, skipDeepSea = true };
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
        }

        static GameObject Block(Transform root, HoleFrame f, string name, float x, float z, float sizeX, float sizeZ, float y0, float y1, Material mat, float yawOffset = 0f)
        {
            if (y1 - y0 < 0.01f) return null;
            var go = ProvingKit.Box(name, root, Vector3.zero, Quaternion.Euler(0f, f.yaw + yawOffset, 0f), new Vector3(sizeX, y1 - y0, sizeZ), mat, collider: false);
            go.transform.position = f.L(x, z) + Vector3.up * ((y0 + y1) * 0.5f);   // f.L already includes the hole origin's height
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

            // The sanctuary above and behind the Altar: stepped tiers, pillars, an arch and a golden disc, so the destination is the highest thing in sight.
            float ax = (s.AltarWestX + s.AltarEastX) * 0.5f, az = (s.AltarZ0 + s.AltarZ1) * 0.5f, ay = s.AltarHeight;
            Block(root, f, "Sanctuary_Tier1", ax + 2.2f, az, 3.2f, 5.2f, -3.0f, ay + 1.2f, s_Stone);
            Block(root, f, "Sanctuary_Tier2", ax + 2.6f, az, 2.4f, 3.8f, ay + 1.2f, ay + 2.4f, s_Stone);
            Block(root, f, "Sanctuary_Tier3", ax + 3.0f, az, 1.6f, 2.4f, ay + 2.4f, ay + 3.4f, s_Stone);
            var disc = ProvingKit.AxialCylinder("SanctuaryDisc", root, Vector3.zero, Quaternion.Euler(0f, f.yaw + 90f, 0f), 0.9f, 0.12f, s_Gold);
            disc.transform.position = f.L(ax + 2.1f, az) + Vector3.up * (ay + 2.0f);
            foreach (float dz in new[] { -1.6f, 1.6f })
            {
                Block(root, f, "AltarPillar", s.AltarEastX + 0.3f, az + dz, 0.4f, 0.4f, ay, ay + 2.0f, s_Stone);
                Block(root, f, "BridgePillar", s.LandingEndX, az + dz * 0.75f, 0.35f, 0.35f, s.SummitLevel, s.SummitLevel + 1.7f, s_Stone);
            }
            Block(root, f, "BridgeLintel", s.LandingEndX, az, 0.4f, 3.0f, s.SummitLevel + 1.7f, s.SummitLevel + 1.95f, s_Stone);   // the arch that frames the bridge

            // The summit cliff: tall, exposed rock beyond the sanctuary and along the east side of the mountain.
            Block(root, f, "SummitCliff", ax + 6.5f, az + 1.0f, 5.0f, 12f, -3.0f, ay + 6.5f, s_StoneDark);
            Block(root, f, "WestCliff", s.Left - 2.6f, 12f, 3.0f, 14f, -3.0f, 3.2f, s_StoneDark);

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
