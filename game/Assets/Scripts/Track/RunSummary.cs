namespace MotionRunner.Track
{
    /// What the result screen needs to know about a finished run. A struct rather than six
    /// parameters, so adding a field later does not reshape RunHud's signature.
    public readonly struct RunSummary
    {
        public readonly RunMode Mode;

        /// Who was steering. Carried because the two bests below belong to this scheme's board and
        /// not to the player as a whole, so the result screen has to be able to say which board it
        /// is quoting.
        public readonly ControlScheme Scheme;

        public readonly int Score;
        public readonly int Coins;
        public readonly int BestCombo;
        public readonly int Distance;

        /// All-time personal best on the ACTIVE CONTROL SCHEME's board. Camera and tilt keep
        /// separate boards as of the 2026-09-01 split (see BestBoard), so this is no longer one
        /// number across everything the player has ever run.
        public readonly int AllTimeBest;

        /// Best on today's Daily Run, on the active control scheme's board. Zero in Free mode.
        public readonly int DailyBest;

        /// "2026-08-21" — the UTC date this run belongs to.
        public readonly string DailyLabel;

        public RunSummary(RunMode mode, ControlScheme scheme, int score, int coins, int bestCombo,
            int distance, int allTimeBest, int dailyBest, string dailyLabel)
        {
            Mode = mode;
            Scheme = scheme;
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
