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
        }

        /// <summary>A decorative block (no collider) from hole space: x/z centre, y span (hole-space heights), size across and along.</summary>
        static void Block(Transform root, HoleFrame f, string name, float x, float z, float sizeX, float sizeZ, float y0, float y1, Material mat)
        {
            if (y1 - y0 < 0.01f) return;
            var go = ProvingKit.Box(name, root, Vector3.zero, Quaternion.Euler(0f, f.yaw, 0f), new Vector3(sizeX, y1 - y0, sizeZ), mat, collider: false);
            go.transform.position = f.L(x, z) + Vector3.up * ((y0 + y1) * 0.5f);   // f.L already includes the hole origin's height
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

            // The temple above: three stepped tiers behind the cup and a golden sun disc, so the destination is clearly above the player at the tee.
            float zb = s.UpperEndZ + 0.1f;
            Block(root, f, "Temple_Tier1", 0f, zb + 0.8f, s.Width + 1.4f, 1.6f, -0.6f, t3 + 1.0f, s_Stone);
            Block(root, f, "Temple_Tier2", 0f, zb + 0.7f, s.Width - 0.8f, 1.2f, t3 + 1.0f, t3 + 1.9f, s_Stone);
            Block(root, f, "Temple_Tier3", 0f, zb + 0.6f, s.Width - 2.6f, 0.9f, t3 + 1.9f, t3 + 2.6f, s_Stone);
            var disc = ProvingKit.AxialCylinder("SunDisc", root, Vector3.zero, Quaternion.Euler(0f, f.yaw, 0f), 0.75f, 0.1f, s_Gold);
            disc.transform.position = f.L(0f, zb) + Vector3.up * (t3 + 1.55f);

            // Torches beside the stair, on the ground outside the rails.
            foreach (float z in new[] { 1.5f, 4.5f, 7.5f, 10.0f })
            {
                d.Torch(f.L(s.Left - 0.7f, z));
                d.Torch(f.L(s.Right + 0.7f, z));
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

            // A bare mill house behind the wheel on the far side from the road, and a stone block behind the final green.
            float wz = s.wheel.WheelZ;
            Block(root, f, "MillHouse", 2.6f, wz + 0.2f, 2.2f, 2.4f, -0.6f, 1.9f, s_Stone);
            Block(root, f, "MillRoof", 2.6f, wz + 0.2f, 2.5f, 2.7f, 1.9f, 2.2f, s_StoneDark);
            float gy = s.GreenHeight(r), gz = s.GreenEndZ(r);
            Block(root, f, "GreenBackdrop", -0.5f, gz + 0.7f, 4.4f, 1.0f, -0.6f, gy + 1.4f, s_Stone);
            // Masonry under the green and a facing on the road's cliff beside it.
            Block(root, f, "GreenBody", 0f, (s.GreenStartZ(r) + gz) * 0.5f, s.wheel.GreenWidth - 0.1f, s.wheel.GreenLength - 0.2f, -0.6f, gy - 0.03f, s_StoneDark);
            foreach (var (x, z) in new[] { (s.RoadOuterX - 0.7f, 0.7f), (s.padRight + 0.5f, 0.7f), (-0.7f, 5.5f), (0.9f, 5.5f), (s.RoadOuterX - 0.7f, gz - 0.3f), (1.5f, gz - 0.3f) })
                d.Torch(f.L(x, z));
        }
    }
}
