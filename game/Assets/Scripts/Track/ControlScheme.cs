namespace MotionRunner.Track
{
    /// Who steers: the phone's gyro plus touch, or the player's own body in front of the camera.
    ///
    /// Deliberately NOT RunMode. RunMode says *which track* the run belongs to (Daily — everyone on
    /// today's UTC date runs the same seed — or Free); this says *how the runner is driven*. The two
    /// are orthogonal, and every combination is reachable from the mode picker, so folding them into
    /// one enum would have made "today's daily, steered by tilt" and "today's daily, steered by the
    /// camera" indistinguishable at exactly the place they must not be.
    ///
    /// That place is the leaderboards. The owner's call (Feature D, 2026-09-01) is that camera and
    /// tilt are separate games and keep separate boards: leaning your whole body to dodge is not the
    /// same skill as thumbing a phone, and one shared board would have meant one of the two control
    /// schemes silently owning every personal best. The same daily seed still drives both — the
    /// track is identical, determinism is untouched (CLAUDE.md rule 4) — only the boards split. See
    /// BestBoard for the keying and the one-time migration of the pre-split scores.
    ///
    /// Values are pinned because they are persisted indirectly through BestBoard's key suffixes and
    /// will become the two leaderboard IDs when T-009 (Play Games) lands.
    public enum ControlScheme
    {
        Tilt = 0,
        Camera = 1
    }
}
