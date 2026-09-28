namespace MotionRunner.Track
{
    /// Whether another run may start — and, the part that is easy to get wrong, on WHICH FRAME.
    ///
    /// Engine-free like PauseState and ScoreState, because every bug this has produced is a frame
    /// ordering bug, and frame ordering is the one thing a playtest reports as "it sometimes jumps
    /// on the first frame" rather than as a reproducible fault.
    ///
    /// ---- who may ask ----------------------------------------------------------------------
    ///
    /// Two ways, and only two, since the 24 Sep owner call retired "tap anywhere":
    ///   * the RUN AGAIN button — RequestFromButton, called from the EventSystem's click dispatch;
    ///   * the camera hop — Tick's gestureActive, which RunSession reads from its RestartGesture
    ///     input. RunFlow sets that to the run's CameraFaceInput in camera mode and leaves it null
    ///     (button-only) in tilt mode and after DropCameraMode. A touch tap is not a gesture:
    ///     TouchTapInput lives in the composite the runner reads and never reaches this class,
    ///     so tapping the card outside its buttons does nothing.
    ///
    /// ---- why a restart is never immediate ----------------------------------------------------
    ///
    /// Both asks are, physically, the input that would make the runner act on the new run's
    /// first frame:
    ///   * the click — RunSession runs at DefaultExecutionOrder(100), so its Update is guaranteed
    ///     to come after the EventSystem's dispatch: a click lands *earlier in the very same
    ///     frame* whose input RunSession is about to read. RUN AGAIN used to call StartRun()
    ///     straight from the click (PR #5 review): the run was live again before RunSession's own
    ///     Update, which then read the same release and jumped.
    ///   * the hop — FaceSteering holds IsJumpActive for JumpWindowSeconds (0.18 s, ~11 frames)
    ///     and RunnerController reads jump as a level, so a run started on the hop's first frame
    ///     would jump on its own first frame. The same bug, ten frames wide.
    ///
    /// The rule that fixes both is one rule: an asked-for restart is QUEUED, and the run is only
    /// started from Tick — the same call that has just consumed this frame's input and that
    /// returns without stepping the runner — on the first frame on which NO gesture is active.
    /// For a click that is the very next frame; for a hop it is the frame after the pulse ends.
    /// Whatever asked has therefore already been spent by the time the runner exists to obey it.
    ///
    /// The restart that comes from the PAUSE menu is a different shape and is not gated here: that
    /// run is still IsRunning, so there is nothing for this class to release. It is deferred by
    /// PauseMenu's own frame counter instead, and the tap it defers past is consumed by RunSession
    /// while the session is still Frozen — see the comment on RunFlow.RestartRun.
    public sealed class RestartGate
    {
        /// Ignore restart input for a moment after a crash, so the hop that killed the player does
        /// not also skip the result screen.
        public const float LockoutSeconds = 0.5f;

        float _lockout;
        bool _queued;

        /// A restart is waiting for a later Tick to act on it.
        public bool IsQueued => _queued;

        /// The post-crash lockout is still running: nothing can ask for a run yet.
        public bool IsLockedOut => _lockout > 0f;

        /// The run just ended. Arms the lockout - by default the constant, or for as long as the
        /// caller's result screen takes to appear, whichever it passes: a hop that lands while the
        /// crash pose is still playing must not skip a card the player has not yet seen.
        public void LockOut(float seconds = LockoutSeconds) => _lockout = seconds > LockoutSeconds ? seconds : LockoutSeconds;

        /// The run was abandoned rather than finished (RunSession.Stop): no queued restart may
        /// survive into whatever comes next.
        public void Clear()
        {
            _queued = false;
            _lockout = 0f;
        }

        /// The RUN AGAIN button was clicked. Queues the run rather than starting it — see the
        /// class note. Returns whether the request was accepted, which is false during the
        /// post-crash lockout and false for a second click on top of one already queued.
        public bool RequestFromButton()
        {
            if (_lockout > 0f || _queued) return false;
            _queued = true;
            return true;
        }

        /// One frame with no run on screen. Returns true on the frame the next run must start.
        ///
        /// gestureActive is this frame's restart gesture (the camera hop; always false in tilt
        /// mode). It both ASKS - a gesture outside the lockout queues a run - and HOLDS: a queued
        /// run, however it was asked for, does not start while a gesture is in flight, so the hop
        /// is over before the runner it started can read it as a jump.
        ///
        /// overlayOpen is a screen that owns the taps — the main menu, which QUIT TO MENU and
        /// SKINS & SHOP put up with the very release that clicked them. A restart that lands
        /// while the overlay is up is dropped, not held, so closing it does not immediately
        /// start a run nobody asked for (found on device, 27 Aug, when the overlay was the store).
        public bool Tick(float deltaTime, bool gestureActive, bool overlayOpen)
        {
            if (_lockout > 0f) _lockout -= deltaTime;

            // Checked before the input below, which is what makes a queued restart cost a frame:
            // the request was made either by an earlier frame's Tick or by a click that landed
            // ahead of this one, and either way this frame's input has already been read and
            // discarded by the caller.
            if (_queued)
            {
                if (overlayOpen)
                {
                    _queued = false;
                    return false;
                }
                if (gestureActive) return false; // held until the hop has ended
                _queued = false;
                return true;
            }

            if (_lockout <= 0f && !overlayOpen && gestureActive) _queued = true;
            return false;
        }
    }
}
