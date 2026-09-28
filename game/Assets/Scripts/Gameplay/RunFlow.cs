using MotionRunner.Audio;
using MotionRunner.CameraInput;
using MotionRunner.Commerce;
using MotionRunner.Core;
using MotionRunner.Inputs;
using MotionRunner.Menu;
using MotionRunner.Notifications;
using MotionRunner.Social;
using MotionRunner.Track;
using UnityEngine;

namespace MotionRunner.Gameplay
{
    /// Everything either side of the run loop: the main menu, the pause menu, the road back to
    /// the menu, and the camera's lifetime across all of it. GameBootstrap builds the world
    /// once and hands it here, so there is exactly one place that knows what starting, pausing
    /// and leaving a run costs.
    ///
    /// It also owns the attract run - the game playing itself behind the menu - for the same
    /// reason it owns the camera: both are things that must be off during a real run and on
    /// outside one, and having two owners of "is the track being driven right now" is how the
    /// menu ends up fighting RunSession for the TrackDirector.
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
        IStore _store;
        SkinService _skins;
        IProfileService _profile;
        FaceTrackingRig _rig;
        CameraFaceInput _faceInput;
        PauseMenu _pauseMenu;
        FirstRunGuide _guide;
        MainMenu _menu;
        AttractRun _attract;
        Camera _gameCamera;
        int _gameCullingMask;
        CameraClearFlags _gameClearFlags;
        FaceOverlay _overlay;
        IPushService _push;
        PushPromptPolicy _pushPrompt;
        NotificationPanel _notificationPanel;
        AnalyticsPanel _analyticsPanel;

        /// The settings panels are mutually exclusive and all close on back; this is the one
        /// question the guards below keep asking. (Sound had a panel too until its levels moved
        /// onto the profile tab itself - ProfileSettingsCard.)
        bool AnyPanelOpen => _notificationPanel != null || _analyticsPanel != null;

        /// The challenge link the next run plays, if any (T-024). Held here rather than read
        /// straight out of PendingChallenge at StartRun, because taking it is what stops one link
        /// from restarting itself every time the menu comes back.
        ChallengeLink _challenge;
        bool _hasChallenge;

        /// The frame the current menu was created on - see TryStartPendingChallenge.
        int _menuFrame = -1;

        public static RunFlow Create(RunSession session, RunHud hud, IStore store, SkinService skins,
            IProfileService profile, IPushService push)
        {
            var go = new GameObject("RunFlow");
            var flow = go.AddComponent<RunFlow>();
            flow._session = session;
            flow._hud = hud;
            flow._store = store;
            flow._skins = skins;
            flow._profile = profile;
            flow._push = push;
            flow._pushPrompt = new PushPromptPolicy(PlayerPrefs.GetInt(PushPromptPolicy.SeenKey, 0) == 1);
            flow._gameCamera = Camera.main;
            if(flow._gameCamera!=null) { flow._gameCullingMask=flow._gameCamera.cullingMask; flow._gameClearFlags=flow._gameCamera.clearFlags; }
            hud.PauseRequested += flow.RequestPause;
            hud.QuitRequested += flow.QuitToMenu;

            // The result screen's store button leaves the run and lands on the shop tab, rather
            // than stacking a modal over a finished run. That reuses the QUIT path, which is the
            // one already proven safe against the tap that opened it also restarting the run
            // behind it (RunHud's note on the Quit button, and the 27 Aug store-tap bug).
            hud.StoreRequested += flow.QuitToShop;

            flow.ShowMenu(MenuTab.Run);
            return flow;
        }

        // ---- menu -> run ----

        void ShowMenu(MenuTab tab)
        {
            _menuFrame = Time.frameCount;
            _menu = MainMenu.Create(_store, _skins, _profile, tab);
            _menu.Chosen += StartRun;
            _menu.GuideRequested += ShowGuide;
            _menu.NotificationsRequested += () => ShowNotifications(false);
            _menu.AnalyticsRequested += ShowAnalytics;

            // The menu loop (T-045). Coming back from a run this crossfades over the run track
            // the crash faded down; at launch it is simply the first thing heard.
            GameAudio.PlayMenuMusic();

            // The home screen now showcases the equipped character. Do not also render/simulate
            // the hidden attract run behind it, especially while a preview camera is active.
            // URP still needs a screen-output camera after the preview RenderTexture cameras.
            // Leaving only offscreen cameras misprojects masked overlay UI on Android.
            if(_gameCamera!=null) { _gameCamera.enabled=true;_gameCamera.cullingMask=0;_gameCamera.clearFlags=CameraClearFlags.SolidColor; }
            if(_session.Runner!=null) _session.Runner.gameObject.SetActive(false);

            // First launch only. It goes up over the menu rather than before it, so the mode
            // choice is made with the controls already explained and the menu is the first
            // thing the player sees behind it.
            if (FirstRunGuide.IsDue) ShowGuide();
        }

