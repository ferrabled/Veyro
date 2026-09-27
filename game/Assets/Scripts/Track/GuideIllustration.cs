using System.Globalization;

namespace MotionRunner.Track
{
    /// Where the how-to-play illustrations live and what they are called - the contract between
    /// GuideState.Pages (which names a key and a frame count), FirstRunGuide (which loads the
    /// frames and cycles them) and the PNGs the art brief in docs/GUIDE_ILLUSTRATIONS.md asks
    /// for. Engine-free so the naming is tested rather than discovered on device when a frame
    /// silently fails to load.
    ///
    /// A "GIF" here is a numbered frame sequence: Unity cannot play GIFs, a video is APK weight
    /// and a store-review surface, and stills localise for free. `tilt` with four frames is
    /// Art/Guide/tilt_01 … tilt_04; a sequence of one is a still.
    public static class GuideIllustration
    {
        /// Resources path of the folder, as Resources.Load wants it.
        public const string ResourceFolder = "Art/Guide";

        /// The same folder as an asset path, with the trailing slash so a StartsWith on it
        /// cannot match a sibling folder that merely shares the prefix.
        public const string AssetFolder = "Assets/Resources/" + ResourceFolder + "/";

        /// How long each frame of a multi-frame sequence is held. Unscaled: the guide can sit over
        /// a paused game.
        public const float SecondsPerFrame = 1.1f;

        /// "tilt_01": the file name without folder or extension. Frames are numbered from one and
        /// zero-padded to two digits so they sort in the folder the way they play.
        public static string FrameName(string key, int index) =>
            key + "_" + index.ToString("00", CultureInfo.InvariantCulture);

        /// "Art/Guide/tilt_01": what Resources.Load takes.
        public static string FramePath(string key, int index) =>
            ResourceFolder + "/" + FrameName(key, index);

        /// Expected source filenames for a sequence: "tilt_01.png … tilt_04.png", or the
        /// single file name for a still. Asset names are not displayed in the game's fallback.
        public static string FileRange(string key, int frameCount) =>
            frameCount <= 1
                ? FrameName(key, 1) + ".png"
                : FrameName(key, 1) + ".png … " + FrameName(key, frameCount) + ".png";

        /// Keys are lowercase ASCII letters only: they become file names on a case-insensitive
        /// Windows checkout and a case-sensitive Android one, and Resources paths on both.
        public static bool IsValidKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            foreach (char c in key)
                if (c < 'a' || c > 'z') return false;
            return true;
        }

        /// The frame after `current` in a loop of `frameCount`. A still never advances.
        public static int NextFrame(int current, int frameCount)
        {
            if (frameCount <= 1) return 0;
            int next = current + 1;
            return next >= frameCount ? 0 : next;
        }
    }
}
