using UnityEditor;
using UnityEngine;

namespace Gamebreak.MiniGolf.Editor
{
    /// <summary>World music (Assets/_Game/Audio/Music): streamed Vorbis, so a multi-minute track is not decoded into memory.</summary>
    public class MusicImporter : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/_Game/Audio/Music/")) return;
            var a = (AudioImporter)assetImporter;
            a.forceToMono = false;
            a.loadInBackground = true;
            var s = a.defaultSampleSettings;
            s.loadType = AudioClipLoadType.Streaming;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.6f;
            s.preloadAudioData = false;
            a.defaultSampleSettings = s;
        }
    }
}
