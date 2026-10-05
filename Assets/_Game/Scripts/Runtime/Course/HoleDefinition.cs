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
        /// <summary>
        /// Optional pieces that hang off the lane (a bowl, a ramp's pit...). Called by <see cref="HoleFactory"/> with the hole's root; it builds
        /// the pieces and returns the hole's cup (the layout then has no cup of its own). Null for ordinary rectangle-union holes.
        /// </summary>
        public System.Func<Transform, WorldTheme, GolfTuning, Cup> buildExtras;
        /// <summary>Footprints (layout XZ) of those pieces, so the terrain plateau and the foliage keep-out cover them as well as the lane.</summary>
        public readonly List<Rect> extraAreas = new List<Rect>();
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
                Hole04(),
                Hole05(),
                Hole06(),
            };
        }

        public const string StartCluster = "start", JungleCluster = "jungle", TempleCluster = "temple";

        /// <summary>
        /// The archipelago: island clusters with their holes, music and position. Holes 1-2 share the Starting Island,
        /// Holes 3-4 the Jungle Island. Later clusters (Temple, Volcanic, Summit) are added here.
        /// </summary>
        public static List<IslandCluster> Clusters()
        {
            return new List<IslandCluster>
            {
                new IslandCluster { id = StartCluster, displayName = "Starting Island", holes = new[] { 1, 2 }, musicName = "IslandExploration", centre = Vector2.zero, radius = 34f,
                    tagline = "Where every adventure begins", accent = new Color(1f, 0.84f, 0.42f) },
                new IslandCluster { id = JungleCluster, displayName = "Jungle Island", holes = new[] { 3, 4 }, musicName = "JungleTheme", centre = JungleCentre, radius = 26f,
                    tagline = "Deep green, and deeper secrets", accent = new Color(0.62f, 0.95f, 0.5f) },
                // Planned clusters: music is imported, islands and holes are built in later checkpoints/milestones (centre/radius unset).
                new IslandCluster { id = TempleCluster, displayName = "Temple Island", holes = new[] { 5, 6 }, musicName = "TempleTheme", centre = TempleCentre, radius = 24f,
                    tagline = "Ruins older than the tide", accent = new Color(0.98f, 0.8f, 0.48f) },
                new IslandCluster { id = "volcanic", displayName = "Volcanic Island", holes = new[] { 7, 8 }, musicName = "VolcanicTheme",
                    tagline = "Where the island still breathes fire", accent = new Color(1f, 0.55f, 0.3f) },
                new IslandCluster { id = "summit", displayName = "Summit Sanctuary", holes = new[] { 9 }, musicName = "SummitTheme",
                    tagline = "The whole archipelago at your feet", accent = new Color(0.82f, 0.93f, 1f) },
            };
        }

        /// <summary>World XZ of the Jungle Island's centre: south-east of the Starting Island across a ~30 m channel.</summary>
        public static readonly Vector2 JungleCentre = new Vector2(64f, -62f);

        /// <summary>World XZ of the Temple Island's centre: east of the Jungle Island across a ~20 m channel.</summary>
        public static readonly Vector2 TempleCentre = new Vector2(130f, -38f);

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
        /// Hole 4, "Hollow Drop" (par 3): the valley hole, shortened. Headset feedback on the first version (a five-leg S, par 4) was "too
        /// long, too many turns". Now: a short lane with the 24 cm drop (the ball arrives at the ramp already moving), a flat run-up, the
        /// launch ramp over a pit, and a jump into a small roulette bowl (offset so the ball enters at an angle) with the cup at its
        /// lowest point. A soft putt rolls back; a short jump falls in the pit (one stroke, back to the last rest spot); a ball that lands
        /// in the bowl cannot leave it. The bowl is built by <see cref="HoleDefinition.buildExtras"/>. Local axes: x across the lane, z along it.
        /// </summary>
        static HoleDefinition Hole04()
        {
            var spec = Hole4Spec();
            var def = new HoleDefinition
            {
                number = 4,
                name = "Hollow Drop",
                par = 3,
                layout = spec.LaneLayout(),
                tee = new Vector2(0f, 0.6f),
                origin = new Vector3(69f, Hole4Height, -80f),
                yaw = 0f,
                cluster = JungleCluster,
                buildExtras = (root, theme, tuning) => ProvingGround.BuildJumpBowlPieces(root, tuning, ProvingMaterials.FromTheme(theme), spec),
            };
            def.extraAreas.Add(spec.BowlFootprint);
            return def;
        }

        /// <summary>The numbers behind Hole 4: lane 1.2 m wide, a drop of 24 cm, a 1 m run-up, the 14 degree ramp, and a 1.6 m bowl entered at an angle.</summary>
        public static JumpBowlSpec Hole4Spec()
        {
            var s = new JumpBowlSpec { dropHeight = 0.24f, dropStartZ = 1.4f, dropEndZ = 4.0f, entryOffsetX = 0.5f, entryHalfAngle = 26f };
            s.ramp.width = 1.2f;
            s.ramp.approachLength = 5.0f;     // flat run-up from the end of the drop (z 4.0) to the ramp (z 5.0)
            s.bowl.radius = 1.6f;
            s.bowl.shelfWidth = 0.45f;
            return s;
        }

        /// <summary>Ground height of the Hole 4 plateau on the Jungle Island.</summary>
        public const float Hole4Height = 2.4f;

        /// <summary>Ground height of the Hole 5 plateau (its lower terrace) on the Temple Island.</summary>
        public const float Hole5Height = 2.0f;
        /// <summary>Ground height of the Hole 6 plateau (the tee pad and intake lane) on the Temple Island.</summary>
        public const float Hole6Height = 2.2f;

        /// <summary>
        /// Hole 5, "The Sun Stair" (par 4): a ziggurat of three terraces on the Temple Island, built by <see cref="SunStairSpec"/> (one rectangle-union
        /// green with a height function, angled <see cref="BankWall"/> kickers and rails, and a launch jump that can skip the second ramp).
        /// Local axes: x across the stair, z up it.
        /// </summary>
        static HoleDefinition Hole05()
        {
            var spec = Hole5Spec();
            return new HoleDefinition
            {
                number = 5,
                name = "The Sun Stair",
                par = 4,
                layout = spec.BuildLayout(),
                tee = spec.Tee,
                origin = new Vector3(118f, Hole5Height, -46f),
                yaw = 0f,
                cluster = TempleCluster,
                buildExtras = (root, theme, tuning) => spec.BuildPieces(root, ProvingMaterials.FromTheme(theme)),
            };
        }

        /// <summary>The numbers behind Hole 5 (all defaults of <see cref="SunStairSpec"/>).</summary>
        public static SunStairSpec Hole5Spec() => new SunStairSpec();

        /// <summary>
        /// Hole 6, "The Waterwheel Mill" (par 3): tee, an intake lane to a dock, the proven waterwheel lifts the ball to an elevated aqueduct, and a final
        /// green. A long gentle mill road beside the lane climbs to the same green for a player who skips the wheel. Built by <see cref="MillHoleSpec"/>.
        /// Local axes: x across, z along the lane.
        /// </summary>
        static HoleDefinition Hole06()
        {
            var spec = Hole6Spec();
            float r = GolfTuning.Default.ballRadius;
            return new HoleDefinition
            {
                number = 6,
                name = "The Waterwheel Mill",
                par = 3,
                layout = spec.BuildLayout(r),
                tee = spec.Tee,
                origin = new Vector3(141f, Hole6Height, -44f),
                yaw = 0f,
                cluster = TempleCluster,
                buildExtras = (root, theme, tuning) => spec.BuildPieces(root, tuning, ProvingMaterials.FromTheme(theme)),
            };
        }

        /// <summary>The numbers behind Hole 6: the proven wheel, a bucket every 2 s, a 3 m aqueduct and a 2 x 3 m final green.</summary>
        public static MillHoleSpec Hole6Spec() => new MillHoleSpec();

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
