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
            };
        }

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
