using System;
using MotionRunner.CameraInput;
using MotionRunner.Core;
using MotionRunner.Gameplay;
using MotionRunner.Track;
using UnityEngine;
using UnityEngine.UI;

namespace MotionRunner.Menu
{
    /// The two ways to start a run, at the bottom of the home screen where the thumb is: TILT &
    /// TOUCH and CAMERA (BETA). Lifted wholesale out of the old ModeSelectMenu, which was the
    /// whole first screen; it is now one card on one tab, and everything that made it correct
    /// came with it.
    ///
    /// The camera path walks the FaceTrackingRig's states on-screen (speed gate -> permission ->
    /// camera -> orientation) via the shared CameraStaging, and only hands the run over once a
    /// face has actually been seen, so the player gets "I can see you" feedback before the track
    /// starts moving. Every failure lands back here with the reason shown - camera mode can never
    /// strand the player (CLAUDE.md rule 3: tilt+touch always works).
    public sealed class ModePickerCard
    {
        public const float Height = 380f;

        const string InputModeKey = "veyro.input_mode";

        /// The scheme the player last ran with, defaulting to tilt on a fresh install.
        ///
        /// Public because the boards split per scheme (BestBoard, Feature D) and the profile tab
        /// has to know WHICH board to show when there is no run in progress to ask. This card owns
        /// the key, so the answer comes from here rather than from a second reader of the same
        /// string - the same reason ProgressStore exists for the streak.
        public static ControlScheme LastUsed =>
            PlayerPrefs.GetString(InputModeKey, "tilt") == "camera"
                ? ControlScheme.Camera
                : ControlScheme.Tilt;

        static void Remember(ControlScheme scheme)
        {
            PlayerPrefs.SetString(InputModeKey, scheme == ControlScheme.Camera ? "camera" : "tilt");
            PlayerPrefs.Save();
        }

        /// What the status line says when nothing is happening - the line the card opens with,
        /// and the line it must go back to when camera staging is cancelled.
        const string IdleStatus =
            "camera mode plays hands-free:\nlean to steer, hop to jump, crouch to slide";

        enum Pick
        {
            None,
            Tilt,
            Camera
        }

        /// Raised once with the chosen scheme; the rig is non-null only for camera mode.
        public event Action<bool, FaceTrackingRig> Chosen;

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

        /// A mode was picked and its run starts on a coming frame. Anything else that could start
        /// a run (a challenge link) must stand aside for it - see BeginPick / Commit.
        public bool HasPendingPick => !_done && _pending != Pick.None;

        /// `onGuide` opens how-to-play: the "i" in the card's corner, the quick way back to the
        /// guide from the one screen where the choice it explains is made.
        public void Build(RectTransform slot, Action onGuide)
        {
            CosmeticUi.Card(slot);

            // Inset symmetrically by the "i" so the centred status line stays centred and clear
            // of it.
            _status = RuntimeUi.Label("Status", slot,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(MenuTheme.CardPadding + InfoReserve, -96f),
                new Vector2(-MenuTheme.CardPadding - InfoReserve, -12f),
                30, TextAnchor.UpperCenter, MenuTheme.Dim);
            _status.text = IdleStatus;

            BuildInfoButton(slot, onGuide);

            bool lastWasCamera = LastUsed == ControlScheme.Camera;
            _tiltButton = BuildButton(slot, "Tilt", -104f, "TILT & TOUCH",
                lastWasCamera ? MenuTheme.Slot : MenuTheme.Accent,
                lastWasCamera ? MenuTheme.Text : MenuTheme.OnAccent,
                () => BeginPick(Pick.Tilt));

            _cameraButton = BuildButton(slot, "Camera", -244f, "CAMERA",
                lastWasCamera ? MenuTheme.Accent : MenuTheme.Slot,
                lastWasCamera ? MenuTheme.OnAccent : MenuTheme.Text,
                ChooseCamera);
            AddBetaTag(_cameraButton);
        }

        /// "BETA" as a gold pill after the CAMERA label - the guide's tile tag - instead of the
        /// "(BETA)" that made the label the longest word on the screen.
        static void AddBetaTag(Button button)
        {
            const float tagWidth = 92f, gap = 18f;
            var label = button.GetComponentInChildren<Text>();
            float labelWidth = label.preferredWidth;

            // Word and tag centred as one group: the label steps left by half the tag.
            float shift = (tagWidth + gap) * 0.5f;
            label.rectTransform.offsetMin -= new Vector2(shift, 0f);
            label.rectTransform.offsetMax -= new Vector2(shift, 0f);

            RuntimeUi.Element("Beta", button.transform, out var tag);
            tag.anchorMin = tag.anchorMax = new Vector2(0.5f, 0.5f);
            tag.pivot = new Vector2(0f, 0.5f);
            tag.anchoredPosition = new Vector2(labelWidth * 0.5f + gap - shift, 0f);
            tag.sizeDelta = new Vector2(tagWidth, 38f);
            CosmeticUi.Surface(tag, MenuTheme.Gold, 19f).raycastTarget = false;
            var text = RuntimeUi.Label("Label", tag, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                22, TextAnchor.MiddleCenter, MenuTheme.OnAccent);
            text.fontStyle = FontStyle.Bold;
            text.text = "BETA";
        }

