using System;

namespace MotionRunner.Social
{
    /// The rules that keep a reinstalling player's OLD profile reachable (18 Sep review, R1).
    /// Engine-free so every transition is unit-tested; the Supabase adapter only executes what
    /// this class decides.
    ///
    /// The invariant: a recovery key that belongs to another user is the device's ONLY road
    /// back to the old profile. Until its claim reaches a DEFINITIVE outcome, the key must not
    /// be overwritten, and no run may be submitted — a submission would make this fresh user a
    /// non-empty destination and permanently block the claim.
    public static class RecoveryGate
    {
        /// A foreign key is on file and its claim has not resolved: recovery is pending.
        public static bool IsPending(string storedUserId, string storedKey, string currentUserId)
        {
            if (string.IsNullOrEmpty(storedKey)) return false;
            if (string.IsNullOrEmpty(currentUserId)) return false;
            return storedUserId != currentUserId;
        }

        /// Issuing a fresh key for the CURRENT user is allowed only when it cannot destroy the
        /// road back to an old one: no key on file at all. (A key tagged with the current user
        /// is already right; a foreign key means pending — see IsPending.)
        public static bool MayRotate(string storedUserId, string storedKey, string currentUserId)
        {
            if (IsPending(storedUserId, storedKey, currentUserId)) return false;
            return string.IsNullOrEmpty(storedKey);
        }

        /// What a recovery response means for the stored key.
        public enum Resolution
        {
            /// Transient failure (no answer, 429, 5xx): keep the key, retry later, keep
            /// blocking submissions.
            StillPending,

            /// Claim succeeded: store the rotated key under the current user (or rotate
            /// fresh if the server returned none).
            Claimed,

            /// The server does not know this key (rotated away elsewhere, or the old profile
            /// was deleted): the key recovers nothing — discard it and move on.
            KeyInvalid,

            /// This install has already played as its own profile, so the claim is refused to
            /// protect it. Preserve the old key separately for support before issuing this
            /// profile's own code and allowing submissions again.
            Blocked
        }

        public static Resolution Resolve(long httpStatus, bool recovered, string reason)
        {
            if (httpStatus == 200 && recovered) return Resolution.Claimed;
            if (httpStatus == 200) return Resolution.KeyInvalid;
            if (httpStatus == 409 && reason == "destination_not_empty") return Resolution.Blocked;
            // 400 (malformed key) is definitive too - a key the server cannot even parse
            // recovers nothing.
            if (httpStatus == 400) return Resolution.KeyInvalid;
            return Resolution.StillPending; // 0, 401 (session hiccup), 429, 5xx
        }
    }

    /// Bounded exponential backoff with server override (18 Sep review, R4). Pure arithmetic;
    /// the adapter supplies the jitter factor and stores the state.
    public static class RetrySchedule
    {
        public const float FirstDelaySeconds = 5f;
        public const float MaxDelaySeconds = 300f;

        /// Server-supplied Retry-After is respected up to an hour — longer than our own cap,
        /// because the server knows why it is asking (R4: a 120 s Retry-After must not be
        /// shortened, and a 20-minute one must not be ignored).
        public const float MaxServerDelaySeconds = 3600f;

        /// The delay to apply now. `currentBackoff` is the stored doubling value;
        /// `retryAfterSeconds` &gt; 0 wins; `jitter` is a caller-supplied factor near 1.
        public static float Delay(float currentBackoff, float retryAfterSeconds, float jitter)
        {
            if (retryAfterSeconds > 0f)
                return Math.Min(retryAfterSeconds, MaxServerDelaySeconds);
            float delay = currentBackoff * jitter;
            return Math.Min(delay, MaxDelaySeconds);
        }

        /// The next stored doubling value after a failure.
        public static float NextBackoff(float currentBackoff) =>
            Math.Min(Math.Max(currentBackoff, FirstDelaySeconds) * 2f, MaxDelaySeconds);

        /// True when the HTTP status is worth retrying (vs a definitive rejection).
        public static bool IsTransient(long status) =>
            status == 0 || status == 429 || status >= 500;
    }
}
