using System;
using MotionRunner.CameraInput;
using MotionRunner.Core;
using MotionRunner.Progression;
using UnityEngine;

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

        public void CancelStaging() => _modes?.CancelStaging();

        protected override void Build()
        {
            var stack = new MenuStack(Root, TopOffset);

            _seasonPass = new SeasonPassCard(Menu.Season, Menu.IsPassOwned);
            _seasonPass.Build(stack.Add("SeasonPass", SeasonPassCard.Height), Menu.ShowSeason);

            _stamps = new DailyStampsCard(() => DateTime.UtcNow);
            _stamps.Build(stack.Add("Stamps", DailyStampsCard.Height));

            // The character fills the space between the fixed cards and the mode picker.
            RuntimeUi.Element("CharacterLocker",Root,out var character);
            RuntimeUi.Stretch(character,Vector2.zero,Vector2.one,
                new Vector2(MenuTheme.SidePadding,ModePickerCard.Height+14),new Vector2(-MenuTheme.SidePadding,stack.Y-14));
            _preview=RunnerPreview.Create(character);
            var target=RuntimeUi.Panel("OpenLocker",character,new Color(0,0,0,0));
            var open=target.gameObject.AddComponent<UnityEngine.UI.Button>();open.targetGraphic=target;
            open.onClick.AddListener(Menu.ShowCosmetics);
            RuntimeUi.Label("LockerHint",character,Vector2.zero,new Vector2(1,0),new Vector2(12,8),new Vector2(-12,62),
                32,TextAnchor.MiddleCenter,MenuTheme.Text).text="YOUR LOOK  ·  TAP TO CUSTOMIZE";
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
