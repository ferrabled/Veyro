using System;
using System.Globalization;
using MotionRunner.Audio;
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
        public const float Height = 236f;

        const float StampSize = 84f;

        readonly Func<DateTime> _utcNow;

        /// The last day whose stamp this process has already celebrated, and whether a baseline
        /// has been taken. Static because the card is rebuilt with every MainMenu (a run destroys
        /// the menu), and "today was already stamped when the app launched" must not sound like
        /// news: the first Refresh of the process only records what is there; a stamp that
        /// APPEARS on a later Refresh - the menu coming back after the day's first Daily Run -
        /// is the one that lands with a sound (T-045).
        static bool _baselined;
        static int _celebratedDay = int.MinValue;

        Text _streak;
        CosmeticPanel _streakPill;
        Text _note;
        CosmeticPanel[] _stamps;
        CosmeticPanel[] _stampRims;
        GameObject[] _ticks;
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
            CosmeticUi.Card(slot);

            float pad = MenuTheme.CardPadding;

            RuntimeUi.Label("Title", slot,
                new Vector2(0f, 1f), new Vector2(0.6f, 1f),
                new Vector2(pad, -56f), new Vector2(0f, -8f),
                38, TextAnchor.MiddleLeft, MenuTheme.Text).text = "DAILY STREAK";

            // The streak as a pill: pink and filled while there is one to keep, a quiet grey
            // "NO STREAK" when there is not.
            RuntimeUi.Element("Streak", slot, out var pill);
            pill.anchorMin = pill.anchorMax = new Vector2(1f, 1f);
            pill.pivot = new Vector2(1f, 1f);
            pill.anchoredPosition = new Vector2(-pad, -12f);
            pill.sizeDelta = new Vector2(200f, 44f);
            _streakPill = CosmeticUi.Surface(pill, MenuTheme.Accent, 22f);
            _streakPill.raycastTarget = false;
            _streak = RuntimeUi.Label("Label", pill, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, 24, TextAnchor.MiddleCenter, MenuTheme.OnAccent);
            _streak.fontStyle = FontStyle.Bold;

            BuildStampRow(slot, pad);

            _note = RuntimeUi.Label("Note", slot,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(pad, -224f), new Vector2(-pad, -192f),
                26, TextAnchor.MiddleLeft, MenuTheme.Faint);

            Refresh();
        }

        void BuildStampRow(RectTransform slot, float pad)
        {
            int days = DailyStreak.WindowDays;
            _stamps = new CosmeticPanel[days];
            _stampRims = new CosmeticPanel[days];
            _ticks = new GameObject[days];
            _dayLabels = new Text[days];

            // One row inset by the card padding, the stamps spread across it edge to edge: the
            // first sits on the title's left edge and the last under the streak pill's right
            // edge, whatever the card's width. Each cell is anchored at its share of the row with
            // a matching pivot, which is what lands the ends exactly on the row's edges.
            var row = RuntimeUi.Element("Stamps", slot, out var rowRect);
            RuntimeUi.Stretch(rowRect,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(pad, -186f), new Vector2(-pad, -66f));

            for (int i = 0; i < days; i++)
            {
                float t = days > 1 ? i / (float)(days - 1) : 0.5f;
                var cell = RuntimeUi.Element("Day" + i, row.transform, out var cellRect);
                cellRect.anchorMin = new Vector2(t, 0f);
                cellRect.anchorMax = new Vector2(t, 1f);
                cellRect.pivot = new Vector2(t, 0.5f);
                cellRect.anchoredPosition = Vector2.zero;
                cellRect.sizeDelta = new Vector2(StampSize, 0f);

                // A rounded stamp: a rim (pink on today, so today reads as "this one" before it
                // is earned), the fill inside it, and a paper tick once the day is stamped.
                RuntimeUi.Element("Stamp", cell.transform, out var stampRect);
                stampRect.anchorMin = new Vector2(0.5f, 1f);
                stampRect.anchorMax = new Vector2(0.5f, 1f);
                stampRect.anchoredPosition = new Vector2(0f, -StampSize * 0.5f);
                stampRect.sizeDelta = new Vector2(StampSize, StampSize);
                _stampRims[i] = CosmeticUi.Surface(stampRect, MenuTheme.Empty, 20f);
                _stampRims[i].raycastTarget = false;

                RuntimeUi.Element("Fill", stampRect, out var fillRect);
                RuntimeUi.Stretch(fillRect, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
                _stamps[i] = CosmeticUi.Surface(fillRect, MenuTheme.Empty, 16f);
                _stamps[i].raycastTarget = false;

                var tick = RuntimeUi.Element("Tick", stampRect, out var tickRect);
                RuntimeUi.Stretch(tickRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                MenuIcons.Tick(tickRect, MenuTheme.OnAccent, 38f, 7f);
                _ticks[i] = tick;

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
                Color fill = stamps[i]
                    ? (isToday ? MenuTheme.Accent : MenuTheme.Gold)
                    : isToday ? MenuTheme.Card : MenuTheme.Empty;
                _stamps[i].color = fill;
                _stampRims[i].color = isToday ? MenuTheme.Accent : fill;
                _ticks[i].SetActive(stamps[i]);

                var day = utcNow.Date.AddDays(-(days - 1 - i));
                _dayLabels[i].text = DayLetter(day);
                _dayLabels[i].color = isToday ? MenuTheme.Text : MenuTheme.Faint;
                _dayLabels[i].fontStyle = isToday ? FontStyle.Bold : FontStyle.Normal;
            }

            int length = DailyStreak.LengthOn(record, today);
            _streak.text = length == 0 ? "NO STREAK" : length + (length == 1 ? " DAY" : " DAYS");
            _streak.color = length == 0 ? MenuTheme.Faint : MenuTheme.OnAccent;
            _streakPill.color = length == 0 ? MenuTheme.Empty : MenuTheme.Accent;

            bool stampedToday = DailyStreak.PlayedOn(record, today);
            CelebrateNewStamp(stampedToday, today);
            _note.text = stampedToday
                ? "today is stamped — come back tomorrow"
                : length == 0
                    ? "finish a daily run to start a streak"
                    : "run today to keep the streak alive";
        }

        /// One streak cue per stamped day, and only for a stamp that landed during this process -
        /// see the statics above.
        static void CelebrateNewStamp(bool stampedToday, int today)
        {
            if (!_baselined)
            {
                _baselined = true;
                if (stampedToday) _celebratedDay = today;
                return;
            }
            if (!stampedToday || _celebratedDay == today) return;
            _celebratedDay = today;
            GameAudio.Play(Sfx.Streak);
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
