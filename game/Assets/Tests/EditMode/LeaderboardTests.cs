using System.Collections.Generic;
using MotionRunner.Progression;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// The stand-in board the profile card shows until the shared leaderboard exists. The names
    /// and scores are made up; the rank beside the player's own row is not, so these pin the rank
    /// a score earns - above all the below-the-board case, where the display slot the row occupies
    /// and the rank the score earned are different numbers.
    public sealed class LeaderboardTests
    {
        static int YouRows(IReadOnlyList<LeaderboardEntry> rows)
        {
            int count = 0;
            for (int i = 0; i < rows.Count; i++)
                if (rows[i].IsYou) count++;
            return count;
        }

        [Test]
        public void AScoreBelowEveryRowStillGetsARowAtItsEarnedRank()
        {
            var rows = new MockLeaderboard().Top(6, 100);

            Assert.AreEqual(6, rows.Count);
            var you = rows[rows.Count - 1];
            Assert.IsTrue(you.IsYou);
            Assert.AreEqual(100, you.Score);
            // All ten mocked scores beat 100, so the earned rank is 11 - not the sixth display
            // slot the row happens to occupy. The rows above keep their own contiguous ranks; the
            // jump to the player's is the usual "..." at the bottom of a cut-off board.
            Assert.AreEqual(11, you.Rank);
            Assert.AreEqual(1, rows[0].Rank);
            Assert.AreEqual(5, rows[4].Rank);
        }

        [Test]
        public void AScoreInsideTheBoardSlotsInAtItsRank()
        {
            // 4000 sits between Pilot (4310) and Rook (3775): third place, on screen and in rank,
            // and every row below shifts down by one.
            var rows = new MockLeaderboard().Top(6, 4000);

            Assert.AreEqual(6, rows.Count);
            Assert.IsTrue(rows[2].IsYou);
            Assert.AreEqual(3, rows[2].Rank);
            Assert.AreEqual(4000, rows[2].Score);
            Assert.AreEqual(1, rows[0].Rank);
            Assert.AreEqual(4, rows[3].Rank);
        }

        [Test]
        public void EveryBoardHasExactlyOneYouRow()
        {
            // Above the board, inside it, tied with the last mocked score, and below everything:
            // the contract is one IsYou row per board, never zero and never two.
            var board = new MockLeaderboard();
            Assert.AreEqual(1, YouRows(board.Top(6, 99999)));
            Assert.AreEqual(1, YouRows(board.Top(6, 4000)));
            Assert.AreEqual(1, YouRows(board.Top(6, 640)));
            Assert.AreEqual(1, YouRows(board.Top(6, 100)));
            Assert.AreEqual(1, YouRows(board.Top(11, 0)));
        }
    }
}
