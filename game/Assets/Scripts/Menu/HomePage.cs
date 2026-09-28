using System;
using MotionRunner.CameraInput;
using MotionRunner.Core;
using MotionRunner.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace MotionRunner.Menu
{
    /// The middle tab: recorded season progress, daily stamps, the equipped runner opening
    /// the locker, and the two ways to start a run. Hidden pages stop their preview cameras.
    public sealed class HomePage : MenuPage
    {
        /// Top margin above the first card.
        const float TopOffset = 12f;

        SeasonPassCard _seasonPass;
        DailyStampsCard _stamps;
        ModePickerCard _modes;
        RunnerPreview _preview;

        /// Forwarded from the card, so MainMenu never reaches through a page into a card.
        public event Action<bool, FaceTrackingRig> Chosen;

        public bool IsStagingCamera => _modes != null && _modes.IsStagingCamera;
        public bool HasPendingPick => _modes != null && _modes.HasPendingPick;

        public void CancelStaging() => _modes?.CancelStaging();

        protected override void Build()
        {
            // The profile's page colour, so the home cards read as cards on it - the same paper
            // on sage the other two tabs use.
            RuntimeUi.Panel("Scrim", Root, MenuTheme.Scrim).raycastTarget = false;

            var stack = new MenuStack(Root, TopOffset);

            _seasonPass = new SeasonPassCard(Menu.Season, Menu.IsPassOwned);
            _seasonPass.Build(stack.Add("SeasonPass", SeasonPassCard.Height), Menu.ShowSeason);

            _stamps = new DailyStampsCard(() => DateTime.UtcNow);
            _stamps.Build(stack.Add("Stamps", DailyStampsCard.Height));

            BuildLocker(stack.Y);
            var picker = BuildPickerSlot();
            _modes = new ModePickerCard();
            _modes.Chosen += OnChosen;
            _modes.Build(picker, () => Menu.RequestGuide());
        }

        /// The character fills the space between the fixed cards and the mode picker, standing on
        /// the locker's own teal-to-cream stage - the wash the result card and the locker use -
        /// so the runner the player dresses looks the same in all three places. The whole stage
        /// opens the locker; the pill at its foot says so.
        void BuildLocker(float stackBottom)
        {
            RuntimeUi.Element("CharacterLocker", Root, out var character);
            RuntimeUi.Stretch(character, Vector2.zero, Vector2.one,
                new Vector2(MenuTheme.SidePadding, ModePickerCard.Height + 2f * MenuTheme.CardGap),
                new Vector2(-MenuTheme.SidePadding, stackBottom - MenuTheme.CardGap));
            // Teal to cream: the locker's wash, ending on the item-card cream rather than the
            // wash's own pale sage, which is the page colour and lost the stage's bottom edge.
            var wash = CosmeticUi.Card(character, MenuTheme.PreviewTop);
            wash.Gradient = true;
            wash.Bottom = MenuTheme.ItemCard;

            _preview = RunnerPreview.Create(character);

            var target = RuntimeUi.Panel("OpenLocker", character, new Color(0, 0, 0, 0));
            var open = target.gameObject.AddComponent<Button>();
            open.targetGraphic = target;
            open.transition = Selectable.Transition.None;
            open.onClick.AddListener(Menu.ShowCosmetics);
            RuntimeUi.TapSound(open);

            RuntimeUi.Label("YourLook", character, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(MenuTheme.CardPadding, -64f), new Vector2(-MenuTheme.CardPadding, -18f),
                26, TextAnchor.MiddleLeft, MenuTheme.Dim).text = "YOUR LOOK";

            // Visual only - the stage is the button.
            RuntimeUi.Element("Customize", character, out var pill);
            pill.anchorMin = pill.anchorMax = new Vector2(0.5f, 0f);
            pill.pivot = new Vector2(0.5f, 0f);
            pill.anchoredPosition = new Vector2(0f, 20f);
            pill.sizeDelta = new Vector2(300f, 64f);
            CosmeticUi.Surface(pill, MenuTheme.ItemCard, 32f).raycastTarget = false;
            var label = RuntimeUi.Label("Label", pill, Vector2.zero, Vector2.one,
                new Vector2(0f, 0f), new Vector2(-30f, 0f), 26, TextAnchor.MiddleCenter, MenuTheme.Text);
            label.fontStyle = FontStyle.Bold;
            label.text = "CUSTOMIZE";
            RuntimeUi.Element("Chevron", pill, out var chevron);
            chevron.anchorMin = chevron.anchorMax = new Vector2(1f, 0.5f);
            chevron.anchoredPosition = new Vector2(-44f, 0f);
            chevron.sizeDelta = new Vector2(24f, 24f);
            MenuIcons.Chevron(chevron, MenuTheme.Text, length: 16f, thickness: 4f);
        }

        /// The picker is anchored to the BOTTOM of the page rather than placed by the stack: it is
        /// the thing the thumb reaches for, so it belongs a fixed distance above the tab bar no
        /// matter how tall the screen is.
        RectTransform BuildPickerSlot()
        {
            RuntimeUi.Element("ModePicker", Root, out var rect);
            // A card gap above the tab bar, like every gap between two cards: flush, the card's
            // rounded bottom sat on the bar's paper and read as cut off.
            RuntimeUi.Stretch(rect,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(MenuTheme.SidePadding, MenuTheme.CardGap),
                new Vector2(-MenuTheme.SidePadding, MenuTheme.CardGap + ModePickerCard.Height));
            return rect;
        }

        void OnChosen(bool cameraMode, FaceTrackingRig rig) => Chosen?.Invoke(cameraMode, rig);

        public override void OnShown() => RefreshCards();

        /// Re-reads everything the cards display. Called when the tab comes up, and again whenever
        /// entitlements change under it - buying the pass from the shop repaints the banner here
        /// without the player having to come back to look.
        public void RefreshCards()
        {
            _seasonPass?.Refresh();
            _stamps?.Refresh();
            _preview?.Show(Menu.Skins.Effective);
        }

        /// Switching tabs abandons a camera setup in progress AND a pick that has not committed
        /// yet - Abandon covers both, where CancelStaging misses the one frame between staging
        /// turning Ready and the Commit that follows. The rig would otherwise stay live behind the
        /// shop with nothing ticking it - a camera held open behind a price list is exactly the
        /// look the on-device-only promise is meant to rule out (and it is the same reason
        /// PauseState releases the camera while the pause menu is up).
        public override void OnHidden() => _modes?.Abandon();

        void Update()
        {
            // Only while this page is the one on screen: a page that is hidden is deactivated, so
            // Unity stops calling this - which is also what stops camera staging from continuing
            // behind the shop.
            _modes?.Tick();
        }

        void OnDestroy() => _modes?.Dispose();
    }
}
