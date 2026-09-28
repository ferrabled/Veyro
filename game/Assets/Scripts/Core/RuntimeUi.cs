using System;
using MotionRunner.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MotionRunner.Core
{
    /// The UGUI plumbing every screen shares. All UI is built from code (CLAUDE.md rule 1), so
    /// each screen used to carry its own copy of "make a canvas, make a label, make a button";
    /// with the pause menu that was about to be the fourth copy, which is where the duplication
    /// stopped paying for itself.
    ///
    /// Deliberately not a framework: this is exactly what RunHud, the mode picker and the store
    /// screen were already doing, with the conventions they already agreed on - one portrait
    /// 1080x1920 reference resolution, one font resolution chain, one EventSystem. The main menu
    /// adds its own layer on top (MenuTheme, MenuStack, MenuPage) rather than growing this one:
    /// the run screens do not need a tab bar and should not pay for one.
    public static class RuntimeUi
    {
        /// Portrait design canvas. Every screen is laid out against these numbers, so a value
        /// copied from one screen to another means the same thing on the phone.
        public static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);

        static Font _font;

        /// Legacy UGUI Text needs a Font object and renders nothing at all when it is null.
        /// LegacyRuntime.ttf is the built-in one in Unity 2022.2+; the fallbacks exist because a
        /// screen that silently renders nothing is the kind of bug that only shows up on device.
        public static Font Font
        {
            get
            {
                if (_font != null) return _font;

                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (_font == null) _font = Resources.Load<Font>("Fonts/HudFont");
                if (_font == null)
                {
                    // Fully qualified: inside a property called Font, a bare `Font.` is the
                    // property, not the type.
                    var installed = UnityEngine.Font.GetOSInstalledFontNames();
                    if (installed != null && installed.Length > 0)
                        _font = UnityEngine.Font.CreateDynamicFontFromOSFont(installed[0], 48);
                }
                if (_font == null) Debug.LogError("RuntimeUi: no font could be resolved - UI text will not render.");
                return _font;
            }
        }

        /// Turns a GameObject into a screen-space portrait canvas with a raycaster and an
        /// EventSystem behind it. sortingOrder is how screens stack: HUD 0, menus above it.
        public static Canvas PortraitCanvas(GameObject host, int sortingOrder = 0)
        {
            var canvas = host.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = host.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;

            host.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvas;
        }

        /// A full-screen child of a canvas that shrinks itself to the device's safe area
        /// (SafeAreaFitter): build what must stay readable and tappable under it, and leave
        /// backdrops on the canvas so they still reach the screen's edges. The same call does the
        /// right thing on a notched iPhone, a punch-hole Android and a plain 16:9 phone (where it
        /// is simply the whole screen).
        public static RectTransform SafeRoot(Transform canvas, string name = "Safe area")
        {
            var go = Element(name, canvas, out var rect);
            Stretch(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            go.AddComponent<SafeAreaFitter>();
            return rect;
        }

        /// A full-screen backdrop: opaque for a menu, translucent for something laid over the run.
        public static GameObject FullScreenPanel(string name, Transform parent, Color color)
        {
            var go = Element(name, parent, out var rect);
            Stretch(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            go.AddComponent<Image>().color = color;
            return go;
        }

        /// A fixed-size panel centred on the screen - the card a result or a menu sits on.
        public static GameObject Card(string name, Transform parent, Vector2 size, Color color)
        {
            var go = Element(name, parent, out var rect);
            Stretch(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rect.sizeDelta = size;
            go.AddComponent<Image>().color = color;
            return go;
        }

        /// A coloured rectangle filling its parent - the backdrop of a card that has already been
        /// placed, so the placement and the paint stay separate concerns.
        public static Image Panel(string name, Transform parent, Color color)
        {
            var go = Element(name, parent, out var rect);
            Stretch(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var image = go.AddComponent<Image>();
            image.color = color;
            return image;
        }

        /// A horizontal progress bar: a track with a fill pinned to its left edge. Returns the
        /// fill, whose anchorMax.x the caller sets between 0 and 1 - that is the whole API,
        /// because a bar that is resized rather than re-anchored stops lining up with its track
        /// the moment the canvas scales.
        public static RectTransform Bar(string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
            Color trackColor, Color fillColor)
        {
            var track = Element(name, parent, out var trackRect);
            Stretch(trackRect, anchorMin, anchorMax, offsetMin, offsetMax);
            track.AddComponent<Image>().color = trackColor;

            var fill = Element("Fill", track.transform, out var fillRect);
            Stretch(fillRect, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            fill.AddComponent<Image>().color = fillColor;
            return fillRect;
        }

        /// Moves a bar's fill. Clamped, because a fraction out of range is a data bug that should
        /// not become a fill hanging past the end of its track.
        public static void SetBarFill(RectTransform fill, float fraction)
        {
            if (fill == null) return;
            float clamped = fraction < 0f ? 0f : fraction > 1f ? 1f : fraction;
            fill.anchorMax = new Vector2(clamped, 1f);
            fill.offsetMax = Vector2.zero;
            fill.offsetMin = Vector2.zero;
        }

        /// A text element positioned by anchors + offsets, which is how every screen here places
        /// its copy. Never a raycast target: taps belong to buttons and to the run.
        public static Text Label(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax, int fontSize, TextAnchor alignment, Color color)
        {
            var go = Element(name, parent, out var rect);
            Stretch(rect, anchorMin, anchorMax, offsetMin, offsetMax);

            var text = go.AddComponent<Text>();
            text.font = Font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        /// A coloured rectangle with a centred label on it, placed by anchor + offset + size.
        /// Every button clicks (`sound`, default the UI tap; CLOSE / QUIT / back links pass
        /// Sfx.UiBack) - see TapSound for why the cue is added here rather than at each site.
        public static Button TextButton(string name, Transform parent, Vector2 anchor,
            Vector2 position, Vector2 size, Color color,
            string label, int fontSize, Color labelColor, Action onTap, Sfx sound = Sfx.UiTap)
        {
            var go = Element(name, parent, out var rect);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var image = go.AddComponent<Image>();
            image.color = color;

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onTap());
            TapSound(button, sound);

            Label("Label", go.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                fontSize, TextAnchor.MiddleCenter, labelColor).text = label;
            return button;
        }

        /// Gives a hand-rolled button its click. Added AFTER the action listener on purpose, and
        /// UnityEvent invokes in registration order: an action that fires a confirm or a deny has
        /// already done so by the time this runs, and GameAudio lets that stronger cue stand in
        /// for the plain tap. The closure holds no reference to the button, so a button that
        /// destroys its own screen (QUIT TO MENU) still clicks.
        public static void TapSound(Button button, Sfx sound = Sfx.UiTap)
        {
            if (button == null) return;
            button.onClick.AddListener(() => GameAudio.Play(sound));
        }

        /// An empty parented RectTransform - the first three lines of every element above.
        public static GameObject Element(string name, Transform parent, out RectTransform rect)
        {
            var go = new GameObject(name);
            rect = go.AddComponent<RectTransform>();
            go.transform.SetParent(parent, false);
            return go;
        }

        public static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
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
    }
}
