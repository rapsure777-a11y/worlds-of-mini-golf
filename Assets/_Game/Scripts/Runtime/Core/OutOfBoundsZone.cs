using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>Trigger volume (water, sand pit, off the island) that sends the ball back.</summary>
    [RequireComponent(typeof(Collider))]
    public class OutOfBoundsZone : MonoBehaviour
    {
        void Reset() => GetComponent<Collider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            var ball = other.GetComponentInParent<GolfBall>();
            if (!ball || !ball.InPlay) return;
            var hole = HoleController.Active;
            if (hole) hole.BallOutOfBounds(ball);
        }
    }
}
