using System;

namespace MotionRunner.Audio
{
    /// Where every clip lives under Resources/. Static and engine-free so the mapping is a table
    /// a test can walk, not a set of string literals scattered through the call sites.
    ///
    /// Music is discovered rather than listed: run tracks are `run_01`, `run_02`, ... and the
    /// player probes upwards from 1 until the first miss (GameAudio.RunTrackCount), so the art
    /// side can add a fourth track by dropping a file in and touching no code. MaxRunTracks only
    /// bounds the probe.
    public static class AudioCatalog
    {
        public const string SfxFolder = "Audio/Sfx/";
        public const string MusicFolder = "Audio/Music/";

        public const string MenuMusic = MusicFolder + "menu_loop";

        /// The loops under the result card, after the stinger: one for a run that set a best
        /// (all-time or daily), one for every other run. See RunHud's reveal.
        public const string ResultBestMusic = MusicFolder + "result_best";
        public const string ResultLostMusic = MusicFolder + "result_lost";

        /// Upper bound of the run-track probe. Not a count: the count is whatever is on disk.
        public const int MaxRunTracks = 8;

        /// Every Sfx, in declaration order - the loop AudioCatalogTests and GameAudio's per-frame
        /// cap both index by.
        public static readonly Sfx[] All = (Sfx[])Enum.GetValues(typeof(Sfx));

        /// `run_01` for 1. One-based on purpose, matching the file names the asset side uses.
        public static string RunTrackPath(int oneBasedIndex) =>
            MusicFolder + "run_" + oneBasedIndex.ToString("00");

        /// Resources path of a one-shot, or null for an enum value nobody has mapped yet (the
        /// test turns that null into a failure naming the entry).
        public static string Path(Sfx id)
        {
            switch (id)
            {
                case Sfx.UiTap: return SfxFolder + "ui_tap";
                case Sfx.UiBack: return SfxFolder + "ui_back";
                case Sfx.UiConfirm: return SfxFolder + "ui_confirm";
                case Sfx.UiDeny: return SfxFolder + "ui_deny";
                case Sfx.Coin: return SfxFolder + "coin";
                case Sfx.Jump: return SfxFolder + "jump";
                case Sfx.Slide: return SfxFolder + "slide";
                case Sfx.LaneSwoosh: return SfxFolder + "lane_swoosh";
                case Sfx.Crash: return SfxFolder + "crash";
                case Sfx.ResultJingle: return SfxFolder + "result_jingle";
                case Sfx.NewBest: return SfxFolder + "new_best";
                case Sfx.CountdownTick: return SfxFolder + "countdown_tick";
                case Sfx.CountdownGo: return SfxFolder + "countdown_go";
                case Sfx.Streak: return SfxFolder + "streak";
                default: return null;
            }
        }
    }
}
