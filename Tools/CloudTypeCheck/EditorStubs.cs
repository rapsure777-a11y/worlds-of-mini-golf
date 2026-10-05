using System;
using System.Collections.Generic;
namespace UnityEngine {
    public class Camera : Component { public float nearClipPlane, farClipPlane; }
    public class AudioListener : Behaviour {}
    public class Light : Behaviour { public LightType type; public float intensity; public LightShadows shadows; public Color color; }
    public enum LightType { Directional } public enum LightShadows { Soft }
    public class LineRenderer : Renderer { public float widthMultiplier; public int numCapVertices; public bool useWorldSpace; public UnityEngine.Rendering.ShadowCastingMode shadowCastingMode; }
    public static class RenderSettings { public static Light sun; public static Material skybox; public static UnityEngine.Rendering.AmbientMode ambientMode; public static Color ambientSkyColor, ambientEquatorColor, ambientGroundColor; }
}
namespace UnityEngine.Rendering { public enum ShadowCastingMode { Off } public enum AmbientMode { Trilight } }
namespace Gamebreak.MiniGolf.Editor { public static class Automation { public static string[] FindUnresolvableComponentScripts()=>null; } }
namespace UnityEditor {
    [AttributeUsage(AttributeTargets.Method)] public class MenuItem : Attribute { public MenuItem(string s){} }
    public static class AssetDatabase { public static T LoadAssetAtPath<T>(string p) where T:UnityEngine.Object=>null; public static void CreateAsset(UnityEngine.Object o,string p){} public static void SaveAssets(){} }
    public static class EditorUtility { public static void SetDirty(UnityEngine.Object o){} }
    public enum BuildTarget { StandaloneWindows64 } [Flags] public enum BuildOptions { None=0, CleanBuildCache=1 }
    public struct BuildPlayerOptions { public string[] scenes; public string locationPathName; public BuildTarget target; public BuildOptions options; }
    public static class BuildPipeline { public static UnityEditor.Build.Reporting.BuildReport BuildPlayer(BuildPlayerOptions o)=>null; }
}
namespace UnityEditor.Build.Reporting { public class BuildReport { public BuildSummary summary; } public class BuildSummary { public object result; public int totalErrors; } }
namespace UnityEditor.SceneManagement {
    public enum NewSceneSetup { EmptyScene } public enum NewSceneMode { Single }
    public struct Scene {}
    public static class EditorSceneManager { public static Scene NewScene(NewSceneSetup s, NewSceneMode m)=>default; public static bool SaveScene(Scene s,string p)=>true; }
}

namespace Gamebreak.MiniGolf {
 using UnityEngine;
    public class Putter : MonoBehaviour { public void Configure(GolfTuning t, Transform handTransform, GolfBall golfBall, Material shaft, Material head, Material grip){} }
    public class CourseController : MonoBehaviour { public void Configure(string name, HoleController[] holeList, GolfBall golfBall, MonoBehaviour mover){} }
    public class VRRig : MonoBehaviour { public void Configure(Transform offset, Camera cam, Transform left, Transform right, Putter p, CourseController c, LineRenderer line, Transform reticle, GameObject card){} }
    public class ScorecardPanel : MonoBehaviour { public void Configure(CourseController c){} }
    public class WristDisplay : MonoBehaviour { public void Configure(CourseController c, VRRig r){} }
    public class GolfFeedback : MonoBehaviour { public void Configure(Putter p, GolfBall b, VRRig r, CourseController c){} }
    public class DebugOverlay : MonoBehaviour { public void Configure(CourseController c, VRRig r){} }
    public class SessionLog : MonoBehaviour { public void Configure(CourseController c, VRRig r){} }
}