        /// The game playing itself behind the menu. Only ever alive while the menu is: it drives
        /// the same TrackDirector and RunnerController a run does, so the two must never overlap.
        void BeginAttract()
        {
            StopAttract();
            if (_session == null) return;
            _attract = AttractRun.Begin(_session.Director, _session.Runner, Camera.main);
        }

        void StopAttract()
        {
            if (_attract == null) return;
            _attract.Stop();
            _attract = null;
        }

        void StartRun(bool cameraMode, FaceTrackingRig rig)
        {
            // A guide left open never survives into a run: camera staging finishes on its own
            // clock, so the run can start while the player is still reading.
            if (_guide != null) _guide.Dismiss();

            // Before the session touches the track: the attract run hands the world back (empty
            // road, runner on its mark, camera at the gameplay framing) and RunSession.StartRun
            // then lays the real seed over it.
            StopAttract();

            if(_gameCamera!=null) { _gameCamera.enabled=true;_gameCamera.cullingMask=_gameCullingMask;_gameCamera.clearFlags=_gameClearFlags; }
            if(_session.Runner!=null) _session.Runner.gameObject.SetActive(true);
            _skins.Apply();
            _menu = null; // it destroys itself on the way out of the pick
            _rig = rig;
            _pause.BeginRun(cameraMode);
            ApplyPhase();

            // Which run this is: a challenge link's track, or the day's. Set on every start, so
            // the run after a challenge is back to the ordinary Daily Run without anything
            // having to remember to put Mode back.
            if (_hasChallenge)
            {
                // Always Free, never Daily: today's track is the one thing every player shares,
                // and a link that could replace it would break the Daily Run for whoever tapped
                // it. A challenge is a rematch, not a daily attempt - it stamps no day and sets
                // no daily best.
                _session.Mode = RunMode.Free;
                _session.BeginChallenge(_challenge.Seed, _challenge.Score);
                _hasChallenge = false;
            }
            else
            {
                _session.Mode = RunMode.Daily;
            }
            // The CHALLENGE banner is RunSession.StartRun's: it is set per run from the seed the
            // run actually uses, so it cannot outlive the challenged track.

            // The control scheme travels into the session because the boards split on it
            // (Feature D, owner call: camera and tilt are separate games). It is fixed at
            // Begin on purpose - a camera run that DropCameraMode lands on tilt+touch still
            // scores as the camera run the player chose to start.
            IGameInput input = BuildInput(cameraMode);

            // Which input may restart a finished run from the result card: the face adapter's
            // hop in camera mode (the player is 2 m from the phone), nothing in tilt mode - the
            // RUN AGAIN button is then the only way. The same CameraFaceInput instance the
            // composite already ticks, so the hop the runner obeys and the hop that restarts are
            // one reading. Cleared again by DropCameraMode.
            _session.RestartGesture = _faceInput;

            _session.Begin(input, cameraMode ? ControlScheme.Camera : ControlScheme.Tilt);
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

        /// Touch and keyboard stay in the mix in camera mode: they cost nothing, let a bystander
        /// jump or pause the hands-free demo without standing in frame, and the RUN AGAIN button
        /// is always there for the result card. What they do NOT do any more is restart a run by
        /// tapping the card: only the face adapter, handed to RunSession.RestartGesture on its
        /// own, may do that with a hop.
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

            // The countdown losing the face cancels through the same door the back button uses:
            // RequestPause walks Resuming back to Paused via ApplyPhase (the one timeScale
            // writer) and CancelResume above puts the menu back to rest.
            _pauseMenu.ResumeCancelled += RequestPause;
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
            _session.RestartSameRun();
        }

