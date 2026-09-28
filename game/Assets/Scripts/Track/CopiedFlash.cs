// The iOS player and every editor, never the Android player: EditMode pins this from either
// build target while the Android build stays byte-identical (IOS_HANDOFF §3).
#if UNITY_IOS || UNITY_EDITOR
namespace MotionRunner.Track
{
    /// The SHARE button's "COPIED" beat on iOS (T-024, docs/IOS_HANDOFF.md decision 4). iOS has
    /// no share sheet, so SHARE puts the challenge text on the clipboard. The clipboard write is
    /// silent, so without this the tap looks like it did nothing, and App Review reads a
    /// button that does nothing as a bug (guideline 2.1).
    ///
    /// Engine-free on purpose, so the timing is EditMode-tested: the HUD reports each share,
    /// polls once a frame and writes the label only on the frames Poll says it changed - twice
    /// per tap, not every frame. The clock is whatever the caller passes (the HUD uses unscaled
    /// time, because the result card can be up at timeScale 0).
    public sealed class CopiedFlash
    {
        /// What the button reads while the flash runs.
        public const string Label = "COPIED";

        /// How long it reads that before going back to its own label.
        public const float Seconds = 2f;

        float _until;
        bool _active;   // a flash is running
        bool _applied;  // what the button was last told to show (true = Label)

        /// `sheetOpened` is ShareSheet.Send's answer. False means the text went to the clipboard,
        /// which starts the flash; a second copy inside the window restarts it. True means a real
        /// sheet opened, which is its own feedback, so any running flash stops.
        public void Report(bool sheetOpened, float now)
        {
            _active = !sheetOpened;
            _until = now + Seconds;
        }

        /// Whether the button should read Label at `now`.
        public bool Showing(float now) => _active && now < _until;

        /// True only when the button has to be redrawn at `now`, with `showCopied` saying what
        /// to: Label (true) or the button's own text (false).
        public bool Poll(float now, out bool showCopied)
        {
            showCopied = Showing(now);
            if (!showCopied) _active = false;
            if (showCopied == _applied) return false;
            _applied = showCopied;
            return true;
        }

        /// Stops the flash; the next Poll puts the button's own text back if it was showing.
        public void Clear() => _active = false;
    }
}
#endif
