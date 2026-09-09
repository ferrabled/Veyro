using System;
using MotionRunner.CameraInput;
using MotionRunner.Core;
using MotionRunner.Progression;
using UnityEngine;

namespace MotionRunner.Menu
{
    /// The middle tab, and the one the app opens on: season pass at the top, the daily stamp card
    /// under it, the live run showing through the middle, and the two ways to start a run at the
    /// bottom.
    ///
    /// The window in the middle is not a component - it is a gap the stack leaves and nothing
    /// draws in. Everything the player sees through it is the real game: TrackDirector generating
    /// a real seeded track and RunnerController being steered by AutoPilot, run by AttractRun
    /// behind this canvas. That is why the cards are translucent and why the page has no backdrop
    /// of its own.
    public sealed class HomePage : MenuPage
    {
        /// Top margin above the first card.
        const float TopOffset = 12f;

        SeasonPassCard _seasonPass;
        DailyStampsCard _stamps;
        ModePickerCard _modes;

        /// Forwarded from the card, so MainMenu never reaches through a page into a card.
        public event Action<bool, FaceTrackingRig> Chosen;

        public bool IsStagingCamera => _modes != null && _modes.IsStagingCamera;

        public void CancelStaging() => _modes?.CancelStaging();

        protected override void Build()
        {
            var stack = new MenuStack(Root, TopOffset);

            _seasonPass = new SeasonPassCard(new PlaceholderSeasonProgress(), Menu.IsPassOwned);
            _seasonPass.Build(stack.Add("SeasonPass", SeasonPassCard.Height), () => Menu.Show(MenuTab.Shop));

            _stamps = new DailyStampsCard(() => DateTime.UtcNow);
            _stamps.Build(stack.Add("Stamps", DailyStampsCard.Height));

            // The window onto the live run. Whatever height is left after the cards above and the
            // picker below, which is how the layout stays honest on a taller or shorter phone:
            // the furniture keeps its size and the game gets the rest.
            var picker = BuildPickerSlot();
            _modes = new ModePickerCard();
            _modes.Chosen += OnChosen;
            _modes.Build(picker);
        }

        /// The picker is anchored to the BOTTOM of the page rather than placed by the stack: it is
        /// the thing the thumb reaches for, so it belongs a fixed distance above the tab bar no
        /// matter how tall the screen is.
        RectTransform BuildPickerSlot()
        {
            RuntimeUi.Element("ModePicker", Root, out var rect);
            RuntimeUi.Stretch(rect,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(MenuTheme.SidePadding, 0f),
                new Vector2(-MenuTheme.SidePadding, ModePickerCard.Height));
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
