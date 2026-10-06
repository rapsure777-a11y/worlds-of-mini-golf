using System.Collections.Generic;
using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// Hole 7, "Lava Falls": three tiers, a lava hazard on every tier, and a vent that carries the ball across a lava river to the last tier. One
    /// rectangle-union green with a height function (like Holes 5 and 6), <see cref="BankWall"/> pieces, lava (out-of-bounds boxes) and one
    /// <see cref="VentTransport"/>. Plan (x across, z up the hole, metres; defaults):
    ///
    ///   Tier 1 (height 0)      tee pad (z 0..2) and a cross lane (z 2..4) with a 45 degree bank at its corner: a tee putt banks east.
    ///                          A causeway (x 1.8..3.0, z 4..7.2) climbs 0.24 m at 7.5% (a ball can rest on it) with a lava lake along its open west side.
    ///   Tier 2 (height 0.24)   a pad with the vent mouth (a shallow dish) near its back wall; the pad's south-west edge is open to the lava lake.
    ///   the vent               captures a slow ball in the mouth, charges, and carries it along a visible chute over a lava river to Tier 3.
    ///   Tier 3 (height 0.80)   the final green (3.4 x 5.6 m) beside the falls; the ball leaves the vent mouth with 2 m/s straight up the green,
    ///                          rolls out about 3 m and rests ~2 m from the cup (not in line with it). Lava lies off the green's west edge.
    ///
    /// Misses: a ball too fast for the mouth rebounds off the back wall for a slower second pass; a ball that leaves a lava edge costs one stroke
    /// and returns to its last rest spot; nothing can trap the ball (the vent carries it no matter what, and the ride has a safety timeout).
    /// </summary>
    [System.Serializable]
    public class LavaFallsSpec
    {
        [Header("Tier 1")]
        public float padWidth = 1.6f;
        public float padLength = 2.0f;
        public float crossLength = 2.0f;
        public float crossEastX = 3.0f;
        [Header("Causeway to Tier 2")]
        public float causewayWidth = 1.2f;
        public float causewayRun = 3.2f;
        [Tooltip("Rise of Tier 2. The causeway is rise/run: keep it under about 7.8% so a ball can rest on it and be struck again.")]
        public float tier2Rise = 0.24f;
        [Header("Tier 2 and the vent")]
        public float tier2Length = 2.8f;
        public float tier2WestX = 0.6f;
        [Tooltip("Distance of the vent mouth from Tier 2's back wall.")]
        public float mouthFromBack = 0.7f;
        public float dishRadius = 0.45f;
        public float dishDepth = 0.045f;
        [Tooltip("Flat radius at the bottom of the dish. 0 keeps the dish steeper than the rest-hold slope on the 0.1 m grid, so a ball can only come to rest at its centre.")]
        public float dishApron = 0f;
        public VentTransportSpec vent = NewVent();
        [Header("Tier 3")]
        public float riverWidth = 4.0f;
        [Tooltip("Height of Tier 3 above Tier 1.")]
        public float tier3Height = 0.80f;
        public float tier3WestX = -0.4f;
        public float tier3Length = 5.6f;
        [Tooltip("The vent's exit sits this far from Tier 3's front edge.")]
        public float exitFromFront = 0.5f;
        public Vector2 cupFromWestFront = new Vector2(0.8f, 4.6f);
        [Header("Rails and lava")]
        public float railHeight = 0.12f;
        public float wallHeight = 0.16f;
        [Tooltip("How far the lava surface lies below the floor of the tier beside it.")]
        public float lavaDrop = 0.12f;

        static float Snap(float v) => Mathf.Round(v * 10f) / 10f;

        public static VentTransportSpec NewVent() => new VentTransportSpec
        {
            captureRadius = 0.15f, maxCaptureSpeed = 2.4f, captureBlendSeconds = 0.2f, sinkDepth = 0.03f,
            dwellSeconds = 0.6f, travelSeconds = 1.6f, exitSpeed = 2.0f, exitDirection = Vector3.forward,
        };

        const float Tol = 0.05f;   // vertices on a boundary belong to the higher-numbered side, so lanes keep their full width

        // ---------------- geometry (hole space)

        public float PadWidth => Snap(padWidth);
        public float PadHalf => PadWidth * 0.5f;
        public float PadLength => Snap(padLength);
        public float CrossEndZ => PadLength + Snap(crossLength);
        public float CrossEastX => Snap(crossEastX);
        public float CausewayWidth => Snap(causewayWidth);
        public float CausewayWestX => CrossEastX - CausewayWidth;
        public float CausewayRun => Snap(causewayRun);
        public float RampEndZ => CrossEndZ + CausewayRun;
        public float Tier2Height => tier2Rise;
        public float CausewaySlope => tier2Rise / CausewayRun;
        public float Tier2WestX => Snap(tier2WestX);
        public float Tier2EndZ => RampEndZ + Snap(tier2Length);
        public float Tier3FrontZ => Tier2EndZ + Snap(riverWidth);
        public float Tier3Height => tier3Height;
        public float Tier3WestX => Snap(tier3WestX);
        public float Tier3EndZ => Tier3FrontZ + Snap(tier3Length);
        /// <summary>Vent mouth on Tier 2 (hole-space XZ): in line with the causeway.</summary>
        public Vector2 Mouth => new Vector2((CausewayWestX + CrossEastX) * 0.5f, Tier2EndZ - mouthFromBack);
        public Vector2 ExitXZ => new Vector2(Mouth.x, Tier3FrontZ + exitFromFront);
        public Vector2 Tee => new Vector2(0f, 0.6f);
        public Vector2 Cup => new Vector2(Snap(Tier3WestX + cupFromWestFront.x), Snap(Tier3FrontZ + cupFromWestFront.y));

        /// <summary>Surface height at hole-space (x, z).</summary>
        public float Height(float x, float z)
        {
            if (z >= Tier3FrontZ - Tol) return Tier3Height;
            if (z >= RampEndZ - Tol)
            {
                Vector2 m = Mouth;
                float d = Mathf.Sqrt((x - m.x) * (x - m.x) + (z - m.y) * (z - m.y));
                if (d < dishRadius)
                {
                    float t = Mathf.Clamp01((Mathf.Max(d, dishApron) - dishApron) / Mathf.Max(0.01f, dishRadius - dishApron));
                    return Tier2Height - dishDepth * (1f - t);
                }
                return Tier2Height;
            }
            if (x >= CausewayWestX - Tol && z >= CrossEndZ - Tol) return Tier2Height * Mathf.Clamp01((z - CrossEndZ) / CausewayRun);
            return 0f;
        }

        public GreenLayout BuildLayout()
        {
            var l = new GreenLayout { wallHeight = railHeight };
            float pw = PadHalf;
            l.Area(-pw, 0f, PadWidth, PadLength);                                     // tee pad
            l.Area(-pw, PadLength, CrossEastX + pw, CrossEndZ - PadLength);           // cross lane
            l.Area(CausewayWestX, CrossEndZ, CausewayWidth, CausewayRun);             // causeway
            l.Area(Tier2WestX, RampEndZ, CrossEastX - Tier2WestX, Tier2EndZ - RampEndZ);   // Tier 2 pad
            l.Area(Tier3WestX, Tier3FrontZ, CrossEastX - Tier3WestX, Tier3EndZ - Tier3FrontZ);   // Tier 3: the final green
            // No rail where the lava is: the causeway's west side, Tier 2's south-west edge and part of Tier 3's west edge.
            l.openEdges.Add(new Rect(CausewayWestX - 0.005f, CrossEndZ, 0.01f, CausewayRun));
            l.openEdges.Add(new Rect(Tier2WestX, RampEndZ - 0.005f, CausewayWestX - Tier2WestX, 0.01f));
            var w = Tier3WestEdge();
            l.openEdges.Add(new Rect(Tier3WestX - 0.005f, w.z0, 0.01f, w.z1 - w.z0));
            l.cup = Cup;
            l.height = Height;
            return l;
        }

        /// <summary>The stretch of Tier 3's west edge that is open to the lava (z range), level with the cup's approach.</summary>
        public (float z0, float z1) Tier3WestEdge() => (Snap(Tier3FrontZ + 1.4f), Snap(Tier3FrontZ + 3.8f));

        /// <summary>The hole's footprint, for the terrain plateau and the foliage keep-out.</summary>
        public Rect Footprint => new Rect(-2.2f, 0f, CrossEastX + 3.6f, Tier3EndZ);

        Vector3 P(float x, float z) => new Vector3(x, Height(x, z), z);

        /// <summary>Angled walls with their end points (hole space), also used by tests. The tee pad's corner bank turns a tee putt east along the cross lane.</summary>
        public List<(string name, Vector3 a, Vector3 b)> WallPlan()
        {
            float pw = PadHalf, leg = Snap(pw * 2f);
            return new List<(string, Vector3, Vector3)>
            {
                ("BendBank", P(-pw, CrossEndZ - leg), P(pw, CrossEndZ)),
            };
        }

        /// <summary>Lava surfaces (out-of-bounds boxes): name, XZ extent and the height of the lava surface.</summary>
        public List<(string name, float x0, float x1, float z0, float z1, float top)> LavaPlan()
        {
            float low = -lavaDrop, t3 = Tier3Height - lavaDrop;
            var w = Tier3WestEdge();
            return new List<(string, float, float, float, float, float)>
            {
                ("LavaLake", -PadHalf - 0.8f, CausewayWestX, CrossEndZ + 0.05f, RampEndZ + 0.1f, low),
                ("LavaRiver", -PadHalf - 1.2f, CrossEastX + 1.4f, Tier2EndZ, Tier3FrontZ, low),
                ("LavaPool", Tier3WestX - 1.6f, Tier3WestX, w.z0, w.z1, t3),
            };
        }

        /// <summary>The vent's ride, in hole space: the ball's centre in the mouth, up and over the river, down to the exit.</summary>
        public Vector3[] RidePath(float ballRadius)
        {
            Vector2 m = Mouth, e = ExitXZ;
            float y0 = Height(m.x, m.y) + ballRadius, y1 = Tier3Height + ballRadius + 0.004f;
            float zStart = m.y, zEnd = e.y;
            Vector3 At(float f, float y) => new Vector3(Mathf.Lerp(m.x, e.x, f), y, Mathf.Lerp(zStart, zEnd, f));
            var control = new[]
            {
                new Vector3(m.x, y0, zStart), At(0.12f, y0 + 0.40f), At(0.34f, 1.15f), At(0.62f, 1.22f), At(0.88f, y1 + 0.16f), new Vector3(e.x, y1, zEnd),
            };
            return VentTransport.Smooth(control, 8);
        }

        public LavaFallsSpec Clone() { var c = (LavaFallsSpec)MemberwiseClone(); c.vent = vent.Clone(); return c; }

        /// <summary>Human-readable problems with this combination (empty when it will work).</summary>
        public string Validate()
        {
            if (CausewaySlope > 0.0785f) return "the causeway is steeper than the rest-hold slope";
            if (CausewaySlope < 0.04f) return "the causeway is too gentle to read as a climb";
            if (Tier3Height <= Tier2Height + 0.2f) return "Tier 3 must stand clearly above Tier 2";
            if (Mathf.Abs(Mouth.x - ExitXZ.x) > 0.01f) return "the exit should be in line with the mouth";
            if (Tier3Length() < 4f) return "Tier 3 is too short for a real putting section";
            if (Vector2.Distance(Cup, ExitXZ) < 1.5f) return "the cup is too close to the vent exit";
            if (vent.exitSpeed < 1.2f) return "the exit speed is too low for the ball to roll away from the exit";
            return "";
        }

        float Tier3Length() => Tier3EndZ - Tier3FrontZ;

        /// <summary>
        /// Builds everything that is not the green itself under <paramref name="root"/> (hole space): the bank walls, the lava, the vent (mouth stones, the
        /// <see cref="VentTransport"/> component, the visible chute with its supports) and a low stone cannon at the exit. Returns the cup found in the green.
        /// </summary>
        public Cup BuildPieces(Transform root, GolfTuning tuning, ProvingMaterials mats)
        {
            float r = tuning.ballRadius;
            var walls = new GameObject("LavaFallsWalls").transform;
            walls.SetParent(root, false);
            foreach (var (name, a, b) in WallPlan())
                BankWall.Segment(walls, a, b, mats.wall, wallHeight, BankWall.DefaultThickness, BankWall.DefaultSkirt, name);

            // Lava: solid out-of-bounds boxes (a ball that falls onto one costs a stroke and returns to its last rest spot). The scene builder re-skins them as lava.
            var lava = new GameObject("Lava").transform;
            lava.SetParent(root, false);
            foreach (var (name, x0, x1, z0, z1, top) in LavaPlan())
                ProvingKit.Box(name, lava, new Vector3((x0 + x1) * 0.5f, top - 0.25f, (z0 + z1) * 0.5f), Quaternion.identity,
                    new Vector3(x1 - x0, 0.5f, z1 - z0), mats.wood, true, false, true);

            // The vent itself.
            var path = RidePath(r);
            var go = new GameObject("VentTransport");
            go.transform.SetParent(root, false);
            var vent = go.AddComponent<VentTransport>();
            vent.Configure(this.vent, path);
            BuildVentDressing(root, mats, path, r);
            return root.GetComponentInChildren<Cup>();
        }

        /// <summary>Decorative (collider-free) mouth stones, chute trough and supports, and the exit cannon.</summary>
        void BuildVentDressing(Transform root, ProvingMaterials mats, Vector3[] path, float r)
        {
            var dress = new GameObject("VentDressing").transform;
            dress.SetParent(root, false);
            Material stone = mats.wall, metal = mats.metal ? mats.metal : mats.wood;
            Vector2 m = Mouth;
            float floor = Tier2Height;

            // The mouth: a ring of low stones round the dish, with an open front toward the causeway.
            const int stones = 14;
            for (int i = 0; i < stones; i++)
            {
                float a = 360f * i / stones;
                Vector3 radial = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), 0f, Mathf.Sin(a * Mathf.Deg2Rad));
                if (radial.z < -0.55f) continue;                         // the south side stays open: the ball arrives from there
                ProvingKit.Box("MouthStone", dress, new Vector3(m.x, floor + 0.03f, m.y) + radial * (dishRadius + 0.05f), Quaternion.LookRotation(radial, Vector3.up),
                    new Vector3(0.14f, 0.06f, 0.07f), stone, false);
            }
            // An arch over the mouth, where the chute starts.
            for (int side = -1; side <= 1; side += 2)
                ProvingKit.Box("MouthPost", dress, new Vector3(m.x + side * (dishRadius + 0.12f), floor + 0.3f, m.y + 0.1f), Quaternion.identity, new Vector3(0.12f, 0.6f, 0.12f), stone, false);
            ProvingKit.Box("MouthLintel", dress, new Vector3(m.x, floor + 0.62f, m.y + 0.1f), Quaternion.identity, new Vector3(dishRadius * 2f + 0.4f, 0.1f, 0.14f), stone, false);

            // The chute: an open trough along the ride, so the ball is seen all the way, plus supports.
            for (int i = 0; i + 2 < path.Length; i += 2)
            {
                Vector3 a = path[i], b = path[Mathf.Min(i + 2, path.Length - 1)];
                Vector3 d = b - a;
                if (d.magnitude < 1e-3f) continue;
                Quaternion rot = Quaternion.LookRotation(d.normalized, Vector3.up);
                Vector3 mid = (a + b) * 0.5f;
                ProvingKit.Box("ChuteFloor", dress, mid + Vector3.down * (r + 0.015f), rot, new Vector3(0.22f, 0.03f, d.magnitude + 0.02f), metal, false);
                for (int side = -1; side <= 1; side += 2)
                    ProvingKit.Box("ChuteRail", dress, mid + rot * new Vector3(side * 0.115f, -0.005f, 0f), rot, new Vector3(0.025f, 0.06f, d.magnitude + 0.02f), metal, false);
                if ((i / 2) % 3 == 1 && a.z > Tier2EndZ - 0.4f && a.z < Tier3FrontZ + 0.4f)
                    ProvingKit.Box("ChuteSupport", dress, new Vector3(a.x, (a.y - 0.4f) * 0.5f, a.z), Quaternion.identity, new Vector3(0.1f, a.y + 0.4f, 0.1f), stone, false);
            }

            // The exit: a short stone cannon barrel on Tier 3 that the chute runs into (the ball leaves it toward +z).
            Vector2 e = ExitXZ;
            float ty = Tier3Height;
            for (int side = -1; side <= 1; side += 2)
                ProvingKit.Box("CannonCheek", dress, new Vector3(e.x + side * 0.16f, ty + 0.05f, e.y - 0.1f), Quaternion.identity, new Vector3(0.06f, 0.1f, 0.5f), stone, false);
            ProvingKit.Box("CannonBase", dress, new Vector3(e.x, ty + 0.02f, e.y - 0.5f), Quaternion.identity, new Vector3(0.5f, 0.12f, 0.4f), stone, false);
        }
    }
}
