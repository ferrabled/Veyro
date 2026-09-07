namespace MotionRunner.Progression
{
    /// What the season-pass banner draws: where the player is on the Season 1 ladder
    /// (COSMETICS_CATALOG §3), and whether that number is real yet.
    public readonly struct SeasonSnapshot
    {
        public readonly int Level;
        public readonly int Xp;
        public readonly int XpForNextLevel;

        /// True once XP is actually earned from runs (T-025). Until then the banner says so
        /// instead of drawing a zero that looks like a bug or, worse, a bar that looks earned.
        public readonly bool IsLive;

        /// Whether the `season1` entitlement is held, i.e. whether the paid column is unlocked.
        public readonly bool PassOwned;

        public SeasonSnapshot(int level, int xp, int xpForNextLevel, bool isLive, bool passOwned)
        {
            Level = level;
            Xp = xp;
            XpForNextLevel = xpForNextLevel;
            IsLive = isLive;
            PassOwned = passOwned;
        }

        /// 0..1 for the progress bar. Zero while XP is not live, so the bar reads as empty rather
        /// than as an arbitrary fraction.
        public float Fraction
        {
            get
            {
                if (!IsLive || XpForNextLevel <= 0) return 0f;
                float fraction = (float)Xp / XpForNextLevel;
                return fraction < 0f ? 0f : fraction > 1f ? 1f : fraction;
            }
        }

        public bool IsMaxLevel => Level >= SeasonProgress.MaxLevel;
    }

    /// Where the banner gets its numbers. One interface with one placeholder behind it, so
    /// T-025 replaces the implementation and touches no UI: the card already renders levels, a
    /// bar and an owned/locked state, it just has nothing true to put in them yet.
    public interface ISeasonProgress
    {
        SeasonSnapshot Read(bool passOwned);
    }

    public static class SeasonProgress
    {
        /// The Season 1 ladder is 10 levels, level 1 is the starting level
        /// (COSMETICS_CATALOG §3) - so a fresh player is on the ladder, not below it.
        public const int MaxLevel = 10;
        public const int StartLevel = 1;

        /// The one line the banner shows in place of a fake XP number.
        public const string NotLiveNote = "XP from runs arrives with the Season 1 update";
    }

    /// The pre-T-025 source: the starting level, no XP, and honest about it. Deliberately does
    /// not read storage - there is no XP on disk to read, and inventing a key now would fix a
    /// format before the thresholds it has to hold are decided.
    public sealed class PlaceholderSeasonProgress : ISeasonProgress
    {
        public SeasonSnapshot Read(bool passOwned) =>
            new SeasonSnapshot(SeasonProgress.StartLevel, 0, 0, false, passOwned);
    }
}
