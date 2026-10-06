using UnityEditor;
using UnityEngine;
using HoleFrame = Gamebreak.MiniGolf.Editor.Art.TropicalWorld.HoleFrame;

namespace Gamebreak.MiniGolf.Editor.Art
{
    /// <summary>
    /// Temple Island (cluster "temple", Holes 5-6). Functional stage dressing only (Graphics Pass 3 comes after all nine holes play): a levelled
    /// plateau for each hole, plain stone masonry that makes the terraces read as a ziggurat, a stepped temple mass with a sun disc behind the Sun Stair so the
    /// destination is visibly above the player, torches, a hole sign and a bare mill house. Everything here is decorative: no colliders, nothing on a green.
    /// </summary>
    public static class TempleIsland
    {
        // ------------------------------------------------------------------ terrain

        public static IslandGen CreateIsland()
        {
            var c = TropicalCourse.TempleCentre;
            var g = new IslandGen { centre = c, radius = 24f, seed = 23, hillHeight = 3.5f, extent = 34f, splat = true, skipDeepSea = true };
            g.mounds.Add((c + new Vector2(-4f, 18f), 10f, 3.0f));   // a backdrop hill behind both holes
            g.mounds.Add((c + new Vector2(14f, 6f), 8f, 2.0f));
            return g;
        }

        /// <summary>Flattens a plateau under the hole. The terraces and the mill's elevated parts stand on masonry above it, so the terrain stays flat and below every surface.</summary>
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

        static Material s_Stone, s_StoneDark, s_Gold;

