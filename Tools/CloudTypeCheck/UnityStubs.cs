using System;
using System.Collections;
using System.Collections.Generic;
namespace UnityEngine
{
    public class Object { public string name; public static void Destroy(Object o){} public static void DestroyImmediate(Object o){} public static implicit operator bool(Object o) => o != null && !ReferenceEquals(o,null);
        public static T Instantiate<T>(T o) where T:Object => o; public static void DontDestroyOnLoad(Object o){} }
    public class Component : Object { public GameObject gameObject; public Transform transform;
        public T GetComponent<T>() => default; public T GetComponentInChildren<T>() => default; public T GetComponentInChildren<T>(bool inc) => default; public T GetComponentInParent<T>() => default;
        public T[] GetComponentsInChildren<T>(bool inc) => null; public T[] GetComponentsInChildren<T>() => null; }
    public class Behaviour : Component { public bool enabled; }
    public class MonoBehaviour : Behaviour { public Coroutine StartCoroutine(IEnumerator r)=>null; public void StopCoroutine(Coroutine c){} }
    public class Coroutine {}
    public class WaitForSeconds : YieldInstruction { public WaitForSeconds(float s){} }
    public class YieldInstruction {}
    public class WaitForFixedUpdate : YieldInstruction {}
    public class ScriptableObject : Object { public static T CreateInstance<T>() where T:ScriptableObject => null; }
    public class GameObject : Object { public GameObject(){} public GameObject(string n){} public Transform transform; public bool activeSelf; public void SetActive(bool b){}
        public T AddComponent<T>() where T:Component => null; public T GetComponent<T>() => default; public T[] GetComponentsInChildren<T>() => null; public T GetComponentInChildren<T>() => default; public static GameObject CreatePrimitive(PrimitiveType t)=>null; public string tag; }
    public enum PrimitiveType { Sphere, Capsule, Cylinder, Cube, Plane, Quad }
    public class Transform : Component { public Vector3 position, localPosition, localScale, forward, up, right; public int childCount; public Quaternion rotation, localRotation;
        public void SetParent(Transform p){} public void SetParent(Transform p, bool w){} public Transform Find(string n)=>null; public Vector3 TransformPoint(Vector3 p)=>p; public Vector3 InverseTransformPoint(Vector3 p)=>p;
        public Vector3 TransformVector(Vector3 v)=>v; public Vector3 TransformDirection(Vector3 v)=>v; public void Rotate(Vector3 a,float f,Space s){} public void SetPositionAndRotation(Vector3 p,Quaternion q){} }
    public enum Space { World, Self }
    [AttributeUsage(AttributeTargets.Field)] public class SerializeField : Attribute {}
    [AttributeUsage(AttributeTargets.Field)] public class TooltipAttribute : Attribute { public TooltipAttribute(string s){} }
    [AttributeUsage(AttributeTargets.Field)] public class HeaderAttribute : Attribute { public HeaderAttribute(string s){} }
    [AttributeUsage(AttributeTargets.Field)] public class RangeAttribute : Attribute { public RangeAttribute(float a,float b){} }
    [AttributeUsage(AttributeTargets.Field)] public class TextAreaAttribute : Attribute {}
    [AttributeUsage(AttributeTargets.Class)] public class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int o){} }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple=true)] public class RequireComponent : Attribute { public RequireComponent(params Type[] t){} }
    [AttributeUsage(AttributeTargets.Method)] public class ContextMenu : Attribute { public ContextMenu(string s){} }
    [AttributeUsage(AttributeTargets.Class)] public class CreateAssetMenuAttribute : Attribute { public string menuName, fileName; }
    [AttributeUsage(AttributeTargets.Method)] public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){} }
    public enum RuntimeInitializeLoadType { BeforeSceneLoad }
    public struct Vector2 { public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;} public float magnitude=>0; public float sqrMagnitude=>0; public Vector2 normalized=>this; public static Vector2 zero, one, right, up, left;
        public static Vector2 operator+(Vector2 a,Vector2 b)=>a; public static Vector2 operator-(Vector2 a,Vector2 b)=>a; public static Vector2 operator-(Vector2 a)=>a; public static Vector2 operator*(Vector2 a,float f)=>a; public static Vector2 operator*(float f,Vector2 a)=>a; public static Vector2 operator/(Vector2 a,float f)=>a;
        public static float Distance(Vector2 a,Vector2 b)=>0; public static float Dot(Vector2 a,Vector2 b)=>0; public static float Angle(Vector2 a,Vector2 b)=>0; public static implicit operator Vector2(Vector3 v)=>default; public static implicit operator Vector3(Vector2 v)=>default; }
    public struct Vector3 { public float x,y,z; public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;} public Vector3(float x,float y){this.x=x;this.y=y;z=0;} public static bool operator==(Vector3 a,Vector3 b)=>true; public static bool operator!=(Vector3 a,Vector3 b)=>false; public override bool Equals(object o)=>false; public override int GetHashCode()=>0;
        public static Vector3 zero, up, down, forward, back, left, right, one;
        public float magnitude=>0; public float sqrMagnitude=>0; public Vector3 normalized=>this;
        public static Vector3 operator+(Vector3 a,Vector3 b)=>a; public static Vector3 operator-(Vector3 a,Vector3 b)=>a; public static Vector3 operator-(Vector3 a)=>a; public static Vector3 operator*(Vector3 a,float f)=>a; public static Vector3 operator*(float f,Vector3 a)=>a; public static Vector3 operator/(Vector3 a,float f)=>a;
        public static float Dot(Vector3 a,Vector3 b)=>0; public static Vector3 Cross(Vector3 a,Vector3 b)=>a; public static float Distance(Vector3 a,Vector3 b)=>0; public static Vector3 Lerp(Vector3 a,Vector3 b,float t)=>a; public void Normalize(){} public static float Angle(Vector3 a,Vector3 b)=>0;
        public string ToString(string f)=>""; }
    public struct Quaternion { public static Quaternion identity; public static Quaternion Euler(float x,float y,float z)=>default; public static Quaternion Euler(Vector3 v)=>default; public static Quaternion LookRotation(Vector3 f)=>default; public static Quaternion LookRotation(Vector3 f,Vector3 u)=>default;
        public static Quaternion operator*(Quaternion a,Quaternion b)=>a; public static Vector3 operator*(Quaternion a,Vector3 v)=>v; }
    public struct Rect { public Rect(float x,float y,float w,float h){this.x=x;this.y=y;width=w;height=h;xMin=x;yMin=y;xMax=x+w;yMax=y+h;} public float xMin,xMax,yMin,yMax,x,y,width,height; public bool Contains(Vector2 p)=>false; public bool Overlaps(Rect o)=>false; public static Rect MinMaxRect(float a,float b,float c,float d)=>default; public Vector2 center, size; }
    public struct Color { public float r,g,b,a; public Color(float r,float g,float b){this.r=r;this.g=g;this.b=b;a=1;} public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;} public static Color white; }
    public static class Mathf { public const float PI=3.14159265f; public const float Deg2Rad=0.0174532924f, Rad2Deg=57.29578f;
        public static float Clamp(float v,float a,float b)=>v; public static int Clamp(int v,int a,int b)=>v; public static float Clamp01(float v)=>v; public static float Max(float a,float b)=>a; public static int Max(int a,int b)=>a; public static float Min(float a,float b)=>a; public static int Min(int a,int b)=>a;
        public static float Abs(float v)=>v; public static bool Approximately(float a,float b)=>a==b; public static float Sqrt(float v)=>v; public static float Sin(float v)=>v; public static float Cos(float v)=>v; public static float Tan(float v)=>v; public static float Atan(float v)=>v; public static float Repeat(float a,float b)=>a;
        public static float Lerp(float a,float b,float t)=>a; public static float InverseLerp(float a,float b,float v)=>a; public static float Round(float v)=>v; public static int RoundToInt(float v)=>0; public static int FloorToInt(float v)=>0; public static int CeilToInt(float v)=>0; public static float Floor(float v)=>v;
        public static float Log(float v)=>v; public static float DeltaAngle(float a,float b)=>a; public static float Pow(float a,float b)=>a; public static float Atan2(float a,float b)=>a; public static float Sign(float v)=>v; }
    public class Material : Object { public Material(Shader s){} public void SetColor(string n,Color c){} public void SetFloat(string n,float v){} public bool HasProperty(string n)=>false; }
    public class Shader : Object { public static Shader Find(string n)=>null; }
    public class PhysicsMaterial : Object { public PhysicsMaterial(string n){} public float dynamicFriction,staticFriction,bounciness; public PhysicsMaterialCombine frictionCombine,bounceCombine; }
    public enum PhysicsMaterialCombine { Average, Minimum, Multiply, Maximum }
    public class Collider : Component { public bool isTrigger; public PhysicsMaterial sharedMaterial; public Rigidbody attachedRigidbody; }
    public class BoxCollider : Collider {} public class SphereCollider : Collider { public float radius,contactOffset; } public class MeshCollider : Collider { public Mesh sharedMesh; }
    public class Collision { public Collider collider; public int contactCount; public ContactPoint GetContact(int i)=>default; }
    public struct ContactPoint { public Vector3 point, normal; public float separation; }
    public enum CollisionDetectionMode { Discrete, Continuous, ContinuousDynamic, ContinuousSpeculative }
    public enum RigidbodyConstraints { FreezeRotation }
    public enum RigidbodyInterpolation { None, Interpolate }
    public class Rigidbody : Component { public float mass,linearDamping,angularDamping,sleepThreshold,maxDepenetrationVelocity; public bool useGravity,isKinematic; public RigidbodyConstraints constraints; public RigidbodyInterpolation interpolation; public CollisionDetectionMode collisionDetectionMode;
        public Vector3 linearVelocity, position; public void MovePosition(Vector3 p){} }
    public class Mesh : Object { public string name; public UnityEngine.Rendering.IndexFormat indexFormat; public int subMeshCount; public Vector3[] vertices; public int[] triangles; public void SetVertices(List<Vector3> v){} public void SetUVs(int c,List<Vector2> u){} public void SetTriangles(List<int> t,int s){}
        public void RecalculateNormals(){} public void RecalculateTangents(){} public void RecalculateBounds(){} }
    public class MeshFilter : Component { public Mesh sharedMesh; } public class Renderer : Component { public Material sharedMaterial; public Material[] sharedMaterials; } public class MeshRenderer : Renderer {}
    public static class Physics { public static Vector3 gravity; public static float bounceThreshold, defaultContactOffset; public static int defaultSolverIterations, defaultSolverVelocityIterations; public static void SyncTransforms(){}
        public static RaycastHit[] RaycastAll(Vector3 o,Vector3 d,float m,int mask,QueryTriggerInteraction q)=>null; }
    public struct RaycastHit { public Collider collider; } public enum QueryTriggerInteraction { Ignore }
    public static class Time { public static float time, fixedTime, fixedDeltaTime, deltaTime, timeScale, maximumDeltaTime; }
    public static class Debug { public static void Log(object o){} public static void LogWarning(object o){} public static void LogError(object o){} }
    public static class Application { public static bool isPlaying, isBatchMode; }
    public static class Resources { public static T Load<T>(string p) where T:Object => null; }
}
namespace UnityEngine.Rendering { public enum IndexFormat { UInt16, UInt32 } }
namespace UnityEngine { public class AudioClip : Object {} }
namespace UnityEngine.TestTools { [System.AttributeUsage(System.AttributeTargets.Method)] public class UnityTestAttribute : System.Attribute {} }
