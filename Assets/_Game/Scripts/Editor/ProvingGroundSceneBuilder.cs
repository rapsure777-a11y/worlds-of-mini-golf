using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gamebreak.MiniGolf.Editor
{
    /// <summary>
    /// Builds the Obstacle Proving Ground demonstration scene: four holes (launch ramp, waterwheel carrier, roulette bowl, and a jump from a ramp into the bowl) with the same ball,
    /// putter, rig, scorecard and feedback the real course uses. Writes only its own scene and materials under Assets/_Game/Worlds/ProvingGround;
    /// it never touches the Tropical scene, the Tropical theme asset or the build settings.
    /// Menu: Gamebreak/Proving Ground. Batch: -executeMethod Gamebreak.MiniGolf.Editor.ProvingGroundSceneBuilder.BuildDefaultBatch
    /// </summary>
    public static class ProvingGroundSceneBuilder
    {
        const string WorldDir = "Assets/_Game/Worlds/ProvingGround";
        const string MatDir = WorldDir + "/Materials";
        public const string ScenePath = WorldDir + "/ObstacleProvingGround.unity";
        public const string PlayerPath = "Builds/ProvingGround/ObstacleProvingGround.exe";
        const string TuningPath = "Assets/_Game/Resources/GolfTuning.asset";
        const string TropicalThemePath = "Assets/_Game/Worlds/Tropical/TropicalTheme.asset";

        [MenuItem("Gamebreak/Proving Ground/Build Scene (Default tuning)")]
        public static void BuildDefault() => Build(ProvingPreset.Default);

        [MenuItem("Gamebreak/Proving Ground/Build Scene (Easy tuning)")]
        public static void BuildEasy() => Build(ProvingPreset.Easy);

        [MenuItem("Gamebreak/Proving Ground/Build Scene (Hard tuning)")]
        public static void BuildHard() => Build(ProvingPreset.Hard);

        /// <summary>Batch entry point. Optional argument: -provingPreset Easy|Default|Hard.</summary>
        public static void BuildDefaultBatch()
        {
            var preset = ProvingPreset.Default;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-provingPreset" && System.Enum.TryParse(args[i + 1], true, out ProvingPreset parsed)) preset = parsed;
            Build(preset);
        }

        [MenuItem("Gamebreak/Proving Ground/Build Windows Player (PCVR, proving ground only)")]
        public static void BuildPlayer()
        {
            if (!File.Exists(ScenePath)) Build(ProvingPreset.Default);
            var bad = Automation.FindUnresolvableComponentScripts();
            if (bad.Length > 0)
            {
                Debug.LogError("[ProvingGround] Build blocked: component class name does not match file name in: " + string.Join(", ", bad));
                return;
            }
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = PlayerPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.CleanBuildCache,
            });
            Debug.Log($"[ProvingGround] Build {report.summary.result}: {report.summary.totalErrors} errors -> {PlayerPath}");
        }

        public static void Build(ProvingPreset preset)
        {
            Directory.CreateDirectory(MatDir);
            var tuning = LoadOrCreate<GolfTuning>(TuningPath);
            var tropical = AssetDatabase.LoadAssetAtPath<WorldTheme>(TropicalThemePath);
            var mats = tropical ? ProvingMaterials.FromTheme(tropical) : new ProvingMaterials();
            if (!mats.green) mats.green = Mat("Turf", new Color(0.22f, 0.68f, 0.24f), 0.15f);
            if (!mats.wall) mats.wall = Mat("Rail", new Color(0.62f, 0.4f, 0.22f), 0.25f);
            if (!mats.cup) mats.cup = Mat("CupInterior", new Color(0.92f, 0.92f, 0.9f), 0.3f);
            if (!mats.flag) mats.flag = Mat("Flag", new Color(0.95f, 0.25f, 0.15f), 0.3f);
            if (!mats.tee) mats.tee = Mat("Tee", new Color(0.12f, 0.45f, 0.75f), 0.2f);
            if (!mats.wood) mats.wood = Mat("Wood", new Color(0.55f, 0.36f, 0.2f), 0.2f);
            var ballMat = tropical && tropical.ball ? tropical.ball : Mat("Ball", Color.white, 0.8f);
            var ground = Mat("Ground", new Color(0.7f, 0.62f, 0.45f), 0.1f);
            var lineMat = tropical && tropical.teleportLine ? tropical.teleportLine : Mat("TeleportLine", new Color(0.3f, 0.9f, 1f), 0.5f);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.sun = sun;
            if (tropical && tropical.skybox) RenderSettings.skybox = tropical.skybox;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.78f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.7f, 0.6f);
            RenderSettings.ambientGroundColor = new Color(0.45f, 0.38f, 0.28f);

            // Ball (same set-up as the Tropical scene).
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
            ballVisual.GetComponent<MeshRenderer>().sharedMaterial = ballMat;
            ball.SetVisual(ballVisual.transform);

            // The demonstration holes.
            var courseRoot = new GameObject("ProvingGround").transform;
            var holes = ProvingGround.BuildAll(courseRoot, tuning, ball, mats, preset, ground);
            ballGo.transform.position = holes[0].TeePosition;

            // Player rig (mirrors SceneBuilder; no quality preset, post volume, music or title cards).
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
            var left = MakeHand("LeftHand", offset, ballMat);
            var right = MakeHand("RightHand", offset, ballMat);

            var putterGo = new GameObject("Putter");
            var putter = putterGo.AddComponent<Putter>();
            var shaft = tropical && tropical.putterShaft ? tropical.putterShaft : Mat("PutterShaft", new Color(0.8f, 0.82f, 0.85f), 0.85f, 1f);
            var head = tropical && tropical.putterHead ? tropical.putterHead : Mat("PutterHead", new Color(0.95f, 0.6f, 0.1f), 0.6f, 0.6f);
            var grip = tropical && tropical.putterGrip ? tropical.putterGrip : Mat("PutterGrip", new Color(0.1f, 0.1f, 0.12f), 0.2f);
            putter.Configure(tuning, right, ball, shaft, head, grip);

            var line = new GameObject("TeleportArc").AddComponent<LineRenderer>();
            line.transform.SetParent(rigGo.transform, false);
            line.widthMultiplier = 0.012f;
            line.numCapVertices = 4;
            line.sharedMaterial = lineMat;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.useWorldSpace = true;
            var reticle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            reticle.name = "TeleportReticle";
            Object.DestroyImmediate(reticle.GetComponent<Collider>());
            reticle.transform.SetParent(rigGo.transform, false);
            reticle.transform.localScale = new Vector3(0.35f, 0.004f, 0.35f);
            reticle.GetComponent<MeshRenderer>().sharedMaterial = lineMat;

            var courseGo = new GameObject("CourseController");
            var course = courseGo.AddComponent<CourseController>();
            var cardGo = new GameObject("Scorecard");
            cardGo.AddComponent<ScorecardPanel>().Configure(course);
            cardGo.SetActive(false);

            var rig = rigGo.AddComponent<VRRig>();
            rig.Configure(offset, cam, left, right, putter, course, line, reticle.transform, cardGo);
            course.Configure("Obstacle Proving Ground", holes, ball, rig);
            rigGo.transform.SetPositionAndRotation(holes[0].PlayerStart.position, holes[0].PlayerStart.rotation);

            new GameObject("WristDisplay").AddComponent<WristDisplay>().Configure(course, rig);
            courseGo.AddComponent<GolfFeedback>().Configure(putter, ball, rig, course);
            courseGo.AddComponent<DebugOverlay>().Configure(course, rig);
            courseGo.AddComponent<SessionLog>().Configure(course, rig);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ProvingGround] Built {ScenePath} ({preset} tuning, {holes.Length} holes). Build settings were not changed.");
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

        static Material Mat(string name, Color color, float smoothness, float metallic = 0f)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (!shader) shader = Shader.Find("Standard");
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.SetColor("_Color", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
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
