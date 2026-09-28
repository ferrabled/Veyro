using System;
using MotionRunner.Audio;
using MotionRunner.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MotionRunner.Menu
{
    /// The shared shape of a modal sheet - the notification and analytics panels, and the guide's
    /// corner buttons: the park-ink dim, a rounded paper card, a round X in its corner, and a tap
    /// on the dim that closes it like the X does. One set of pieces so every modal in the game is
    /// dismissed the same three ways (X, tap outside, Android back) and looks like the result card
    /// and the guide rather than like a debug dialog.
    internal static class MenuSheet
    {
        public const float CardRadius = 44f;
        public const float IconSize = 72f;

        /// Every settings sheet is this wide, so two opened one after the other line up.
        public const float Width = 900f;

        /// The scrim behind every sheet: the park's ink, as the result card and guide use.
        public static readonly Color Dim = new Color(Art.ParkTheme.Ink.r, Art.ParkTheme.Ink.g, Art.ParkTheme.Ink.b, 0.86f);

        /// Canvas, dim (which closes on tap), rounded card and its X. Returns the card.
        public static RectTransform Build(GameObject host, int sortingOrder, Vector2 size, Action close)
        {
            RuntimeUi.PortraitCanvas(host, sortingOrder);

            var dim = RuntimeUi.FullScreenPanel("Dim", host.transform, Dim);
            var outside = dim.AddComponent<Button>();
            outside.transition = Selectable.Transition.None;
            outside.onClick.AddListener(() => close());
            RuntimeUi.TapSound(outside, Sfx.UiBack);

            // The card's own surface takes the taps that land on it, so only the dim closes.
            RuntimeUi.Element("Card", host.transform, out var card);
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.sizeDelta = size;
            CosmeticUi.Surface(card, Art.ParkTheme.Paper, CardRadius);

            IconButton("Close", card, new Vector2(1f, 1f), new Vector2(-38f, -28f),
                box => MenuIcons.Cross(box, MenuTheme.Dim, 30f, 5f), close);
            return card;
        }

        /// A round icon button pinned to a corner of a card - the X, the guide's back arrow.
        /// The 72 px disc is the whole target, well past the 48 dp minimum.
        public static GameObject IconButton(string name, Transform card, Vector2 corner, Vector2 position,
            Action<RectTransform> drawIcon, Action onTap)
        {
            RuntimeUi.Element(name, card, out var rect);
            rect.anchorMin = rect.anchorMax = corner;
            rect.pivot = corner;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(IconSize, IconSize);

            var disc = CosmeticUi.Surface(rect, MenuTheme.Slot, IconSize * 0.5f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = disc;
            QuietSelection(button);
            button.onClick.AddListener(() => onTap());
            RuntimeUi.TapSound(button, Sfx.UiBack); // every icon button steps back or out

            RuntimeUi.Element("Icon", rect, out var icon);
            RuntimeUi.Stretch(icon, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            drawIcon(icon);
            return rect.gameObject;
        }

        /// A centred title band `top` px under the card's top edge. Bold, best-fit down from
        /// `size` so a long vendor string still fits its band.
        public static Text Title(RectTransform card, float top, float height, int size = 44)
        {
            var text = Band("Title", card, top, height, size, MenuTheme.Text);
            text.fontStyle = FontStyle.Bold;
            return text;
        }

        /// The body copy, centred, best-fit down from `size`.
        public static Text Body(RectTransform card, float top, float height, int size = 32) =>
            Band("Body", card, top, height, size, MenuTheme.Dim);

        static Text Band(string name, RectTransform card, float top, float height, int size, Color color)
        {
            var text = RuntimeUi.Label(name, card, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(56f, -top - height), new Vector2(-56f, -top), size, TextAnchor.MiddleCenter, color);
            MenuRows.Fit(text, size);
            text.resizeTextMinSize = 22;
            return text;
        }

        /// The one action the sheet exists for: pink, bold, RUN AGAIN's shape. Centred `y` px
        /// above the card's bottom edge.
        public static Button Primary(RectTransform card, string name, float y, string label, Action onTap) =>
            Pill(card, name, y, 110f, MenuTheme.Accent, label, 36, MenuTheme.OnAccent, onTap, Sfx.UiTap);

        /// An ordinary action, or one of a pair that must carry equal weight (a consent choice).
        public static Button Secondary(RectTransform card, string name, float y, string label, Action onTap,
            Sfx sound = Sfx.UiTap) =>
            Pill(card, name, y, 92f, MenuTheme.Slot, label, 30, MenuTheme.Text, onTap, sound);

        /// A quiet text action (copy a support id, open the policy): no slab, the row is the target.
        public static Button Link(RectTransform card, string name, float y, string label, Action onTap)
        {
            RuntimeUi.Element(name, card, out var rect);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(780f, 60f);
            var text = RuntimeUi.Label("Label", rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                26, TextAnchor.MiddleCenter, MenuTheme.Dim);
            text.text = label;
            text.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = text;
            QuietSelection(button);
            button.onClick.AddListener(() => onTap());
            RuntimeUi.TapSound(button);
            return button;
        }

        static Button Pill(RectTransform card, string name, float y, float height, Color fill, string label,
            int fontSize, Color labelColor, Action onTap, Sfx sound)
        {
            RuntimeUi.Element(name, card, out var rect);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(780f, height);

            var surface = CosmeticUi.Surface(rect, fill, height * 0.32f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = surface;
            QuietSelection(button);
            button.onClick.AddListener(() => onTap());
            RuntimeUi.TapSound(button, sound);

            var text = RuntimeUi.Label("Label", rect, Vector2.zero, Vector2.one,
                new Vector2(20f, 0f), new Vector2(-20f, 0f), fontSize, TextAnchor.MiddleCenter, labelColor);
            text.fontStyle = FontStyle.Bold;
            text.text = label;
            MenuRows.Fit(text, fontSize);
            return button;
        }

        /// On a touch screen a tapped button stays "selected" until something else is, and the
        /// default tint left it a shade darker after every tap.
        static void QuietSelection(Button button)
        {
            var colors = button.colors;
            colors.selectedColor = colors.normalColor;
            button.colors = colors;
        }
    }
}
