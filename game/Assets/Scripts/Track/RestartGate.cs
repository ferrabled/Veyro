namespace MotionRunner.Track
{
    /// Whether another run may start — and, the part that is easy to get wrong, on WHICH FRAME.
    ///
    /// Engine-free like PauseState and ScoreState, because every bug this has produced is a frame
    /// ordering bug, and frame ordering is the one thing a playtest reports as "it sometimes jumps
    /// on the first frame" rather than as a reproducible fault.
    ///
    /// ---- why a restart is never immediate ----------------------------------------------------
    ///
    /// A restart can be asked for two ways, and on a phone both arrive on the same
    /// TouchPhase.Ended:
    ///   * tapping anywhere on the result screen — TouchTapInput reports that release as a JUMP;
    ///   * tapping the RUN AGAIN button — the EventSystem reports that same release as a CLICK.
    /// So the input that asks for the next run is also, physically, the input that would make the
    /// runner jump on the new run's first frame. RunSession runs at DefaultExecutionOrder(100), so
    /// its Update is guaranteed to come after the EventSystem's dispatch: a click therefore lands
    /// *earlier in the very same frame* whose input RunSession is about to read.
    ///
    /// The rule that fixes both paths is the same one: an asked-for restart is QUEUED, and the run
    /// is only started from Tick — the same call that has just consumed this frame's input and that
    /// returns without stepping the runner. Whatever that release was read as has therefore already
    /// been spent by the time the runner exists to obey it.
    ///
    /// RUN AGAIN used to skip all of this by calling StartRun() straight from the click (PR #5
    /// review): the run was live again before RunSession's own Update, which then read the same
    /// release and jumped. The button now goes through RequestFromButton, so there is exactly one
    /// path into a new run.
    ///
    /// The restart that comes from the PAUSE menu is a different shape and is not gated here: that
    /// run is still IsRunning, so there is nothing for this class to release. It is deferred by
    /// PauseMenu's own frame counter instead, and the tap it defers past is consumed by RunSession
    /// while the session is still Frozen — see the comment on RunFlow.RestartRun.
    public sealed class RestartGate
    {
        /// Ignore restart input for a moment after a crash, so the tap that killed the player does
        /// not also skip the result screen.
        public const float LockoutSeconds = 0.5f;

        float _lockout;
        bool _queued;

        /// A restart is waiting for the next Tick to act on it.
        public bool IsQueued => _queued;

        /// The post-crash lockout is still running: nothing can ask for a run yet.
        public bool IsLockedOut => _lockout > 0f;

        /// The run just ended. Arms the lockout - by default the constant, or for as long as the
        /// caller's result screen takes to appear, whichever it passes: a tap that lands while the
        /// crash pose is still playing must not skip a card the player has not yet seen.
        public void LockOut(float seconds = LockoutSeconds) => _lockout = seconds > LockoutSeconds ? seconds : LockoutSeconds;

        /// The run was abandoned rather than finished (RunSession.Stop): no queued restart may
        /// survive into whatever comes next.
        public void Clear()
        {
            _queued = false;
            _lockout = 0f;
        }

        /// A restart BUTTON was clicked. Queues the run rather than starting it — see the class
        /// note. Returns whether the request was accepted, which is false during the post-crash
        /// lockout and false for a second click on top of one already queued.
        public bool RequestFromButton()
        {
            if (_lockout > 0f || _queued) return false;
            _queued = true;
            return true;
        }

        /// One frame with no run on screen. Returns true on the frame the next run must start.
        ///
        /// restartPressed is this frame's jump-or-special (the tap-anywhere path); overlayOpen is a
        /// screen that owns the taps — the store, which the SKINS & STORE button opens with the very
        /// release that would otherwise restart the run behind it (found on device, 27 Aug). A
        /// restart that lands while the overlay is up is dropped, not held, so closing the store
        /// does not immediately start a run nobody asked for.
        public bool Tick(float deltaTime, bool restartPressed, bool overlayOpen)
        {
            if (_lockout > 0f) _lockout -= deltaTime;

            // Checked before the input below, which is what makes a queued restart cost a frame:
            // the request was made either by an earlier frame's Tick or by a click that landed
            // ahead of this one, and either way this frame's input has already been read and
            // discarded by the caller.
            if (_queued)
            {
                _queued = false;
                return !overlayOpen;
            }

            if (_lockout <= 0f && !overlayOpen && restartPressed) _queued = true;
            return false;
        }
    }
}
