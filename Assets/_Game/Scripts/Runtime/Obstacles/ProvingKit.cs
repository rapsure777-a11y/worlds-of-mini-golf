using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>Surface materials for the proving-ground pieces. Any field may be null (the primitive's default material is used).</summary>
    [System.Serializable]
    public class ProvingMaterials
    {
        public Material green, wall, cup, flag, wood, metal, tee, cupRim;

        public static ProvingMaterials FromTheme(WorldTheme theme)
        {
            var m = new ProvingMaterials();
            if (!theme) return m;
            m.green = theme.green; m.wall = theme.wall; m.cup = theme.cup; m.flag = theme.flag; m.tee = theme.tee; m.cupRim = theme.cupRim;
            m.wood = theme.deck ? theme.deck : theme.wall;
            return m;
        }
    }

    /// <summary>
    /// Small shared helpers for the obstacle proving ground: collider boxes and cylinders with the golf physics material, flags, and the
    /// hole wrapper (tee, player start, <see cref="HoleController"/>) that <see cref="HoleFactory"/> builds for rectangle-union greens.
    /// Functional pieces carry colliders; decorative pieces never do.
    /// </summary>
    public static class ProvingKit
    {
        /// <summary>A cube at a local pose. Functional boxes get the zero-friction course material; decorative boxes get no collider.</summary>
        public static GameObject Box(string name, Transform parent, Vector3 localPos, Quaternion localRot, Vector3 size, Material mat,
            bool collider = true, bool playable = false, bool outOfBounds = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = size;
            if (mat) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var col = go.GetComponent<Collider>();
            if (!collider) { Object.DestroyImmediate(col); return go; }
            col.sharedMaterial = GolfMaterials.Course;
            if (playable) go.AddComponent<PlayableSurface>();
            if (outOfBounds) go.AddComponent<OutOfBoundsSurface>();
            return go;
        }

        /// <summary>A decorative cylinder whose axis is the local Z axis of <paramref name="localRot"/> (rotated so Unity's Y-axis cylinder lies along Z).</summary>
        public static GameObject AxialCylinder(string name, Transform parent, Vector3 localPos, Quaternion localRot, float radius, float length, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot * Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
            if (mat) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>Destroys a generated child safely in both edit mode and play mode.</summary>
        public static void Discard(GameObject go)
        {
            if (!go) return;
            go.name = "_discarded";
            go.SetActive(false);
            if (Application.isPlaying) Object.Destroy(go); else Object.DestroyImmediate(go);
        }

        /// <summary>A pole and cloth for a cup that has no flag of its own. The flag hides when a ball is close (see <see cref="Cup"/>).</summary>
        public static GameObject MakeFlag(Transform cup, Material mat, float height = 0.3f)
        {
            var flag = new GameObject("Flag");
            flag.transform.SetParent(cup, false);
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            Object.DestroyImmediate(pole.GetComponent<Collider>());
            pole.transform.SetParent(flag.transform, false);
            pole.transform.localScale = new Vector3(0.006f, height * 0.5f, 0.006f);
            pole.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            var cloth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cloth.name = "Cloth";
            Object.DestroyImmediate(cloth.GetComponent<Collider>());
            cloth.transform.SetParent(flag.transform, false);
            cloth.transform.localScale = new Vector3(0.07f, 0.05f, 0.004f);
            cloth.transform.localPosition = new Vector3(0.035f, height * 0.9f, 0f);
            if (mat) { pole.GetComponent<MeshRenderer>().sharedMaterial = mat; cloth.GetComponent<MeshRenderer>().sharedMaterial = mat; }
            return flag;
        }

        /// <summary>
        /// Wraps a built hole in a <see cref="HoleController"/> like <see cref="HoleFactory.Build"/> does: a tee, a player start behind
        /// it looking at the cup, and a kill plane <paramref name="killDepth"/> below the hole origin. <paramref name="teeLocal"/> is on
        /// the surface, in <paramref name="root"/> space.
        /// </summary>
        public static HoleController AttachHole(GameObject root, int number, int par, Vector3 teeLocal, Cup cup, GolfTuning tuning,
            GolfBall ball, Material teeMat = null, float killDepth = 3f)
        {
            var tee = new GameObject("Tee").transform;
            tee.SetParent(root.transform, false);
            tee.localPosition = teeLocal;
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "TeeMat";
            Object.DestroyImmediate(disc.GetComponent<Collider>());
            disc.transform.SetParent(tee, false);
            disc.transform.localPosition = new Vector3(0f, 0.0015f, 0f);
            disc.transform.localScale = new Vector3(0.14f, 0.0015f, 0.14f);
            if (teeMat) disc.GetComponent<MeshRenderer>().sharedMaterial = teeMat;

            Vector3 cupLocal = cup ? root.transform.InverseTransformPoint(cup.transform.position) : teeLocal + Vector3.forward;
            Vector3 line = cupLocal - teeLocal; line.y = 0f;
            line = line.sqrMagnitude > 1e-6f ? line.normalized : Vector3.forward;
            var start = new GameObject("PlayerStart").transform;
            start.SetParent(root.transform, false);
            start.localPosition = teeLocal - line * 0.9f;
            start.localRotation = Quaternion.LookRotation(line);

            var hole = root.AddComponent<HoleController>();
            hole.Configure(tuning, number, par, tee, start, cup, ball, root.transform.position.y - killDepth);
            return hole;
        }
    }
}
