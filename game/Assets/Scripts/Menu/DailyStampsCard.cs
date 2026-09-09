using System;
using System.Globalization;
using MotionRunner.Core;
using MotionRunner.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace MotionRunner.Menu
{
    /// The stamp card: one square per day for the last week, filled on the days a Daily Run was
    /// finished, with the current streak beside it. The Duolingo shape, because the Daily Run is
    /// already the retention loop (D9/D10 - one shared seed a day, no server) and this is the part
    /// of it the player can see.
    ///
    /// Every number on it is real from the first run onward: the streak comes off disk via
    /// ProgressStore and the rules are unit tested in DailyStreak. The sponsor wiring the streak
    /// feeds - a OneSignal "your streak ends tonight" campaign, tagged with the streak length
    /// (T-021) - reads the same StreakRecord, so nothing here has to change when it lands.
    public sealed class DailyStampsCard
    {
        public const float Height = 218f;

        /// Gap between two stamps. The stamps themselves take whatever is left, so the row fits
        /// any card width without a layout group.
        const float StampGap = 14f;
        const float StampSize = 92f;

        readonly Func<DateTime> _utcNow;

        Text _streak;
        Text _note;
        Image[] _stamps;
        Text[] _dayLabels;

        /// The clock arrives as a function, like everywhere else that has a date in it: it makes
        /// the card's "which day is today" the caller's business and keeps this testable by hand
        /// on device (change the phone's date, reopen the menu).
        public DailyStampsCard(Func<DateTime> utcNow)
        {
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public void Build(RectTransform slot)
        {
            RuntimeUi.Panel("Card", slot, MenuTheme.Card);

            float pad = MenuTheme.CardPadding;

            RuntimeUi.Label("Title", slot,
                new Vector2(0f, 1f), new Vector2(0.6f, 1f),
                new Vector2(pad, -56f), new Vector2(0f, -8f),
                38, TextAnchor.MiddleLeft, MenuTheme.Text).text = "DAILY STREAK";

            _streak = RuntimeUi.Label("Streak", slot,
                new Vector2(0.4f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -56f), new Vector2(-pad, -8f),
                32, TextAnchor.MiddleRight, MenuTheme.Accent);

            BuildStampRow(slot, pad);

            _note = RuntimeUi.Label("Note", slot,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(pad, -214f), new Vector2(-pad, -186f),
                26, TextAnchor.MiddleLeft, MenuTheme.Faint);

            Refresh();
        }

        void BuildStampRow(RectTransform slot, float pad)
        {
            int days = DailyStreak.WindowDays;
            _stamps = new Image[days];
            _dayLabels = new Text[days];

            // One row inset by the card padding, then cells that split it evenly in anchor space.
            // Insetting the row rather than each cell is what keeps the seven columns equal: a
            // pixel padding applied per cell would only shorten the two on the ends.
            var row = RuntimeUi.Element("Stamps", slot, out var rowRect);
            RuntimeUi.Stretch(rowRect,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(pad, -186f), new Vector2(-pad, -62f));

            float cellWidth = 1f / days;
            for (int i = 0; i < days; i++)
            {
                var cell = RuntimeUi.Element("Day" + i, row.transform, out var cellRect);
                RuntimeUi.Stretch(cellRect,
                    new Vector2(i * cellWidth, 0f), new Vector2((i + 1) * cellWidth, 1f),
                    new Vector2(StampGap * 0.5f, 0f), new Vector2(-StampGap * 0.5f, 0f));

                var stamp = RuntimeUi.Element("Stamp", cell.transform, out var stampRect);
                stampRect.anchorMin = new Vector2(0.5f, 1f);
                stampRect.anchorMax = new Vector2(0.5f, 1f);
                stampRect.anchoredPosition = new Vector2(0f, -StampSize * 0.5f);
                stampRect.sizeDelta = new Vector2(StampSize, StampSize);
                _stamps[i] = stamp.AddComponent<Image>();

                _dayLabels[i] = RuntimeUi.Label("Letter", cell.transform,
                    new Vector2(0f, 0f), new Vector2(1f, 0f),
                    new Vector2(0f, 0f), new Vector2(0f, 30f),
                    24, TextAnchor.MiddleCenter, MenuTheme.Faint);
            }
        }

        public void Refresh()
        {
            if (_stamps == null) return;

            var utcNow = _utcNow();
            int today = DailyStreak.DayNumber(utcNow);
            var record = ProgressStore.Streak;

            var stamps = DailyStreak.Stamps(record, today);
            int days = _stamps.Length;

            for (int i = 0; i < days; i++)
            {
                bool isToday = i == days - 1;
                _stamps[i].color = stamps[i]
                    ? (isToday ? MenuTheme.Accent : MenuTheme.Gold)
                    : MenuTheme.Empty;

                var day = utcNow.Date.AddDays(-(days - 1 - i));
                _dayLabels[i].text = DayLetter(day);
                _dayLabels[i].color = isToday ? MenuTheme.Text : MenuTheme.Faint;
            }

            int length = DailyStreak.LengthOn(record, today);
            _streak.text = length == 0 ? "NO STREAK" : length + (length == 1 ? " DAY" : " DAYS");
            _streak.color = length == 0 ? MenuTheme.Faint : MenuTheme.Accent;

            bool stampedToday = DailyStreak.PlayedOn(record, today);
            _note.text = stampedToday
                ? "today is stamped — come back tomorrow"
                : length == 0
                    ? "finish a daily run to start a streak"
                    : "run today to keep the streak alive";
        }

        /// The first letter of the weekday, invariant rather than localized: the card is seven
        /// single characters wide and a localized name can be two bytes of something the legacy
        /// font has no glyph for (CLAUDE.md gotcha #7's failure mode - text that renders as boxes,
        /// or as nothing, only on the phone).
        static string DayLetter(DateTime day) =>
            CultureInfo.InvariantCulture.DateTimeFormat
                .GetDayName(day.DayOfWeek).Substring(0, 1).ToUpperInvariant();
    }
}
