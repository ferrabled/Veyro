namespace MotionRunner.Track
{
    /// A challenge link that has arrived but has not been played yet (T-024).
    ///
    /// One static slot, deliberately: Android can deliver a link before anything in the game
    /// exists (cold start, `Application.absoluteURL`) or in the middle of a run
    /// (`Application.deepLinkActivated`), and both have to survive until the player is back at
    /// the menu, which is the only moment starting a run is safe. Engine-free so the hand-off is
    /// testable without a phone; the Unity side is only the two event sources.
    ///
    /// Last link wins - two links tapped in a row means the second is the one the player meant.
    public static class PendingChallenge
    {
        static ChallengeLink _link;

        /// True while a challenge is waiting to be played.
        public static bool Has { get; private set; }

        /// The waiting challenge, or default. Reading does not consume it - see TryTake.
        public static ChallengeLink Peek => _link;

        public static void Set(in ChallengeLink link)
        {
            _link = link;
            Has = true;
        }

        /// Takes the challenge and clears the slot, so one link starts exactly one run: the menu
        /// is shown again after every run, and a link left in place would restart itself forever.
        public static bool TryTake(out ChallengeLink link)
        {
            link = _link;
            bool had = Has;
            Has = false;
            _link = default;
            return had;
        }

        public static void Clear()
        {
            Has = false;
            _link = default;
        }
    }
}
