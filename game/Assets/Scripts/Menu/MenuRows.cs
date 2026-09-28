using System;
using MotionRunner.Audio;
using MotionRunner.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MotionRunner.Menu
{
    /// The list-row furniture of the profile tab's SETTINGS and ACCOUNT cards: a title band, then
    /// full-width rows split by hairlines, each a label (with an optional quieter line under it)
    /// and one thing on the right - a chevron for "opens something", a pill for "does something
    /// here". The whole row takes the tap, so the target is the row and not the word.
    ///
    /// It replaced a card of free-floating text links laid out three across, where nothing said
    /// which words were tappable, what they would do, or which ones belonged together.
    internal static class MenuRows
    {
        public const float TitleHeight = 84f;
        public const float Height = 96f;

        /// A row with a line under its label - room for the line to wrap once.
        public const float TallHeight = 128f;

        /// Space under the last row, inside the card.
        public const float BottomPadding = 10f;

        /// Width kept clear on the right of a label for a pill.
        public const float PillReserve = 200f;

        /// Width kept clear on the right of a label for a chevron.
        public const float ChevronReserve = 80f;

        public static void Title(RectTransform slot, string text)
        {
            RuntimeUi.Label("Title", slot,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(MenuTheme.CardPadding, -70f), new Vector2(-MenuTheme.CardPadding, -18f),
                38, TextAnchor.MiddleLeft, MenuTheme.Text).text = text;
        }

        /// A full-width row. Placement is separate (Place) so a card can re-flow its rows when
        /// one of them hides.
        public static RectTransform Row(RectTransform slot, string name)
        {
            RuntimeUi.Element(name, slot, out var row);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);

            // The hairline across the top; Place hides it on the first row of a card.
            var line = RuntimeUi.Element("Divider", row, out var lineRect);
            RuntimeUi.Stretch(lineRect, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(MenuTheme.CardPadding, -2f), new Vector2(-MenuTheme.CardPadding, 0f));
            line.AddComponent<Image>().color = MenuTheme.Empty;
            return row;
        }

        /// Puts a row `top` px under the card's top edge and returns the y under it.
        public static float Place(RectTransform row, float top, float height, bool first)
        {
            row.anchoredPosition = new Vector2(0f, -top);
            row.sizeDelta = new Vector2(0f, height);
            var divider = row.Find("Divider");
            if (divider != null) divider.gameObject.SetActive(!first);
            return top + height;
        }

        /// Makes the whole row one button: a graphic under everything else takes the raycast, and
        /// inside the profile's scroll view a drag that starts on it still scrolls (Button has no
        /// drag handler, so the drag bubbles to the ScrollRect).
        ///
        /// The graphic is a Slot-coloured fill that the tint keeps invisible except while pressed
        /// - the row lights under the thumb. Selected is invisible too: on a touch screen a
        /// tapped button stays "selected" until something else is, which would leave the row lit.
        public static Button Tap(RectTransform row, Action onTap, Sfx sound = Sfx.UiTap)
        {
            var hit = RuntimeUi.Element("Hit", row, out var hitRect);
            hit.transform.SetAsFirstSibling();
            RuntimeUi.Stretch(hitRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var image = hit.AddComponent<Image>();
            image.color = MenuTheme.Slot;

            var button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var hidden = new Color(1f, 1f, 1f, 0f);
            button.colors = new ColorBlock
            {
                normalColor = hidden,
                highlightedColor = hidden,
                selectedColor = hidden,
                disabledColor = hidden,
                pressedColor = Color.white,
                colorMultiplier = 1f,
                fadeDuration = 0.06f
            };
            button.onClick.AddListener(() => onTap());
            RuntimeUi.TapSound(button, sound);
            return button;
        }

        /// The row's label: vertically centred, or in the top half when the row has a sub-line.
        public static Text Label(RectTransform row, string text, float rightReserve, bool withSub)
        {
            var label = RuntimeUi.Label("Label", row,
                new Vector2(0f, withSub ? 1f : 0f), new Vector2(1f, 1f),
                new Vector2(MenuTheme.CardPadding, withSub ? -60f : 0f),
                new Vector2(-rightReserve, withSub ? -14f : 0f),
                32, TextAnchor.MiddleLeft, MenuTheme.Text);
            label.text = text;
            Fit(label, 32);
            return label;
        }

        /// The quieter line under a label: what the row does, or what just happened.
        public static Text Sub(RectTransform row, float rightReserve)
        {
            var sub = RuntimeUi.Label("Sub", row,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(MenuTheme.CardPadding, -118f), new Vector2(-rightReserve, -60f),
                24, TextAnchor.UpperLeft, MenuTheme.Faint);
            Fit(sub, 24);
            return sub;
        }

        /// ">" drawn as two rotated strokes rather than a glyph: the legacy runtime font's
        /// chevrons sit on the baseline and read as a greater-than sign.
        public static void Chevron(RectTransform row)
        {
            var box = RuntimeUi.Element("Chevron", row, out var boxRect);
            boxRect.anchorMin = boxRect.anchorMax = new Vector2(1f, 0.5f);
            boxRect.anchoredPosition = new Vector2(-MenuTheme.CardPadding - 14f, 0f);
            boxRect.sizeDelta = new Vector2(28f, 28f);
            Stroke(boxRect, 45f, 6f);
            Stroke(boxRect, -45f, -6f);
        }

        static void Stroke(RectTransform box, float angle, float y)
        {
            var go = RuntimeUi.Element("Stroke", box, out var rect);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(20f, 4f);
            rect.localRotation = Quaternion.Euler(0f, 0f, -angle);
            var image = go.AddComponent<Image>();
            image.color = MenuTheme.Dim;
            image.raycastTarget = false;
        }

        /// A rounded pill on the right edge saying what a tap on the row does ("COPY"). Visual
        /// only - the row is the button.
        public static Text Pill(RectTransform row, string text, float width = 150f)
        {
            RuntimeUi.Element("Pill", row, out var rect);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-MenuTheme.CardPadding, 0f);
            rect.sizeDelta = new Vector2(width, 58f);
            CosmeticUi.Surface(rect, MenuTheme.Slot, 29f).raycastTarget = false;
            var label = RuntimeUi.Label("Label", rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                24, TextAnchor.MiddleCenter, MenuTheme.Text);
            label.fontStyle = FontStyle.Bold;
            label.text = text;
            return label;
        }

        /// Rows swap their text for variable-length feedback ("import failed: …"), so every
        /// label best-fits down from its designed size instead of running off the card. The
        /// overflow modes matter: RuntimeUi.Label ships Overflow/Overflow, under which best-fit is
        /// a no-op because an unbounded line always "fits".
        public static void Fit(Text text, int designedSize)
        {
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 18;
            text.resizeTextMaxSize = designedSize;
        }
    }
}
