namespace MotionRunner.Track
{
    /// Who asked for the resume that is currently staging.
    ///
    /// The distinction exists because of what the pause menu is allowed to do when the resume
    /// gesture is unavailable. A resume the PLAYER started (RESUME, RESTART RUN, Android back) was
    /// already confirmed — by the tap that started it — so standing back in frame is enough to
    /// count down. A resume the GAME started (RunFlow's camera-outage auto-pause calling
    /// PauseMenu.BeginAutoResume) carries no confirmation at all: nobody touched anything, the
    /// player may have walked away for a reason, and their face reappearing in frame is not
    /// consent to unfreeze a run they can lose.
    public enum ResumeOrigin
    {
        /// RESUME / RESTART RUN / Android back — the tap was the confirmation.
        User = 0,

        /// PauseMenu.BeginAutoResume, off a camera outage. Nothing has been confirmed yet.
        Auto = 1
    }

    /// What the pause menu does once the camera is back and the face is held.
    public enum ResumeConfirmStep
    {
        /// "stand still…", then "hop twice when ready — or tap RESUME". The hop confirm is up;
        /// the gesture is the confirmation, and a tap is the always-available second way (rule 3).
        Gesture,

        /// Straight into the 3-2-1. Only legal when the resume was already confirmed by touch.
        Countdown,

        /// No gesture and no confirmation yet: buttons live, "tap RESUME when ready", and nothing
        /// moves until the player says so.
        TouchConfirm
    }

    /// The one rule deciding it — pure, so the case that matters can be pinned in a test rather
    /// than only reachable through a broken gesture on a phone.
    ///
    /// Read it as: the gesture is the confirmation when it can be; a tap that already happened is
    /// a confirmation; otherwise SOMEBODY still has to confirm. The bug this closes (PR #6
    /// review) is the fourth row of the table below, where a camera outage auto-paused the run,
    /// the gesture was unavailable (then: a pose model that failed to load), and simply walking
    /// back into frame counted 3-2-1 and handed a moving runner to a player who had not asked for
    /// one.
    ///
    /// Since 28 Sep 2026 the gesture is two hops (MotionRunner.Pose.DoubleHopConfirm), which
    /// needs no model and so is available whenever the staging holds a face: the "down" rows
    /// are a safety net rather than a path. They stay, and stay pinned, because the rule is about
    /// who has consented — not about which gesture happens to be asking.
    ///
    ///   origin | gesture | step
    ///   -------|---------|---------------
    ///   User   | up      | Gesture       (hops or tap; either confirms)
    ///   User   | down    | Countdown     (the RESUME tap was the confirmation)
    ///   Auto   | up      | Gesture       (the hops are the confirmation)
    ///   Auto   | down    | TouchConfirm  (nothing has confirmed anything — ask)
    public static class ResumeConfirm
    {
        public static ResumeConfirmStep For(ResumeOrigin origin, bool gestureAvailable)
        {
            if (gestureAvailable) return ResumeConfirmStep.Gesture;
            return origin == ResumeOrigin.User
                ? ResumeConfirmStep.Countdown
                : ResumeConfirmStep.TouchConfirm;
        }
    }
}
