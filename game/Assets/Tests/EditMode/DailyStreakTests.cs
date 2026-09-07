using System;
using MotionRunner.Progression;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// The stamp card's arithmetic. Every bug a streak counter has is a date bug, and a date bug
    /// takes a day to reproduce by playing - which is exactly the kind of thing that gets shipped.
    public sealed class DailyStreakTests
    {
        const int Day = 1000; // an arbitrary day number; only the differences matter

        [Test]
        public void NothingPlayed_IsNoStreakAndNoStamps()
        {
            var record = StreakRecord.Empty;
            Assert.AreEqual(0, DailyStreak.LengthOn(record, Day));
            Assert.IsFalse(DailyStreak.PlayedOn(record, Day));
            CollectionAssert.AreEqual(new bool[DailyStreak.WindowDays], DailyStreak.Stamps(record, Day));
        }

        [Test]
        public void TheFirstRunStartsAStreakOfOne()
        {
            var record = DailyStreak.Record(StreakRecord.Empty, Day);
            Assert.AreEqual(1, DailyStreak.LengthOn(record, Day));
            Assert.IsTrue(DailyStreak.PlayedOn(record, Day));
        }

        [Test]
        public void ASecondRunTheSameDay_ChangesNothing()
        {
            var once = DailyStreak.Record(StreakRecord.Empty, Day);
            var twice = DailyStreak.Record(once, Day);

            Assert.AreEqual(once.Length, twice.Length);
            Assert.AreEqual(once.Mask, twice.Mask);
            Assert.AreEqual(once.LastDayNumber, twice.LastDayNumber);
        }

        [Test]
        public void ConsecutiveDaysExtendTheStreak()
        {
            var record = StreakRecord.Empty;
            for (int i = 0; i < 5; i++) record = DailyStreak.Record(record, Day + i);
            Assert.AreEqual(5, DailyStreak.LengthOn(record, Day + 4));
        }

        [Test]
        public void ASkippedDayResetsTheStreakToOne()
        {
            var record = DailyStreak.Record(StreakRecord.Empty, Day);
            record = DailyStreak.Record(record, Day + 1);
            record = DailyStreak.Record(record, Day + 3);

            Assert.AreEqual(1, DailyStreak.LengthOn(record, Day + 3));
        }

        [Test]
        public void AStreakSurvivesTheDayAfterItsLastRun()
        {
            // Played yesterday, not yet today: the streak is alive and the card must say so -
            // that is the whole moment the card exists for.
            var record = DailyStreak.Record(StreakRecord.Empty, Day);
            Assert.AreEqual(1, DailyStreak.LengthOn(record, Day + 1));
        }

        [Test]
        public void AStreakBreaksOnceAWholeDayIsMissed()
        {
            var record = DailyStreak.Record(StreakRecord.Empty, Day);
            Assert.AreEqual(0, DailyStreak.LengthOn(record, Day + 2));
        }

        [Test]
        public void StampsAreOldestFirstWithTodayLast()
        {
            // Played today and three days ago.
            var record = DailyStreak.Record(StreakRecord.Empty, Day - 3);
            record = DailyStreak.Record(record, Day);

            var stamps = DailyStreak.Stamps(record, Day);
            Assert.AreEqual(DailyStreak.WindowDays, stamps.Length);
            Assert.IsTrue(stamps[DailyStreak.WindowDays - 1], "today should be stamped");
            Assert.IsTrue(stamps[DailyStreak.WindowDays - 4], "three days ago should be stamped");
            Assert.IsFalse(stamps[DailyStreak.WindowDays - 2]);
            Assert.IsFalse(stamps[DailyStreak.WindowDays - 3]);
        }

        [Test]
        public void StampsScrollOffTheEndOfTheWindow()
        {
            var record = DailyStreak.Record(StreakRecord.Empty, Day);
            var later = DailyStreak.Record(record, Day + DailyStreak.WindowDays);

            var stamps = DailyStreak.Stamps(later, Day + DailyStreak.WindowDays);
            Assert.IsTrue(stamps[DailyStreak.WindowDays - 1]);
            for (int i = 0; i < DailyStreak.WindowDays - 1; i++)
                Assert.IsFalse(stamps[i], "day " + i + " should have scrolled off");
        }

        [Test]
        public void AClockThatGoesBackwardsNeverTakesAStampAway()
        {
            // The device date being wrong is the player's problem; losing a streak over it is
            // ours. A day before the one on record is ignored outright.
            var record = DailyStreak.Record(StreakRecord.Empty, Day);
            record = DailyStreak.Record(record, Day + 1);
            var afterSkew = DailyStreak.Record(record, Day - 5);

            Assert.AreEqual(record.LastDayNumber, afterSkew.LastDayNumber);
            Assert.AreEqual(record.Length, afterSkew.Length);
            Assert.AreEqual(record.Mask, afterSkew.Mask);
        }

        [Test]
        public void DayNumbersAdvanceOncePerUtcDay()
        {
            var midnight = new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc);
            var lateSameDay = new DateTime(2026, 9, 2, 23, 59, 59, DateTimeKind.Utc);
            var nextDay = new DateTime(2026, 9, 3, 0, 0, 1, DateTimeKind.Utc);

            Assert.AreEqual(DailyStreak.DayNumber(midnight), DailyStreak.DayNumber(lateSameDay));
            Assert.AreEqual(DailyStreak.DayNumber(midnight) + 1, DailyStreak.DayNumber(nextDay));
        }
    }
}
