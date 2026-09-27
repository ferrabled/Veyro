using System;
using System.Collections.Generic;
using MotionRunner.Audio;
using MotionRunner.Core;
using MotionRunner.Progression;
using MotionRunner.Social;
using MotionRunner.Track;
using UnityEngine;
using UnityEngine.UI;

namespace MotionRunner.Menu
{
    /// The right tab: who the player is, where they stand, and what they have been running.
    ///
    /// All three cards can be real now. Best score, streak and the recent-runs list come off
    /// disk through ProgressStore; the leaderboard comes from the shared Supabase board (T-009)
    /// through LiveLeaderboard when the backend answers, and falls back to MockLeaderboard —
    /// still labelled as sample data — when it does not (no key, offline, first frames). The
    /// player card shows the server-generated handle once a profile exists, with unlimited
    /// rerolls, and the links card carries the account-deletion path Play policy requires.
    public sealed class ProfilePage : MenuPage
    {
        const float TopOffset = 12f;
        const float PlayerHeight = 180f;
        const float BoardHeight = 520f;
        const float RunsHeight = 430f;
        const float LinksHeight = 372f;

        const float RowHeight = 62f;
        const int BoardRows = 6;
        const int RunRows = 5;

        /// Vertical spacing of the links-card rows. Tighter than RowHeight so the six rows
        /// (guide, privacy, delete, player id, recovery code, import) fit the height budget.
        const float LinkSpacing = 56f;

        readonly ILeaderboardSource _mock = new MockLeaderboard();
        LiveLeaderboard _live;

        BoardScope _scope = BoardScope.Daily;
        int _allTimeBestShown;

        Text _headline;
        Text _reroll;
        Text _subline;
        Text[] _boardRows;
        Text[] _runRows;
        Text _runsEmpty;
        Text _boardNote;
        Button _scopeDaily;
        Button _scopeAllTime;
        Text _delete;
        Text _playerId;
        Text _recoveryCode;
        Text _import;
        bool _deleteArmed;

        protected override void Build()
        {
            RuntimeUi.Panel("Scrim", Root, MenuTheme.Scrim);

            _live = new LiveLeaderboard(Menu.Profile);
            _live.Changed += OnBoardChanged;
            if (Menu.Profile != null) Menu.Profile.ProfileChanged += OnProfileChanged;

            var stack = new MenuStack(Root, TopOffset);
            BuildPlayerCard(stack.Add("Player", PlayerHeight));
            BuildBoardCard(stack.Add("Board", BoardHeight));
            BuildRunsCard(stack.Add("Runs", RunsHeight));
            BuildLinksCard(stack.Add("Links", LinksHeight));
        }

        void OnDestroy()
        {
            if (_live != null) _live.Changed -= OnBoardChanged;
            if (Menu != null && Menu.Profile != null)
                Menu.Profile.ProfileChanged -= OnProfileChanged;
        }

        void BuildPlayerCard(RectTransform slot)
        {
            RuntimeUi.Panel("Card", slot, MenuTheme.Card);
            float pad = MenuTheme.CardPadding;

            // A square avatar in the accent colour: there is no photo, so this is a placeholder
            // that is honest about being one rather than a borrowed silhouette.
            var avatar = RuntimeUi.Element("Avatar", slot, out var avatarRect);
            avatarRect.anchorMin = new Vector2(0f, 0.5f);
            avatarRect.anchorMax = new Vector2(0f, 0.5f);
            avatarRect.anchoredPosition = new Vector2(pad + 55f, 0f);
            avatarRect.sizeDelta = new Vector2(110f, 110f);
            avatar.AddComponent<Image>().color = MenuTheme.Slot;

            _headline = RuntimeUi.Label("Name", slot,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(pad + 132f, -84f), new Vector2(-pad - 250f, -26f),
                44, TextAnchor.MiddleLeft, MenuTheme.Text);

            // The reroll is a right-aligned link on the name row: rerolling is a small act on
            // the name, not a card of its own. Hidden until a profile exists.
            _reroll = RuntimeUi.Label("Reroll", slot,
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-pad - 240f, -84f), new Vector2(-pad, -26f),
                26, TextAnchor.MiddleRight, MenuTheme.Dim);
            _reroll.raycastTarget = true;
            var rerollButton = _reroll.gameObject.AddComponent<Button>();
            rerollButton.targetGraphic = _reroll;
            rerollButton.onClick.AddListener(RerollHandle);
            RuntimeUi.TapSound(rerollButton);

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
                new Vector2(pad, -66f), new Vector2(-pad - 360f, -14f),
                38, TextAnchor.MiddleLeft, MenuTheme.Text).text = "LEADERBOARD";

