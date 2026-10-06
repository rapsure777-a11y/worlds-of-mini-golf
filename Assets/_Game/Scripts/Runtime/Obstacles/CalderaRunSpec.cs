using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// Hole 8, "Caldera Run": a crater route that builds toward a large roulette bowl, with a bold optional jump straight into it. Both routes end in the
    /// same bowl (the proven <see cref="RouletteBowl"/>: no forces, no scripted steering) and the cup is its lowest point. Plan (x across, z up; defaults):
    ///
    ///   shortcut  the tee lane runs straight north (z 0..2.6) into the proven launch ramp (<see cref="JumpBowlSpec"/>); a firm putt (about 4.4 m/s) leaps the
    ///             0.6 m gap and lands on the bowl's rim shelf through its jump notch, moving tangentially, so it orbits. Short = the pit (one stroke, back to
    ///             the last rest spot); a soft putt just rolls back down the ramp.
    ///   caldera route  the same tee also opens west (z 0.5..1.5): a crater-floor lane to a corner bank that turns the ball north up a 3 m crater-wall
    ///             climb (a 0.11 m rise at 3%), a second bank at the top that turns it east, and a straight lane that rolls into the bowl through a
    ///             ground-level gate on its north-west side: a low curb the ball steps over, flush with the lane, offset from the bowl's centre so the ball enters
    ///             tangentially (the same sense as the jump) and orbits. Nothing pulls it in: a slow entry orbits the shelf and leaves a real putt,
    ///             a good one settles close or drops.
    ///
    /// The gate and the jump notch are both the bowl's low curb (<see cref="RouletteBowlSpec.InNotch"/>): a ball in the bowl cannot roll back out of either.
    /// </summary>
    [System.Serializable]
    public class CalderaRunSpec
    {
        public JumpBowlSpec jump = NewJump();
        [Header("Caldera route")]
        [Tooltip("x of the route's west wall.")]
        public float westX = -3.4f;
        public float columnWidth = 1.0f;
        public float routeZ0 = 0.5f, routeZ1 = 1.5f;
        [Tooltip("The climb up the crater wall starts and ends at these z (the column's floor rises linearly to the gate level).")]
        public float climbStartZ = 1.8f, climbEndZ = 5.2f;
        [Header("Gate lane (the ground-level bowl entry)")]
        [Tooltip("How far north of the bowl's centre the gate lane's centre line runs. Larger = a more tangential entry.")]
        public float gateOffset = 0.77f;
        public float gateLaneWidth = 0.8f;
        [Header("Rails and kickers")]
        public float wallHeight = 0.16f;
        [Tooltip("Height of the low ring round the cup that a jump landing has to climb. 0 = none.")]
        public float ringHeight = 0.015f;
        [Tooltip("Half-width (degrees) of the ring opening centred on the gate, so a ball rolled in through the gate is not hindered.")]
        public float ringOpeningHalf = 55f;

        static float Snap(float v) => Mathf.Round(v * 10f) / 10f;

        /// <summary>The proven jump with this hole's choices: a 14 degree ramp, a 0.6 m gap and a large (2 m) bowl entered at a tangent from the west.</summary>
        public static JumpBowlSpec NewJump()
        {
            var s = new JumpBowlSpec { entryOffsetX = 0.8f, entryHalfAngle = 26f };
            s.ramp.width = 1.2f;
            s.ramp.approachLength = 2.0f;
            s.ramp.gap = 1.0f;
            s.ramp.teeZ = 0.9f;
            s.bowl.radius = 1.5f;
            s.bowl.shelfWidth = 0.5f;
            return s;
        }

        const float Tol = 0.05f;

        // ---------------- geometry (hole space)

        /// <summary>The jump spec with the gate notch filled in (it depends on where the gate lane meets the bowl).</summary>
        public JumpBowlSpec Jump
        {
            get
            {
                var (angle, half) = GateArc();
                jump.bowl.gateAngleDegrees = angle;
                jump.bowl.gateHalfWidthDegrees = half;
                // An inner ring round the cup, open on the gate side so the long route is not hindered.
                jump.bowl.ringHeight = ringHeight;
                jump.bowl.ringOpeningAngleDegrees = angle;
                jump.bowl.ringOpeningHalfWidthDegrees = ringOpeningHalf;
                return jump;
            }
        }

        public float TeeHalf => jump.ramp.Width * 0.5f;
        public Vector2 BowlCentre => jump.BowlCentreXZ;
        public float BowlRadius => jump.bowl.radius;
        public float WestX => Snap(westX);
        public float ColumnEastX => WestX + Snap(columnWidth);
        public float GateLaneZ0 => Snap(BowlCentre.y + gateOffset - gateLaneWidth * 0.5f);
        public float GateLaneZ1 => GateLaneZ0 + Snap(gateLaneWidth);
        /// <summary>Radius of the arc where the approach meets the bowl: the middle of the notch curb.</summary>
        public float GateArcRadius => BowlRadius + 0.02f;

        /// <summary>x of the bowl's rim at hole-space z (the west side), on the gate arc.</summary>
        public float GateArcX(float z)
        {
            float v = z - BowlCentre.y, r = GateArcRadius;
            return BowlCentre.x - Mathf.Sqrt(Mathf.Max(0.0001f, r * r - v * v));
        }

        /// <summary>The lane ends this far (on the 0.1 m grid) before the rim's nearest point, with the stone approach filling the rest.</summary>
        public float GateLaneEndX
        {
            get
            {
                // The arc is westernmost at the bowl centre's own z (or at the nearer edge of the lane when that lies outside it).
                float nearest = GateArcX(Mathf.Clamp(BowlCentre.y, GateLaneZ0, GateLaneZ1));
                return Mathf.Floor((nearest - 0.05f) * 10f) / 10f;
            }
        }

        /// <summary>Surface height (hole space) of the gate lane and its stone approach: level with the notch curb's top, 2 cm above the bowl's rim shelf.</summary>
        public float GateLevel => jump.BowlOrigin.y + jump.bowl.Height(jump.bowl.radius) + RouletteBowlSpec.NotchCurbRise;

        /// <summary>Direction (degrees from +X toward +Z, about the bowl's centre) and half-width of the arc the gate lane covers, with a margin.</summary>
        public (float angle, float halfWidth) GateArc()
        {
            float r = GateArcRadius;
            float Angle(float z) { float v = z - BowlCentre.y; return Mathf.Atan2(v, -Mathf.Sqrt(Mathf.Max(0.0001f, r * r - v * v))) * Mathf.Rad2Deg; }
            float a0 = Angle(GateLaneZ0), a1 = Angle(GateLaneZ1);
            return ((a0 + a1) * 0.5f, Mathf.Abs(a1 - a0) * 0.5f + 2.5f);
        }

        public Vector2 Tee => new Vector2(0f, jump.ramp.teeZ);

        /// <summary>Surface height at hole-space (x, z).</summary>
        public float Height(float x, float z)
        {
            if (x >= ColumnEastX - Tol && z >= GateLaneZ0 - Tol && x < TeeHalf) return GateLevel;     // the gate lane
            if (x >= -TeeHalf - Tol) return jump.LaneHeight(x, z);                                    // the tee lane and ramp (the junction's vertices too)
            float climb = Mathf.Clamp01((z - climbStartZ) / Mathf.Max(0.1f, climbEndZ - climbStartZ));
            return GateLevel * climb;                                                                 // the crater floor, then the climb up the column
        }

        public GreenLayout BuildLayout()
        {
            var l = jump.LaneLayout();
            l.Area(WestX, routeZ0, -TeeHalf - WestX, routeZ1 - routeZ0);                       // the crater floor, opening off the tee lane
            l.Area(WestX, routeZ1, ColumnEastX - WestX, GateLaneZ1 - routeZ1);                  // the climb
            l.Area(ColumnEastX, GateLaneZ0, GateLaneEndX - ColumnEastX, GateLaneZ1 - GateLaneZ0);   // the gate lane
            l.openEdges.Add(new Rect(GateLaneEndX - 0.005f, GateLaneZ0, 0.01f, GateLaneZ1 - GateLaneZ0));
            l.wallHeight = Mathf.Max(l.wallHeight, 0.12f);
            l.height = Height;
            return l;
        }

        Vector3 P(float x, float z) => new Vector3(x, Height(x, z), z);

        /// <summary>Angled walls with their end points (hole space), also used by tests: a bank that turns the ball north up the climb, and one that turns it east into the gate lane.</summary>
        public List<(string name, Vector3 a, Vector3 b)> WallPlan()
        {
            float cw = ColumnEastX - WestX;
            return new List<(string, Vector3, Vector3)>
            {
                ("CraterBankSouth", P(WestX, routeZ1), P(WestX + cw, routeZ0)),
                ("CraterBankNorth", P(WestX, GateLaneZ1 - cw), P(ColumnEastX, GateLaneZ1)),
            };
        }

        /// <summary>
        /// Low rails along the two sides of the stone approach (the lane's own rails stop at its end), from the lane's end to the bowl's curb: nothing can roll off the
        /// approach's edges. Each stands 3 cm outside the lane's edge line so it does not narrow the lane. End points in hole space, also used by tests.
        /// </summary>
        public List<(string name, Vector3 a, Vector3 b)> GateRails()
        {
            float x0 = GateLaneEndX - 0.03f, zs = GateLaneZ0 - 0.03f, zn = GateLaneZ1 + 0.03f;
            return new List<(string, Vector3, Vector3)>
            {
                ("GateRailSouth", P(x0, zs), new Vector3(GateArcX(zs) + 0.02f, GateLevel, zs)),
                ("GateRailNorth", P(x0, zn), new Vector3(GateArcX(zn) + 0.02f, GateLevel, zn)),
            };
        }

        /// <summary>The ground the hole occupies beyond its green rectangles: the bowl and the pit under the gap (terrain plateau and foliage keep-out).</summary>
        public List<Rect> ExtraAreas()
        {
            var r = new List<Rect> { jump.BowlFootprint };
            var ramp = jump.ramp;
            r.Add(new Rect(-ramp.Width * 0.5f - 0.8f, ramp.LipZ, ramp.Width + 1.6f, ramp.Gap + 1f));
            return r;
        }

        public CalderaRunSpec Clone() => (CalderaRunSpec)MemberwiseClone();

        /// <summary>Human-readable problems with this combination (empty when it will work).</summary>
        public string Validate()
        {
            if (GateLaneEndX >= GateArcX(GateLaneZ0) - 0.05f || GateLaneEndX >= GateArcX(GateLaneZ1) - 0.05f) return "the gate lane reaches into the bowl";
            if (GateLaneEndX <= ColumnEastX + 0.3f) return "the gate lane is too short";
            if (GateLevel < 0.03f) return "the route has nothing to climb";
            if (GateLevel / Mathf.Max(0.1f, climbEndZ - climbStartZ) > 0.0785f) return "the climb is steeper than the rest-hold slope";
            if (gateOffset < gateLaneWidth * 0.5f + 0.1f) return "the gate lane enters too head-on: it should be offset from the bowl's centre";
            var (a, half) = GateArc();
            float jumpAngle = jump.BowlForHole().entryAngleDegrees;
            if (Mathf.Abs(Mathf.DeltaAngle(a, jumpAngle)) < half + jump.entryHalfAngle + 10f) return "the gate and the jump notch overlap";
            return "";
        }

        /// <summary>The stone approach between the gate lane's end and the bowl's rim, as a flat mesh (hole space), flush with the lane and with the notch curb.</summary>
        public Mesh BuildApproachMesh()
        {
            const int strips = 10;
            float z0 = GateLaneZ0, z1 = GateLaneZ1, y = GateLevel, xe = GateLaneEndX;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            for (int k = 0; k <= strips; k++)
            {
                float z = Mathf.Lerp(z0, z1, k / (float)strips);
                verts.Add(new Vector3(xe, y, z)); uvs.Add(new Vector2(xe, z));
                float xa = GateArcX(z);
                verts.Add(new Vector3(xa, y, z)); uvs.Add(new Vector2(xa, z));
            }
            var tris = new List<int>();
            for (int k = 0; k < strips; k++)
            {
                int a = 2 * k, b = 2 * k + 1, c = 2 * k + 2, d = 2 * k + 3;    // a,c on the lane's end (west), b,d on the arc (east)
                AddUp(tris, verts, a, c, d);
                AddUp(tris, verts, a, d, b);
            }
            var mesh = new Mesh { name = "CalderaGateApproach" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void AddUp(List<int> tris, List<Vector3> v, int a, int b, int c)
        {
            if (Vector3.Cross(v[b] - v[a], v[c] - v[a]).y < 0f) { tris.Add(a); tris.Add(c); tris.Add(b); }
            else { tris.Add(a); tris.Add(b); tris.Add(c); }
        }

        /// <summary>
        /// Builds everything that is not the green itself under <paramref name="root"/> (hole space): the proven jump pieces (ramp frame, the bowl with its
        /// jump notch and gate, the pit under the gap), the stone approach into the gate, and the crater banks. Returns the bowl's cup.
        /// </summary>
        public Cup BuildPieces(Transform root, GolfTuning tuning, ProvingMaterials mats)
        {
            Cup cup = ProvingGround.BuildJumpBowlPieces(root, tuning, mats, Jump);

            var approach = new GameObject("GateApproach");
            approach.transform.SetParent(root, false);
            var mesh = BuildApproachMesh();
            approach.AddComponent<MeshFilter>().sharedMesh = mesh;
            approach.AddComponent<MeshRenderer>().sharedMaterial = mats.green;
            var mc = approach.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            mc.sharedMaterial = GolfMaterials.Course;
            approach.AddComponent<PlayableSurface>();

            var walls = new GameObject("CalderaWalls").transform;
            walls.SetParent(root, false);
            foreach (var (name, a, b) in WallPlan().Concat(GateRails()))
                BankWall.Segment(walls, a, b, mats.wall, wallHeight, BankWall.DefaultThickness, BankWall.DefaultSkirt, name);
            return cup;
        }
    }
}
