using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Gamebreak.MiniGolf.Tests
{
    /// <summary>
    /// Graphics Pass 3 golf-contact visuals: the putter head, the cup rim and the rail UVs. They are visual only: these tests pin that the strike box, the cup mechanics
    /// and the green geometry are untouched, and that the new meshes stay inside the physical envelopes.
    /// </summary>
    public class GolfVisualTests
    {
        static Material AnyMaterial() => new Material(Shader.Find("Sprites/Default"));

        // ------------------------------------------------------------------ putter head mesh

        [Test]
        public void PutterHeadMesh_StaysInsideTheStrikeBox_WithAHoselTowardTheShaft()
        {
            var size = GolfTuning.Default.headSize;
            var mesh = GolfVisualMeshes.PutterHead(size);
            try
            {
                var v = mesh.vertices;
                Assert.Greater(v.Length, 100);
                const float tol = 0.0005f;
                foreach (var p in v)
                {
                    Assert.LessOrEqual(Mathf.Abs(p.x), size.x * 0.5f + tol, "within the face-to-back thickness");
                    if (p.z > -size.z * 0.5f - 0.001f) Assert.LessOrEqual(Mathf.Abs(p.y), size.y * 0.5f + tol, "within the toe-to-heel length");
                    Assert.LessOrEqual(p.z, size.z * 0.5f + tol, "nothing sticks out of the sole");
                    Assert.GreaterOrEqual(p.z, -size.z * 0.5f - GolfVisualMeshes.HoselLength - tol, "the hosel is the only thing above the head");
                }
                // The faces are exactly the strike box's faces, so what the player sees is what hits the ball.
                Assert.That(v.Max(p => p.x), Is.EqualTo(size.x * 0.5f).Within(tol));
                Assert.That(v.Min(p => p.x), Is.EqualTo(-size.x * 0.5f).Within(tol));
                Assert.That(v.Max(p => p.y), Is.EqualTo(size.y * 0.5f).Within(tol));
                Assert.That(v.Max(p => p.z), Is.EqualTo(size.z * 0.5f).Within(tol));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void PutterHeadMesh_HasAllFourFinishes_AndEveryTriangleFacesOutward()
        {
            var mesh = GolfVisualMeshes.PutterHead(GolfTuning.Default.headSize);
            try
            {
                Assert.AreEqual(4, mesh.subMeshCount, "body, bevel, insert, line");
                for (int s = 0; s < 4; s++) Assert.Greater(mesh.GetTriangles(s).Length, 0, $"submesh {s} is empty");
                var v = mesh.vertices; var n = mesh.normals;
                Assert.AreEqual(v.Length, n.Length);
                for (int s = 0; s < 4; s++)
                {
                    var t = mesh.GetTriangles(s);
                    for (int i = 0; i < t.Length; i += 3)
                    {
                        Vector3 face = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                        if (face.sqrMagnitude < 1e-14f) continue;    // degenerate corner slivers
                        Assert.Greater(Vector3.Dot(face.normalized, n[t[i]]), 0.99f, $"submesh {s} triangle {i / 3} is wound against its normal");
                    }
                }
                Assert.That(n.All(x => Mathf.Abs(x.magnitude - 1f) < 1e-3f), "unit normals");
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void PutterHeadMesh_FollowsATuningWithADifferentHeadSize()
        {
            var size = new Vector3(0.04f, 0.15f, 0.045f);
            var mesh = GolfVisualMeshes.PutterHead(size);
            try
            {
                Assert.That(mesh.bounds.size.y, Is.EqualTo(size.y).Within(0.001f));
                Assert.That(mesh.bounds.size.x, Is.EqualTo(size.x).Within(0.001f));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        // ------------------------------------------------------------------ cup rim

        [Test]
        public void CupRimMesh_SitsOutsideThePit_AndFlushWithTheTurf()
        {
            float r = GolfTuning.Default.cupRadius;
            var mesh = GolfVisualMeshes.CupRim(r);
            try
            {
                foreach (var p in mesh.vertices)
                {
                    float d = new Vector2(p.x, p.z).magnitude;
                    Assert.GreaterOrEqual(d, r - 0.0006f, "never inside the pit (the ball falls through here)");
                    Assert.LessOrEqual(d, r + 0.02f, "a thin ring, not a collar");
                    Assert.That(p.y, Is.InRange(-0.001f, 0.0005f), "flush with the turf, at most 0.5 mm proud");
                }
                Assert.Greater(mesh.vertices.Length, 100);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void Cup_SetRim_AddsAVisualOnlyRing_ReplacesIt_AndLeavesTheMechanicsAlone()
        {
            var tuning = ScriptableObject.CreateInstance<GolfTuning>();
            var go = new GameObject("Cup");
            var ballGo = new GameObject("Ball");
            try
            {
                ballGo.AddComponent<Rigidbody>(); ballGo.AddComponent<SphereCollider>();
                var ball = ballGo.AddComponent<GolfBall>();
                ball.SetTuning(tuning);
                var cup = go.AddComponent<Cup>();
                cup.Configure(tuning, null);
                Vector3[] probes = { new Vector3(0f, -0.05f, 0f), new Vector3(0.03f, -0.04f, 0f), new Vector3(0.06f, -0.04f, 0f), new Vector3(0f, 0.01f, 0f), new Vector3(0f, -0.2f, 0f) };
                bool[] before = probes.Select(p => { ball.Body.position = go.transform.TransformPoint(p); return cup.ContainsBall(ball); }).ToArray();
                var rim = cup.SetRim(AnyMaterial());
                Assert.IsNotNull(rim);
                Assert.IsNull(rim.GetComponent<Collider>(), "the rim has no collider");
                Assert.AreEqual(1, go.GetComponentsInChildren<MeshRenderer>().Count(m => m.name == Cup.RimName));
                cup.SetRim(AnyMaterial());
                Assert.AreEqual(1, go.GetComponentsInChildren<Transform>(true).Count(t => t.name == Cup.RimName), "replaced, not duplicated");
                bool[] after = probes.Select(p => { ball.Body.position = go.transform.TransformPoint(p); return cup.ContainsBall(ball); }).ToArray();
                CollectionAssert.AreEqual(before, after, "holing is decided exactly as before");
                Assert.AreEqual(tuning.cupRadius, cup.Radius);
                Assert.IsNull(cup.SetRim(null), "no material, no ring");
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(ballGo); Object.DestroyImmediate(tuning); }
        }

        [Test]
        public void CreateGreen_AddsTheRim_OnlyWhenGivenAMaterial_AndTheSurfaceIsIdentical()
        {
            var tuning = ScriptableObject.CreateInstance<GolfTuning>();
            var parent = new GameObject("Greens");
            try
            {
                var l = new GreenLayout();
                l.Area(-0.6f, 0f, 1.2f, 3f);
                l.cup = new Vector2(0f, 2.4f);
                var a = CourseGeometry.CreateGreen("A", l, tuning, parent.transform, null, null, null, null, null, out Cup cupA);
                var b = CourseGeometry.CreateGreen("B", l, tuning, parent.transform, null, null, null, null, null, out Cup cupB, AnyMaterial());
                Assert.IsNull(cupA.transform.Find(Cup.RimName));
                Assert.IsNotNull(cupB.transform.Find(Cup.RimName));
                var ma = a.transform.Find("Surface").GetComponent<MeshFilter>().sharedMesh; var mb = b.transform.Find("Surface").GetComponent<MeshFilter>().sharedMesh;
                CollectionAssert.AreEqual(ma.vertices, mb.vertices, "the playing surface does not change");
                CollectionAssert.AreEqual(ma.triangles, mb.triangles);
                Assert.AreEqual(ma.subMeshCount, mb.subMeshCount);
            }
            finally { Object.DestroyImmediate(parent); Object.DestroyImmediate(tuning); }
        }

        // ------------------------------------------------------------------ rails: continuous UVs, unchanged geometry

        [Test]
        public void RailUVs_AreWorldSpaceAndContinuous_ForStoneBlockRails()
        {
            var l = new GreenLayout { wallHeight = 0.12f };
            l.Area(-0.6f, 0f, 1.2f, 2f);
            var mesh = CourseGeometry.BuildWalls(l);
            try
            {
                var v = mesh.vertices; var n = mesh.normals; var uv = mesh.uv;
                Assert.AreEqual(v.Length, uv.Length);
                for (int i = 0; i < v.Length; i++)
                {
                    Vector2 want = Mathf.Abs(n[i].y) > 0.5f ? new Vector2(v[i].x, v[i].z) : Mathf.Abs(n[i].x) > 0.5f ? new Vector2(v[i].z, v[i].y) : new Vector2(v[i].x, v[i].y);
                    Assert.That(Vector2.Distance(uv[i], want), Is.LessThan(1e-4f), $"vertex {i} {v[i]} normal {n[i]}");
                }
                // Along the long east rail the u coordinate keeps growing with z (no restart every cell).
                var east = Enumerable.Range(0, v.Length).Where(i => Mathf.Abs(n[i].x) > 0.9f && v[i].x > 0.5f).Select(i => uv[i].x).ToList();
                Assert.That(east.Max() - east.Min(), Is.GreaterThan(1.5f), "u spans the whole rail, not one 10 cm cell");
                // Rail geometry is the established 10 cm-cell construction: bounds are the layout plus the wall thickness.
                Assert.That(mesh.bounds.size.x, Is.EqualTo(1.2f + 2f * l.wallThickness).Within(0.01f));
                Assert.That(mesh.bounds.size.z, Is.EqualTo(2f + 2f * l.wallThickness).Within(0.01f));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        // ------------------------------------------------------------------ the putter

        [UnityTest]
        public IEnumerator Putter_VisibleHeadFollowsTheStrikeBox_AndTheSwingStillStrikes()
        {
            Time.timeScale = 1f;
            var tuning = ScriptableObject.CreateInstance<GolfTuning>();
            GolfPhysicsBootstrap.Apply(tuning);
            var root = new GameObject("PutterBed");
            try
            {
                var layout = new GreenLayout();
                layout.Area(-0.6f, 0f, 1.2f, 6f);
                var def = new HoleDefinition { number = 1, name = "Lane", par = 2, layout = layout, tee = new Vector2(0f, 1f), origin = Vector3.zero };
                var ballGo = new GameObject("Ball"); ballGo.transform.SetParent(root.transform);
                ballGo.AddComponent<Rigidbody>(); ballGo.AddComponent<SphereCollider>();
                var ball = ballGo.AddComponent<GolfBall>(); ball.SetTuning(tuning);
                var hole = HoleFactory.Build(def, null, tuning, root.transform, ball);
                hole.BeginHole(ball);
                for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();

                var putter = new GameObject("Putter").AddComponent<Putter>();
                putter.Configure(tuning, null, ball, AnyMaterial(), AnyMaterial(), AnyMaterial(), AnyMaterial(), AnyMaterial(), AnyMaterial());
                putter.Ball = ball;
                putter.SetAdjustments(0.85f, 0f, 0f);
                putter.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.left);
                putter.transform.position = new Vector3(0f, 1f, 0f);
                putter.Step(Time.time);
                yield return null;   // Destroy() of the proxy renderer takes effect at the end of the frame

                var head = putter.GetComponentsInChildren<Transform>().First(t => t.name == "Head");
                var visual = putter.GetComponentsInChildren<Transform>().First(t => t.name == "HeadVisual");
                Assert.IsNull(head.GetComponent<MeshRenderer>(), "the strike-box proxy no longer draws");
                Assert.That(Vector3.Distance(head.position, visual.position), Is.LessThan(1e-5f), "the visible head sits exactly on the strike box");
                Assert.That(Quaternion.Angle(head.rotation, visual.rotation), Is.LessThan(0.01f));
                Assert.That(head.lossyScale, Is.EqualTo(tuning.headSize), "the strike box keeps its size");
                var mr = visual.GetComponent<MeshRenderer>();
                Assert.AreEqual(4, mr.sharedMaterials.Length);
                Assert.IsNull(visual.GetComponent<Collider>(), "no collider on the visible head");
                Assert.IsNotNull(visual.GetComponent<MeshFilter>().sharedMesh);

                // The existing strike still works through the same box.
                Vector3 ballPos = ball.Position;
                float z = ballPos.z - 0.35f; float end = Time.time + 2f;
                while (Time.time < end && hole.Strokes == 0)
                {
                    z += 1.5f * Time.deltaTime;
                    putter.transform.position = new Vector3(ballPos.x, ballPos.y + 0.85f, z);
                    yield return null;
                }
                Assert.AreEqual(1, hole.Strokes, "the swing no longer strikes the ball");
                Assert.Greater(ball.Velocity.z, 0f);
                // And SetVisible still hides and shows the visible head.
                putter.SetVisible(false);
                Assert.IsFalse(mr.enabled);
                putter.SetVisible(true);
                Assert.IsTrue(mr.enabled);
                Object.Destroy(putter.gameObject);
            }
            finally { Time.timeScale = 1f; Object.Destroy(root); Object.Destroy(tuning); }
        }
    }
}
