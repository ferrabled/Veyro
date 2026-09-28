using System;
using System.Collections.Generic;
using MotionRunner.Art;
using MotionRunner.Audio;
using MotionRunner.Core;
using MotionRunner.Menu;
using MotionRunner.Track;
using UnityEngine;
using UnityEngine.UI;

namespace MotionRunner.Gameplay
{
    /// The one-time guide: what the two control modes are, how you move, and what a run looks
    /// like. Code-built on the shared RuntimeUi plumbing like every other screen (CLAUDE.md
    /// rule 1); the pages themselves are data in GuideState, so the copy and the paging are
    /// testable without a canvas.
    ///
    /// Shown automatically on the very first launch and reachable afterwards from the "how to
    /// play" row on the profile tab. It is never shown during a run - it explains the choice
    /// the player is about to make, and RunFlow dismisses it if a run somehow starts underneath
    /// (camera staging can finish while it is up).
    ///
    /// Every way out marks it seen, skipping included (GuideState.MarksSeen). Dismissing is the
    /// only thing that writes the flag: a guide abandoned by force-quitting mid-read comes back
    /// once, which is the friendlier way to be wrong.
    ///
    /// ---- the card -------------------------------------------------------------------------
    ///
    /// Dressed like the result card on purpose (RunHud): a rounded paper card on the park's ink,
    /// a bold title, progress dots instead of a "1 / 4", and a rounded pink button - the first
    /// screen a new player sees should look like the game, not like a debug overlay. The copy is
    /// not one hand-wrapped paragraph any more: each page is a lead line, rows and a note
    /// (GuidePage), wrapped to the card's width and stacked by measured height, so the text
    /// fills the card instead of hugging a 40-character left column. Mode pages show a verb chip
    /// per movement; the choice page shows one tile per way of playing, with that mode's picture.
    ///
    /// Each mode page has a picture above its copy: a frame sequence loaded from
    /// Resources/Art/Guide (GuideIllustration names the files, docs/GUIDE_ILLUSTRATIONS.md is the
    /// art brief), cycled in unscaled time when there is more than one frame - the closest thing
    /// to a GIF UGUI can show. All ten frames ship with the game. If a sequence cannot load,
    /// the slot falls back to its instruction caption without exposing asset filenames.
    public sealed class FirstRunGuide : MonoBehaviour
    {
        static readonly Color TextColor = MenuTheme.Text;

        /// The result card's scrim: the park's ink, so the card is paper laid on the game's dark.
        static readonly Color DimColor = new Color(ParkTheme.Ink.r, ParkTheme.Ink.g, ParkTheme.Ink.b, 0.86f);

        // Opaque paper, not MenuTheme.Card: Card is 98% alpha, which over the dim lands a few
        // levels darker than #FBF5E9 and outlines the opaque illustration as a lighter rectangle.
        static readonly Color PanelColor = ParkTheme.Paper;
        static readonly Color AccentColor = MenuTheme.Accent;
        static readonly Color StatusColor = MenuTheme.Dim;
        static readonly Color LinkColor = MenuTheme.Faint;

        /// The illustrations' own paper, averaged: textured, a touch warmer and lighter than the
        /// card's. Behind the art inside a frame, so a letterboxed edge would not read as a gap.
        static readonly Color ArtPaper = ParkTheme.Hex(0xFDF7EB);

        // ---- layout, from the top of the 880x1480 card ----
        //
        // 880 is the result card's width (RunHud.CardWidth), which keeps ~30 px of air each side
        // on the tallest phone we target; 1480 leaves 220 above and below on a 16:9 one.

        const float CardWidth = 880f;
        const float CardHeight = 1480f;
        const float CardRadius = 44f;
        const float Margin = 48f;

        const float DotsY = -196f;

        /// The picture: the card less its margins wide, by the 16:10 the art is delivered in, so
        /// an opaque frame melts into the card with no letterbox.
        const float SlotTop = -228f;
        const float SlotWidth = CardWidth - 2f * Margin;
        const float SlotHeight = SlotWidth * 10f / 16f;

        /// Where the copy starts under a picture, and where it starts when a page has none.
        const float CopyTopUnderSlot = SlotTop - SlotHeight - 28f;
        const float CopyTopFullHeight = SlotTop - 8f;

