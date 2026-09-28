using System;
using MotionRunner.Core;
using MotionRunner.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace MotionRunner.Menu
{
    /// The banner at the top of the home screen: Season 1, the level ladder, and the XP bar
    /// towards the next level (COSMETICS_CATALOG §3).
    ///
    /// Dressed as the shop's own season-pass card - the ink-to-teal wash with paper type, gold for
    /// progress - so the banner on the home tab and the product in the shop read as the same
    /// thing. Everything on it is true: the pass entitlement is live, so OWNED vs VIEW REWARDS is
    /// real, and tapping the card is a real route to the thing that sells it.
    ///
    /// The whole card is one tap target. It is the biggest thing on the home screen and it is an
    /// advertisement; making the player find a button on it would be pretending otherwise.
    public sealed class SeasonPassCard
    {
        public const float Height = 200f;

        const float PillWidth = 236f;

        /// The XP track on the dark wash: paper at low opacity, so it reads as an empty groove.
        static readonly Color Track = new Color(1f, 1f, 1f, 0.16f);

        readonly ISeasonProgress _progress;
        readonly Func<bool> _passOwned;

        Text _state;
        CosmeticPanel _statePill;
        Text _level;
        Text _note;
        RectTransform _fill;

        public SeasonPassCard(ISeasonProgress progress, Func<bool> passOwned)
        {
            _progress = progress;
            _passOwned = passOwned;
        }

        public void Build(RectTransform slot, Action onTap)
        {
            var background = CosmeticUi.Card(slot, MenuTheme.Text);
            background.Gradient = true;
            background.Bottom = MenuTheme.PassGradient;
            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() => onTap());
            RuntimeUi.TapSound(button);

            float pad = MenuTheme.CardPadding;

            var title = RuntimeUi.Label("Title", slot,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(pad, -72f), new Vector2(-pad - PillWidth - 12f, -18f),
                36, TextAnchor.MiddleLeft, MenuTheme.OnAccent);
            title.fontStyle = FontStyle.Bold;
            title.text = "SEASON 1 PASS";

            // The state as a pill on the title row: the one piece of the banner that changes when
            // the pass is bought, so it gets the contrast.
            RuntimeUi.Element("State", slot, out var pill);
            pill.anchorMin = pill.anchorMax = new Vector2(1f, 1f);
            pill.pivot = new Vector2(1f, 1f);
            pill.anchoredPosition = new Vector2(-pad, -22f);
            pill.sizeDelta = new Vector2(PillWidth, 48f);
            _statePill = CosmeticUi.Surface(pill, MenuTheme.ItemCard, 24f);
            _statePill.raycastTarget = false;
            _state = RuntimeUi.Label("Label", pill, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, 22, TextAnchor.MiddleCenter, MenuTheme.Text);
            _state.fontStyle = FontStyle.Bold;

            _level = RuntimeUi.Label("Level", slot,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(pad, -112f), new Vector2(-pad, -76f),
                28, TextAnchor.MiddleLeft, MenuTheme.PreviewTop);

            // A rounded groove and a rounded gold fill, moved by anchor like RuntimeUi.Bar so it
            // stays aligned with its track at any canvas scale.
            RuntimeUi.Element("Xp", slot, out var track);
            RuntimeUi.Stretch(track, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(pad, -146f), new Vector2(-pad, -124f));
            CosmeticUi.Surface(track, Track, 11f).raycastTarget = false;
            RuntimeUi.Element("Fill", track, out _fill);
            RuntimeUi.Stretch(_fill, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            CosmeticUi.Surface(_fill, MenuTheme.Gold, 11f).raycastTarget = false;

            _note = RuntimeUi.Label("Note", slot,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(pad, -190f), new Vector2(-pad, -154f),
                24, TextAnchor.MiddleLeft, MenuTheme.Owned);

            Refresh();
        }

        public void Refresh()
        {
            if (_state == null) return;

            bool owned = _passOwned != null && _passOwned();
            var snapshot = _progress.Read(owned);

            _state.text = owned ? "PASS OWNED" : "VIEW REWARDS";
            _statePill.color = owned ? MenuTheme.Premium : MenuTheme.ItemCard;

            _level.text = "LEVEL " + snapshot.Level + " / " + SeasonProgress.MaxLevel;

            RuntimeUi.SetBarFill(_fill, snapshot.Fraction);

            _note.text = snapshot.IsMaxLevel ? "All levels reached · collect your rewards" : snapshot.IsLive
                ? snapshot.Xp + " / " + snapshot.XpForNextLevel + " XP to level " + (snapshot.Level + 1)
                : SeasonProgress.NotLiveNote;
        }
    }
}
