using System.Collections.Generic;
using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>Code-defined hole: layout, tee, par and where it sits in the world.</summary>
    public class HoleDefinition
    {
        public int number;
        public string name;
        public int par;
        public GreenLayout layout;
        /// <summary>Tee position in layout coordinates (XZ).</summary>
        public Vector2 tee;
        /// <summary>World position of the layout origin.</summary>
        public Vector3 origin;
        /// <summary>Rotation of the hole about Y, degrees.</summary>
        public float yaw;
        /// <summary>Id of the <see cref="IslandCluster"/> this hole belongs to.</summary>
        public string cluster;
    }

    /// <summary>World 1: Tropical Adventure. Holes are added here as they are designed.</summary>
    public static class TropicalCourse
    {
        public const string Name = "Tropical Adventure";
        public const float GreenElevation = 0.15f;

        public static List<HoleDefinition> Holes()
        {
            return new List<HoleDefinition>
            {
                Hole01(),
                Hole02(),
                Hole03(),
            };
        }

        public const string StartCluster = "start", JungleCluster = "jungle", TempleCluster = "temple";

        /// <summary>
        /// The archipelago: island clusters with their holes, music and position. Holes 1-2 share the Starting Island,
        /// Holes 3-4 the Jungle Island (Hole 4 arrives in the next checkpoint). Later clusters (Temple, Volcanic, Summit) are added here.
        /// </summary>
        public static List<IslandCluster> Clusters()
        {
            return new List<IslandCluster>
            {
                new IslandCluster { id = StartCluster, displayName = "Starting Island", holes = new[] { 1, 2 }, musicName = "IslandExploration", centre = Vector2.zero, radius = 34f },
                new IslandCluster { id = JungleCluster, displayName = "Jungle Island", holes = new[] { 3, 4 }, musicName = "JungleTheme", centre = JungleCentre, radius = 26f },
                // Planned clusters: music is imported, islands and holes are built in later checkpoints/milestones (centre/radius unset).
                new IslandCluster { id = TempleCluster, displayName = "Temple Island", holes = new[] { 5, 6 }, musicName = "TempleTheme" },
                new IslandCluster { id = "volcanic", displayName = "Volcanic Island", holes = new[] { 7, 8 }, musicName = "VolcanicTheme" },
                new IslandCluster { id = "summit", displayName = "Summit Sanctuary", holes = new[] { 9 }, musicName = "SummitTheme" },
            };
        }

        /// <summary>World XZ of the Jungle Island's centre: south-east of the Starting Island across a ~30 m channel.</summary>
        public static readonly Vector2 JungleCentre = new Vector2(64f, -62f);

        public static IslandCluster ClusterOf(int holeNumber)
        {
            foreach (var c in Clusters()) if (c.Contains(holeNumber)) return c;
            return null;
        }

        /// <summary>
        /// Hole 3, "Jungle Crossing" (par 3): a winding lane that crosses a small jungle ravine on a short wooden bridge, which is
        /// part of the putting surface (<see cref="GreenLayout.deckAreas"/>: same collider and rails, plank material).
        /// Tee lane north, turn east across a wide elbow, over the bridge (a slight hump), onto a landing pad, then north into a
        /// short lane to the cup. The cup is not in line from the landing pad: aim straight up from the pad's east side, or bank
        /// the ball off the lane's east rail. The lane tilts a little toward the east rail for a small read. Bridge rails are
        /// raised to 12 cm, and a ball that does leave the course costs the usual one stroke and returns to its last rest spot.
        /// Local axes: x across the first lane, z along it.
        /// </summary>
        static HoleDefinition Hole03()
        {
            var l = new GreenLayout { wallHeight = 0.12f };
            l.Area(-0.6f, 0f, 1.2f, 3.4f);        // A: tee lane
            l.Area(-0.6f, 3.4f, 4.0f, 2.4f);      // B: wide elbow, x -0.6..3.4
            l.Area(3.4f, 4.0f, 3.6f, 1.2f);       // bridge, x 3.4..7.0
            l.Area(7.0f, 3.4f, 3.0f, 3.0f);       // D: landing pad, x 7..10
            l.Area(8.8f, 6.4f, 1.2f, 2.8f);       // final lane to the cup
            l.deckAreas.Add(new Rect(3.4f, 4.0f, 3.6f, 1.2f));
            l.cup = new Vector2(9.4f, 8.5f);
            l.height = (x, z) =>
            {
                // Hump bridge: crest 0.18 m at x = 5.2 (fades over 2.4 m each side): a ball needs about 1.6 m/s to crest it.
                float t = Mathf.Clamp01(1f - Mathf.Abs(x - 5.2f) / 2.4f);
                float hump = 0.18f * t * t * (3f - 2f * t);
                // Final lane leans toward the east rail (2.5% cross-slope), blended in over the first 0.8 m of the lane.
                float lane = Mathf.Clamp01((z - 6.4f) / 0.8f);
                float lean = Slopes.RampX(x, 8.8f, 10.0f, 0f, -0.03f) * lane * lane * (3f - 2f * lane);
                return hump + lean;
            };
            return new HoleDefinition
            {
                number = 3,
                name = "Jungle Crossing",
                par = 3,
                layout = l,
                tee = new Vector2(0f, 0.6f),
                // On the Jungle Island's west side: the lane starts running east (away from the Starting Island), the bridge crosses to the south.
                origin = new Vector3(46f, JungleDeckHeight, -56f),
                yaw = 90f,
                cluster = JungleCluster,
            };
        }

        /// <summary>Ground height of the Hole 3 plateau on the Jungle Island.</summary>
        public const float JungleDeckHeight = 2.6f;

        /// <summary>
        /// Hole 2, "Palm Corner" (DRAFT greybox, pending HQ creative approval). A left-hand dogleg:
        /// lay up to the corner, or bank off the far rail. A low mound guards the inside of the turn
        /// and the green drops gently toward the cup. Mainly here so hole-to-hole progression can be
        /// tested in the headset.
        /// </summary>
        static HoleDefinition Hole02()
        {
            var l = new GreenLayout();
            l.Area(-0.6f, 0f, 1.2f, 5.2f);     // tee lane, running +Z
            l.Area(-3.6f, 4.0f, 4.2f, 1.2f);   // cross lane to the left at the far end
            var mound = new Vector2(-0.15f, 4.35f);
            l.height = (x, z) =>
                Slopes.Mound(x, z, mound, 0.45f, 0.05f)
                + Slopes.RampX(x, -1.2f, -2.6f, 0f, -0.04f);
            l.cup = new Vector2(-3.0f, 4.6f);
            return new HoleDefinition
            {
                number = 2,
                name = "Palm Corner",
                cluster = StartCluster,
                par = 3,
                layout = l,
                tee = new Vector2(0f, 0.6f),
                origin = new Vector3(5.5f, 0.75f, -22f),
                yaw = 0f,
            };
        }

        /// <summary>
        /// Hole 1, "Beach Warm-up". Milestone 1 test hole: a straight 7 m lane with one
        /// gentle rise, so strike feel, roll-out and the cup can be judged in isolation.
        /// </summary>
        static HoleDefinition Hole01()
        {
            var l = new GreenLayout();
            l.Area(-0.6f, 0f, 1.2f, 7f);
            l.height = (x, z) => Slopes.RampZ(z, 2.4f, 3.4f, 0f, 0.08f);
            l.cup = new Vector2(0f, 6.2f);
            return new HoleDefinition
            {
                number = 1,
                name = "Beach Warm-up",
                cluster = StartCluster,
                par = 2,
                layout = l,
                tee = new Vector2(0f, 0.6f),
                // Runs along the south beach (lagoon on the right, cliffs on the left).
                origin = new Vector3(-3.5f, 0.5f, -27f),
                yaw = 90f,
            };
        }
    }
}
