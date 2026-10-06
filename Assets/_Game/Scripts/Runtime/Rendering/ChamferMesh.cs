using System.Collections.Generic;
using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// Chamfered boxes (every edge cut by a small bevel so lighting catches it) with flat shading and box-projected UVs, for decorative architecture
    /// and visual-only props. Visual only: never use these for colliders. Mirrors the chamfer in Tools/Blender/archlib.py.
    /// </summary>
    public static class ChamferMesh
    {
        static readonly Dictionary<(int, int, int, int, int), Mesh> s_Cache = new Dictionary<(int, int, int, int, int), Mesh>();

        static int Q(float v) => Mathf.RoundToInt(v * 1000f);

        /// <summary>A box of <paramref name="size"/> centred on the origin, edges chamfered by <paramref name="chamfer"/> (0 = plain), UVs = local metres / <paramref name="uvTile"/>.</summary>
        public static Mesh Box(Vector3 size, float chamfer, float uvTile = 1.5f)
        {
            var key = (Q(size.x) ^ (Q(uvTile) << 20), Q(size.y), Q(size.z), Q(chamfer), Q(uvTile));
            if (s_Cache.TryGetValue(key, out var cached) && cached) return cached;
            var m = Build(size, chamfer, uvTile);
            s_Cache[key] = m;
            return m;
        }

        public static Mesh Build(Vector3 size, float chamfer, float uvTile)
        {
            float a = size.x * 0.5f, b = size.y * 0.5f, c = size.z * 0.5f;
            float d = Mathf.Max(0f, Mathf.Min(chamfer, Mathf.Min(a, Mathf.Min(b, c)) * 0.48f));
            var half = new[] { a, b, c };
            var polys = new List<Vector3[]>();
            var corners = new List<int[]>();
            for (int sx = -1; sx <= 1; sx += 2) for (int sy = -1; sy <= 1; sy += 2) for (int sz = -1; sz <= 1; sz += 2) corners.Add(new[] { sx, sy, sz });

            Vector3 V(int[] k, int axis)
            {
                float inset = d;
                return new Vector3(k[0] * (axis == 0 ? half[0] : half[0] - inset), k[1] * (axis == 1 ? half[1] : half[1] - inset), k[2] * (axis == 2 ? half[2] : half[2] - inset));
            }
            if (d < 1e-5f)
            {
                for (int axis = 0; axis < 3; axis++)
                    for (int s = -1; s <= 1; s += 2)
                    {
                        var pts = new List<Vector3>();
                        foreach (var k in corners) if (k[axis] == s) pts.Add(new Vector3(k[0] * a, k[1] * b, k[2] * c));
                        var n = Vector3.zero; n[axis] = s;
                        polys.Add(Order(pts, n));
                    }
            }
            else
            {
                for (int axis = 0; axis < 3; axis++)
                    for (int s = -1; s <= 1; s += 2)
                    {
                        var pts = new List<Vector3>();
                        foreach (var k in corners) if (k[axis] == s) pts.Add(V(k, axis));
                        var n = Vector3.zero; n[axis] = s;
                        polys.Add(Order(pts, n));
                    }
                int[][] axisPairs = { new[] { 0, 1 }, new[] { 0, 2 }, new[] { 1, 2 } };
                foreach (var pair in axisPairs)
                {
                    int A = pair[0], B = pair[1], C = 3 - A - B;
                    for (int sA = -1; sA <= 1; sA += 2)
                        for (int sB = -1; sB <= 1; sB += 2)
                        {
                            var lo = new int[3]; var hi = new int[3];
                            lo[A] = hi[A] = sA; lo[B] = hi[B] = sB; lo[C] = -1; hi[C] = 1;
                            var pts = new List<Vector3> { V(lo, A), V(hi, A), V(hi, B), V(lo, B) };
                            var n = Vector3.zero; n[A] = sA; n[B] = sB;
                            polys.Add(Order(pts, n));
                        }
                }
                foreach (var k in corners)
                {
                    var pts = new List<Vector3> { V(k, 0), V(k, 1), V(k, 2) };
                    polys.Add(Order(pts, new Vector3(k[0], k[1], k[2])));
                }
            }

            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            float inv = uvTile > 0.001f ? 1f / uvTile : 1f;
            foreach (var poly in polys)
            {
                Vector3 n = Vector3.Cross(poly[1] - poly[0], poly[2] - poly[0]).normalized;
                int baseIndex = verts.Count;
                float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
                foreach (var p in poly)
                {
                    verts.Add(p); norms.Add(n);
                    uvs.Add(ax >= ay && ax >= az ? new Vector2(p.z, p.y) * inv : ay >= az ? new Vector2(p.x, p.z) * inv : new Vector2(p.x, p.y) * inv);
                }
                for (int i = 1; i < poly.Length - 1; i++) { tris.Add(baseIndex); tris.Add(baseIndex + i); tris.Add(baseIndex + i + 1); }
            }
            var mesh = new Mesh { name = $"Chamfer_{size.x:F2}x{size.y:F2}x{size.z:F2}_{d:F3}" };
            mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        /// <summary>Orders a convex polygon counter-clockwise as seen from outside (normal <paramref name="outward"/>), Unity's clockwise-front winding handled by the caller's triangle order.</summary>
        static Vector3[] Order(List<Vector3> pts, Vector3 outward)
        {
            var centre = Vector3.zero; foreach (var p in pts) centre += p; centre /= pts.Count;
            Vector3 n = outward.normalized;
            Vector3 u = Mathf.Abs(n.y) < 0.9f ? Vector3.Cross(Vector3.up, n).normalized : Vector3.Cross(Vector3.right, n).normalized;
            Vector3 v = Vector3.Cross(n, u);
            pts.Sort((p, q) =>
            {
                float ap = Mathf.Atan2(Vector3.Dot(p - centre, v), Vector3.Dot(p - centre, u));
                float aq = Mathf.Atan2(Vector3.Dot(q - centre, v), Vector3.Dot(q - centre, u));
                return ap.CompareTo(aq);
            });
            // Counter-clockwise about +n is a front face for the right-handed cross; Unity front faces are clockwise when viewed from the front, so reverse.
            pts.Reverse();
            return pts.ToArray();
        }

        /// <summary>Creates a chamfered block object (no collider) at a pose; the object's scale stays 1 so the chamfer is true.</summary>
        public static GameObject Block(string name, Transform parent, Vector3 worldPos, Quaternion worldRot, Vector3 size, float chamfer, Material mat, float uvTile = 1.6f, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(worldPos, worldRot);
            go.AddComponent<MeshFilter>().sharedMesh = Box(size, chamfer, uvTile);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return go;
        }
    }
}
