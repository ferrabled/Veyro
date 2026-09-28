using System.Collections.Generic;

namespace MotionRunner.Progression
{
    /// What the profile's "my last runs" card says about the runs RunHistory keeps: the best of
    /// them and where it sits, the average, and the distance covered - plus the bar height each
    /// run gets on the card's chart. Engine-free, so the numbers on the card are tested numbers.
    public readonly struct RunDigest
    {
        /// The shortest bar a run gets, as a fraction of the tallest: a score of zero still
        /// draws a stub, so a run is never an empty gap in the chart.
        public const float MinBar = 0.06f;

        public readonly int Count;
        public readonly int Best;

        /// Index of the best run in the list (newest first), -1 with no runs. The newest wins a
        /// tie: a player who matches their best just now should see it on the run they just did.
        public readonly int BestIndex;

        /// Mean score, rounded to the nearest point.
        public readonly int Average;

        public readonly int TotalDistance;
        public readonly int TotalCoins;

        RunDigest(int count, int best, int bestIndex, int average, int distance, int coins)
        {
            Count = count;
            Best = best;
            BestIndex = bestIndex;
            Average = average;
            TotalDistance = distance;
            TotalCoins = coins;
        }

        public static RunDigest Of(IReadOnlyList<RunRecord> runs)
        {
            if (runs == null || runs.Count == 0) return new RunDigest(0, 0, -1, 0, 0, 0);

            int best = int.MinValue, bestIndex = -1;
            long sum = 0, distance = 0, coins = 0;
            for (int i = 0; i < runs.Count; i++)
            {
                var run = runs[i];
                if (run.Score > best)
                {
                    best = run.Score;
                    bestIndex = i;
                }
                sum += run.Score;
                distance += run.Distance;
                coins += run.Coins;
            }

            int average = (int)((sum + runs.Count / 2) / runs.Count);
            return new RunDigest(runs.Count, best, bestIndex, average, Clamp(distance), Clamp(coins));
        }

        /// A run's bar as a fraction of the chart's height, against the best score shown.
        public static float BarFraction(int score, int best)
        {
            if (best <= 0 || score <= 0) return MinBar;
            float fraction = score / (float)best;
            return fraction < MinBar ? MinBar : fraction > 1f ? 1f : fraction;
        }

        static int Clamp(long value) => value > int.MaxValue ? int.MaxValue : value < 0 ? 0 : (int)value;
    }
}
