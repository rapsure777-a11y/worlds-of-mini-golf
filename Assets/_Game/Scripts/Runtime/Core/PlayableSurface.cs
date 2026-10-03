using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// Marks a collider the ball may legally come to rest on (greens, fairway pieces).
    /// A ball that stops anywhere else (rail tops, rocks, scenery) is out of bounds.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PlayableSurface : MonoBehaviour
    {
        public static bool IsUnder(Vector3 ballCentre, float radius)
        {
            var hits = Physics.RaycastAll(ballCentre, Vector3.down, radius + 0.03f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
                if (h.collider.GetComponent<PlayableSurface>()) return true;
            return false;
        }
    }
}
