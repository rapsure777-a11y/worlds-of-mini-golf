using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Gamebreak.MiniGolf.Editor.Art
{
    /// <summary>
    /// The reusable Tropical art kit: generated textures, shared materials and named mesh assets.
    /// Every hole is dressed from this one kit, so the whole world stays visually consistent and
    /// cheap to render (nearly everything shares the palette material).
    /// </summary>
    public class TropicalKit
    {
        public const string Root = "Assets/_Game/Worlds/Tropical/Kit";
        const string MeshDir = Root + "/Meshes";
        const string MatDir = Root + "/Materials";

        public Material Solid, Foliage, Emissive, Terrain, Water, Cloud, Turf, Rail, Cup, Flag, Tee, Rope, Sky;
        readonly Dictionary<string, Mesh> m_Meshes = new Dictionary<string, Mesh>();

        public Mesh this[string name] => m_Meshes.TryGetValue(name, out var m) ? m : throw new KeyNotFoundException("Kit mesh " + name);
        public IEnumerable<string> Names => m_Meshes.Keys;

        public static TropicalKit Build()
        {
            var kit = new TropicalKit();
            Directory.CreateDirectory(MeshDir);
            Directory.CreateDirectory(MatDir);
            var palette = TextureGen.Palette();
            var grain = TextureGen.Grain();
            var waterNormal = TextureGen.WaterNormal();
            var foam = TextureGen.Foam();
            kit.CreateMaterials(palette, grain, waterNormal, foam);
            kit.CreateMeshes();
            AssetDatabase.SaveAssets();
            return kit;
        }

        // ------------------------------------------------------------ materials

        void CreateMaterials(Texture2D palette, Texture2D grain, Texture2D waterNormal, Texture2D foam)
        {
            var lit = Shader.Find("Gamebreak/StylizedLit");
            var water = Shader.Find("Gamebreak/StylizedWater");

            Material Stylized(string name, Texture2D baseMap, Color color, float detailScale, float detailStrength,
                float smooth = 0.3f, float spec = 0.08f, float wind = 0f, bool twoSided = false, float rim = 0.15f, float ambient = 1f)
            {
                var m = LoadOrCreate(name, lit);
                m.SetTexture("_BaseMap", baseMap);
                m.SetColor("_BaseColor", color);
                m.SetTexture("_DetailTex", grain);
                m.SetFloat("_DetailScale", detailScale);
                m.SetFloat("_DetailStrength", detailStrength);
                m.SetFloat("_Smoothness", smooth);
                m.SetFloat("_Specular", spec);
                m.SetFloat("_WindStrength", wind);
                m.SetFloat("_WindSpeed", 1.3f);
                m.SetColor("_RimColor", new Color(1f, 0.95f, 0.8f, rim));
                m.SetFloat("_RimPower", 3f);
                m.SetFloat("_AmbientBoost", ambient);
                m.SetFloat("_Cull", twoSided ? 0f : 2f);
                m.SetColor("_EmissionColor", Color.black);
                m.enableInstancing = true;
                EditorUtility.SetDirty(m);
                return m;
            }

            Solid = Stylized("Kit_Solid", palette, Color.white, 1.6f, 0.18f, wind: 0.08f);
            Foliage = Stylized("Kit_Foliage", palette, Color.white, 2.5f, 0.12f, smooth: 0.45f, spec: 0.12f, wind: 0.09f, twoSided: true, rim: 0.25f);
            Emissive = Stylized("Kit_Emissive", palette, Color.white, 1f, 0f, wind: 0.05f);
            Emissive.SetColor("_EmissionColor", new Color(1.0f, 0.55f, 0.15f));
            Terrain = Stylized("Kit_Terrain", palette, Color.white, 0.9f, 0.32f, smooth: 0.15f, spec: 0.03f, rim: 0.08f);
            Cloud = Stylized("Kit_Cloud", palette, Color.white, 0.05f, 0f, smooth: 0f, spec: 0f, rim: 0.3f, ambient: 1.5f);
            Cloud.SetColor("_EmissionColor", new Color(0.22f, 0.22f, 0.24f));
            var white = Texture2D.whiteTexture;
            Turf = Stylized("Kit_Turf", white, new Color(0.28f, 0.74f, 0.22f), 7f, 0.32f, smooth: 0.08f, spec: 0.02f, rim: 0.1f);
            Rail = Stylized("Kit_Rail", white, new Color(0.86f, 0.56f, 0.30f), 2.2f, 0.35f, smooth: 0.35f, spec: 0.12f);
            Cup = Stylized("Kit_Cup", white, new Color(0.95f, 0.95f, 0.92f), 3f, 0.1f);
            Flag = Stylized("Kit_Flag", white, new Color(1f, 0.25f, 0.15f), 3f, 0.05f, smooth: 0.5f);
            Flag.SetColor("_EmissionColor", new Color(0.25f, 0.04f, 0.02f));
            Tee = Stylized("Kit_Tee", white, new Color(0.12f, 0.55f, 0.85f), 6f, 0.2f);
            Rope = Foliage;

            var w = LoadOrCreate("Kit_Water", water);
            w.SetColor("_ShallowColor", new Color(0.22f, 0.92f, 0.82f, 0.5f));
            w.SetColor("_DeepColor", new Color(0.02f, 0.38f, 0.66f, 0.96f));
            w.SetColor("_HorizonColor", new Color(0.72f, 0.9f, 1f, 1f));
            w.SetTexture("_NormalMap", waterNormal);
            w.SetTexture("_FoamTex", foam);
            w.SetFloat("_SpecStrength", 0.35f); // fewer, softer glints than the default
            w.SetFloat("_Gloss", 320f);
            w.enableInstancing = true;
            EditorUtility.SetDirty(w);
            Water = w;

            var sky = LoadOrCreate("Kit_Sky", Shader.Find("Gamebreak/GradientSky"));
            sky.SetColor("_TopColor", new Color(0.16f, 0.46f, 0.95f));
            sky.SetColor("_HorizonColor", new Color(0.70f, 0.89f, 1.0f));
            sky.SetColor("_BottomColor", new Color(0.50f, 0.78f, 0.92f));
            sky.SetFloat("_Exponent", 0.5f);
            sky.SetColor("_SunColor", new Color(1f, 0.93f, 0.78f));
            sky.SetFloat("_SunSize", 0.012f);
            sky.SetFloat("_SunGlow", 0.55f);
            EditorUtility.SetDirty(sky);
            Sky = sky;
        }

        static Material LoadOrCreate(string name, Shader shader)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader) m.shader = shader;
            return m;
        }

        // ------------------------------------------------------------ meshes

        void Add(string name, Mesh mesh)
        {
            mesh.name = name;
            string path = $"{MeshDir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing)
            {
                // Keep the asset (and its GUID) so scene references survive; just replace the data.
                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                existing.name = name;
                Object.DestroyImmediate(mesh);
                mesh = existing;
            }
            else
            {
                MeshUtility.Optimize(mesh);
                AssetDatabase.CreateAsset(mesh, path);
            }
            EditorUtility.SetDirty(mesh);
            m_Meshes[name] = mesh;
        }

        void CreateMeshes()
        {
            float[] palmHeights = { 3.6f, 4.3f, 5.0f, 5.8f };
            for (int i = 0; i < palmHeights.Length; i++)
            {
                var (trunk, crown) = KitMeshes.Palm(100 + i, palmHeights[i]);
                Add($"PalmTrunk{i}", trunk);
                Add($"PalmCrown{i}", crown);
            }
            for (int i = 0; i < 4; i++) Add($"Bush{i}", KitMeshes.Bush(200 + i, 0.55f + i * 0.12f, i < 2 ? Palette.Row.Leaf : Palette.Row.Jungle));
            for (int i = 0; i < 3; i++) Add($"BigLeaf{i}", KitMeshes.BigLeafPlant(300 + i, 0.9f + i * 0.25f));
            for (int i = 0; i < 3; i++) Add($"Grass{i}", KitMeshes.GrassTuft(400 + i, 0.32f + i * 0.08f));
            Add("FlowerBush0", KitMeshes.FlowerBush(500, 0.6f, Palette.Flower.Hibiscus));
            Add("FlowerBush1", KitMeshes.FlowerBush(501, 0.55f, Palette.Flower.Yellow));
            Add("FlowerBush2", KitMeshes.FlowerBush(502, 0.65f, Palette.Flower.Pink));
            Add("FlowerBush3", KitMeshes.FlowerBush(503, 0.5f, Palette.Flower.Orange));
            Add("FlowerBush4", KitMeshes.FlowerBush(504, 0.55f, Palette.Flower.Purple));
            Add("FlowerBush5", KitMeshes.FlowerBush(505, 0.5f, Palette.Flower.White));
            Add("FlowerBush6", KitMeshes.FlowerBush(506, 0.6f, Palette.Flower.Magenta));

            Add("RockSmall0", KitMeshes.Rock(600, new Vector3(0.35f, 0.25f, 0.3f)));
            Add("RockSmall1", KitMeshes.Rock(601, new Vector3(0.45f, 0.3f, 0.35f), grey: true));
            Add("RockMedium0", KitMeshes.Rock(602, new Vector3(1.0f, 0.75f, 0.85f), grassTop: true));
            Add("RockMedium1", KitMeshes.Rock(603, new Vector3(1.2f, 0.9f, 1.0f)));
            Add("RockLarge0", KitMeshes.Rock(604, new Vector3(2.4f, 1.8f, 2.0f), grassTop: true));
            Add("RockLarge1", KitMeshes.Rock(605, new Vector3(2.8f, 1.6f, 2.2f), grey: true, grassTop: true));
            Add("Cliff0", KitMeshes.CliffStack(700, 5.5f, 3.6f));
            Add("Cliff1", KitMeshes.CliffStack(701, 7.5f, 4.2f));
            Add("Cliff2", KitMeshes.CliffStack(702, 4.0f, 3.0f));

            Add("TikiHut", KitMeshes.TikiHut(800));
            Add("Boardwalk6", KitMeshes.Boardwalk(810, 6f));
            Add("Boardwalk3", KitMeshes.Boardwalk(811, 3f));
            var (torch, flame) = KitMeshes.TikiTorch(820);
            Add("TikiTorch", torch);
            Add("TikiFlame", flame);
            Add("Crate0", KitMeshes.Crate(830, 0.6f));
            Add("Crate1", KitMeshes.Crate(831, 0.45f));
            Add("Barrel", KitMeshes.Barrel(840));
            Add("SignLarge", KitMeshes.Signboard(1.5f, 0.95f, 0.75f));
            Add("SignSmall", KitMeshes.Signboard(0.8f, 0.45f, 0.65f));
            Add("RopeFence3", KitMeshes.RopeFence(850, 3.6f));
            for (int i = 0; i < 4; i++) Add($"Cloud{i}", KitMeshes.Cloud(900 + i, 18f + i * 6f));
            Add("DistantIsland", DistantIsland(950));
        }

        /// <summary>Low-detail island silhouette for the horizon.</summary>
        static Mesh DistantIsland(int seed)
        {
            var mb = new MeshBuilder();
            mb.With(Matrix4x4.Scale(new Vector3(1f, 0.28f, 0.75f)), () =>
                mb.Blob(2, d => 1f + 0.25f * (Noise.Value3(d * 1.7f, seed) - 0.5f) * 2f,
                    (p, n) => p.y < 0.12f
                        ? (Palette.UV(Palette.Row.Sand, 0.7f), new Color(1f, 1f, 1f, 0f))
                        : (Palette.UV(Palette.Row.Jungle, 0.3f + 0.5f * Noise.Value3(p * 2f, seed)), new Color(1f, 1f, 1f, 0f)),
                    faceted: true));
            return mb.ToMesh("DistantIsland");
        }
    }
}