            // The two scopes of the shared board (D15). The scheme half of the split (tilt vs
            // camera) follows the mode the player last ran with, same as the bests above.
            _scopeDaily = BuildScopeTab(slot, "ScopeDaily", -pad - 344f, -pad - 184f, "TODAY",
                () => SetScope(BoardScope.Daily));
            _scopeAllTime = BuildScopeTab(slot, "ScopeAll", -pad - 176f, -pad, "ALL-TIME",
                () => SetScope(BoardScope.AllTime));

            _boardRows = BuildRows(slot, BoardRows, -76f);

            // One bottom line, two honest states: the sample-data admission or the player's
            // live rank. (The TAP TO JOIN consent ask lived here until 17 Sep - joining is
            // automatic now, owner call; the privacy policy copy describes exactly that.)
            _boardNote = RuntimeUi.Label("Note", slot,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(pad, 12f), new Vector2(-pad, 56f),
                26, TextAnchor.MiddleLeft, MenuTheme.Faint);
        }

        Button BuildScopeTab(RectTransform slot, string name, float left, float right,
            string label, Action onTap)
        {
            var text = RuntimeUi.Label(name, slot,
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(left, -62f), new Vector2(right, -18f),
                26, TextAnchor.MiddleCenter, MenuTheme.Dim);
            text.text = label;
            text.raycastTarget = true;
            var button = text.gameObject.AddComponent<Button>();
            button.targetGraphic = text;
            button.onClick.AddListener(() => onTap());
            RuntimeUi.TapSound(button);
            return button;
        }

        void SetScope(BoardScope scope)
        {
            if (_scope == scope) return;
            _scope = scope;
            ShowBoard();
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
            // not only on the store listing. DELETE PROFILE is Play's account-deletion policy:
            // the moment a server-side profile exists, an in-app deletion path must too
            // (STORE_COMPLIANCE T-009). Two taps, because it is immediate and permanent.
            // Three across on the first row (T-045 added SOUND next to NOTIFICATIONS): each link
            // gets a third of the card and best-fits down from 32 if a width ever asks it to.
            var guide = BuildLink(slot, "Guide", -30f, "HOW TO PLAY", () => Menu.RequestGuide());
            guide.rectTransform.anchorMax = new Vector2(1f / 3f, 1f);
            var notifications = BuildLink(slot, "Notifications", -30f,
                "NOTIFICATIONS", () => Menu.RequestNotifications());
            notifications.rectTransform.anchorMin = new Vector2(1f / 3f, 1f);
            notifications.rectTransform.anchorMax = new Vector2(2f / 3f, 1f);
            notifications.alignment = TextAnchor.MiddleCenter;
            var sound = BuildLink(slot, "Sound", -30f, "SOUND", () => Menu.RequestSound());
            sound.rectTransform.anchorMin = new Vector2(2f / 3f, 1f);
            sound.alignment = TextAnchor.MiddleRight;
            ShrinkToFit(guide, 32);
            ShrinkToFit(notifications, 32);
            ShrinkToFit(sound, 32);
            var privacy = BuildLink(slot, "Privacy", -30f - LinkSpacing, "PRIVACY POLICY",
                () => Application.OpenURL(GameLinks.PrivacyPolicyUrl));
            privacy.rectTransform.anchorMax = new Vector2(.5f, 1f);
            var analytics = BuildLink(slot, "Analytics", -30f - LinkSpacing, "GAMEPLAY ANALYTICS",
                () => Menu.RequestAnalytics());
            analytics.rectTransform.anchorMin = new Vector2(.5f, 1f);
            ShrinkToFit(privacy, 28);
            ShrinkToFit(analytics, 28);
            _delete = BuildLink(slot, "Delete", -30f - 2 * LinkSpacing,
                "DELETE ONLINE PROFILE", DeleteTapped);
            ShrinkToFit(_delete, 32);

            // The support identifier (18 Sep review R3): deletion/support requests without the
            // app need something that locates the record. Tap copies the full id. It locates,
            // it does not authorize — ownership is proven with the recovery code below.
            _playerId = BuildLink(slot, "PlayerId", -30f - 3 * LinkSpacing, "PLAYER ID", PlayerIdTapped);
            _playerId.fontSize = 26;
            _playerId.color = MenuTheme.Faint;
            ShrinkToFit(_playerId, 26);

            // The account's private key, player-visible (owner call, 18 Sep): proves ownership
            // to support and re-imports the profile on another install. Anyone holding it can
            // claim the profile, so the row says exactly that.
            _recoveryCode = BuildLink(slot, "Recovery", -30f - 4 * LinkSpacing,
                "RECOVERY CODE", RecoveryCodeTapped);
            _recoveryCode.fontSize = 26;
            _recoveryCode.color = MenuTheme.Faint;
            ShrinkToFit(_recoveryCode, 26);

            // The manual rescue when Auto Backup did not carry the key (new device, backup
            // off): paste a recovery code copied on the old install.
            _import = BuildLink(slot, "Import", -30f - 5 * LinkSpacing,
                "IMPORT PROFILE (paste a recovery code)", ImportTapped);
            _import.fontSize = 26;
            _import.color = MenuTheme.Faint;
            ShrinkToFit(_import, 26);
        }

        /// The links card's lower rows swap their label for variable-length feedback ("copied",
        /// "import failed: …", the post-delete summary), and a long message used to run straight
        /// off the right edge of the screen. Best-fit with the DESIGNED size as the ceiling
        /// leaves every short label drawn exactly as before and shrinks only the lines that would
        /// not otherwise fit, on any screen width.
        ///
        /// The overflow modes are load-bearing, not tidying: RuntimeUi.Label ships
        /// Overflow/Overflow, which makes best-fit a NO-OP — an unbounded line always "fits", so
        /// Unity never shrinks anything and the text just spills past the card. Best-fit only
        /// means something once the rect actually bounds the text.
        static void ShrinkToFit(Text text, int designedSize)
        {
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 18;
            text.resizeTextMaxSize = designedSize;
        }

        Text BuildLink(RectTransform slot, string name, float top, string label, Action onTap)
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
            RuntimeUi.TapSound(button);
            return text;
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
            _deleteArmed = false;
            _delete.text = "DELETE ONLINE PROFILE";
            _delete.color = MenuTheme.Dim;
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
            _allTimeBestShown = allTimeBest;

            RefreshHeadline();
            _subline.text = SchemeName(scheme) + " best " + allTimeBest +
                            "   ·   today " + dailyBest +
                            "   ·   streak " + streak;

            ShowBoard();
            FillRuns();
        }

        void RefreshHeadline()
        {
            var profile = Menu.Profile?.Current;
            _headline.text = profile != null ? profile.Handle : "RUNNER";

            // Rerolls are unlimited (owner call, 17 Sep) - the link shows whenever a profile
            // exists and never counts down.
            _reroll.gameObject.SetActive(profile != null);
            _reroll.text = "new name";

            if (_playerId != null)
            {
                _playerId.gameObject.SetActive(profile != null);
                if (profile != null)
                    _playerId.text = "PLAYER ID  " + profile.UserId + "   (tap to copy)";
            }
            if (_recoveryCode != null)
            {
                bool hasCode = profile != null &&
                               !string.IsNullOrEmpty(Menu.Profile?.RecoveryCode);
                _recoveryCode.gameObject.SetActive(hasCode);
                if (hasCode)
                    _recoveryCode.text = "RECOVERY CODE — tap to copy. Restores this profile " +
                                         "anywhere; anyone with it can claim it";
            }
            if (_import != null)
                _import.gameObject.SetActive(Menu.Profile != null && Menu.Profile.IsReady);
        }

        void PlayerIdTapped()
        {
            var profile = Menu.Profile?.Current;
            if (profile == null) return;
            GUIUtility.systemCopyBuffer = profile.UserId;
            _playerId.text = "PLAYER ID copied to clipboard";
        }

        void RecoveryCodeTapped()
        {
            string code = Menu.Profile?.RecoveryCode;
            if (string.IsNullOrEmpty(code)) return;
            GUIUtility.systemCopyBuffer = code;
            _recoveryCode.text = "RECOVERY CODE copied — store it somewhere safe";
        }

        void ImportTapped()
        {
            var service = Menu.Profile;
            if (service == null || !service.IsReady) return;

            string pasted = (GUIUtility.systemCopyBuffer ?? string.Empty).Trim();
            _import.text = "importing…";
            service.ImportProfile(pasted, error =>
            {
                if (this == null || !gameObject.activeInHierarchy) return;
                if (error != null)
                {
                    GameAudio.Deny();
                    _import.text = "import failed: " + error.Message;
                    _import.color = MenuTheme.Dim;
                    return;
                }
                GameAudio.Confirm();
                _import.text = "profile imported";
                _import.color = MenuTheme.Dim;
                RefreshHeadline();
                ShowBoard();
            });
        }

        void OnProfileChanged()
        {
            if (this == null || !gameObject.activeInHierarchy) return;
            RefreshHeadline();
        }

        void RerollHandle()
        {
            _reroll.text = "…";
            Menu.Profile?.RerollHandle((profile, error) =>
            {
                if (this == null || !gameObject.activeInHierarchy) return;
                RefreshHeadline();
                // The board shows handles, so the old name on it is stale until the next fetch.
                _live.Refresh();
            });
        }

        // ---- board ----

        /// Points the live board at (scope of the toggle) × (group of the last-used scheme) ×
        /// today's content. The fetch is async; until rows land — or forever, with no backend —
        /// the mock fills the card, labelled as the sample data it is.
        void ShowBoard()
        {
            var scheme = ModePickerCard.LastUsed;
            string group = scheme == ControlScheme.Camera
                ? InputModes.GroupCamera
                : InputModes.GroupStandard;

            StyleScope(_scopeDaily, _scope == BoardScope.Daily);
            StyleScope(_scopeAllTime, _scope == BoardScope.AllTime);

            _live.Show(new BoardQuery(_scope, DailySeed.LabelForDate(DateTime.UtcNow),
                ChunkLibrary.ContentVersion, RunSeed.DefaultWorldId, group, BoardRows));

            FillBoard();
        }

        static void StyleScope(Button tab, bool selected)
        {
            if (tab == null) return;
            var text = tab.targetGraphic as Text;
            if (text != null) text.color = selected ? MenuTheme.Accent : MenuTheme.Dim;
        }

        void OnBoardChanged()
        {
            if (this == null || !gameObject.activeInHierarchy) return;
            FillBoard();
        }

        void FillBoard()
        {
            ILeaderboardSource source = _live.IsLive ? _live : _mock;
            var entries = source.Top(_boardRows.Length, _allTimeBestShown);

            for (int i = 0; i < _boardRows.Length; i++)
            {
                if (i >= entries.Count)
                {
                    _boardRows[i].text = i == 0 && _live.IsLive ? "nobody yet — set the first score" : string.Empty;
                    _boardRows[i].color = MenuTheme.Faint;
                    continue;
                }

                var entry = entries[i];
                _boardRows[i].text = entry.Rank + ".   " + entry.Name + "        " + entry.Score;
                _boardRows[i].color = entry.IsYou ? MenuTheme.Accent : MenuTheme.Text;
            }

            RefreshBoardNote();
        }

        /// The bottom line of the board card: no live board → say the rows are samples;
        /// live → the player's rank, or a nudge to finish a run.
        void RefreshBoardNote()
        {
            if (!_live.IsLive)
            {
                _boardNote.text = _live.IsLoading
                    ? "sample board — loading online scores…"
                    : "sample board — online scores unavailable";
                _boardNote.color = MenuTheme.Faint;
                return;
            }

            _boardNote.text = _live.MyRank > 0
                ? "you are #" + _live.MyRank + " on this board"
                : "finish a run to land on this board";
            _boardNote.color = MenuTheme.Faint;
        }

        // ---- deletion ----

        void DeleteTapped()
        {
            var service = Menu.Profile;
            if (service == null || !service.IsReady)
            {
                GameAudio.Deny();
                _delete.text = "no online profile to delete";
                _delete.color = MenuTheme.Faint;
                return;
            }

            if (!_deleteArmed)
            {
                _deleteArmed = true;
                _delete.text = "TAP AGAIN TO DELETE — immediate and permanent";
                _delete.color = MenuTheme.Accent;
                return;
            }

            _deleteArmed = false;
            _delete.text = "deleting…";
            _delete.color = MenuTheme.Faint;
            service.DeleteAccount(error =>
            {
                if (this == null || !gameObject.activeInHierarchy) return;
                if (error != null)
                {
                    GameAudio.Deny();
                    _delete.text = "couldn't delete — try again (" + error.Code + ")";
                    _delete.color = MenuTheme.Dim;
                    return;
                }
                // Deliberately NO board refresh here: the service is dormant after deletion
                // and a fetch must not mint a replacement account mid-flow (18 Sep review R2).
                // Scope is stated honestly: local device stats are device data and stay.
                _delete.text = "online profile deleted — device stats stay; a fresh profile starts next launch";
                _delete.color = MenuTheme.Dim;
                RefreshHeadline();
                _boardNote.text = "profile deleted — the board returns next launch";
                _boardNote.color = MenuTheme.Faint;
            });
        }

        static string SchemeName(ControlScheme scheme) =>
            scheme == ControlScheme.Camera ? "CAMERA" : "TILT";

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
