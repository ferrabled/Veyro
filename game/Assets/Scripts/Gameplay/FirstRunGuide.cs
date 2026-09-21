using System;
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
    public sealed class FirstRunGuide : MonoBehaviour
    {
        static readonly Color TextColor = Menu.MenuTheme.Text;
        static readonly Color DimColor = new Color(0.04f, 0.05f, 0.09f, 0.88f);
        static readonly Color PanelColor = Menu.MenuTheme.Card;
        static readonly Color AccentColor = Menu.MenuTheme.Accent;
        static readonly Color StatusColor = Menu.MenuTheme.Dim;
        static readonly Color LinkColor = Menu.MenuTheme.Faint;

        /// The guide is done with. RunFlow owns the screen's lifetime, exactly as it does for the
        /// pause menu, so nothing here destroys itself.
        public event Action Closed;

        readonly GuideState _state = new GuideState();

        Text _title;
        Text _body;
        Text _counter;
        Text _primaryLabel;
        GameObject _backLink;

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
                new Vector2(28f, -212f), new Vector2(-28f, -124f),
                56, TextAnchor.MiddleCenter, TextColor);

            _counter = RuntimeUi.Label("Counter", card.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(28f, -264f), new Vector2(-28f, -216f),
                30, TextAnchor.MiddleCenter, StatusColor);

            _body = RuntimeUi.Label("Body", card.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(48f, -1110f), new Vector2(-48f, -300f),
                40, TextAnchor.UpperLeft, TextColor);
            // RuntimeUi.Label overflows by default, which is right for a score and wrong for a
            // paragraph: without this a long line runs off the card instead of wrapping.
            _body.horizontalOverflow = HorizontalWrapMode.Wrap;

            _primaryLabel = RuntimeUi.TextButton("Primary", card.transform,
                new Vector2(0.5f, 0f), new Vector2(0f, 112f), new Vector2(560f, 132f),
                AccentColor, string.Empty, 50, Menu.MenuTheme.OnAccent,
                Advance).GetComponentInChildren<Text>();

            ShowPage();
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
        }

        void Close(GuideExit exit)
        {
            if (GuideState.MarksSeen(exit)) MarkSeen();
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
