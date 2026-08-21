namespace MotionRunner.Track
{
    /// Pacing curve (handoff 3.2): minute 1 easy, minute 5 hard.
    /// T-007 refines the shape; the contract (time-driven, monotonic, clamped) stays.
    public static class DifficultyCurve
    {
        public const int MinDifficulty = 1;
        public const int MaxDifficulty = 8;
        public const float SecondsPerStep = 45f;

        public static int At(float elapsedSeconds)
        {
            if (elapsedSeconds < 0f) elapsedSeconds = 0f;
            int difficulty = MinDifficulty + (int)(elapsedSeconds / SecondsPerStep);
            return difficulty > MaxDifficulty ? MaxDifficulty : difficulty;
        }
    }
}
