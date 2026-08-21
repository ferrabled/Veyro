using System;

namespace MotionRunner.Track
{
    /// Axis-aligned box. Collision in this game is a handful of interval tests per frame
    /// against obstacles near the runner, so PhysX buys us nothing and costs determinism
    /// (CLAUDE.md rule 4) - see the note on TrackGeometry.
    public readonly struct Aabb
    {
        public readonly float CenterX, CenterY, CenterZ;
        public readonly float HalfX, HalfY, HalfZ;

        public Aabb(float centerX, float centerY, float centerZ, float halfX, float halfY, float halfZ)
        {
            CenterX = centerX;
            CenterY = centerY;
            CenterZ = centerZ;
            HalfX = halfX;
            HalfY = halfY;
            HalfZ = halfZ;
        }

        public float MinY => CenterY - HalfY;
        public float MaxY => CenterY + HalfY;
        public float MinZ => CenterZ - HalfZ;
        public float MaxZ => CenterZ + HalfZ;

        /// Strict overlap: boxes that exactly touch do not count as a hit, so a pixel-perfect
        /// squeeze past a block is a pass rather than a crash.
        public bool Intersects(in Aabb other) =>
            Math.Abs(CenterX - other.CenterX) < HalfX + other.HalfX &&
            Math.Abs(CenterY - other.CenterY) < HalfY + other.HalfY &&
            Math.Abs(CenterZ - other.CenterZ) < HalfZ + other.HalfZ;
    }

    /// Turns chunk-relative placements into world boxes. The runner always sits at z = 0 and
    /// the world slides past it (handoff 3.1), so a chunk's world position is one float.
    public static class TrackGeometry
    {
        public static Aabb Runner(float x, float y) =>
            new Aabb(x, y, 0f, TrackMetrics.RunnerHalfX, TrackMetrics.RunnerHalfY, TrackMetrics.RunnerHalfZ);

        public static Aabb Obstacle(in ObstaclePlacement placement, float chunkStartZ)
        {
            bool full = placement.Kind == ObstacleKind.FullBlock;
            float halfX = full ? TrackMetrics.FullBlockHalfX : TrackMetrics.LowBarrierHalfX;
            float halfY = full ? TrackMetrics.FullBlockHalfY : TrackMetrics.LowBarrierHalfY;
            float halfZ = full ? TrackMetrics.FullBlockHalfZ : TrackMetrics.LowBarrierHalfZ;
            return new Aabb(
                TrackMetrics.LaneCenterX(placement.Lane),
                halfY,                                  // resting on the deck
                chunkStartZ + placement.Z,
                halfX, halfY, halfZ);
        }

        public static Aabb Coin(in CoinPlacement placement, float chunkStartZ) =>
            new Aabb(
                TrackMetrics.LaneCenterX(placement.Lane),
                TrackMetrics.CoinCenterY,
                chunkStartZ + placement.Z,
                TrackMetrics.CoinHalf, TrackMetrics.CoinHalf, TrackMetrics.CoinHalf);

        /// Height of an obstacle's top edge - the number that decides whether a jump clears it.
        public static float TopOf(ObstacleKind kind) =>
            kind == ObstacleKind.FullBlock ? TrackMetrics.FullBlockHalfY * 2f : TrackMetrics.LowBarrierHalfY * 2f;
    }
}
