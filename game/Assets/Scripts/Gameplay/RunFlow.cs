using MotionRunner.CameraInput;
using MotionRunner.Core;
using MotionRunner.Inputs;
using MotionRunner.Track;
using UnityEngine;

namespace MotionRunner.Gameplay
{
    /// Everything either side of the run loop: the mode picker, the pause menu, the road back to
    /// the picker, and the camera's lifetime across all of it. GameBootstrap builds the world
    /// once and hands it here, so there is exactly one place that knows what starting, pausing
    /// and leaving a run costs.
    ///
    /// The camera lives here rather than in RunSession on purpose: gameplay code never touches
    /// sensors (CLAUDE.md rule 2), and every way out of camera mode - pause, restart, quit, a rig
    /// that will not come back - has to land the player on tilt+touch instead of stranding them
    /// (rule 3). That includes the camera going away without anybody asking: see
    /// TickCameraOutage.
    public sealed class RunFlow : MonoBehaviour
    {
        readonly PauseState _pause = new PauseState();

        /// Watches for a camera that has stopped answering during a live run. See TickCameraOutage.
        readonly CameraOutage _outage = new CameraOutage();

        RunSession _session;
        RunHud _hud;
        FaceTrackingRig _rig;
        CameraFaceInput _faceInput;
        PauseMenu _pauseMenu;
        FirstRunGuide _guide;
        ModeSelectMenu _menu;
        FaceOverlay _overlay;

        public static RunFlow Create(RunSession session, RunHud hud)
        {
            var go = new GameObject("RunFlow");
            var flow = go.AddComponent<RunFlow>();
            flow._session = session;
            flow._hud = hud;
            hud.PauseRequested += flow.RequestPause;
            hud.QuitRequested += flow.QuitToMenu;
            flow.ShowMenu();
            return flow;
        }

        // ---- menu -> run ----

        void ShowMenu()
        {
            _menu = ModeSelectMenu.Create();
            _menu.Chosen += StartRun;
            _menu.GuideRequested += ShowGuide;

            // First launch only. It goes up over the picker rather than before it, so the mode
            // choice is made with the controls already explained and the picker is the first
            // thing the player sees behind it.
            if (FirstRunGuide.IsDue) ShowGuide();
        }

        void StartRun(bool cameraMode, FaceTrackingRig rig)
        {
            // A guide left open never survives into a run: camera staging finishes on its own
            // clock, so the run can start while the player is still reading.
            if (_guide != null) _guide.Dismiss();

            _menu = null; // it destroys itself on the way out of the pick
            _rig = rig;
            _pause.BeginRun(cameraMode);
            ApplyPhase();
            _session.Begin(BuildInput(cameraMode));
            ShowOverlay(cameraMode);
        }

        /// The player-position panel, HUD-sized, for camera runs only. It reads the run's own
        /// adapter, so it can never show a different lean from the one the runner is obeying.
        void ShowOverlay(bool cameraMode)
        {
            DismissOverlay();
            if (!cameraMode || _faceInput == null) return;
            _overlay = FaceOverlay.Attach(_rig, _faceInput);
        }

        void DismissOverlay()
        {
            if (_overlay == null) return;
            _overlay.Dismiss();
            _overlay = null;
        }

        /// Touch and keyboard stay in the mix in camera mode: they cost nothing, keep the result
        /// screen restartable if tracking drops, and are how a bystander pauses the hands-free
        /// demo without standing in frame.
        IGameInput BuildInput(bool cameraMode)
        {
            _faceInput = cameraMode ? new CameraFaceInput(_rig) : null;
            return cameraMode
                ? new CompositeInput(
                    _faceInput,
                    new TouchTapInput(),
                    new KeyboardInput())
                : new CompositeInput(
                    new GyroTiltInput(),
                    new TouchTapInput(),
                    new KeyboardInput());
        }

        // ---- pause ----

        void RequestPause()
        {
            if (!_session.enabled || !_session.IsRunning) return;
            if (!_pause.Pause()) return;

            ApplyPhase();
            if (_pauseMenu != null)
            {
                // Already up: this is back cancelling a re-acquisition, not a new pause.
                _pauseMenu.CancelResume();
                return;
            }

            _pauseMenu = PauseMenu.Show(_pause.CameraMode ? _rig : null);
            _pauseMenu.ResumeStarted += BeginResume;
            _pauseMenu.ResumeRequested += Resume;
            _pauseMenu.RestartRequested += RestartRun;
            _pauseMenu.QuitRequested += QuitToMenu;
            _pauseMenu.CameraGaveUp += DropCameraMode;
        }

        /// The player asked to come back. The world stays frozen until the camera is ready -
        /// ApplyPhase is what turns it back on, because HoldsCamera is true again from here.
        ///
        /// The old face calibration goes with it: a player who walked away and stood back down
        /// is somewhere else now, and FaceSteering's neutral is where they were standing when
        /// they paused. It re-calibrates during the "stand where the phone can see you" wait, so
        /// the run resumes already aimed straight rather than leaning into a wall.
        void BeginResume()
        {
            if (!_pause.BeginResume()) return;
            _faceInput?.Steering.Reset();
            ApplyPhase();
        }

