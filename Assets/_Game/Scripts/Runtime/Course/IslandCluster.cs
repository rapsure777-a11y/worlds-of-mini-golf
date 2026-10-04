using System;
using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// An environmental cluster of one to three holes sharing an island (or part of one), a musical theme and a look.
    /// The nine-hole Tropical Adventure is a handful of these; the grouping is data, not code, so it can change.
    /// World-agnostic: every world defines its own clusters.
    /// </summary>
    [Serializable]
    public class IslandCluster
    {
        public string id;
        public string displayName;
        /// <summary>Hole numbers (1-based) that belong to this cluster, in play order.</summary>
        public int[] holes = Array.Empty<int>();
        /// <summary>In-project music file name without extension (Assets/_Game/Audio/Music/{musicName}.ogg).</summary>
        public string musicName;
        /// <summary>Island centre and nominal radius in world XZ.</summary>
        public Vector2 centre;
        public float radius;
        [Header("Title card (shown on arrival)")]
        /// <summary>One line of atmosphere under the island's name.</summary>
        public string tagline;
        /// <summary>Colour of the card's ornaments and small caps (the title itself stays ivory).</summary>
        public Color accent = new Color(1f, 0.82f, 0.45f);

        public bool Contains(int holeNumber) => Array.IndexOf(holes, holeNumber) >= 0;
        public string MusicAssetPath => string.IsNullOrEmpty(musicName) ? null : $"Assets/_Game/Audio/Music/{musicName}.ogg";
    }
}
