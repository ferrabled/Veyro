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
    /// XP is not earned yet - that is T-025 - and the card says so rather than drawing a zero
    /// that reads like a bug or a bar that reads like progress. Everything else on it is already
    /// true: the pass entitlement is live, so OWNED vs LOCKED is real, and tapping the card is a
    /// real route to the thing that sells it.
    ///
    /// The whole card is one tap target. It is the biggest thing on the home screen and it is an
    /// advertisement; making the player find a button on it would be pretending otherwise.
    public sealed class SeasonPassCard
    {
        public const float Height = 200f;

        readonly ISeasonProgress _progress;
        readonly Func<bool> _passOwned;

        Text _state;
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
            var background = RuntimeUi.Panel("Card", slot, MenuTheme.Card);
            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() => onTap());
            RuntimeUi.TapSound(button);

            float pad = MenuTheme.CardPadding;

            RuntimeUi.Label("Title", slot,
                new Vector2(0f, 1f), new Vector2(0.6f, 1f),
                new Vector2(pad, -56f), new Vector2(0f, -8f),
                38, TextAnchor.MiddleLeft, MenuTheme.Text).text = "SEASON 1 PASS";

            _state = RuntimeUi.Label("State", slot,
                new Vector2(0.4f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -56f), new Vector2(-pad, -8f),
                30, TextAnchor.MiddleRight, MenuTheme.Dim);

            _level = RuntimeUi.Label("Level", slot,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(pad, -102f), new Vector2(-pad, -60f),
                30, TextAnchor.MiddleLeft, MenuTheme.Dim);

            _fill = RuntimeUi.Bar("Xp", slot,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(pad, -142f), new Vector2(-pad, -112f),
                MenuTheme.Empty, MenuTheme.Gold);

            _note = RuntimeUi.Label("Note", slot,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(pad, -192f), new Vector2(-pad, -150f),
                26, TextAnchor.MiddleLeft, MenuTheme.Faint);

            Refresh();
        }

        public void Refresh()
        {
            if (_state == null) return;

            bool owned = _passOwned != null && _passOwned();
            var snapshot = _progress.Read(owned);

            _state.text = owned ? "PASS OWNED · VIEW" : "VIEW REWARDS";
            _state.color = owned ? MenuTheme.Accent : MenuTheme.Dim;

            _level.text = "LEVEL " + snapshot.Level + " / " + SeasonProgress.MaxLevel;

            RuntimeUi.SetBarFill(_fill, snapshot.Fraction);

            _note.text = snapshot.IsMaxLevel ? "All levels reached · collect your rewards" : snapshot.IsLive
                ? snapshot.Xp + " / " + snapshot.XpForNextLevel + " XP to level " + (snapshot.Level + 1)
                : SeasonProgress.NotLiveNote;
        }
    }
}