        /// FinishResume is asked, not told: if the phase moved on under a late callback the menu
        /// stays up rather than leaving the game frozen with nothing on screen to unfreeze it.
        void Resume()
        {
            if (!_pause.FinishResume()) return;
            ClosePauseMenu();
            ApplyPhase();
        }

        /// RESTART RUN from the pause menu. StartRun is called straight out, deliberately, and does
        /// NOT go through RunSession's RestartGate: that gate releases a run that has ENDED, and
        /// this run is still IsRunning - it is only frozen.
        ///
        /// What keeps the tap off the restarted run's first frame is the pause menu's own frame
        /// counter plus the fact that RunSession keeps ticking input while Frozen. The tap lands on
        /// frame N; RunSession's Update - order 100, so after the EventSystem and after PauseMenu -
        /// reads and discards it that same frame because the session is frozen; PauseMenu only
        /// invokes this on frame N+1 or later, by which time TouchPhase.Ended is gone. In camera
        /// mode the wait is seconds long and ends on an arbitrary frame, so PauseMenu re-arms the
        /// same counter when staging completes rather than firing on the frame it happens to land
        /// on - see the Ready case there.
        void RestartRun()
        {
            if (!_pause.FinishResume()) return;
            ClosePauseMenu();
            ApplyPhase();
            _session.StartRun();
        }

        /// The camera never came back. The run finishes on tilt+touch: the rig goes, the input is
        /// rebuilt without it, and the pause menu stays up so the player resumes when ready.
        void DropCameraMode()
        {
            _pause.DropCameraMode();
            DestroyRig();
            _session.Input = BuildInput(false);
            ApplyPhase();
            ShowOverlay(false); // nothing left to show a face position from
        }

        // ---- run -> menu ----

        /// Leaving a run: the loop stops, the world empties, the camera is released for good and
        /// the mode picker comes back exactly as it looks at launch. Nothing is scored - an
        /// abandoned run is not a result, so the all-time and daily bests are untouched. (A run
        /// that reached the result screen has already been scored by Crash(), so leaving from
        /// there keeps the best it just set.)
        ///
        /// Three ways in, all landing here: the pause menu's QUIT TO MENU, the result screen's,
        /// and back on the result screen. Stop() is what makes the result screen's button safe -
        /// see the comment on it in RunHud.
        void QuitToMenu()
        {
            ClosePauseMenu();
            DestroyRig();
            _pause.BeginRun(false);
            ApplyPhase();
            _session.Stop();
            ShowMenu();
        }

        // ---- first-run guide ----

        /// First launch, or the "how to play" link on the picker. Idempotent: a second request
        /// while it is already up is a no-op rather than a second canvas.
        void ShowGuide()
        {
            if (_guide != null) return;
            _guide = FirstRunGuide.Show();
            _guide.Closed += CloseGuide;
        }

        void CloseGuide()
        {
            if (_guide == null) return;
            // Hidden before it is destroyed, like the pause menu: Destroy only lands at the end
            // of the frame.
            _guide.gameObject.SetActive(false);
            Destroy(_guide.gameObject);
            _guide = null;
        }

        // ---- back button ----

        void Update()
        {
            TickCameraOutage();

            // Android delivers the back button as Escape (and so does the Editor, which is how
            // this gets exercised without a phone).
            //
            // Everything back can mean is decided by one table, including the two screens that
            // are not a run: the guide (which back closes) and the mode picker (where there is
            // no run to go back from). The early return that used to live here would have made
            // back fall through the guide whenever it was opened from the picker.
            if (!UnityEngine.Input.GetKeyDown(KeyCode.Escape)) return;

            switch (PauseState.BackFor(_pause.Phase, _session.IsRunning, StorePanel.IsOpen,
                        _guide != null, _session.enabled,
                        _menu != null && _menu.IsStagingCamera))
            {
                case BackAction.CloseGuide:
                    _guide.Dismiss();
                    break;
                case BackAction.Pause:
                    RequestPause();
                    break;
                case BackAction.Resume:
                    if (_pauseMenu != null) _pauseMenu.RequestResume();
                    break;
                case BackAction.QuitToMenu:
                    QuitToMenu();
                    break;
                case BackAction.CancelStaging:
                    _menu.CancelStaging();
                    break;
            }
        }

        // ---- the camera going away mid-run ----