        /// The primary button, from the bottom edge the thumb measures from, and the copy's floor
        /// just above it.
        const float ButtonY = 124f;
        const float ButtonHeight = 120f;
        const float CopyBottom = -(CardHeight - ButtonY - ButtonHeight * 0.5f - 24f);

        // ---- type ----
        const int LeadSize = 34;
        const int StepSize = 33;
        const int NoteSize = 29;
        const int TileHeadingSize = 40;
        const int TileTextSize = 30;

        const float ChipWidth = 210f;
        const float ChipHeight = 64f;
        const float Gap = 24f;
        const float StepGap = 16f;

        /// The most a gap between two blocks of copy grows when the page has room to spare.
        const float MaxExtraGap = 22f;

        /// The guide is done with. RunFlow owns the screen's lifetime, exactly as it does for the
        /// pause menu, so nothing here destroys itself.
        public event Action Closed;

        readonly GuideState _state = new GuideState();

        Text _title;
        Text _primaryLabel;
        GameObject _backLink;
        RectTransform _card;
        RectTransform _copy;
        readonly List<CosmeticPanel> _dots = new List<CosmeticPanel>();

        GameObject _slot;
        GameObject _frameGroup;
        Image _frame;
        GameObject _placeholder;
        Text _caption;

        Sprite[] _frames;
        int _frameIndex;
        float _frameClock;

        /// True when this player has never been shown the guide. Read once, at the mode picker.
        public static bool IsDue =>
            GuideState.ShouldShowOnLaunch(PlayerPrefs.GetInt(GuideState.SeenKey, 0));

        /// How far the current page's copy runs past its floor, in card pixels - 0 when it fits.
        /// Measured, not estimated; GuideUiReview fails a render where it is not 0.
        public float Overflow { get; private set; }

        public static FirstRunGuide Show()
        {
            var go = new GameObject("FirstRunGuide");
            var guide = go.AddComponent<FirstRunGuide>();
            guide.Build();
            return guide;
        }

        /// The X, a tap outside the card, and the Android back button. All count as seen - a guide
        /// that reappears every launch because it was closed early is not help, it is a nag.
        public void Dismiss() => Close(GuideExit.Skipped);

        void Build()
        {
            // Above the mode picker (100) and below the pause menu (150): the guide only ever
            // covers the picker, and a run - which is the only thing that can be paused - is
            // never underneath it.
            RuntimeUi.PortraitCanvas(gameObject, 120);

            // A tap on the dim around the card closes the guide, like any sheet: it is the same
            // exit as the X (and the Android back button), so it marks the guide seen too.
            var dim = RuntimeUi.FullScreenPanel("Dim", transform, DimColor);
            var outside = dim.AddComponent<Button>();
            outside.transition = Selectable.Transition.None;
            outside.onClick.AddListener(Dismiss);
            RuntimeUi.TapSound(outside, Sfx.UiBack);

            // Named "Card" and parent of "Primary" and "Illustration": GuideUiReview finds them
            // by those paths. The card's own surface takes the taps that land on it, so only the
            // dim around it closes.
            RuntimeUi.Element("Card", transform, out _card);
            _card.anchorMin = _card.anchorMax = new Vector2(0.5f, 0.5f);
            _card.sizeDelta = new Vector2(CardWidth, CardHeight);
            CosmeticUi.Surface(_card, PanelColor, CardRadius);

            _backLink = IconButton("Back", _card, new Vector2(0f, 1f), new Vector2(Margin - 10f, -28f),
                box => MenuIcons.Chevron(box, StatusColor, left: true, length: 24f, thickness: 5f),
                () => Page(-1));

            IconButton("Close", _card, new Vector2(1f, 1f), new Vector2(-Margin + 10f, -28f),
                box => MenuIcons.Cross(box, StatusColor, length: 30f, thickness: 5f),
                Dismiss);

            _title = RuntimeUi.Label("Title", _card,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(Margin, -170f), new Vector2(-Margin, -96f),
                52, TextAnchor.MiddleCenter, TextColor);
            _title.fontStyle = FontStyle.Bold;

            BuildDots();
            BuildSlot();

            RuntimeUi.Element("Copy", _card, out _copy);
            RuntimeUi.Stretch(_copy, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(Margin, CopyBottom), new Vector2(-Margin, CopyTopUnderSlot));

            _primaryLabel = BuildPrimary();

            ShowPage();
        }

