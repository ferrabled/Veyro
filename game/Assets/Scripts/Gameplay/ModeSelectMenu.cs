using System;
using MotionRunner.CameraInput;
using MotionRunner.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MotionRunner.Gameplay
{
    /// The startup choice between the two control schemes, built entirely from code like RunHud
    /// (CLAUDE.md rule 1). Shown at launch and again whenever a run is left from the pause menu,
    /// so it is the one place the control scheme is picked.
    ///
    /// The camera path walks the FaceTrackingRig's states on-screen (speed gate -> permission ->
    /// camera -> orientation) via the shared CameraStaging, and only hands the run over once a
    /// face has actually been seen, so the player gets "I can see you" feedback before the track
    /// starts moving. Every failure falls back to this menu with the reason shown — camera mode
    /// can never strand the player (CLAUDE.md rule 3: gyro+touch always works).
    public sealed class ModeSelectMenu : MonoBehaviour
    {
        const string InputModeKey = "veyro.input_mode";

        /// What the status line says when nothing is happening — the line the screen opens with,
        /// and the line it must go back to when camera staging is cancelled.
        const string IdleStatus =
            "camera mode plays hands-free:\nlean to steer, hop to jump, crouch to slide";

        /// Raised once with the chosen scheme; the rig is non-null only for camera mode.
        public event Action<bool, FaceTrackingRig> Chosen;

        /// Raised by the "how to play" link. RunFlow owns which screen is up, so this menu only
        /// announces the tap - the same shape as RunHud's StoreRequested.
        public event Action GuideRequested;

        enum Pick
        {
            None,
            Tilt,
            Camera
        }

        GameObject _panel;
        Button _tiltButton;
        Button _cameraButton;
        Text _status;
        FaceTrackingRig _rig;
        CameraStaging _staging;
        FaceOverlay _overlay;
        Pick _pending;
        int _pendingFrame = -1;
        bool _done;

        /// True while the camera is being staged and the two mode buttons are therefore disabled.
        /// The one state back has to be able to undo here: see BackAction.CancelStaging.
        public bool IsStagingCamera => !_done && _pending == Pick.None && _staging != null;

        public static ModeSelectMenu Create()
        {
            var go = new GameObject("ModeSelectMenu");
            var menu = go.AddComponent<ModeSelectMenu>();
            menu.Build();
            return menu;
        }

        void Build()
        {
            // Above the HUD, which the bootstrap has already built.
            RuntimeUi.PortraitCanvas(gameObject, 100);

            _panel = RuntimeUi.FullScreenPanel("Panel", transform, new Color(0.06f, 0.07f, 0.12f, 1f));

            RuntimeUi.Label("Title", _panel.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24f, -420f), new Vector2(-24f, -220f),
                110, TextAnchor.MiddleCenter, new Color(0.94f, 0.96f, 1f)).text = "VEYRO RUN";

            RuntimeUi.Label("Sub", _panel.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24f, -500f), new Vector2(-24f, -430f),
                40, TextAnchor.MiddleCenter, new Color(0.62f, 0.68f, 0.80f)).text = "how do you want to play?";

            string last = PlayerPrefs.GetString(InputModeKey, "tilt");
            _tiltButton = BuildButton("Tilt", 240f, "TILT & TOUCH",
                last == "tilt" ? new Color(1f, 0.55f, 0.15f) : new Color(0.35f, 0.38f, 0.48f),
                ChooseTilt);
            _cameraButton = BuildButton("Camera", 60f, "CAMERA (BETA)",
                last == "camera" ? new Color(1f, 0.55f, 0.15f) : new Color(0.35f, 0.38f, 0.48f),
                ChooseCamera);

            _status = RuntimeUi.Label("Status", _panel.transform,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(24f, 120f), new Vector2(-24f, 260f),
                36, TextAnchor.UpperCenter, new Color(0.72f, 0.76f, 0.85f));
            _status.text = IdleStatus;

            BuildFooterLinks();
        }

        /// The two dim lines along the bottom, sharing one row so neither competes with the mode
        /// buttons. Both are always tappable, deliberately outside SetButtonsInteractable:
        ///   * "how to play" re-opens the first-run guide, which is the only way back to it once
        ///     it has been dismissed;
        ///   * "privacy policy" is Play policy - it has to be reachable inside the app, not only
        ///     on the store listing, and this is the screen every player passes through.
        void BuildFooterLinks()
        {
            BuildLink("Guide", new Vector2(0f, 0f), new Vector2(0.5f, 0f),
                new Vector2(24f, 24f), new Vector2(-8f, 84f),
                "how to play", () => GuideRequested?.Invoke());

            BuildLink("Privacy", new Vector2(0.5f, 0f), new Vector2(1f, 0f),
                new Vector2(8f, 24f), new Vector2(-24f, 84f),
                "privacy policy", () => Application.OpenURL(GameLinks.PrivacyPolicyUrl));
        }

        void BuildLink(string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax, string label, Action onTap)
        {
            var text = RuntimeUi.Label(name, _panel.transform, anchorMin, anchorMax,
                offsetMin, offsetMax,
                30, TextAnchor.MiddleCenter, new Color(0.45f, 0.50f, 0.62f));
            text.text = label;
            text.raycastTarget = true;

            var button = text.gameObject.AddComponent<Button>();
            button.targetGraphic = text;
            button.onClick.AddListener(() => onTap());
        }

        Button BuildButton(string name, float bottomOffset, string label, Color color, Action onTap) =>
            RuntimeUi.TextButton(name, _panel.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0f, bottomOffset - 260f), new Vector2(640f, 150f),
                color, label, 52, new Color(0.06f, 0.06f, 0.08f), onTap);

        void ChooseTilt() => BeginPick(Pick.Tilt);

        void ChooseCamera()
        {
            if (_done || _pending != Pick.None || _rig != null) return; // already starting
            SetButtonsInteractable(false);
            _status.text = "checking device speed…";
            _rig = FaceTrackingRig.Create();
            _staging = new CameraStaging(_rig);
            _rig.Begin();

            // Framing yourself is exactly when it helps to see what the camera makes of you, so
            // the same panel the run uses goes up here, larger, for the whole wait.
            _overlay = FaceOverlay.Framing(_rig, FaceOverlay.PickerSortingOrder,
                FaceOverlay.PickerPosition);
        }

        /// Back at the picker while the camera is being staged. Nothing has started, so this simply
        /// undoes the tap that began it: the rig goes, the buttons come back, and the status line
        /// returns to what it said before. Without it a player the camera cannot find is stuck —
        /// both mode buttons are disabled and back had nothing to do (CLAUDE.md rule 3).
        public void CancelStaging()
        {
            if (!IsStagingCamera) return;
            CleanupRig();
            SetButtonsInteractable(true);
            _status.text = IdleStatus;
        }

        /// A pick never acts on the frame it arrives, exactly like PauseMenu.Begin: TouchTapInput
        /// reports a jump on the same TouchPhase.Ended that fires the button, so handing the run
        /// over here would give that tap to the runner on the run's very first frame. Update and
        /// the EventSystem run in the same phase in no fixed order, so a frame counter is the only
        /// thing that makes this a rule rather than a hope.
        ///
        /// It applies to the camera handover too, even though the tap that started staging is
        /// seconds old by then: staging finishes on its own clock, so the frame it lands on is
        /// arbitrary and can be the frame of any other tap the player happens to make.
        void BeginPick(Pick pick)
        {
            if (_done || _pending != Pick.None) return;
            _pending = pick;
            _pendingFrame = Time.frameCount;
            SetButtonsInteractable(false);
        }

        void Update()
        {
            if (_pending != Pick.None)
            {
                if (Time.frameCount == _pendingFrame) return;
                Commit();
                return;
            }

            if (_done || _staging == null) return;

            switch (_staging.Poll())
            {
                case CameraStaging.Stage.Waiting:
                    _status.text = _staging.Status;
                    break;

                case CameraStaging.Stage.Ready:
                    BeginPick(Pick.Camera);
                    break;

                case CameraStaging.Stage.Failed:
                    _status.text = _staging.Status + "\n— pick a mode to play";
                    CleanupRig();
                    SetButtonsInteractable(true);
                    break;
            }
        }

        void Commit()
        {
            Pick pick = _pending;
            _pending = Pick.None;
            _done = true;

            PlayerPrefs.SetString(InputModeKey, pick == Pick.Camera ? "camera" : "tilt");
            PlayerPrefs.Save();

            if (pick == Pick.Camera)
            {
                // Handed over, not torn down: the rig is its own DontDestroyOnLoad object and the
                // run takes ownership of it from here. Only this screen's copy of the overlay goes
                // (RunFlow puts the HUD-sized one up).
                FaceTrackingRig rig = _rig;
                _rig = null;
                _staging = null;
                DismissOverlay();
                Chosen?.Invoke(true, rig);
            }
            else
            {
                CleanupRig();
                Chosen?.Invoke(false, null);
            }

            Destroy(gameObject);
        }

        void SetButtonsInteractable(bool on)
        {
            _tiltButton.interactable = on;
            _cameraButton.interactable = on;
        }

        void CleanupRig()
        {
            _staging = null;
            DismissOverlay();
            if (_rig == null) return;
            Destroy(_rig.gameObject);
            _rig = null;
        }

        /// The overlay is a root object with its own canvas, so it does not go when this screen
        /// does - it has to be taken down explicitly, from every exit including OnDestroy.
        void DismissOverlay()
        {
            if (_overlay == null) return;
            _overlay.Dismiss();
            _overlay = null;
        }

        void OnDestroy() => DismissOverlay();
    }
}
