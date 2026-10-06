using System.Collections.Generic;
using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// Shape of the roulette bowl. Defaults come from the design simulator: a ball struck roughly along the rim enters the cone, orbits
    /// and settles. The numbers are sensitive around the ball's rest-hold slope (about 7.85%): a gentle rim shelf below it lets a ball rest
    /// to be re-struck; a cone well above it never lets a ball rest on the slope. Tune in the headset.
    /// </summary>
    [System.Serializable]
    public class RouletteBowlSpec
    {
        [Tooltip("Radius to the inside of the wall.")]
        public float radius = 2.0f;
        [Tooltip("Width of the gentle rim shelf next to the wall (a ball can rest here, so it can be re-struck).")]
        public float shelfWidth = 0.5f;
        [Tooltip("Radius of the flat apron around the cup (the lowest point).")]
        public float apronRadius = 0.30f;
        [Tooltip("Slope of the cone toward the cup. Above ~0.0785 a ball cannot rest on it; keep it near 0.10.")]
        [Range(0.08f, 0.16f)] public float coneSlope = 0.10f;
        [Tooltip("Slope of the rim shelf. Below ~0.0785 a ball can rest on it; above it the ball always runs in.")]
        [Range(0.0f, 0.12f)] public float shelfSlope = 0.065f;
        public float wallHeight = 0.14f;
        public float wallThickness = 0.05f;
        [Tooltip("Facets in the surface and the wall (more is rounder).")]
        [Range(32, 180)] public int segments = 96;
        [Tooltip("Tee position around the rim, degrees from +X toward +Z (looking down).")]
        public float teeAngleDegrees = -90f;
        [Tooltip("Optional entry notch for a ball arriving from outside (a jump): direction of the notch from the centre, degrees from +X toward +Z.")]
        public float entryAngleDegrees = 0f;
        [Tooltip("Half-width of the entry notch in degrees. 0 = the wall is closed all the way round. In the notch the wall is replaced by a low curb that a landing ball clears but a ball rolling out cannot.")]
        public float entryHalfWidthDegrees = 0f;
        [Tooltip("Optional second notch, for a ball that arrives by rolling (a ground-level approach lane) rather than by a jump: direction from the centre, degrees from +X toward +Z.")]
        public float gateAngleDegrees = 0f;
        [Tooltip("Half-width of the gate notch in degrees. 0 = no gate. It gets the same low curb as the entry notch.")]
        public float gateHalfWidthDegrees = 0f;

        /// <summary>True when the wall is replaced by a low curb at this angle (degrees from +X toward +Z): inside the entry notch or the gate.</summary>
        public bool InNotch(float angleDegrees) =>
            (entryHalfWidthDegrees > 0f && Mathf.Abs(Mathf.DeltaAngle(angleDegrees, entryAngleDegrees)) < entryHalfWidthDegrees)
            || (gateHalfWidthDegrees > 0f && Mathf.Abs(Mathf.DeltaAngle(angleDegrees, gateAngleDegrees)) < gateHalfWidthDegrees);

        /// <summary>Height of the notch curb's top above the rim of the shelf. A landing ball clears it, a ball rolling out cannot; an approach lane at this level rolls the ball in.</summary>
        public const float NotchCurbRise = 0.02f;

        public float ShelfInnerRadius => radius - shelfWidth;

        /// <summary>Surface height (cup rim = 0) at distance r from the centre.</summary>
        public float Height(float r)
        {
            if (r <= apronRadius) return 0f;
            float rIn = ShelfInnerRadius;
            if (r <= rIn) return coneSlope * (r - apronRadius);
            return coneSlope * (rIn - apronRadius) + shelfSlope * (r - rIn);
        }

        public Vector3 TeeLocal
        {
            get
            {
                float r = radius - shelfWidth * 0.5f, a = teeAngleDegrees * Mathf.Deg2Rad;
                return new Vector3(Mathf.Cos(a) * r, Height(r), Mathf.Sin(a) * r);
            }
        }

        public RouletteBowlSpec Clone() => (RouletteBowlSpec)MemberwiseClone();
    }

    /// <summary>
    /// A genuinely sloped round putting bowl with the cup at its lowest point. The surface is a cone (with a flat apron round the cup and a
    /// gentler rim shelf) closed by a circular wall. There is no scripted behaviour at all: no forces, no attraction, no Update loop. The
    /// ball enters, orbits, loses speed through the ordinary rolling model and settles toward the cup because the surface runs downhill
    /// to it. The component only builds geometry; call <see cref="Rebuild"/> after changing <see cref="Spec"/>.
    /// </summary>
    public class RouletteBowl : MonoBehaviour
    {
        [SerializeField] RouletteBowlSpec spec = new RouletteBowlSpec();
        [SerializeField] GolfTuning tuning;
        [SerializeField] ProvingMaterials materials = new ProvingMaterials();

        const string GeometryName = "RouletteBowlGeometry";
        /// <summary>How far the wall (and the notch curb) reach below the rim, so the bowl sits on a solid ring.</summary>
        const float BaseDepth = 0.4f;

        public RouletteBowlSpec Spec => spec;
        public Cup Cup { get; private set; }
        public GolfTuning Tuning => tuning ? tuning : GolfTuning.Default;

        public void Configure(RouletteBowlSpec s, GolfTuning t, ProvingMaterials m)
        {
            spec = s ?? new RouletteBowlSpec(); tuning = t; materials = m ?? new ProvingMaterials();
        }

        [ContextMenu("Rebuild")]
        public void Rebuild()
        {
            var old = transform.Find(GeometryName);
            if (old) ProvingKit.Discard(old.gameObject);
            var root = new GameObject(GeometryName).transform;
            root.SetParent(transform, false);

            var mesh = BuildMesh(spec, Tuning.cupRadius, Tuning.cupDepth);
            var surface = new GameObject("Surface");
            surface.transform.SetParent(root, false);
            surface.AddComponent<MeshFilter>().sharedMesh = mesh;
            surface.AddComponent<MeshRenderer>().sharedMaterials = new[] { materials.green, materials.cup ? materials.cup : materials.green };
            var mc = surface.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            mc.sharedMaterial = GolfMaterials.Course;
            surface.AddComponent<PlayableSurface>();

            // Wall: a ring of boxes whose inner faces are tangent to the radius.
            int n = Mathf.Max(24, spec.segments);
            float width = 2f * spec.radius * Mathf.Tan(Mathf.PI / n) + 0.01f;
            float hR = spec.Height(spec.radius);
            var walls = new GameObject("Wall").transform;
            walls.SetParent(root, false);
            for (int i = 0; i < n; i++)
            {
                float a = 2f * Mathf.PI * i / n;
                Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                if (spec.InNotch(a * Mathf.Rad2Deg))
                {
                    // Entry notch: a low curb instead of the wall (the same trick as the launch pad's lip).
                    // It stands on a solid base so nothing shows under it.
                    ProvingKit.Box("EntryCurb", walls, radial * (spec.radius + 0.02f) + Vector3.up * (hR + RouletteBowlSpec.NotchCurbRise - BaseDepth * 0.5f), Quaternion.LookRotation(radial, Vector3.up),
                        new Vector3(width, BaseDepth, 0.04f), materials.wall, true);
                    continue;
                }
                // The wall runs from BaseDepth below the rim to wallHeight above it: the bowl stands on a solid ring, with no gap under its rim.
                Vector3 pos = radial * (spec.radius + spec.wallThickness * 0.5f) + Vector3.up * (hR + (spec.wallHeight - BaseDepth) * 0.5f);
                ProvingKit.Box("WallSegment", walls, pos, Quaternion.LookRotation(radial, Vector3.up),
                    new Vector3(width, spec.wallHeight + BaseDepth, spec.wallThickness), materials.wall, true);
            }

            var cupGo = new GameObject("Cup");
            cupGo.transform.SetParent(root, false);
            Cup = cupGo.AddComponent<Cup>();
            Cup.Configure(Tuning, ProvingKit.MakeFlag(cupGo.transform, materials.flag));

            // Rebuilt after the hole was assembled (tuning in the editor): point the hole at the new cup and tee.
            var hole = GetComponent<HoleController>();
            if (hole)
            {
                hole.Configure(hole.Tuning, hole.HoleNumber, hole.Par, hole.Tee, hole.PlayerStart, Cup, hole.Ball, transform.position.y - 3f);
                hole.Tee.localPosition = spec.TeeLocal;
                if (Application.isPlaying && HoleController.Active == hole) hole.BeginHole(hole.Ball); // re-hook the new cup
            }
        }

        /// <summary>Surface (submesh 0) and cup interior (submesh 1), local to the bowl centre with the cup rim at y = 0.</summary>
        public static Mesh BuildMesh(RouletteBowlSpec s, float cupRadius, float cupDepth)
        {
            int ns = Mathf.Max(24, s.segments);
            var radii = new List<float> { cupRadius, Mathf.Max(cupRadius + 0.02f, s.apronRadius) };
            float rIn = s.ShelfInnerRadius;
            const int coneSteps = 14, shelfSteps = 4;
            for (int i = 1; i <= coneSteps; i++) radii.Add(Mathf.Lerp(radii[1], rIn, i / (float)coneSteps));
            for (int i = 1; i <= shelfSteps; i++) radii.Add(Mathf.Lerp(rIn, s.radius, i / (float)shelfSteps));

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var surf = new List<int>();
            var cupTris = new List<int>();
            for (int k = 0; k < radii.Count; k++)
                for (int j = 0; j < ns; j++)
                {
                    float a = 2f * Mathf.PI * j / ns;
                    var p = new Vector3(Mathf.Cos(a) * radii[k], s.Height(radii[k]), Mathf.Sin(a) * radii[k]);
                    verts.Add(p); uvs.Add(new Vector2(p.x, p.z));
                }
            int V(int k, int j) => k * ns + (j % ns);
            for (int k = 0; k < radii.Count - 1; k++)
                for (int j = 0; j < ns; j++)
                {
                    Tri(surf, verts, V(k, j), V(k, j + 1), V(k + 1, j + 1), Vector3.up);
                    Tri(surf, verts, V(k, j), V(k + 1, j + 1), V(k + 1, j), Vector3.up);
                }

            // Cup pit: wall facing inward, flat bottom.
            var top = new List<int>(); var bottom = new List<int>();
            for (int j = 0; j < ns; j++)
            {
                float a = 2f * Mathf.PI * j / ns;
                var p = new Vector3(Mathf.Cos(a) * cupRadius, 0f, Mathf.Sin(a) * cupRadius);
                top.Add(verts.Count); verts.Add(p); uvs.Add(new Vector2(j / (float)ns, 1f));
                bottom.Add(verts.Count); verts.Add(p + Vector3.down * cupDepth); uvs.Add(new Vector2(j / (float)ns, 0f));
            }
            for (int j = 0; j < ns; j++)
            {
                int n2 = (j + 1) % ns;
                Vector3 mid = (verts[top[j]] + verts[top[n2]]) * 0.5f;
                Vector3 inward = -new Vector3(mid.x, 0f, mid.z);
                Tri(cupTris, verts, top[j], bottom[j], bottom[n2], inward);
                Tri(cupTris, verts, top[j], bottom[n2], top[n2], inward);
            }
            int centre = verts.Count;
            verts.Add(Vector3.down * cupDepth); uvs.Add(new Vector2(0.5f, 0f));
            for (int j = 0; j < ns; j++) Tri(cupTris, verts, centre, bottom[j], bottom[(j + 1) % ns], Vector3.up);

            var mesh = new Mesh { name = "RouletteBowl" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(surf, 0);
            mesh.SetTriangles(cupTris, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void Tri(List<int> tris, List<Vector3> v, int a, int b, int c, Vector3 want)
        {
            Vector3 n = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            if (Vector3.Dot(n, want) < 0f) { tris.Add(a); tris.Add(c); tris.Add(b); }
            else { tris.Add(a); tris.Add(b); tris.Add(c); }
        }
    }
}
