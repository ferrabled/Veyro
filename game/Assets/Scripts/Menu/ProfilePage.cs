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
    /// The right tab: who the player is, where they stand, what they have been running, and
    /// then the settings and the account.
    ///
    /// All the cards can be real now. Best score, streak and the recent-runs list come off
    /// disk through ProgressStore; the leaderboard comes from the shared Supabase board (T-009)
    /// through LiveLeaderboard when the backend answers; until then (first frames, offline, no
    /// key) the card shows empty slots under a "loading" / "no connection" panel. The
    /// player card shows the server-generated handle once a profile exists, with unlimited
    /// rerolls and a critter drawn from the handle (PlayerAvatar), and the account card carries
    /// the account-deletion path Play policy requires.
    ///
    /// The page scrolls. Five cards do not fit a 16:9 screen, and the alternative - squeezing
    /// settings into a grid of bare text links under the runs - is what this layout replaced.
    /// The player card is at the top, so the numbers the tab is for are what opens.
    public sealed class ProfilePage : MenuPage
    {
        const float TopOffset = 12f;
        const float PlayerHeight = 180f;

        /// Under the last card, so it can be scrolled clear of the tab bar's edge.
        const float BottomPadding = 28f;

        const float AvatarSize = 116f;

        const string DeleteLabel = "DELETE ONLINE PROFILE";
        const string DeleteHint = "two taps · immediate and permanent";

        LiveLeaderboard _live;

        BoardScope _scope = BoardScope.Daily;
        int _allTimeBestShown;

        ScrollRect _scroll;
        RectTransform _content;

        PlayerAvatarView _avatar;
        Text _headline;
        Text _reroll;
        Text _subline;
        LeaderboardCard _board;
        RecentRunsCard _runs;
        Text _boardNote;
        Button _scopeDaily;
        Button _scopeAllTime;

        ProfileSettingsCard _settings;

        RectTransform _accountSlot;
        RectTransform _playerIdRow;
        RectTransform _recoveryRow;
        RectTransform _importRow;
        RectTransform _privacyRow;
        RectTransform _deleteRow;
        Text _playerIdSub;
        Text _recoverySub;
        Text _importSub;
        Text _deleteLabel;
        Text _deleteSub;
        bool _deleteArmed;

        protected override void Build()
        {
            _live = new LiveLeaderboard(Menu.Profile);
            _live.Changed += OnBoardChanged;
            if (Menu.Profile != null) Menu.Profile.ProfileChanged += OnProfileChanged;

            _content = BuildScroll();
            var stack = new MenuStack(_content, TopOffset);
            BuildPlayerCard(stack.Add("Player", PlayerHeight));
            BuildBoardCard(stack.Add("Board", LeaderboardCard.Height));
            _runs = new RecentRunsCard();
            _runs.Build(stack.Add("Runs", RecentRunsCard.Height));

            _settings = new ProfileSettingsCard(
                () => GameAudio.Instance != null ? GameAudio.Instance.Settings : null,
                () => Menu.RequestNotifications(), () => Menu.RequestAnalytics(),
                () => Menu.RequestGuide());
            _settings.Build(stack.Add("Settings", ProfileSettingsCard.Height));

            // Placed at its title's height; LayoutAccount sizes it to the rows that are showing
            // and sizes the scroll content with it (it is the last card).
            _accountSlot = stack.Add("Account", MenuRows.TitleHeight);
            BuildAccountCard(_accountSlot);
            LayoutAccount();
        }

        void OnDestroy()
        {
            if (_live != null) _live.Changed -= OnBoardChanged;
            if (Menu != null && Menu.Profile != null)
                Menu.Profile.ProfileChanged -= OnProfileChanged;
            _settings?.Dispose();
            _avatar?.Dispose();
            _board?.Dispose();
        }

        /// The page's scroll view. The viewport paints the scrim itself, so a drag that starts in
        /// a gap between two cards still has a graphic under it to scroll by.
        RectTransform BuildScroll()
        {
            var viewport = RuntimeUi.Element("Scroll", Root, out var viewRect);
            RuntimeUi.Stretch(viewRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            viewport.AddComponent<Image>().color = MenuTheme.Scrim;
            viewport.AddComponent<RectMask2D>();

            _scroll = viewport.AddComponent<ScrollRect>();
            _scroll.viewport = viewRect;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Elastic;
            _scroll.elasticity = 0.08f;
            _scroll.scrollSensitivity = 45f;

            RuntimeUi.Element("Content", viewRect, out var content);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            _scroll.content = content;
            return content;
        }

        void BuildPlayerCard(RectTransform slot)
        {
            CosmeticUi.Card(slot);
            float pad = MenuTheme.CardPadding;

            // The avatar is generated from the handle (PlayerAvatar): a critter on a colour tile,
            // so a player without a photo still has a face, and a reroll changes it.
            RuntimeUi.Element("Avatar", slot, out var avatarRect);
            avatarRect.anchorMin = new Vector2(0f, 0.5f);
            avatarRect.anchorMax = new Vector2(0f, 0.5f);
            avatarRect.anchoredPosition = new Vector2(pad + AvatarSize * 0.5f, 0f);
            avatarRect.sizeDelta = new Vector2(AvatarSize, AvatarSize);
            _avatar = new PlayerAvatarView(avatarRect, 28f);

            _headline = RuntimeUi.Label("Name", slot,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(pad + AvatarSize + 24f, -84f), new Vector2(-pad - 220f, -26f),
                44, TextAnchor.MiddleLeft, MenuTheme.Text);

            // The reroll is a right-aligned link on the name row: rerolling is a small act on
            // the name, not a card of its own. Hidden until a profile exists.
            _reroll = RuntimeUi.Label("Reroll", slot,
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-pad - 210f, -84f), new Vector2(-pad, -26f),
                26, TextAnchor.MiddleRight, MenuTheme.Dim);
            _reroll.raycastTarget = true;
            var rerollButton = _reroll.gameObject.AddComponent<Button>();
            rerollButton.targetGraphic = _reroll;
            rerollButton.onClick.AddListener(RerollHandle);
            RuntimeUi.TapSound(rerollButton);

            _subline = RuntimeUi.Label("Stats", slot,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(pad + AvatarSize + 24f, -150f), new Vector2(-pad, -88f),
                30, TextAnchor.MiddleLeft, MenuTheme.Dim);
            MenuRows.Fit(_subline, 30);
        }

        /// The podium and the list are LeaderboardCard's; the scope tabs and the note's wording
        /// stay here with the board logic.
        void BuildBoardCard(RectTransform slot)
        {
            float pad = MenuTheme.CardPadding;
            _board = new LeaderboardCard();
            _board.Build(slot);
            _board.Retry = () => _live.Refresh();

            // The two scopes of the shared board (D15). The scheme half of the split (tilt vs
            // camera) follows the mode the player last ran with, same as the bests above.
            _scopeDaily = BuildScopeTab(slot, "ScopeDaily", -pad - 344f, -pad - 184f, "TODAY",
                () => SetScope(BoardScope.Daily));
            _scopeAllTime = BuildScopeTab(slot, "ScopeAll", -pad - 176f, -pad, "ALL-TIME",
                () => SetScope(BoardScope.AllTime));

            // One bottom line under a live board: the player's rank. (The TAP TO JOIN consent ask
            // lived here until 17 Sep - joining is automatic now, owner call; the privacy policy
            // copy describes exactly that.)
            _boardNote = _board.Note;
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

        /// Who the player is to the backend, and the way out. Each row keeps a quiet second line
        /// that doubles as the feedback slot ("copied", "import failed: …", the post-delete
        /// summary) - MenuRows.Fit shrinks a long message rather than letting it leave the card.
        void BuildAccountCard(RectTransform slot)
        {
            CosmeticUi.Card(slot);
            MenuRows.Title(slot, "ACCOUNT");

            // The support identifier (18 Sep review R3): deletion/support requests without the
            // app need something that locates the record. Tap copies the full id. It locates,
            // it does not authorize — ownership is proven with the recovery code below.
            _playerIdRow = PillRow(slot, "PlayerId", "PLAYER ID", "COPY", PlayerIdTapped, out _playerIdSub);

            // The account's private key, player-visible (owner call, 18 Sep): proves ownership
            // to support and re-imports the profile on another install. Anyone holding it can
            // claim the profile, so the row says exactly that.
            _recoveryRow = PillRow(slot, "Recovery", "RECOVERY CODE", "COPY", RecoveryCodeTapped, out _recoverySub);

            // The manual rescue when Auto Backup did not carry the key (new device, backup
            // off): paste a recovery code copied on the old install.
            _importRow = PillRow(slot, "Import", "IMPORT PROFILE", "PASTE", ImportTapped, out _importSub);

            // Play policy: the privacy policy has to be reachable inside the app, not only on
            // the store listing.
            _privacyRow = MenuRows.Row(slot, "Privacy");
            MenuRows.Tap(_privacyRow, () => Application.OpenURL(GameLinks.PrivacyPolicyUrl));
            MenuRows.Label(_privacyRow, "PRIVACY POLICY", MenuRows.ChevronReserve, false);
            MenuRows.Chevron(_privacyRow);

            // Play's account-deletion policy: the moment a server-side profile exists, an in-app
            // deletion path must too (STORE_COMPLIANCE T-009). Two taps, because it is immediate
            // and permanent - and the second line says so before the first tap, not after.
            _deleteRow = MenuRows.Row(slot, "Delete");
            MenuRows.Tap(_deleteRow, DeleteTapped);
            _deleteLabel = MenuRows.Label(_deleteRow, DeleteLabel, MenuTheme.CardPadding, true);
            _deleteSub = MenuRows.Sub(_deleteRow, MenuTheme.CardPadding);
            ResetDelete();
        }

        static RectTransform PillRow(RectTransform slot, string name, string label, string pill,
            Action onTap, out Text sub)
        {
            var row = MenuRows.Row(slot, name);
            MenuRows.Tap(row, onTap);
            MenuRows.Label(row, label, MenuRows.PillReserve, true);
            sub = MenuRows.Sub(row, MenuRows.PillReserve);
            MenuRows.Pill(row, pill);
            return row;
        }

        /// Stacks the account rows that are showing, sizes the card to them and, since it is the
        /// last card, the scroll content to it. Rows hide rather than grey out when they have
        /// nothing to act on (no profile, no recovery code), as the old links did.
        void LayoutAccount()
        {
            if (_accountSlot == null) return;
            float y = MenuRows.TitleHeight;
            bool first = true;
            y = Flow(_playerIdRow, y, MenuRows.TallHeight, ref first);
            y = Flow(_recoveryRow, y, MenuRows.TallHeight, ref first);
            y = Flow(_importRow, y, MenuRows.TallHeight, ref first);
            y = Flow(_privacyRow, y, MenuRows.Height, ref first);
            y = Flow(_deleteRow, y, MenuRows.TallHeight, ref first);

            float height = y + MenuRows.BottomPadding;
            float top = _accountSlot.offsetMax.y;
            _accountSlot.offsetMin = new Vector2(_accountSlot.offsetMin.x, top - height);
            _content.sizeDelta = new Vector2(0f, -top + height + BottomPadding);
        }

        static float Flow(RectTransform row, float y, float height, ref bool first)
        {
            if (!row.gameObject.activeSelf) return y;
            y = MenuRows.Place(row, y, height, first);
            first = false;
            return y;
        }


        public override void OnShown()
        {
            ResetDelete();

            // Every visit opens at the top - the player card is what the tab is for - and a tap
            // on the PROFILE tab while already on it is how a player gets back up there.
            _scroll.StopMovement();
            _scroll.verticalNormalizedPosition = 1f;
            _content.anchoredPosition = Vector2.zero;

            _settings.Refresh();

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
            _runs.Show(ProgressStore.History);
        }

        void RefreshHeadline()
        {
            var profile = Menu.Profile?.Current;
            _headline.text = profile != null ? profile.Handle : "RUNNER";
            _avatar.Show(_headline.text);

            // Rerolls are unlimited (owner call, 17 Sep) - the link shows whenever a profile
            // exists and never counts down.
            _reroll.gameObject.SetActive(profile != null);
            _reroll.text = "re-roll name";

            _playerIdRow.gameObject.SetActive(profile != null);
            SetSub(_playerIdSub, profile != null ? profile.UserId : string.Empty, false);

            bool hasCode = profile != null && !string.IsNullOrEmpty(Menu.Profile?.RecoveryCode);
            _recoveryRow.gameObject.SetActive(hasCode);
            SetSub(_recoverySub, "restores this profile anywhere · anyone with it can claim it", false);

            _importRow.gameObject.SetActive(Menu.Profile != null && Menu.Profile.IsReady);
            SetSub(_importSub, "paste a recovery code copied on another install", false);

            LayoutAccount();
        }

        /// A row's second line: its standing description in Faint, or feedback on what the tap
        /// just did in Dim, one step louder.
        static void SetSub(Text sub, string text, bool feedback)
        {
            sub.text = text;
            sub.color = feedback ? MenuTheme.Dim : MenuTheme.Faint;
        }

        void PlayerIdTapped()
        {
            var profile = Menu.Profile?.Current;
            if (profile == null) return;
            GUIUtility.systemCopyBuffer = profile.UserId;
            SetSub(_playerIdSub, "copied to clipboard", true);
        }

        void RecoveryCodeTapped()
        {
            string code = Menu.Profile?.RecoveryCode;
            if (string.IsNullOrEmpty(code)) return;
            GUIUtility.systemCopyBuffer = code;
            SetSub(_recoverySub, "copied — store it somewhere safe", true);
        }

        void ImportTapped()
        {
            var service = Menu.Profile;
            if (service == null || !service.IsReady) return;

            string pasted = (GUIUtility.systemCopyBuffer ?? string.Empty).Trim();
            SetSub(_importSub, "importing…", true);
            service.ImportProfile(pasted, error =>
            {
                if (this == null || !gameObject.activeInHierarchy) return;
                if (error != null)
                {
                    GameAudio.Deny();
                    SetSub(_importSub, "import failed: " + error.Message, true);
                    return;
                }
                GameAudio.Confirm();
                RefreshHeadline(); // before the message: a refresh restores the standing line
                SetSub(_importSub, "profile imported", true);
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
        /// the card shows its empty slots under the loading / no-connection panel.
        void ShowBoard()
        {
            var scheme = ModePickerCard.LastUsed;
            string group = scheme == ControlScheme.Camera
                ? InputModes.GroupCamera
                : InputModes.GroupStandard;

            StyleScope(_scopeDaily, _scope == BoardScope.Daily);
            StyleScope(_scopeAllTime, _scope == BoardScope.AllTime);

            _live.Show(new BoardQuery(_scope, DailySeed.LabelForDate(DateTime.UtcNow),
                ChunkLibrary.ContentVersion, RunSeed.DefaultWorldId, group, LeaderboardCard.Rows));

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

        /// Real rows, or nothing: while the board is loading or unreachable the card shows its
        /// slots empty with a "loading" / "no connection" panel over them (owner call, 28 Sep).
        /// It used to fill them with MockLeaderboard's sample players, labelled as samples, and
        /// the jump from made-up names to the real board when the fetch landed read as a glitch.
        void FillBoard()
        {
            if (!_live.IsLive)
            {
                _board.ShowUnavailable(_live.IsLoading);
                _boardNote.text = string.Empty;
                return;
            }

            var entries = _live.Top(LeaderboardCard.Rows, _allTimeBestShown);

            // The player's own critter on their row: the same face as the player card above.
            _board.Show(entries, _headline.text);
            RefreshBoardNote(entries.Count);
        }

        /// The bottom line of a live board: the player's rank, or a nudge to finish a run.
        void RefreshBoardNote(int rows)
        {
            _boardNote.text = rows == 0 ? "nobody yet — set the first score"
                : _live.MyRank > 0 ? "you are #" + _live.MyRank + " on this board"
                : "finish a run to land on this board";
            _boardNote.color = MenuTheme.Faint;
        }

        // ---- deletion ----

        void ResetDelete()
        {
            _deleteArmed = false;
            _deleteLabel.text = DeleteLabel;
            _deleteLabel.color = MenuTheme.Text;
            SetSub(_deleteSub, DeleteHint, false);
        }

        void DeleteTapped()
        {
            var service = Menu.Profile;
            if (service == null || !service.IsReady)
            {
                GameAudio.Deny();
                SetSub(_deleteSub, "no online profile to delete", true);
                return;
            }

            if (!_deleteArmed)
            {
                _deleteArmed = true;
                _deleteLabel.text = "TAP AGAIN TO DELETE";
                _deleteLabel.color = MenuTheme.Accent;
                SetSub(_deleteSub, "immediate and permanent", true);
                return;
            }

            _deleteArmed = false;
            _deleteLabel.text = DeleteLabel;
            _deleteLabel.color = MenuTheme.Text;
            SetSub(_deleteSub, "deleting…", true);
            service.DeleteAccount(error =>
            {
                if (this == null || !gameObject.activeInHierarchy) return;
                if (error != null)
                {
                    GameAudio.Deny();
                    SetSub(_deleteSub, "couldn't delete — try again (" + error.Code + ")", true);
                    return;
                }
                // Deliberately NO board refresh here: the service is dormant after deletion
                // and a fetch must not mint a replacement account mid-flow (18 Sep review R2).
                // Scope is stated honestly: local device stats are device data and stay.
                RefreshHeadline();
                SetSub(_deleteSub, "online profile deleted — device stats stay; a fresh profile starts next launch", true);
                _boardNote.text = "profile deleted — the board returns next launch";
                _boardNote.color = MenuTheme.Faint;
            });
        }

        static string SchemeName(ControlScheme scheme) =>
            scheme == ControlScheme.Camera ? "CAMERA" : "TILT";
    }
}
