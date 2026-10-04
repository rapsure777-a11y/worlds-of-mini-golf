using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Gamebreak.MiniGolf.Tests
{
    /// <summary>Cluster-based world music: mapping, continuity within a cluster, crossfades and loops.</summary>
    public class MusicTests
    {
        static AudioClip Tone(string name, float seconds, float hz)
        {
            const int rate = 22050;
            int n = Mathf.RoundToInt(seconds * rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = 0.05f * Mathf.Sin(2f * Mathf.PI * hz * i / rate);
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static MusicPlayer NewPlayer(MusicCluster[] clusters)
        {
            var p = new GameObject("TestMusic").AddComponent<MusicPlayer>();
            p.Configure(clusters, null, 0.55f, null);
            return p;
        }

        [Test]
        public void Clusters_MapHolesToTracks()
        {
            var clusters = new[]
            {
                new MusicCluster { name = "A", firstHole = 1, lastHole = 2 },
                new MusicCluster { name = "B", firstHole = 3, lastHole = 4 },
                new MusicCluster { name = "C", firstHole = 9, lastHole = 9 },
            };
            Assert.AreEqual("A", MusicCluster.For(clusters, 1).name);
            Assert.AreEqual("A", MusicCluster.For(clusters, 2).name);
            Assert.AreEqual("B", MusicCluster.For(clusters, 3).name);
            Assert.AreEqual("C", MusicCluster.For(clusters, 9).name);
            Assert.IsNull(MusicCluster.For(clusters, 6));
        }

        [UnityTest]
        public IEnumerator TropicalScene_MusicContinuesWithinCluster()
        {
            SceneManager.LoadScene("TropicalAdventure");
            yield return null;
            yield return null;
            var player = Object.FindFirstObjectByType<MusicPlayer>();
            Assert.IsNotNull(player, "no MusicPlayer in the scene");
            Assert.IsNotNull(player.CurrentClip, "no music on hole 1");
            Assert.AreEqual("IslandExploration", player.CurrentClip.name);
            Assert.AreEqual(0f, player.ActiveSource.spatialBlend, "music should be 2D");
            Assert.AreEqual(0.55f, player.Volume, 1e-4f, "default music volume");
            yield return new WaitForSecondsRealtime(1f);
            var source = player.ActiveSource;
            float before = source.time;
            Assert.Greater(before, 0.5f, "music should be playing");

            // Holes 1 and 2 share the Starting Island cluster: no restart, no second source.
            var course = Object.FindFirstObjectByType<CourseController>();
            course.StartHole(1, true);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreSame(source, player.ActiveSource, "same cluster must not switch sources");
            Assert.Greater(source.time, before, "same cluster must not restart the track");
            Assert.AreEqual(1, player.PlayingSourceCount);
        }

        [UnityTest]
        public IEnumerator NewCluster_CrossfadesToItsTrack()
        {
            var a = Tone("A", 30f, 220f);
            var b = Tone("B", 30f, 330f);
            var player = NewPlayer(new[]
            {
                new MusicCluster { name = "Island A", firstHole = 1, lastHole = 2, clip = a },
                new MusicCluster { name = "Island B", firstHole = 3, lastHole = 4, clip = b },
            });
            yield return null;
            player.PlayFor(1, 0.1f);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreSame(a, player.CurrentClip);
            player.PlayFor(3, 1f);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreSame(b, player.CurrentClip);
            Assert.AreEqual(2, player.PlayingSourceCount, "both tracks audible mid-crossfade");
            yield return new WaitForSecondsRealtime(1f);
            Assert.AreEqual(1, player.PlayingSourceCount, "old track stopped after the crossfade");
            Assert.AreEqual(0.55f, player.ActiveSource.volume, 0.01f);
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator MissingClusterClip_KeepsCurrentMusic()
        {
            var a = Tone("A", 30f, 220f);
            var player = NewPlayer(new[]
            {
                new MusicCluster { name = "Island A", firstHole = 1, lastHole = 1, clip = a },
                new MusicCluster { name = "Island B", firstHole = 2, lastHole = 2, clip = null },
            });
            yield return null;
            player.PlayFor(1, 0.1f);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Island B.*no clip"));
            player.PlayFor(2, 0.1f);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreSame(a, player.CurrentClip);
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator TrackEnd_LoopsByCrossfadingIntoItsStart()
        {
            var a = Tone("A", 12f, 220f);
            var player = NewPlayer(new[] { new MusicCluster { name = "Island A", firstHole = 1, lastHole = 1, clip = a } });
            yield return null;
            player.PlayFor(1, 0.05f);
            yield return new WaitForSecondsRealtime(0.2f);
            var first = player.ActiveSource;
            first.time = 12f - 2.6f; // just before the 2.5 s loop crossfade
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreNotSame(first, player.ActiveSource, "loop should hand over to the other source");
            Assert.AreSame(a, player.CurrentClip);
            Assert.Less(player.ActiveSource.time, 1f, "loop restarts the track from the beginning");
            Assert.AreEqual(2, player.PlayingSourceCount, "tail and new start overlap during the loop crossfade");
            yield return new WaitForSecondsRealtime(2.6f);
            Assert.AreEqual(1, player.PlayingSourceCount);
            Object.Destroy(player.gameObject);
        }
    }
}
