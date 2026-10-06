using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// Makes the lava alive (Graphics Pass 3B): the crust texture drifts slowly and the glow breathes. It only moves the lava material's texture offset and emission
    /// strength (two property writes per frame, no allocation), so it costs nothing in VR. Purely visual.
    /// </summary>
    public class LavaFlow : MonoBehaviour
    {
        [SerializeField] Material lava;
        [SerializeField] Vector2 drift = new Vector2(0.012f, 0.007f);
        [SerializeField] Color emission = new Color(0.85f, 0.26f, 0.05f);
        [SerializeField, Range(0f, 0.6f)] float pulse = 0.28f;
        static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");

        public void Configure(Material m, Color emissive, Vector2? driftSpeed = null) { lava = m; emission = emissive; if (driftSpeed.HasValue) drift = driftSpeed.Value; }

        void Update()
        {
            if (!lava) return;
            float t = Time.time;
            lava.SetTextureOffset(BaseMap, new Vector2(t * drift.x, t * drift.y));
            float k = 1f + pulse * (0.6f * Mathf.Sin(t * 0.9f) + 0.4f * Mathf.Sin(t * 2.3f + 1.7f));
            lava.SetColor(Emission, emission * k);
        }

        void OnDisable()
        {
            if (lava) { lava.SetTextureOffset(BaseMap, Vector2.zero); lava.SetColor(Emission, emission); }
        }
    }
}
