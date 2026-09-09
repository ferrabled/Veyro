using MotionRunner.Progression;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// The profile's "my last runs" list, which lives its whole life as one PlayerPrefs string.
    /// A lossy round trip or a throw on a malformed value would surface as the menu failing to
    /// build on a player's phone and nowhere else.
    public sealed class RunHistoryTests
    {
        static RunRecord Run(int score) => new RunRecord("2026-09-02", true, score, 12, 340);

        [Test]
        public void AnEmptyStringDecodesToNoRuns()
        {
            Assert.AreEqual(0, RunHistory.Decode(null).Count);
            Assert.AreEqual(0, RunHistory.Decode(string.Empty).Count);
        }

        [Test]
        public void ARunRoundTrips()
        {
            string encoded = RunHistory.Append(string.Empty,
                new RunRecord("2026-09-02", false, 1234, 17, 890));

            var runs = RunHistory.Decode(encoded);
            Assert.AreEqual(1, runs.Count);
            Assert.AreEqual("2026-09-02", runs[0].DayLabel);
            Assert.IsFalse(runs[0].Daily);
            Assert.AreEqual(1234, runs[0].Score);
            Assert.AreEqual(17, runs[0].Coins);
            Assert.AreEqual(890, runs[0].Distance);
        }

        [Test]
        public void TheNewestRunIsFirst()
        {
            string encoded = RunHistory.Append(string.Empty, Run(100));
            encoded = RunHistory.Append(encoded, Run(200));
            encoded = RunHistory.Append(encoded, Run(300));

            var runs = RunHistory.Decode(encoded);
            Assert.AreEqual(300, runs[0].Score);
            Assert.AreEqual(200, runs[1].Score);
            Assert.AreEqual(100, runs[2].Score);
        }

        [Test]
        public void TheListIsBoundedAndDropsTheOldest()
        {
            string encoded = string.Empty;
            for (int i = 0; i < RunHistory.MaxEntries + 4; i++)
                encoded = RunHistory.Append(encoded, Run(i));

            var runs = RunHistory.Decode(encoded);
            Assert.AreEqual(RunHistory.MaxEntries, runs.Count);
            Assert.AreEqual(RunHistory.MaxEntries + 3, runs[0].Score);
            Assert.AreEqual(4, runs[runs.Count - 1].Score);
        }

        [Test]
        public void AMalformedStringIsSkippedRatherThanThrown()
        {
            // A truncated write, a value from a future version, a field that is not a number:
            // whatever is unreadable is dropped and whatever is readable survives.
            string good = RunHistory.Append(string.Empty, Run(500));
            var runs = RunHistory.Decode("garbage;1|2|3;" + good + ";|||");

            Assert.AreEqual(1, runs.Count);
            Assert.AreEqual(500, runs[0].Score);
        }

        [Test]
        public void AMalformedDailyFlagDropsTheEntry()
        {
            // The flag has exactly two legal spellings. A corrupted one must not quietly decode
            // as "not daily" - that would refile a Daily Run as a free run - so the entry is
            // dropped like any other unreadable value.
            string good = RunHistory.Append(string.Empty, Run(500));
            var runs = RunHistory.Decode("2026-09-02|x|10|1|2;" + good + ";2026-09-02||10|1|2");

            Assert.AreEqual(1, runs.Count);
            Assert.AreEqual(500, runs[0].Score);
        }

        [Test]
        public void ASeparatorInAFieldCannotEatTheNextEntry()
        {
            // The day label is machine-generated and cannot contain one - but the encoding must
            // survive it anyway, because a corrupted key that silently shifted every following
            // run's numbers would be indistinguishable from real history.
            string encoded = RunHistory.Append(string.Empty,
                new RunRecord("2026;09|02", true, 42, 1, 2));
            encoded = RunHistory.Append(encoded, Run(7));

            var runs = RunHistory.Decode(encoded);
            Assert.AreEqual(2, runs.Count);
            Assert.AreEqual(7, runs[0].Score);
            Assert.AreEqual(42, runs[1].Score);
        }
    }
}