        /// One dot per page, centred under the title. ShowPage stretches the current one into a
        /// pink pill, the way a carousel says where it is without numbers.
        void BuildDots()
        {
            int count = GuideState.Pages.Length;
            const float pitch = 30f;
            for (int i = 0; i < count; i++)
            {
                RuntimeUi.Element("Dot" + i, _card, out var rect);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2((i - (count - 1) * 0.5f) * pitch, DotsY);
                rect.sizeDelta = new Vector2(14f, 14f);
                var dot = CosmeticUi.Surface(rect, MenuTheme.Empty, 7f);
                dot.raycastTarget = false;
                _dots.Add(dot);
            }
        }

        /// RunHud's RUN AGAIN, in size and weight: the thumb finds forward in the same place.
        Text BuildPrimary()
        {
            RuntimeUi.Element("Primary", _card, out var rect);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, ButtonY);
            rect.sizeDelta = new Vector2(560f, ButtonHeight);

            var surface = CosmeticUi.Surface(rect, AccentColor, ButtonHeight * 0.32f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = surface;
            button.onClick.AddListener(Advance);
            RuntimeUi.TapSound(button);

            var label = RuntimeUi.Label("Label", rect, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, 48, TextAnchor.MiddleCenter, MenuTheme.OnAccent);
            label.fontStyle = FontStyle.Bold;
            return label;
        }

