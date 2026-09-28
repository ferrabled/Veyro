using System;
using MotionRunner.Audio;
using MotionRunner.CameraInput;
using MotionRunner.Core;
using MotionRunner.Track;
using UnityEngine;
using UnityEngine.UI;

namespace MotionRunner.Gameplay
{
    /// The pause screen: resume, restart, or leave for the mode picker. Code-built on the shared
    /// RuntimeUi plumbing like every other screen (CLAUDE.md rule 1).
    ///
    /// In camera mode pausing has released the camera, so both RESUME and RESTART have to put
    /// the player back in front of it first. That staging is FaceTrackingRig's own state machine
    /// read through CameraStaging - the same words, in the same order, as the mode picker at
    /// launch. If it never gets there, the run drops to tilt+touch instead of stranding the
    /// player behind a camera that will not come back (CLAUDE.md rule 3).
    ///
    /// Since the hands-free flow (2026-09-01) a camera resume is three waits, not one:
    ///   camera back -> raise your right hand (or tap RESUME) -> 3-2-1 -> run.
    /// The gesture is the whole point of the mode - the player walked away from the phone, so
    /// asking them to walk back and tap it defeats hands-free - but it AUGMENTS, never gates
    /// (rule 3): both buttons come back the moment the face is found, so a player whose pose
    /// never detects resumes by touch, and Android back cancels at every step. The countdown
    /// exists for FaceSteering as much as for the player: its neutral was reset at BeginResume,
    /// and three seconds of standing still is what it calibrates on, so the run resumes aimed
    /// straight instead of leaning into a wall.
    ///
    /// One asymmetry runs through all of it: a resume the player asked for has already been
    /// confirmed by the tap that asked, while a resume the GAME asked for (BeginAutoResume, off a
    /// camera outage) has been confirmed by nobody. So when the gesture is unavailable the first
    /// may count down on the strength of that tap and the second may not - it comes back to the
    /// card and asks. ResumeConfirm is that rule, engine-free and pinned in tests.
    ///
    /// The menu is up while the game is frozen at Time.timeScale = 0, so everything here counts
    /// in unscaled time and nothing here waits on a coroutine. The BlazePose probe behind the
    /// gesture lives on the rig and is affordable for exactly the same reason - see
    /// PoseGestureProbe; it is disposed the moment the countdown starts.
    public sealed class PauseMenu : MonoBehaviour
    {
        static readonly Color TextColor = Menu.MenuTheme.Text;
        /// The park's ink, like the result card's and the guide's scrims.
        static readonly Color DimColor = new Color(Art.ParkTheme.Ink.r, Art.ParkTheme.Ink.g, Art.ParkTheme.Ink.b, 0.84f);
        static readonly Color PanelColor = Menu.MenuTheme.Card;
        static readonly Color AccentColor = Menu.MenuTheme.Accent;
        static readonly Color SecondaryColor = Menu.MenuTheme.Slot;
        static readonly Color StatusColor = Menu.MenuTheme.Dim;

        const string IdleHint = "back button resumes";
        const string GesturePrompt = "raise your right hand when ready\n— or tap RESUME";
        const string GestureHoldHint = "hold it…";
        const string TouchOnlyPrompt = "tap RESUME when ready";
        const string CountdownHint = "get ready…";
        const string FaceLostReason = "couldn't see you — paused";

        /// How long the countdown tolerates the staging losing the face before it cancels back to
        /// the paused card. CameraStaging demands a fresh CONFIDENT observation, and a single
        /// blurred frame fails that bar - a player settling into their lane must not have the
        /// countdown yanked away by one soft frame. 0.75 s is a couple of real detector losses,
        /// well past the blur regime (FaceSteering carries positions for 0.30 s) and still short
        /// enough that a player who genuinely walked off does not get an unattended unfreeze.
        const float CountdownFaceGraceSeconds = 0.75f;

