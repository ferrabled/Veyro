namespace MotionRunner.Social.Supabase
{
    /// The Supabase project coordinates. Both values are PUBLIC by design and safe to commit —
    /// the anon (publishable) key ships in every client and is harmless under RLS: it can read
    /// leaderboard RPCs and the caller's own rows, and can write NOTHING (all writes go through
    /// Edge Functions; see supabase/migrations/0001_init.sql). Same rationale as RevenueCatKeys.
    ///
    /// NEVER paste the service-role key, the JWT secret or the database password anywhere in
    /// this repo — those stay with the owner (PREREQUISITES P11).
    ///
    /// Empty values = backend disabled: GameBootstrap falls back to FakeProfileService and the
    /// game plays fully offline (fail-open, rule 3).
    public static class SupabaseKeys
    {
        /// https://<project-ref>.supabase.co — no trailing slash.
        public const string Url = "https://mrtedxxuwkoiumatxowx.supabase.co";

        /// The anon/publishable API key from Project Settings → API.
        public const string AnonKey = "sb_publishable_KF7FFNG13PUrDe-sSdPvpw_Ec0qJ3qH";

        public static bool IsConfigured =>
            !string.IsNullOrEmpty(Url) && !string.IsNullOrEmpty(AnonKey);
    }
}