        /// The picture slot under the dots: the framed art and a rounded caption fallback if
        /// the sequence cannot load. ShowPage picks which of the two is active.
        void BuildSlot()
        {
            _slot = RuntimeUi.Element("Illustration", _card, out var slotRect);
            RuntimeUi.Stretch(slotRect, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(Margin, SlotTop - SlotHeight), new Vector2(-Margin, SlotTop));

            // Card/Illustration/Frame/Art is the path GuideUiReview reads the sprite from.
            var art = Framed("Frame", _slot.transform, 32f, out _frameGroup);
            _frame = art.gameObject.AddComponent<Image>();
            _frame.preserveAspect = true;
            _frame.raycastTarget = false;

            _placeholder = RuntimeUi.Element("Placeholder", _slot.transform, out var holderRect);
            RuntimeUi.Stretch(holderRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            RoundedFill("Panel", _placeholder.transform, 0f, MenuTheme.Slot, 28f);
            RoundedFill("Outline", _placeholder.transform, 12f, MenuTheme.Empty, 18f);
            RoundedFill("Inner", _placeholder.transform, 16f, MenuTheme.Slot, 14f);

            _caption = RuntimeUi.Label("Caption", _placeholder.transform,
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(40f, -40f), new Vector2(-40f, 40f),
                30, TextAnchor.MiddleCenter, StatusColor);
            _caption.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        /// A picture mount: a hairline border, and inside it a rounded mask the picture is
        /// clipped to. The art's paper is textured and never quite the card's flat #FBF5E9, so
        /// a bare picture showed its own edge as a faint rectangle; framed, that edge is a
        /// decision - the same outlined, rounded shape as the choice page's tiles. The mask is
        /// the rounded CosmeticPanel's own geometry (a stencil Mask), so the corners are real.
        /// Returns the rect to put the picture in; `group` is the whole mount, to show or hide.
        static RectTransform Framed(string name, Transform parent, float radius, out GameObject group)
        {
            group = RuntimeUi.Element(name, parent, out var rect);
            RuntimeUi.Stretch(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var border = group.AddComponent<CosmeticPanel>();
            border.color = MenuTheme.Empty;
            border.Radius = radius;
            border.raycastTarget = false;

            var window = RuntimeUi.Element("Window", rect, out var windowRect);
            RuntimeUi.Stretch(windowRect, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            var shape = window.AddComponent<CosmeticPanel>();
            shape.color = ArtPaper; // what a letterboxed frame would show
            shape.Radius = radius - 3f;
            shape.raycastTarget = false;
            window.AddComponent<Mask>().showMaskGraphic = true;

            RuntimeUi.Element("Art", windowRect, out var artRect);
            RuntimeUi.Stretch(artRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return artRect;
        }

        /// A rounded rectangle inset from its parent by `inset` on every side. Three of them,
        /// alternating colours, draw the placeholder's outlined look with nothing but canvas
        /// geometry (CosmeticPanel), so the fallback needs no image asset of its own.
        static CosmeticPanel RoundedFill(string name, Transform parent, float inset, Color color, float radius)
        {
            var go = RuntimeUi.Element(name, parent, out var rect);
            RuntimeUi.Stretch(rect, Vector2.zero, Vector2.one,
                new Vector2(inset, inset), new Vector2(-inset, -inset));
            var panel = go.AddComponent<CosmeticPanel>();
            panel.color = color;
            panel.Radius = radius;
            panel.raycastTarget = false;
            return panel;
        }

        const float IconSize = 72f;

        /// A round icon button in a corner of the card - the back arrow and the X. Icons instead
        /// of "< back" / "skip": two words competing with the title for a corner, where a shape
        /// every app uses says the same thing without being read. The disc is the whole target
        /// (72 px), well past the 48 dp minimum.
        static GameObject IconButton(string name, Transform card, Vector2 corner, Vector2 position,
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
            var colors = button.colors;
            colors.selectedColor = colors.normalColor; // no lingering tint after a tap on touch
            button.colors = colors;
            button.onClick.AddListener(() => onTap());
            RuntimeUi.TapSound(button, Sfx.UiBack); // both step backwards or out

            RuntimeUi.Element("Icon", rect, out var icon);
            RuntimeUi.Stretch(icon, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            drawIcon(icon);
            return rect.gameObject;
        }

        /// The big button: forward while there is a page left, out at the end.
        void Advance()
        {
            if (!_state.Next())
            {
                Close(GuideExit.Completed);
                return;
            }
            ShowPage();
        }

        void Page(int direction)
        {
            if (direction < 0 && !_state.Back()) return;
            ShowPage();
        }

        void ShowPage()
        {
            GuidePage page = _state.Page;
            _title.text = page.Title;
            _primaryLabel.text = _state.PrimaryLabel;

            for (int i = 0; i < _dots.Count; i++)
            {
                bool current = i == _state.Index;
                var rect = _dots[i].rectTransform;
                rect.sizeDelta = new Vector2(current ? 40f : 14f, 14f);
                // Widening the current dot would push into its neighbours; shifting the ones
                // after it by the extra width keeps the row's spacing even.
                float pitch = 30f, extra = 26f;
                float x = (i - (_dots.Count - 1) * 0.5f) * pitch - extra * 0.5f;
                if (i > _state.Index) x += extra;
                else if (current) x += extra * 0.5f;
                rect.anchoredPosition = new Vector2(x, DotsY);
                _dots[i].color = current ? AccentColor : MenuTheme.Empty;
            }

            // Hidden rather than greyed on page one: there is nothing behind the first page, and
            // a dead link is worse than no link.
            _backLink.SetActive(!_state.IsFirst);

            ShowIllustration(page);
            LayOutCopy(page);
        }

        /// The picture for this page, or its placeholder, or neither - and the copy's top edge
        /// follows: a page without a picture gets the height for its tiles.
        void ShowIllustration(GuidePage page)
        {
            _frames = LoadFrames(page);
            _frameIndex = 0;
            _frameClock = 0f;

            _slot.SetActive(page.HasIllustration);
            _copy.offsetMax = new Vector2(-Margin,
                page.HasIllustration ? CopyTopUnderSlot : CopyTopFullHeight);
            if (!page.HasIllustration) return;

            bool hasArt = _frames != null;
            _frameGroup.SetActive(hasArt);
            _placeholder.SetActive(!hasArt);
            if (hasArt)
            {
                _frame.sprite = _frames[0];
                return;
            }

            _caption.text = page.Caption;
        }

        // ---- the copy ----

        /// Rebuilds the copy column for a page: lead, rows, note, top to bottom, each placed by
        /// its measured height. Rebuilt rather than pooled - four pages, a handful of labels, and
        /// a rebuild cannot leave a previous page's row behind.
        void LayOutCopy(GuidePage page)
        {
            for (int i = _copy.childCount - 1; i >= 0; i--)
            {
                var child = _copy.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child); // the editor review renders
            }

            float width = _copy.rect.width;
            float band = _copy.rect.height;
            float used;

            Text lead = null, note = null;
            float leadHeight = 0f, noteHeight = 0f;
            if (!string.IsNullOrEmpty(page.Lead))
            {
                lead = CopyLabel("Lead", page.Lead, LeadSize, StatusColor, TextAnchor.UpperLeft);
                leadHeight = Measure(lead, width);
            }
            if (!string.IsNullOrEmpty(page.Note))
            {
                note = CopyLabel("Note", page.Note, NoteSize, LinkColor, TextAnchor.UpperLeft);
                noteHeight = Measure(note, width);
            }

            bool tiles = page.Rows.Length > 0 && page.Rows[0].IsTile;
            if (tiles)
            {
                // Tiles share the band: lead on top, note at the bottom, tiles split the rest.
                float y = 0f;
                if (lead != null) { Place(lead.rectTransform, 0f, leadHeight); y = leadHeight + Gap; }
                float room = band - y - (note != null ? noteHeight + Gap : 0f);
                float tileHeight = (room - Gap * (page.Rows.Length - 1)) / page.Rows.Length;
                for (int i = 0; i < page.Rows.Length; i++)
                    y = Tile(page.Rows[i], i, y, width, tileHeight) + (i < page.Rows.Length - 1 ? Gap : 0f);
                if (note != null)
                {
                    Place(note.rectTransform, band - noteHeight, noteHeight);
                    y = band;
                }
                used = y;
            }
            else
            {
                var blocks = new List<RectTransform>();
                var heights = new List<float>();
                var gaps = new List<float>(); // before each block
                if (lead != null) Add(lead.rectTransform, leadHeight, 0f);
                for (int i = 0; i < page.Rows.Length; i++)
                {
                    var step = Step(page.Rows[i], i, width, out float height);
                    Add(step, height, blocks.Count == 0 ? 0f : i == 0 ? Gap : StepGap);
                }
                if (note != null) Add(note.rectTransform, noteHeight, Gap);

                used = 0f;
                for (int i = 0; i < blocks.Count; i++) used += gaps[i] + heights[i];

                // What the copy leaves of the band is shared out rather than left as a hole over
                // the button: each gap grows by up to MaxExtraGap, and whatever is still spare
                // splits above and below, so the copy sits centred between picture and button.
                float spare = Mathf.Max(0f, band - used);
                int inner = Mathf.Max(0, blocks.Count - 1);
                float extra = inner == 0 ? 0f : Mathf.Min(MaxExtraGap, spare * 0.5f / inner);
                float y = (spare - extra * inner) * 0.5f;
                for (int i = 0; i < blocks.Count; i++)
                {
                    y += gaps[i] + (i > 0 ? extra : 0f);
                    Place(blocks[i], y, heights[i]);
                    y += heights[i];
                }

                void Add(RectTransform rect, float height, float gap)
                {
                    blocks.Add(rect);
                    heights.Add(height);
                    gaps.Add(gap);
                }
            }

            Overflow = Mathf.Max(0f, used - band);
            if (Overflow > 0.5f)
                Debug.LogWarning("[FirstRunGuide] '" + page.Title + "' runs " + Overflow.ToString("0") +
                                 " px past its band - shorten the copy (GuideState budgets).");
        }

        /// A movement and what it does: the verb on a chip, the rest wrapped beside it, the row
        /// as tall as whichever of the two is taller. Built unplaced; LayOutCopy places it.
        RectTransform Step(GuideRow row, int index, float width, out float height)
        {
            RuntimeUi.Element("Step" + index, _copy, out var rect);

            RuntimeUi.Element("Chip", rect, out var chip);
            chip.anchorMin = chip.anchorMax = new Vector2(0f, 1f);
            chip.pivot = new Vector2(0f, 1f);
            chip.anchoredPosition = Vector2.zero;
            chip.sizeDelta = new Vector2(ChipWidth, ChipHeight);
            CosmeticUi.Surface(chip, MenuTheme.Slot, ChipHeight * 0.5f).raycastTarget = false;
            const float chipInset = 16f;
            var key = RuntimeUi.Label("Key", chip, Vector2.zero, Vector2.one,
                new Vector2(chipInset, 0f), new Vector2(-chipInset, 0f), 28, TextAnchor.MiddleCenter, TextColor);
            key.fontStyle = FontStyle.Bold;
            key.text = row.Key;
            // Always one line: a verb that is too wide steps its size down to fit. Best-fit with
            // wrapping would rather break "SWIPE DOWN" in two and fill the pill edge to edge.
            float room = ChipWidth - 2f * chipInset;
            if (key.preferredWidth > room)
                key.fontSize = Mathf.Max(20, Mathf.FloorToInt(key.fontSize * room / key.preferredWidth));

            var text = RuntimeUi.Label("Text", rect, new Vector2(0f, 1f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero, StepSize, TextAnchor.MiddleLeft, TextColor);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.text = row.Text;
            float textWidth = width - ChipWidth - Gap;
            float textHeight = Measure(text, textWidth);
            height = Mathf.Max(ChipHeight, textHeight);

            // The text is centred on the chip when it is one line, and top-aligned with it
            // when it wraps, so a two-line row still starts level with its verb.
            text.alignment = textHeight <= ChipHeight ? TextAnchor.MiddleLeft : TextAnchor.UpperLeft;
            var textRect = text.rectTransform;
            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.pivot = new Vector2(0.5f, 1f);
            textRect.offsetMin = new Vector2(ChipWidth + Gap, -(textHeight <= ChipHeight ? ChipHeight : textHeight));
            textRect.offsetMax = new Vector2(0f, textHeight <= ChipHeight ? 0f : -2f);
            return rect;
        }

        /// One way of playing: an outlined paper box with the mode's own picture on the left -
        /// the first frame of its page's sequence, cropped square to the middle where the hands
        /// and the phone are - and the heading, an optional pill and the sentence on the right.
        float Tile(GuideRow row, int index, float y, float width, float height)
        {
            RuntimeUi.Element("Tile" + index, _copy, out var rect);
            RoundedFill("Border", rect, 0f, MenuTheme.Empty, 32f);
            RoundedFill("Paper", rect, 3f, PanelColor, 29f);
            Place(rect, y, height);

            const float pad = 28f;
            float thumb = Mathf.Min(300f, height - 2f * pad);
            RuntimeUi.Element("Thumb", rect, out var thumbRect);
            thumbRect.anchorMin = thumbRect.anchorMax = new Vector2(0f, 0.5f);
            thumbRect.pivot = new Vector2(0f, 0.5f);
            thumbRect.anchoredPosition = new Vector2(pad, 0f);
            thumbRect.sizeDelta = new Vector2(thumb, thumb);
            ShowThumbnail(thumbRect, row.Thumbnail);

            float left = pad + thumb + pad;
            float textWidth = width - left - pad;

            var heading = RuntimeUi.Label("Heading", rect, new Vector2(0f, 1f), new Vector2(0f, 1f),
                Vector2.zero, Vector2.zero, TileHeadingSize, TextAnchor.MiddleLeft, TextColor);
            heading.fontStyle = FontStyle.Bold;
            heading.text = row.Key;
            float headingWidth = Mathf.Min(heading.preferredWidth, textWidth);
            const float headingHeight = 52f;

            var body = RuntimeUi.Label("Text", rect, new Vector2(0f, 1f), new Vector2(0f, 1f),
                Vector2.zero, Vector2.zero, TileTextSize, TextAnchor.UpperLeft, TextColor);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.text = row.Text;
            body.rectTransform.sizeDelta = new Vector2(textWidth, 0f);
            float bodyHeight = Measure(body, textWidth);

            // Heading and sentence as one block, centred in the tile beside the picture.
            float block = headingHeight + 10f + bodyHeight;
            float top = -Mathf.Max(pad, (height - block) * 0.5f);

            PlaceAt(heading.rectTransform, left, top, headingWidth, headingHeight);
            PlaceAt(body.rectTransform, left, top - headingHeight - 10f, textWidth, bodyHeight);

            if (!string.IsNullOrEmpty(row.Tag))
            {
                RuntimeUi.Element("Tag", rect, out var tag);
                PlaceAt(tag, left + headingWidth + 16f, top - 8f, 96f, 38f);
                CosmeticUi.Surface(tag, MenuTheme.Gold, 19f).raycastTarget = false;
                var tagLabel = RuntimeUi.Label("Label", tag, Vector2.zero, Vector2.one,
                    Vector2.zero, Vector2.zero, 22, TextAnchor.MiddleCenter, MenuTheme.OnAccent);
                tagLabel.fontStyle = FontStyle.Bold;
                tagLabel.text = row.Tag;
            }

            return y + height;
        }

        /// The first frame of a sequence, cropped to its centre square. A RawImage rather than an
        /// Image because the crop is a uvRect; the sprite's own texture rect is honoured so an
        /// atlased import would still crop the right region. Missing art leaves a slot-coloured
        /// square - the tile still reads, it just has no picture.
        static void ShowThumbnail(RectTransform slot, string key)
        {
            var sprite = Resources.Load<Sprite>(GuideIllustration.FramePath(key, 1));
            if (sprite == null)
            {
                CosmeticUi.Surface(slot, MenuTheme.Slot, 24f).raycastTarget = false;
                return;
            }

            var art = Framed("Frame", slot, 24f, out _);
            var image = art.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            image.texture = sprite.texture;

            Rect source = sprite.textureRect;
            float side = Mathf.Min(source.width, source.height);
            float texW = sprite.texture.width, texH = sprite.texture.height;
            image.uvRect = new Rect(
                (source.x + (source.width - side) * 0.5f) / texW,
                (source.y + (source.height - side) * 0.5f) / texH,
                side / texW, side / texH);
        }

        Text CopyLabel(string name, string text, int size, Color color, TextAnchor alignment)
        {
            var label = RuntimeUi.Label(name, _copy, new Vector2(0f, 1f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero, size, alignment, color);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.text = text;
            return label;
        }

        /// The wrapped height of a label at a given width, in card pixels. The card is a fixed
        /// size, so every width here is known at build time and the stack is exact.
        static float Measure(Text text, float width)
        {
            var settings = text.GetGenerationSettings(new Vector2(width, 0f));
            settings.scaleFactor = 1f;
            float height = text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text, settings);
            return Mathf.Ceil(height) + 2f;
        }

        /// Full-width placement inside the copy column, `y` px down from its top.
        static void Place(RectTransform rect, float y, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, -y - height);
            rect.offsetMax = new Vector2(0f, -y);
        }

        static void PlaceAt(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, top);
            rect.sizeDelta = new Vector2(width, height);
        }

        /// Every frame of the page's sequence that actually exists, in order, or null when none
        /// does. A frame missing from the middle is skipped rather than shown as a hole: the
        /// owner dropping in three of four files should see three frames cycle, not a blank.
        static Sprite[] LoadFrames(GuidePage page)
        {
            if (!page.HasIllustration) return null;

            var found = new List<Sprite>(page.FrameCount);
            for (int i = 1; i <= page.FrameCount; i++)
            {
                var sprite = Resources.Load<Sprite>(GuideIllustration.FramePath(page.Illustration, i));
                if (sprite != null) found.Add(sprite);
            }
            return found.Count == 0 ? null : found.ToArray();
        }

        /// The GIF substitute: hold each frame for SecondsPerFrame, then the next, round and
        /// round. Unscaled time, because the guide can be up while the game behind it is frozen
        /// at timeScale zero. A still, or a placeholder, has nothing to advance.
        void Update()
        {
            if (_frames == null || _frames.Length < 2) return;

            _frameClock += Time.unscaledDeltaTime;
            if (_frameClock < GuideIllustration.SecondsPerFrame) return;

            _frameClock -= GuideIllustration.SecondsPerFrame;
            _frameIndex = GuideIllustration.NextFrame(_frameIndex, _frames.Length);
            _frame.sprite = _frames[_frameIndex];
        }

        void Close(GuideExit exit)
        {
            if (GuideState.MarksSeen(exit)) MarkSeen();
            // Reading to the end is the one exit worth a confirm; a skip is just a back.
            if (exit == GuideExit.Completed) GameAudio.Confirm();
            Closed?.Invoke();
        }

        /// Written only when it changes: re-opening the guide from the profile tab is not a
        /// reason to touch the disk.
        static void MarkSeen()
        {
            if (PlayerPrefs.GetInt(GuideState.SeenKey, 0) == GuideState.SeenValue) return;
            PlayerPrefs.SetInt(GuideState.SeenKey, GuideState.SeenValue);
            PlayerPrefs.Save();
        }
    }
}