        /// What the player asked for. Raised one frame after the tap at the earliest, and only
        /// once the camera (if any) is back and the countdown has run out.
        public event Action ResumeRequested;
        public event Action RestartRequested;
        public event Action QuitRequested;

        /// Raised the moment a resume or restart is asked for, before anything is ready: RunFlow
        /// turns the camera back on off the back of this.
        public event Action ResumeStarted;

        /// The countdown lost the player mid-count. RunFlow routes it through RequestPause - the
        /// same path as the back button - so the phase walks back to Paused through the one
        /// place that owns it (ApplyPhase stays the only timeScale writer).
        public event Action ResumeCancelled;

        /// The camera did not come back. RunFlow rebuilds the run on tilt+touch; the menu stays
        /// up so the player chooses when to go.
        public event Action CameraGaveUp;

        enum Pending
        {
            None,
            Resume,
            Restart
        }

        /// Which of the camera resume's three waits is on screen. None for tilt, and for the
        /// one-frame gap between the countdown finishing and the resume firing.
        enum Wait
        {
            None,
            Camera,

            /// Waiting to be confirmed. Usually "raise your right hand — or tap RESUME"; with no
            /// working probe (it never loaded, or it died mid-wait) the same wait runs on touch
            /// alone, which is why this is the confirm step and not literally the gesture.
            Gesture,
            Countdown
        }

        Text _status;
        Text _countdownDigits;
        GameObject _panel;
        Button _resumeButton;
        Button _restartButton;
        FaceTrackingRig _rig;
        CameraStaging _staging;
        FaceOverlay _overlay;
        Pending _pending;
        Wait _wait;

        /// Whether the pending resume was asked for by the player or by the game. It decides one
        /// thing only, and it is the one thing that must never be got wrong: what happens at the
        /// end of the camera wait when there is no working gesture probe. See ResumeConfirm.
        ResumeOrigin _origin;

        /// Why the game paused itself, prefixed onto whatever the card says next. Cleared the
        /// moment the face is found again — a resolved reason narrating a live staging is stale
        /// news over the line that matters.
        string _reason;
        readonly ResumeCountdown _countdown = new ResumeCountdown();
        float _faceUnseenFor;
        int _pendingFrame = -1;

        /// The numeral last ticked for. The tick plays on each change of DisplayDigit (3, 2, 1)
        /// and the go on the frame the count completes - so the sound and the drawn digit are
        /// the same edge, never one frame apart.
        int _tickedDigit;

        /// The volume rows under QUIT: the space they take from the card's bottom edge to QUIT
        /// (the rows' band, its margins and the gap), and the band itself.
        const float SoundRowsHeight = 250f;
        const float SoundRowsBand = 210f;
        const float SoundLabelWidth = 160f;

        /// rig is null in tilt mode, and is dropped here when camera mode gives up.
        public static PauseMenu Show(FaceTrackingRig rig)
        {
            var go = new GameObject("PauseMenu");
            var menu = go.AddComponent<PauseMenu>();
            menu._rig = rig;
            menu.Build();
            return menu;
        }

        /// The back button, routed here so it does exactly what the RESUME button does — a
        /// deliberate player action, so it confirms the resume like the button does.
        public void RequestResume() => Begin(Pending.Resume, ResumeOrigin.User);

        /// Back out of a re-acquisition: the camera goes off again and the menu returns to rest.
        /// Every wait ends here - the probe, the countdown and the staging all go, whichever of
        /// them was up. An auto-pause reason survives the cancel: backing out of the recovery
        /// does not change why the game stopped.
        public void CancelResume()
        {
            _rig?.EndGestureProbe();
            _countdown.Cancel();
            _countdownDigits.gameObject.SetActive(false);
            _panel.SetActive(true); // the card comes back from wherever the count had put it
            _pending = Pending.None;
            _wait = Wait.None;
            _staging = null;
            _faceUnseenFor = 0f;
            DismissOverlay();
            SetButtonsInteractable(true);
            _status.text = WithReason(IdleHint);
        }

