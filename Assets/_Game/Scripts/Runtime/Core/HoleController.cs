using System;
using System.Collections;
using UnityEngine;

namespace Gamebreak.MiniGolf
{
    /// <summary>
    /// Rules for one hole: tee placement, stroke counting, out-of-bounds returns and completion.
    /// Knows nothing about the art around it, so every world reuses it unchanged.
    /// </summary>
    public class HoleController : MonoBehaviour
    {
        public static HoleController Active { get; private set; }

        [SerializeField] GolfTuning tuning;
        [SerializeField] int holeNumber = 1;
        [SerializeField] int par = 2;
        [Tooltip("Ball spawn point. The ball is placed one radius above it.")]
        [SerializeField] Transform tee;
        [Tooltip("Where the player is moved when the hole starts.")]
        [SerializeField] Transform playerStart;
        [SerializeField] Cup cup;
        [SerializeField] GolfBall ball;
        [Tooltip("A ball below this world height is out of bounds.")]
        [SerializeField] float killY = -2f;
        [SerializeField] float returnDelay = 0.6f;

        public GolfTuning Tuning => tuning ? tuning : GolfTuning.Default;
        public int HoleNumber => holeNumber;
        public int Par => par;
        public Transform Tee => tee;
        public Transform PlayerStart => playerStart ? playerStart : tee;
        public Cup Cup => cup;
        public GolfBall Ball => ball;
        public int Strokes { get; private set; }
        public bool IsComplete { get; private set; }
        public Vector3 LastRestPosition { get; private set; }

        public event Action<HoleController> StrokesChanged;
        public event Action<HoleController> Completed;
        /// <summary>Raised when the ball is sent back (out of bounds or player reset).</summary>
        public event Action<HoleController, bool> BallReturned;

        Coroutine m_Return;

        public void Configure(GolfTuning t, int number, int holePar, Transform teeT, Transform start, Cup c, GolfBall b, float kill)
        {
            tuning = t; holeNumber = number; par = holePar; tee = teeT; playerStart = start;
            cup = c; ball = b; killY = kill;
        }

        void OnDisable()
        {
            Unhook();
            if (Active == this) Active = null;
        }

        /// <summary>Make this the active hole and put the ball on the tee.</summary>
        public void BeginHole(GolfBall golfBall = null)
        {
            if (golfBall) ball = golfBall;
            if (Active && Active != this) Active.Unhook();
            Active = this;
            Hook();
            if (cup) cup.Track(ball);
            Strokes = 0;
            IsComplete = false;
            LastRestPosition = TeePosition;
            ball.PlaceAt(LastRestPosition);
            StrokesChanged?.Invoke(this);
        }

        public Vector3 TeePosition => tee.position + Vector3.up * (ball ? ball.Radius : 0.02f) * 1.05f;

        void Hook()
        {
            ball.Struck -= OnStruck; ball.Struck += OnStruck;
            ball.Stopped -= OnStopped; ball.Stopped += OnStopped;
            if (cup) { cup.BallHoled -= OnHoled; cup.BallHoled += OnHoled; }
        }

        void Unhook()
        {
            if (cup) cup.BallHoled -= OnHoled;
            if (!ball) return;
            ball.Struck -= OnStruck;
            ball.Stopped -= OnStopped;
        }

        void OnStruck(GolfBall b, Vector3 v)
        {
            if (IsComplete || Active != this) return;
            Strokes++;
            StrokesChanged?.Invoke(this);
        }

        void OnStopped(GolfBall b)
        {
            if (IsComplete || Active != this || !b.InPlay) return;
            if (!PlayableSurface.IsUnder(b.Position, b.Radius))
            {
                BallOutOfBounds(b); // came to rest on a rail top, rock or other scenery
                return;
            }
            LastRestPosition = b.Position;
            if (Strokes >= Tuning.strokeLimit) Finish(Tuning.strokeLimit);
        }

        void OnHoled(Cup c, GolfBall b)
        {
            if (b != ball || IsComplete || Active != this) return;
            Finish(Strokes);
        }

        void Finish(int strokes)
        {
            Strokes = strokes;
            IsComplete = true;
            ball.InPlay = false;
            StrokesChanged?.Invoke(this);
            Completed?.Invoke(this);
        }

        void FixedUpdate()
        {
            if (Active != this || IsComplete || !ball || !ball.InPlay) return;
            if (ball.Position.y < killY) { BallOutOfBounds(ball); return; }

            // Stuck-ball safeguard: a ball that creeps or rattles for too long is stopped where it is.
            m_MovingTime = ball.IsAtRest ? 0f : m_MovingTime + Time.fixedDeltaTime;
            if (m_MovingTime > maxRollSeconds && ball.SurfaceSpeed < 0.3f)
            {
                m_MovingTime = 0f;
                ball.ForceStop();
            }
        }

        [Tooltip("A ball still moving slowly after this many seconds is stopped where it is.")]
        [SerializeField] float maxRollSeconds = 20f;
        float m_MovingTime;

        public void BallOutOfBounds(GolfBall b)
        {
            if (b != ball || IsComplete || !b.InPlay) return;
            b.InPlay = false;
            Strokes += Tuning.outOfBoundsPenalty;
            StrokesChanged?.Invoke(this);
            ReturnBall(true);
        }

        /// <summary>Player-requested reset to the last resting spot. No penalty.</summary>
        public void RequestReset()
        {
            if (IsComplete || !ball) return;
            ball.InPlay = false;
            ReturnBall(false);
        }

        void ReturnBall(bool outOfBounds)
        {
            if (m_Return != null) StopCoroutine(m_Return);
            m_Return = StartCoroutine(ReturnRoutine(outOfBounds));
        }

        IEnumerator ReturnRoutine(bool outOfBounds)
        {
            if (outOfBounds && returnDelay > 0f) yield return new WaitForSeconds(returnDelay);
            ball.PlaceAt(LastRestPosition);
            m_Return = null;
            BallReturned?.Invoke(this, outOfBounds);
            if (Strokes >= Tuning.strokeLimit) Finish(Tuning.strokeLimit);
        }
    }
}
