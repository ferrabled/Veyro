using System;
using MotionRunner.CameraInput;
using MotionRunner.Core;
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
    /// The menu is up while the game is frozen at Time.timeScale = 0, so everything here counts
    /// in unscaled time and nothing here waits on a coroutine.
    public sealed class PauseMenu : MonoBehaviour
    {
        static readonly Color TextColor = new Color(0.94f, 0.96f, 1f);
        static readonly Color DimColor = new Color(0.04f, 0.05f, 0.09f, 0.82f);
        static readonly Color PanelColor = new Color(0.11f, 0.13f, 0.20f, 0.96f);
        static readonly Color AccentColor = new Color(1f, 0.55f, 0.15f);
        static readonly Color SecondaryColor = new Color(0.24f, 0.28f, 0.40f);
        static readonly Color StatusColor = new Color(0.72f, 0.76f, 0.85f);

        const string IdleHint = "back button resumes";

        /// What the player asked for. Raised one frame after the tap at the earliest, and only
        /// once the camera (if any) is back.
        public event Action ResumeRequested;
        public event Action RestartRequested;
        public event Action QuitRequested;

        /// Raised the moment a resume or restart is asked for, before anything is ready: RunFlow
        /// turns the camera back on off the back of this.
        public event Action ResumeStarted;

        /// The camera did not come back. RunFlow rebuilds the run on tilt+touch; the menu stays
        /// up so the player chooses when to go.
        public event Action CameraGaveUp;

        enum Pending
        {
            None,
            Resume,
            Restart
        }

        Text _status;
        Button _resumeButton;
        Button _restartButton;
        FaceTrackingRig _rig;
        CameraStaging _staging;
        FaceOverlay _overlay;
        Pending _pending;
        int _pendingFrame = -1;

        /// rig is null in tilt mode, and is dropped here when camera mode gives up.
        public static PauseMenu Show(FaceTrackingRig rig)
        {
            var go = new GameObject("PauseMenu");
            var menu = go.AddComponent<PauseMenu>();
            menu._rig = rig;
            menu.Build();
            return menu;
        }

        /// The back button, routed here so it does exactly what the RESUME button does.
        public void RequestResume() => Begin(Pending.Resume);

        /// Back out of a re-acquisition: the camera goes off again and the menu returns to rest.
        public void CancelResume()
        {
            _pending = Pending.None;
            _staging = null;
            DismissOverlay();
            SetButtonsInteractable(true);
            _status.text = IdleHint;
        }

        void Build()
        {
            RuntimeUi.PortraitCanvas(gameObject, 150); // above the HUD, below the store

            var panel = RuntimeUi.FullScreenPanel("Panel", transform, DimColor);
            var card = RuntimeUi.Card("Card", panel.transform, new Vector2(760f, 900f), PanelColor);

            RuntimeUi.Label("Title", card.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24f, -130f), new Vector2(-24f, -40f),
                56, TextAnchor.UpperCenter, TextColor).text = "PAUSED";

            _status = RuntimeUi.Label("Status", card.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(32f, -330f), new Vector2(-32f, -160f),
                34, TextAnchor.UpperCenter, StatusColor);
            _status.text = IdleHint;

            _resumeButton = RuntimeUi.TextButton("Resume", card.transform,
                new Vector2(0.5f, 0f), new Vector2(0f, 430f), new Vector2(520f, 140f),
                AccentColor, "RESUME", 52, new Color(0.08f, 0.06f, 0.04f),
                () => Begin(Pending.Resume));

            _restartButton = RuntimeUi.TextButton("Restart", card.transform,
                new Vector2(0.5f, 0f), new Vector2(0f, 270f), new Vector2(480f, 110f),
                SecondaryColor, "RESTART RUN", 42, TextColor,
                () => Begin(Pending.Restart));

            // Quit never waits on the camera: getting out is the one thing that must always
            // work on the first tap.
            RuntimeUi.TextButton("Quit", card.transform,
                new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(480f, 110f),
                SecondaryColor, "QUIT TO MENU", 42, TextColor,
                () => QuitRequested?.Invoke());
        }

        /// A tap on RESUME or RESTART never acts on the frame it arrives: TouchTapInput reports a
        /// jump on the same TouchPhase.Ended that fires the button, so resuming here would hand
        /// that tap straight to the runner (the bug that made the store tap restart the run,
        /// found on device 27 Aug). The frame check is what makes that a rule rather than a
        /// hope - Update and the EventSystem run in the same phase, in no fixed order.
        ///
        /// In camera mode the tap is only half of it: the wait then runs for seconds and ends on an
        /// arbitrary frame, which is a second chance to land on somebody's tap. The counter is
        /// re-armed when staging completes for exactly that reason - see the Ready case in Update.
        void Begin(Pending pending)
        {
            if (_pending != Pending.None) return;
            _pending = pending;
            _pendingFrame = Time.frameCount;

            if (_rig != null)
            {
                _staging = new CameraStaging(_rig);
                SetButtonsInteractable(false);
                _status.text = "getting the camera back…";

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

            if (_staging != null)
            {
                switch (_staging.Poll())
                {
                    case CameraStaging.Stage.Waiting:
                        _status.text = _staging.Status;
                        return;

                    case CameraStaging.Stage.Failed:
                        _status.text = _staging.Status + "\n— finishing this run on tilt & touch";
                        _staging = null;
                        _rig = null;
                        _pending = Pending.None;
                        DismissOverlay();
                        SetButtonsInteractable(true);
                        CameraGaveUp?.Invoke();
                        return;

                    case CameraStaging.Stage.Ready:
                        // The camera is back - but re-arm the frame counter rather than resuming on
                        // this frame. The tap that started the wait is seconds old, so the frame
                        // staging lands on is arbitrary and can be the frame of any other tap the
                        // player happens to make; resuming here unfreezes the run before
                        // RunSession's own Update (order 100) has read that tap, and hands it over
                        // as a first-frame jump. Exactly the hazard ModeSelectMenu.BeginPick closes
                        // for the picker's camera handover, and the reason _pendingFrame cannot
                        // just be the tap's frame.
                        _staging = null;
                        _pendingFrame = Time.frameCount;
                        return;
                }
            }

            Pending done = _pending;
            _pending = Pending.None;
            _staging = null;
            DismissOverlay();

            if (done == Pending.Resume) ResumeRequested?.Invoke();
            else RestartRequested?.Invoke();
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

        void OnDestroy() => DismissOverlay();
    }
}
