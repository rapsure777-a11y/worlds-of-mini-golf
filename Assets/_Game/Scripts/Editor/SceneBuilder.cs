using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gamebreak.MiniGolf.Editor
{
    /// <summary>
    /// Rebuilds the Tropical Adventure scene from code. Re-run it after changing hole layouts;
    /// hand-placed set dressing should live in a separate additive scene or prefab so it survives.
    /// </summary>
    public static class SceneBuilder
    {
        const string WorldDir = "Assets/_Game/Worlds/Tropical";
        const string MatDir = WorldDir + "/Materials";
        const string ScenePath = WorldDir + "/Scenes/TropicalAdventure.unity";
        const string TuningPath = "Assets/_Game/Resources/GolfTuning.asset";
        const string ThemePath = WorldDir + "/TropicalTheme.asset";
        const string MusicDir = "Assets/_Game/Audio/Music/";

        [MenuItem("Gamebreak/Build Tropical Scene")]
        public static void BuildTropicalScene()
        {
            var tuning = LoadOrCreate<GolfTuning>(TuningPath);
            var clusters = TropicalCourse.Clusters();
            ProjectSetup.EnsureQualityAssets();
            var kit = Art.TropicalKit.Build();
            var hero = Art.HeroKit.Build(kit);
            Art.Gp3.Run("textures and materials", () => Art.Gp3Materials.Upgrade(kit, hero));   // Graphics Pass 3: turf, stone rails, golf realism, biome materials
            var theme = CreateTropicalTheme();
            // Putting surface and rails use the Blender-baked PBR sets (Hero_Turf: low bump, Hero_Rail: wood). The cup, flag and tee stay on the kit.
            theme.deck = hero.Deck;
            theme.green = hero.Turf; theme.wall = hero.Rail; theme.cup = kit.Cup; theme.flag = kit.Flag; theme.tee = kit.Tee;
            theme.water = kit.Water;
            if (Art.Gp3Materials.Ready)
            {
                // Graphics Pass 3 golf realism: brushed-steel cup liner and rim, a refined putter head (the shaft and grip are unchanged).
                theme.cup = Art.Gp3Materials.CupLiner; theme.cupRim = Art.Gp3Materials.CupRim;
                theme.putterHead = Art.Gp3Materials.PutterBody; theme.putterBevel = Art.Gp3Materials.PutterBevel;
                theme.putterInsert = Art.Gp3Materials.PutterInsert; theme.putterLine = Art.Gp3Materials.PutterLine;
            }
            // Music clusters come from the archipelago data (TropicalCourse.Clusters): one track per island cluster.
            var musicClusters = new List<MusicCluster>();
            foreach (var c in clusters)
                musicClusters.Add(Cluster(c.displayName, c.holes[0], c.holes[c.holes.Length - 1], c.musicName));
            theme.musicClusters = musicClusters.ToArray();
            theme.music = theme.musicClusters[0].clip;
            theme.musicVolume = 0.55f; // Andrew, headset: 0.3 was too quiet ("almost twice as loud")
            EditorUtility.SetDirty(theme);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Lighting.
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = theme.sunColor;
            sun.intensity = theme.sunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(theme.sunEuler);
            RenderSettings.sun = sun;
            RenderSettings.skybox = theme.skybox;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.78f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.7f, 0.6f);
            RenderSettings.ambientGroundColor = new Color(0.45f, 0.38f, 0.28f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = theme.fogColor;
            RenderSettings.fogDensity = theme.fogDensity;

            // Ball.
            var ballGo = new GameObject("GolfBall");
            ballGo.AddComponent<Rigidbody>();
            ballGo.AddComponent<SphereCollider>();
            var ball = ballGo.AddComponent<GolfBall>();
            ball.SetTuning(tuning);
            var ballVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballVisual.name = "Visual";
            Object.DestroyImmediate(ballVisual.GetComponent<Collider>());
            ballVisual.transform.SetParent(ballGo.transform, false);
            ballVisual.transform.localScale = Vector3.one * tuning.ballRadius * 2f;
            ballVisual.GetComponent<MeshRenderer>().sharedMaterial = theme.ball;
            ball.SetVisual(ballVisual.transform);

            // Course.
            var courseRoot = new GameObject("Course").transform;
            var defs = TropicalCourse.Holes();
            var holes = new HoleController[defs.Count];
            for (int i = 0; i < defs.Count; i++)
                holes[i] = HoleFactory.Build(defs[i], theme, tuning, courseRoot, ball);
            if (holes.Length > 0) ballGo.transform.position = holes[0].TeePosition;

            Art.TropicalWorld.Build(kit, hero, defs, clusters, null);

            // Player rig.
            var rigGo = new GameObject("PlayerRig");
            var offset = new GameObject("CameraOffset").transform;
            offset.SetParent(rigGo.transform, false);
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(offset, false);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 400f;
            camGo.AddComponent<AudioListener>();
            // Post-processing: a global Volume (tonemap, grade, low bloom) that only the Rich preset turns on.
            // QualityPreset swaps the lean / balanced / rich URP assets and toggles the camera flag at runtime.
            var volume = new GameObject("PostProcessVolume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = BuildPostProfile();
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var quality = new GameObject("QualityPreset").AddComponent<QualityPreset>();
            quality.Configure(
                AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(ProjectSetup.LeanAssetPath),
                AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(ProjectSetup.BalancedAssetPath),
                AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(ProjectSetup.RichAssetPath),
                QualityLevel.Balanced, cam, volume);
            // Ablation probe for VR frame cost (starts only with -probe or F10 in a built player; see FrameCostProbe).
            new GameObject("FrameCostProbe").AddComponent<FrameCostProbe>();
            // Light, small marker for the free hand (a dark 5 cm sphere read as a "black ball" in VR).
            var left = MakeHand("LeftHand", offset, theme.ball);
            var right = MakeHand("RightHand", offset, theme.ball);

            var putterGo = new GameObject("Putter");
            var putter = putterGo.AddComponent<Putter>();
            putter.Configure(tuning, right, ball, theme.putterShaft, theme.putterHead, theme.putterGrip, theme.putterBevel, theme.putterInsert, theme.putterLine);

            var line = new GameObject("TeleportArc").AddComponent<LineRenderer>();
            line.transform.SetParent(rigGo.transform, false);
            line.widthMultiplier = 0.012f;
            line.numCapVertices = 4;
            line.sharedMaterial = theme.teleportLine;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.useWorldSpace = true;
            var reticle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            reticle.name = "TeleportReticle";
            Object.DestroyImmediate(reticle.GetComponent<Collider>());
            reticle.transform.SetParent(rigGo.transform, false);
            reticle.transform.localScale = new Vector3(0.35f, 0.004f, 0.35f);
            reticle.GetComponent<MeshRenderer>().sharedMaterial = theme.teleportLine;

            var courseGo = new GameObject("CourseController");
            var course = courseGo.AddComponent<CourseController>();

            var cardGo = new GameObject("Scorecard");
            var card = cardGo.AddComponent<ScorecardPanel>();
            card.Configure(course);
            cardGo.SetActive(false);

            var rig = rigGo.AddComponent<VRRig>();
            rig.Configure(offset, cam, left, right, putter, course, line, reticle.transform, cardGo);
            course.Configure(TropicalCourse.Name, holes, ball, rig);
            // Graphics Pass 3: each biome gets its own sun colour and height, ambient, fog and sky (eased in as the player moves between islands).
            Art.Gp3.Run("biome atmosphere", () => new GameObject("BiomeAtmosphere").AddComponent<BiomeAtmosphere>().Configure(course, sun, kit.Sky));

            var fadeMat = new Material(Shader.Find("Gamebreak/Mist")) { name = "TransitionFade" };
            fadeMat.SetColor("_BaseColor", new Color(0f, 0f, 0f, 0f));
            new GameObject("TransitionFade").AddComponent<TransitionFade>().Configure(course, cam, fadeMat, clusters);
            // Cinematic title card on arriving at each island cluster (and at the start).
            new GameObject("AreaTitleCard").AddComponent<AreaTitleCard>().Configure(course, rig, clusters);
            if (holes.Length > 0)
            {
                rigGo.transform.SetPositionAndRotation(holes[0].PlayerStart.position, holes[0].PlayerStart.rotation);
            }

            var wrist = new GameObject("WristDisplay").AddComponent<WristDisplay>();
            wrist.Configure(course, rig);
            var feedback = courseGo.AddComponent<GolfFeedback>();
            feedback.Configure(putter, ball, rig, course);
            // Under-par celebrations (confetti, floating label, fanfare). Particles use a bright copy of the soft Mist material.
            var mistSource = AssetDatabase.LoadAssetAtPath<Material>(Art.TropicalKit.Root + "/Materials/Hero_Mist.mat");
            Material burstMat = null;
            if (mistSource)
            {
                burstMat = new Material(mistSource) { name = "ScoreBurst" };
                burstMat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 1f));
                burstMat.SetFloat("_SoftDistance", 0.1f);
            }
            else Debug.LogWarning("[Gamebreak] Hero_Mist material not found; score confetti will use the default particle material.");
            courseGo.AddComponent<ScoreCelebration>().Configure(course, rig, burstMat);
            var overlay = courseGo.AddComponent<DebugOverlay>();
            overlay.Configure(course, rig);
            var sessionLog = courseGo.AddComponent<SessionLog>();
            sessionLog.Configure(course, rig);
            // Music by island cluster (one track per range of holes), following the course.
            new GameObject("Music").AddComponent<MusicPlayer>().Configure(theme.musicClusters, theme.music, theme.musicVolume, course);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"[Gamebreak] Built {ScenePath} with {holes.Length} hole(s).");
        }

        /// <summary>
        /// Global post-processing profile for the Rich preset. Deliberately minimal for VR: tonemapping, a colour grade
        /// and one low-resolution bloom (no SSAO, DoF, motion blur, vignette or film grain). Tune values here; the
        /// profile is regenerated by every scene build.
        /// </summary>
        static VolumeProfile BuildPostProfile()
        {
            const string path = WorldDir + "/TropicalPostProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            foreach (var old in profile.components.ToArray())
            {
                AssetDatabase.RemoveObjectFromAsset(old);
                Object.DestroyImmediate(old, true);
            }
            profile.components.Clear();
            T Add<T>() where T : VolumeComponent
            {
                var c = profile.Add<T>(true);
                c.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(c, profile);
                return c;
            }
            var tone = Add<Tonemapping>();
            tone.mode.Override(TonemappingMode.Neutral);   // keeps the saturated palette; ACES crushes it toward filmic
            var bloom = Add<Bloom>();
            bloom.threshold.Override(1.1f);
            bloom.intensity.Override(0.22f);
            bloom.scatter.Override(0.55f);
            bloom.tint.Override(new Color(1f, 0.96f, 0.88f));
            bloom.highQualityFiltering.Override(false);
            bloom.downscale.Override(BloomDownscaleMode.Quarter);
            bloom.maxIterations.Override(4);
            var grade = Add<ColorAdjustments>();
            grade.postExposure.Override(0.1f);
            grade.contrast.Override(10f);
            grade.saturation.Override(14f);
            var wb = Add<WhiteBalance>();
            wb.temperature.Override(8f);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        static Transform MakeHand(string name, Transform parent, Material mat)
        {
            var hand = new GameObject(name).transform;
            hand.SetParent(parent, false);
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "Marker";
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.transform.SetParent(hand, false);
            marker.transform.localScale = Vector3.one * 0.03f;
            marker.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return hand;
        }

        static WorldTheme CreateTropicalTheme()
        {
            Directory.CreateDirectory(MatDir);
            var theme = LoadOrCreate<WorldTheme>(ThemePath);
            theme.worldName = TropicalCourse.Name;
            theme.inspiration = "Crash Bandicoot 4: It's About Time (style reference only).";
            theme.green = Mat("Green", new Color(0.22f, 0.68f, 0.24f), 0.15f);
            theme.wall = Mat("WallWood", new Color(0.62f, 0.4f, 0.22f), 0.25f);
            theme.cup = Mat("CupInterior", new Color(0.92f, 0.92f, 0.9f), 0.3f);
            theme.flag = Mat("Flag", new Color(0.95f, 0.25f, 0.15f), 0.3f);
            theme.tee = Mat("TeeMat", new Color(0.12f, 0.45f, 0.75f), 0.2f);
            theme.ball = BallMaterial();
            theme.putterShaft = Mat("PutterShaft", new Color(0.8f, 0.82f, 0.85f), 0.85f, 1f);
            theme.putterHead = Mat("PutterHead", new Color(0.95f, 0.6f, 0.1f), 0.6f, 0.6f);
            theme.putterGrip = Mat("PutterGrip", new Color(0.1f, 0.1f, 0.12f), 0.2f);
            theme.ground = Mat("Sand", new Color(0.96f, 0.86f, 0.62f), 0.1f);
            theme.water = Mat("Water", new Color(0.1f, 0.62f, 0.78f), 0.92f);
            theme.teleportLine = UnlitMat("TeleportLine", new Color(0.3f, 0.9f, 1f));
            theme.skybox = Skybox();
            theme.sunColor = new Color(1f, 0.95f, 0.85f);
            theme.sunIntensity = 1.4f;
            theme.sunEuler = new Vector3(48f, -35f, 0f);
            theme.fogColor = new Color(0.66f, 0.86f, 0.96f);
            theme.fogDensity = 0.004f;
            EditorUtility.SetDirty(theme);
            return theme;
        }

        static MusicCluster Cluster(string name, int firstHole, int lastHole, string track)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{MusicDir}{track}.ogg");
            if (!clip) Debug.LogWarning($"[Gamebreak] Music cluster '{name}': missing {MusicDir}{track}.ogg");
            return new MusicCluster { name = name, firstHole = firstHole, lastHole = lastHole, clip = clip };
        }

        static Material Mat(string name, Color color, float smoothness, float metallic = 0f)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material UnlitMat(string name, Color color)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>White ball with a coloured band so the roll is visible.</summary>
        static Material BallMaterial()
        {
            string texPath = $"{MatDir}/BallStripe.png";
            if (!File.Exists(texPath))
            {
                var tex = new Texture2D(128, 64, TextureFormat.RGBA32, false);
                for (int y = 0; y < 64; y++)
                for (int x = 0; x < 128; x++)
                {
                    bool band = Mathf.Abs(y - 32) < 4;
                    bool dot = (x % 64 > 26 && x % 64 < 38) && Mathf.Abs(y - 50) < 6;
                    tex.SetPixel(x, y, band ? new Color(1f, 0.45f, 0.1f) : dot ? new Color(0.1f, 0.5f, 0.9f) : Color.white);
                }
                File.WriteAllBytes(texPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(texPath);
            }
            var m = Mat("Ball", Color.white, 0.75f);
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            // Graphics Pass 3: golf-ball dimples as a normal map (the stripe stays, so the roll is still visible). Same sphere, same physics.
            var dimples = Art.Gp3Textures.Load("ball", "normal");
            if (dimples)
            {
                m.SetTexture("_BumpMap", dimples);
                m.SetFloat("_BumpScale", 1.1f);
                m.EnableKeyword("_NORMALMAP");
            }
            return m;
        }

        static Material Skybox()
        {
            string path = $"{MatDir}/TropicalSky.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetFloat("_SunSize", 0.04f);
            m.SetFloat("_AtmosphereThickness", 0.8f);
            m.SetColor("_SkyTint", new Color(0.45f, 0.7f, 1f));
            m.SetColor("_GroundColor", new Color(0.25f, 0.55f, 0.65f));
            m.SetFloat("_Exposure", 1.25f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a) return a;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }
    }
}
