using System;

namespace MotionRunner.Social
{
    /// Everything gameplay and the menu are allowed to know about the player's online profile.
    /// No HTTP type and no Supabase shape crosses this boundary — the same seam rule as IStore
    /// (CLAUDE.md rule 5; design in docs/PROFILE_LEADERBOARD_PLAN.md §6).
    ///
    /// Callback-shaped, not async, for the same reason IStore is: engine-free without a Task
    /// scheduler, and it matches how the adapters actually complete.
    ///
    /// Fail-open throughout: no network, no key, backend down — every call completes with an
    /// outcome, the profile just stays absent, and the game never blocks on the backend
    /// (rule 3's shape, same as Commerce and the camera).
    public interface IProfileService
    {
        /// True once a session exists and the profile row has been read at least once.
        bool IsReady { get; }

        /// The player's profile, or null until IsReady (and again after DeleteAccount).
        Profile Current { get; }

        /// Raised when Current changes: first load, handle reroll, deletion.
        event Action ProfileChanged;

        /// Sends one finished run. Every finished run participates — joining the board is
        /// automatic (owner call, 17 Sep, reversing the 3 Sep opt-in; the privacy policy copy
        /// describes exactly this). Offline or not ready → the run is queued on device and
        /// retried later (outcome Queued).
        void SubmitRun(RunSubmission run, Action<SubmitOutcome> done);

        /// One board page: top rows plus the caller's own rank on that board.
        void FetchBoard(BoardQuery query, Action<BoardResult, SocialError> done);

        /// Server-generated replacement handle; unlimited rerolls, with a server burst throttle.
        void RerollHandle(Action<Profile, SocialError> done);

        /// True deletion of the profile and every run/board row (Play account-deletion policy).
        /// On success the adapter also clears its local session and recovery key.
        void DeleteAccount(Action<SocialError> done);

        /// The account's recovery code, when one belongs to the CURRENT profile — the secret
        /// that proves ownership and re-imports the profile on another install (owner call,
        /// 18 Sep: player-visible, copyable). Empty while none exists or while a foreign
        /// key's claim is pending. Anyone holding it can claim the profile: the UI shows it
        /// with that warning.
        string RecoveryCode { get; }

        /// Claims the profile a pasted recovery code belongs to onto THIS install (the manual
        /// cross-device / backup-off rescue; same server path as automatic reinstall
        /// recovery). Fails honestly when this install has already played
        /// ("destination_not_empty") — delete the local profile and relaunch first.
        void ImportProfile(string recoveryCode, Action<SocialError> done);
    }

    /// The player as the backend knows them.
    public sealed class Profile
    {
        public readonly string UserId;
        public readonly string Handle;
        public readonly int Xp;

        public Profile(string userId, string handle, int xp)
        {
            UserId = userId ?? string.Empty;
            Handle = handle ?? string.Empty;
            Xp = xp;
        }
    }

    /// Error shape shared by every call; null means success.
    public sealed class SocialError
    {
        public readonly string Code;
        public readonly string Message;

        public SocialError(string code, string message)
        {
            Code = code ?? "unknown";
            Message = message ?? string.Empty;
        }

        public override string ToString() => Code + ": " + Message;
    }
}
