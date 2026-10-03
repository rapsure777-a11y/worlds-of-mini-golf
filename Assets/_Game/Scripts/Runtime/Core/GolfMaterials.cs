using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// Shared physics materials. Friction and bounce are zero because <see cref="GolfBall"/>
    /// computes rolling resistance and rebounds itself.
    /// </summary>
    public static class GolfMaterials
    {
        static PhysicsMaterial s_Ball, s_Course;

        public static PhysicsMaterial Ball => s_Ball ? s_Ball : (s_Ball = Make("GolfBall"));
        public static PhysicsMaterial Course => s_Course ? s_Course : (s_Course = Make("GolfCourse"));

        static PhysicsMaterial Make(string name) => new PhysicsMaterial(name)
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            bounciness = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Minimum,
        };
    }
}
