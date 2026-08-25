using System;
using MotionRunner.CameraInput;
using MotionRunner.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MotionRunner.Gameplay
{
    /// The startup choice between the two control schemes, built entirely from code like RunHud
    /// (CLAUDE.md rule 1). Shown every launch — switching input mid-session is deliberately not a
    /// thing yet, so this screen is the one place the choice happens.
    ///
    /// The camera path walks the FaceTrackingRig's states on-screen (speed gate -> permission ->
    /// camera -> orientation) and only hands the run over once a face has actually been seen, so
    /// the player gets "I can see you" feedback before the track starts moving. Every failure
    /// falls back to this menu with the reason shown — camera mode can never strand the player
    /// (CLAUDE.md rule 3: gyro+touch always works).
    public sealed class ModeSelectMenu : MonoBehaviour
    {
        const string InputModeKey = "veyro.input_mode";
        const float FaceSeenHoldSeconds = 0.5f;

        /// Raised once with the chosen scheme; the rig is non-null only for camera mode.
        public event Action<bool, FaceTrackingRig> Chosen;

        GameObject _panel;
        Button _tiltButton;
        Button _cameraButton;
        Text _status;
        FaceTrackingRig _rig;
        float _faceSeenSince = -1f;
        bool _done;

        static Font _font;

        public static ModeSelectMenu Create()
        {
            var go = new GameObject("ModeSelectMenu");
            var menu = go.AddComponent<ModeSelectMenu>();
            menu.Build();
            return menu;
        }

        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // above the HUD, which the bootstrap has already built

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            _panel = new GameObject("Panel");
            var panelRect = _panel.AddComponent<RectTransform>();
            _panel.transform.SetParent(transform, false);
            Stretch(panelRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _panel.AddComponent<Image>().color = new Color(0.06f, 0.07f, 0.12f, 1f);

            CreateText("Title", _panel.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24f, -420f), new Vector2(-24f, -220f),
                110, TextAnchor.MiddleCenter, new Color(0.94f, 0.96f, 1f)).text = "VEYRO RUN";

            CreateText("Sub", _panel.transform,
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

            _status = CreateText("Status", _panel.transform,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(24f, 120f), new Vector2(-24f, 260f),
                36, TextAnchor.UpperCenter, new Color(0.72f, 0.76f, 0.85f));
            _status.text = "camera mode plays hands-free:\nlean to steer, hop to jump, crouch to slide";

            BuildPrivacyLink();
        }

        /// Play policy requires the privacy policy to be reachable inside the app, not only on
        /// the store listing. Smallest honest surface: one dim link on the screen every player
        /// passes through, always tappable (deliberately not part of SetButtonsInteractable).
        void BuildPrivacyLink()
        {
            var text = CreateText("Privacy", _panel.transform,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(24f, 24f), new Vector2(-24f, 84f),
                30, TextAnchor.MiddleCenter, new Color(0.45f, 0.50f, 0.62f));
            text.text = "privacy policy";
            text.raycastTarget = true;

            var button = text.gameObject.AddComponent<Button>();
            button.targetGraphic = text;
            button.onClick.AddListener(() => Application.OpenURL(GameLinks.PrivacyPolicyUrl));
        }

        Button BuildButton(string name, float bottomOffset, string label, Color color, Action onTap)
        {
            var go = new GameObject(name);
            var rect = go.AddComponent<RectTransform>();
            go.transform.SetParent(_panel.transform, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, bottomOffset - 260f);
            rect.sizeDelta = new Vector2(640f, 150f);

            var image = go.AddComponent<Image>();
            image.color = color;

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onTap());

            CreateText("Label", go.transform,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                52, TextAnchor.MiddleCenter, new Color(0.06f, 0.06f, 0.08f)).text = label;
            return button;
        }

        void ChooseTilt()
        {
            if (_done) return;
            _done = true;
            PlayerPrefs.SetString(InputModeKey, "tilt");
            PlayerPrefs.Save();
            CleanupRig();
            Chosen?.Invoke(false, null);
            Destroy(gameObject);
        }

        void ChooseCamera()
        {
            if (_rig != null) return; // already starting
            SetButtonsInteractable(false);
            _status.text = "checking device speed…";
            _rig = FaceTrackingRig.Create();
            _rig.Begin();
        }

        void Update()
        {
            if (_done || _rig == null) return;

            switch (_rig.State)
            {
                case FaceTrackingRig.RigState.Gating:
                    _status.text = "checking device speed…";
                    break;
                case FaceTrackingRig.RigState.RequestingPermission:
                    _status.text = "waiting for camera permission…";
                    break;
                case FaceTrackingRig.RigState.StartingCamera:
                    _status.text = $"starting camera…  (speed check: {_rig.GateMedianMs:F0} ms ✓)";
                    break;
                case FaceTrackingRig.RigState.Probing:
                    _status.text = "finding which way up the camera is —\nprop the phone up and step back";
                    break;

                case FaceTrackingRig.RigState.Tracking:
                    bool faceNow = _rig.Latest.HasFace && _rig.LatestAgeSeconds < 0.35f;
                    if (!faceNow)
                    {
                        _faceSeenSince = -1f;
                        _status.text = "stand where the phone can see you…";
                        break;
                    }

                    if (_faceSeenSince < 0f) _faceSeenSince = Time.unscaledTime;
                    _status.text = "I can see you!";
                    if (Time.unscaledTime - _faceSeenSince >= FaceSeenHoldSeconds)
                    {
                        _done = true;
                        PlayerPrefs.SetString(InputModeKey, "camera");
                        PlayerPrefs.Save();
                        Chosen?.Invoke(true, _rig);
                        Destroy(gameObject);
                    }
                    break;

                case FaceTrackingRig.RigState.Failed:
                    _status.text = _rig.FailReason + "\n— pick a mode to play";
                    CleanupRig();
                    SetButtonsInteractable(true);
                    break;
            }
        }

        void SetButtonsInteractable(bool on)
        {
            _tiltButton.interactable = on;
            _cameraButton.interactable = on;
        }

        void CleanupRig()
        {
            if (_rig == null) return;
            Destroy(_rig.gameObject);
            _rig = null;
        }

        // ---- same UGUI plumbing as RunHud; duplicated on purpose while there are exactly two
        // screens. A third screen is the point at which this becomes a shared helper. ----

        static Text CreateText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax, int fontSize, TextAnchor alignment, Color color)
        {
            var go = new GameObject(name);
            var rect = go.AddComponent<RectTransform>();
            go.transform.SetParent(parent, false);
            Stretch(rect, anchorMin, anchorMax, offsetMin, offsetMax);

            var text = go.AddComponent<Text>();
            text.font = MenuFont();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        static Font MenuFont()
        {
            if (_font != null) return _font;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return _font;
        }
    }
}
