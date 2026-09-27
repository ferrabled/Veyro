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

        /// True when THIS run just became the all-time best on its scheme's board. Captured from
        /// BestBoard.RecordAllTime at crash time, because once the board is written
        /// AllTimeBest == Score and the fact is no longer recoverable (ties do not record).
        public readonly bool NewAllTimeBest;

        /// True when this run just became today's daily best (always false in Free mode).
        public readonly bool NewDailyBest;

        /// What ended the run - drives the crash pose in the world and the card's character pose.
        public readonly CrashKind Crash;

        /// The seed the run was generated from: the share/challenge payload (T-024).
        public readonly RunSeed Seed;

        public RunSummary(RunMode mode, ControlScheme scheme, int score, int coins, int bestCombo,
            int distance, int allTimeBest, int dailyBest, string dailyLabel)
            : this(mode, scheme, score, coins, bestCombo, distance, allTimeBest, dailyBest, dailyLabel,
                false, false, CrashKind.WallSlam, default) { }

        public RunSummary(RunMode mode, ControlScheme scheme, int score, int coins, int bestCombo,
            int distance, int allTimeBest, int dailyBest, string dailyLabel,
            bool newAllTimeBest, bool newDailyBest, CrashKind crash, RunSeed seed)
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
            NewAllTimeBest = newAllTimeBest;
            NewDailyBest = newDailyBest;
            Crash = crash;
            Seed = seed;
        }

        public bool IsDaily => Mode == RunMode.Daily;

        /// The card's one headline: a new all-time best outranks a new daily best.
        public bool IsNewRecord => NewAllTimeBest || NewDailyBest;
    }

    /// How the run ended, derived from the obstacle the runner hit and whether it was airborne.
    /// Engine-free so the mapping is unit-testable (see CrashKinds).
    public enum CrashKind
    {
        /// Ran face-first into a full-height block: the runner is stamped against the wall.
        WallSlam = 0,
        /// Clipped a knee-high hurdle: the runner trips and tumbles forward.
        Trip = 1
    }

    public static class CrashKinds
    {
        /// A FullBlock is always a wall slam. A LowBarrier is a trip whether the runner never
        /// jumped or came down onto it mid-air - either way the feet catch the hurdle.
        public static CrashKind For(ObstacleKind kind, bool airborne) =>
            kind == ObstacleKind.FullBlock ? CrashKind.WallSlam : CrashKind.Trip;
    }
}
