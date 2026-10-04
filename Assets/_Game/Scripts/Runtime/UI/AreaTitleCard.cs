using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// A cinematic title card shown on arriving at a new island cluster (and at the very start): the island's name in large ivory
    /// serif type with a soft drop shadow, the world name above it and the tagline and hole range below, over a dark banner and a
    /// coloured glow, with an ornamental divider that draws outward from the centre. It fades in as the card settles toward the
    /// player, holds, then fades away while drifting upward, with a soft three-note chime (scaled by the effects volume).
    /// It is a world-space card about 3 m away and a little above eye level: never interactive, never on the putting line, and it
    /// does not move with head turns. Data (name, tagline, accent colour) comes from <see cref="IslandCluster"/>; the card follows
    /// <see cref="CourseController.HoleStarted"/>, so golf code never references it. Reusable by every world.
    /// </summary>
    public class AreaTitleCard : MonoBehaviour
    {
        const float CanvasW = 1600f, CanvasH = 560f, UnitScale = 0.0017f;   // about 2.7 m wide
        public const float FadeIn = 1.4f, Hold = 3.8f, FadeOut = 1.6f;

        [SerializeField] CourseController course;
        [SerializeField] VRRig rig;
        [SerializeField] IslandCluster[] clusters = System.Array.Empty<IslandCluster>();
        [SerializeField] float delayAfterArrival = 1.1f;     // lets a fade-from-black finish first

        public bool IsShowing { get; private set; }
        /// <summary>Id of the cluster whose card was last shown (or is showing).</summary>
        public string LastClusterId { get; private set; }
        public float Alpha => m_Group ? m_Group.alpha : 0f;
        public Text TitleText => m_Title;
        public Text TaglineText => m_Tagline;

        Transform m_Root;
        CanvasGroup m_Group;
        Text m_World, m_Title, m_Tagline, m_Range;
        Image m_Glow, m_Divider, m_Banner;
        RectTransform m_DividerRect;
        Coroutine m_Routine;
        AudioClip m_Chime;

        public void Configure(CourseController c, VRRig r, System.Collections.Generic.IEnumerable<IslandCluster> islandClusters)
        {
            course = c; rig = r;
            clusters = new System.Collections.Generic.List<IslandCluster>(islandClusters).ToArray();
        }

        void Awake() => Build();

        bool m_Started;

        void OnEnable() { if (m_Started) Subscribe(); }

        void OnDisable() { if (course) course.HoleStarted -= OnHoleStarted; }

        void Subscribe()
        {
            if (!course) return;
            course.HoleStarted -= OnHoleStarted;
            course.HoleStarted += OnHoleStarted;
        }

        void Start()
        {
            m_Started = true;
            Subscribe();
            // First arrival: the course may have started before we subscribed.
            if (course && course.Current && LastClusterId == null) OnHoleStarted(course, course.Current);
        }

        void OnHoleStarted(CourseController c, HoleController hole)
        {
            var cluster = Find(hole.HoleNumber);
            if (cluster == null || cluster.id == LastClusterId) return;
            Show(cluster, delayAfterArrival);
        }

        IslandCluster Find(int holeNumber)
        {
            foreach (var c in clusters) if (c != null && c.Contains(holeNumber)) return c;
            return null;
        }

        /// <summary>Show the card for a cluster (after an optional delay), replacing any card in progress.</summary>
        public void Show(IslandCluster cluster, float delay = 0f)
        {
            LastClusterId = cluster.id;
            if (m_Routine != null) StopCoroutine(m_Routine);
            m_Routine = StartCoroutine(Play(cluster, delay));
        }

        public static string RangeText(IslandCluster c)
        {
            if (c.holes == null || c.holes.Length == 0) return "";
            return c.holes.Length == 1 ? $"HOLE {c.holes[0]}" : $"HOLES {c.holes[0]} – {c.holes[c.holes.Length - 1]}";
        }

        /// <summary>Wide, letter-spaced capitals for the small lines (a regular space between letters; spaces between words are widened).</summary>
        public static string Spaced(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char ch in s.ToUpperInvariant())
            {
                if (ch == ' ') sb.Append("   ");
                else { sb.Append(ch); sb.Append(' '); }
            }
            return sb.ToString().TrimEnd();
        }

        IEnumerator Play(IslandCluster cluster, float delay)
        {
            IsShowing = true;
            m_Group.alpha = 0f;
            m_Root.gameObject.SetActive(false);
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

            // Content.
            var accent = cluster.accent;
            m_World.text = Spaced(course ? course.CourseName : "Tropical Adventure");
            m_World.color = new Color(accent.r, accent.g, accent.b, 0.95f);
            m_Title.text = cluster.displayName.ToUpperInvariant();
            m_Tagline.text = string.IsNullOrEmpty(cluster.tagline) ? "" : cluster.tagline;
            m_Range.text = Spaced(RangeText(cluster));
            m_Range.color = new Color(accent.r, accent.g, accent.b, 0.95f);
            m_Divider.color = accent;
            m_Glow.color = new Color(accent.r, accent.g, accent.b, 0.42f);

            // Place once, in front of the player at the moment of arrival; it then stays put in the world.
            Place();
            m_Root.gameObject.SetActive(true);
            PlayChime(cluster);

            Vector3 basePos = m_Root.position;
            float total = FadeIn + Hold + FadeOut, t = 0f;
            while (t < total)
            {
                t += Time.unscaledDeltaTime;
                float inK = Ease(t / FadeIn), outK = Ease((total - t) / FadeOut);
                m_Group.alpha = Mathf.Min(inK, outK);
                // Settle in (slightly larger -> normal), then drift upward as it leaves.
                float settle = 1f + 0.07f * (1f - inK);
                m_Root.localScale = Vector3.one * (UnitScale * settle);
                float drift = Mathf.Clamp01((t - FadeIn - Hold) / FadeOut);
                m_Root.position = basePos + Vector3.up * (0.22f * Ease(drift));
                // The divider draws outward from the centre during the fade-in.
                m_DividerRect.localScale = new Vector3(Ease((t - 0.25f) / (FadeIn * 1.1f)), 1f, 1f);
                yield return null;
            }
            m_Group.alpha = 0f;
            m_Root.gameObject.SetActive(false);
            IsShowing = false;
            m_Routine = null;
        }

        void Place()
        {
            var cam = rig && rig.Head ? rig.Head.transform : (Camera.main ? Camera.main.transform : null);
            if (!cam) return;
            Vector3 fwd = cam.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            m_Root.position = cam.position + fwd * 3.1f + Vector3.up * 0.55f;
            m_Root.rotation = Quaternion.LookRotation(fwd);
        }

        static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        // ------------------------------------------------------------------ build

        void Build()
        {
            var canvas = WorldText.CreateCanvas("AreaTitleCanvas", transform, new Vector2(CanvasW, CanvasH), new Color(0, 0, 0, 0));
            m_Root = canvas.transform;
            m_Root.localScale = Vector3.one * UnitScale;
            m_Group = canvas.gameObject.AddComponent<CanvasGroup>();
            m_Group.interactable = false; m_Group.blocksRaycasts = false;

            m_Glow = Pic(m_Root, "Glow", TitleArt.Glow, new Rect(150f, -60f, CanvasW - 300f, CanvasH + 120f));
            m_Banner = Pic(m_Root, "Banner", TitleArt.Banner, new Rect(0f, 60f, CanvasW, 360f));
            m_Banner.color = new Color(0.02f, 0.05f, 0.07f, 1f);

            m_World = Line("World", new Rect(0f, 100f, CanvasW, 46f), 36, FontStyle.Normal, TextAnchor.MiddleCenter);
            m_Title = Line("Title", new Rect(0f, 150f, CanvasW, 150f), 128, FontStyle.Bold, TextAnchor.MiddleCenter);
            m_Title.color = new Color(1f, 0.97f, 0.9f, 1f);
            Shadowed(m_Title, 5f, 0.7f);
            m_Divider = Pic(m_Root, "Divider", TitleArt.Divider, new Rect(0f, 304f, 960f, 40f));
            m_DividerRect = m_Divider.rectTransform;
            // Centre-anchored and centre-pivoted so scaling it in x grows it outward from the middle.
            m_DividerRect.anchorMin = m_DividerRect.anchorMax = new Vector2(0.5f, 1f);
            m_DividerRect.pivot = new Vector2(0.5f, 1f);
            m_DividerRect.anchoredPosition = new Vector2(0f, -304f);
            m_Tagline = Line("Tagline", new Rect(0f, 346f, CanvasW, 66f), 50, FontStyle.Italic, TextAnchor.MiddleCenter);
            m_Tagline.color = new Color(1f, 0.97f, 0.9f, 0.9f);
            Shadowed(m_Tagline, 3f, 0.6f);
            m_Range = Line("Range", new Rect(0f, 414f, CanvasW, 40f), 30, FontStyle.Normal, TextAnchor.MiddleCenter);

            m_Root.gameObject.SetActive(false);
            m_Chime = MakeChime();
        }

        Text Line(string name, Rect r, int size, FontStyle style, TextAnchor anchor)
        {
            var t = WorldText.CreateCell(m_Root, name, r, size, Color.white);
            t.font = TitleArt.Serif;
            t.fontStyle = style;
            t.alignment = anchor;
            t.raycastTarget = false;
            return t;
        }

        static void Shadowed(Text t, float dist, float alpha)
        {
            var s = t.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, alpha);
            s.effectDistance = new Vector2(dist, -dist);
        }

        static Image Pic(Transform parent, string name, Sprite sprite, Rect r)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(r.x, -r.y);
            rt.sizeDelta = new Vector2(r.width, r.height);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        // ------------------------------------------------------------------ chime

        void PlayChime(IslandCluster cluster)
        {
            float vol = 0.5f * GolfAudio.SfxVolume;
            if (m_Chime && vol > 0.001f && m_Root) AudioSource.PlayClipAtPoint(m_Chime, m_Root.position, vol);
        }

        /// <summary>A gentle rising major arpeggio (C-E-G-C) of soft bell tones with long decays.</summary>
        static AudioClip MakeChime()
        {
            const int rate = 44100;
            const float length = 2.4f;
            int n = (int)(rate * length);
            var data = new float[n];
            float[] freqs = { 523.25f, 659.25f, 783.99f, 1046.5f };
            for (int k = 0; k < freqs.Length; k++)
            {
                int start = (int)(rate * 0.16f * k);
                for (int i = start; i < n; i++)
                {
                    float t = (i - start) / (float)rate;
                    float env = Mathf.Exp(-t * 2.4f) * Mathf.Clamp01(t * 120f);
                    float tone = Mathf.Sin(2f * Mathf.PI * freqs[k] * t) + 0.35f * Mathf.Sin(2f * Mathf.PI * freqs[k] * 2f * t) + 0.12f * Mathf.Sin(2f * Mathf.PI * freqs[k] * 3.01f * t);
                    data[i] += tone * env * 0.18f;
                }
            }
            var clip = AudioClip.Create("area_chime", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
