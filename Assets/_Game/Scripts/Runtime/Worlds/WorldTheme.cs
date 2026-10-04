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
        [Tooltip("Wooden bridge/boardwalk surface for GreenLayout.deckAreas; null falls back to the green material.")]
        public Material deck;
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

        [Header("Audio")]
        [Tooltip("Fallback track for holes not covered by any cluster.")]
        public AudioClip music;
        [Range(0f, 1f)] public float musicVolume = 0.55f;
        [Tooltip("Island/area clusters: one track shared by a range of holes (one, two or three holes each).")]
        public MusicCluster[] musicClusters = new MusicCluster[0];
    }

    /// <summary>A musical environment: one looping track for a contiguous range of holes (1-based, inclusive).</summary>
    [System.Serializable]
    public class MusicCluster
    {
        public string name;
        public int firstHole;
        public int lastHole;
        public AudioClip clip;

        public bool Contains(int holeNumber) => holeNumber >= firstHole && holeNumber <= lastHole;

        /// <summary>The cluster for a hole, or null if none covers it.</summary>
        public static MusicCluster For(MusicCluster[] clusters, int holeNumber)
        {
            if (clusters == null) return null;
            foreach (var c in clusters) if (c != null && c.Contains(holeNumber)) return c;
            return null;
        }
    }
}
