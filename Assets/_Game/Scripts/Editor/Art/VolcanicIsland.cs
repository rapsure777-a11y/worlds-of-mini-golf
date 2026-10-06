using System.Linq;
using UnityEditor;
using UnityEngine;
using HoleFrame = Gamebreak.MiniGolf.Editor.Art.TropicalWorld.HoleFrame;

namespace Gamebreak.MiniGolf.Editor.Art
{
    /// <summary>
    /// Volcanic Island (cluster "volcanic", Holes 7-8). Functional stage dressing only (Graphics Pass 3 comes after all nine holes play): dark basalt terrain
    /// with a levelled plateau under each hole, plain basalt masonry that makes the tiers and the crater read from the tee, the hole's lava boxes re-skinned as
    /// glowing lava, a lava-falls cliff beside Hole 7's final green, a rim of crater blocks round Hole 8's bowl, a few big silhouettes, torches, a few
    /// warm lights so the route reads, and a hole sign. Everything here is decorative: no colliders, nothing on a green. No particles, no vegetation.
    /// </summary>
    public static class VolcanicIsland
    {
        // ------------------------------------------------------------------ terrain

        public static IslandGen CreateIsland()
        {
            var c = TropicalCourse.VolcanicCentre;
            var g = new IslandGen { centre = c, radius = 26f, seed = 41, hillHeight = 4.0f, extent = 36f, splat = false, skipDeepSea = true };
            LoadMaterials();
            g.terrainMaterial = s_Basalt;
            g.mounds.Add((c + new Vector2(-3f, 15f), 11f, 8.5f));    // the volcano: a big dark silhouette behind both holes
            g.mounds.Add((c + new Vector2(17f, -4f), 7f, 3.5f));
            g.mounds.Add((c + new Vector2(-16f, -6f), 7f, 3.0f));
            return g;
        }

        /// <summary>Flattens a plateau under the hole. The tiers and the climb stand on masonry above it, so the terrain stays flat and below every surface.</summary>
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

        // ------------------------------------------------------------------ materials and small helpers

        static Material s_Basalt, s_BasaltDark, s_Lava, s_LavaFall, s_Ember;

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
            s_Basalt = Mat("Volcanic_Basalt", new Color(0.19f, 0.18f, 0.2f));
            s_BasaltDark = Mat("Volcanic_BasaltDark", new Color(0.11f, 0.105f, 0.12f));
            s_Lava = Mat("Volcanic_Lava", new Color(1f, 0.38f, 0.07f), 0.3f, 1.6f);
            s_LavaFall = Mat("Volcanic_LavaFall", new Color(1f, 0.55f, 0.12f), 0.3f, 2.0f);
            s_Ember = Mat("Volcanic_Ember", new Color(1f, 0.25f, 0.05f), 0.2f, 1.2f);
        }

        /// <summary>A decorative block (no collider) from hole space: x/z centre, y span (hole-space heights), size across and along.</summary>
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

        /// <summary>Re-skins the hole's lava boxes (children of "Lava", built by the spec) with the glowing lava material.</summary>
        static void SkinLava(int holeNumber)
        {
            foreach (var hole in Object.FindObjectsByType<HoleController>(FindObjectsSortMode.None))
            {
                if (hole.HoleNumber != holeNumber) continue;
                foreach (var r in hole.GetComponentsInChildren<MeshRenderer>(true))
                    if (r.transform.parent && r.transform.parent.name == "Lava") r.sharedMaterial = s_Lava;
            }
        }

