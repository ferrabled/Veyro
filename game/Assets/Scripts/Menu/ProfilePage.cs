using System;
using System.Collections.Generic;
using MotionRunner.Core;
using MotionRunner.Progression;
using MotionRunner.Track;
using UnityEngine;
using UnityEngine.UI;

namespace MotionRunner.Menu
{
    /// The right tab: who the player is, where they stand, and what they have been running.
    ///
    /// Two of the three cards are already real. Best score, streak and the recent-runs list all
    /// come off disk through ProgressStore, so they are true from the first run. The leaderboard
    /// is the one that cannot be - a shared board needs the database D10 defers past v1.0 - so it
    /// is drawn from MockLeaderboard behind ILeaderboardSource, with the player's own real daily
    /// best slotted into it and a line under it saying exactly that. A mocked board that does not
    /// admit it is mocked is a lie to the player and a trap for whoever reads a screenshot of it
    /// six months from now.
    public sealed class ProfilePage : MenuPage
    {
        const float TopOffset = 12f;
        const float PlayerHeight = 180f;
        const float BoardHeight = 520f;
        const float RunsHeight = 452f;
        const float LinksHeight = 180f;

        const float RowHeight = 62f;
        const int BoardRows = 6;
        const int RunRows = 5;

        readonly ILeaderboardSource _leaderboard = new MockLeaderboard();

        Text _headline;
        Text _subline;
        Text[] _boardRows;
        Text[] _runRows;
        Text _runsEmpty;

        protected override void Build()
        {
            RuntimeUi.Panel("Scrim", Root, MenuTheme.Scrim);

            var stack = new MenuStack(Root, TopOffset);
            BuildPlayerCard(stack.Add("Player", PlayerHeight));
            BuildBoardCard(stack.Add("Board", BoardHeight));
            BuildRunsCard(stack.Add("Runs", RunsHeight));
            BuildLinksCard(stack.Add("Links", LinksHeight));
        }

        void BuildPlayerCard(RectTransform slot)
        {
            RuntimeUi.Panel("Card", slot, MenuTheme.Card);
            float pad = MenuTheme.CardPadding;

            // A square avatar in the accent colour: there is no account and no photo, so this is
            // a placeholder that is honest about being one rather than a borrowed silhouette.
            var avatar = RuntimeUi.Element("Avatar", slot, out var avatarRect);
            avatarRect.anchorMin = new Vector2(0f, 0.5f);
            avatarRect.anchorMax = new Vector2(0f, 0.5f);
            avatarRect.anchoredPosition = new Vector2(pad + 55f, 0f);
            avatarRect.sizeDelta = new Vector2(110f, 110f);
            avatar.AddComponent<Image>().color = MenuTheme.Slot;

            _headline = RuntimeUi.Label("Name", slot,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(pad + 132f, -84f), new Vector2(-pad, -26f),
                44, TextAnchor.MiddleLeft, MenuTheme.Text);

            _subline = RuntimeUi.Label("Stats", slot,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(pad + 132f, -150f), new Vector2(-pad, -88f),
                30, TextAnchor.MiddleLeft, MenuTheme.Dim);
        }

        void BuildBoardCard(RectTransform slot)
        {
            RuntimeUi.Panel("Card", slot, MenuTheme.Card);
            float pad = MenuTheme.CardPadding;

            RuntimeUi.Label("Title", slot,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(pad, -66f), new Vector2(-pad, -14f),
                38, TextAnchor.MiddleLeft, MenuTheme.Text).text = "LEADERBOARD";

            _boardRows = BuildRows(slot, BoardRows, -76f);

            RuntimeUi.Label("Note", slot,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(pad, 12f), new Vector2(-pad, 56f),
                26, TextAnchor.MiddleLeft, MenuTheme.Faint).text = _leaderboard.IsLive
                ? string.Empty
                : "sample board — the shared one arrives with online scores";
        }

        void BuildRunsCard(RectTransform slot)
        {
            RuntimeUi.Panel("Card", slot, MenuTheme.Card);
            float pad = MenuTheme.CardPadding;

            RuntimeUi.Label("Title", slot,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(pad, -66f), new Vector2(-pad, -14f),
                38, TextAnchor.MiddleLeft, MenuTheme.Text).text = "MY LAST RUNS";

            _runRows = BuildRows(slot, RunRows, -76f);

            _runsEmpty = RuntimeUi.Label("Empty", slot,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(pad, -170f), new Vector2(-pad, -100f),
                30, TextAnchor.UpperLeft, MenuTheme.Faint);
            _runsEmpty.text = "no runs yet — the first one lands here";
        }

