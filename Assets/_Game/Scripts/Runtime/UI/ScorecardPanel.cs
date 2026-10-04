using UnityEngine;
using UnityEngine.UI;

namespace Gamebreak.MiniGolf
{
    /// <summary>Scorecard grid (hole / par / score per hole plus total). The rig toggles it and keeps it in front of the player.</summary>
    public class ScorecardPanel : MonoBehaviour
    {
        const float Width = 1000f, Height = 690f, LabelW = 130f, TotalW = 110f, RowH = 62f, Top = 90f;

        [SerializeField] CourseController course;

        Text m_Title, m_Footer;
        Text[] m_Holes, m_Pars, m_Scores;
        Text m_ParTotal, m_ScoreTotal;
        Transform m_Canvas;
        int m_Columns = -1;

        public void Configure(CourseController c) => course = c;

        void Awake()
        {
            m_Canvas = WorldText.CreateCanvas("ScorecardCanvas", transform, new Vector2(Width, Height), new Color(0.98f, 0.95f, 0.85f, 0.95f)).transform;
            var ink = new Color(0.15f, 0.12f, 0.1f);
            m_Title = WorldText.CreateCell(m_Canvas, "Title", new Rect(0, 15, Width, 60), 40, ink);
            m_Footer = WorldText.CreateCell(m_Canvas, "Footer", new Rect(0, Top + RowH * 3 + 10, Width, 60), 34, ink);
            // Music / effects volume sliders below the card (touch with a controller, hold the trigger).
            gameObject.AddComponent<ScorecardVolumeControls>().Build(m_Canvas, Height);
        }

        void Build(int n)
        {
            m_Columns = n;
            var ink = new Color(0.15f, 0.12f, 0.1f);
            float colW = (Width - LabelW - TotalW - 40f) / Mathf.Max(1, n);
            string[] labels = { "Hole", "Par", "Score" };
            for (int r = 0; r < 3; r++)
            {
                var label = WorldText.CreateCell(m_Canvas, labels[r], new Rect(20, Top + r * RowH, LabelW, RowH), 32, ink);
                label.fontStyle = FontStyle.Bold;
                label.text = labels[r];
            }
            m_Holes = new Text[n]; m_Pars = new Text[n]; m_Scores = new Text[n];
            for (int i = 0; i < n; i++)
            {
                float x = 20 + LabelW + i * colW;
                m_Holes[i] = WorldText.CreateCell(m_Canvas, "H" + i, new Rect(x, Top, colW, RowH), 32, ink);
                m_Pars[i] = WorldText.CreateCell(m_Canvas, "P" + i, new Rect(x, Top + RowH, colW, RowH), 32, ink);
                m_Scores[i] = WorldText.CreateCell(m_Canvas, "S" + i, new Rect(x, Top + 2 * RowH, colW, RowH), 36, ink);
            }
            float tx = Width - TotalW - 20;
            var tot = WorldText.CreateCell(m_Canvas, "TotLabel", new Rect(tx, Top, TotalW, RowH), 32, ink);
            tot.fontStyle = FontStyle.Bold;
            tot.text = "Tot";
            m_ParTotal = WorldText.CreateCell(m_Canvas, "ParTot", new Rect(tx, Top + RowH, TotalW, RowH), 32, ink);
            m_ScoreTotal = WorldText.CreateCell(m_Canvas, "ScoreTot", new Rect(tx, Top + 2 * RowH, TotalW, RowH), 36, ink);
            m_ScoreTotal.fontStyle = FontStyle.Bold;
        }

        void OnEnable()
        {
            if (!course) return;
            course.ScorecardChanged += Refresh;
            Refresh(course);
        }

        void OnDisable() { if (course) course.ScorecardChanged -= Refresh; }

        void Refresh(CourseController c)
        {
            var card = c.Card;
            if (card == null) return;
            int n = card.par.Length;
            if (m_Columns != n) Build(n);
            m_Title.text = $"<b>{c.CourseName}</b>";
            int parTotal = 0;
            for (int i = 0; i < n; i++)
            {
                bool current = i == c.CurrentIndex && !c.Finished;
                m_Holes[i].text = current ? $"<color=#1E6FD9><b>{i + 1}</b></color>" : (i + 1).ToString();
                m_Pars[i].text = card.par[i].ToString();
                parTotal += card.par[i];
                int s = card.strokes[i];
                m_Scores[i].text = s <= 0 ? "-" : s < card.par[i] ? $"<color=#1E8A3A>{s}</color>" : s > card.par[i] ? $"<color=#B33A2A>{s}</color>" : s.ToString();
            }
            m_ParTotal.text = parTotal.ToString();
            m_ScoreTotal.text = card.TotalStrokes.ToString();
            int diff = card.TotalStrokes - card.TotalParPlayed;
            string rel = diff == 0 ? "even" : diff > 0 ? "+" + diff : diff.ToString();
            m_Footer.text = c.Finished ? $"<b>Course complete: {rel}</b>" : card.TotalParPlayed > 0 ? $"To par: {rel}" : "";
        }
    }
}