        /// <summary>A warm point light: enough glow to read the route, no shadows (cheap on the Steam Frame).</summary>
        static void Glow(Transform root, Vector3 world, float range, float intensity, string name = "LavaGlow")
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = world;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, 0.5f, 0.2f); l.range = range; l.intensity = intensity; l.shadows = LightShadows.None;
        }

        // ------------------------------------------------------------------ hole 7: Lava Falls

        public static void DressHole7(Dresser d, HoleFrame f, HoleDefinition def)
        {
            LoadMaterials();
            var root = new GameObject("Hole07_Dressing").transform;
            root.SetParent(d.Root, false);
            KeepOuts(d, f, def);
            var s = TropicalCourse.Hole7Spec();
            SkinLava(7);
            HoleSign(d, root, f, def, s.PadHalf + 0.9f, 0.9f);

            // Masonry under the tiers and the causeway so they read as stepped rock, not floating decks (tops sit below every surface).
            float t2 = s.Tier2Height, t3 = s.Tier3Height;
            Block(root, f, "Body_Tier1", (s.CrossEastX - s.PadHalf) * 0.5f, s.CrossEndZ * 0.5f, s.CrossEastX + s.PadHalf - 0.1f, s.CrossEndZ - 0.2f, -0.6f, -0.03f, s_Basalt);
            const int steps = 4;
            for (int k = 0; k < steps; k++)
            {
                float z0 = s.CrossEndZ + s.CausewayRun * k / steps, z1 = s.CrossEndZ + s.CausewayRun * (k + 1) / steps;
                Block(root, f, $"Body_Causeway{k}", s.Mouth.x, (z0 + z1) * 0.5f, s.CausewayWidth - 0.1f, z1 - z0 - 0.04f, -0.6f, t2 * k / steps - 0.03f, s_Basalt);
            }
            Block(root, f, "Body_Tier2", (s.Tier2WestX + s.CrossEastX) * 0.5f, (s.RampEndZ + s.Tier2EndZ) * 0.5f, s.CrossEastX - s.Tier2WestX - 0.1f, s.Tier2EndZ - s.RampEndZ - 0.2f, -0.6f, t2 - 0.08f, s_Basalt);
            Block(root, f, "Body_Tier3", (s.Tier3WestX + s.CrossEastX) * 0.5f, (s.Tier3FrontZ + s.Tier3EndZ) * 0.5f, s.CrossEastX - s.Tier3WestX - 0.1f, s.Tier3EndZ - s.Tier3FrontZ - 0.2f, -0.6f, t3 - 0.08f, s_Basalt);

            // Lava Falls: a dark cliff beside the final green, with glowing falls pouring down its face into the river.
            float cliffX = s.CrossEastX + 1.5f;
            Block(root, f, "Falls_Cliff", cliffX + 1.5f, (s.Tier2EndZ + s.Tier3EndZ) * 0.5f, 3.0f, s.Tier3EndZ - s.Tier2EndZ + 1.0f, -0.6f, 4.2f, s_BasaltDark);
            Block(root, f, "Falls_Stream", cliffX - 0.04f, (s.Tier2EndZ + s.Tier3FrontZ) * 0.5f, 0.1f, 1.3f, -0.1f, 3.9f, s_LavaFall);
            Block(root, f, "Falls_StreamTwo", cliffX - 0.04f, s.Tier3FrontZ + 2.2f, 0.1f, 0.8f, t3 - 0.1f, 3.4f, s_LavaFall);
            Block(root, f, "Falls_Splash", cliffX - 0.35f, (s.Tier2EndZ + s.Tier3FrontZ) * 0.5f, 0.9f, 1.8f, -0.2f, -0.05f, s_Lava);
            Glow(root, f.L(cliffX - 0.8f, (s.Tier2EndZ + s.Tier3FrontZ) * 0.5f) + Vector3.up * 0.9f, 7f, 2.2f, "FallsGlow");
            Glow(root, f.L(0.3f, (s.CrossEndZ + s.RampEndZ) * 0.5f) + Vector3.up * 0.4f, 5f, 1.6f, "LakeGlow");
            Glow(root, f.L(s.Tier3WestX - 0.8f, s.Tier3FrontZ + 2.6f) + Vector3.up * t3, 4f, 1.4f, "PoolGlow");
            // A glowing stone ring over the mouth so the receiver reads from the causeway.
            Block(root, f, "MouthGlow", s.Mouth.x, s.Mouth.y, s.dishRadius * 2f + 0.1f, s.dishRadius * 2f + 0.1f, s.Tier2Height - s.dishDepth - 0.012f, s.Tier2Height - s.dishDepth - 0.006f, s_Ember);

            // A few silhouettes: tall basalt spires on the far side of the river, and torches along the tiers.
            foreach (var (x, z, h) in new[] { (-3.2f, s.Tier2EndZ + 1.0f, 4.5f), (-3.8f, s.Tier3FrontZ + 1.5f, 6.0f), (6.2f, s.Tier3EndZ + 0.5f, 5.2f) })
                Block(root, f, "Spire", x, z, 1.4f, 1.4f, -0.6f, h, s_BasaltDark, 20f);
            foreach (float z in new[] { 1.0f, 3.0f })
                d.Torch(f.L(-s.PadHalf - 0.6f, z));
            d.Torch(f.L(s.CrossEastX + 0.6f, s.CrossEndZ + 1.0f));
            d.Torch(f.L(s.CrossEastX + 0.6f, s.Tier2EndZ - 0.5f));
        }

        // ------------------------------------------------------------------ hole 8: Caldera Run

        public static void DressHole8(Dresser d, HoleFrame f, HoleDefinition def)
        {
            LoadMaterials();
            var root = new GameObject("Hole08_Dressing").transform;
            root.SetParent(d.Root, false);
            KeepOuts(d, f, def);
            var s = TropicalCourse.Hole8Spec();
            HoleSign(d, root, f, def, s.jump.ramp.Width * 0.5f + 0.9f, 0.9f);

            // Masonry under the tee lane, the crater floor, the climb and the gate lane (tops below every surface).
            Block(root, f, "Body_Tee", 0f, s.jump.ramp.LipZ * 0.5f, s.jump.ramp.Width - 0.1f, s.jump.ramp.LipZ - 0.2f, -0.6f, -0.03f, s_Basalt);
            Block(root, f, "Body_Floor", (s.WestX - s.TeeHalf) * 0.5f, (s.routeZ0 + s.routeZ1) * 0.5f, -s.TeeHalf - s.WestX - 0.1f, s.routeZ1 - s.routeZ0 - 0.1f, -0.6f, -0.03f, s_Basalt);
            const int steps = 4;
            float cx = (s.WestX + s.ColumnEastX) * 0.5f, cz0 = s.routeZ1, cz1 = s.GateLaneZ1;
            for (int k = 0; k < steps; k++)
            {
                float z0 = Mathf.Lerp(cz0, cz1, k / (float)steps), z1 = Mathf.Lerp(cz0, cz1, (k + 1) / (float)steps);
                float top = s.GateLevel * Mathf.Clamp01((z0 - s.climbStartZ) / (s.climbEndZ - s.climbStartZ));
                Block(root, f, $"Body_Climb{k}", cx, (z0 + z1) * 0.5f, s.ColumnEastX - s.WestX - 0.1f, z1 - z0 - 0.04f, -0.6f, top - 0.03f, s_Basalt);
            }
            Block(root, f, "Body_Gate", (s.ColumnEastX + s.GateLaneEndX) * 0.5f, (s.GateLaneZ0 + s.GateLaneZ1) * 0.5f, s.GateLaneEndX - s.ColumnEastX - 0.1f, s.GateLaneZ1 - s.GateLaneZ0 - 0.1f, -0.6f, s.GateLevel - 0.03f, s_Basalt);

            // The caldera: a rim of big dark blocks round the whole hole, rising toward the north (the volcano side), so the course sits in a crater.
            Vector2 c = s.BowlCentre;
            const int rim = 22; const float rimR = 8.0f;
            for (int i = 0; i < rim; i++)
            {
                float a = 360f * i / rim, ra = a * Mathf.Deg2Rad;
                float north = Mathf.Clamp01(0.5f + 0.5f * Mathf.Sin(ra));
                float h = 0.9f + 2.6f * north + 0.4f * ((i * 7) % 3);
                Block(root, f, "Rim", c.x + Mathf.Cos(ra) * rimR * 1.15f, c.y + Mathf.Sin(ra) * rimR, 2.6f, 1.6f, -0.6f, h, i % 2 == 0 ? s_BasaltDark : s_Basalt, -a + 90f);
            }
            // Lava seams: glowing patches on the crater floor outside the rails, and a glow so the climb and the bowl read.
            foreach (var (x, z, w, l) in new[] { (s.WestX - 1.6f, 3.0f, 1.0f, 3.2f), (s.WestX - 1.4f, 7.0f, 1.2f, 1.8f), (c.x + s.BowlRadius + 1.4f, c.y - 1.5f, 1.2f, 2.4f) })
                Block(root, f, "LavaSeam", x, z, w, l, -0.55f, -0.38f, s_Lava);
            Glow(root, f.L(s.WestX - 1.2f, 3.0f) + Vector3.up * 0.5f, 6f, 1.8f, "SeamGlow");
            Glow(root, f.L(c.x, c.y) + Vector3.up * 2.5f, 7f, 1.0f, "BowlGlow");
            // Torches: the tee, the foot of the climb, the gate and the ramp's lip.
            d.Torch(f.L(-s.TeeHalf - 0.5f, 0.2f)); d.Torch(f.L(s.TeeHalf + 0.5f, 0.2f));
            d.Torch(f.L(s.WestX - 0.5f, s.routeZ1 + 0.5f));
            d.Torch(f.L(s.ColumnEastX + 0.5f, s.GateLaneZ0 - 0.6f));
        }
    }
}