        /// A camera-mode run whose camera has stopped answering PAUSES ITSELF, and the pause menu
        /// takes it from there: its resume staging is already the "stand where the phone can see
        /// you…" prompt over the framing overlay, and its CameraGaveUp path already finishes the
        /// run on tilt+touch when the camera genuinely will not come back (rule 3). So this is only
        /// the trigger; nothing new is on screen because of it.
        ///
        /// Before it, a rig that reached Failed - or a player who walked out of shot - left the
        /// steering axis frozen at its last value for the rest of the run (the loss contract on
        /// FaceSteering holds the lane rather than recentring, which is right for a blur gap and
        /// wrong for an outage), with nothing to recover it. It also bounds the quirk that
        /// documented that hold as uncapped: a lane now survives at most one outage window.
        ///
        /// Three facts, one decision, all of the arithmetic engine-free in CameraOutage:
        ///   * live - a camera run that is actually moving. Not frozen (the pause menu re-acquires
        ///     the camera itself and must not be pause-spammed), not the result screen, and never a
        ///     tilt run;
        ///   * hasPosition - FaceSteering's own answer, so there is one definition of "lost" in the
        ///     build. It already covers the rig not tracking and the stale-observation cutoff,
        ///     because CameraFaceInput submits score 0 in both cases;
        ///   * Failed - terminal, so it does not wait out the window.
        /// Read one frame behind the session (this Update is order 0, RunSession's is 100), which
        /// costs a frame out of 1.75 s.
        void TickCameraOutage()
        {
            bool live = _pause.CameraMode && !_pause.IsFrozen &&
                        _session.enabled && _session.IsRunning;
            bool failed = _rig != null && _rig.State == FaceTrackingRig.RigState.Failed;
            bool hasPosition = _faceInput != null && _faceInput.Steering.HasPosition;

            if (!_outage.Tick(Time.unscaledDeltaTime, live, hasPosition, failed)) return;

            Debug.Log("[CAM] auto-pause: " + (failed
                ? "rig failed — " + (_rig != null ? _rig.FailReason : "unknown")
                : "no usable face position for " + CameraOutage.PauseAfterSeconds + "s"));
            RequestPause();
        }

        /// The overlay's one piece of plumbing: which lane the runner is actually committed to.
        /// Everything else it shows comes from the input adapter it was handed.
        void LateUpdate()
        {
            if (!SyncOverlay()) return;
            if (_session.Runner == null) return;
            _overlay.ReportLane(_session.Runner.Lane);
        }

        /// Whether the face panel should be on screen at all, checked every frame rather than only
        /// when the phase changes. Returns true while it is up.
        ///
        /// Two states hide it, and only one of them is a phase:
        ///   * frozen - the pause menu puts up its own, larger copy of the panel while it
        ///     re-acquires the camera, so the HUD one steps aside rather than sitting there showing
        ///     a lost face;
        ///   * the session not running - the crash/result screen. Nothing calls ApplyPhase on a
        ///     crash (the phase is still Running: a run that ended is not a run that paused), so a
        ///     visibility rule that lived only there left the stickman floating over the result card
        ///     - the overlay canvas is sortingOrder 50 and the result card is on the HUD's own
        ///     canvas at 0, so it drew on top of the score the player had just earned (PR #5
        ///     review). Checking it here is what makes the rule hold for every way a run can stop.
        /// It comes back by itself when the next run starts, including a RUN AGAIN restart, which
        /// goes straight to RunSession and never reaches this class.
        bool SyncOverlay()
        {
            if (_overlay == null) return false;
            bool visible = _session.IsRunning && !_pause.IsFrozen;
            if (_overlay.gameObject.activeSelf != visible) _overlay.gameObject.SetActive(visible);
            return visible;
        }

        // ---- one place that acts on the phase ----

        /// Freezing, and holding the camera, follow from the phase and nothing else. Running it
        /// through one method means a stray exception cannot leave the game stopped at
        /// timeScale 0, or leave the camera live behind a menu.
        void ApplyPhase()
        {
            Time.timeScale = _pause.IsFrozen ? 0f : 1f;
            _session.Frozen = _pause.IsFrozen;

            // Applied here as well as every frame in LateUpdate, so a phase change lands on the
            // same frame it happens rather than one later. SyncOverlay is the only place that
            // decides this.
            SyncOverlay();

            if (_rig == null) return;
            if (_pause.HoldsCamera) _rig.Begin(); // no-op unless the rig is idle
            else _rig.Suspend();
        }

        void ClosePauseMenu()
        {
            if (_pauseMenu == null) return;
            // Hidden before it is destroyed: Destroy only lands at the end of the frame, and a
            // pause menu drawn over the screen it just handed off to is a visible flicker.
            _pauseMenu.gameObject.SetActive(false);
            Destroy(_pauseMenu.gameObject);
            _pauseMenu = null;
        }

        void DestroyRig()
        {
            _faceInput = null;
            DismissOverlay();
            if (_rig == null) return;
            Destroy(_rig.gameObject); // OnDestroy disposes the camera and the model
            _rig = null;
        }

        void OnDestroy()
        {
            DismissOverlay();
            if (_hud != null)
            {
                _hud.PauseRequested -= RequestPause;
                _hud.QuitRequested -= QuitToMenu;
            }
            Time.timeScale = 1f;
        }
    }
}
