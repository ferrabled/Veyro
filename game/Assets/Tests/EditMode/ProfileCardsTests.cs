using System.Collections.Generic;
using MotionRunner.Progression;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// The data behind the profile's leaderboard podium and its recent-runs chart.
    public sealed class ProfileCardsTests
    {
        // ---- podium ----

        [Test]
        public void AFullBoard_PutsTheTopThreeOnThePodiumAndTheRestBelow()
        {
            var rows = Board(8);
            var podium = Podium.Split(rows, out var rest);

            Assert.AreEqual(Podium.Places, podium.Length);
            for (int i = 0; i < Podium.Places; i++) Assert.AreEqual(i + 1, podium[i].Value.Rank);
            Assert.AreEqual(5, rest.Count);
            Assert.AreEqual(4, rest[0].Rank, "the list starts where the podium ends");
        }

        [Test]
        public void AFreshBoard_LeavesTheEmptyPlacesEmpty()
        {
            var podium = Podium.Split(Board(1), out var rest);
            Assert.IsTrue(podium[0].HasValue);
            Assert.IsFalse(podium[1].HasValue);
            Assert.IsFalse(podium[2].HasValue);
            Assert.AreEqual(0, rest.Count);

            podium = Podium.Split(null, out rest);
            Assert.AreEqual(Podium.Places, podium.Length);
            Assert.AreEqual(0, rest.Count);
        }

        [Test]
        public void ThePlayerBelowTheVisibleRows_StaysAtTheBottomOfTheList()
        {
            // The board's "..." contract (LeaderboardTests): the last row can be the player's,
            // at a rank far below the others. It belongs to the list, never the podium.
            var rows = Board(7);
            rows.Add(new LeaderboardEntry(41, "YOU", 120, true));
            Podium.Split(rows, out var rest);
            Assert.AreEqual(41, rest[rest.Count - 1].Rank);
            Assert.IsTrue(rest[rest.Count - 1].IsYou);
        }

        [Test]
        public void ThePodium_IsSecondFirstThird_WithTheWinnerTallest()
        {
            CollectionAssert.AreEqual(new[] { 1, 0, 2 }, Podium.DisplayOrder);
            Assert.AreEqual(1f, Podium.BlockHeights[0]);
            Assert.Greater(Podium.BlockHeights[0], Podium.BlockHeights[1]);
            Assert.Greater(Podium.BlockHeights[1], Podium.BlockHeights[2]);
        }

        // ---- recent runs ----

        [Test]
        public void NoRuns_DigestsToNothing()
        {
            var digest = RunDigest.Of(new RunRecord[0]);
            Assert.AreEqual(0, digest.Count);
            Assert.AreEqual(-1, digest.BestIndex);
            Assert.AreEqual(0, digest.Average);
            Assert.AreEqual(0, RunDigest.Of(null).Count);
        }

        [Test]
        public void TheDigest_AddsUpTheRunsItIsGiven()
        {
            var runs = new[]
            {
                Run(785, 135, 19), // newest
                Run(1805, 255, 37),
                Run(183, 63, 7)
            };
            var digest = RunDigest.Of(runs);
            Assert.AreEqual(3, digest.Count);
            Assert.AreEqual(1805, digest.Best);
            Assert.AreEqual(1, digest.BestIndex);
            Assert.AreEqual(924, digest.Average, "(785 + 1805 + 183) / 3 = 924.33");
            Assert.AreEqual(453, digest.TotalDistance);
            Assert.AreEqual(63, digest.TotalCoins);
        }

        [Test]
        public void ATiedBest_IsTheNewestRun()
        {
            var digest = RunDigest.Of(new[] { Run(900, 1, 1), Run(900, 1, 1), Run(10, 1, 1) });
            Assert.AreEqual(0, digest.BestIndex, "the run just played should wear the best colour");
        }

        [Test]
        public void Bars_AreProportional_ButNeverVanish()
        {
            Assert.AreEqual(1f, RunDigest.BarFraction(1805, 1805));
            Assert.AreEqual(0.5f, RunDigest.BarFraction(900, 1800), 1e-6f);
            Assert.AreEqual(RunDigest.MinBar, RunDigest.BarFraction(0, 1805));
            Assert.AreEqual(RunDigest.MinBar, RunDigest.BarFraction(3, 1805));
            Assert.AreEqual(RunDigest.MinBar, RunDigest.BarFraction(0, 0), "all-zero runs still draw stubs");
        }

        static List<LeaderboardEntry> Board(int count)
        {
            var rows = new List<LeaderboardEntry>();
            for (int i = 0; i < count; i++)
                rows.Add(new LeaderboardEntry(i + 1, "P" + (i + 1), 5000 - i * 400, false));
            return rows;
        }

        static RunRecord Run(int score, int distance, int coins) =>
            new RunRecord("2026-09-27", true, score, coins, distance);
    }
}
