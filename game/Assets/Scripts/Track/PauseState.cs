namespace MotionRunner.Track
{
    /// What the Android back button means right now. Never Quit: back can leave a *run*, it can
    /// never leave the *app* out from under a player who is mid-run.
    public enum BackAction
    {
        Ignore,
        Pause,
        Resume,
        QuitToMenu,

        /// Close the first-run guide. It is the only overlay back *dismisses* rather than leaves
        /// alone, because it is the only one with nothing else on screen behind it to tap.
        CloseGuide,

        /// Give up on the camera at the mode picker and put the two mode buttons back.
        ///
        /// Not cosmetic: while the picker is staging the camera, both mode buttons are disabled
        /// (they are what started it) and there is no session for back to act on, so back used to
        /// be Ignore — and a player the camera never finds had no way out of the app but
        /// force-quitting it. That is CLAUDE.md rule 3 broken at the one screen whose whole job is
        /// to always offer tilt, and it was confirmed on the device. Cancelling is not a "quit":
        /// nothing has started, so back simply undoes the tap that began staging.
        CancelStaging
    }

    /// Running, paused, or on the way back from paused. Resuming exists because camera mode has
    /// to get the camera back - and stand the player in front of it again - before the world is
    /// allowed to move; tilt mode passes through it in a single frame.
    public enum PausePhase
    {
        Running,
        Paused,
        Resuming
    }

    /// The pause half of the run loop: which phase the run is in, whether the camera may be held,
    /// and what the back button does. Engine-free like ScoreState, so the transitions are unit
    /// tested rather than eyeballed on a phone - a pause that can be entered twice, or a resume
    /// that lands while the camera is still coming back, is not something a playtest reliably
    /// catches.
    ///
    /// The class decides *state* only. Freezing time, releasing the camera and building the
    /// screens are RunFlow's, and follow from IsFrozen / HoldsCamera.
    public sealed class PauseState
    {
        public PausePhase Phase { get; private set; } = PausePhase.Running;

        /// True while this run is steered by the camera. Falls to false for good if the camera
        /// cannot be re-acquired - the run finishes on tilt+touch (CLAUDE.md rule 3).
        public bool CameraMode { get; private set; }

        /// True whenever the world must not move.
        public bool IsFrozen => Phase != PausePhase.Running;

        /// True while the camera rig should hold the device camera. Pausing releases it - battery,
        /// and a camera that stays live behind a menu is the wrong look for a game that promises
        /// the frames never leave the phone.
        public bool HoldsCamera => CameraMode && Phase != PausePhase.Paused;

        /// A fresh run with the control scheme the player picked.
        public void BeginRun(bool cameraMode)
        {
            CameraMode = cameraMode;
            Phase = PausePhase.Running;
        }

        /// Freezes the run. Also the way back out of Resuming, so cancelling a re-acquisition
        /// lands on the pause menu rather than in a half-resumed run. False when already paused.
        public bool Pause()
        {
            if (Phase == PausePhase.Paused) return false;
            Phase = PausePhase.Paused;
            return true;
        }

        /// The player asked to come back. The world stays frozen until FinishResume - in camera
        /// mode that is seconds away, and in tilt mode it is the next frame, which is what keeps
        /// the tap that pressed RESUME from also being read as a jump.
        public bool BeginResume()
        {
            if (Phase != PausePhase.Paused) return false;
            Phase = PausePhase.Resuming;
            return true;
        }

        /// Whatever the resume was waiting for is ready.
        public bool FinishResume()
        {
            if (Phase != PausePhase.Resuming) return false;
            Phase = PausePhase.Running;
            return true;
        }

        /// The camera did not come back. Camera mode ends here for this run - the player finishes
        /// on tilt+touch instead of being stranded - and the pause menu goes back to plain paused,
        /// so resuming stays one deliberate tap.
        public bool DropCameraMode()
        {
            bool wasCamera = CameraMode;
            CameraMode = false;
            if (Phase == PausePhase.Resuming) Phase = PausePhase.Paused;
            return wasCamera;
        }

        /// The back button, given what is on screen. Pure so the mapping is a table, not a nest
        /// of ifs spread through the UI: during a run back pauses, on the pause menu it resumes,
        /// on the result screen it leaves for the menu, and an overlay that owns the screen
        /// (the store) keeps it.
        ///
        /// The two trailing arguments default to "a run is on screen and the guide is not",
        /// which is every case the pause menu itself cares about. They exist because back is
        /// owned in exactly one place (RunFlow.Update), so the two screens that are *not* a run
        /// have to be decidable here rather than by an early return up there:
        ///   * guideOpen - the first-run guide is over the top of everything, including the mode
        ///     picker, and back closes it;
        ///   * inSession - false at the mode picker, where there is no run to go back from and
        ///     back must fall through to Android rather than quit a run that does not exist;
        ///   * stagingCamera - the picker is waiting on the camera with both mode buttons
        ///     disabled, which is the one thing back has to be able to undo there (see
        ///     BackAction.CancelStaging). Only consulted outside a session: the pause menu's copy
        ///     of the same wait is cancelled by the Resuming row below, which already lands on
        ///     Pause -> RunFlow.RequestPause -> PauseMenu.CancelResume.
        public static BackAction BackFor(PausePhase phase, bool runLive, bool overlayOpen,
            bool guideOpen = false, bool inSession = true, bool stagingCamera = false)
        {
            if (guideOpen) return BackAction.CloseGuide;
            if (!inSession) return stagingCamera ? BackAction.CancelStaging : BackAction.Ignore;
            if (overlayOpen) return BackAction.Ignore;
            if (phase == PausePhase.Paused) return BackAction.Resume;
            if (phase == PausePhase.Resuming) return BackAction.Pause; // cancels the re-acquisition
            return runLive ? BackAction.Pause : BackAction.QuitToMenu;
        }
    }
}
