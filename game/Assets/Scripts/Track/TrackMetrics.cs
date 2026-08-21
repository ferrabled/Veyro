namespace MotionRunner.Track
{
    /// Every world-space number that the track, the runner and the collision checks agree on.
    /// Deliberately engine-free (see MotionRunner.Track.asmdef) so generation and collision are
    /// testable headlessly and behave identically on Mono in the editor and IL2CPP on the phone.
    public static class TrackMetrics
    {
        public const int LaneCount = 3;
        public const float LaneWidth = 1.6f;
        public const float RoadHalfWidth = LaneWidth * LaneCount * 0.5f;   // 2.4
        public const float DeckThickness = 0.2f;

        // Lane index is -1 (left), 0 (centre) or +1 (right).
        public const int MinLane = -1;
        public const int MaxLane = 1;

        public static float LaneCenterX(int lane) => lane * LaneWidth;

        // Runner: a capsule scaled (0.6, 0.5, 0.6) - 0.6 wide, 1.0 tall, resting at y = 0.5.
        public const float RunnerHalfX = 0.3f;
        public const float RunnerHalfY = 0.5f;
        public const float RunnerHalfZ = 0.3f;
        public const float RunnerRestY = 0.5f;

        // Low barrier: top at y = 0.5, so a jump (apex about 1.78) clears it comfortably.
        public const float LowBarrierHalfX = 0.62f;
        public const float LowBarrierHalfY = 0.25f;
        public const float LowBarrierHalfZ = 0.35f;

        // Full block: top at y = 1.8, unjumpable by design - the player must steer away.
        public const float FullBlockHalfX = 0.62f;
        public const float FullBlockHalfY = 0.9f;
        public const float FullBlockHalfZ = 0.45f;

        public const float CoinHalf = 0.4f;
        public const float CoinCenterY = 0.95f;

        // World speed (the world moves, the runner does not - handoff 3.1).
        public const float BaseSpeed = 12f;
        public const float SpeedPerDifficulty = 1.1f;

        public static float SpeedFor(int difficulty)
        {
            if (difficulty < DifficultyCurve.MinDifficulty) difficulty = DifficultyCurve.MinDifficulty;
            if (difficulty > DifficultyCurve.MaxDifficulty) difficulty = DifficultyCurve.MaxDifficulty;
            return BaseSpeed + (difficulty - DifficultyCurve.MinDifficulty) * SpeedPerDifficulty;
        }

        // Playability constraints that the chunk validator and the generator tests enforce.

        /// Obstacles within this z distance of each other count as one cluster (a pattern).
        public const float ClusterEpsilon = 0.6f;

        /// Minimum metres between two obstacle clusters, inside a chunk and across a seam.
        public const float MinClusterSpacing = 6f;

        /// No obstacle may sit within this distance of a chunk's start or end, which is what
        /// makes MinClusterSpacing hold across chunk boundaries too (3 + 3 = 6).
        public const float ChunkEdgeClearance = MinClusterSpacing * 0.5f;
    }
}
