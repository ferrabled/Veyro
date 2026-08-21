using System;

namespace MotionRunner.Track
{
    /// Obstacles standing at (roughly) the same z, i.e. one thing the player has to read and
    /// solve. Lanes are stored as a 3-bit mask, bit (lane + 1).
    public readonly struct ObstacleCluster
    {
        public readonly float Z;
        public readonly int FullBlockLanes;
        public readonly int LowBarrierLanes;

        public ObstacleCluster(float z, int fullBlockLanes, int lowBarrierLanes)
        {
            Z = z;
            FullBlockLanes = fullBlockLanes;
            LowBarrierLanes = lowBarrierLanes;
        }

        public int OccupiedLanes => FullBlockLanes | LowBarrierLanes;

        public const int AllLanes = 0b111;

        public static int LaneBit(int lane) => 1 << (lane - TrackMetrics.MinLane);
    }

    /// Metadata for one hand-authored track segment (handoff 4.3).
    ///
    /// Plain C# data for the greybox phase, on purpose: the library is code, so it is
    /// diffable, testable headlessly and free of scene/asset merge hazards (CLAUDE.md rule 1).
    /// It becomes ScriptableObjects in T-006, when chunks carry real visual content.
    public sealed class ChunkDefinition
    {
        static readonly ObstaclePlacement[] NoObstacles = new ObstaclePlacement[0];
        static readonly CoinPlacement[] NoCoins = new CoinPlacement[0];

        public string ChunkId { get; }
        public Biome Biome { get; }
        public int DifficultyMin { get; }
        public int DifficultyMax { get; }
        public float Length { get; }
        public ChunkEdge EntryType { get; }
        public ChunkEdge ExitType { get; }
        public RunnerSkill RequiredSkills { get; }
        public ChunkTag Tags { get; }

        /// Selection weight. 1 = common, below 1 = rare set-piece (handoff 4.3 "rarity").
        public float Rarity { get; }

        public ObstaclePlacement[] Obstacles { get; }
        public CoinPlacement[] Coins { get; }

        /// Obstacles grouped into the patterns the player actually reads. Built once, at
        /// construction, so neither generation nor the per-frame path allocates.
        public ObstacleCluster[] Clusters { get; }

        public ChunkDefinition(
            string chunkId,
            Biome biome,
            int difficultyMin,
            int difficultyMax,
            float length,
            ChunkEdge entryType,
            ChunkEdge exitType,
            RunnerSkill requiredSkills,
            ChunkTag tags,
            float rarity,
            ObstaclePlacement[] obstacles = null,
            CoinPlacement[] coins = null)
        {
            ChunkId = chunkId;
            Biome = biome;
            DifficultyMin = difficultyMin;
            DifficultyMax = difficultyMax;
            Length = length;
            EntryType = entryType;
            ExitType = exitType;
            RequiredSkills = requiredSkills;
            Tags = tags;
            Rarity = rarity;
            Obstacles = obstacles ?? NoObstacles;
            Coins = coins ?? NoCoins;
            Clusters = BuildClusters(Obstacles);
        }

        public bool AcceptsDifficulty(int difficulty) =>
            difficulty >= DifficultyMin && difficulty <= DifficultyMax;

        public bool HasTag(ChunkTag tag) => (Tags & tag) != 0;

        public bool HasObstacles => Clusters.Length > 0;

        /// z of the first / last obstacle cluster, used for the cross-seam spacing check.
        public float FirstClusterZ => Clusters.Length > 0 ? Clusters[0].Z : float.NaN;
        public float LastClusterZ => Clusters.Length > 0 ? Clusters[Clusters.Length - 1].Z : float.NaN;

        static ObstacleCluster[] BuildClusters(ObstaclePlacement[] obstacles)
        {
            if (obstacles.Length == 0) return new ObstacleCluster[0];

            var sorted = new ObstaclePlacement[obstacles.Length];
            Array.Copy(obstacles, sorted, obstacles.Length);
            // Insertion sort: authored chunks hold a handful of obstacles, and a stable,
            // dependency-free sort keeps this assembly engine- and BCL-quirk-free.
            for (int i = 1; i < sorted.Length; i++)
            {
                var item = sorted[i];
                int j = i - 1;
                while (j >= 0 && sorted[j].Z > item.Z)
                {
                    sorted[j + 1] = sorted[j];
                    j--;
                }
                sorted[j + 1] = item;
            }

            var clusters = new ObstacleCluster[sorted.Length];
            int count = 0;
            int index = 0;
            while (index < sorted.Length)
            {
                float startZ = sorted[index].Z;
                int full = 0;
                int low = 0;
                while (index < sorted.Length && sorted[index].Z - startZ <= TrackMetrics.ClusterEpsilon)
                {
                    var o = sorted[index];
                    if (o.Lane >= TrackMetrics.MinLane && o.Lane <= TrackMetrics.MaxLane)
                    {
                        int bit = ObstacleCluster.LaneBit(o.Lane);
                        if (o.Kind == ObstacleKind.FullBlock) full |= bit;
                        else low |= bit;
                    }
                    index++;
                }
                clusters[count++] = new ObstacleCluster(startZ, full, low);
            }

            if (count == clusters.Length) return clusters;
            var trimmed = new ObstacleCluster[count];
            Array.Copy(clusters, trimmed, count);
            return trimmed;
        }

