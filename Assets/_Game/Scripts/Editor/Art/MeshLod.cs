using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gamebreak.MiniGolf.Editor.Art
{
    /// <summary>
    /// Editor-time mesh simplification by vertex clustering: vertices are snapped to a 3D grid, each cell becomes one
    /// vertex (averaged position, normal and colour) and degenerate triangles are dropped. The grid size is searched so the
    /// result has at most the requested triangle count. Crude, but deterministic and good enough for distant LODs of
    /// UV-less rocks (RockTriplanar only reads position, normal and vertex colour). UVs are not carried over.
    /// </summary>
    public static class MeshLod
    {
        struct Result { public List<Vector3> pos, nrm; public List<Color> col; public List<int> tris; }

        public static Mesh Cluster(Mesh src, int targetTris, string name)
        {
            var v = src.vertices;
            var n = src.normals;
            var c = src.colors;
            var tris = new List<int>();
            for (int s = 0; s < src.subMeshCount; s++) tris.AddRange(src.GetTriangles(s));
            var b = src.bounds;
            float lo = 0.002f, hi = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            Result best = Build(v, n, c, tris, b.min, hi);
            for (int i = 0; i < 18; i++)
            {
                float mid = Mathf.Sqrt(lo * hi);
                var r = Build(v, n, c, tris, b.min, mid);
                if (r.tris.Count / 3 > targetTris) lo = mid; else { hi = mid; best = r; }
            }
            var mesh = new Mesh { name = name, indexFormat = best.pos.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.SetVertices(best.pos);
            mesh.SetNormals(best.nrm);
            mesh.SetColors(best.col);
            mesh.SetTriangles(best.tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static Result Build(Vector3[] v, Vector3[] n, Color[] c, List<int> tris, Vector3 origin, float cell)
        {
            var map = new Dictionary<long, int>();
            var sumP = new List<Vector3>(); var sumN = new List<Vector3>(); var sumC = new List<Color>(); var cnt = new List<int>();
            var remap = new int[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                int ix = (int)((v[i].x - origin.x) / cell), iy = (int)((v[i].y - origin.y) / cell), iz = (int)((v[i].z - origin.z) / cell);
                long key = ix | ((long)iy << 21) | ((long)iz << 42);
                if (!map.TryGetValue(key, out int id))
                {
                    id = sumP.Count; map[key] = id;
                    sumP.Add(Vector3.zero); sumN.Add(Vector3.zero); sumC.Add(Color.clear); cnt.Add(0);
                }
                remap[i] = id;
                sumP[id] += v[i];
                if (n.Length == v.Length) sumN[id] += n[i];
                sumC[id] += c.Length == v.Length ? c[i] : Color.white;
                cnt[id]++;
            }
            var seen = new HashSet<(int, int, int)>();
            var outTris = new List<int>();
            var used = new int[sumP.Count];
            for (int i = 0; i + 2 < tris.Count; i += 3)
            {
                int a = remap[tris[i]], b = remap[tris[i + 1]], d = remap[tris[i + 2]];
                if (a == b || b == d || a == d) continue;
                // Canonical rotation keeps orientation but removes duplicates from collapsed sheets.
                (int, int, int) key = a <= b && a <= d ? (a, b, d) : b <= a && b <= d ? (b, d, a) : (d, a, b);
                if (!seen.Add(key)) continue;
                outTris.Add(a); outTris.Add(b); outTris.Add(d);
                used[a] = used[b] = used[d] = 1;
            }
            // Compact to the vertices actually used.
            var final = new int[sumP.Count];
            var r = new Result { pos = new List<Vector3>(), nrm = new List<Vector3>(), col = new List<Color>(), tris = new List<int>(outTris.Count) };
            for (int i = 0; i < sumP.Count; i++)
            {
                if (used[i] == 0) { final[i] = -1; continue; }
                final[i] = r.pos.Count;
                r.pos.Add(sumP[i] / cnt[i]);
                var nn = sumN[i];
                r.nrm.Add(nn.sqrMagnitude > 1e-8f ? nn.normalized : Vector3.up);
                r.col.Add(sumC[i] / cnt[i]);
            }
            foreach (int t in outTris) r.tris.Add(final[t]);
            return r;
        }
    }
}
