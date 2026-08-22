namespace MotionRunner.Track
{
    /// What the result screen needs to know about a finished run. A struct rather than six
    /// parameters, so adding a field later does not reshape RunHud's signature.
    public readonly struct RunSummary
    {
        public readonly RunMode Mode;
        public readonly int Score;
        public readonly int Coins;
        public readonly int BestCombo;
        public readonly int Distance;

        /// All-time personal best, across every mode.
        public readonly int AllTimeBest;

        /// Best on today's Daily Run only. Zero in Free mode.
        public readonly int DailyBest;

        /// "2026-08-21" — the UTC date this run belongs to.
        public readonly string DailyLabel;

        public RunSummary(RunMode mode, int score, int coins, int bestCombo, int distance,
            int allTimeBest, int dailyBest, string dailyLabel)
        {
            Mode = mode;
            Score = score;
            Coins = coins;
            BestCombo = bestCombo;
            Distance = distance;
            AllTimeBest = allTimeBest;
            DailyBest = dailyBest;
            DailyLabel = dailyLabel ?? string.Empty;
        }

        public bool IsDaily => Mode == RunMode.Daily;
    }
}
