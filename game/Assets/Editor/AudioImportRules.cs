using UnityEditor;
using UnityEngine;

namespace MotionRunner.EditorTools
{
    /// Import settings for everything under Assets/Resources/Audio, applied in code so they hold
    /// on FIRST import - the clips arrive from the art side after this rule exists, and a .meta
    /// somebody hand-edited in the inspector would not survive a re-export anyway (the ParkArtImport
    /// shape).
    ///
    ///   Music/  - Streaming, Vorbis 0.7, no preload: a two-minute loop decoded on the fly costs
    ///             a few hundred KB of RAM instead of tens of MB decompressed, and the game
    ///             never waits on it at load.
    ///   Sfx/    - DecompressOnLoad, Vorbis 0.8, mono: one-shots must fire on the frame asked
    ///             (a coin that arrives 40 ms late is a coin nobody hears as theirs), and stereo
    ///             buys nothing for a 2D cue coming out of one phone speaker.
    ///
    /// The Android override mirrors the default so a platform-specific setting somebody flips in
    /// the inspector cannot quietly diverge from what the rule says.
    public sealed class AudioImportRules : AssetPostprocessor
    {
        const string MusicFolder = "Assets/Resources/Audio/Music/";
        const string SfxFolder = "Assets/Resources/Audio/Sfx/";

        /// Bumping this forces every clip under the folders to reimport with the new rule.
        public override uint GetVersion() => 1;

        void OnPreprocessAudio()
        {
            bool music = assetPath.StartsWith(MusicFolder);
            if (!music && !assetPath.StartsWith(SfxFolder)) return;

            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            if (music)
            {
                settings.loadType = AudioClipLoadType.Streaming;
                settings.quality = 0.7f;
                settings.preloadAudioData = false;
            }
            else
            {
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.quality = 0.8f;
                settings.preloadAudioData = true;
            }

            importer.defaultSampleSettings = settings;
            importer.SetOverrideSampleSettings("Android", settings);
            importer.forceToMono = !music;
            importer.loadInBackground = false;
            importer.ambisonic = false;
        }
    }
}