        static Material Mat(string name, Color color, float smoothness = 0.15f, bool emissive = false)
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
            if (emissive && m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * 0.6f); }
            EditorUtility.SetDirty(m);
            return m;
        }

        static void LoadMaterials()
        {
            s_Stone = Mat("Temple_Stone", new Color(0.78f, 0.68f, 0.5f));
            s_StoneDark = Mat("Temple_StoneDark", new Color(0.58f, 0.5f, 0.38f));
            s_Gold = Mat("Temple_Gold", new Color(0.95f, 0.75f, 0.25f), 0.5f, true);
            // Graphics Pass 3B: real textured stone (block UVs are in metres, so the masonry texture keeps its scale on every block).
            if (Gp3Materials.Ready) { s_Stone = Gp3Materials.Limestone; s_StoneDark = Gp3Materials.Sandstone; s_Gold = Gp3Materials.Gold ? Gp3Materials.Gold : s_Gold; }
        }

        /// <summary>A decorative chamfered block (no collider) from hole space: x/z centre, y span (hole-space heights), size across and along.</summary>
        static void Block(Transform root, HoleFrame f, string name, float x, float z, float sizeX, float sizeZ, float y0, float y1, Material mat)
        {
            if (y1 - y0 < 0.01f) return;
            ChamferMesh.Block(name, root, f.L(x, z) + Vector3.up * ((y0 + y1) * 0.5f), Quaternion.Euler(0f, f.yaw, 0f), new Vector3(sizeX, y1 - y0, sizeZ), 0.035f, mat, 1.6f);
        }

        /// <summary>A hero model placed in hole space (x, z on the ground plane, y a hole-space height). <paramref name="faceLocalYaw"/> turns it in the hole's frame
        /// (0 = front faces down the lane, 180 = front faces the tee).</summary>
        static GameObject HeroAt(Dresser d, Transform root, HoleFrame f, string model, float x, float z, float y, float faceLocalYaw, float scale = 1f)
        {
            if (!d.HasModel(model)) return null;
            var pos = f.L(x, z); pos.y = f.origin.y + y;
            return d.Model(model, pos, f.Yaw(faceLocalYaw), scale, snap: false, parent: root);
        }

        /// <summary>A warm sandstone massif (rock-kit cliffs, boulders and stones) as a backdrop, centred at hole-space (cx, cz) and spread along hole x.</summary>
        static void Massif(Dresser d, Transform root, HoleFrame f, float cx, float cz, float halfWidth, int count, int seed, float scale = 1.5f)
        {
            var mat = Gp3Materials.Ready ? Dresser.RockVariant(Gp3Materials.RockSandstoneWarm, "Temple_Rock", 0.9f) : null;
            var rr = new System.Random(seed);
            string[] cliffs = { "HeroCliff_A", "HeroCliff_B", "HeroCliff_C" };
            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? i / (float)(count - 1) : 0.5f;
                float x = cx - halfWidth + t * 2f * halfWidth + ((float)rr.NextDouble() - 0.5f) * 1.5f;
                float z = cz + ((float)rr.NextDouble() - 0.3f) * 3.0f;
                d.Crag(cliffs[i % 3], f.L(x, z), (float)rr.NextDouble() * 360f, scale * (0.85f + (float)rr.NextDouble() * 0.5f), mat, root, lods: true, extraSink: 0.25f);
            }
            for (int i = 0; i < count; i++)
            {
                float x = cx - halfWidth * 1.1f + (float)rr.NextDouble() * 2.2f * halfWidth, z = cz - 2.2f - (float)rr.NextDouble() * 2.5f;
                d.Crag(i % 2 == 0 ? "HeroRock_C" : "HeroStone_C", f.L(x, z), (float)rr.NextDouble() * 360f, 0.8f + (float)rr.NextDouble() * 0.8f, mat, root, lods: false);
            }
        }

        /// <summary>A brazier pier at the edge of the course, its carved front turned toward the lane's centre line.</summary>
        static void Pier(Dresser d, Transform root, HoleFrame f, float x, float z, float laneX = 0f)
        {
            var pos = f.L(x, z);
            if (!d.HasModel("HeroBrazierPillar")) { d.Torch(pos); return; }
            float yaw = x < laneX ? 90f : 270f;
            d.Model("HeroBrazierPillar", pos, f.Yaw(yaw), 1f, snap: true, sink: 0.03f, parent: root);
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

        // ------------------------------------------------------------------ hole 5: the Sun Stair

        public static void DressHole5(Dresser d, HoleFrame f, HoleDefinition def)
        {
            LoadMaterials();
            var root = new GameObject("Hole05_Dressing").transform;
            root.SetParent(d.Root, false);
            KeepOuts(d, f, def);
            var s = TropicalCourse.Hole5Spec();
            HoleSign(d, root, f, def, s.Right + 0.9f, 1.0f);

            // Solid masonry under the terraces and ramps so the stair reads as one stepped mass from outside (tops sit below every surface).
            float t2 = s.MiddleHeight, t3 = s.UpperHeight, midW = s.RightLaneEdge - s.LeftLaneEdge;
            Block(root, f, "Body_Middle", 0f, (s.DeckFrontZ + s.UpperFrontZ) * 0.5f, midW - 0.1f, s.UpperFrontZ - s.DeckFrontZ - 0.2f, -0.6f, t2 - 0.03f, s_StoneDark);
            Block(root, f, "Body_Upper", 0f, (s.UpperFrontZ + s.UpperEndZ) * 0.5f, s.Width - 0.1f, s.UpperEndZ - s.UpperFrontZ - 0.2f, -0.6f, t3 - 0.1f, s_StoneDark);
            Block(root, f, "Body_Ramp1", (s.Left + s.LeftLaneEdge) * 0.5f, (s.RampFootZ + s.LipZ) * 0.5f, s.Lane - 0.1f, s.LipZ - s.RampFootZ - 0.2f, -0.6f, -0.03f, s_StoneDark);
            Block(root, f, "Body_Ramp2", (s.RightLaneEdge + s.Right) * 0.5f, (s.Ramp2FootZ + s.UpperFrontZ) * 0.5f, s.Lane - 0.1f, s.UpperFrontZ - s.Ramp2FootZ - 0.2f, -0.6f, -0.03f, s_StoneDark);

            // The sun temple above the cup (Graphics Pass 3B): one modelled hero facade, its stair and plinth standing wholly beyond the upper terrace.
            float zb = s.UpperEndZ + 1.45f;
            HeroAt(d, root, f, "HeroTempleFacade", 0f, zb, t3 - 0.12f, 180f);

            Massif(d, root, f, 0f, zb + 9.5f, 12f, 7, 505, 1.25f);

            // Carved brazier piers beside the stair, on the ground outside the rails.
            foreach (float z in new[] { 1.5f, 4.5f, 7.5f })
            {
                Pier(d, root, f, s.Left - 0.8f, z);
                Pier(d, root, f, s.Right + 0.8f, z);
            }
        }

        // ------------------------------------------------------------------ hole 6: the Waterwheel Mill

        public static void DressHole6(Dresser d, HoleFrame f, HoleDefinition def)
        {
            LoadMaterials();
            var root = new GameObject("Hole06_Dressing").transform;
            root.SetParent(d.Root, false);
            KeepOuts(d, f, def);
            var s = TropicalCourse.Hole6Spec();
            float r = GolfTuning.Default.ballRadius;
            HoleSign(d, root, f, def, s.padRight + 0.9f, 1.0f);

            // The mill (Graphics Pass 3B): a modelled stone-and-timber mill house beside the wheel, its door turned to the lane; an arcade carries the green; a scaled
            // sun-temple facade closes the view behind the final green.
            float wz = s.wheel.WheelZ;
            float gy = s.GreenHeight(r), gz = s.GreenEndZ(r);
            if (d.HasModel("HeroMillHouse")) d.Model("HeroMillHouse", f.L(1.9f, wz + 0.2f), f.Yaw(270f), 0.92f, snap: true, sink: 0.04f, parent: root);
            HeroAt(d, root, f, "HeroTempleFacade", -0.5f, gz + 1.05f, gy - 0.1f, 180f, 0.58f);
            // The aqueduct arcade under the green: length along the course, top just below the surface.
            HeroAt(d, root, f, "HeroArcade", 0f, (s.GreenStartZ(r) + gz) * 0.5f, gy - 1.12f, 90f);
            Massif(d, root, f, -0.5f, gz + 10.5f, 10f, 6, 606, 1.15f);
            foreach (var (x, z) in new[] { (s.RoadOuterX - 0.7f, 0.7f), (s.padRight + 0.5f, 0.7f), (-0.7f, 5.5f), (0.9f, 5.5f), (s.RoadOuterX - 0.7f, gz - 0.3f), (1.5f, gz - 0.3f) })
                Pier(d, root, f, x, z);
        }
    }
}
