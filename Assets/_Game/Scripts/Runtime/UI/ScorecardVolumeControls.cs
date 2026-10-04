using UnityEngine;
using UnityEngine.UI;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// Music and effects volume sliders on the scorecard (X). In VR, touch a slider with the tip of a controller and hold the
    /// trigger to drag it; haptic ticks confirm the touch and each 5% step, and the effects slider plays a short blip when released.
    /// The card holds still while a hand is near it (<see cref="VRRig.ScorecardPinned"/>). On desktop the keys are F5/F6 (music) and
    /// F7/F8 (effects). Values live in <see cref="GolfAudio"/>: the music slider is the audible music level (the world's level, 0.55 by
    /// default, times the user scale), the effects slider the effects volume.
    /// </summary>
    public class ScorecardVolumeControls : MonoBehaviour
    {
        /// <summary>Layout in canvas units, measured from the card's top-left corner (1000 units = 1 m).</summary>
        public const float CanvasWidth = 1000f, TrackLeft = 250f, TrackRight = 810f, RowHeight = 50f;
        public static readonly float[] RowTop = { 408f, 474f };
        const float TouchDepth = 80f, DragDepth = 160f, HoverMargin = 28f;
        const float PinDistance = 0.5f;

        [SerializeField] VRRig rig;

        RectTransform m_Canvas;
        float m_CanvasHeight;
        readonly Image[] m_Fill = new Image[2], m_Handle = new Image[2];
        readonly Text[] m_Value = new Text[2];
        int m_Drag = -1, m_Hover = -1;
        Transform m_HoverHand;
        float m_LastStep = -1f;
        float m_BaseLevel = 0.55f;
        AudioClip m_Blip;
        Transform m_DragHand;

        public int DraggingRow => m_Drag;

        // ------------------------------------------------------------------ mapping (pure, tested)

        /// <summary>Slider fraction (0..1) for a canvas x position.</summary>
        public static float Fraction(float canvasX) => Mathf.Clamp01((canvasX - TrackLeft) / (TrackRight - TrackLeft));

        public static float MusicFractionOf(float musicScale) => Mathf.Clamp01(musicScale / GolfAudio.MaxMusicScale);
        public static float MusicScaleOf(float fraction) => Mathf.Clamp01(fraction) * GolfAudio.MaxMusicScale;

        /// <summary>The percentage shown for music: the audible level (world level x user scale), capped at 100.</summary>
        public static int MusicPercent(float baseLevel, float musicScale) => Mathf.RoundToInt(Mathf.Clamp01(baseLevel * musicScale) * 100f);

        // ------------------------------------------------------------------ build

        public void Build(Transform canvas, float canvasHeight)
        {
            m_Canvas = (RectTransform)canvas;
            m_CanvasHeight = canvasHeight;
            var ink = new Color(0.15f, 0.12f, 0.1f);
            Bar(canvas, "SettingsRule", new Rect(30f, RowTop[0] - 24f, CanvasWidth - 60f, 3f), new Color(0.15f, 0.12f, 0.1f, 0.25f));
            string[] names = { "Music", "Effects" };
            for (int r = 0; r < 2; r++)
            {
                float y = RowTop[r];
                var label = WorldText.CreateCell(canvas, names[r] + "Label", new Rect(20f, y, 215f, RowHeight), 34, ink);
                label.alignment = TextAnchor.MiddleLeft; label.fontStyle = FontStyle.Bold; label.text = names[r];
                Bar(canvas, names[r] + "Track", new Rect(TrackLeft, y + RowHeight * 0.5f - 7f, TrackRight - TrackLeft, 14f), new Color(0.15f, 0.12f, 0.1f, 0.22f));
                m_Fill[r] = Bar(canvas, names[r] + "Fill", new Rect(TrackLeft, y + RowHeight * 0.5f - 7f, 1f, 14f), new Color(0.12f, 0.55f, 0.75f, 0.95f));
                m_Handle[r] = Bar(canvas, names[r] + "Handle", new Rect(TrackLeft - 16f, y + RowHeight * 0.5f - 22f, 32f, 44f), new Color(0.98f, 0.98f, 0.95f, 1f));
                var hr = (RectTransform)m_Handle[r].transform;
                hr.pivot = new Vector2(0.5f, 0.5f);
                hr.anchoredPosition = new Vector2(TrackLeft, -(y + RowHeight * 0.5f));
                m_Value[r] = WorldText.CreateCell(canvas, names[r] + "Value", new Rect(TrackRight + 20f, y, 150f, RowHeight), 34, ink);
                m_Value[r].alignment = TextAnchor.MiddleLeft;
            }
            var hint = WorldText.CreateCell(canvas, "SettingsHint", new Rect(0f, RowTop[1] + RowHeight + 6f, CanvasWidth, 36f), 24, new Color(0.15f, 0.12f, 0.1f, 0.6f));
            hint.text = "Touch a slider with your controller and hold the trigger to drag";
            Refresh();
        }

        static Image Bar(Transform parent, string name, Rect r, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(r.x, -r.y);
            rt.sizeDelta = new Vector2(r.width, r.height);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        void Awake()
        {
            m_Blip = MakeBlip();
        }

        void OnEnable()
        {
            if (!rig) rig = FindFirstObjectByType<VRRig>();
            var mp = FindFirstObjectByType<MusicPlayer>();
            if (mp) m_BaseLevel = mp.Volume;
            GolfAudio.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            GolfAudio.Changed -= Refresh;
            if (rig) rig.ScorecardPinned = false;
            m_Drag = m_Hover = -1;
        }

        // ------------------------------------------------------------------ interaction

        void Update()
        {
            if (!rig || !rig.XRActive || m_Canvas == null) return;
            bool pin = false, touching = false;
            Touch(rig.DominantHand, ref pin, ref touching);
            Touch(rig.OffHand, ref pin, ref touching);
            if (!touching && m_Drag >= 0) EndDrag();
            rig.ScorecardPinned = pin;
        }

        void Touch(Transform hand, ref bool pin, ref bool touching)
        {
            if (!hand) return;
            Vector3 tip = hand.position + hand.forward * 0.08f;
            if (Vector3.Distance(tip, m_Canvas.position) < PinDistance) pin = true;
            // While dragging only the dragging hand counts, so the other hand cannot steal the slider.
            if (m_Drag >= 0 && hand != m_DragHand) return;
            if (ProcessTouch(tip, rig.IsTriggerPressed(hand), hand)) touching = true;
        }

        /// <summary>
        /// Feeds one fingertip (world position) and the trigger state. Returns true while this fingertip is touching or dragging a
        /// slider. Public so tests and other input paths can drive it.
        /// </summary>
        public bool ProcessTouch(Vector3 tipWorld, bool trigger, Transform hand = null)
        {
            if (m_Canvas == null) return false;
            Vector3 local = m_Canvas.InverseTransformPoint(tipWorld);
            float cx = local.x + CanvasWidth * 0.5f, cy = m_CanvasHeight * 0.5f - local.y;
            bool dragging = m_Drag >= 0;
            float depth = dragging ? DragDepth : TouchDepth;
            int row = -1;
            if (Mathf.Abs(local.z) < depth)
                for (int r = 0; r < 2; r++)
                {
                    float margin = dragging ? 60f : HoverMargin;
                    bool inY = cy > RowTop[r] - margin && cy < RowTop[r] + RowHeight + margin;
                    bool inX = cx > TrackLeft - 40f && cx < TrackRight + 40f;
                    if (dragging ? r == m_Drag && inY : inY && inX) { row = r; break; }
                }
            // Hover belongs to the hand that started it, so the idle hand cannot clear it.
            if (row >= 0 && row != m_Hover)
            {
                m_Hover = row; m_HoverHand = hand;
                if (hand && rig) rig.HapticFor(hand, 0.2f, 0.02f);
                UpdateHandles();
            }
            else if (row < 0 && m_Hover >= 0 && hand == m_HoverHand)
            {
                m_Hover = -1; m_HoverHand = null;
                UpdateHandles();
            }
            if (row < 0) return false;
            if (!trigger) { if (dragging) EndDrag(); return true; }
            if (!dragging) { m_Drag = row; m_DragHand = hand; m_LastStep = -1f; }
            SetFraction(row, Fraction(cx));
            float step = Mathf.Round(Fraction(cx) * 20f);
            if (!Mathf.Approximately(step, m_LastStep))
            {
                m_LastStep = step;
                if (hand && rig) rig.HapticFor(hand, 0.12f, 0.012f);
            }
            return true;
        }

        void EndDrag()
        {
            int row = m_Drag;
            m_Drag = -1; m_DragHand = null;
            UpdateHandles();
            if (row == 1 && m_Blip) AudioSource.PlayClipAtPoint(m_Blip, m_Canvas.position, 0.7f * GolfAudio.SfxVolume);
        }

        /// <summary>Set a slider directly (0..1). Row 0 = music, row 1 = effects.</summary>
        public void SetFraction(int row, float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            if (row == 0) GolfAudio.MusicScale = MusicScaleOf(fraction);
            else GolfAudio.SfxVolume = fraction;
        }

        public float GetFraction(int row) => row == 0 ? MusicFractionOf(GolfAudio.MusicScale) : GolfAudio.SfxVolume;

        /// <summary>World position of a point on a slider's track (fraction 0..1), for tests and tutorials.</summary>
        public Vector3 TrackWorldPoint(int row, float fraction)
        {
            float cx = Mathf.Lerp(TrackLeft, TrackRight, fraction), cy = RowTop[row] + RowHeight * 0.5f;
            return m_Canvas.TransformPoint(new Vector3(cx - CanvasWidth * 0.5f, m_CanvasHeight * 0.5f - cy, 0f));
        }

        // ------------------------------------------------------------------ visuals

        void Refresh()
        {
            if (m_Canvas == null) return;
            for (int r = 0; r < 2; r++)
            {
                float f = GetFraction(r);
                float w = Mathf.Max(1f, f * (TrackRight - TrackLeft));
                ((RectTransform)m_Fill[r].transform).sizeDelta = new Vector2(w, 14f);
                var h = (RectTransform)m_Handle[r].transform;
                h.anchoredPosition = new Vector2(TrackLeft + f * (TrackRight - TrackLeft), h.anchoredPosition.y);
                m_Value[r].text = r == 0 ? MusicPercent(m_BaseLevel, GolfAudio.MusicScale) + "%" : Mathf.RoundToInt(GolfAudio.SfxVolume * 100f) + "%";
            }
            UpdateHandles();
        }

        void UpdateHandles()
        {
            for (int r = 0; r < 2; r++)
            {
                if (m_Handle[r] == null) continue;
                bool active = r == m_Drag, hover = r == m_Hover;
                m_Handle[r].color = active ? new Color(1f, 0.78f, 0.25f) : hover ? new Color(0.75f, 0.95f, 1f) : new Color(0.98f, 0.98f, 0.95f);
                m_Handle[r].rectTransform.localScale = Vector3.one * (active ? 1.2f : hover ? 1.1f : 1f);
            }
        }

        /// <summary>A short soft two-tone blip so the effects level can be judged on release.</summary>
        static AudioClip MakeBlip()
        {
            const int rate = 44100;
            int n = (int)(rate * 0.16f);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Exp(-t * 22f) * Mathf.Clamp01(t * 400f);
                data[i] = (Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 1320f * t) * 0.3f) * env * 0.5f;
            }
            var clip = AudioClip.Create("volume_blip", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