        void BuildLinksCard(RectTransform slot)
        {
            RuntimeUi.Panel("Card", slot, MenuTheme.Card);

            // "how to play" is the only way back to the first-run guide once it has been
            // dismissed; "privacy policy" is Play policy - it has to be reachable inside the app,
            // not only on the store listing. Both used to sit along the bottom of the old mode
            // picker; the profile tab is where a player looks for them now.
            // No version line here. It was on the first device build and it read as a duplicate:
            // the header carries it on every tab, so a second copy two thirds of the way down the
            // same screen only makes the reader wonder whether the two could disagree.
            BuildLink(slot, "Guide", -34f, "HOW TO PLAY", () => Menu.RequestGuide());
            BuildLink(slot, "Privacy", -102f, "PRIVACY POLICY",
                () => Application.OpenURL(GameLinks.PrivacyPolicyUrl));
        }

        void BuildLink(RectTransform slot, string name, float top, string label, Action onTap)
        {
            var text = RuntimeUi.Label(name, slot,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(MenuTheme.CardPadding, top - 52f),
                new Vector2(-MenuTheme.CardPadding, top),
                32, TextAnchor.MiddleLeft, MenuTheme.Dim);
            text.text = label;
            text.raycastTarget = true;

            var button = text.gameObject.AddComponent<Button>();
            button.targetGraphic = text;
            button.onClick.AddListener(() => onTap());
        }

        /// A column of single-line rows, built empty and filled by Refresh. Rows are text rather
        /// than three aligned columns on purpose: one string per row is one thing to get right,
        /// and the numbers here are short enough that a tab stop buys nothing.
        Text[] BuildRows(RectTransform slot, int count, float top)
        {
            var rows = new Text[count];
            for (int i = 0; i < count; i++)
            {
                rows[i] = RuntimeUi.Label("Row" + i, slot,
                    new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(MenuTheme.CardPadding, top - (i + 1) * RowHeight),
                    new Vector2(-MenuTheme.CardPadding, top - i * RowHeight),
                    30, TextAnchor.MiddleLeft, MenuTheme.Text);
            }
            return rows;
        }

        public override void OnShown()
        {
            var utcNow = DateTime.UtcNow;
            string todayLabel = DailySeed.LabelForDate(utcNow);
            int streak = DailyStreak.LengthOn(ProgressStore.Streak, DailyStreak.DayNumber(utcNow));

            // Camera and tilt keep separate boards (BestBoard, Feature D), so a profile that just
            // said "best 7787" would be quoting one of two boards without saying which - the exact
            // claim the result screen was changed to stop making. The tab shows the scheme the
            // player last ran with and labels it; switching mode and coming back switches the
            // board with it.
            var scheme = ModePickerCard.LastUsed;
            var board = new BestBoard(new PlayerPrefsScoreStore());

            // Migrate before any read. RunSession runs the same migration in Start(), but the
            // session is a disabled component the whole time the menu is up — it wakes when a mode
            // is picked — so on a cold launch after an upgrade this tab reads the per-scheme keys
            // first and would show an existing player's best as 0. Migrate is idempotent by design
            // (see BestBoard), so both call sites can keep it with no coordination between them.
            board.Migrate();

            int allTimeBest = board.AllTimeBest(scheme);
            int dailyBest = board.DailyBest(scheme, todayLabel);

            _headline.text = "RUNNER";
            _subline.text = SchemeName(scheme) + " best " + allTimeBest +
                            "   ·   today " + dailyBest +
                            "   ·   streak " + streak;

            // The board is ranked on the ALL-TIME best, not today's. Today's is the number the
            // real (daily) board will eventually use, but it is zero for most of every day, so
            // ranking on it put the player last on their own profile every morning - a worse lie
            // than the mocked names beside it. The subline above still shows both.
            FillBoard(allTimeBest);
            FillRuns();
        }

        static string SchemeName(ControlScheme scheme) =>
            scheme == ControlScheme.Camera ? "CAMERA" : "TILT";

        void FillBoard(int yourScore)
        {
            var entries = _leaderboard.Top(_boardRows.Length, yourScore);
            for (int i = 0; i < _boardRows.Length; i++)
            {
                if (i >= entries.Count)
                {
                    _boardRows[i].text = string.Empty;
                    continue;
                }

                var entry = entries[i];
                _boardRows[i].text = entry.Rank + ".   " + entry.Name + "        " + entry.Score;
                _boardRows[i].color = entry.IsYou ? MenuTheme.Accent : MenuTheme.Text;
            }
        }

        void FillRuns()
        {
            IReadOnlyList<RunRecord> runs = ProgressStore.History;
            _runsEmpty.gameObject.SetActive(runs.Count == 0);

            for (int i = 0; i < _runRows.Length; i++)
            {
                if (i >= runs.Count)
                {
                    _runRows[i].text = string.Empty;
                    continue;
                }

                var run = runs[i];
                _runRows[i].text = run.DayLabel + "   " + (run.Daily ? "daily" : "free") +
                                   "        " + run.Score + "   " + run.Distance + "m   " +
                                   run.Coins + "c";
                _runRows[i].color = i == 0 ? MenuTheme.Text : MenuTheme.Dim;
            }
        }
    }
}
