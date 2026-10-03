using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// Solid scenery (terrain, sand, rocks, water bed): a ball that touches it is out of bounds
    /// straight away, like leaving the carpet in real mini golf. Players can still walk and teleport on it.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class OutOfBoundsSurface : MonoBehaviour
    {
        void OnCollisionEnter(Collision collision)
        {
            var ball = collision.collider.GetComponentInParent<GolfBall>();
            if (!ball || !ball.InPlay) return;
            var hole = HoleController.Active;
            if (hole) hole.BallOutOfBounds(ball);
        }
    }
}