        /// The camera never came back. The run finishes on tilt+touch: the rig goes, the input is
        /// rebuilt without it, and the pause menu stays up so the player resumes when ready.
        void DropCameraMode()
        {
            // The session records the drop: the shared board files this run as
            // camera_fallback (standard group) even though the local boards keep scoring it
            // as the camera run the player chose (Feature D vs the 3 Sep owner call).
            if (_pause.DropCameraMode()) _session.NoteCameraDropped();
            DestroyRig();
            _session.Input = BuildInput(false);
            _session.RestartGesture = null; // no camera, no hop: the fallen-back run is button-only
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
        /// Four ways in, all landing here: the pause menu's QUIT TO MENU, the result screen's, the
        /// result screen's SHOP (which only differs in which tab comes up), and back on the result
        /// screen. Stop() is what makes the result screen's buttons safe - see the comment on it
        /// in RunHud.
        void QuitToMenu() => QuitToMenu(MenuTab.Run);

        void QuitToShop() => QuitToMenu(MenuTab.Shop);

        void QuitToMenu(MenuTab tab)
        {
            ClosePauseMenu();
            DestroyRig();
            _pause.BeginRun(false);
            ApplyPhase();
            _session.Stop();

            // The challenge banner belongs to the run that was challenged, not to the HUD.
            if (_hud != null) _hud.SetChallenge(0);

            ShowMenu(tab);
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

        // ---- challenge links (T-024) ----

        /// A challenge link that arrived from outside the game - at launch or while it was
        /// already open - starts its run HERE, and only here: at the menu, with nothing else on
        /// screen. Checked every frame rather than only in ShowMenu, because
        /// Application.deepLinkActivated can fire at any moment, including while the menu is
        /// already up (the player tapped the web page's button with the game in the background).
        ///
        /// Skipped while the camera picker is staging: that flow ends in a run of its own, and
        /// two runs starting in the same frame is the one thing RunFlow exists to prevent. The
        /// link stays pending and is taken on the next visit to the menu.
        ///
        /// The run itself is tilt+touch. A challenge is a link tapped from a chat app, so
        /// starting it must not open with a camera-permission dialog - and camera mode remains
        /// one tap away from the menu for the rematch.
        void TryStartPendingChallenge()
        {
            if (!PendingChallenge.Has) return;
            if (_menu == null || _guide != null || _menu.IsStagingCamera || _menu.HasPendingPick) return;
            // Same rule as the offers below: a modal over the menu owns the screen, so the link
            // waits for the notification / analytics panel to close before starting.
            if (AnyPanelOpen) return;
            // Never on the frame the menu appeared: QuitToMenu runs inside the EventSystem's
            // dispatch and this runs later in the same frame, which would flash the menu for
            // one frame and start a run the player did not ask for at that moment. Waiting a
            // frame also keeps the one-frame deferral every other start path obeys.
            if (Time.frameCount <= _menuFrame) return;
            if (!PendingChallenge.TryTake(out _challenge)) return;

            // The link must describe a track THIS build can generate. A different content
            // version or world would give the same PRNG stream over different chunks - a
            // lookalike run, not the sender's - so it is refused rather than played unfairly.
            if (_challenge.Seed.ContentVersion != ChunkLibrary.ContentVersion ||
                _challenge.Seed.WorldId != _session.WorldId)
            {
                Debug.LogWarning("[DeepLink] challenge refused, built for another version: " + _challenge);
                return;
            }

            _hasChallenge = true;
            Debug.Log("[DeepLink] starting challenge run: " + _challenge);

            // The menu is dismissed the way MainMenu.OnChosen dismisses it - hidden now, destroyed
            // at the end of the frame - because this start does not go through the picker.
            _menu.gameObject.SetActive(false);
            Destroy(_menu.gameObject);

            StartRun(false, null);
        }

        void Update()
        {
            TryStartPendingChallenge();
            TickCameraOutage();
            bool safeForOffer = _menu != null && _menu.Tab == MenuTab.Run &&
                !_menu.IsStagingCamera && _guide == null && !AnyPanelOpen;
            if (GrowthRuntime.Tracker != null && GrowthRuntime.Tracker.DailyEntryPending &&
                _menu != null && !_menu.IsStagingCamera && _guide == null && !AnyPanelOpen)
            {
                _session.Mode = RunMode.Daily;
                _menu.GoHome();
                GrowthRuntime.Tracker.ConsumeDailyEntry();
            }
            if (_pushPrompt.TryOffer(_push.Status, safeForOffer,
                    ProgressStore.Streak.Length > 0, Debug.isDebugBuild))
            {
                PlayerPrefs.SetInt(PushPromptPolicy.SeenKey, 1);
                PlayerPrefs.Save();
                ShowNotifications(Debug.isDebugBuild);
            }

            // Android delivers the back button as Escape (and so does the Editor, which is how
            // this gets exercised without a phone).
            //
            // Everything back can mean is decided by one table, including the two screens that
            // are not a run: the guide (which back closes) and the main menu (where there is
            // no run to go back from, but there may be a tab to come home from). The early return
            // that used to live here would have made back fall through the guide whenever it was
            // opened from the menu.
            if (!UnityEngine.Input.GetKeyDown(KeyCode.Escape)) return;
            if (_analyticsPanel != null) { _analyticsPanel.Close(); return; }
            if (_notificationPanel != null)
            {
                _notificationPanel.Close();
                return;
            }

            switch (PauseState.BackFor(_pause.Phase, _session.IsRunning, MainMenu.IsOpen,
                        _guide != null, _session.enabled,
                        _menu != null && _menu.IsStagingCamera,
                        _menu != null && _menu.IsAwayFromHome))
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
                case BackAction.MenuHome:
                    _menu.GoHome();
                    break;
            }
        }

        // ---- the camera going away mid-run ----

        void ShowNotifications(bool verification)
        {
            if (AnyPanelOpen || _menu == null || _menu.IsStagingCamera || _guide != null) return;
            _notificationPanel = NotificationPanel.Show(_push, verification, () => _notificationPanel = null);
        }

        void ShowAnalytics()
        {
            if (AnyPanelOpen || _menu == null ||
                _menu.IsStagingCamera || _guide != null || GrowthRuntime.Analytics == null) return;
            _analyticsPanel = AnalyticsPanel.Show(GrowthRuntime.Analytics, () => _analyticsPanel = null);
        }

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

            // Straight back into the resume staging, reason on top: the player is away from the
            // phone (that is what the outage means), so the framing overlay goes up and the
            // camera starts looking for them without anyone touching anything — walk back in,
            // raise your hand, 3-2-1 (acceptance criterion 1). A rig that is terminally Failed
            // fails that staging immediately and drops the run to tilt through the existing
            // CameraGaveUp path, which is the fastest honest recovery for that case too.
            if (_pauseMenu != null)
                _pauseMenu.BeginAutoResume(failed
                    ? "camera stopped working — paused"
                    : "couldn't see you — paused");
        }

