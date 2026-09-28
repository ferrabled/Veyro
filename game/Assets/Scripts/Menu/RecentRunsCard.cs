using System.Collections.Generic;
using MotionRunner.Core;
using MotionRunner.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace MotionRunner.Menu
{
    /// The profile tab's "my last runs", drawn as the story they tell rather than as a table: a
    /// bar per run, oldest on the left and the newest on the right (pink, "latest"), the best of
    /// them in gold with every score on top of its bar; then three tiles - BEST, AVERAGE,
    /// DISTANCE - in the result card's own tile style, and one line spelling out the latest run.
    ///
    /// A podium makes no sense for a player's own runs; a trend does. The eight slots are fixed,
    /// so a new player sees their first runs fill the chart from the right with the empty slots
    /// still waiting on the left. Numbers come from RunDigest, which is engine-free and tested.
    public sealed class RecentRunsCard
    {
        public const float Height = 600f;

        const float ChartTop = 92f;
        const float ChartHeight = 264f;
        const float CaptionHeight = 32f;
        const float ScoreHeight = 32f;
        const float BarMax = ChartHeight - CaptionHeight - ScoreHeight - 14f;

        const float TilesTop = ChartTop + ChartHeight + 18f;
        const float TileHeight = 128f;
        const float LatestTop = TilesTop + TileHeight + 14f;

        readonly Column[] _columns = new Column[RunHistory.MaxEntries];
        readonly Text[] _tiles = new Text[3];
        Text _count;
        Text _empty;
        Text _latest;

        public void Build(RectTransform slot)
        {
            RuntimeUi.Panel("Card", slot, MenuTheme.Card);
            float pad = MenuTheme.CardPadding;

            RuntimeUi.Label("Title", slot,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(pad, -66f), new Vector2(-pad - 240f, -14f),
                38, TextAnchor.MiddleLeft, MenuTheme.Text).text = "MY LAST RUNS";
            _count = RuntimeUi.Label("Count", slot,
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-pad - 240f, -66f), new Vector2(-pad, -14f),
                26, TextAnchor.MiddleRight, MenuTheme.Faint);

            RuntimeUi.Element("Chart", slot, out var chart);
            RuntimeUi.Stretch(chart, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(pad, -ChartTop - ChartHeight), new Vector2(-pad, -ChartTop));
            for (int i = 0; i < _columns.Length; i++)
                _columns[i] = new Column(chart, i, _columns.Length);

            _empty = RuntimeUi.Label("Empty", chart, Vector2.zero, Vector2.one,
                new Vector2(0f, CaptionHeight), Vector2.zero, 30, TextAnchor.MiddleCenter, MenuTheme.Faint);
            _empty.text = "no runs yet — the first one lands here";

            RuntimeUi.Element("Tiles", slot, out var tiles);
            RuntimeUi.Stretch(tiles, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(pad - 7f, -TilesTop - TileHeight), new Vector2(-pad + 7f, -TilesTop));
            _tiles[0] = Tile(tiles, 0, "BEST");
            _tiles[1] = Tile(tiles, 1, "AVERAGE");
            _tiles[2] = Tile(tiles, 2, "DISTANCE");
            _tiles[0].color = MenuTheme.Gold;

            _latest = RuntimeUi.Label("Latest", slot,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(pad, -LatestTop - 40f), new Vector2(-pad, -LatestTop),
                24, TextAnchor.MiddleLeft, MenuTheme.Faint);
            MenuRows.Fit(_latest, 24);
        }

        /// RunHud's stat tile: bold value over a quiet caption, on a rounded slot.
        static Text Tile(RectTransform row, int index, string caption)
        {
            RuntimeUi.Element("Tile" + index, row, out var tile);
            RuntimeUi.Stretch(tile, new Vector2(index / 3f, 0f), new Vector2((index + 1) / 3f, 1f),
                new Vector2(7f, 0f), new Vector2(-7f, 0f));
            CosmeticUi.Surface(tile, MenuTheme.Slot, 22f).raycastTarget = false;

            var value = RuntimeUi.Label("Value", tile, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(6f, -80f), new Vector2(-6f, -14f), 44, TextAnchor.MiddleCenter, MenuTheme.Text);
            value.fontStyle = FontStyle.Bold;
            MenuRows.Fit(value, 44);

            RuntimeUi.Label("Caption", tile, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(4f, 12f), new Vector2(-4f, 50f), 22, TextAnchor.MiddleCenter, MenuTheme.Faint).text = caption;
            return value;
        }

        /// Runs arrive newest first (RunHistory's order) and are laid out newest on the right.
        public void Show(IReadOnlyList<RunRecord> runs)
        {
            int count = runs?.Count ?? 0;
            var digest = RunDigest.Of(runs);

            for (int slot = 0; slot < _columns.Length; slot++)
            {
                int index = _columns.Length - 1 - slot; // the rightmost slot is the newest run
                if (index >= count)
                {
                    _columns[slot].ShowEmpty();
                    continue;
                }

                var run = runs[index];
                Color color = index == digest.BestIndex ? MenuTheme.Gold
                    : index == 0 ? MenuTheme.Accent
                    : MenuTheme.Owned;
                string caption = index == 0 ? "latest" : run.Daily ? "daily" : "free";
                _columns[slot].Show(run.Score, RunDigest.BarFraction(run.Score, digest.Best), color,
                    caption, index == 0);
            }

            _empty.gameObject.SetActive(count == 0);
            _count.text = count == 0 ? string.Empty : "last " + count + (count == 1 ? " run" : " runs");

            _tiles[0].text = count == 0 ? "—" : digest.Best.ToString();
            _tiles[1].text = count == 0 ? "—" : digest.Average.ToString();
            _tiles[2].text = count == 0 ? "—" : digest.TotalDistance + " m";

            if (count == 0)
            {
                _latest.text = string.Empty;
                return;
            }
            var latest = runs[0];
            _latest.text = "latest  ·  " + latest.DayLabel + "  ·  " + (latest.Daily ? "daily" : "free") +
                           "  ·  " + latest.Score + " pts  ·  " + latest.Distance + " m  ·  " +
                           latest.Coins + " coins";
        }

        /// One slot on the chart: the score, the bar, and a caption under the baseline.
        sealed class Column
        {
            readonly RectTransform _bar;
            readonly CosmeticPanel _barPanel;
            readonly Text _score;
            readonly Text _caption;

            public Column(RectTransform chart, int slot, int slots)
            {
                RuntimeUi.Element("Run" + slot, chart, out var rect);
                RuntimeUi.Stretch(rect, new Vector2(slot / (float)slots, 0f), new Vector2((slot + 1) / (float)slots, 1f),
                    Vector2.zero, Vector2.zero);

                RuntimeUi.Element("Bar", rect, out _bar);
                _bar.anchorMin = new Vector2(0.18f, 0f);
                _bar.anchorMax = new Vector2(0.82f, 0f);
                _bar.pivot = new Vector2(0.5f, 0f);
                _barPanel = CosmeticUi.Surface(_bar, MenuTheme.Owned, 10f);
                _barPanel.raycastTarget = false;

                _score = RuntimeUi.Label("Score", rect, Vector2.zero, new Vector2(1f, 0f),
                    Vector2.zero, Vector2.zero, 22, TextAnchor.LowerCenter, MenuTheme.Text);
                _score.fontStyle = FontStyle.Bold;

                _caption = RuntimeUi.Label("Caption", rect, Vector2.zero, new Vector2(1f, 0f),
                    Vector2.zero, new Vector2(0f, CaptionHeight - 6f), 20, TextAnchor.MiddleCenter, MenuTheme.Faint);
            }

            public void Show(int score, float fraction, Color color, string caption, bool latest)
            {
                float height = BarMax * fraction;
                _bar.anchoredPosition = new Vector2(0f, CaptionHeight);
                _bar.sizeDelta = new Vector2(0f, height);
                _barPanel.color = color;

                var scoreRect = _score.rectTransform;
                scoreRect.offsetMin = new Vector2(0f, CaptionHeight + height + 4f);
                scoreRect.offsetMax = new Vector2(0f, CaptionHeight + height + 4f + ScoreHeight);
                _score.text = score.ToString();
                _score.color = MenuTheme.Text;

                _caption.text = caption;
                _caption.color = latest ? MenuTheme.Accent : MenuTheme.Faint;
                _caption.fontStyle = latest ? FontStyle.Bold : FontStyle.Normal;
            }

            /// A slot no run has reached yet: a flat stub on the baseline, nothing else.
            public void ShowEmpty()
            {
                _bar.anchoredPosition = new Vector2(0f, CaptionHeight);
                _bar.sizeDelta = new Vector2(0f, 6f);
                _barPanel.color = MenuTheme.Empty;
                _score.text = string.Empty;
                _caption.text = string.Empty;
            }
        }
    }
}
