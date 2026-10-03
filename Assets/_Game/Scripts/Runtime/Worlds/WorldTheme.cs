using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// Look of one nine-hole world. Golf systems never reference this; only the course
    /// builder and set dressing do, so a new world is new data, not new mechanics.
    /// </summary>
    [CreateAssetMenu(menuName = "Gamebreak/World Theme", fileName = "WorldTheme")]
    public class WorldTheme : ScriptableObject
    {
        public string worldName = "World";
        [TextArea] public string inspiration;

        [Header("Course surfaces")]
        public Material green;
        public Material wall;
        public Material cup;
        public Material flag;
        public Material tee;

        [Header("Ball and putter")]
        public Material ball;
        public Material putterShaft;
        public Material putterHead;
        public Material putterGrip;

        [Header("Environment")]
        public Material ground;
        public Material water;
        public Material skybox;
        public Color sunColor = Color.white;
        public float sunIntensity = 1.3f;
        public Vector3 sunEuler = new Vector3(50f, -30f, 0f);
        public Color fogColor = new Color(0.7f, 0.85f, 0.95f);
        public float fogDensity = 0.004f;

        [Header("UI and effects")]
        public Material teleportLine;
    }
}