        const float InfoSize = 60f;
        const float InfoReserve = InfoSize + 8f;

        /// A round "i" in the card's top-right corner. The whole 60 px disc is the target; the
        /// letter is drawn in the dim ink of the status line beside it, so it reads as part of the
        /// card's furniture rather than as a third way to start a run.
        static void BuildInfoButton(RectTransform slot, Action onGuide)
        {
            RuntimeUi.Element("Info", slot, out var rect);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-MenuTheme.CardPadding, -14f); // flush with the buttons' edge
            rect.sizeDelta = new Vector2(InfoSize, InfoSize);

            var disc = CosmeticUi.Surface(rect, MenuTheme.Slot, InfoSize * 0.5f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = disc;
            button.onClick.AddListener(() => onGuide?.Invoke());
            RuntimeUi.TapSound(button);

            var letter = RuntimeUi.Label("Label", rect, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, 38, TextAnchor.MiddleCenter, MenuTheme.Dim);
            letter.fontStyle = FontStyle.Bold;
            letter.text = "i";
        }

        Button BuildButton(RectTransform slot, string name, float top, string label,
            Color color, Color labelColor, Action onTap)
        {
            // Rounded and bold like RUN AGAIN on the result card: the thumb meets the same button
            // at both ends of a run.
            var go = RuntimeUi.Element(name, slot, out var rect);
            RuntimeUi.Stretch(rect,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(MenuTheme.CardPadding, top - 124f),
                new Vector2(-MenuTheme.CardPadding, top));

            var image = CosmeticUi.Surface(rect, color, 124f * 0.32f);

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onTap());
            RuntimeUi.TapSound(button);

            var text = RuntimeUi.Label("Label", go.transform, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, 46, TextAnchor.MiddleCenter, labelColor);
            text.fontStyle = FontStyle.Bold;
            text.text = label;

            return button;
        }

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

        /// Back while the camera is being staged. Nothing has started, so this simply undoes the
        /// tap that began it: the rig goes, the buttons come back, and the status line returns to
        /// what it said before. Without it a player the camera cannot find is stuck - both mode
        /// buttons are disabled and back had nothing to do (CLAUDE.md rule 3).
        public void CancelStaging()
        {
            if (!IsStagingCamera) return;
            CleanupRig();
            SetButtonsInteractable(true);
            _status.text = IdleStatus;
        }

        /// The page is going away with the pick not yet committed - a tab switch, which can land
        /// on the one frame between staging turning Ready (or a TILT tap) and the next Tick that
        /// would Commit. CancelStaging cannot cover that frame: IsStagingCamera is false the
        /// moment _pending is set, while the rig, the staging and the overlay are all still this
        /// card's to clean up. A hidden page stops ticking, so without this the pending pick
        /// freezes with a live camera behind the shop and fires a run the player did not just ask
        /// for when the tab comes back.
        ///
        /// After Commit there is nothing to abandon: _done guards the handed-over rig during the
        /// menu's own teardown, where _rig and _staging are already null.
        public void Abandon()
        {
            if (_done) return;
            _pending = Pick.None;
            _pendingFrame = -1;
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

        /// Driven by the page's Update, not by an Update of its own: a card is a plain class, and
        /// one component per page is what keeps "who ticks what" legible.
        public void Tick()
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

            Remember(pick == Pick.Camera ? ControlScheme.Camera : ControlScheme.Tilt);

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
        }

        void SetButtonsInteractable(bool on)
        {
            if (_tiltButton != null) _tiltButton.interactable = on;
            if (_cameraButton != null) _cameraButton.interactable = on;
        }

        void CleanupRig()
        {
            _staging = null;
            DismissOverlay();
            if (_rig == null) return;
            UnityEngine.Object.Destroy(_rig.gameObject);
            _rig = null;
        }

        /// The overlay is a root object with its own canvas, so it does not go when the menu does
        /// - it has to be taken down explicitly, from every exit including the menu being
        /// destroyed. Dispose is the page's OnDestroy calling in.
        void DismissOverlay()
        {
            if (_overlay == null) return;
            _overlay.Dismiss();
            _overlay = null;
        }

        public void Dispose() => CleanupRig();
    }
}