        /// Returns null when the chunk is well-formed, otherwise the reason it is not.
        /// Every chunk in ChunkLibrary is validated by an EditMode test, so an unplayable
        /// or badly connected chunk cannot reach a build.
        public string Validate()
        {
            if (string.IsNullOrEmpty(ChunkId)) return "chunkId is empty";
            if (DifficultyMin < DifficultyCurve.MinDifficulty || DifficultyMax > DifficultyCurve.MaxDifficulty)
                return ChunkId + ": difficulty range " + DifficultyMin + ".." + DifficultyMax + " is outside 1.." + DifficultyCurve.MaxDifficulty;
            if (DifficultyMin > DifficultyMax)
                return ChunkId + ": difficultyMin > difficultyMax";
            if (Length < TrackMetrics.MinClusterSpacing * 2f)
                return ChunkId + ": length " + Length + " is shorter than two obstacle clearances";
            if (Rarity <= 0f)
                return ChunkId + ": rarity must be positive";
            if ((EntryType == ChunkEdge.Bridge || ExitType == ChunkEdge.Bridge) && !HasTag(ChunkTag.Bridge))
                return ChunkId + ": has a bridge edge but no Bridge tag";

            for (int i = 0; i < Obstacles.Length; i++)
            {
                var o = Obstacles[i];
                if (o.Lane < TrackMetrics.MinLane || o.Lane > TrackMetrics.MaxLane)
                    return ChunkId + ": obstacle " + i + " sits in lane " + o.Lane;
                string zError = ValidateZ("obstacle " + i, o.Z);
                if (zError != null) return zError;
            }

            for (int i = 0; i < Coins.Length; i++)
            {
                var c = Coins[i];
                if (c.Lane < TrackMetrics.MinLane || c.Lane > TrackMetrics.MaxLane)
                    return ChunkId + ": coin " + i + " sits in lane " + c.Lane;
                if (c.Z < 0f || c.Z > Length)
                    return ChunkId + ": coin " + i + " at z=" + c.Z + " is outside the chunk";
                string blocker = FindObstacleCovering(c);
                if (blocker != null)
                    return ChunkId + ": coin " + i + " at z=" + c.Z + " is unreachable, buried in " + blocker;
            }

            if ((RequiredSkills & RunnerSkill.Jump) == 0 && HasAnyLowBarrier())
                return ChunkId + ": has a low barrier but does not declare RunnerSkill.Jump";
            if ((RequiredSkills & RunnerSkill.LaneChange) == 0 && HasAnyFullBlock())
                return ChunkId + ": has a full block but does not declare RunnerSkill.LaneChange";

            for (int i = 0; i < Clusters.Length; i++)
            {
                var cluster = Clusters[i];
                if ((cluster.FullBlockLanes & cluster.LowBarrierLanes) != 0)
                    return ChunkId + ": cluster at z=" + cluster.Z + " stacks two obstacles in one lane";
                if (cluster.FullBlockLanes == ObstacleCluster.AllLanes)
                    return ChunkId + ": cluster at z=" + cluster.Z + " blocks all three lanes - impossible";
                if (i > 0 && cluster.Z - Clusters[i - 1].Z < TrackMetrics.MinClusterSpacing)
                    return ChunkId + ": clusters at z=" + Clusters[i - 1].Z + " and z=" + cluster.Z +
                           " are closer than " + TrackMetrics.MinClusterSpacing + "m of reaction distance";
            }

            return null;
        }

        string ValidateZ(string what, float z)
        {
            if (z < TrackMetrics.ChunkEdgeClearance)
                return ChunkId + ": " + what + " at z=" + z + " is inside the entry clearance";
            if (z > Length - TrackMetrics.ChunkEdgeClearance)
                return ChunkId + ": " + what + " at z=" + z + " is inside the exit clearance";
            return null;
        }

        bool HasAnyLowBarrier()
        {
            for (int i = 0; i < Clusters.Length; i++)
                if (Clusters[i].LowBarrierLanes != 0) return true;
            return false;
        }

        bool HasAnyFullBlock()
        {
            for (int i = 0; i < Clusters.Length; i++)
                if (Clusters[i].FullBlockLanes != 0) return true;
            return false;
        }

        string FindObstacleCovering(CoinPlacement coin)
        {
            for (int i = 0; i < Obstacles.Length; i++)
            {
                var o = Obstacles[i];
                if (o.Lane != coin.Lane) continue;
                float halfZ = o.Kind == ObstacleKind.FullBlock
                    ? TrackMetrics.FullBlockHalfZ
                    : TrackMetrics.LowBarrierHalfZ;
                if (Math.Abs(o.Z - coin.Z) < halfZ + TrackMetrics.CoinHalf)
                    return o.Kind + " at z=" + o.Z;
            }
            return null;
        }

        public override string ToString() => ChunkId;
    }
}
