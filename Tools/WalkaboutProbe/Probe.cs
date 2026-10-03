using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

[assembly: MelonInfo(typeof(WalkaboutProbe.Probe), "WalkaboutProbe", "0.1.0", "Gamebreak Labs")]
[assembly: MelonGame(null, null)]

namespace WalkaboutProbe
{
    /// <summary>
    /// Reads observable physics values and records motion. Commands go in UserData/probe/cmd.txt
    /// (one per line); results land next to it. By default it auto-records any rigidbody whose
    /// name contains "ball" and any transform whose name contains "putter" while they move.
    ///
    ///   settings          global physics and time settings
    ///   bodies            every rigidbody with colliders, physics materials and component type names
    ///   track <filter>    record rigidbodies whose name contains filter (default: ball)
    ///   trackxf <filter>  record transforms whose name contains filter (default: putter)
    ///   stop              stop recording
    /// </summary>
    public class Probe : MelonMod
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        string m_Dir;
        float m_NextPoll;
        string m_BodyFilter = "ball";
        string m_XfFilter = "putter";
        bool m_Recording = true;
        readonly List<Rigidbody> m_Bodies = new List<Rigidbody>();
        readonly List<Transform> m_Xfs = new List<Transform>();
        float m_NextRescan;
        StreamWriter m_BodyCsv, m_XfCsv;
        readonly Dictionary<IntPtr, Vector3> m_LastPos = new Dictionary<IntPtr, Vector3>();

        public override void OnInitializeMelon()
        {
            m_Dir = Path.Combine(MelonEnvironment.UserDataDirectory, "probe");
            Directory.CreateDirectory(m_Dir);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            m_BodyCsv = new StreamWriter(Path.Combine(m_Dir, $"bodies_{stamp}.csv")) { AutoFlush = true };
            m_BodyCsv.WriteLine("time,fixedDt,name,px,py,pz,vx,vy,vz,speed,wx,wy,wz,sleeping");
            m_XfCsv = new StreamWriter(Path.Combine(m_Dir, $"xforms_{stamp}.csv")) { AutoFlush = true };
            m_XfCsv.WriteLine("time,dt,name,px,py,pz,qx,qy,qz,qw,speed");
            LoggerInstance.Msg("WalkaboutProbe ready. Commands: " + Path.Combine(m_Dir, "cmd.txt"));
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            Log($"scene loaded {sceneName} ({buildIndex})");
            m_NextRescan = 0f;
        }

        public override void OnUpdate()
        {
            PollCommands();
            if (!m_Recording) return;
            if (Time.realtimeSinceStartup >= m_NextRescan) Rescan();
            foreach (var t in m_Xfs)
            {
                if (t == null) continue;
                Vector3 p = t.position;
                float speed = 0f;
                if (m_LastPos.TryGetValue(t.Pointer, out var last) && Time.deltaTime > 0f) speed = (p - last).magnitude / Time.deltaTime;
                m_LastPos[t.Pointer] = p;
                if (speed < 0.02f) continue; // only log while it moves
                var q = t.rotation;
                m_XfCsv.WriteLine(string.Join(",", F(Time.time), F(Time.deltaTime), Q(Path_(t)), F(p.x), F(p.y), F(p.z), F(q.x), F(q.y), F(q.z), F(q.w), F(speed)));
            }
        }

        public override void OnFixedUpdate()
        {
            if (!m_Recording) return;
            foreach (var rb in m_Bodies)
            {
                if (rb == null) continue;
                Vector3 v = rb.linearVelocity;
                if (v.sqrMagnitude < 1e-6f && rb.IsSleeping()) continue;
                Vector3 p = rb.position, w = rb.angularVelocity;
                m_BodyCsv.WriteLine(string.Join(",", F(Time.fixedTime), F(Time.fixedDeltaTime), Q(rb.name), F(p.x), F(p.y), F(p.z),
                    F(v.x), F(v.y), F(v.z), F(v.magnitude), F(w.x), F(w.y), F(w.z), rb.IsSleeping() ? "1" : "0"));
            }
        }

        void Rescan()
        {
            m_NextRescan = Time.realtimeSinceStartup + 3f;
            m_Bodies.Clear();
            foreach (var rb in Resources.FindObjectsOfTypeAll<Rigidbody>())
                if (InScene(rb.gameObject) && rb.name.ToLowerInvariant().Contains(m_BodyFilter)) m_Bodies.Add(rb);
            m_Xfs.Clear();
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                if (InScene(t.gameObject) && t.name.ToLowerInvariant().Contains(m_XfFilter)) m_Xfs.Add(t);
        }

