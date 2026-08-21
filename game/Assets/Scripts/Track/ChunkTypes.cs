using System;

namespace MotionRunner.Track
{
    /// One biome at launch (handoff 4.4); the field exists so content updates do not
    /// have to reshape the metadata.
    public enum Biome
    {
        Forest = 0
    }

    /// Shape of a chunk's start and end. A chunk may only follow one whose ExitType equals
    /// its EntryType, which is what keeps assembled runs geometrically clean (handoff 4.3).
    public enum ChunkEdge
    {
        Ground = 0,
        Bridge = 1
    }

    [Flags]
    public enum RunnerSkill
    {
        None = 0,
        Jump = 1 << 0,
        LaneChange = 1 << 1,
        Slide = 1 << 2
    }

    [Flags]
    public enum ChunkTag
    {
        None = 0,
        Start = 1 << 0,
        Straight = 1 << 1,
        Coins = 1 << 2,
        Bridge = 1 << 3,
        Tunnel = 1 << 4,
        Scenic = 1 << 5,
        RiskReward = 1 << 6,
        SetPiece = 1 << 7
    }

    public enum ObstacleKind
    {
        /// Knee height: jumpable, and jumping is the only way past it.
        LowBarrier = 0,

        /// Full height: cannot be jumped, the player has to steer out of the lane.
        FullBlock = 1
    }

    /// An obstacle inside a chunk. Z is metres from the chunk's start, lane is -1/0/+1.
    public readonly struct ObstaclePlacement
    {
        public readonly ObstacleKind Kind;
        public readonly int Lane;
        public readonly float Z;

        public ObstaclePlacement(ObstacleKind kind, int lane, float z)
        {
            Kind = kind;
            Lane = lane;
            Z = z;
        }
    }

    /// A collectible inside a chunk. Z is metres from the chunk's start, lane is -1/0/+1.
    public readonly struct CoinPlacement
    {
        public readonly int Lane;
        public readonly float Z;

        public CoinPlacement(int lane, float z)
        {
            Lane = lane;
            Z = z;
        }
    }
}
