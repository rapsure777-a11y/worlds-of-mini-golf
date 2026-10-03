using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gamebreak.MiniGolf.Editor.Art
{
    /// <summary>Pixel rectangles of the Blender leaf atlas (2048², origin bottom-left). Mirrors Tools/Blender/leaf_atlas.py.</summary>
    public static class LeafAtlas
    {
        public const float Size = 2048f;
        public static readonly Dictionary<string, RectInt> Cells = new Dictionary<string, RectInt>
        {
            ["palm_frond"] = new RectInt(0, 0, 512, 1024), ["palm_dry"] = new RectInt(0, 1024, 512, 1024),
            ["broadleaf"] = new RectInt(512, 0, 512, 512), ["monstera"] = new RectInt(1024, 0, 512, 512),
            ["fern"] = new RectInt(1536, 0, 512, 512), ["bush_a"] = new RectInt(512, 512, 512, 512),
            ["bush_b"] = new RectInt(1024, 512, 512, 512), ["grass"] = new RectInt(1536, 512, 512, 512),
            ["hibiscus"] = new RectInt(512, 1024, 512, 512), ["plumeria"] = new RectInt(1024, 1024, 512, 512),
            ["flowers_purple"] = new RectInt(1536, 1024, 512, 512), ["banana"] = new RectInt(512, 1536, 512, 512),
            ["ivy"] = new RectInt(1024, 1536, 512, 512), ["bush_c"] = new RectInt(1536, 1536, 512, 512),
        };

        /// <summary>UV inside a cell; u, v in 0..1 (v = 0 at the leaf base). A small inset avoids bleeding.</summary>
        public static Vector2 UV(string cell, float u, float v)
        {
            var c = Cells[cell];
            const float inset = 3f;
            return new Vector2((c.x + inset + u * (c.width - 2 * inset)) / Size, (c.y + inset + v * (c.height - 2 * inset)) / Size);
        }
    }

    /// <summary>
    /// The higher-fidelity layer of the Tropical kit: Blender-baked PBR textures, hero models (FBX) and
    /// leaf-card foliage built from the leaf atlas. Materials are created here; hero model children are
    /// matched to materials by their name suffix (e.g. "HeroPalm_0__Leaves" -> Leaves).
    /// </summary>
    public class HeroKit
    {
        public const string TexDir = "Assets/_Game/Art/Generated/Textures";
        public const string ModelDir = "Assets/_Game/Art/Generated/Models";
        const string MatDir = TropicalKit.Root + "/Materials";
        const string MeshDir = TropicalKit.Root + "/Meshes";

        public Material Terrain, Rock, Wood, Thatch, Bamboo, Bark, Totem, Lantern, Paint, Leaves, Turf, Rail, Waterfall, PoolWater, Mist;
        readonly Dictionary<string, Material> m_BySuffix = new Dictionary<string, Material>();
        readonly Dictionary<string, Mesh> m_Foliage = new Dictionary<string, Mesh>();

        public Mesh Foliage(string name) => m_Foliage[name];

        public static HeroKit Build(TropicalKit kit)
        {
            var h = new HeroKit();
            h.CreateMaterials(kit);
            h.CreateFoliage();
            AssetDatabase.SaveAssets();
            return h;
        }

        // ------------------------------------------------------------------ materials

        static Texture2D T(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{name}.png")
                                           ?? throw new FileNotFoundException($"Missing generated texture {name}. Run Tools/blender-assets.ps1.");

        static Material LoadOrCreate(string name, Shader shader)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            else if (m.shader != shader) m.shader = shader;
            return m;
        }

        Material Surface(string name, string tex, Color tint, float smooth, float spec, float normal = 1f, Vector2? tiling = null)
        {
            var m = LoadOrCreate(name, Shader.Find("Gamebreak/StylizedLit"));
            m.SetTexture("_BaseMap", T($"{tex}_albedo"));
            m.SetTextureScale("_BaseMap", tiling ?? Vector2.one);
            m.SetColor("_BaseColor", tint);
            m.SetTexture("_BumpMap", T($"{tex}_normal"));
            m.SetFloat("_BumpScale", normal);
            m.SetFloat("_UseNormalMap", 1f); m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_MaskMap", T($"{tex}_mask"));
            m.SetFloat("_UseMaskMap", 1f); m.EnableKeyword("_MASKMAP");
            m.SetFloat("_OcclusionStrength", 0.8f);
            m.SetFloat("_DetailStrength", 0f);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Specular", spec);
            m.SetColor("_RimColor", new Color(1f, 0.95f, 0.85f, 0.12f));
            m.SetFloat("_AmbientBoost", 1f);
            m.SetFloat("_WindStrength", 0.04f);
            m.SetFloat("_Cull", 2f);
            m.DisableKeyword("_ALPHATEST_ON"); m.SetFloat("_AlphaClip", 0f);
            m.SetColor("_EmissionColor", Color.black);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        void CreateMaterials(TropicalKit kit)
        {
            var grain = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureGen.Dir + "/Grain.png");

            Terrain = LoadOrCreate("Hero_Terrain", Shader.Find("Gamebreak/TerrainSplat"));
            Terrain.SetTexture("_SandAlb", T("sand_albedo")); Terrain.SetTexture("_SandNrm", T("sand_normal"));
            Terrain.SetTexture("_LawnAlb", T("lawn_albedo")); Terrain.SetTexture("_LawnNrm", T("lawn_normal"));
            Terrain.SetTexture("_RockAlb", T("sandstone_albedo")); Terrain.SetTexture("_RockNrm", T("sandstone_normal"));
            Terrain.SetTexture("_PathAlb", T("path_albedo")); Terrain.SetTexture("_PathNrm", T("path_normal"));
            Terrain.SetVector("_Tiling", new Vector4(3.5f, 2.4f, 4f, 2.2f));
            Terrain.SetVector("_Smooth", new Vector4(0.2f, 0.18f, 0.15f, 0.1f));
            Terrain.SetFloat("_NormalStrength", 1.1f);
            Terrain.SetFloat("_BlendSharpness", 5f);
            Terrain.SetColor("_LawnTint", new Color(1.02f, 1.06f, 0.95f));
            Terrain.SetColor("_SeabedTint", new Color(0.55f, 0.88f, 0.84f));
            Terrain.SetTexture("_MacroTex", grain);
            Terrain.SetFloat("_MacroScale", 0.03f);
            EditorUtility.SetDirty(Terrain);

            Rock = LoadOrCreate("Hero_Rock", Shader.Find("Gamebreak/RockTriplanar"));
            Rock.SetTexture("_RockAlb", T("sandstone_albedo")); Rock.SetTexture("_RockNrm", T("sandstone_normal"));
            Rock.SetTexture("_TopAlb", T("lawn_albedo")); Rock.SetTexture("_TopNrm", T("lawn_normal"));
            Rock.SetFloat("_RockTile", 3.5f); Rock.SetFloat("_TopTile", 2.2f);
            Rock.SetFloat("_TopCoverage", 0.6f); Rock.SetFloat("_TopSoftness", 0.12f);
            Rock.SetFloat("_NormalStrength", 1.3f); Rock.SetFloat("_AOStrength", 0.9f); Rock.SetFloat("_Smoothness", 0.18f);
            Rock.SetTexture("_NoiseTex", grain);
            Rock.enableInstancing = true;
            EditorUtility.SetDirty(Rock);

            Wood = Surface("Hero_Wood", "wood", Color.white, 0.45f, 0.12f);
            Thatch = Surface("Hero_Thatch", "thatch", new Color(1f, 0.97f, 0.9f), 0.2f, 0.05f, 1.3f);
            Thatch.SetFloat("_WindStrength", 0.05f);
            Bamboo = Surface("Hero_Bamboo", "bamboo", Color.white, 0.6f, 0.2f);
            Bark = Surface("Hero_Bark", "bark", new Color(1f, 0.95f, 0.9f), 0.25f, 0.06f, 1.4f);
            Totem = Surface("Hero_Totem", "wood", new Color(0.82f, 0.62f, 0.48f), 0.4f, 0.1f, 1.2f, new Vector2(0.8f, 0.8f));
            Turf = Surface("Hero_Turf", "turf", new Color(1f, 1.05f, 1f), 0.1f, 0.03f, 0.8f, new Vector2(1.2f, 1.2f));
            Rail = Surface("Hero_Rail", "wood", new Color(1.05f, 0.95f, 0.85f), 0.5f, 0.12f, 1f, new Vector2(0.7f, 0.9f));

            Paint = LoadOrCreate("Hero_Paint", Shader.Find("Gamebreak/StylizedLit"));
            Paint.SetTexture("_BaseMap", Texture2D.whiteTexture);
            Paint.SetColor("_BaseColor", Color.white);
            Paint.SetFloat("_Smoothness", 0.55f); Paint.SetFloat("_Specular", 0.2f); Paint.SetFloat("_DetailStrength", 0f);
            Paint.SetFloat("_Cull", 2f);
            EditorUtility.SetDirty(Paint);

            Lantern = LoadOrCreate("Hero_Lantern", Shader.Find("Gamebreak/StylizedLit"));
            Lantern.SetTexture("_BaseMap", Texture2D.whiteTexture);
            Lantern.SetColor("_BaseColor", new Color(1f, 0.85f, 0.6f));
            Lantern.SetColor("_EmissionColor", new Color(2.4f, 1.3f, 0.45f));
            Lantern.SetFloat("_DetailStrength", 0f);
            Lantern.SetFloat("_WindStrength", 0.05f);
            EditorUtility.SetDirty(Lantern);

            Leaves = LoadOrCreate("Hero_Leaves", Shader.Find("Gamebreak/StylizedLit"));
            Leaves.SetTexture("_BaseMap", T("leaves_albedo"));
            Leaves.SetColor("_BaseColor", new Color(1.05f, 1.08f, 1f));
            Leaves.SetTexture("_BumpMap", T("leaves_normal"));
            Leaves.SetFloat("_UseNormalMap", 1f); Leaves.EnableKeyword("_NORMALMAP");
            Leaves.SetFloat("_BumpScale", 0.8f);
            Leaves.SetFloat("_AlphaClip", 1f); Leaves.EnableKeyword("_ALPHATEST_ON");
            Leaves.SetFloat("_Cutoff", 0.45f);
            Leaves.SetFloat("_Cull", 0f);
            Leaves.SetFloat("_Translucency", 0.9f);
            Leaves.SetColor("_TranslucencyColor", new Color(0.75f, 1f, 0.35f));
            Leaves.SetFloat("_Smoothness", 0.4f); Leaves.SetFloat("_Specular", 0.15f);
            Leaves.SetColor("_RimColor", new Color(0.9f, 1f, 0.7f, 0.18f));
            Leaves.SetFloat("_DetailStrength", 0f);
            Leaves.SetFloat("_WindStrength", 0.07f);
            Leaves.SetFloat("_WindSpeed", 1.3f);
            Leaves.enableInstancing = true;
            EditorUtility.SetDirty(Leaves);

            Waterfall = LoadOrCreate("Hero_Waterfall", Shader.Find("Gamebreak/Waterfall"));
            Waterfall.SetTexture("_NoiseTex", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureGen.Dir + "/FoamNoise.png"));
            EditorUtility.SetDirty(Waterfall);

            PoolWater = LoadOrCreate("Hero_PoolWater", Shader.Find("Gamebreak/StylizedWater"));
            PoolWater.CopyPropertiesFromMaterial(kit.Water);
            PoolWater.SetFloat("_Absorption", 0.9f);
            PoolWater.SetFloat("_WaveHeight", 0f);
            PoolWater.SetFloat("_IntersectFoam", 0.5f);
            EditorUtility.SetDirty(PoolWater);

            Mist = LoadOrCreate("Hero_Mist", Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            Mist.SetTexture("_BaseMap", SoftDot());
            Mist.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.35f));
            Mist.SetFloat("_Surface", 1f);
            Mist.SetFloat("_Blend", 0f);
            Mist.SetOverrideTag("RenderType", "Transparent");
            Mist.renderQueue = (int)RenderQueue.Transparent;
            Mist.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            Mist.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            Mist.SetInt("_ZWrite", 0);
            Mist.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            EditorUtility.SetDirty(Mist);

            m_BySuffix["Bark"] = Bark; m_BySuffix["Leaves"] = Leaves; m_BySuffix["Rock"] = Rock;
            m_BySuffix["Wood"] = Wood; m_BySuffix["Thatch"] = Thatch; m_BySuffix["Bamboo"] = Bamboo;
            m_BySuffix["Totem"] = Totem; m_BySuffix["Lantern"] = Lantern; m_BySuffix["Paint"] = Paint;
        }

        static Texture2D SoftDot()
        {
            string path = TexDir + "/soft_dot.png";
            if (!File.Exists(path))
            {
                var t = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 31.5f;
                    float a = Mathf.Clamp01(1f - d);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
                File.WriteAllBytes(path, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(path);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ------------------------------------------------------------------ hero models

        /// <summary>Instantiate a generated FBX and assign materials by child-name suffix.</summary>
        public GameObject Instantiate(string model, Transform parent, Vector3 pos, float yaw, float scale = 1f,
            bool colliders = false, bool outOfBounds = false, bool shadows = true)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{model}.fbx")
                        ?? throw new FileNotFoundException($"Missing generated model {model}. Run Tools/blender-assets.ps1.");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = model;
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * scale;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                string n = r.gameObject.name;
                int k = n.LastIndexOf("__");
                string suffix = k >= 0 ? n.Substring(k + 2) : "";
                if (suffix.Contains(".")) suffix = suffix.Substring(0, suffix.IndexOf('.'));
                r.sharedMaterial = m_BySuffix.TryGetValue(suffix, out var mat) ? mat : Paint;
                r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                if (colliders && suffix != "Leaves" && suffix != "Lantern")
                {
                    var mc = r.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = r.GetComponent<MeshFilter>().sharedMesh;
                    if (outOfBounds) r.gameObject.AddComponent<OutOfBoundsSurface>();
                }
            }
            foreach (var t in go.GetComponentsInChildren<Transform>())
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            return go;
        }

        // ------------------------------------------------------------------ leaf-card foliage

        void AddFoliage(string name, Mesh mesh)
        {
            mesh.name = name;
            mesh.RecalculateTangents();
            string path = $"{MeshDir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing)
            {
                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                mesh = existing;
            }
            else AssetDatabase.CreateAsset(mesh, path);
            m_Foliage[name] = mesh;
        }

        void CreateFoliage()
        {
            AddFoliage("LeafBush0", CardBush(1, 0.9f, new[] { "bush_a", "bush_c" }, 34));
            AddFoliage("LeafBush1", CardBush(2, 1.1f, new[] { "bush_b", "bush_a" }, 42));
            AddFoliage("LeafBush2", CardBush(3, 0.75f, new[] { "bush_b" }, 28));
            AddFoliage("FlowerShrub0", CardBush(4, 0.85f, new[] { "bush_a", "bush_b" }, 30, "hibiscus", 9));
            AddFoliage("FlowerShrub1", CardBush(5, 0.8f, new[] { "bush_c", "bush_a" }, 28, "plumeria", 8));
            AddFoliage("FlowerShrub2", CardBush(6, 0.7f, new[] { "bush_b" }, 26, "flowers_purple", 7));
            AddFoliage("BigLeaf0", LeafPlant(7, 1.2f, "broadleaf", 8));
            AddFoliage("BigLeaf1", LeafPlant(8, 1.4f, "monstera", 7));
            AddFoliage("Banana0", LeafPlant(9, 2.2f, "banana", 6, upright: true));
            AddFoliage("Fern0", LeafPlant(10, 0.9f, "fern", 11, arch: 1.2f));
            AddFoliage("GrassClump0", GrassClump(11, 0.45f));
            AddFoliage("GrassClump1", GrassClump(12, 0.6f));
        }

        static Color Wind(float w) => new Color(1f, 1f, 1f, w);

        /// <summary>Dense bush of outward-facing leaf cards with spherical normals (soft, volumetric shading).</summary>
        static Mesh CardBush(int seed, float size, string[] cells, int cards, string flowerCell = null, int flowers = 0)
        {
            var rnd = new System.Random(seed);
            float Rf(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var mb = new MeshBuilder();
            Vector3 centre = new Vector3(0f, size * 0.45f, 0f);
            void Card(Vector3 dir, float w, float h, string cell, float lift)
            {
                Vector3 pos = centre + Vector3.Scale(dir, new Vector3(size * 0.55f, size * 0.42f, size * 0.55f));
                pos.y = Mathf.Max(pos.y, lift);
                Vector3 up = (dir + Vector3.up * 0.6f).normalized;
                Vector3 right = Vector3.Cross(up, dir).normalized;
                if (right.sqrMagnitude < 1e-4f) right = Vector3.right;
                right = Quaternion.AngleAxis(Rf(-25f, 25f), dir) * right;
                up = Vector3.Cross(dir, right).normalized;
                Vector3 b0 = pos - right * w * 0.5f - up * h * 0.25f, b1 = pos + right * w * 0.5f - up * h * 0.25f;
                Vector3 t0 = b0 + up * h, t1 = b1 + up * h;
                Vector3 N(Vector3 p) => (p - centre + Vector3.up * size * 0.25f).normalized;
                float windBase = 0.15f + 0.25f * Mathf.Clamp01(pos.y / (size * 1.2f));
                int a = mb.Vertex(b0, N(b0), LeafAtlas.UV(cell, 0f, 0f), Wind(windBase));
                int b = mb.Vertex(b1, N(b1), LeafAtlas.UV(cell, 1f, 0f), Wind(windBase));
                int c = mb.Vertex(t1, N(t1), LeafAtlas.UV(cell, 1f, 1f), Wind(windBase + 0.35f));
                int d = mb.Vertex(t0, N(t0), LeafAtlas.UV(cell, 0f, 1f), Wind(windBase + 0.35f));
                mb.Quad(a, d, c, b);
            }
            for (int i = 0; i < cards; i++)
            {
                var dir = new Vector3(Rf(-1f, 1f), Rf(-0.25f, 1f), Rf(-1f, 1f)).normalized;
                float s = size * Rf(0.55f, 0.8f);
                Card(dir, s, s, cells[i % cells.Length], 0.05f);
            }
            for (int i = 0; i < flowers; i++)
            {
                var dir = new Vector3(Rf(-1f, 1f), Rf(0.1f, 1f), Rf(-1f, 1f)).normalized;
                float s = size * Rf(0.22f, 0.3f);
                Card(dir * 1.08f, s, s, flowerCell, 0.2f);
            }
            return mb.ToMesh("bush");
        }

        /// <summary>Arching leaf ribbons mapped to one atlas leaf (texture alpha gives the silhouette).</summary>
        static Mesh LeafPlant(int seed, float size, string cell, int leaves, bool upright = false, float arch = 1f)
        {
            var rnd = new System.Random(seed);
            float Rf(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var mb = new MeshBuilder();
            var c = LeafAtlas.Cells[cell];
            float aspect = c.width / (float)c.height;
            for (int i = 0; i < leaves; i++)
            {
                float yaw = i * 360f / leaves + Rf(-20f, 20f);
                var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                float len = size * Rf(0.7f, 1.0f);
                float stem = upright ? size * Rf(0.25f, 0.4f) : size * 0.08f;
                float rise = upright ? Rf(1.6f, 2.2f) : Rf(0.8f, 1.3f) * arch;
                float droop = upright ? Rf(1.2f, 1.8f) : Rf(1.0f, 1.5f) * arch;
                var basePos = Vector3.up * stem;
                const int segs = 8;
                var side = Vector3.Cross(Vector3.up, dir).normalized;
                int[] prev = null;
                for (int k = 0; k <= segs; k++)
                {
                    float t = k / (float)segs;
                    Vector3 p = basePos + dir * (t * len * 0.8f) + Vector3.up * (rise * t * len * 0.6f - droop * t * t * len * 0.6f);
                    Vector3 tangent = (dir * 0.8f + Vector3.up * (rise * 0.6f - 2f * droop * t * 0.6f)).normalized;
                    Vector3 up = Vector3.Cross(side, tangent).normalized;
                    float half = len * aspect * 0.5f;
                    float fold = half * 0.25f;
                    var row = new int[3];
                    for (int j = 0; j < 3; j++)
                    {
                        float u = j * 0.5f;
                        Vector3 v = p + side * ((u - 0.5f) * 2f * half) + up * (fold * (1f - Mathf.Abs(u - 0.5f) * 2f));
                        row[j] = mb.Vertex(v, up, LeafAtlas.UV(cell, u, t), Wind(0.1f + 0.9f * t));
                    }
                    if (prev != null)
                    {
                        mb.Quad(prev[0], prev[1], row[1], row[0]);
                        mb.Quad(prev[1], prev[2], row[2], row[1]);
                    }
                    prev = row;
                }
                if (upright)
                {
                    // Pseudo-stem: a narrow card down to the ground using the leaf's base colour.
                    int s0 = mb.Vertex(-side * 0.04f, side, LeafAtlas.UV(cell, 0.48f, 0.02f), Wind(0f));
                    int s1 = mb.Vertex(side * 0.04f, side, LeafAtlas.UV(cell, 0.52f, 0.02f), Wind(0f));
                    int s2 = mb.Vertex(basePos + side * 0.04f, side, LeafAtlas.UV(cell, 0.52f, 0.05f), Wind(0.1f));
                    int s3 = mb.Vertex(basePos - side * 0.04f, side, LeafAtlas.UV(cell, 0.48f, 0.05f), Wind(0.1f));
                    mb.Quad(s0, s3, s2, s1);
                }
            }
            return mb.ToMesh("leafplant");
        }

        /// <summary>Crossed vertical grass cards with upward normals (lit like the ground they grow from).</summary>
        static Mesh GrassClump(int seed, float height)
        {
            var rnd = new System.Random(seed);
            var mb = new MeshBuilder();
            for (int i = 0; i < 4; i++)
            {
                float yaw = i * 45f + (float)rnd.NextDouble() * 20f;
                var r = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
                float w = height * 0.9f;
                int a = mb.Vertex(-r * w * 0.5f, Vector3.up, LeafAtlas.UV("grass", 0f, 0f), Wind(0f));
                int b = mb.Vertex(r * w * 0.5f, Vector3.up, LeafAtlas.UV("grass", 1f, 0f), Wind(0f));
                int c = mb.Vertex(r * w * 0.5f + Vector3.up * height, Vector3.up, LeafAtlas.UV("grass", 1f, 1f), Wind(1f));
                int d = mb.Vertex(-r * w * 0.5f + Vector3.up * height, Vector3.up, LeafAtlas.UV("grass", 0f, 1f), Wind(1f));
                mb.Quad(a, d, c, b);
            }
            return mb.ToMesh("grass");
        }
    }
}
