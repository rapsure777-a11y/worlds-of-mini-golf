using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>Turns a <see cref="HoleDefinition"/> into a playable hole. Usable at edit time and runtime.</summary>
    public static class HoleFactory
    {
        public static HoleController Build(HoleDefinition def, WorldTheme theme, GolfTuning tuning, Transform parent, GolfBall ball)
        {
            var root = new GameObject($"Hole{def.number:00}_{def.name.Replace(" ", "")}");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = def.origin;
            root.transform.localRotation = Quaternion.Euler(0f, def.yaw, 0f);

            CourseGeometry.CreateGreen("Green", def.layout, tuning, root.transform,
                theme ? theme.green : null, theme ? theme.cup : null, theme ? theme.wall : null, theme ? theme.flag : null,
                theme ? theme.deck : null, out Cup cup);

            float teeH = def.layout.Height(def.tee.x, def.tee.y);
            var tee = new GameObject("Tee").transform;
            tee.SetParent(root.transform, false);
            tee.localPosition = new Vector3(def.tee.x, teeH, def.tee.y);

            var mat = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mat.name = "TeeMat";
            Object.DestroyImmediate(mat.GetComponent<Collider>());
            mat.transform.SetParent(tee, false);
            mat.transform.localPosition = new Vector3(0f, 0.0015f, 0f);
            mat.transform.localScale = new Vector3(0.14f, 0.0015f, 0.14f);
            if (theme && theme.tee) mat.GetComponent<MeshRenderer>().sharedMaterial = theme.tee;

            // Player starts behind the tee looking down the line toward the cup.
            Vector3 cupLocal = cup ? cup.transform.localPosition : tee.localPosition + Vector3.forward;
            Vector3 line = cupLocal - tee.localPosition; line.y = 0f; line.Normalize();
            var start = new GameObject("PlayerStart").transform;
            start.SetParent(root.transform, false);
            start.localPosition = tee.localPosition - line * 0.9f;
            start.localRotation = Quaternion.LookRotation(line);

            var hole = root.AddComponent<HoleController>();
            float killY = root.transform.position.y - 3f;
            hole.Configure(tuning, def.number, def.par, tee, start, cup, ball, killY);
            return hole;
        }
    }
}
