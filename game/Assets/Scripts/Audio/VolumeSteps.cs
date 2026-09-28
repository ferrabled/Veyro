namespace MotionRunner.Audio
{
    /// The volume control's scale: a flat zero mark followed by ten bars, tapped or dragged
    /// (VolumeControl). Discrete rather than a free slider because ten steps are finer than anyone
    /// sets a game's music, and a step the finger lands on is a step the player can see. The
    /// stored level stays the 0..1 float SoundSettings already persists, so the pause menu, the
    /// profile tab and a level set by the old slider all read the same value.
    public static class VolumeSteps
    {
        /// Bars on the scale.
        public const int Count = 10;

        /// Positions a finger can land on: silence, then one per bar.
        public const int Positions = Count + 1;

        /// How many bars are lit for a stored level: nearest step, and none while muted - a muted
        /// channel should look silent even though its level is kept for the unmute.
        public static int Lit(float level, bool muted)
        {
            if (muted || float.IsNaN(level)) return 0;
            int steps = (int)(level * Count + 0.5f);
            return steps < 0 ? 0 : steps > Count ? Count : steps;
        }

        /// The position under a point `fraction` of the way along the scale (0 = its left edge,
        /// 1 = its right). The scale is Positions equal cells - the zero mark's, then each bar's -
        /// so a finger anywhere over a bar picks that bar, and anywhere left of the first bar,
        /// including past the edge while dragging, picks silence.
        public static int PositionAt(float fraction)
        {
            if (float.IsNaN(fraction) || fraction <= 0f) return 0;
            int position = (int)(fraction * Positions);
            return position > Count ? Count : position;
        }

        /// The level a position stands for: 0 is silence, Count is full.
        public static float LevelAt(int position)
        {
            if (position < 0) position = 0;
            if (position > Count) position = Count;
            return position / (float)Count;
        }
    }
}