        void PollCommands()
        {
            if (Time.realtimeSinceStartup < m_NextPoll) return;
            m_NextPoll = Time.realtimeSinceStartup + 1f;
            string file = Path.Combine(m_Dir, "cmd.txt");
            if (!File.Exists(file)) return;
            string[] lines;
            try { lines = File.ReadAllLines(file); File.Delete(file); } catch { return; }
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;
                try { Run(line); }
                catch (Exception e) { Log("ERROR " + line + ": " + e.Message); }
            }
        }

        void Run(string line)
        {
            var parts = line.Split(' ', 2);
            string arg = parts.Length > 1 ? parts[1].Trim().ToLowerInvariant() : null;
            switch (parts[0])
            {
                case "settings": Settings(); break;
                case "bodies": Bodies(); break;
                case "track": m_BodyFilter = arg ?? "ball"; m_Recording = true; m_NextRescan = 0f; Log("tracking bodies: " + m_BodyFilter); break;
                case "trackxf": m_XfFilter = arg ?? "putter"; m_Recording = true; m_NextRescan = 0f; Log("tracking transforms: " + m_XfFilter); break;
                case "stop": m_Recording = false; Log("recording stopped"); break;
                default: Log("unknown command: " + line); break;
            }
        }

        void Settings()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"gravity\t{Physics.gravity}");
            sb.AppendLine($"fixedDeltaTime\t{F(Time.fixedDeltaTime)}");
            sb.AppendLine($"maximumDeltaTime\t{F(Time.maximumDeltaTime)}");
            sb.AppendLine($"bounceThreshold\t{F(Physics.bounceThreshold)}");
            sb.AppendLine($"defaultContactOffset\t{F(Physics.defaultContactOffset)}");
            sb.AppendLine($"sleepThreshold\t{F(Physics.sleepThreshold)}");
            sb.AppendLine($"defaultSolverIterations\t{Physics.defaultSolverIterations}");
            sb.AppendLine($"defaultSolverVelocityIterations\t{Physics.defaultSolverVelocityIterations}");
            sb.AppendLine($"defaultMaxAngularSpeed\t{F(Physics.defaultMaxAngularSpeed)}");
            sb.AppendLine($"defaultMaxDepenetrationVelocity\t{F(Physics.defaultMaxDepenetrationVelocity)}");
            sb.AppendLine($"simulationMode\t{Physics.simulationMode}");
            sb.AppendLine($"autoSyncTransforms\t{Physics.autoSyncTransforms}");
            File.WriteAllText(Path.Combine(m_Dir, "settings.tsv"), sb.ToString());
            Log("settings written");
        }

        void Bodies()
        {
            var sb = new StringBuilder();
            sb.AppendLine("path\tmass\tdrag\tangularDrag\tuseGravity\tkinematic\tinterp\tcollision\tconstraints\tmaxAngVel\tsleepThr\tcolliders\tcomponents");
            foreach (var rb in Resources.FindObjectsOfTypeAll<Rigidbody>())
            {
                if (!InScene(rb.gameObject)) continue;
                var cols = new List<string>();
                foreach (var c in rb.GetComponentsInChildren<Collider>(true)) cols.Add(Describe(c));
                var comps = new List<string>();
                foreach (var c in rb.GetComponents<Component>()) comps.Add(c.GetIl2CppType().Name);
                sb.AppendLine(string.Join("\t", Path_(rb.transform), F(rb.mass), F(rb.linearDamping), F(rb.angularDamping),
                    rb.useGravity, rb.isKinematic, rb.interpolation, rb.collisionDetectionMode, rb.constraints,
                    F(rb.maxAngularVelocity), F(rb.sleepThreshold), string.Join(" | ", cols), string.Join(",", comps)));
            }
            File.WriteAllText(Path.Combine(m_Dir, "bodies.tsv"), sb.ToString());

            // Physics materials across all scene colliders, with use counts.
            var mats = new Dictionary<string, int>();
            foreach (var c in Resources.FindObjectsOfTypeAll<Collider>())
            {
                if (!InScene(c.gameObject)) continue;
                string key = MatString(c.sharedMaterial) + "\t" + c.GetIl2CppType().Name + "\ttrigger=" + c.isTrigger;
                mats[key] = mats.TryGetValue(key, out int n) ? n + 1 : 1;
            }
            var sm = new StringBuilder("material\tcolliderType\ttrigger\tcount\n");
            foreach (var kv in mats) sm.AppendLine(kv.Key + "\t" + kv.Value);
            File.WriteAllText(Path.Combine(m_Dir, "materials.tsv"), sm.ToString());
            Log("bodies and materials written");
        }

        static string Describe(Collider c)
        {
            string shape = c.GetIl2CppType().Name;
            var s = c.TryCast<SphereCollider>();
            if (s != null) shape += $"(r={F(s.radius)})";
            var b = c.TryCast<BoxCollider>();
            if (b != null) shape += $"(size={b.size})";
            var cap = c.TryCast<CapsuleCollider>();
            if (cap != null) shape += $"(r={F(cap.radius)},h={F(cap.height)})";
            return $"{c.name}:{shape} trig={c.isTrigger} offset={F(c.contactOffset)} mat={MatString(c.sharedMaterial)}";
        }

        static string MatString(PhysicsMaterial m) => m == null ? "none"
            : $"{m.name}[dyn={F(m.dynamicFriction)} stat={F(m.staticFriction)} bounce={F(m.bounciness)} fc={m.frictionCombine} bc={m.bounceCombine}]";

        static bool InScene(GameObject go)
        {
            var sc = go.scene;
            return sc.IsValid() && sc.isLoaded;
        }

        static string Path_(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (var p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }

        static string F(float f) => f.ToString("G6", Inv);
        static string Q(string s) => "\"" + (s ?? "").Replace("\"", "'") + "\"";

        void Log(string s) => File.AppendAllText(Path.Combine(m_Dir, "probe.log"), $"{DateTime.Now:HH:mm:ss} {s}\n");
    }
}
