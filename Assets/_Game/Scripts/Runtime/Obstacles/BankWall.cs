using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// Angled and sloped walls for holes whose greens are rectangle unions (whose own rails are axis-aligned): corner kickers, funnel guides,
    /// rails that follow a ramp. A wall is a box collider laid along a straight segment between two points on the surface, in the hole's own
    /// space, so it can be at any angle and can climb a ramp. The ball needs no special handling: the existing wall rebound uses the real
    /// contact normal (outgoing angle = the model's, e.g. a 45 degree hit leaves at about 37.5 degrees from the wall).
    /// The wall is functional (collider) and carries no <see cref="PlayableSurface"/>, so a ball that comes to rest on its top is out of bounds
    /// like on any rail. Decorative skins go on separate, collider-free objects.
    /// </summary>
    public static class BankWall
    {
        public const float DefaultThickness = 0.06f, DefaultHeight = 0.14f, DefaultSkirt = 0.1f;

        /// <summary>
        /// One straight wall piece from <paramref name="a"/> to <paramref name="b"/> (points on the surface, hole space; they may differ in height).
        /// The wall stands <paramref name="height"/> above the line and is sunk <paramref name="skirt"/> below it so there is no crack at the foot.
        /// It is centred on the line and extended by half its thickness at both ends so consecutive pieces close their corners.
        /// </summary>
        public static GameObject Segment(Transform parent, Vector3 a, Vector3 b, Material mat, float height = DefaultHeight,
            float thickness = DefaultThickness, float skirt = DefaultSkirt, string name = "BankWall")
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 1e-3f) return null;
            Quaternion rot = Quaternion.LookRotation(d / len, Vector3.up);
            Vector3 up = rot * Vector3.up;
            Vector3 centre = (a + b) * 0.5f + up * ((height - skirt) * 0.5f);
            return ProvingKit.Box(name, parent, centre, rot, new Vector3(thickness, height + skirt, len + thickness), mat, true);
        }

        /// <summary>A chain of segments through <paramref name="points"/> (a curved bank approximated by short facets, a funnel, a zig-zag).</summary>
        public static GameObject Polyline(Transform parent, Vector3[] points, Material mat, float height = DefaultHeight,
            float thickness = DefaultThickness, float skirt = DefaultSkirt, string name = "BankWall")
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            for (int i = 0; i + 1 < points.Length; i++)
                Segment(group.transform, points[i], points[i + 1], mat, height, thickness, skirt, $"{name}_{i}");
            return group;
        }

        /// <summary>
        /// Normal (unit, in the XZ plane) of the face of a segment that looks toward <paramref name="from"/>. Handy for tests and for choosing which
        /// way a kicker turns a ball: the ball's outgoing direction is its incoming direction reflected about this normal.
        /// </summary>
        public static Vector2 FaceNormal(Vector2 a, Vector2 b, Vector2 from)
        {
            Vector2 d = (b - a).normalized;
            var n = new Vector2(-d.y, d.x);
            Vector2 mid = (a + b) * 0.5f;
            return Vector2.Dot(n, from - mid) >= 0f ? n : -n;
        }

        /// <summary>The model's outgoing direction when a ball moving along <paramref name="dir"/> meets a wall with normal <paramref name="normal"/> (tangent kept, normal reflected).</summary>
        public static Vector2 Reflect(Vector2 dir, Vector2 normal, float tangentKeep = 0.94f, float bounce = 0.72f)
        {
            float vn = Vector2.Dot(dir, normal);
            Vector2 tangent = dir - vn * normal;
            return tangent * tangentKeep - vn * bounce * normal;
        }
    }
}
