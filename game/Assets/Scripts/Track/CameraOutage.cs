namespace MotionRunner.Track
{
    /// A camera-mode run that has stopped being steered. Answers one question per frame: has the
    /// camera been unable to say where the player is for long enough that the run should PAUSE
    /// ITSELF and ask them back into frame?
    ///
    /// ---- the hole this closes ----------------------------------------------------------------
    ///
    /// Since 31 Aug a lost face HOLDS the lane instead of recentring the runner (the loss contract
    /// on FaceSteering — walking into the middle lane is what kills you). That is right for a blur
    /// gap and wrong for an outage: if the rig fails outright, or the player steps out of shot, or
    /// the camera stalls, the axis freezes at its last value and lateral control is gone for the
    /// rest of the run with nothing on screen to recover it. Rule 3 says camera mode must always
    /// land the player on something that works; a frozen axis is not that.
    ///
    /// The owner's call (PR #5 review) is to pause rather than to silently swap in tilt: the pause
    /// menu already prompts "stand where the phone can see you…" over the framing overlay while it
    /// re-acquires, and its existing CameraGaveUp path already drops the run to tilt+touch when
    /// re-acquisition genuinely fails. So the whole recovery flow exists from the pause menu
    /// onward; this class is only the detector that reaches it. It also bounds the documented quirk
    /// that stepping out of frame keeps a lane indefinitely — the lane now holds for at most one
    /// outage window, and then the game stops and asks.
    ///
    /// Engine-free like PauseState and LaneSelector: "a blur gap must not pause the run, and a real
    /// outage must pause it exactly once" is arithmetic over a clock, and it is not something a
    /// playtest reliably reproduces in either direction. RunFlow owns the rig and the pause flow
    /// and feeds this the three facts it needs.
    public sealed class CameraOutage
    {
        /// How long a live camera run tolerates having no usable face position before it pauses
        /// itself. Seconds, unscaled.
        ///
        /// It has to clear the whole blur/loss-hold regime, which is what the pipeline deliberately
        /// rides out without complaining:
        ///   * CameraFaceInput treats an observation older than 0.35 s as loss (its StaleSeconds),
        ///     so a camera hiccup of a frame or two never even reaches this class;
        ///   * FaceSteering.PositionCarrySeconds (0.30 s) is how long a confident sighting vouches
        ///     for the marginal frames after it — the motion-blur streak in the middle of a
        ///     side-step or a hop, which is exactly when the score collapses;
        ///   * FaceSteering.LossGraceSeconds (0.28 s) is how long Lift is held before it decays.
        /// Every one of those is a third of a second, so **1.75 s is five times the longest gap the
        /// design considers normal** — 18 to 26 consecutive missed detections at the camera's 30 fps
        /// with inference every second or third frame. Nothing that reads as blur can reach it.
        ///
        /// And it has to stay under "the player has clearly left", because pausing is not free:
        /// coming back costs the camera restart plus CameraStaging.FaceSeenHoldSeconds of "I can
        /// see you", so a false auto-pause is a couple of seconds of a run that was fine. Against
        /// that, the cost of waiting is real too — the world moves at 12 to 19.7 m/s, so 1.75 s is
        /// 21 to 34 m of running on a held lane, which is likely to end in an obstacle. That is the
        /// deliberate trade: the run is probably lost either way once the camera has been blind this
        /// long, and pausing a doomed run is recoverable where a frozen axis is not, while pausing a
        /// healthy one for a blur streak would be a new bug in a mode that already asks a lot.
        ///
        /// One number, and it is the only dial here. Lower it and blur streaks start pausing real
        /// runs; raise it and stepping out of frame goes back to being uncapped.
        public const float PauseAfterSeconds = 1.75f;

        /// The most one frame may contribute. The same 0.1 s clamp FaceSteering puts on its own dt,
        /// and for the same reason: a hitch, an app coming back from the background, or an editor
        /// breakpoint hands over one enormous delta, and a single frame must not be able to spend
        /// the whole window. A run at under 10 fps stretches the real window proportionally, which
        /// is the harmless direction.
        public const float MaxStepSeconds = 0.1f;

        /// How long the current outage has lasted. Zero whenever the camera is delivering.
        public float ElapsedSeconds { get; private set; }

        /// True once this outage has asked for its pause, so it cannot ask twice.
        public bool HasAskedForPause { get; private set; }

        /// A fresh run, or a state where an outage means nothing.
        public void Reset()
        {
            ElapsedSeconds = 0f;
            HasAskedForPause = false;
        }

        /// One frame. Returns true exactly once per outage, on the frame the run must be paused.
        ///
        /// unscaledDeltaTime, not scaled: RunFlow.Update runs while Time.timeScale can be 0.
        /// cameraRunLive is the guard that makes the scaled clock irrelevant anyway — it is false
        /// for a tilt run, for a paused or resuming one, and for a session that is not running
        /// (the result screen), and in all of those an outage is either impossible or none of this
        /// class's business. Time only accumulates while a camera run is actually moving.
        ///
        /// hasPosition is the pipeline's own answer to "do we know where the player is" —
        /// FaceSteering.HasPosition, which already folds in the rig's state, the stale-observation
        /// cutoff and the marginal-score tier, so there is one definition of loss and this is not a
        /// second opinion. rigFailed is RigState.Failed, which is terminal: there is nothing to wait
        /// for, so it pauses on the spot rather than after the window.
        public bool Tick(float unscaledDeltaTime, bool cameraRunLive, bool hasPosition,
            bool rigFailed)
        {
            if (!cameraRunLive)
            {
                // Not just "stop counting": a paused run must come back armed, because the pause
                // menu's own re-acquisition is what decides whether the camera is usable again.
                Reset();
                return false;
            }

            if (rigFailed) return Ask();

            if (hasPosition)
            {
                // The outage ended. Re-arming here rather than only on Reset is what lets a second
                // outage later in the same run pause it again.
                ElapsedSeconds = 0f;
                HasAskedForPause = false;
                return false;
            }

            if (HasAskedForPause) return false;

            if (unscaledDeltaTime > 0f)
                ElapsedSeconds += unscaledDeltaTime < MaxStepSeconds
                    ? unscaledDeltaTime
                    : MaxStepSeconds;

            return ElapsedSeconds >= PauseAfterSeconds && Ask();
        }

        bool Ask()
        {
            if (HasAskedForPause) return false;
            HasAskedForPause = true;
            return true;
        }
    }
}
