namespace MotionRunner.Audio
{
    /// The profile tab's volume bars: ten tappable steps instead of a slider. A slider inside a
    /// scrolling page fights the scroll - a vertical swipe that starts on it jumps the level - and
    /// ten steps are finer than anyone sets a game's music. The stored level stays the 0..1 float
    /// SoundSettings already persists, so the pause menu's toggles and a level set by the old
    /// slider both carry over.
    public static class VolumeSteps
    {
        public const int Count = 10;

        /// How many bars are lit for a stored level: nearest step, and none while muted - a muted
        /// channel should look silent even though its level is kept for the unmute.
        public static int Lit(float level, bool muted)
        {
            if (muted || float.IsNaN(level)) return 0;
            int steps = (int)(level * Count + 0.5f);
            return steps < 0 ? 0 : steps > Count ? Count : steps;
        }

        /// The level a tap on bar `index` (0 = quietest) sets. The quietest bar is one step, not
        /// silence: silence is the ON/OFF pill's job, and a bar that sets zero is a bar that looks
        /// lit and plays nothing.
        public static float LevelFor(int index)
        {
            if (index < 0) index = 0;
            if (index >= Count) index = Count - 1;
            return (index + 1) / (float)Count;
        }
    }
}
