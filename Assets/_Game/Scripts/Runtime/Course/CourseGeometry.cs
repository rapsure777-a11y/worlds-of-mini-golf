using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// Data describing one green: the playable area as a union of rectangles on a grid,
    /// a height function for slopes and where the cup is. Coordinates are local XZ metres.
    /// </summary>
    public class GreenLayout
    {
        public float cell = 0.1f;
        public readonly List<Rect> areas = new List<Rect>();
        /// <summary>Sub-areas drawn with the theme's deck material (wooden bridge planks) instead of turf. Visual only: same collider, same physics.</summary>
        public readonly List<Rect> deckAreas = new List<Rect>();
        public Func<float, float, float> height = (x, z) => 0f;
        public Vector2? cup;
        /// <summary>Square of cells around the cup, kept flat. Even: cup snaps to a grid vertex. Odd: to a cell centre.</summary>
        public int cupTileCells = 4;
        public float wallHeight = 0.09f;
        public float wallThickness = 0.08f;
        public float baseDepth = 0.35f;
        /// <summary>
        /// Thin rectangles (layout XZ) marking boundary edges that get no rail, e.g. the lip of a launch ramp and the near edge of its
        /// landing pad. An edge is open when its midpoint lies inside one of these. An open edge gets a plain vertical skirt instead,
        /// so a ball that comes up short meets a wall rather than a bare sheet. Empty by default: every existing hole is unchanged.
        /// </summary>
        public readonly List<Rect> openEdges = new List<Rect>();

        public GreenLayout Area(float x, float z, float width, float length)
        {
            areas.Add(new Rect(x, z, width, length));
            return this;
        }

        public float Height(float x, float z) => height(x, z);
    }

    /// <summary>Height-function helpers for building slopes.</summary>
    public static class Slopes
    {
        /// <summary>Smooth ramp from h0 to h1 as z goes from z0 to z1.</summary>
        public static float RampZ(float z, float z0, float z1, float h0, float h1) =>
            Mathf.Lerp(h0, h1, Smooth(Mathf.InverseLerp(z0, z1, z)));

        public static float RampX(float x, float x0, float x1, float h0, float h1) =>
            Mathf.Lerp(h0, h1, Smooth(Mathf.InverseLerp(x0, x1, x)));

        /// <summary>Round bump (positive) or bowl (negative) centred at c.</summary>
        public static float Mound(float x, float z, Vector2 c, float radius, float heightAmt)
        {
            float d = Vector2.Distance(new Vector2(x, z), c) / radius;
            if (d >= 1f) return 0f;
            float s = 0.5f + 0.5f * Mathf.Cos(d * Mathf.PI);
            return heightAmt * s;
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);
    }

    public static class CourseGeometry
    {
        /// <summary>Builds the green surface (submesh 0) with the cup cut out and the cup interior (submesh 1).</summary>
        public static Mesh BuildSurface(GreenLayout l, float cupRadius, float cupDepth, out Vector3 cupLocal, int cupSegments = 48)
        {
            var g = new Grid(l);
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var green = new List<int>();
            var deck = new List<int>();
            var cupTris = new List<int>();
            var map = new Dictionary<long, int>();

            int Vtx(int i, int j)
            {
                long key = ((long)i << 32) | (uint)j;
                if (map.TryGetValue(key, out int idx)) return idx;
                float x = g.X(i), z = g.Z(j);
                idx = verts.Count;
                verts.Add(new Vector3(x, l.Height(x, z), z));
                uvs.Add(new Vector2(x, z));
                map[key] = idx;
                return idx;
            }

            // Cup tile as a range of grid vertices [i0, i1] x [j0, j1]. An even tile size centres the
            // cup on the nearest grid vertex, an odd size on the nearest cell centre.
            int k = Mathf.Max(2, l.cupTileCells);
            int i0 = -1, i1 = -1, j0 = -1, j1 = -1;
            cupLocal = Vector3.zero;
            bool hasCup = l.cup.HasValue;
            if (hasCup)
            {
                float fx = (l.cup.Value.x - g.minX) / l.cell, fz = (l.cup.Value.y - g.minZ) / l.cell;
                if (k % 2 == 0) { i0 = Mathf.RoundToInt(fx) - k / 2; j0 = Mathf.RoundToInt(fz) - k / 2; }
                else { i0 = Mathf.FloorToInt(fx) - k / 2; j0 = Mathf.FloorToInt(fz) - k / 2; }
                i1 = i0 + k; j1 = j0 + k;
                float cx = (g.X(i0) + g.X(i1)) * 0.5f, cz = (g.Z(j0) + g.Z(j1)) * 0.5f;
                cupLocal = new Vector3(cx, l.Height(cx, cz), cz);
            }
            bool InCupTile(int i, int j) => hasCup && i >= i0 && i < i1 && j >= j0 && j < j1;

            for (int i = 0; i < g.nx; i++)
            for (int j = 0; j < g.nz; j++)
            {
                if (!g.inside[i, j] || InCupTile(i, j)) continue;
                int a = Vtx(i, j), b = Vtx(i + 1, j), c = Vtx(i, j + 1), d = Vtx(i + 1, j + 1);
                var target = IsDeck(l, g, i, j) ? deck : green;
                AddTri(target, verts, a, c, d, Vector3.up);
                AddTri(target, verts, a, d, b, Vector3.up);
            }

            if (hasCup)
            {
                // Outer loop: tile perimeter grid vertices (shared with the surrounding cells, so no cracks).
                var outer = new List<int>();
                for (int i = i0; i < i1; i++) outer.Add(Vtx(i, j0));
                for (int j = j0; j < j1; j++) outer.Add(Vtx(i1, j));
                for (int i = i1; i > i0; i--) outer.Add(Vtx(i, j1));
                for (int j = j1; j > j0; j--) outer.Add(Vtx(i0, j));

                var inner = new List<int>();
                var wallTop = new List<int>();
                var wallBottom = new List<int>();
                for (int s = 0; s < cupSegments; s++)
                {
                    float ang = s * Mathf.PI * 2f / cupSegments - Mathf.PI;
                    var off = new Vector3(Mathf.Cos(ang) * cupRadius, 0f, Mathf.Sin(ang) * cupRadius);
                    Vector3 top = cupLocal + off;
                    inner.Add(verts.Count); verts.Add(top); uvs.Add(new Vector2(top.x, top.z));
                    wallTop.Add(verts.Count); verts.Add(top); uvs.Add(new Vector2(s / (float)cupSegments, 1f));
                    wallBottom.Add(verts.Count); verts.Add(top + Vector3.down * cupDepth); uvs.Add(new Vector2(s / (float)cupSegments, 0f));
                }

                Zip(green, verts, outer, inner, cupLocal);

                // Cup wall, facing inward.
                for (int s = 0; s < cupSegments; s++)
                {
                    int n = (s + 1) % cupSegments;
                    Vector3 mid = (verts[wallTop[s]] + verts[wallTop[n]]) * 0.5f;
                    Vector3 inward = (cupLocal - mid); inward.y = 0f;
                    AddTri(cupTris, verts, wallTop[s], wallBottom[s], wallBottom[n], inward);
                    AddTri(cupTris, verts, wallTop[s], wallBottom[n], wallTop[n], inward);
                }
                // Cup bottom.
                int centre = verts.Count;
                verts.Add(cupLocal + Vector3.down * cupDepth); uvs.Add(new Vector2(0.5f, 0f));
                for (int s = 0; s < cupSegments; s++)
                    AddTri(cupTris, verts, centre, wallBottom[s], wallBottom[(s + 1) % cupSegments], Vector3.up);
            }

            var mesh = new Mesh { name = "GreenSurface" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = deck.Count > 0 ? 3 : 2;
            mesh.SetTriangles(green, 0);
            mesh.SetTriangles(cupTris, 1);
            if (deck.Count > 0) mesh.SetTriangles(deck, 2);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        static bool IsDeck(GreenLayout l, Grid g, int i, int j)
        {
            if (l.deckAreas.Count == 0) return false;
            var c = new Vector2(g.X(i) + g.cell * 0.5f, g.Z(j) + g.cell * 0.5f);
            foreach (var r in l.deckAreas) if (r.Contains(c)) return true;
            return false;
        }

        /// <summary>Rails around every outside edge of the green. Flat-shaded; the collider welds duplicates.</summary>
        public static Mesh BuildWalls(GreenLayout l)
        {
            var g = new Grid(l);
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            float th = l.wallThickness, wh = l.wallHeight;

            Vector3 P(int i, int j) { float x = g.X(i), z = g.Z(j); return new Vector3(x, l.Height(x, z), z); }

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
            {
                int s = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
                // World-space UVs (metres), continuous along a rail: u runs along the rail, v up it; the top face uses the ground plane. Stone-block rails
                // (Graphics Pass 3) need a pattern that does not restart every 10 cm cell. Geometry is unchanged.
                // The projection follows the quad's real plane (some corner faces are declared with a different facing hint than their geometry).
                Vector3 gn = Vector3.Cross(b - a, d - a).normalized;
                Vector2 Uv(Vector3 p) => Mathf.Abs(gn.y) > 0.5f ? new Vector2(p.x, p.z) : Mathf.Abs(gn.x) > 0.5f ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y);
                uvs.Add(Uv(a)); uvs.Add(Uv(b)); uvs.Add(Uv(c)); uvs.Add(Uv(d));
                AddTri(tris, verts, s, s + 1, s + 2, normal);
                AddTri(tris, verts, s, s + 2, s + 3, normal);
            }

            void Edge(Vector3 a, Vector3 b, Vector3 outward)
            {
                Vector3 up = Vector3.up;
                Vector3 aTop = a + up * wh, bTop = b + up * wh;
                Vector3 aOut = aTop + outward * th, bOut = bTop + outward * th;
                Vector3 aBase = new Vector3(aOut.x, a.y - l.baseDepth, aOut.z);
                Vector3 bBase = new Vector3(bOut.x, b.y - l.baseDepth, bOut.z);
                Quad(a - up * 0.02f, b - up * 0.02f, bTop, aTop, -outward); // inner face
                Quad(aTop, bTop, bOut, aOut, up);                             // top
                Quad(aOut, bOut, bBase, aBase, outward);                      // outer face
            }

            bool IsOpenEdge(Vector3 a, Vector3 b)
            {
                if (l.openEdges.Count == 0) return false;
                float mx = (a.x + b.x) * 0.5f, mz = (a.z + b.z) * 0.5f;
                foreach (var r in l.openEdges)
                    if (mx >= r.xMin - 1e-4f && mx <= r.xMax + 1e-4f && mz >= r.yMin - 1e-4f && mz <= r.yMax + 1e-4f) return true;
                return false;
            }

            void Skirt(Vector3 a, Vector3 b, Vector3 outward)
            {
                Vector3 aBase = new Vector3(a.x, a.y - l.baseDepth, a.z), bBase = new Vector3(b.x, b.y - l.baseDepth, b.z);
                Quad(a, b, bBase, aBase, outward);
            }

            void EdgeOrSkirt(Vector3 a, Vector3 b, Vector3 outward)
            {
                if (IsOpenEdge(a, b)) Skirt(a, b, outward);
                else Edge(a, b, outward);
            }

            bool In(int i, int j) => i >= 0 && j >= 0 && i < g.nx && j < g.nz && g.inside[i, j];

            for (int i = 0; i < g.nx; i++)
            for (int j = 0; j < g.nz; j++)
            {
                if (!g.inside[i, j]) continue;
                if (!In(i - 1, j)) EdgeOrSkirt(P(i, j), P(i, j + 1), Vector3.left);
                if (!In(i + 1, j)) EdgeOrSkirt(P(i + 1, j), P(i + 1, j + 1), Vector3.right);
                if (!In(i, j - 1)) EdgeOrSkirt(P(i, j), P(i + 1, j), Vector3.back);
                if (!In(i, j + 1)) EdgeOrSkirt(P(i, j + 1), P(i + 1, j + 1), Vector3.forward);
            }

            // Fill the square gap at convex outer corners.
            for (int i = 0; i <= g.nx; i++)
            for (int j = 0; j <= g.nz; j++)
            {
                // The four cells around grid vertex (i,j).
                bool sw = In(i - 1, j - 1), se = In(i, j - 1), nw = In(i - 1, j), ne = In(i, j);
                int count = (sw ? 1 : 0) + (se ? 1 : 0) + (nw ? 1 : 0) + (ne ? 1 : 0);
                if (count != 1) continue;
                Vector3 ox = (sw || nw) ? Vector3.right : Vector3.left;
                Vector3 oz = (sw || se) ? Vector3.forward : Vector3.back;
                Vector3 p = P(i, j) + Vector3.up * wh;
                Vector3 px = p + ox * th, pz = p + oz * th, pxz = p + (ox + oz) * th;
                Quad(p, px, pxz, pz, Vector3.up);
                float baseY = P(i, j).y - l.baseDepth;
                Quad(px, pxz, new Vector3(pxz.x, baseY, pxz.z), new Vector3(px.x, baseY, px.z), oz);
                Quad(pz, pxz, new Vector3(pxz.x, baseY, pxz.z), new Vector3(pz.x, baseY, pz.z), ox);
                // Where the boundary edge next to this corner is an open edge (no rail), the corner post has no neighbour on that side:
                // close its face so it is not hollow when seen from the open side. Never runs for layouts without open edges.
                if (IsOpenEdge(P(i, j), P(i, j) + (-ox) * l.cell))
                    Quad(p, pz, new Vector3(pz.x, baseY, pz.z), new Vector3(p.x, baseY, p.z), -ox);
                if (IsOpenEdge(P(i, j), P(i, j) + (-oz) * l.cell))
                    Quad(p, px, new Vector3(px.x, baseY, px.z), new Vector3(p.x, baseY, p.z), -oz);
            }

            var mesh = new Mesh { name = "GreenWalls" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Creates the green, walls and cup under <paramref name="parent"/>. Returns the root.
        /// </summary>
        public static GameObject CreateGreen(string name, GreenLayout layout, GolfTuning tuning, Transform parent,
            Material greenMat, Material cupMat, Material wallMat, Material flagMat, out Cup cup)
            => CreateGreen(name, layout, tuning, parent, greenMat, cupMat, wallMat, flagMat, null, out cup);

        /// <param name="deckMat">Material for <see cref="GreenLayout.deckAreas"/> (null: turf).</param>
        public static GameObject CreateGreen(string name, GreenLayout layout, GolfTuning tuning, Transform parent,
            Material greenMat, Material cupMat, Material wallMat, Material flagMat, Material deckMat, out Cup cup, Material cupRimMat = null)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);

            var surface = BuildSurface(layout, tuning.cupRadius, tuning.cupDepth, out Vector3 cupLocal);
            var greenGo = new GameObject("Surface");
            greenGo.transform.SetParent(root.transform, false);
            greenGo.AddComponent<MeshFilter>().sharedMesh = surface;
            greenGo.AddComponent<MeshRenderer>().sharedMaterials = surface.subMeshCount > 2
                ? new[] { greenMat, cupMat, deckMat ? deckMat : greenMat }
                : new[] { greenMat, cupMat };
            var mc = greenGo.AddComponent<MeshCollider>();
            mc.sharedMesh = surface;
            mc.sharedMaterial = GolfMaterials.Course;
            greenGo.AddComponent<PlayableSurface>();

            var walls = BuildWalls(layout);
            var wallGo = new GameObject("Walls");
            wallGo.transform.SetParent(root.transform, false);
            wallGo.AddComponent<MeshFilter>().sharedMesh = walls;
            wallGo.AddComponent<MeshRenderer>().sharedMaterial = wallMat;
            var wc = wallGo.AddComponent<MeshCollider>();
            wc.sharedMesh = walls;
            wc.sharedMaterial = GolfMaterials.Course;

            cup = null;
            if (layout.cup.HasValue)
            {
                var cupGo = new GameObject("Cup");
                cupGo.transform.SetParent(root.transform, false);
                cupGo.transform.localPosition = cupLocal;
                cup = cupGo.AddComponent<Cup>();
                var flag = CreateFlag(cupGo.transform, tuning, flagMat);
                cup.Configure(tuning, flag);
                if (cupRimMat) cup.SetRim(cupRimMat);
            }
            return root;
        }

        static GameObject CreateFlag(Transform cup, GolfTuning tuning, Material mat)
        {
            var flag = new GameObject("Flag");
            flag.transform.SetParent(cup, false);
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            UnityEngine.Object.DestroyImmediate(pole.GetComponent<Collider>());
            pole.transform.SetParent(flag.transform, false);
            pole.transform.localPosition = new Vector3(0f, 0.6f - tuning.cupDepth * 0.5f, 0f);
            pole.transform.localScale = new Vector3(0.012f, 0.6f + tuning.cupDepth * 0.5f, 0.012f);
            var cloth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cloth.name = "Cloth";
            UnityEngine.Object.DestroyImmediate(cloth.GetComponent<Collider>());
            cloth.transform.SetParent(flag.transform, false);
            cloth.transform.localPosition = new Vector3(0.13f, 1.08f, 0f);
            cloth.transform.localScale = new Vector3(0.25f, 0.16f, 0.005f);
            if (mat)
            {
                pole.GetComponent<MeshRenderer>().sharedMaterial = mat;
                cloth.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
            return flag;
        }

        /// <summary>Adds a triangle, flipping it if needed so its normal faces <paramref name="want"/>.</summary>
        static void AddTri(List<int> tris, List<Vector3> v, int a, int b, int c, Vector3 want)
        {
            Vector3 n = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            if (Vector3.Dot(n, want) < 0f) { tris.Add(a); tris.Add(c); tris.Add(b); }
            else { tris.Add(a); tris.Add(b); tris.Add(c); }
        }

        /// <summary>Triangulates the ring between two closed loops that are star-shaped about centre.</summary>
        static void Zip(List<int> tris, List<Vector3> v, List<int> outer, List<int> inner, Vector3 centre)
        {
            float Ang(int idx) { Vector3 d = v[idx] - centre; return Mathf.Atan2(d.z, d.x); }
            outer = SortByAngle(outer, Ang);
            inner = SortByAngle(inner, Ang);
            int n = outer.Count, m = inner.Count;
            float OA(int k) => Ang(outer[k % n]) + (k >= n ? Mathf.PI * 2f : 0f);
            float IA(int k) => Ang(inner[k % m]) + (k >= m ? Mathf.PI * 2f : 0f);
            int i = 0, j = 0;
            while (i < n || j < m)
            {
                bool advanceOuter = j >= m || (i < n && OA(i + 1) <= IA(j + 1));
                if (advanceOuter)
                {
                    AddTri(tris, v, outer[i % n], outer[(i + 1) % n], inner[j % m], Vector3.up);
                    i++;
                }
                else
                {
                    AddTri(tris, v, inner[j % m], inner[(j + 1) % m], outer[i % n], Vector3.up);
                    j++;
                }
            }
        }

        static List<int> SortByAngle(List<int> loop, Func<int, float> ang)
        {
            var sorted = new List<int>(loop);
            sorted.Sort((a, b) => ang(a).CompareTo(ang(b)));
            return sorted;
        }

        class Grid
        {
            public readonly float minX, minZ, cell;
            public readonly int nx, nz;
            public readonly bool[,] inside;

            public Grid(GreenLayout l)
            {
                cell = l.cell;
                float maxX = float.MinValue, maxZ = float.MinValue;
                minX = float.MaxValue; minZ = float.MaxValue;
                foreach (var r in l.areas)
                {
                    minX = Mathf.Min(minX, r.xMin); minZ = Mathf.Min(minZ, r.yMin);
                    maxX = Mathf.Max(maxX, r.xMax); maxZ = Mathf.Max(maxZ, r.yMax);
                }
                minX = Mathf.Floor(minX / cell + 1e-4f) * cell;
                minZ = Mathf.Floor(minZ / cell + 1e-4f) * cell;
                nx = Mathf.CeilToInt((maxX - minX) / cell - 1e-4f);
                nz = Mathf.CeilToInt((maxZ - minZ) / cell - 1e-4f);
                inside = new bool[nx, nz];
                for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    var c = new Vector2(X(i) + cell * 0.5f, Z(j) + cell * 0.5f);
                    foreach (var r in l.areas)
                        if (r.Contains(c)) { inside[i, j] = true; break; }
                }
            }

            public float X(int i) => minX + i * cell;
            public float Z(int j) => minZ + j * cell;
        }
    }
}
