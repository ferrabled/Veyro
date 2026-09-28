using System.Collections.Generic;
using MotionRunner.Core;
using MotionRunner.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace MotionRunner.Menu
{
    /// The profile tab's leaderboard, drawn: the top three on a podium - second, first, third,
    /// each on a gold / silver / bronze block with their critter (PlayerAvatar) and score - and the
    /// next five as a ranked list with the scores in a right-aligned column. The player's own row
    /// is ringed in pink wherever it lands, podium or list.
    ///
    /// Only the drawing lives here. Which board, the live/sample split and the note under it stay
    /// with ProfilePage, which also puts the TODAY / ALL-TIME tabs on the title band.
    ///
    /// A board with fewer rows than places shows the empty places as unclaimed - a blank tile on a
    /// grey block, a dash in the list - so a fresh day's board reads as "open", not as broken.
    public sealed class LeaderboardCard
    {
        /// Rows the card asks the board for: the podium's three and the list's five.
        public const int Rows = Podium.Places + ListRows;

        public const float Height = ListTop + ListRows * RowHeight + 74f;

        const int ListRows = 5;

        /// The podium band: first place's block, score, name and critter stack to 290 px, so
        /// the band is that plus a little air under the title.
        const float PodiumTop = 84f;
        const float PodiumHeight = 304f;
        const float TallestBlock = 118f;
        const float PodiumAvatar = 88f;

        const float ListTop = PodiumTop + PodiumHeight + 18f;
        const float RowHeight = 66f;
        const float RowAvatar = 44f;
        const float ScoreWidth = 170f;

        readonly Place[] _places = new Place[Podium.Places];
        readonly ListRow[] _rows = new ListRow[ListRows];

        GameObject _status;
        GameObject _slash;
        Text _statusTitle;
        Text _statusDetail;

        /// The line under the board. ProfilePage writes it: the player's live rank, or nothing
        /// while the board is not there.
        public Text Note { get; private set; }

        /// A tap on the "no connection" panel: ProfilePage refetches.
        public System.Action Retry;

        public void Build(RectTransform slot)
        {
            CosmeticUi.Card(slot);
            float pad = MenuTheme.CardPadding;

            RuntimeUi.Label("Title", slot,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(pad, -66f), new Vector2(-pad - 360f, -14f),
                38, TextAnchor.MiddleLeft, MenuTheme.Text).text = "LEADERBOARD";

            RuntimeUi.Element("Podium", slot, out var podium);
            RuntimeUi.Stretch(podium, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(pad, -PodiumTop - PodiumHeight), new Vector2(-pad, -PodiumTop));
            for (int column = 0; column < Podium.Places; column++)
            {
                int place = Podium.DisplayOrder[column];
                _places[place] = new Place(podium, column, place);
            }

            for (int i = 0; i < ListRows; i++)
                _rows[i] = new ListRow(slot, ListTop + i * RowHeight);

            Note = RuntimeUi.Label("Note", slot,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(pad, 12f), new Vector2(-pad, 56f),
                26, TextAnchor.MiddleLeft, MenuTheme.Faint);

            BuildStatus(slot);
        }

        /// The panel over an absent board: signal bars (crossed out when there is no
        /// connection), a line saying which, and - offline - a tap to try again. It sits over
        /// the podium and list drawn as empty grey spots, so the card keeps its shape and real
        /// rows fill the same slots when they arrive instead of replacing a made-up board.
        void BuildStatus(RectTransform slot)
        {
            RuntimeUi.Element("Status", slot, out var rect);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            // Centred on the podium-and-list block.
            rect.anchoredPosition = new Vector2(0f, -(PodiumTop + ListTop + ListRows * RowHeight) * 0.5f);
            rect.sizeDelta = new Vector2(640f, 250f);
            var surface = CosmeticUi.Surface(rect, MenuTheme.ItemCard, 32f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = surface;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => { if (_slash != null && _slash.activeSelf) Retry?.Invoke(); });
            RuntimeUi.TapSound(button);

            RuntimeUi.Element("Icon", rect, out var icon);
            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 1f);
            icon.anchoredPosition = new Vector2(0f, -62f);
            icon.sizeDelta = new Vector2(64f, 64f);
            _slash = MenuIcons.Signal(icon, MenuTheme.Dim, true);

            _statusTitle = RuntimeUi.Label("Title", rect, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24f, -160f), new Vector2(-24f, -108f), 34, TextAnchor.MiddleCenter, MenuTheme.Text);
            _statusTitle.fontStyle = FontStyle.Bold;
            _statusDetail = RuntimeUi.Label("Detail", rect, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24f, -232f), new Vector2(-24f, -162f), 24, TextAnchor.UpperCenter, MenuTheme.Dim);
            MenuRows.Fit(_statusDetail, 24);

            _status = rect.gameObject;
            _status.SetActive(false);
        }

        /// No board to show - still loading, or no connection. Every place and row goes to its
        /// empty grey spot and the panel says which it is.
        public void ShowUnavailable(bool loading)
        {
            for (int i = 0; i < Podium.Places; i++) _places[i].Show(null, null);
            for (int i = 0; i < ListRows; i++) _rows[i].ShowEmpty(Podium.Places + i + 1);

            _status.SetActive(true);
            _slash.SetActive(!loading);
            _statusTitle.text = loading ? "Loading scores…" : "No connection";
            _statusDetail.text = loading
                ? "the leaderboard appears in a moment"
                : "Connect to the internet to see the leaderboard\ntap to try again";
        }

        /// Fills the card from a board, best first. `yourHandle` draws the player's own critter on
        /// their row, the same face as the player card whatever name the row carries.
        public void Show(IReadOnlyList<LeaderboardEntry> entries, string yourHandle)
        {
            if (_status != null) _status.SetActive(false);
            var podium = Podium.Split(entries, out var rest);
            for (int i = 0; i < Podium.Places; i++)
                _places[i].Show(podium[i], yourHandle);
            for (int i = 0; i < ListRows; i++)
            {
                if (i < rest.Count) _rows[i].Show(rest[i], yourHandle);
                else _rows[i].ShowEmpty(Podium.Places + i + 1);
            }
        }

        public void Dispose()
        {
            foreach (var place in _places) place?.Avatar.Dispose();
            foreach (var row in _rows) row?.Avatar.Dispose();
        }

        static readonly Color[] BlockColors = { MenuTheme.Gold, MenuTheme.Silver, MenuTheme.Bronze };

        /// The player's own row, tinted from the accent rather than a new colour.
        static readonly Color YouTint = new Color(MenuTheme.Accent.r, MenuTheme.Accent.g, MenuTheme.Accent.b, 0.12f);

        /// One podium place, built bottom-up from the baseline: block, score, name, critter.
        sealed class Place
        {
            public readonly PlayerAvatarView Avatar;

            readonly int _place;
            readonly CosmeticPanel _block;
            readonly Text _numeral;
            readonly Text _name;
            readonly Text _score;
            readonly GameObject _ring;

            public Place(RectTransform podium, int column, int place)
            {
                _place = place;
                RuntimeUi.Element("Place" + (place + 1), podium, out var rect);
                RuntimeUi.Stretch(rect, new Vector2(column / 3f, 0f), new Vector2((column + 1) / 3f, 1f),
                    new Vector2(8f, 0f), new Vector2(-8f, 0f));

                float block = TallestBlock * Podium.BlockHeights[place];
                RuntimeUi.Element("Block", rect, out var blockRect);
                RuntimeUi.Stretch(blockRect, Vector2.zero, new Vector2(1f, 0f), new Vector2(6f, 0f),
                    new Vector2(-6f, block));
                _block = CosmeticUi.Surface(blockRect, BlockColors[place], 16f);
                _block.raycastTarget = false;
                _numeral = RuntimeUi.Label("Numeral", blockRect, Vector2.zero, Vector2.one,
                    Vector2.zero, Vector2.zero, place == 0 ? 52 : 44, TextAnchor.MiddleCenter, MenuTheme.OnAccent);
                _numeral.fontStyle = FontStyle.Bold;
                _numeral.text = (place + 1).ToString();

                _score = Band(rect, "Score", block + 6f, 38f, 30, MenuTheme.Text);
                _score.fontStyle = FontStyle.Bold;
                _name = Band(rect, "Name", block + 44f, 32f, 24, MenuTheme.Dim);
                MenuRows.Fit(_name, 24);

                float avatarBottom = block + 84f;
                var ring = RuntimeUi.Element("Ring", rect, out var ringRect);
                Centre(ringRect, avatarBottom - 5f, PodiumAvatar + 10f);
                CosmeticUi.Surface(ringRect, MenuTheme.Accent, 29f).raycastTarget = false;
                _ring = ring;

                RuntimeUi.Element("Avatar", rect, out var avatarRect);
                Centre(avatarRect, avatarBottom, PodiumAvatar);
                Avatar = new PlayerAvatarView(avatarRect, 24f);
            }

            public void Show(LeaderboardEntry? held, string yourHandle)
            {
                if (!held.HasValue)
                {
                    _block.color = MenuTheme.Empty;
                    _numeral.color = MenuTheme.Faint;
                    _name.text = "—";
                    _name.color = MenuTheme.Faint;
                    _score.text = string.Empty;
                    _ring.SetActive(false);
                    Avatar.ShowEmpty();
                    return;
                }

                var entry = held.Value;
                _block.color = BlockColors[_place];
                _numeral.color = MenuTheme.OnAccent;
                _numeral.text = entry.Rank.ToString();
                _name.text = entry.Name;
                _name.color = entry.IsYou ? MenuTheme.Accent : MenuTheme.Dim;
                _score.text = entry.Score.ToString();
                _ring.SetActive(entry.IsYou);
                Avatar.Show(entry.IsYou ? yourHandle : entry.Name);
            }

            static Text Band(RectTransform rect, string name, float bottom, float height, int size, Color color) =>
                RuntimeUi.Label(name, rect, Vector2.zero, new Vector2(1f, 0f),
                    new Vector2(0f, bottom), new Vector2(0f, bottom + height),
                    size, TextAnchor.MiddleCenter, color);

            static void Centre(RectTransform rect, float bottom, float size)
            {
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(0f, bottom);
                rect.sizeDelta = new Vector2(size, size);
            }
        }

        /// One list row: rank, critter, name, and the score in its own right-aligned column, so
        /// the scores line up however long the names are.
        sealed class ListRow
        {
            public readonly PlayerAvatarView Avatar;

            readonly GameObject _highlight;
            readonly Text _rank;
            readonly Text _name;
            readonly Text _score;

            public ListRow(RectTransform slot, float top)
            {
                float pad = MenuTheme.CardPadding;
                RuntimeUi.Element("Row", slot, out var rect);
                RuntimeUi.Stretch(rect, new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(pad - 10f, -top - RowHeight + 6f), new Vector2(-pad + 10f, -top));

                var highlight = RuntimeUi.Element("You", rect, out var highlightRect);
                RuntimeUi.Stretch(highlightRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                CosmeticUi.Surface(highlightRect, YouTint, 20f).raycastTarget = false;
                _highlight = highlight;

                _rank = RuntimeUi.Label("Rank", rect, Vector2.zero, new Vector2(0f, 1f),
                    new Vector2(10f, 0f), new Vector2(76f, 0f), 28, TextAnchor.MiddleLeft, MenuTheme.Dim);

                RuntimeUi.Element("Avatar", rect, out var avatarRect);
                avatarRect.anchorMin = avatarRect.anchorMax = new Vector2(0f, 0.5f);
                avatarRect.pivot = new Vector2(0f, 0.5f);
                avatarRect.anchoredPosition = new Vector2(84f, 0f);
                avatarRect.sizeDelta = new Vector2(RowAvatar, RowAvatar);
                Avatar = new PlayerAvatarView(avatarRect, 12f);

                _name = RuntimeUi.Label("Name", rect, Vector2.zero, Vector2.one,
                    new Vector2(84f + RowAvatar + 18f, 0f), new Vector2(-ScoreWidth - 14f, 0f),
                    28, TextAnchor.MiddleLeft, MenuTheme.Text);
                MenuRows.Fit(_name, 28);

                _score = RuntimeUi.Label("Score", rect, new Vector2(1f, 0f), Vector2.one,
                    new Vector2(-ScoreWidth - 14f, 0f), new Vector2(-14f, 0f),
                    28, TextAnchor.MiddleRight, MenuTheme.Text);
                _score.fontStyle = FontStyle.Bold;
            }

            public void Show(LeaderboardEntry entry, string yourHandle)
            {
                _highlight.SetActive(entry.IsYou);
                _rank.text = entry.Rank.ToString();
                _rank.color = entry.IsYou ? MenuTheme.Accent : MenuTheme.Dim;
                _name.text = entry.Name;
                _name.color = entry.IsYou ? MenuTheme.Accent : MenuTheme.Text;
                _score.text = entry.Score.ToString();
                _score.color = entry.IsYou ? MenuTheme.Accent : MenuTheme.Text;
                Avatar.Show(entry.IsYou ? yourHandle : entry.Name);
            }

            public void ShowEmpty(int rank)
            {
                _highlight.SetActive(false);
                _rank.text = rank.ToString();
                _rank.color = MenuTheme.Faint;
                _name.text = "—";
                _name.color = MenuTheme.Faint;
                _score.text = string.Empty;
                Avatar.ShowEmpty();
            }
        }
    }
}