        /// Why the game paused itself, written where the player is already looking. The card
        /// otherwise says nothing after an auto-pause, and "the game stopped and will not say
        /// why" is the wrong first impression for a mode that already asks a lot (Feature A's
        /// missing status line).
        public void ShowAutoPauseReason(string reason)
        {
            _reason = reason;
            if (_pending != Pending.None) return; // a wait is already narrating the screen
            _status.text = WithReason(IdleHint);
        }

        /// A camera outage paused the run: go straight back into the resume staging rather than
        /// sitting on an idle card. The player is by definition away from the phone — that is
        /// what an outage IS — so asking them to walk over and tap RESUME before the camera even
        /// starts looking for them defeats the hands-free loop (acceptance criterion 1:
        /// auto-pause → framing overlay → re-enter → raise your hand, no touch anywhere). The
        /// reason rides along on top of the staging copy, and every existing way out still works:
        /// back cancels to the idle card, QUIT leaves, a camera that will not come back drops to
        /// tilt through the same Failed path.
        ///
        /// Deliberately NOT used for app-background pauses (RunFlow.OnApplicationPause): those
        /// land on the idle card, because auto-starting a camera the moment the app comes back is
        /// presumptuous where the player pressed home, and plain wrong where the run is a tilt
        /// one — an auto-begun tilt resume would unfreeze the run the instant the app returned.
        ///
        /// What separates this from the button, all the way through the staging, is that NOBODY
        /// HAS CONFIRMED ANYTHING yet: the tap that normally starts a resume is the confirmation,
        /// and there was no tap. So a resume begun here may only ever be finished by an explicit
        /// confirm — the raised hand, or a RESUME tap — never by the staging simply reaching
        /// "I can see you". That is ResumeOrigin.Auto, and TickCameraWait is where it pays off.
        public void BeginAutoResume(string reason)
        {
            _reason = reason;
            Begin(Pending.Resume, ResumeOrigin.Auto);
        }

        /// The line the card shows, with the auto-pause reason above it while one is standing.
        string WithReason(string text) => _reason == null ? text : _reason + "\n" + text;

        void Build()
        {
            RuntimeUi.PortraitCanvas(gameObject, 150); // above the HUD, below the store

            _panel = RuntimeUi.FullScreenPanel("Panel", transform, DimColor);

            // The two volume rows take the old 75 px toggle strip under QUIT plus `lift` more: the
            // card grows by exactly `lift` and the buttons rise with its bottom edge, so every
            // gap above them - status line to RESUME included - is what it always was. Rounded,
            // like the result card and the guide, rather than the old square slab.
            float lift = SoundRowsHeight - 75f;
            RuntimeUi.Element("Card", _panel.transform, out var cardRect);
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(760f, 900f + lift);
            Menu.CosmeticUi.Surface(cardRect, PanelColor, 44f);
            var card = cardRect.gameObject;

            RuntimeUi.Label("Title", card.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24f, -130f), new Vector2(-24f, -40f),
                56, TextAnchor.UpperCenter, TextColor).text = "PAUSED";

            _status = RuntimeUi.Label("Status", card.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(32f, -330f), new Vector2(-32f, -160f),
                34, TextAnchor.UpperCenter, StatusColor);
            _status.text = IdleHint;

            _resumeButton = RoundButton("Resume", card.transform, 430f + lift, new Vector2(520f, 140f),
                AccentColor, "RESUME", 52, Menu.MenuTheme.OnAccent,
                () => Begin(Pending.Resume, ResumeOrigin.User));

            _restartButton = RoundButton("Restart", card.transform, 270f + lift, new Vector2(480f, 110f),
                SecondaryColor, "RESTART RUN", 42, TextColor,
                () => Begin(Pending.Restart, ResumeOrigin.User));

            // Quit never waits on the camera: getting out is the one thing that must always
            // work on the first tap.
            RoundButton("Quit", card.transform, 130f + lift, new Vector2(480f, 110f),
                SecondaryColor, "QUIT TO MENU", 42, TextColor,
                () => QuitRequested?.Invoke(), Sfx.UiBack);

