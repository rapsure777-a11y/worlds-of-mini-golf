using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Gamebreak.MiniGolf.Editor.Art
{
    /// <summary>
    /// Graphics Pass 3 switch and safety net. Every Graphics Pass 3 step runs through <see cref="Run"/>: a failure is logged and that step is skipped, so a visual
    /// problem can never stop the scene build. Set <see cref="Enabled"/> to false to build the pre-pass-3 world.
    /// </summary>
    public static class Gp3
    {
        public static bool Enabled = true;

        public static void Run(string what, Action step)
        {
            if (!Enabled) return;
            try { step(); }
            catch (Exception e) { Debug.LogError($"[Gamebreak] Graphics Pass 3 step '{what}' failed and was skipped: {e}"); }
        }
    }

    /// <summary>
    /// Graphics Pass 3 texture assets: runs the pure-maths generators in <see cref="ProceduralPbr"/> and writes the sets as PNGs under Art/Generated/Textures
    /// (<c>gp3_name_albedo / _normal / _mask / _glow</c>), where <see cref="GeneratedAssetImporter"/> applies the import rules. Idempotent: a marker file holds the
    /// generator version, and the sets are only rewritten when it changes or a file is missing.
    /// </summary>
    public static class Gp3Textures
    {
        /// <summary>Bump when a generator changes so the textures are rebuilt on the next scene build.</summary>
        public const int Version = 2;
        const string Marker = HeroKit.TexDir + "/gp3_version.txt";

        static readonly (string name, Func<PbrSet> make, float normal)[] Sets =
        {
            ("turf", () => ProceduralPbr.Turf(), 1.6f),
            ("railstone", () => ProceduralPbr.StoneBlocks(), 3.0f),
            ("sandstone", () => ProceduralPbr.TempleSandstone(), 3.0f),
            ("limestone", () => ProceduralPbr.Limestone(), 3.0f),
            ("limestone_wet", () => ProceduralPbr.Limestone(512, 33, true), 3.0f),
            ("basalt", () => ProceduralPbr.Basalt(), 3.0f),
            ("lava", () => ProceduralPbr.Lava(), 1.5f),
            ("rope", () => ProceduralPbr.Rope(), 3.0f),
            ("metal", () => ProceduralPbr.BrushedMetal(), 1.5f),
            ("cupliner", () => ProceduralPbr.CupLiner(), 1.5f),
            ("ball", () => ProceduralPbr.BallDimples(), 3.0f),
            ("cloth", () => ProceduralPbr.Cloth(), 1.5f),
            ("sunrelief", () => ProceduralPbr.SunRelief(), 3.0f),
        };

        public static string Path(string set, string kind) => $"{HeroKit.TexDir}/gp3_{set}_{kind}.png";

        public static Texture2D Load(string set, string kind) => AssetDatabase.LoadAssetAtPath<Texture2D>(Path(set, kind));

        public static void EnsureAll()
        {
            Directory.CreateDirectory(HeroKit.TexDir);
            bool current = File.Exists(Marker) && File.ReadAllText(Marker).Trim() == Version.ToString();
            bool changed = false;
            foreach (var (name, make, normal) in Sets)
            {
                if (current && File.Exists(Path(name, "albedo")) && File.Exists(Path(name, "normal")) && File.Exists(Path(name, "mask"))) continue;
                var set = make();
                Write(Path(name, "albedo"), set.AlbedoBytes(), set.Size);
                Write(Path(name, "normal"), set.NormalBytes(normal, name != "cupliner"), set.Size);
                Write(Path(name, "mask"), set.MaskBytes(), set.Size);
                var glow = set.GlowBytes();
                if (glow != null) Write(Path(name, "glow"), glow, set.Size);
                changed = true;
            }
            if (changed || !current)
            {
                File.WriteAllText(Marker, Version.ToString());
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }

        static void Write(string path, byte[] rgba, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.LoadRawTextureData(rgba);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }
    }

    /// <summary>
    /// Graphics Pass 3 materials. <see cref="Upgrade"/> re-skins the existing kit materials in place (the putting turf and the rails keep their asset identity, so every
    /// scene reference survives) and creates the new biome materials. All of them use the existing Gamebreak/StylizedLit shader (and the URP Lit shader for metal).
    /// </summary>
    public static class Gp3Materials
    {
        const string MatDir = TropicalKit.Root + "/Materials";

        public static Material LavaFall;
        public static Material Sandstone, Limestone, LimestoneWet, Basalt, Lava, Rope, SunRelief, Banner, Gold, Brazier, Cloud2, Ember, WoodDark;
        public static Material CupLiner, CupRim, PutterBody, PutterBevel, PutterInsert, PutterLine;
        public static Material RockLimestone, RockBasalt, RockSandstoneWarm;
        public static bool Ready { get; private set; }

        static Shader Stylized => Shader.Find("Gamebreak/StylizedLit");

        static Material LoadOrCreate(string name, Shader shader)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            else if (m.shader != shader) m.shader = shader;
            return m;
        }

        /// <summary>Applies one texture set to a StylizedLit material.</summary>
        static Material Surface(string name, string set, Color tint, float smooth, float spec, float bump, Vector2 tiling, float rim = 0.1f, float occlusion = 0.8f)
        {
            var m = LoadOrCreate(name, Stylized);
            m.SetTexture("_BaseMap", Gp3Textures.Load(set, "albedo"));
            m.SetTextureScale("_BaseMap", tiling);
            m.SetColor("_BaseColor", tint);
            m.SetTexture("_BumpMap", Gp3Textures.Load(set, "normal"));
            m.SetFloat("_BumpScale", bump);
            m.SetFloat("_UseNormalMap", 1f); m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_MaskMap", Gp3Textures.Load(set, "mask"));
            m.SetFloat("_UseMaskMap", 1f); m.EnableKeyword("_MASKMAP");
            m.SetFloat("_OcclusionStrength", occlusion);
            m.SetFloat("_DetailStrength", 0f);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Specular", spec);
            m.SetColor("_RimColor", new Color(1f, 0.9f, 0.7f, rim));
            m.SetFloat("_AmbientBoost", 1.05f);
            m.SetFloat("_WindStrength", 0f);
            m.SetFloat("_Cull", 2f);
            m.DisableKeyword("_ALPHATEST_ON"); m.SetFloat("_AlphaClip", 0f);
            m.SetColor("_EmissionColor", Color.black);
            m.SetTexture("_EmissionMap", Texture2D.whiteTexture);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void Glow(Material m, string set, Color emission)
        {
            m.SetTexture("_EmissionMap", Gp3Textures.Load(set, "glow"));
            m.SetColor("_EmissionColor", emission);
            EditorUtility.SetDirty(m);
        }

        static Material Rock(string name, string set, Color tint, float top, Texture2D topAlb, Texture2D topNrm)
        {
            var m = LoadOrCreate(name, Shader.Find("Gamebreak/RockTriplanar"));
            m.SetTexture("_RockAlb", Gp3Textures.Load(set, "albedo")); m.SetTexture("_RockNrm", Gp3Textures.Load(set, "normal"));
            if (topAlb) { m.SetTexture("_TopAlb", topAlb); m.SetTexture("_TopNrm", topNrm); }
            else { m.SetTexture("_TopAlb", Gp3Textures.Load(set, "albedo")); m.SetTexture("_TopNrm", Gp3Textures.Load(set, "normal")); }
            m.SetFloat("_RockTile", 2.2f); m.SetFloat("_TopTile", 2.2f);
            m.SetFloat("_TopCoverage", top); m.SetFloat("_TopSoftness", 0.15f);
            m.SetFloat("_NormalStrength", 1.2f); m.SetFloat("_AOStrength", 0.9f); m.SetFloat("_Smoothness", 0.2f);
            if (m.HasProperty("_Tint")) m.SetColor("_Tint", tint);
            var grain = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureGen.Dir + "/Grain.png");
            if (grain) m.SetTexture("_NoiseTex", grain);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Lit(string name, Color colour, float smooth, float metallic)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", colour);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metallic);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Re-skins the kit's turf and rails and builds every Graphics Pass 3 material. Call once per scene build, after <see cref="Gp3Textures.EnsureAll"/>.</summary>
        public static void Upgrade(TropicalKit kit, HeroKit hero)
        {
            Gp3Textures.EnsureAll();

            // --- the putting surface and the rails (re-skinned in place)
            Retex(hero.Turf, "turf", new Color(1f, 1f, 1f), 0.16f, 0.04f, 0.6f, new Vector2(1.6f, 1.6f));
            Retex(kit.Turf, "turf", new Color(1f, 1f, 1f), 0.16f, 0.04f, 0.6f, new Vector2(1.6f, 1.6f));
            Retex(hero.Rail, "railstone", new Color(1.04f, 0.98f, 0.92f), 0.18f, 0.06f, 1.2f, Vector2.one, 0.08f);
            Retex(kit.Rail, "railstone", new Color(1.04f, 0.98f, 0.92f), 0.18f, 0.06f, 1.2f, Vector2.one, 0.08f);

            // --- architecture and rock
            Sandstone = Surface("Gp3_Sandstone", "sandstone", new Color(1.05f, 0.98f, 0.9f), 0.2f, 0.06f, 1.2f, new Vector2(1f, 1f), 0.12f);
            Limestone = Surface("Gp3_Limestone", "limestone", Color.white, 0.2f, 0.05f, 1.1f, new Vector2(0.5f, 0.5f));
            LimestoneWet = Surface("Gp3_LimestoneWet", "limestone_wet", Color.white, 0.55f, 0.18f, 1.1f, new Vector2(0.5f, 0.5f), 0.14f);
            Basalt = Surface("Gp3_Basalt", "basalt", new Color(0.62f, 0.62f, 0.68f), 0.18f, 0.04f, 1.4f, new Vector2(0.5f, 0.5f), 0.04f);
            Glow(Basalt, "basalt", new Color(2.4f, 0.8f, 0.2f));
            Lava = Surface("Gp3_Lava", "lava", Color.white, 0.4f, 0.2f, 0.6f, Vector2.one, 0.0f, 0f);
            Glow(Lava, "lava", new Color(0.85f, 0.26f, 0.05f));
            LavaFall = Surface("Gp3_LavaFall", "lava", Color.white, 0.4f, 0.2f, 0.6f, Vector2.one, 0.0f, 0f);
            Glow(LavaFall, "lava", new Color(1.3f, 0.42f, 0.08f));
            Rope = Surface("Gp3_Rope", "rope", new Color(1.05f, 1f, 0.92f), 0.12f, 0.03f, 1.2f, new Vector2(4f, 4f));
            SunRelief = Surface("Gp3_SunRelief", "sunrelief", Color.white, 0.25f, 0.08f, 1.4f, Vector2.one, 0.12f);
            Banner = Surface("Gp3_Banner", "cloth", new Color(0.78f, 0.09f, 0.07f), 0.15f, 0.04f, 0.8f, new Vector2(3f, 3f), 0.1f);
            Banner.SetFloat("_Cull", 0f);
            Gold = Surface("Gp3_Gold", "metal", new Color(1.0f, 0.78f, 0.30f), 0.55f, 0.35f, 0.5f, new Vector2(3f, 3f), 0.15f);
            Brazier = Surface("Gp3_Brazier", "metal", new Color(0.46f, 0.34f, 0.22f), 0.4f, 0.2f, 0.6f, new Vector2(3f, 3f));
            WoodDark = hero.Wood;
            Ember = LoadOrCreate("Gp3_Ember", Stylized);
            Ember.SetTexture("_BaseMap", Texture2D.whiteTexture);
            Ember.SetColor("_BaseColor", new Color(1f, 0.55f, 0.15f));
            Ember.SetColor("_EmissionColor", new Color(3.2f, 1.5f, 0.45f));
            Ember.SetFloat("_DetailStrength", 0f); Ember.SetFloat("_WindStrength", 0f);
            EditorUtility.SetDirty(Ember);

            var lawn = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HeroKit.TexDir}/lawn_albedo.png");
            var lawnN = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HeroKit.TexDir}/lawn_normal.png");
            RockLimestone = Rock("Gp3_RockLimestone", "limestone", Color.white, 0.5f, lawn, lawnN);
            RockBasalt = Rock("Gp3_RockBasalt", "basalt", new Color(0.78f, 0.78f, 0.84f), 1f, null, null);   // _TopCoverage is a normal.y threshold: 1 = no cap at all
            RockSandstoneWarm = Rock("Gp3_RockSandstone", "sandstone", Color.white, 0.35f, lawn, lawnN);

            // --- golf realism (URP Lit for real metal response; the cup liner and ball use the stylized shader with their own maps)
            PutterBody = Lit("Putter_Body", new Color(0.30f, 0.31f, 0.33f), 0.62f, 1f);
            PutterBody.SetTexture("_BaseMap", Gp3Textures.Load("metal", "albedo"));
            PutterBody.SetTextureScale("_BaseMap", new Vector2(3f, 3f));
            PutterBevel = Lit("Putter_Bevel", new Color(0.82f, 0.84f, 0.87f), 0.82f, 1f);
            PutterInsert = Lit("Putter_Insert", new Color(0.075f, 0.08f, 0.09f), 0.45f, 0.85f);
            PutterLine = Lit("Putter_Line", new Color(0.97f, 0.97f, 0.95f), 0.3f, 0f);
            CupRim = Lit("Cup_Rim", new Color(0.78f, 0.80f, 0.83f), 0.7f, 1f);
            CupLiner = Surface("Cup_Liner", "cupliner", new Color(0.92f, 0.93f, 0.96f), 0.55f, 0.35f, 0.6f, Vector2.one, 0.04f, 1f);
            Ready = true;
        }

        static void Retex(Material m, string set, Color tint, float smooth, float spec, float bump, Vector2 tiling, float rim = 0.1f)
        {
            if (!m) return;
            m.SetTexture("_BaseMap", Gp3Textures.Load(set, "albedo"));
            m.SetTextureScale("_BaseMap", tiling);
            m.SetColor("_BaseColor", tint);
            m.SetTexture("_BumpMap", Gp3Textures.Load(set, "normal"));
            m.SetFloat("_BumpScale", bump);
            m.SetFloat("_UseNormalMap", 1f); m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_MaskMap", Gp3Textures.Load(set, "mask"));
            m.SetFloat("_UseMaskMap", 1f); m.EnableKeyword("_MASKMAP");
            m.SetFloat("_OcclusionStrength", 0.85f);
            m.SetFloat("_DetailStrength", 0f);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Specular", spec);
            m.SetColor("_RimColor", new Color(1f, 0.95f, 0.8f, rim));
            EditorUtility.SetDirty(m);
        }
    }
}
