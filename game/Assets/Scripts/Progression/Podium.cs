using System.Collections.Generic;

namespace MotionRunner.Progression
{
    /// How the profile's leaderboard splits a board into a podium and the list under it. The
    /// first three rows stand on the podium - in board order, so tied ranks keep the server's
    /// order - and everything after them is the list. Engine-free, so "who stands where" is
    /// tested rather than eyeballed on a board that happens to have three rows today.
    public static class Podium
    {
        public const int Places = 3;

        /// Left to right on screen, as indices into the podium: second, first, third - the
        /// shape every podium has, with the winner raised in the middle.
        public static readonly int[] DisplayOrder = { 1, 0, 2 };

        /// Relative block heights for first, second and third place.
        public static readonly float[] BlockHeights = { 1f, 0.74f, 0.56f };

        /// The rows on the podium - always Places long, with null for a place nobody holds yet
        /// (a fresh board) - and the rows below it, in order.
        public static LeaderboardEntry?[] Split(IReadOnlyList<LeaderboardEntry> rows, out List<LeaderboardEntry> rest)
        {
            var podium = new LeaderboardEntry?[Places];
            rest = new List<LeaderboardEntry>();
            if (rows == null) return podium;

            for (int i = 0; i < rows.Count; i++)
            {
                if (i < Places) podium[i] = rows[i];
                else rest.Add(rows[i]);
            }
            return podium;
        }
    }
}
