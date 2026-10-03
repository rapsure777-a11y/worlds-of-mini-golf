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
                origin = new Vector3(0f, GreenElevation, 0f),
                yaw = 0f,
            };
        }
    }
}