            BuildSoundRows(card.transform);

            // The 3-2-1 numeral, dead centre where no phone bezel or thumb hides it. A sibling of
            // the panel, NOT a child: the whole card steps aside for the count (owner call,
            // 2026-09-02 device session) so the player is looking at the frozen run they are
            // about to rejoin, and the numeral has to survive the panel being hidden.
            _countdownDigits = RuntimeUi.Label("Countdown", transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-300f, -300f), new Vector2(300f, 300f),
                400, TextAnchor.MiddleCenter, Art.ParkTheme.Paper);
            var countdownOutline = _countdownDigits.gameObject.AddComponent<UnityEngine.UI.Outline>();
            countdownOutline.effectColor = Art.ParkTheme.Ink;
            countdownOutline.effectDistance = new Vector2(4f, -4f);
            _countdownDigits.gameObject.SetActive(false);
        }

        /// MUSIC and SOUNDS under QUIT: the same VolumeControl rows as the profile tab - drag or tap
        /// a level, ON/OFF to mute - so a player who paused because the room went quiet (or
        /// loud) sets it right here, without leaving the run. The pause is the one place a level
        /// can be judged against the run's own music, which is playing, ducked, underneath.
        /// Skipped entirely when there is no audio instance (tests, tooling).
        void BuildSoundRows(Transform card)
        {
            if (GameAudio.Instance == null) return;
            Func<SoundSettings> settings = () => GameAudio.Instance != null ? GameAudio.Instance.Settings : null;

            RuntimeUi.Element("SoundRows", card, out var rows);
            RuntimeUi.Stretch(rows, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 16f), new Vector2(0f, 16f + SoundRowsBand));

            var line = RuntimeUi.Element("Divider", rows, out var lineRect);
            RuntimeUi.Stretch(lineRect, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(32f, 0f), new Vector2(-32f, 2f));
            line.AddComponent<Image>().color = Menu.MenuTheme.Empty;

            // Kept alive by their own scale's input handlers; nothing here needs them again.
            new Menu.VolumeControl(Row(rows, "Music", 1), "MUSIC",
                Menu.VolumeControl.Channel.Music, settings, SoundLabelWidth, 32f);
            new Menu.VolumeControl(Row(rows, "Sounds", 0), "SOUNDS",
                Menu.VolumeControl.Channel.Sounds, settings, SoundLabelWidth, 32f);
        }

        /// A rounded, filled button centred `y` px above the card's bottom edge - RunHud's
        /// CardButton shape, so RESUME here and RUN AGAIN on the result card are one family.
        static Button RoundButton(string name, Transform card, float y, Vector2 size, Color fill,
            string label, int fontSize, Color labelColor, Action onTap, Sfx sound = Sfx.UiTap)
        {
            RuntimeUi.Element(name, card, out var rect);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = size;

            var surface = Menu.CosmeticUi.Surface(rect, fill, size.y * 0.32f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = surface;
            button.onClick.AddListener(() => onTap());
            RuntimeUi.TapSound(button, sound);

            RuntimeUi.Label("Label", rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                fontSize, TextAnchor.MiddleCenter, labelColor).text = label;
            return button;
        }

        /// One of the two rows, stacked from the bottom: 0 is SOUNDS, 1 is MUSIC above it.
        static RectTransform Row(RectTransform rows, string name, int index)
        {
            RuntimeUi.Element(name, rows, out var row);
            RuntimeUi.Stretch(row, new Vector2(0f, index * 0.5f), new Vector2(1f, (index + 1) * 0.5f),
                Vector2.zero, Vector2.zero);
            return row;
        }

        /// A tap on RESUME or RESTART never acts on the frame it arrives: TouchTapInput reports a
        /// jump on the same TouchPhase.Ended that fires the button, so resuming here would hand
        /// that tap straight to the runner (the bug that made the store tap restart the run,
        /// found on device 27 Aug). The frame check is what makes that a rule rather than a
        /// hope - Update and the EventSystem run in the same phase, in no fixed order.
        ///
        /// In camera mode the tap is only half of it: the waits then run for seconds and end on
        /// arbitrary frames, which are each a second chance to land on somebody's tap. The
        /// counter is re-armed whenever a wait hands over - staging to gesture, countdown to
        /// done - for exactly that reason.
        ///
        /// While the gesture wait is up, this same method IS the touch confirm: both buttons are
        /// deliberately live there (rule 3 - the gesture augments, it never gates), and a tap
        /// skips the hand-raise and goes straight to the countdown. Only a USER origin may do
        /// that: the shortcut's whole justification is that a tap just happened, so an auto-resume
        /// arriving on top of a confirm wait must not be allowed to spend it as a confirmation.
        void Begin(Pending pending, ResumeOrigin origin)
        {
            if (_wait == Wait.Gesture && origin == ResumeOrigin.User)
            {
                _pending = pending;
                _origin = origin;
                StartCountdown();
                return;
            }

            if (_pending != Pending.None) return;
            _pending = pending;
            _origin = origin;
            _pendingFrame = Time.frameCount;

            if (_rig != null)
            {
                _staging = new CameraStaging(_rig);
                _wait = Wait.Camera;
                SetButtonsInteractable(false);
                _status.text = WithReason("getting the camera back…");

                // Above the card, because the card's own rows are full. Standing back in front of
                // the phone is the whole of this wait, so the panel that shows where the camera
                // thinks you are belongs here as much as it does at the picker.
                _overlay = FaceOverlay.Framing(_rig, FaceOverlay.PauseSortingOrder,
                    FaceOverlay.PausePosition);
            }

            ResumeStarted?.Invoke();
        }

        void Update()
        {
            if (_pending == Pending.None || Time.frameCount == _pendingFrame) return;

            switch (_wait)
            {
                case Wait.Camera:
                    TickCameraWait();
                    return;
                case Wait.Gesture:
                    TickGestureWait();
                    return;
                case Wait.Countdown:
                    TickCountdown();
                    return;
            }

            Pending done = _pending;
            _pending = Pending.None;
            _staging = null;
            DismissOverlay();

            // Back on before the handover: RunFlow.Resume ASKS the phase (FinishResume can refuse
            // under a late callback), and a refused resume leaves this menu up - which must not be
            // an invisible card because a countdown hid it. The normal path destroys the whole
            // menu this same frame, so the restore is never seen.
            _panel.SetActive(true);

            if (done == Pending.Resume) ResumeRequested?.Invoke();
            else RestartRequested?.Invoke();
        }

        void TickCameraWait()
        {
            switch (_staging.Poll())
            {
                case CameraStaging.Stage.Waiting:
                    _status.text = WithReason(_staging.Status);
                    return;

                case CameraStaging.Stage.Failed:
                    HandleCameraFailure();
                    return;

                case CameraStaging.Stage.Ready:
                    // The camera is back and the face is held. Never a resume by itself: what
                    // takes it from here is whoever still owes a confirmation, which is exactly
                    // ResumeConfirm's table.
                    //
                    // The probe is asked for FIRST because loading it is the only way to know
                    // whether the gesture is on the table at all (assets missing, a model that
                    // will not run, a load that throws - all answer false, all leave the touch
                    // path untouched: rule 3, the gesture augments and never gates).
                    switch (ResumeConfirm.For(_origin, _rig.BeginGestureProbe()))
                    {
                        case ResumeConfirmStep.Gesture:
                            // The face is found — whatever reason paused the run is resolved, and
                            // from here the gesture prompt is the only line that matters.
                            _reason = null;
                            _wait = Wait.Gesture;
                            _pendingFrame = Time.frameCount;
                            SetButtonsInteractable(true);
                            _status.text = GesturePrompt;
                            _overlay?.ShowRaiseHandHint(true);
                            return;

                        case ResumeConfirmStep.Countdown:
                            // No gesture available, but the player tapped RESUME (or back) to get
                            // here, and that tap was the confirmation. Standing in frame is all
                            // that was left to wait for, so the count runs - the flow never has
                            // fewer ways forward than it had before the gesture existed.
                            _reason = null;
                            StartCountdown();
                            return;

                        default:
                            // No gesture AND no confirmation: the game paused itself and the
                            // probe is not there to be raised at. Counting down here would
                            // unfreeze a run because the player walked back into shot - which is
                            // not a thing anybody asked for. So the card comes back with its
                            // buttons live and asks, and the reason stays on top of the ask
                            // because this player has not read it yet: they were out of frame
                            // for the whole auto-pause. Everything else still works from here -
                            // a tap counts down, back cancels, a camera that dies drops to tilt,
                            // and losing the face walks back to the camera wait, which gives the
                            // probe another chance at the gesture prompt on the way through here.
                            _wait = Wait.Gesture;
                            _pendingFrame = Time.frameCount;
                            SetButtonsInteractable(true);
                            _status.text = WithReason(TouchOnlyPrompt);
                            return;
                    }
            }
        }

        /// The staging keeps being polled underneath the gesture: it is the one definition of
        /// "the camera can see you", and walking out of shot mid-gesture walks this wait back to
        /// the camera one, probe and all.
        void TickGestureWait()
        {
            switch (_staging.Poll())
            {
                case CameraStaging.Stage.Waiting:
                    _rig.EndGestureProbe();
                    _overlay?.ShowRaiseHandHint(false);
                    _wait = Wait.Camera;
                    SetButtonsInteractable(false);
                    _status.text = WithReason(_staging.Status);
                    return;

                case CameraStaging.Stage.Failed:
                    HandleCameraFailure();
                    return;
            }

            PoseGestureProbe probe = _rig.GestureProbe;
            if (probe == null || !probe.IsRunning)
            {
                // Either the probe died mid-wait (it logs why) or it never loaded and this wait
                // is the auto-resume's touch confirm. Both mean the same thing: the touch path
                // carries on alone, and nothing auto-resumes, because a confirm the player never
                // gave is not one. Note this is the ONE rule that does not split on
                // ResumeOrigin - a user-initiated resume does not fall through to a countdown
                // here either, because the player is standing in front of the phone watching a
                // prompt, and pulling the count out from under them mid-hold reads as the game
                // resuming by itself. The reason line only exists on the auto path.
                _status.text = WithReason(TouchOnlyPrompt);
                return;
            }

            if (probe.Confirmed)
            {
                StartCountdown();
                return;
            }

            // "hold it…" for as long as any evidence is standing, not only on frames the rule
            // agreed: the lite landmarker drops the overhead wrist for the odd sample, and a
            // prompt that flips back to "raise your right hand" against an arm that never moved
            // reads as a broken gesture (the 2026-09-02 device complaint). The streak now winds
            // down through misses instead of resetting, so streak > 0 is exactly "a hold is in
            // progress".
            _status.text = probe.HandRaisedNow || probe.Streak > 0 ? GestureHoldHint : GesturePrompt;
        }

        /// Confirmed - by hand or by tap. The probe goes FIRST (the design rule: the heavy model
        /// is disposed the moment the countdown starts, so it is provably never alive when the
        /// world unfreezes), then three seconds of numerals while FaceSteering calibrates on a
        /// player standing still.
        ///
        /// The card steps ASIDE for the count (owner call from the 2026-09-02 device session):
        /// the countdown belongs to the run the player is about to rejoin, so what is on screen
        /// is the frozen playground, the framing overlay they are settling into a lane with, and
        /// the numeral - not a menu. Hiding the card takes QUIT with it, so the touch cancels
        /// mid-count are Android back and the HUD's own pause button (both land on
        /// RunFlow.RequestPause -> CancelResume, which puts the card back); face loss cancels the
        /// same way on its own.
        void StartCountdown()
        {
            _rig.EndGestureProbe();
            _overlay?.ShowRaiseHandHint(false);
            _wait = Wait.Countdown;
            _faceUnseenFor = 0f;
            SetButtonsInteractable(false);
            _countdown.Begin();
            _status.text = CountdownHint; // waiting on the card if the count is cancelled back to it
            _panel.SetActive(false);
            _countdownDigits.text = _countdown.DisplayDigit.ToString();
            _countdownDigits.gameObject.SetActive(true);
            _tickedDigit = _countdown.DisplayDigit;
            GameAudio.Play(Sfx.CountdownTick); // "3"
        }

        void TickCountdown()
        {
            CameraStaging.Stage stage = _staging.Poll();
            if (stage == CameraStaging.Stage.Failed)
            {
                HandleCameraFailure();
                return;
            }

            // Losing the face mid-count cancels back to the paused card rather than regressing
            // to the camera wait: a countdown that silently re-arms itself can unfreeze the run
            // while nobody is standing in frame, which is the auto-pause's bug re-introduced at
            // the worst moment. Through ResumeCancelled -> RunFlow.RequestPause, the same road
            // the back button takes, so the phase and the camera are wound back by the one
            // place that owns them.
            _faceUnseenFor = stage == CameraStaging.Stage.Ready
                ? 0f
                : _faceUnseenFor + Time.unscaledDeltaTime;
            if (_faceUnseenFor > CountdownFaceGraceSeconds)
            {
                _countdown.Cancel();
                _countdownDigits.gameObject.SetActive(false);
                ResumeCancelled?.Invoke(); // -> RequestPause -> CancelResume resets this menu
                ShowAutoPauseReason(FaceLostReason);
                return;
            }

            if (_countdown.Tick(Time.unscaledDeltaTime))
            {
                // Done - but never fire on the frame the count happens to end on: the re-armed
                // counter gives RunSession one frozen frame to swallow any tap in flight,
                // exactly like the staging handover above.
                _countdownDigits.gameObject.SetActive(false);
                _wait = Wait.None;
                _pendingFrame = Time.frameCount;
                GameAudio.Play(Sfx.CountdownGo);
                return;
            }

            int digit = _countdown.DisplayDigit;
            if (digit != _tickedDigit)
            {
                _tickedDigit = digit;
                GameAudio.Play(Sfx.CountdownTick); // "2", "1"
            }
            _countdownDigits.text = digit.ToString();
        }

        /// The camera is not coming back (CameraStaging.Stage.Failed is the rig's own terminal
        /// verdict). Reached from any of the three waits, so everything any of them put up is
        /// taken down before the run is handed to tilt+touch.
        void HandleCameraFailure()
        {
            _rig.EndGestureProbe();
            _countdown.Cancel();
            _countdownDigits.gameObject.SetActive(false);
            _panel.SetActive(true);
            _reason = null; // the failure line below is the whole story now
            _status.text = _staging.Status + "\n— finishing this run on tilt & touch";
            _staging = null;
            _rig = null;
            _pending = Pending.None;
            _wait = Wait.None;
            DismissOverlay();
            SetButtonsInteractable(true);
            CameraGaveUp?.Invoke();
        }

        void SetButtonsInteractable(bool on)
        {
            _resumeButton.interactable = on;
            _restartButton.interactable = on;
        }

        /// The overlay is a root object with its own canvas, so it outlives this screen unless it
        /// is taken down - from every exit, OnDestroy included.
        void DismissOverlay()
        {
            if (_overlay == null) return;
            _overlay.Dismiss();
            _overlay = null;
        }

        /// The probe dies with the menu, not with the rig: quitting mid-wait destroys the rig
        /// (which also ends it), but a menu closed any other way must not leave a pose model
        /// running behind an unfrozen run.
        void OnDestroy()
        {
            _rig?.EndGestureProbe();
            DismissOverlay();
        }
    }
}
