using System;

namespace MotionRunner.Progression
{
    /// What the streak card knows: the last day a Daily Run was finished, how many days in a row
    /// that made, and which of the recent days were played.
    ///
    /// Days are integers - "days since the epoch below, UTC" - rather than dates or strings. Every
    /// question the card asks ("is today already stamped?", "did the streak survive?", "which of
    /// the last seven?") is then subtraction, which is why this file has no calendar in it and no
    /// timezone can move a stamp.
    public readonly struct StreakRecord
    {
        /// The day of the most recent finished Daily Run, or NeverPlayed.
        public readonly int LastDayNumber;

        /// Consecutive days ending at LastDayNumber. 0 only when nothing has been played.
        public readonly int Length;

        /// Bit i is set when the day LastDayNumber - i was played. Only the low
        /// DailyStreak.WindowDays bits are meaningful; everything older has fallen off the card.
        public readonly int Mask;

        public const int NeverPlayed = int.MinValue;

        public StreakRecord(int lastDayNumber, int length, int mask)
        {
            LastDayNumber = lastDayNumber;
            Length = length;
            Mask = mask;
        }

        public static StreakRecord Empty => new StreakRecord(NeverPlayed, 0, 0);

        public bool HasPlayed => LastDayNumber != NeverPlayed;
    }

    /// The streak rules, engine-free and clock-free: the caller samples the clock and passes the
    /// day in, exactly like DailySeed. Everything a Duolingo-style card gets wrong is arithmetic
    /// on dates - a streak that dies at midnight instead of at the end of the day, a stamp that
    /// lands twice, a device clock that goes backwards - and none of those reproduce reliably by
    /// playing, so they are unit tested instead.
    public static class DailyStreak
    {
        /// How many days the stamp card shows. One week reads at a glance and fits the row.
        public const int WindowDays = 7;

        /// Day zero. Any fixed past date works; this one is recent enough to keep the numbers
        /// small and old enough that no player predates it.
        static readonly DateTime Epoch = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// The UTC day a moment belongs to. UTC, not local: the Daily Run's track already rolls
        /// over on the UTC date (DailySeed), so a streak on any other clock would let a player
        /// "miss" a day they had played or stamp a day whose run they cannot have run.
        public static int DayNumber(DateTime utc) => (int)(utc.Date - Epoch.Date).TotalDays;

        /// A Daily Run was finished on `dayNumber`. Idempotent within a day - finishing five runs
        /// today is one stamp - and inert for a day before the one already recorded, which is what
        /// a device clock jumping backwards looks like from here (never a rollback: a stamp the
        /// player earned is not something a wrong clock may take away).
        public static StreakRecord Record(in StreakRecord current, int dayNumber)
        {
            if (!current.HasPlayed) return new StreakRecord(dayNumber, 1, 1);
            if (dayNumber <= current.LastDayNumber) return current;

            int delta = dayNumber - current.LastDayNumber;
            int mask = delta >= WindowDays ? 1 : ((current.Mask << delta) | 1) & WindowMask;
            int length = delta == 1 ? current.Length + 1 : 1;
            return new StreakRecord(dayNumber, length, mask);
        }

        /// The streak as it stands on `todayDayNumber`.
        ///
        /// A streak survives the whole of the day after its last run: played yesterday and not yet
        /// today is still a live streak the player can extend, and saying "0" there would be both
        /// wrong and the exact moment the card is meant to pull them back in. It breaks once a full
        /// day has been skipped.
        public static int LengthOn(in StreakRecord record, int todayDayNumber)
        {
            if (!record.HasPlayed) return 0;
            int delta = todayDayNumber - record.LastDayNumber;
            return delta <= 1 ? record.Length : 0;
        }

        /// True when today's stamp is already earned - the card's "come back tomorrow" state.
        public static bool PlayedOn(in StreakRecord record, int dayNumber) =>
            record.HasPlayed && BitFor(record, dayNumber);

        /// The stamp row, oldest first: index WindowDays-1 is today, index 0 is six days ago.
        /// Allocates one small array per call, which is a menu refresh, not a frame.
        public static bool[] Stamps(in StreakRecord record, int todayDayNumber)
        {
            var stamps = new bool[WindowDays];
            if (!record.HasPlayed) return stamps;

            for (int i = 0; i < WindowDays; i++)
                stamps[i] = BitFor(record, todayDayNumber - (WindowDays - 1 - i));
            return stamps;
        }

        const int WindowMask = (1 << WindowDays) - 1;

        static bool BitFor(in StreakRecord record, int dayNumber)
        {
            int index = record.LastDayNumber - dayNumber;
            if (index < 0 || index >= WindowDays) return false;
            return (record.Mask & (1 << index)) != 0;
        }
    }
}