        /// The app going to the background takes the same path as every other interruption: the
        /// run pauses and the pause menu owns the way back (Feature A). RequestPause's own guards
        /// make this free everywhere it means nothing - no session, mode picker, result screen -
        /// and a background hop DURING a resume wait cancels the wait exactly like the back
        /// button, because Pause() walks Resuming back to Paused. Android also fires this for
        /// the camera-permission dialog, which is the picker's staging (no running session) and
        /// falls through the guards.
        void OnApplicationPause(bool paused)
        {
            if (paused) RequestPause();
        }

        /// The overlay's one piece of plumbing: which lane the runner is actually committed to.
        /// Everything else it shows comes from the input adapter it was handed.
        void LateUpdate()
        {
            if (!SyncOverlay()) return;
            if (_session.Runner == null || !_session.IsRunning) return;
            _overlay.ReportLane(_session.Runner.Lane);
        }

        /// Whether the face panel should be on screen at all, and where, checked every frame
        /// rather than only when the phase changes. Returns true while it is up.
        ///
        /// Hidden while FROZEN: the pause menu puts up its own, larger copy of the panel while it
        /// re-acquires the camera, so the HUD one steps aside rather than sitting there showing a
        /// lost face.
        ///
        /// Hidden when the session is not running UNLESS the camera is still the run's steering
        /// (_faceInput != null: camera mode, and DropCameraMode has not happened). That is the
        /// crash/result screen, where a camera player standing 2 m away needs to see they are in
        /// frame in order to HOP the next run in (owner call, 24 Sep). Nothing calls ApplyPhase
        /// on a crash (the phase is still Running: a run that ended is not a run that paused), so
        /// the rule lives here, checked every frame, which is what makes it hold for every way a
        /// run can stop. The panel is MOVED for that state rather than left where it was: at its
        /// run position (top-centre, y -320) it drew straight over the card's masthead and score
        /// - overlay canvas 50 above the HUD canvas 0 - which is why the PR #5 review had it
        /// hidden there. FaceOverlay.Place parks it above the card's top edge instead, and the
        /// lane bands follow the live axis (no runner is committed to a lane on a result card).
        /// It comes back to its run position by itself when the next run starts, including a
        /// hop or RUN AGAIN restart, which goes straight to RunSession and never reaches this
        /// class.
        bool SyncOverlay()
        {
            if (_overlay == null) return false;
            bool running = _session.IsRunning;
            bool visible = !_pause.IsFrozen && (running || _faceInput != null);
            if (_overlay.gameObject.activeSelf != visible) _overlay.gameObject.SetActive(visible);
            if (visible) _overlay.Place(onResultCard: !running);
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

            // The music follows the phase from the same place the clock does: ducked while the
            // run is frozen, full when it moves. Unscaled inside GameAudio, so the duck lands
            // even though the scaled clock has stopped.
            GameAudio.SetDucked(_pause.IsFrozen);

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
            if (_notificationPanel != null) _notificationPanel.Close();
            if (_analyticsPanel != null) _analyticsPanel.Close();
            DismissOverlay();
            StopAttract();
            if (_hud != null)
            {
                _hud.PauseRequested -= RequestPause;
                _hud.QuitRequested -= QuitToMenu;
                _hud.StoreRequested -= QuitToShop;
            }
            Time.timeScale = 1f;
        }
    }
}
