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
    /// (rule 3).
    public sealed class RunFlow : MonoBehaviour
    {
        readonly PauseState _pause = new PauseState();

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

        /// The overlay's one piece of plumbing: which lane the runner is actually committed to.
        /// Everything else it shows comes from the input adapter it was handed.
        void LateUpdate()
        {
            if (_overlay == null || _session.Runner == null) return;
            _overlay.ReportLane(_session.Runner.Lane);
        }

        // ---- one place that acts on the phase ----

        /// Freezing, and holding the camera, follow from the phase and nothing else. Running it
        /// through one method means a stray exception cannot leave the game stopped at
        /// timeScale 0, or leave the camera live behind a menu.
        void ApplyPhase()
        {
            Time.timeScale = _pause.IsFrozen ? 0f : 1f;
            _session.Frozen = _pause.IsFrozen;

            // The pause menu puts up its own, larger copy of the panel while it re-acquires the
            // camera, so the HUD one steps aside rather than sitting there showing a lost face.
            if (_overlay != null) _overlay.gameObject.SetActive(!_pause.IsFrozen);

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
