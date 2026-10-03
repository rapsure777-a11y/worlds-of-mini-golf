using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gamebreak.MiniGolf.Editor.Art
{
    /// <summary>
    /// Small procedural mesh toolkit for the art kit. Every vertex carries a palette UV and a colour
    /// (RGB = tint / baked occlusion, A = wind weight). Parts can be added under a transform so a model
    /// is assembled like a little scene graph, then merged into one mesh.
    /// </summary>
    public class MeshBuilder
    {
        readonly List<Vector3> m_Pos = new List<Vector3>();
        readonly List<Vector3> m_Nrm = new List<Vector3>();
        readonly List<Vector2> m_UV = new List<Vector2>();
        readonly List<Color> m_Col = new List<Color>();
        readonly List<int> m_Tri = new List<int>();

        public Matrix4x4 Transform = Matrix4x4.identity;
        public int VertexCount => m_Pos.Count;

        public int Vertex(Vector3 p, Vector3 n, Vector2 uv, Color c)
        {
            m_Pos.Add(Transform.MultiplyPoint3x4(p));
            m_Nrm.Add(Transform.MultiplyVector(n).normalized);
            m_UV.Add(uv);
            m_Col.Add(c);
            return m_Pos.Count - 1;
        }

        public void Triangle(int a, int b, int c) { m_Tri.Add(a); m_Tri.Add(b); m_Tri.Add(c); }
        public void Quad(int a, int b, int c, int d) { Triangle(a, b, c); Triangle(a, c, d); }

        /// <summary>Run <paramref name="build"/> with an extra local transform.</summary>
        public void With(Matrix4x4 local, Action build)
        {
            var saved = Transform;
            Transform = saved * local;
            build();
            Transform = saved;
        }

        public void With(Vector3 pos, Quaternion rot, Vector3 scale, Action build) => With(Matrix4x4.TRS(pos, rot, scale), build);

        /// <summary>
        /// Tube along a polyline with rotation-minimising frames.
        /// radius(t), uv(t, around) and color(t) are sampled per ring; t runs 0..1 along the path.
        /// </summary>
        public void Tube(IList<Vector3> path, int sides, Func<float, float> radius, Func<float, float, Vector2> uv,
            Func<float, Color> color, bool capEnd = true, Func<float, float, float> radiusJitter = null)
        {
            int rings = path.Count;
            Vector3 prevNormal = Vector3.zero;
            int start = m_Pos.Count;
            for (int i = 0; i < rings; i++)
            {
                float t = i / (float)(rings - 1);
                Vector3 tangent = (path[Mathf.Min(i + 1, rings - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
                if (i == 0)
                {
                    prevNormal = Vector3.Cross(tangent, Mathf.Abs(tangent.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                }
                else
                {
                    prevNormal = Vector3.ProjectOnPlane(prevNormal, tangent).normalized;
                }
                Vector3 binormal = Vector3.Cross(tangent, prevNormal);
                float r = radius(t);
                Color c = color(t);
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    Vector3 dir = prevNormal * Mathf.Cos(a) + binormal * Mathf.Sin(a);
                    float rr = r * (radiusJitter != null ? radiusJitter(t, a) : 1f);
                    Vertex(path[i] + dir * rr, dir, uv(t, s / (float)sides), c);
                }
            }
            for (int i = 0; i < rings - 1; i++)
            for (int s = 0; s < sides; s++)
            {
                int a = start + i * (sides + 1) + s, b = a + 1, c = a + sides + 1, d = c + 1;
                Quad(a, b, d, c); // outward-facing for this frame's handedness
            }
            if (capEnd)
            {
                Vector3 tip = path[rings - 1];
                Vector3 tan = (path[rings - 1] - path[rings - 2]).normalized;
                int centre = Vertex(tip, tan, uv(1f, 0.5f), color(1f));
                int ring = start + (rings - 1) * (sides + 1);
                for (int s = 0; s < sides; s++) Triangle(centre, ring + s, ring + s + 1);
            }
        }

        /// <summary>Surface of revolution around local +Y from a (radius, height) profile.</summary>
        public void Lathe(IList<Vector2> profile, int sides, Func<float, Vector2> uv, Func<float, Color> color,
            Func<float, float, float> radiusJitter = null)
        {
            int start = m_Pos.Count;
            for (int i = 0; i < profile.Count; i++)
            {
                float t = i / (float)(profile.Count - 1);
                Vector2 prev = profile[Mathf.Max(i - 1, 0)], next = profile[Mathf.Min(i + 1, profile.Count - 1)];
                Vector2 slope = (next - prev).normalized; // (dr, dy)
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    float r = profile[i].x * (radiusJitter != null ? radiusJitter(t, a) : 1f);
                    Vector3 n = (dir * slope.y - Vector3.up * slope.x).normalized;
                    Vertex(dir * r + Vector3.up * profile[i].y, n, uv(t), color(t));
                }
            }
            for (int i = 0; i < profile.Count - 1; i++)
            for (int s = 0; s < sides; s++)
            {
                int a = start + i * (sides + 1) + s, b = a + 1, c = a + sides + 1, d = c + 1;
                Quad(a, c, d, b);
            }
        }

        /// <summary>Axis-aligned box (in the current transform) with one palette UV per face.</summary>
        public void Box(Vector3 centre, Vector3 size, Vector2 uv, Color color, Vector2? topUV = null)
        {
            Vector3 h = size * 0.5f;
            void Face(Vector3 n, Vector3 u, Vector3 v, Vector2 faceUV)
            {
                Vector3 c = centre + Vector3.Scale(n, h);
                Vector3 du = Vector3.Scale(u, h), dv = Vector3.Scale(v, h);
                int a = Vertex(c - du - dv, n, faceUV, color);
                int b = Vertex(c + du - dv, n, faceUV, color);
                int cc = Vertex(c + du + dv, n, faceUV, color);
                int d = Vertex(c - du + dv, n, faceUV, color);
                Quad(a, d, cc, b);
            }
            Vector2 top = topUV ?? uv;
            Face(Vector3.up, Vector3.right, Vector3.forward, top);
            Face(Vector3.down, Vector3.forward, Vector3.right, uv);
            Face(Vector3.right, Vector3.forward, Vector3.up, uv);
            Face(Vector3.left, Vector3.up, Vector3.forward, uv);
            Face(Vector3.forward, Vector3.up, Vector3.right, uv);
            Face(Vector3.back, Vector3.right, Vector3.up, uv);
        }

        /// <summary>
        /// Icosphere displaced by <paramref name="shape"/> (unit direction -> radius multiplier), coloured per
        /// vertex by <paramref name="paint"/> (local position, normal -> uv, colour).
        /// </summary>
        public void Blob(int subdivisions, Func<Vector3, float> shape, Func<Vector3, Vector3, (Vector2 uv, Color c)> paint,
            bool faceted = false, Func<Vector3, Vector3> warp = null)
        {
            var (verts, tris) = Icosphere(subdivisions);
            int start = m_Pos.Count;
            var displaced = new Vector3[verts.Count];
            for (int i = 0; i < verts.Count; i++)
            {
                displaced[i] = verts[i] * shape(verts[i]);
                if (warp != null) displaced[i] = warp(displaced[i]);
            }
            if (faceted)
            {
                // Low-poly look: every face gets its own vertices, normal and a single palette colour.
                for (int i = 0; i < tris.Count; i += 3)
                {
                    Vector3 a = displaced[tris[i]], b = displaced[tris[i + 1]], c = displaced[tris[i + 2]];
                    Vector3 fn = Vector3.Cross(b - a, c - a).normalized;
                    var (uv, col) = paint((a + b + c) / 3f, fn);
                    int ia = Vertex(a, fn, uv, col), ib = Vertex(b, fn, uv, col), ic = Vertex(c, fn, uv, col);
                    Triangle(ia, ib, ic);
                }
                return;
            }
            // Smooth normals from the displaced surface.
            var normals = new Vector3[verts.Count];
            for (int i = 0; i < tris.Count; i += 3)
            {
                Vector3 n = Vector3.Cross(displaced[tris[i + 1]] - displaced[tris[i]], displaced[tris[i + 2]] - displaced[tris[i]]);
                normals[tris[i]] += n; normals[tris[i + 1]] += n; normals[tris[i + 2]] += n;
            }
            for (int i = 0; i < verts.Count; i++)
            {
                var (uv, c) = paint(displaced[i], normals[i].normalized);
                Vertex(displaced[i], normals[i].normalized, uv, c);
            }
            for (int i = 0; i < tris.Count; i += 3) Triangle(start + tris[i], start + tris[i + 1], start + tris[i + 2]);
        }

        /// <summary>
        /// A leaf/frond: a ribbon along a spine with width(t), optional serrated leaflets.
        /// Two-sided rendering is handled by the foliage material (Cull Off).
        /// </summary>
        public void Ribbon(IList<Vector3> spine, Vector3 sideHint, Func<float, float> width, Func<float, Vector2> uv,
            Func<float, Color> color, Func<float, float> fold = null)
        {
            int start = m_Pos.Count;
            int n = spine.Count;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                Vector3 tangent = (spine[Mathf.Min(i + 1, n - 1)] - spine[Mathf.Max(i - 1, 0)]).normalized;
                Vector3 side = Vector3.ProjectOnPlane(sideHint, tangent).normalized;
                Vector3 up = Vector3.Cross(side, tangent).normalized;
                float w = width(t) * 0.5f;
                float f = fold != null ? fold(t) : 0f; // V-fold along the midrib
                Vector3 left = spine[i] - side * w + up * (w * f);
                Vector3 right = spine[i] + side * w + up * (w * f);
                Color c = color(t);
                Vertex(left, up, uv(t), c);
                Vertex(spine[i], up, uv(Mathf.Min(1f, t + 0.15f)), c);
                Vertex(right, up, uv(t), c);
            }
            for (int i = 0; i < n - 1; i++)
            {
                int a = start + i * 3;
                // Front face matches the stored normal ('up') so two-sided lighting flips correctly.
                Quad(a, a + 1, a + 4, a + 3);
                Quad(a + 1, a + 2, a + 5, a + 4);
            }
        }

        public Mesh ToMesh(string name, bool flatShaded = false)
        {
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            if (flatShaded)
            {
                var p = new List<Vector3>(); var nr = new List<Vector3>(); var uv = new List<Vector2>(); var col = new List<Color>(); var tri = new List<int>();
                for (int i = 0; i < m_Tri.Count; i += 3)
                {
                    int a = m_Tri[i], b = m_Tri[i + 1], c = m_Tri[i + 2];
                    Vector3 fn = Vector3.Cross(m_Pos[b] - m_Pos[a], m_Pos[c] - m_Pos[a]).normalized;
                    foreach (int k in new[] { a, b, c })
                    {
                        tri.Add(p.Count); p.Add(m_Pos[k]); nr.Add(fn); uv.Add(m_UV[k]); col.Add(m_Col[k]);
                    }
                }
                mesh.SetVertices(p); mesh.SetNormals(nr); mesh.SetUVs(0, uv); mesh.SetColors(col); mesh.SetTriangles(tri, 0);
            }
            else
            {
                mesh.SetVertices(m_Pos); mesh.SetNormals(m_Nrm); mesh.SetUVs(0, m_UV); mesh.SetColors(m_Col); mesh.SetTriangles(m_Tri, 0);
            }
            mesh.RecalculateBounds();
            return mesh;
        }

        static (List<Vector3>, List<int>) Icosphere(int subdivisions)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
            var f = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            for (int s = 0; s < subdivisions; s++)
            {
                var cache = new Dictionary<long, int>();
                int Mid(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (cache.TryGetValue(key, out int m)) return m;
                    v.Add(((v[a] + v[b]) * 0.5f).normalized);
                    cache[key] = v.Count - 1;
                    return v.Count - 1;
                }
                var nf = new List<int>();
                for (int i = 0; i < f.Count; i += 3)
                {
                    int a = f[i], b = f[i + 1], c = f[i + 2];
                    int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    nf.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                f = nf;
            }
            return (v, f);
        }
    }
}
