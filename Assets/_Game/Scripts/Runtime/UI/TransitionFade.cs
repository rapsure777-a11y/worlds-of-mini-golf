using System.Collections;
using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// Fades the view to black and back around a teleport that changes island cluster (themed transition), so moving between
    /// islands is not a hard cut. Within a cluster nothing happens. Uses a small unlit quad in front of the camera.
    /// </summary>
    public class TransitionFade : MonoBehaviour
    {
        [SerializeField] Camera cam;
        [SerializeField] Material fadeMaterial;
        [SerializeField] IslandCluster[] clusters = System.Array.Empty<IslandCluster>();
        [SerializeField] float fadeOut = 0.5f, fadeIn = 0.9f;

        CourseController m_Course;
        Renderer m_Quad;
        Color m_Color = Color.black;
        string m_LastCluster;
        float m_Alpha;
        int m_Version;

        public float Alpha => m_Alpha;

        public void Configure(CourseController course, Camera camera, Material material, System.Collections.Generic.IEnumerable<IslandCluster> islandClusters)
        {
            m_Course = course; cam = camera; fadeMaterial = material;
            clusters = new System.Collections.Generic.List<IslandCluster>(islandClusters).ToArray();
        }

        void Start()
        {
            if (!m_Course) m_Course = FindFirstObjectByType<CourseController>();
            if (!cam) cam = Camera.main;
            if (!m_Course || !cam || !fadeMaterial) { enabled = false; return; }
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "TransitionFadeQuad";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, cam.nearClipPlane + 0.12f);
            go.transform.localScale = new Vector3(3f, 3f, 1f);
            m_Quad = go.GetComponent<Renderer>();
            m_Quad.sharedMaterial = new Material(fadeMaterial);
            m_Quad.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_Quad.receiveShadows = false;
            SetAlpha(0f);
            m_Course.HoleFinished += OnFinished;
            m_Course.HoleStarted += OnStarted;
        }

        void OnDestroy()
        {
            if (m_Course) { m_Course.HoleFinished -= OnFinished; m_Course.HoleStarted -= OnStarted; }
        }

        string Cluster(int holeNumber)
        {
            foreach (var c in clusters) if (c != null && c.Contains(holeNumber)) return c.id;
            return null;
        }

        void OnFinished(CourseController c, HoleController hole)
        {
            int next = c.CurrentIndex + 1;
            if (next >= c.Holes.Length) return;
            string a = Cluster(hole.HoleNumber), b = Cluster(c.Holes[next].HoleNumber);
            if (a == null || b == null || a == b) return;
            StartCoroutine(FadeOutBeforeTeleport(c.AdvanceDelay));
        }

        IEnumerator FadeOutBeforeTeleport(float advanceDelay)
        {
            int v = ++m_Version;
            yield return new WaitForSeconds(Mathf.Max(0f, advanceDelay - fadeOut - 0.1f));
            if (v != m_Version) yield break;
            yield return Fade(1f, fadeOut, v);
        }

        void OnStarted(CourseController c, HoleController hole)
        {
            string cl = Cluster(hole.HoleNumber);
            if (m_LastCluster != null && cl != null && cl != m_LastCluster && m_Alpha > 0f)
                StartCoroutine(FadeInAfterTeleport());
            else if (m_Alpha > 0f) { m_Version++; SetAlpha(0f); }
            if (cl != null) m_LastCluster = cl;
        }

        IEnumerator FadeInAfterTeleport()
        {
            int v = ++m_Version;
            yield return Fade(0f, fadeIn, v);
        }

        IEnumerator Fade(float to, float seconds, int version)
        {
            float from = m_Alpha, t = 0f;
            while (t < seconds && version == m_Version)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(t / seconds)));
                yield return null;
            }
            if (version == m_Version) SetAlpha(to);
        }

        void SetAlpha(float a)
        {
            m_Alpha = a;
            if (!m_Quad) return;
            m_Quad.enabled = a > 0.001f;
            var c = m_Color; c.a = a;
            var m = m_Quad.sharedMaterial;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        }
    }
}
