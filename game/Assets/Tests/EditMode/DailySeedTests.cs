using System;
using System.Collections.Generic;
using MotionRunner.Track;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// T-008's acceptance criterion — two devices on the same UTC date generate the identical run —
    /// with no server involved (D10). The date is the seed, so this is all provable headlessly.
    public sealed class DailySeedTests
    {
        static List<string> Sequence(RunSeed seed, int count)
        {
            var generator = new TrackGenerator(ChunkLibrary.Greybox(), seed.CreateRandom());
            var ids = new List<string>(count);
            for (int i = 0; i < count; i++) ids.Add(generator.Next(DifficultyCurve.At(i * 2f)).ChunkId);
            return ids;
        }

        static void AssertSameSequence(RunSeed a, RunSeed b, int count = 400)
        {
            var first = Sequence(a, count);
            var second = Sequence(b, count);
            for (int i = 0; i < count; i++)
                Assert.AreEqual(first[i], second[i], "sequences diverge at chunk " + i);
        }

        static void AssertDifferentSequence(RunSeed a, RunSeed b, string because, int count = 200)
        {
            var first = Sequence(a, count);
            var second = Sequence(b, count);
            for (int i = 0; i < count; i++)
                if (first[i] != second[i]) return;
            Assert.Fail(because);
        }

        [Test]
        public void SeedIsTheDateAsYyyyMmdd()
        {
            // Readable on purpose: a player can tell you which run they played from the number.
            Assert.AreEqual(20260821, DailySeed.ForDate(2026, 8, 21));
            Assert.AreEqual(20260821, DailySeed.ForDate(new DateTime(2026, 8, 21, 23, 59, 59, DateTimeKind.Utc)));
        }

        [Test]
        public void TimeOfDayDoesNotAffectTheSeed()
        {
            var earlyUtc = new DateTime(2026, 8, 21, 0, 0, 1, DateTimeKind.Utc);
            var lateUtc = new DateTime(2026, 8, 21, 23, 59, 59, DateTimeKind.Utc);
            Assert.AreEqual(DailySeed.ForDate(earlyUtc), DailySeed.ForDate(lateUtc));
            AssertSameSequence(DailySeed.ForUtcDate(earlyUtc), DailySeed.ForUtcDate(lateUtc));
        }

        [Test]
        public void TwoDevicesOnTheSameDate_GenerateTheIdenticalRun()
        {
            // The AC. Separate DateTime instances stand in for two devices whose clocks agree on
            // the date but not the millisecond.
            var deviceA = new DateTime(2026, 8, 21, 6, 12, 44, DateTimeKind.Utc);
            var deviceB = new DateTime(2026, 8, 21, 19, 3, 7, DateTimeKind.Utc);
            AssertSameSequence(DailySeed.ForUtcDate(deviceA), DailySeed.ForUtcDate(deviceB), 2000);
        }

        [Test]
        public void ConsecutiveDates_GenerateDifferentRuns()
        {
            AssertDifferentSequence(
                DailySeed.ForUtcDate(new DateTime(2026, 8, 21, 0, 0, 0, DateTimeKind.Utc)),
                DailySeed.ForUtcDate(new DateTime(2026, 8, 22, 0, 0, 0, DateTimeKind.Utc)),
                "two consecutive days produced the same run - the Daily Run would never change");
        }

        [Test]
        public void ManyConsecutiveDates_AllDiffer()
        {
            var seen = new HashSet<uint>();
            var date = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            for (int i = 0; i < 400; i++)
            {
                uint state = DailySeed.ForUtcDate(date).RngState();
                Assert.IsTrue(seen.Add(state), "two dates within 400 days share an RNG state");
                date = date.AddDays(1);
            }
        }

        [Test]
        public void DifferentWorldId_GivesADifferentRunOnTheSameDate()
        {
            var utc = new DateTime(2026, 8, 21, 0, 0, 0, DateTimeKind.Utc);
            AssertDifferentSequence(
                DailySeed.ForUtcDate(utc, RunSeed.DefaultWorldId),
                DailySeed.ForUtcDate(utc, "alps"),
                "two worlds produced the same run on the same date");
        }

        [Test]
        public void DailySeed_CarriesTheContentVersionNotTheAppVersion()
        {
            // A bugfix release must not fork the day's track, so the seed is built from
            // ChunkLibrary.ContentVersion and nothing else version-shaped.
            var utc = new DateTime(2026, 8, 21, 0, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual(ChunkLibrary.ContentVersion, DailySeed.ForUtcDate(utc).ContentVersion);
        }

        [Test]
        public void BumpingTheContentVersion_ChangesTheRun()
        {
            // The flip side: changing the library deliberately invalidates comparability, which is
            // why ContentVersion must be bumped when the library changes.
            var utc = new DateTime(2026, 8, 21, 0, 0, 0, DateTimeKind.Utc);
            int seed = DailySeed.ForDate(utc);
            AssertDifferentSequence(
                new RunSeed(seed, "greybox-1", RunSeed.DefaultWorldId),
                new RunSeed(seed, "greybox-2", RunSeed.DefaultWorldId),
                "a content-version bump left the run unchanged");
        }

        [Test]
        public void RngStateForKnownDates_IsPinned()
        {
            // Pins the actual daily tracks. A failure here means every already-published Daily Run
            // has silently changed - fix the cause, or bump ChunkLibrary.ContentVersion knowingly.
            // (Values assume ContentVersion "greybox-1" and world "greybox".)
            Assert.AreEqual("greybox-1", ChunkLibrary.ContentVersion,
                "the pinned states below were computed for content version greybox-1");

            Assert.AreEqual(2847724621u, Utc(2026, 8, 21).RngState());
            Assert.AreEqual(2778030310u, Utc(2026, 8, 22).RngState());
            Assert.AreEqual(1373213952u, Utc(2027, 1, 1).RngState());
        }

        static RunSeed Utc(int year, int month, int day) =>
            DailySeed.ForUtcDate(new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc));

        [Test]
        public void LabelIsIsoAndCultureIndependent()
        {
            Assert.AreEqual("2026-08-21", DailySeed.LabelForDate(2026, 8, 21));
            Assert.AreEqual("2026-12-01", DailySeed.LabelForDate(2026, 12, 1));
            Assert.AreEqual("2027-01-09", DailySeed.LabelForDate(new DateTime(2027, 1, 9)));
        }

        [Test]
        public void LeapDayIsAnOrdinaryDay()
        {
            var leap = new DateTime(2028, 2, 29, 0, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual(20280229, DailySeed.ForDate(leap));
            Assert.AreEqual("2028-02-29", DailySeed.LabelForDate(leap));
            Assert.AreNotEqual(0u, DailySeed.ForUtcDate(leap).RngState());
        }

        [Test]
        public void EveryDateProducesAPlayableRun()
        {
            // The generator must not dead-end on some particular day's seed.
            var date = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
            for (int i = 0; i < 120; i++)
            {
                var generator = new TrackGenerator(ChunkLibrary.Greybox(), DailySeed.ForUtcDate(date).CreateRandom());
                for (int chunk = 0; chunk < 400; chunk++) generator.Next(DifficultyCurve.At(chunk * 2f));
                Assert.AreEqual(0, generator.RelaxedCount, "date " + DailySeed.LabelForDate(date) + " needed relaxation");
                date = date.AddDays(1);
            }
        }
    }
}
