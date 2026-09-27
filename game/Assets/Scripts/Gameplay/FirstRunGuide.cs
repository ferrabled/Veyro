using System;
using MotionRunner.Audio;
using MotionRunner.Core;
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
    /// play" link on the mode picker. It is never shown during a run - it explains the choice
    /// the player is about to make, and RunFlow dismisses it if a run somehow starts underneath
    /// (camera staging can finish while it is up).
    ///
    /// Every way out marks it seen, skipping included (GuideState.MarksSeen). Dismissing is the
    /// only thing that writes the flag: a guide abandoned by force-quitting mid-read comes back
    /// once, which is the friendlier way to be wrong.
    ///
    /// Each mode page has a picture above its copy: a frame sequence loaded from
    /// Resources/Art/Guide (GuideIllustration names the files, docs/GUIDE_ILLUSTRATIONS.md is the
    /// art brief), cycled in unscaled time when there is more than one frame - the closest thing
    /// to a GIF UGUI can show. All ten frames ship with the game. If a sequence cannot load,
    /// the slot falls back to its instruction caption without exposing asset filenames.
    public sealed class FirstRunGuide : MonoBehaviour
    {
        static readonly Color TextColor = Menu.MenuTheme.Text;
        static readonly Color DimColor = new Color(0.04f, 0.05f, 0.09f, 0.88f);
        // Opaque paper, not MenuTheme.Card: Card is 98% alpha, which over the dim lands a few
        // levels darker than #FBF5E9 and outlines the opaque illustration as a lighter rectangle.
        static readonly Color PanelColor = Art.ParkTheme.Paper;
        static readonly Color AccentColor = Menu.MenuTheme.Accent;
        static readonly Color StatusColor = Menu.MenuTheme.Dim;
        static readonly Color LinkColor = Menu.MenuTheme.Faint;

        // ---- layout, from the top of the 900x1300 card ----

        /// The picture: 804 wide (the card less the body's 48 px margins) by 502, the 16:10 the
        /// art is delivered in, so an opaque frame melts into the card with no letterbox.
        const float SlotTop = -228f;
        const float SlotWidth = 804f;
        const float SlotHeight = 502f;

        /// Where the copy starts under a picture, and where it starts when a page has none and
        /// takes the height instead.
        const float BodyTopUnderSlot = SlotTop - SlotHeight - 18f;
        const float BodyTopFullHeight = SlotTop;

        /// Just above the primary button (bottom-anchored at 100, 132 tall: its top is 1134 down).
        const float BodyBottom = -1118f;

        /// The guide is done with. RunFlow owns the screen's lifetime, exactly as it does for the
        /// pause menu, so nothing here destroys itself.
        public event Action Closed;

        readonly GuideState _state = new GuideState();

        Text _title;
        Text _body;
        Text _counter;
        Text _primaryLabel;
        GameObject _backLink;

        GameObject _slot;
        Image _frame;
        GameObject _placeholder;
        Text _caption;

        Sprite[] _frames;
        int _frameIndex;
        float _frameClock;

        /// True when this player has never been shown the guide. Read once, at the mode picker.
        public static bool IsDue =>
            GuideState.ShouldShowOnLaunch(PlayerPrefs.GetInt(GuideState.SeenKey, 0));

        public static FirstRunGuide Show()
        {
            var go = new GameObject("FirstRunGuide");
            var guide = go.AddComponent<FirstRunGuide>();
            guide.Build();
            return guide;
        }

        /// The skip link and the Android back button. Both count as seen - a guide that reappears
        /// every launch because it was skipped is not help, it is a nag.
        public void Dismiss() => Close(GuideExit.Skipped);

        void Build()
        {
            // Above the mode picker (100) and below the pause menu (150): the guide only ever
            // covers the picker, and a run - which is the only thing that can be paused - is
            // never underneath it.
            RuntimeUi.PortraitCanvas(gameObject, 120);

            RuntimeUi.FullScreenPanel("Dim", transform, DimColor);
            var card = RuntimeUi.Card("Card", transform, new Vector2(900f, 1300f), PanelColor);

            _backLink = BuildLink("Back", card.transform,
                new Vector2(0f, 1f), new Vector2(0.35f, 1f),
                new Vector2(28f, -92f), new Vector2(0f, -32f),
                TextAnchor.MiddleLeft, "< back", () => Page(-1));

            BuildLink("Skip", card.transform,
                new Vector2(0.65f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -92f), new Vector2(-28f, -32f),
                TextAnchor.MiddleRight, "skip", Dismiss);

            _title = RuntimeUi.Label("Title", card.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(28f, -176f), new Vector2(-28f, -100f),
                56, TextAnchor.MiddleCenter, TextColor);

            _counter = RuntimeUi.Label("Counter", card.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(28f, -216f), new Vector2(-28f, -180f),
                30, TextAnchor.MiddleCenter, StatusColor);

            BuildSlot(card.transform);

            _body = RuntimeUi.Label("Body", card.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(48f, BodyBottom), new Vector2(-48f, BodyTopUnderSlot),
                34, TextAnchor.UpperLeft, TextColor);
            // RuntimeUi.Label overflows by default, which is right for a score and wrong for a
            // paragraph: without this a long line runs off the card instead of wrapping.
            _body.horizontalOverflow = HorizontalWrapMode.Wrap;

            _primaryLabel = RuntimeUi.TextButton("Primary", card.transform,
                new Vector2(0.5f, 0f), new Vector2(0f, 100f), new Vector2(560f, 132f),
                AccentColor, string.Empty, 50, Menu.MenuTheme.OnAccent,
                Advance).GetComponentInChildren<Text>();

            ShowPage();
        }

        /// The picture slot under the counter: the frame Image and a rounded caption fallback
        /// if the sequence cannot load. ShowPage picks which of the two is active.
        void BuildSlot(Transform card)
        {
            _slot = RuntimeUi.Element("Illustration", card, out var slotRect);
            RuntimeUi.Stretch(slotRect, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2((900f - SlotWidth) * 0.5f, SlotTop - SlotHeight),
                new Vector2(-(900f - SlotWidth) * 0.5f, SlotTop));

            var frameGo = RuntimeUi.Element("Frame", _slot.transform, out var frameRect);
            RuntimeUi.Stretch(frameRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _frame = frameGo.AddComponent<Image>();
            _frame.preserveAspect = true;
            _frame.raycastTarget = false;

            _placeholder = RuntimeUi.Element("Placeholder", _slot.transform, out var holderRect);
            RuntimeUi.Stretch(holderRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            RoundedFill("Panel", _placeholder.transform, 0f, Menu.MenuTheme.Slot, 28f);
            RoundedFill("Outline", _placeholder.transform, 12f, Menu.MenuTheme.Empty, 18f);
            RoundedFill("Inner", _placeholder.transform, 16f, Menu.MenuTheme.Slot, 14f);

            _caption = RuntimeUi.Label("Caption", _placeholder.transform,
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(40f, -40f), new Vector2(-40f, 40f),
                30, TextAnchor.MiddleCenter, StatusColor);
            _caption.horizontalOverflow = HorizontalWrapMode.Wrap;

        }

        /// A rounded rectangle inset from its parent by `inset` on every side. Three of them,
        /// alternating colours, draw the placeholder's outlined look with nothing but canvas
        /// geometry (CosmeticPanel), so the fallback needs no image asset of its own.
        static void RoundedFill(string name, Transform parent, float inset, Color color, float radius)
        {
            var go = RuntimeUi.Element(name, parent, out var rect);
            RuntimeUi.Stretch(rect, Vector2.zero, Vector2.one,
                new Vector2(inset, inset), new Vector2(-inset, -inset));
            var panel = go.AddComponent<Menu.CosmeticPanel>();
            panel.color = color;
            panel.Radius = radius;
            panel.raycastTarget = false;
        }

        /// A dim tappable line, the same shape as the mode picker's privacy link, so the guide's
        /// secondary affordances read like the screen it opens over.
        GameObject BuildLink(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax, TextAnchor alignment, string label, Action onTap)
        {
            var text = RuntimeUi.Label(name, parent, anchorMin, anchorMax, offsetMin, offsetMax,
                32, alignment, LinkColor);
            text.text = label;
            text.raycastTarget = true;

            var button = text.gameObject.AddComponent<Button>();
            button.targetGraphic = text;
            button.onClick.AddListener(() => onTap());
            RuntimeUi.TapSound(button, Sfx.UiBack); // both links here step backwards or out
            return text.gameObject;
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
            _body.text = page.Body;
            _counter.text = (_state.Index + 1) + " / " + GuideState.Pages.Length;
            _primaryLabel.text = _state.PrimaryLabel;

            // Hidden rather than greyed on page one: there is nothing behind the first page, and
            // a dead link is worse than no link.
            _backLink.SetActive(!_state.IsFirst);

            ShowIllustration(page);
        }

        /// The picture for this page, or its placeholder, or neither - and the body's top edge
        /// follows: a page without a picture gets the height for its copy.
        void ShowIllustration(GuidePage page)
        {
            _frames = LoadFrames(page);
            _frameIndex = 0;
            _frameClock = 0f;

            _slot.SetActive(page.HasIllustration);
            _body.rectTransform.offsetMax = new Vector2(-48f,
                page.HasIllustration ? BodyTopUnderSlot : BodyTopFullHeight);
            if (!page.HasIllustration) return;

            bool hasArt = _frames != null;
            _frame.gameObject.SetActive(hasArt);
            _placeholder.SetActive(!hasArt);
            if (hasArt)
            {
                _frame.sprite = _frames[0];
                return;
            }

            _caption.text = page.Caption;
        }

        /// Every frame of the page's sequence that actually exists, in order, or null when none
        /// does. A frame missing from the middle is skipped rather than shown as a hole: the
        /// owner dropping in three of four files should see three frames cycle, not a blank.
        static Sprite[] LoadFrames(GuidePage page)
        {
            if (!page.HasIllustration) return null;

            var found = new System.Collections.Generic.List<Sprite>(page.FrameCount);
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

        /// Written only when it changes: re-opening the guide from the mode picker is not a
        /// reason to touch the disk.
        static void MarkSeen()
        {
            if (PlayerPrefs.GetInt(GuideState.SeenKey, 0) == GuideState.SeenValue) return;
            PlayerPrefs.SetInt(GuideState.SeenKey, GuideState.SeenValue);
            PlayerPrefs.Save();
        }
    }
}
