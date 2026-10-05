using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Gamebreak.MiniGolf.Tests
{
    /// <summary>Milestone 3: island clusters, cluster music, and Hole 3 "Jungle Crossing" (layout, bridge physics, scene wiring).</summary>
    public class Milestone3Tests
    {
        [SetUp] public void SetUp() { Time.timeScale = 3f; GolfAudio.ResetToDefaults(); }
        [TearDown] public void TearDown() { Time.timeScale = 1f; GolfAudio.ResetToDefaults(); }

        static IEnumerator WaitUntil(Func<bool> condition, float timeout)
        {
            float end = Time.time + timeout;
            while (!condition() && Time.time < end) yield return new WaitForFixedUpdate();
        }

        static IEnumerator Steps(int n) { for (int i = 0; i < n; i++) yield return new WaitForFixedUpdate(); }

        // ------------------------------------------------------------------ cluster data

        [Test]
        public void Clusters_CoverEveryHoleExactlyOnce_AndMatchHoleTags()
        {
            var holes = TropicalCourse.Holes();
            var clusters = TropicalCourse.Clusters();
            var ids = new HashSet<string>();
            foreach (var c in clusters)
            {
                Assert.IsTrue(ids.Add(c.id), $"duplicate cluster id {c.id}");
                Assert.IsNotEmpty(c.holes, $"cluster {c.id} has no holes");
                Assert.IsFalse(string.IsNullOrEmpty(c.musicName), $"cluster {c.id} has no music name");
                Assert.LessOrEqual(c.holes.Length, 3, $"cluster {c.id} has more than three holes");
            }
            foreach (var h in holes)
            {
                int owners = 0;
                foreach (var c in clusters) if (c.Contains(h.number)) { owners++; Assert.AreEqual(c.id, h.cluster, $"hole {h.number} tag"); }
                Assert.AreEqual(1, owners, $"hole {h.number} must belong to exactly one cluster");
            }
            for (int i = 0; i < holes.Count; i++) Assert.AreEqual(i + 1, holes[i].number, "holes must be numbered in play order");
            // Holes of one cluster are consecutive (no returning to an island).
            foreach (var c in clusters)
                for (int i = 0; i + 1 < c.holes.Length; i++) Assert.AreEqual(c.holes[i] + 1, c.holes[i + 1], $"cluster {c.id} holes not consecutive");
        }

        [Test]
        public void Clusters_ArePlayedInOrder_WithNoReturnToAnEarlierIsland()
        {
            string last = null; var seen = new HashSet<string>();
            foreach (var h in TropicalCourse.Holes())
            {
                if (h.cluster != last) { Assert.IsTrue(seen.Add(h.cluster), $"hole {h.number} returns to island '{h.cluster}'"); last = h.cluster; }
            }
        }

        [Test]
        public void MusicFiles_UseTheImportedInProjectNames()
        {
            var expected = new Dictionary<string, string>
            {
                [TropicalCourse.StartCluster] = "Assets/_Game/Audio/Music/IslandExploration.ogg",
                [TropicalCourse.JungleCluster] = "Assets/_Game/Audio/Music/JungleTheme.ogg",
                [TropicalCourse.TempleCluster] = "Assets/_Game/Audio/Music/TempleTheme.ogg",
                ["volcanic"] = "Assets/_Game/Audio/Music/VolcanicTheme.ogg",
                ["summit"] = "Assets/_Game/Audio/Music/SummitTheme.ogg",
            };
            foreach (var c in TropicalCourse.Clusters())
                if (expected.TryGetValue(c.id, out var path)) Assert.AreEqual(path, c.MusicAssetPath, c.id);
        }

        // ------------------------------------------------------------------ Hole 3 layout

        static HoleDefinition Hole3() => TropicalCourse.Holes().Find(h => h.number == 3);

        /// <summary>Flood fill over the layout's 10 cm grid from the tee to the cup.</summary>
        static bool Connected(GreenLayout l, Vector2 from, Vector2 to, out int cells)
        {
            const float step = 0.1f;
            bool Inside(int i, int j)
            {
                var p = new Vector2(i * step + step * 0.5f, j * step + step * 0.5f);
                foreach (var r in l.areas) if (r.Contains(p)) return true;
                return false;
            }
            var start = new Vector2Int(Mathf.FloorToInt(from.x / step), Mathf.FloorToInt(from.y / step));
            var goal = new Vector2Int(Mathf.FloorToInt(to.x / step), Mathf.FloorToInt(to.y / step));
            var seen = new HashSet<Vector2Int> { start };
            var queue = new Queue<Vector2Int>(); queue.Enqueue(start);
            cells = 0;
            while (queue.Count > 0)
            {
                var c = queue.Dequeue(); cells++;
                if (c == goal) return true;
                foreach (var d in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
                {
                    var n = c + d;
                    if (!seen.Contains(n) && Inside(n.x, n.y)) { seen.Add(n); queue.Enqueue(n); }
                }
            }
            return false;
        }

        [Test]
        public void Hole3_IsAParThreeWithAConnectedLaneFromTeeToCup()
        {
            var def = Hole3();
            Assert.IsNotNull(def, "hole 3 is not defined");
            Assert.AreEqual(3, def.par);
            Assert.AreEqual(TropicalCourse.JungleCluster, def.cluster);
            Assert.IsTrue(def.layout.cup.HasValue);
            Assert.IsTrue(Connected(def.layout, def.tee, def.layout.cup.Value, out _), "tee and cup are not connected by playable surface");
        }

        [Test]
        public void Hole3_Bridge_IsPartOfTheSurface_AndWideEnoughForVR()
        {
            var l = Hole3().layout;
            Assert.AreEqual(1, l.deckAreas.Count, "expected one bridge deck");
            var deck = l.deckAreas[0];
            Assert.GreaterOrEqual(Mathf.Min(deck.width, deck.height), 1.2f - 1e-4f, "bridge narrower than Hole 1's lane");
            // The deck must lie wholly inside the playable areas (it is the putting surface, not decoration).
            foreach (var corner in new[] { new Vector2(deck.xMin + 0.01f, deck.yMin + 0.01f), new Vector2(deck.xMax - 0.01f, deck.yMax - 0.01f), deck.center })
            {
                bool inside = false;
                foreach (var r in l.areas) if (r.Contains(corner)) inside = true;
                Assert.IsTrue(inside, $"deck point {corner} is not playable");
            }
            // Walkable on both sides: the deck touches playable area at both ends.
            Assert.IsTrue(Connected(l, new Vector2(deck.xMin - 0.3f, deck.center.y), new Vector2(deck.xMax + 0.3f, deck.center.y), out _), "bridge does not link its two banks");
        }

        [Test]
        public void Hole3_LaneHeightHasNoCliffs()
        {
            var l = Hole3().layout;
            float maxStep = 0f;
            for (float x = -0.5f; x < 10f; x += 0.1f)
            for (float z = 0.05f; z < 9.2f; z += 0.1f)
            {
                float h = l.Height(x, z);
                maxStep = Mathf.Max(maxStep, Mathf.Abs(l.Height(x + 0.1f, z) - h), Mathf.Abs(l.Height(x, z + 0.1f) - h));
            }
            // 10 cm cells: a 0.04 m step is a 40% slope, far beyond the hump (about 19%) and the cross-slope (2.5%).
            Assert.Less(maxStep, 0.04f, $"height jump of {maxStep:F3} m between neighbouring cells");
        }

        [Test]
        public void Hole3_Bridge_HumpIsModest()
        {
            var l = Hole3().layout;
            float crest = l.Height(5.2f, 4.6f), bank = l.Height(2.5f, 4.6f);
            Assert.That(crest - bank, Is.InRange(0.08f, 0.25f), "the bridge hump should be a gentle rise a soft putt can still cross");
        }

        [Test]
        public void Hole3_FinalLane_LeansTowardTheEastRail_ButTheCupTileIsLevelEnough()
        {
            var l = Hole3().layout;
            float west = l.Height(8.9f, 8.0f), east = l.Height(9.9f, 8.0f);
            Assert.Less(east, west, "the final lane should lean toward the east rail");
            Assert.Less(west - east, 0.05f, "cross-slope should stay under about 5%");
        }

        // ------------------------------------------------------------------ Hole 3 physics (isolated)

        /// <summary>Hole 3 built alone at the origin (hole-local = world coordinates), with a ball and no scenery.</summary>
        class Hole3Bed : IDisposable
        {
            public readonly GolfTuning tuning;
            public readonly GolfBall ball;
            public readonly HoleController hole;
            public readonly GreenLayout layout;
            readonly GameObject m_Root;

            public Hole3Bed(int number = 3)
            {
                tuning = ScriptableObject.CreateInstance<GolfTuning>();
                GolfPhysicsBootstrap.Apply(tuning);
                m_Root = new GameObject("Hole3Bed");
                var ballGo = new GameObject("Ball");
                ballGo.transform.SetParent(m_Root.transform);
                ballGo.AddComponent<Rigidbody>();
                ballGo.AddComponent<SphereCollider>();
                ball = ballGo.AddComponent<GolfBall>();
                ball.SetTuning(tuning);
                var src = TropicalCourse.Holes().Find(h => h.number == number);
                layout = src.layout;
                var def = new HoleDefinition { number = number, name = src.name, par = src.par, layout = src.layout, tee = src.tee, origin = Vector3.zero, yaw = 0f, cluster = src.cluster, buildExtras = src.buildExtras };
                hole = HoleFactory.Build(def, null, tuning, m_Root.transform, ball);
                hole.BeginHole(ball);
            }

            public Vector3 Surface(float x, float z) => new Vector3(x, layout.Height(x, z) + ball.Radius + 0.002f, z);

            public void Dispose() { Object.Destroy(m_Root); Object.Destroy(tuning); }
        }

        [UnityTest]
        public IEnumerator Hole3_Bridge_CanBeCrossedWithAFirmPutt()
        {
            using var b = new Hole3Bed();
            yield return Steps(5);
            b.ball.PlaceAt(b.Surface(2.8f, 4.6f));
            yield return Steps(5);
            b.ball.Strike(new Vector3(2.6f, 0f, 0f));
            yield return WaitUntil(() => b.ball.Position.x > 7.4f, 6f);
            Assert.Greater(b.ball.Position.x, 7.4f, $"ball did not reach the landing pad, at {b.ball.Position}");
            Assert.AreEqual(1, b.hole.Strokes, "the crossing should not have cost a penalty stroke");
            Assert.IsTrue(b.ball.InPlay);
        }

        [UnityTest]
        public IEnumerator Hole3_Bridge_RailsKeepAnAngledPuttOnTheDeck()
        {
            foreach (float angle in new[] { -14f, 14f })
            {
                using var b = new Hole3Bed();
                yield return Steps(5);
                b.ball.PlaceAt(b.Surface(2.8f, 4.6f));
                yield return Steps(5);
                b.ball.Strike(Quaternion.Euler(0f, angle, 0f) * new Vector3(3.2f, 0f, 0f));
                yield return Steps(3);
                yield return WaitUntil(() => b.ball.Position.x > 7.2f || b.ball.IsAtRest, 7f);
                Assert.AreEqual(1, b.hole.Strokes, $"angled putt ({angle} deg) left the course and cost a stroke");
                Assert.Greater(b.ball.Position.x, 3.2f, $"ball ({angle} deg) went backwards, at {b.ball.Position}");
                var z = b.ball.Position.z;
                Assert.That(z, Is.InRange(3.4f, 6.5f), $"ball ({angle} deg) ended outside the corridor, z {z:F2}");
            }
        }

        [UnityTest]
        public IEnumerator Hole3_SoftPutt_RollsBackOffTheHump_WithoutLeavingTheCourse()
        {
            using var b = new Hole3Bed();
            yield return Steps(5);
            b.ball.PlaceAt(b.Surface(3.1f, 4.6f));
            yield return Steps(5);
            b.ball.Strike(new Vector3(1.0f, 0f, 0f)); // too soft to crest (needs about 1.6 m/s)
            yield return Steps(3);
            yield return WaitUntil(() => b.ball.IsAtRest, 8f);
            yield return Steps(20);
            Assert.AreEqual(1, b.hole.Strokes, "a soft putt must not cost a penalty");
            Assert.Less(b.ball.Position.x, 6.0f, "a 1.0 m/s putt should not make it over the crest");
            Assert.IsTrue(b.ball.InPlay);
        }

        [UnityTest]
        public IEnumerator Hole3_FinalLane_CupCanBeHoled()
        {
            using var b = new Hole3Bed();
            yield return Steps(5);
            var cup = b.hole.Cup.transform.position;
            b.ball.PlaceAt(new Vector3(cup.x, b.layout.Height(cup.x, cup.z - 0.4f) + b.ball.Radius + 0.002f, cup.z - 0.4f));
            yield return Steps(5);
            Vector3 d = cup - b.ball.Position; d.y = 0f;
            b.ball.Strike(d.normalized * 1.1f);
            yield return WaitUntil(() => b.hole.IsComplete, 8f);
            Assert.IsTrue(b.hole.IsComplete, $"cup not holed, ball at {b.ball.Position}, cup {cup}");
        }

        [UnityTest]
        public IEnumerator Hole3_BankShotOffTheLaneRail_EntersTheFinalLane()
        {
            using var b = new Hole3Bed();
            yield return Steps(5);
            // From the landing pad the cup is not in line: mirror it across the lane's east rail (x = 10) and aim there.
            b.ball.PlaceAt(b.Surface(7.5f, 4.4f));
            yield return Steps(5);
            Vector3 aim = new Vector3(10.6f, 0f, 8.5f) - b.ball.Position; aim.y = 0f;
            b.ball.Strike(aim.normalized * 2.4f);
            yield return Steps(3);
            yield return WaitUntil(() => b.ball.IsAtRest || b.hole.IsComplete, 8f);
            Assert.AreEqual(1, b.hole.Strokes);
            Assert.That(b.hole.IsComplete || b.ball.Position.z > 6.5f, $"the bank shot did not reach the final lane, ball at {b.ball.Position}");
        }

        // ------------------------------------------------------------------ audio settings

        static AudioClip Tone(string name) => AudioClip.Create(name, 22050 * 30, 1, 22050, false);

        [UnityTest]
        public IEnumerator Music_UserScaleMultipliesTheWorldLevel_Live()
        {
            var p = new GameObject("TestMusic").AddComponent<MusicPlayer>();
            p.Configure(new[] { new MusicCluster { name = "A", firstHole = 1, lastHole = 1, clip = Tone("a") } }, null, 0.55f, null);
            p.FadeInSeconds = 0.05f; // check levels right away, not the 4 s in-game fade-in
            yield return null;
            p.PlayFor(1, 0.05f);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(0.55f, p.ActiveSource.volume, 0.01f, "default scale keeps the world's 0.55");
            GolfAudio.MusicScale = 0.5f;
            yield return null; yield return null;
            Assert.AreEqual(0.275f, p.ActiveSource.volume, 0.01f, "scale 0.5 halves it");
            GolfAudio.MusicScale = 10f;
            yield return null; yield return null;
            Assert.AreEqual(Mathf.Clamp01(0.55f * GolfAudio.MaxMusicScale), p.ActiveSource.volume, 0.01f, "scale is capped");
            Object.Destroy(p.gameObject);
        }

        [Test]
        public void GolfAudio_MusicAndEffectsAreIndependentAndClamped()
        {
            Assert.AreEqual(1f, GolfAudio.MusicScale, 1e-4f, "default music scale (keeps the 0.55 world level)");
            Assert.AreEqual(1f, GolfAudio.SfxVolume, 1e-4f);
            GolfAudio.SfxVolume = 0.25f;
            Assert.AreEqual(1f, GolfAudio.MusicScale, 1e-4f, "changing effects must not change music");
            GolfAudio.MusicScale = 5f;
            Assert.AreEqual(GolfAudio.MaxMusicScale, GolfAudio.MusicScale, 1e-4f);
            Assert.AreEqual(0.25f, GolfAudio.SfxVolume, 1e-4f);
            GolfAudio.SfxVolume = -1f;
            Assert.AreEqual(0f, GolfAudio.SfxVolume, 1e-4f);
            GolfAudio.ResetToDefaults();
            Assert.AreEqual(1f, GolfAudio.MusicScale, 1e-4f);
        }

        // ------------------------------------------------------------------ scorecard volume sliders

        [Test]
        public void VolumeSliders_MapFractionsToLevels()
        {
            Assert.AreEqual(0f, ScorecardVolumeControls.Fraction(ScorecardVolumeControls.TrackLeft - 100f), 1e-4f);
            Assert.AreEqual(1f, ScorecardVolumeControls.Fraction(ScorecardVolumeControls.TrackRight + 100f), 1e-4f);
            Assert.AreEqual(0.5f, ScorecardVolumeControls.Fraction((ScorecardVolumeControls.TrackLeft + ScorecardVolumeControls.TrackRight) * 0.5f), 1e-4f);
            Assert.AreEqual(55, ScorecardVolumeControls.MusicPercent(0.55f, 1f), "default scale shows the world's 55%");
            Assert.AreEqual(99, ScorecardVolumeControls.MusicPercent(0.55f, GolfAudio.MaxMusicScale));
            Assert.AreEqual(0, ScorecardVolumeControls.MusicPercent(0.55f, 0f));
            float f = ScorecardVolumeControls.MusicFractionOf(1f);
            Assert.AreEqual(1f, ScorecardVolumeControls.MusicScaleOf(f), 1e-4f, "round trip of the default scale");
        }

        static ScorecardVolumeControls FindControls()
        {
            var all = Object.FindObjectsByType<ScorecardVolumeControls>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return all.Length > 0 ? all[0] : null;
        }

        [UnityTest]
        public IEnumerator Scorecard_HasVolumeSliders_ThatAControllerTipCanDrag()
        {
            SceneManager.LoadScene("TropicalAdventure");
            yield return null;
            yield return null;
            // The scorecard starts hidden and builds its sliders on first show: open it like pressing X.
            Object.FindFirstObjectByType<VRRig>().ToggleScorecard();
            yield return null;
            var c = FindControls();
            Assert.IsNotNull(c, "the scorecard has no volume controls");
            var canvasT = c.GetComponentInChildren<Canvas>(true).transform;

            // Touch the music track at 25% and hold the trigger: music scale follows, effects are untouched.
            c.ProcessTouch(c.TrackWorldPoint(0, 0.25f), true);
            Assert.AreEqual(0.25f * GolfAudio.MaxMusicScale, GolfAudio.MusicScale, 0.02f);
            Assert.AreEqual(0, c.DraggingRow);
            Assert.AreEqual(GolfAudio.DefaultSfx, GolfAudio.SfxVolume, 1e-4f);
            // Dragging along the track keeps following the fingertip, even slightly off the track vertically.
            c.ProcessTouch(c.TrackWorldPoint(0, 0.75f) + canvasT.up * 0.02f, true);
            Assert.AreEqual(0.75f * GolfAudio.MaxMusicScale, GolfAudio.MusicScale, 0.02f);
            c.ProcessTouch(c.TrackWorldPoint(0, 0.75f), false); // release
            Assert.AreEqual(-1, c.DraggingRow);
            float kept = GolfAudio.MusicScale;

            // A fingertip 20 cm in front of the card (not touching) changes nothing, even with the trigger down.
            c.ProcessTouch(c.TrackWorldPoint(1, 0.1f) + canvasT.forward * 0.2f, true);
            Assert.AreEqual(GolfAudio.DefaultSfx, GolfAudio.SfxVolume, 1e-4f, "a hovering hand must not move a slider");
            Assert.AreEqual(kept, GolfAudio.MusicScale, 1e-4f);

            // Effects slider.
            c.ProcessTouch(c.TrackWorldPoint(1, 0.4f), true);
            Assert.AreEqual(0.4f, GolfAudio.SfxVolume, 0.02f);
            c.ProcessTouch(c.TrackWorldPoint(1, 0.4f), false);
            // Touching without the trigger never changes anything.
            c.ProcessTouch(c.TrackWorldPoint(1, 0.9f), false);
            Assert.AreEqual(0.4f, GolfAudio.SfxVolume, 0.02f);
        }

        [UnityTest]
        public IEnumerator Scorecard_PutterHead_HoldsThenSlidesRelatively_WithoutJumping()
        {
            SceneManager.LoadScene("TropicalAdventure");
            yield return null;
            yield return null;
            Object.FindFirstObjectByType<VRRig>().ToggleScorecard();
            yield return null;
            var c = FindControls();
            GolfAudio.MusicScale = 1f;
            float start = c.GetFraction(0);
            // Rest the head on the far-left end of the music track: nothing may jump, even while holding.
            var left = c.TrackWorldPoint(0, 0f);
            for (int i = 0; i < 12; i++) { c.ProcessClub(left); yield return null; }
            Assert.AreEqual(start, c.GetFraction(0), 0.02f, "holding the club at the track end must not throw the slider to 0");
            // The idle hand is processed every frame too; it must not cancel the club's hold.
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 0.5f) { c.ProcessClub(left); c.ProcessTouch(left + Vector3.up * 5f, false); yield return null; }
            c.ProcessClub(left);
            Assert.AreEqual(0, c.DraggingRow, "the club should be dragging the music slider after its hold");
            // Sliding the head 10% of the track to the right moves the slider by about 10%.
            c.ProcessClub(c.TrackWorldPoint(0, 0.1f));
            Assert.AreEqual(start + 0.1f, c.GetFraction(0), 0.03f);
            // Leaving the slider reports "not touching"; the component's Update then ends the drag.
            Assert.IsFalse(c.ProcessClub(c.TrackWorldPoint(0, 0.1f) + Vector3.up * 5f));
        }

        [UnityTest]
        public IEnumerator Scorecard_RestartButtons_RestartHoleAtOnce_AndCourseOnConfirm()
        {
            SceneManager.LoadScene("TropicalAdventure");
            yield return null;
            yield return null;
            Object.FindFirstObjectByType<VRRig>().ToggleScorecard();
            yield return null;
            var c = FindControls();
            var course = Object.FindFirstObjectByType<CourseController>();
            course.StartHole(1, true);
            yield return null;
            c.PressButton(0); // Restart Hole: immediate, stays on hole 2
            Assert.AreEqual(1, course.CurrentIndex);
            c.PressButton(1); // Restart Course: the first press only arms the confirmation
            Assert.AreEqual(1, course.CurrentIndex, "restart course must need a second press");
            c.PressButton(1);
            Assert.AreEqual(0, course.CurrentIndex);
            Assert.IsFalse(course.Finished);
        }

        // ------------------------------------------------------------------ Hole 4 ("Hollow Drop": drop, ramp jump, bowl)

        static HoleDefinition Hole4() => TropicalCourse.Holes().Find(h => h.number == 4);

        [Test]
        public void Hole4_IsAParThreeOnTheJungleIsland_WithALaneAndABowlCup()
        {
            var def = Hole4();
            Assert.IsNotNull(def, "hole 4 is not defined");
            Assert.AreEqual(3, def.par);
            Assert.AreEqual(TropicalCourse.JungleCluster, def.cluster);
            Assert.AreEqual(TropicalCourse.Holes().Find(h => h.number == 3).cluster, def.cluster, "holes 3 and 4 share an island");
            Assert.AreEqual(0, def.layout.deckAreas.Count, "hole 4 has no bridge");
            Assert.IsFalse(def.layout.cup.HasValue, "the cup is in the bowl, not on the lane");
            Assert.IsNotNull(def.buildExtras, "the bowl is built as an extra piece");
            Assert.Greater(def.layout.openEdges.Count, 0, "the ramp lip is an open edge");
            Assert.AreEqual(1, def.extraAreas.Count, "the bowl's footprint is registered for the terrain and the foliage");
            Assert.IsTrue(def.layout.areas.Exists(r => r.Contains(def.tee)), "the tee is on the lane");
        }

        [Test]
        public void Hole4_LaneHeightHasNoCliffs_AndTheDropIsModest()
        {
            var l = Hole4().layout;
            float maxStep = 0f;
            for (float x = -0.6f; x < 0.5f; x += 0.1f)
            for (float z = 0.05f; z < 5.5f; z += 0.1f)
            {
                float h = l.Height(x, z);
                maxStep = Mathf.Max(maxStep, Mathf.Abs(l.Height(x + 0.1f, z) - h), Mathf.Abs(l.Height(x, z + 0.1f) - h));
            }
            Assert.Less(maxStep, 0.04f, $"height jump of {maxStep:F3} m between neighbouring cells");
            float drop = l.Height(0f, 1.0f) - l.Height(0f, 4.4f);
            Assert.That(drop, Is.InRange(0.15f, 0.30f), "the drop should be a visible but gentle step");
        }

        [UnityTest]
        public IEnumerator Hole4_BuildsABowlCup_AndABallRollsDownTheDrop_AndStaysInPlay()
        {
            using var b = new Hole3Bed(4);
            yield return Steps(5);
            Assert.IsNotNull(b.hole.Cup, "the hole needs the bowl's cup");
            b.ball.PlaceAt(b.Surface(0f, 1.2f));
            yield return Steps(5);
            b.ball.Strike(new Vector3(0f, 0f, 1.2f));
            yield return WaitUntil(() => b.ball.Position.z > 4.2f, 8f);
            Assert.Greater(b.ball.Position.z, 4.2f, $"ball did not roll down the drop, at {b.ball.Position}");
            Assert.AreEqual(1, b.hole.Strokes, "the drop should not cost a penalty");
            Assert.IsTrue(b.ball.InPlay);
        }

        [UnityTest]
        [NUnit.Framework.Timeout(900000)]
        public IEnumerator Hole4_TeePuttSweep_RollsBack_FallsShort_OrLandsInTheBowl()
        {
            var spec = TropicalCourse.Hole4Spec();
            var table = new System.Text.StringBuilder("[Test] hole 4 tee putt sweep (strike m/s -> outcome)\n");
            int landed = 0, holed = 0, gap = 0, back = 0, lost = 0;
            foreach (float s in new[] { 1.6f, 2.2f, 2.6f, 3.0f, 3.4f, 3.8f, 4.2f, 4.6f })
            {
                using var b = new Hole3Bed(4);
                yield return Steps(5);
                b.ball.Strike(Vector3.forward * s);
                yield return WaitUntil(() => b.hole.IsComplete || b.hole.Strokes >= 2 || (b.ball.IsAtRest && b.ball.Position.z > spec.ramp.LipZ), 60f);
                var o = spec.BowlOrigin;
                float r = new Vector2(b.ball.Position.x - o.x, b.ball.Position.z - o.z).magnitude;
                string text;
                if (b.hole.IsComplete) { text = "HOLED"; holed++; landed++; }
                else if (b.hole.Strokes >= 2) { bool left = b.ball.Position.z > spec.ramp.LipZ + spec.ramp.Gap + 0.5f; text = left ? "left the bowl (penalty)" : "short: pit (penalty)"; if (left) lost++; else gap++; }
                else if (r < spec.bowl.radius + 0.05f && b.ball.Position.z > spec.ramp.LipZ) { text = $"in the bowl at r {r:F2}, resting"; landed++; }
                else { text = $"rolled back (rest z {b.ball.Position.z:F2})"; back++; }
                table.AppendLine($"  {s:F1} -> {text} (strokes {b.hole.Strokes})");
            }
            Debug.Log(table.ToString());
            Assert.GreaterOrEqual(landed, 2, "several tee putts must land in the bowl");
            Assert.GreaterOrEqual(gap, 1, "a tee putt that is a little short must fall in the pit");
            Assert.GreaterOrEqual(back, 1, "a soft tee putt must stay on the lane");
            Assert.AreEqual(0, lost, "a ball that landed in the bowl must not leave it");
        }

        [UnityTest]
        public IEnumerator Hole4_Cup_CanBeHoled_FromTheBowl()
        {
            using var b = new Hole3Bed(4);
            yield return Steps(5);
            var cup = b.hole.Cup.transform.position;
            // On the bowl's cone, 0.5 m from the cup on the lane side: a gentle putt downhill should drop.
            var from = new Vector3(cup.x, cup.y + 0.06f, cup.z - 0.5f);
            b.ball.PlaceAt(from);
            yield return Steps(5);
            Vector3 d = cup - b.ball.Position; d.y = 0f;
            b.ball.Strike(d.normalized * 0.9f);
            yield return WaitUntil(() => b.hole.IsComplete, 10f);
            Assert.IsTrue(b.hole.IsComplete, $"cup not holed, ball at {b.ball.Position}, cup {cup}");
        }

        // ------------------------------------------------------------------ score celebrations

        [Test]
        public void ScoreCelebration_TiersFollowScoreAgainstPar()
        {
            Assert.AreEqual(ScoreCelebration.Tier.HoleInOne, ScoreCelebration.TierFor(1, 3));
            Assert.AreEqual(ScoreCelebration.Tier.HoleInOne, ScoreCelebration.TierFor(1, 2));
            Assert.AreEqual(ScoreCelebration.Tier.Albatross, ScoreCelebration.TierFor(2, 5));
            Assert.AreEqual(ScoreCelebration.Tier.Eagle, ScoreCelebration.TierFor(2, 4));
            Assert.AreEqual(ScoreCelebration.Tier.Birdie, ScoreCelebration.TierFor(2, 3));
            Assert.AreEqual(ScoreCelebration.Tier.Par, ScoreCelebration.TierFor(3, 3));
            Assert.AreEqual(ScoreCelebration.Tier.Bogey, ScoreCelebration.TierFor(4, 3));
            Assert.AreEqual(ScoreCelebration.Tier.None, ScoreCelebration.TierFor(0, 3));
            Assert.AreEqual("BIRDIE!", ScoreCelebration.LabelFor(ScoreCelebration.Tier.Birdie, -1));
            Assert.AreEqual("Double Bogey", ScoreCelebration.LabelFor(ScoreCelebration.Tier.Bogey, 2));
        }

        [UnityTest]
        public IEnumerator ScoreCelebration_BirdieAndBetterBurst_ParSparkles_BogeyOnlyLabels()
        {
            SceneManager.LoadScene("TropicalAdventure");
            yield return null;
            yield return null;
            var sc = Object.FindFirstObjectByType<ScoreCelebration>();
            Assert.IsNotNull(sc, "the scene has no ScoreCelebration");
            var at = Object.FindFirstObjectByType<CourseController>().Current.Cup.transform.position;

            sc.Celebrate(ScoreCelebration.Tier.HoleInOne, -2, at);
            yield return null; yield return null;
            Assert.AreEqual(ScoreCelebration.Tier.HoleInOne, sc.LastTier);
            Assert.IsNotNull(sc.LastLabel);
            Assert.IsNotNull(sc.LastBurst);
            Assert.Greater(sc.LastBurst.particleCount, 100, "a hole in one should throw plenty of confetti");
            int hio = sc.LastBurst.particleCount;

            sc.Celebrate(ScoreCelebration.Tier.Birdie, -1, at);
            yield return null; yield return null;
            Assert.Greater(sc.LastBurst.particleCount, 20);
            Assert.Less(sc.LastBurst.particleCount, hio, "a birdie is a smaller celebration than a hole in one");
            Assert.AreEqual("BIRDIE!", sc.LastLabel.GetComponentInChildren<UnityEngine.UI.Text>().text);

            var before = sc.LastBurst;
            sc.Celebrate(ScoreCelebration.Tier.Bogey, 1, at); // quiet: a label but no new confetti
            yield return null;
            Assert.AreSame(before, sc.LastBurst, "bogey must not throw confetti");
            Assert.AreEqual("Bogey", sc.LastLabel.GetComponentInChildren<UnityEngine.UI.Text>().text);

            // The label cleans itself up.
            var label = sc.LastLabel;
            float end = Time.time + 4f;
            while (label && Time.time < end) yield return null;
            Assert.IsTrue(label == null, "the score label should disappear on its own");
        }

        // ------------------------------------------------------------------ area title card

        [Test]
        public void TitleCard_Texts_AreFormattedForTheCard()
        {
            var jungle = TropicalCourse.Clusters().Find(c => c.id == TropicalCourse.JungleCluster);
            Assert.AreEqual("HOLES 3 – 4", AreaTitleCard.RangeText(jungle));
            Assert.AreEqual("HOLE 9", AreaTitleCard.RangeText(TropicalCourse.Clusters().Find(c => c.id == "summit")));
            Assert.AreEqual("J U N G L E   I S L A N D", AreaTitleCard.Spaced("Jungle Island"));
        }

        [Test]
        public void Clusters_HaveCardContent()
        {
            foreach (var c in TropicalCourse.Clusters())
            {
                Assert.IsFalse(string.IsNullOrEmpty(c.displayName), c.id);
                Assert.IsFalse(string.IsNullOrEmpty(c.tagline), $"{c.id} has no tagline for its title card");
                Assert.Greater(c.accent.maxColorComponent, 0.5f, $"{c.id} accent too dark to read");
            }
        }

        [UnityTest]
        public IEnumerator TitleCard_AppearsOnArrivalAndOnlyWhenTheIslandChanges()
        {
            SceneManager.LoadScene("TropicalAdventure");
            yield return null;
            yield return null;
            var card = Object.FindFirstObjectByType<AreaTitleCard>();
            var course = Object.FindFirstObjectByType<CourseController>();
            Assert.IsNotNull(card, "no AreaTitleCard in the scene");
            Assert.AreEqual(TropicalCourse.StartCluster, card.LastClusterId, "the first arrival gets a card");
            Assert.IsTrue(card.IsShowing);

            course.StartHole(1, true); // hole 2: same island, no new card
            yield return null;
            Assert.AreEqual(TropicalCourse.StartCluster, card.LastClusterId);

            course.StartHole(2, true); // hole 3: the Jungle Island
            yield return null;
            Assert.AreEqual(TropicalCourse.JungleCluster, card.LastClusterId);
            yield return new WaitForSecondsRealtime(3.4f); // 1.1 s delay + 1.4 s fade-in, then fully visible
            Assert.IsTrue(card.IsShowing);
            Assert.Greater(card.Alpha, 0.9f, "the card should be fully visible after its fade-in");
            Assert.AreEqual("JUNGLE ISLAND", card.TitleText.text);
            StringAssert.Contains("secrets", card.TaglineText.text);
            // Not interactive: it must never block the volume sliders' or anything else's raycasts.
            var group = card.GetComponentInChildren<CanvasGroup>(true);
            Assert.IsFalse(group.blocksRaycasts);
            yield return new WaitForSecondsRealtime(AreaTitleCard.Hold + AreaTitleCard.FadeOut + 0.3f);
            Assert.IsFalse(card.IsShowing, "the card should leave on its own");
            Assert.AreEqual(0f, card.Alpha, 1e-4f);
        }

        // ------------------------------------------------------------------ the generated scene

        [UnityTest]
        public IEnumerator TropicalScene_HasJungleIsland_WithHoleThreeOnABridge()
        {
            SceneManager.LoadScene("TropicalAdventure");
            yield return null;
            yield return new WaitForFixedUpdate();
            var course = Object.FindFirstObjectByType<CourseController>();
            Assert.GreaterOrEqual(course.Holes.Length, 3);
            var hole3 = course.Holes[2];
            Assert.AreEqual(3, hole3.HoleNumber);
            Assert.AreEqual(3, hole3.Par);
            var surface = hole3.GetComponentInChildren<PlayableSurface>();
            var renderer = surface.GetComponent<MeshRenderer>();
            Assert.AreEqual(3, renderer.sharedMaterials.Length, "the surface should carry the extra bridge-deck material");
            Assert.IsNotNull(renderer.sharedMaterials[2]);
            Assert.IsNotNull(GameObject.Find("IslandTerrain2"), "no Jungle Island terrain");

            // The deck floats over a ravine: terrain beneath the bridge centre is far below the surface.
            var t = hole3.transform;
            Vector3 deckCentre = t.TransformPoint(new Vector3(5.2f, 0.1f, 4.6f));
            var hits = Physics.RaycastAll(deckCentre + Vector3.up * 40f, Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore);
            float deckY = float.NaN, groundY = float.NaN;
            foreach (var h in hits)
            {
                if (h.collider.GetComponent<PlayableSurface>()) deckY = h.point.y;
                else if (h.collider.name == "IslandTerrain2") groundY = h.point.y;
            }
            Assert.IsFalse(float.IsNaN(deckY), "no deck under the bridge centre");
            Assert.IsFalse(float.IsNaN(groundY), "no terrain under the bridge");
            Assert.Greater(deckY - groundY, 2.0f, $"expected at least 2 m of ravine under the bridge, got {deckY - groundY:F2} m");
        }
    }
}
