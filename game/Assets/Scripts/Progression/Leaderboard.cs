using System.Collections.Generic;

namespace MotionRunner.Progression
{
    public readonly struct LeaderboardEntry
    {
        public readonly int Rank;
        public readonly string Name;
        public readonly int Score;

        /// True for the row that is the player. Exactly one row per board carries it.
        public readonly bool IsYou;

        public LeaderboardEntry(int rank, string name, int score, bool isYou)
        {
            Rank = rank;
            Name = name ?? string.Empty;
            Score = score;
            IsYou = isYou;
        }
    }

    /// Where the profile screen's board comes from. One interface so the Supabase board (D10:
    /// "only when the shared leaderboard ships") replaces the mock without the card knowing.
    public interface ILeaderboardSource
    {
        /// False while the rows are made up, which is what the card puts on screen next to them.
        /// A mocked board that does not say it is mocked is a lie to the player and a trap for
        /// whoever reads a screenshot of it later.
        bool IsLive { get; }

        /// Rows around the player, best first, with exactly one IsYou row at the rank `yourScore`
        /// earns. Never more than `count`.
        IReadOnlyList<LeaderboardEntry> Top(int count, int yourScore);
    }

    /// The stand-in board. Fixed names and fixed scores - no randomness, so the same score always
    /// produces the same board and a screenshot is reproducible - with the player slotted in at
    /// the rank their real daily best actually earns against them. The one true number on the card
    /// is therefore the player's own.
    public sealed class MockLeaderboard : ILeaderboardSource
    {
        /// Scores chosen to straddle the range a first session produces, so a new player lands
        /// mid-table rather than last, and a good run visibly climbs.
        static readonly string[] Names =
        {
            "Nova", "Pilot", "Rook", "Sable", "Wren", "Juno", "Ash", "Kite", "Vale", "Onyx"
        };

        static readonly int[] Scores =
        {
            4820, 4310, 3775, 3240, 2810, 2295, 1860, 1420, 1015, 640
        };

        public const string YourName = "YOU";

        public bool IsLive => false;

        public IReadOnlyList<LeaderboardEntry> Top(int count, int yourScore)
        {
            if (count < 1) return new LeaderboardEntry[0];

            var rows = new List<LeaderboardEntry>(count);
            int rank = 1;
            bool placed = false;

            for (int i = 0; i < Names.Length && rows.Count < count; i++)
            {
                if (!placed && yourScore > Scores[i])
                {
                    rows.Add(new LeaderboardEntry(rank++, YourName, yourScore, true));
                    placed = true;
                    if (rows.Count == count) break;
                }
                rows.Add(new LeaderboardEntry(rank++, Names[i], Scores[i], false));
            }

            // A score below every visible row still gets a row - the player is on the board, at
            // the bottom of it, rather than absent from their own profile - but it carries the
            // rank the score earns against the whole table, not the display slot it lands in. A
            // gap between the last mocked rank and the player's own is ordinary "..." board
            // behaviour; ties keep the loop's convention that an equal score ranks the player
            // below it.
            if (!placed && rows.Count > 0)
            {
                int yourRank = 1;
                for (int i = 0; i < Scores.Length; i++)
                    if (Scores[i] >= yourScore) yourRank++;
                rows[rows.Count - 1] = new LeaderboardEntry(yourRank, YourName, yourScore, true);
            }

            return rows;
        }
    }
}
