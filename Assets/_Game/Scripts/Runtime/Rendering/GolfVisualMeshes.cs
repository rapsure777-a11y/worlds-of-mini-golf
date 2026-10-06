using System.Collections.Generic;
using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// Procedural meshes for the golf-contact elements (Graphics Pass 3): the putter head and the cup's rim. Purely visual: no colliders are created here and
    /// the strike box (<see cref="GolfTuning.headSize"/>), the cup radius and the cup depth are never read back from these meshes, so the physics cannot change.
    /// </summary>
    public static class GolfVisualMeshes
    {
        /// <summary>Submesh indices of <see cref="PutterHead"/>: 0 body (satin steel), 1 bevels (bright steel), 2 top insert (dark milled plate), 3 alignment line (white).</summary>
        public const int SubBody = 0, SubBevel = 1, SubInsert = 2, SubLine = 3;

        /// <summary>How far the hosel (neck) reaches from the head's top face toward the shaft.</summary>
        public const float HoselLength = 0.034f;

        /// <summary>
        /// A modern blade-style putter head in head-local space (x = face normal, y = toe to heel, z = height; the shaft lies toward -z). The body fills exactly
        /// <paramref name="size"/> (the strike box), with chamfered edges, a dark milled insert and a white alignment line on the top (-z) face, and a tapered hosel with a
        /// bright ferrule ring reaching <see cref="HoselLength"/> toward the shaft. Both faces are flat and symmetric, like the strike box.
        /// </summary>
        public static Mesh PutterHead(Vector3 size)
        {
            var b = new Builder(4);
            Vector3 h = size * 0.5f;
            float bev = Mathf.Min(size.x, size.y, size.z) * 0.14f;

            // Chamfered box: six inset faces (body), twelve edge strips and eight corner caps (bevel).
            BevelBox(b, h, bev, SubBody, SubBevel);

            // Top (-z) face: dark insert inset by one bevel plus a white line down its middle, standing 0.15 mm proud.
            float zt = -h.z - 0.00015f;
            Vector3 ih = new Vector3(h.x - bev * 1.2f, h.y - bev * 2.2f, 0f);
            b.Quad(SubInsert, new Vector3(-ih.x, -ih.y, zt), new Vector3(ih.x, -ih.y, zt), new Vector3(ih.x, ih.y, zt), new Vector3(-ih.x, ih.y, zt), -Vector3.forward);
            float lw = 0.0011f;
            float zl = zt - 0.00012f;
            b.Quad(SubLine, new Vector3(-ih.x * 0.92f, -lw, zl), new Vector3(ih.x * 0.92f, -lw, zl), new Vector3(ih.x * 0.92f, lw, zl), new Vector3(-ih.x * 0.92f, lw, zl), -Vector3.forward);

            // Hosel: a tapered neck rising from the top face, with a bright ferrule ring where the shaft enters.
            float zNeck0 = -h.z, zNeck1 = -h.z - HoselLength * 0.72f, zFerrule = -h.z - HoselLength;
            Frustum(b, SubBody, 0.0135f, 0.0085f, zNeck0, zNeck1, 20);
            Frustum(b, SubBevel, 0.0092f, 0.0092f, zNeck1, zFerrule, 20);
            Disc(b, SubBevel, 0.0092f, zFerrule, 20, -Vector3.forward);

            var mesh = b.ToMesh("PutterHead");
            return mesh;
        }

        /// <summary>
        /// The cup's rim: a thin metal ring in the green's plane (cup-local space, y up), from the cup radius outward by <paramref name="width"/>, standing 0.3 mm proud of the
        /// turf with a rolled outer edge. It has no collider and sits outside the pit, so the ball's mechanics are untouched.
        /// </summary>
        public static Mesh CupRim(float cupRadius, float width = 0.0075f, int segments = 48)
        {
            var b = new Builder(1);
            float r0 = cupRadius - 0.0004f, r1 = cupRadius + width, r2 = r1 + 0.0025f;
            const float top = 0.0003f;
            for (int i = 0; i < segments; i++)
            {
                float a0 = 2f * Mathf.PI * i / segments, a1 = 2f * Mathf.PI * (i + 1) / segments;
                Vector3 P(float r, float a, float y) => new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                b.Quad(0, P(r0, a0, top), P(r0, a1, top), P(r1, a1, top), P(r1, a0, top), Vector3.up);          // flat ring
                b.Quad(0, P(r1, a0, top), P(r1, a1, top), P(r2, a1, -0.0004f), P(r2, a0, -0.0004f), Vector3.up);  // rolled outer edge
            }
            return b.ToMesh("CupRim");
        }

        // ------------------------------------------------------------------ geometry helpers

        static void BevelBox(Builder b, Vector3 h, float bev, int body, int bevel)
        {
            // Face quads shrunk by bev; edges connect neighbouring faces at 45 degrees; corners are triangles.
            Vector3[] sgn = { new Vector3(1, 1, 1), new Vector3(-1, 1, 1), new Vector3(1, -1, 1), new Vector3(-1, -1, 1), new Vector3(1, 1, -1), new Vector3(-1, 1, -1), new Vector3(1, -1, -1), new Vector3(-1, -1, -1) };
            Vector3 inner = h - Vector3.one * bev;
            // Faces
            for (int axis = 0; axis < 3; axis++)
            for (int side = -1; side <= 1; side += 2)
            {
                int u = (axis + 1) % 3, v = (axis + 2) % 3;
                Vector3 n = Vector3.zero; n[axis] = side;
                Vector3 c = n * h[axis];
                Vector3 du = Vector3.zero; du[u] = inner[u];
                Vector3 dv = Vector3.zero; dv[v] = inner[v];
                b.Quad(body, c - du - dv, c + du - dv, c + du + dv, c - du + dv, n);
            }
            // Edges: for each pair of axes (a, b) and sign pair, a strip along the third axis.
            for (int axis = 0; axis < 3; axis++)
            {
                int u = (axis + 1) % 3, v = (axis + 2) % 3;
                for (int su = -1; su <= 1; su += 2)
                for (int sv = -1; sv <= 1; sv += 2)
                {
                    Vector3 nA = Vector3.zero; nA[u] = su;
                    Vector3 nB = Vector3.zero; nB[v] = sv;
                    Vector3 along = Vector3.zero; along[axis] = inner[axis];
                    Vector3 p0 = nA * h[u] + nB * inner[v], p1 = nA * inner[u] + nB * h[v];
                    b.Quad(bevel, p0 - along, p0 + along, p1 + along, p1 - along, (nA + nB).normalized);
                }
            }
            // Corners
            foreach (var s in sgn)
            {
                Vector3 px = new Vector3(s.x * h.x, s.y * inner.y, s.z * inner.z), py = new Vector3(s.x * inner.x, s.y * h.y, s.z * inner.z), pz = new Vector3(s.x * inner.x, s.y * inner.y, s.z * h.z);
                b.Tri(bevel, px, py, pz, s.normalized);
            }
        }

        static void Frustum(Builder b, int sub, float r0, float r1, float z0, float z1, int sides)
        {
            for (int i = 0; i < sides; i++)
            {
                float a0 = 2f * Mathf.PI * i / sides, a1 = 2f * Mathf.PI * (i + 1) / sides;
                Vector3 A(float r, float a, float z) => new Vector3(0f, 0f, z) + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * r;
                Vector3 n = new Vector3(Mathf.Cos((a0 + a1) * 0.5f), Mathf.Sin((a0 + a1) * 0.5f), 0f);
                b.Quad(sub, A(r0, a0, z0), A(r0, a1, z0), A(r1, a1, z1), A(r1, a0, z1), n);
            }
        }

        static void Disc(Builder b, int sub, float r, float z, int sides, Vector3 normal)
        {
            for (int i = 0; i < sides; i++)
            {
                float a0 = 2f * Mathf.PI * i / sides, a1 = 2f * Mathf.PI * (i + 1) / sides;
                b.Tri(sub, new Vector3(0f, 0f, z), new Vector3(Mathf.Cos(a0) * r, Mathf.Sin(a0) * r, z), new Vector3(Mathf.Cos(a1) * r, Mathf.Sin(a1) * r, z), normal);
            }
        }

        /// <summary>Flat-shaded triangle soup with submeshes; every triangle is wound to face the stated normal.</summary>
        sealed class Builder
        {
            readonly List<Vector3> m_V = new List<Vector3>();
            readonly List<Vector3> m_N = new List<Vector3>();
            readonly List<Vector2> m_UV = new List<Vector2>();
            readonly List<int>[] m_Tris;

            public Builder(int subMeshes)
            {
                m_Tris = new List<int>[subMeshes];
                for (int i = 0; i < subMeshes; i++) m_Tris[i] = new List<int>();
            }

            public void Tri(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 want)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), want) < 0f) { var t = b; b = c; c = t; }
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                int s = m_V.Count;
                m_V.Add(a); m_V.Add(b); m_V.Add(c);
                m_N.Add(n); m_N.Add(n); m_N.Add(n);
                m_UV.Add(new Vector2(a.x, a.y)); m_UV.Add(new Vector2(b.x, b.y)); m_UV.Add(new Vector2(c.x, c.y));
                m_Tris[sub].Add(s); m_Tris[sub].Add(s + 1); m_Tris[sub].Add(s + 2);
            }

            public void Quad(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 want)
            {
                Tri(sub, a, b, c, want);
                Tri(sub, a, c, d, want);
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(m_V);
                mesh.SetNormals(m_N);
                mesh.SetUVs(0, m_UV);
                mesh.subMeshCount = m_Tris.Length;
                for (int i = 0; i < m_Tris.Length; i++) mesh.SetTriangles(m_Tris[i], i);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
