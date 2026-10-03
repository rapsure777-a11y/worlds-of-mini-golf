using System;
using System.Collections;
using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>Anything that can move the player (the VR rig, the desktop debug rig).</summary>
    public interface IPlayerMover
    {
        void TeleportTo(Vector3 feetPosition, Vector3 facing);
    }

    [Serializable]
    public class Scorecard
    {
        public int[] par;
        public int[] strokes; // 0 = not played yet

        public Scorecard(int holes)
        {
            par = new int[holes];
            strokes = new int[holes];
        }

        public int TotalStrokes { get { int s = 0; foreach (var x in strokes) s += x; return s; } }

        public int TotalParPlayed
        {
            get { int s = 0; for (int i = 0; i < par.Length; i++) if (strokes[i] > 0) s += par[i]; return s; }
        }

        public static string ResultName(int strokes, int par)
        {
            if (strokes == 1) return "Hole in one!";
            switch (strokes - par)
            {
                case <= -3: return "Albatross!";
                case -2: return "Eagle!";
                case -1: return "Birdie!";
                case 0: return "Par";
                case 1: return "Bogey";
                case 2: return "Double bogey";
                default: return $"+{strokes - par}";
            }
        }
    }

    /// <summary>Runs a sequence of holes (one nine-hole world) and keeps the scorecard.</summary>
    public class CourseController : MonoBehaviour
    {
        [SerializeField] string courseName = "Tropical Adventure";
        [SerializeField] HoleController[] holes = Array.Empty<HoleController>();
        [SerializeField] GolfBall ball;
        [SerializeField] MonoBehaviour playerMover;
        [SerializeField] float advanceDelay = 3f;
        [SerializeField] bool startOnAwake = true;
        [Tooltip("Hole index to start at, for testing a single hole.")]
        [SerializeField] int startHole = 0;

        public string CourseName => courseName;
        public HoleController[] Holes => holes;
        public int CurrentIndex { get; private set; } = -1;
        public HoleController Current => CurrentIndex >= 0 && CurrentIndex < holes.Length ? holes[CurrentIndex] : null;
        public Scorecard Card { get; private set; }
        public bool Finished { get; private set; }

        public event Action<CourseController> ScorecardChanged;
        public event Action<CourseController, HoleController> HoleStarted;
        public event Action<CourseController, HoleController> HoleFinished;
        public event Action<CourseController> CourseFinished;

        public IPlayerMover Mover => playerMover as IPlayerMover;
        public float AdvanceDelay { get => advanceDelay; set => advanceDelay = value; }
        public bool StartOnAwake { get => startOnAwake; set => startOnAwake = value; }

        Coroutine m_Advance;

        public void Configure(string name, HoleController[] holeList, GolfBall golfBall, MonoBehaviour mover)
        {
            courseName = name; holes = holeList; ball = golfBall; playerMover = mover;
        }

        void Start()
        {
            if (startOnAwake) StartCourse(startHole);
        }

        public void StartCourse(int firstHole = 0)
        {
            Card = new Scorecard(holes.Length);
            for (int i = 0; i < holes.Length; i++) Card.par[i] = holes[i].Par;
            Finished = false;
            StartHole(Mathf.Clamp(firstHole, 0, holes.Length - 1), true);
        }

        public void StartHole(int index, bool movePlayer)
        {
            if (m_Advance != null) { StopCoroutine(m_Advance); m_Advance = null; }
            if (Current) Current.Completed -= OnHoleCompleted;
            CurrentIndex = index;
            var hole = Current;
            hole.Completed += OnHoleCompleted;
            hole.BeginHole(ball);
            if (movePlayer && Mover != null)
            {
                Vector3 toCup = hole.Cup ? hole.Cup.transform.position - hole.PlayerStart.position : hole.PlayerStart.forward;
                Mover.TeleportTo(hole.PlayerStart.position, toCup);
            }
            HoleStarted?.Invoke(this, hole);
            ScorecardChanged?.Invoke(this);
        }

        public void NextHole()
        {
            if (CurrentIndex + 1 < holes.Length) StartHole(CurrentIndex + 1, true);
        }

        public void RestartHole() => StartHole(CurrentIndex, true);

        void OnHoleCompleted(HoleController hole)
        {
            Card.strokes[CurrentIndex] = hole.Strokes;
            HoleFinished?.Invoke(this, hole);
            ScorecardChanged?.Invoke(this);
            m_Advance = StartCoroutine(AdvanceRoutine());
        }

        IEnumerator AdvanceRoutine()
        {
            yield return new WaitForSeconds(advanceDelay);
            m_Advance = null;
            if (CurrentIndex + 1 < holes.Length) NextHole();
            else
            {
                Finished = true;
                CourseFinished?.Invoke(this);
                ScorecardChanged?.Invoke(this);
            }
        }
    }
}
