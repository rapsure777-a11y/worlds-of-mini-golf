using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>Applies global physics settings the golf simulation depends on.</summary>
    public static class GolfPhysicsBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init() => Apply(GolfTuning.Default);

        public static void Apply(GolfTuning tuning)
        {
            Time.fixedDeltaTime = 1f / Mathf.Max(30, tuning.physicsRate);
            Time.maximumDeltaTime = 0.1f;
            // Let slow taps still rebound off rails instead of sticking.
            Physics.bounceThreshold = 0.05f;
            // The ball is tiny; the default 1 cm contact offset makes it hover and catch on seams.
            Physics.defaultContactOffset = 0.002f;
            Physics.defaultSolverIterations = 8;
            Physics.defaultSolverVelocityIterations = 4;
        }
    }
}
