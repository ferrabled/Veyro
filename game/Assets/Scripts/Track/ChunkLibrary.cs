using System.Collections.Generic;

namespace MotionRunner.Track
{
    /// The greybox chunk library (handoff 4.2): a small set of reusable segments the generator
    /// assembles into a run. Authored as code for now - see the note on ChunkDefinition.
    ///
    /// House rules, all enforced by ChunkLibraryTests:
    ///  - obstacle z stays inside [ChunkEdgeClearance, Length - ChunkEdgeClearance], which is
    ///    what makes reaction distance hold across chunk seams as well as inside a chunk;
    ///  - every cluster leaves at least one lane open;
    ///  - low barriers mean "jump", full blocks mean "steer" - and the chunk must say so in
    ///    RequiredSkills;
    ///  - at every difficulty there is more than one Ground -> Ground candidate, so the
    ///    generator never has to repeat a chunk back to back.
    public static class ChunkLibrary
    {
        /// Identifies *this* set of chunks, and is part of every run's seed.
        ///
        /// **Bump this whenever the library below changes** — adding, removing or re-tuning a chunk
        /// changes what a given seed generates, so a stale version would mean two players on the
        /// same Daily Run seed running different tracks. Bumping it is also the deliberate act of
        /// invalidating comparability with previously published daily runs, so do it knowingly.
        /// The application version is *not* part of the seed: a bugfix release must not fork the day.
        public const string ContentVersion = "greybox-1";

        static readonly ChunkDefinition[] Chunks = Build();

        /// Shared, immutable, allocated once: BeginRun must not churn the heap.
        public static IReadOnlyList<ChunkDefinition> Greybox() => Chunks;

        static ObstaclePlacement Low(int lane, float z) => new ObstaclePlacement(ObstacleKind.LowBarrier, lane, z);
        static ObstaclePlacement Block(int lane, float z) => new ObstaclePlacement(ObstacleKind.FullBlock, lane, z);
        static CoinPlacement Coin(int lane, float z) => new CoinPlacement(lane, z);

        static ChunkDefinition[] Build()
        {
            return new[]
            {
                // --- Ground -------------------------------------------------------------

                // Always the first chunk of a run: nothing to hit while the player finds the tilt.
                // Long on purpose - TrackDirector.FirstChunkZ places part of it behind the
                // runner, and what is left still has to cover the first couple of seconds.
                new ChunkDefinition(
                    "start_flat", Biome.Forest, 1, 1, 36f,
                    ChunkEdge.Ground, ChunkEdge.Ground,
                    RunnerSkill.None, ChunkTag.Start | ChunkTag.Straight, 1f,
                    coins: new[] { Coin(0, 14f), Coin(0, 20f), Coin(0, 26f) }),

                new ChunkDefinition(
                    "straight_coins", Biome.Forest, 1, 3, 24f,
                    ChunkEdge.Ground, ChunkEdge.Ground,
                    RunnerSkill.None, ChunkTag.Straight | ChunkTag.Coins, 1.3f,
                    coins: new[]
                    {
                        Coin(0, 6f), Coin(0, 8f), Coin(0, 10f),
                        Coin(1, 14f), Coin(1, 16f),
                        Coin(-1, 18f), Coin(-1, 20f)
                    }),

                new ChunkDefinition(
                    "hop_gate", Biome.Forest, 1, 4, 24f,
                    ChunkEdge.Ground, ChunkEdge.Ground,
                    RunnerSkill.Jump, ChunkTag.Straight, 1.1f,
                    obstacles: new[] { Low(-1, 12f), Low(0, 12f), Low(1, 12f) },
                    coins: new[] { Coin(0, 6f), Coin(0, 18f) }),

                new ChunkDefinition(
                    "weave_blocks", Biome.Forest, 2, 5, 28f,
                    ChunkEdge.Ground, ChunkEdge.Ground,
                    RunnerSkill.LaneChange, ChunkTag.Straight, 1.1f,
                    obstacles: new[] { Block(-1, 6f), Block(1, 14f), Block(0, 22f) },
                    coins: new[] { Coin(1, 6f), Coin(-1, 14f), Coin(1, 22f) }),

                new ChunkDefinition(
                    "gate_pair", Biome.Forest, 3, 6, 28f,
                    ChunkEdge.Ground, ChunkEdge.Ground,
                    RunnerSkill.LaneChange, ChunkTag.Straight | ChunkTag.RiskReward, 1f,
                    obstacles: new[] { Block(-1, 8f), Block(0, 8f), Block(0, 18f), Block(1, 18f) },
                    coins: new[] { Coin(1, 8f), Coin(-1, 18f) }),

                new ChunkDefinition(
                    "hop_run", Biome.Forest, 4, 8, 32f,
                    ChunkEdge.Ground, ChunkEdge.Ground,
                    RunnerSkill.Jump, ChunkTag.Straight, 0.9f,
                    obstacles: new[]
                    {
                        Low(-1, 8f), Low(0, 8f), Low(1, 8f),
                        Low(-1, 16f), Low(0, 16f), Low(1, 16f),
                        Low(-1, 24f), Low(0, 24f), Low(1, 24f)
                    },
                    coins: new[] { Coin(0, 12f), Coin(0, 20f) }),

                new ChunkDefinition(
                    "mixed_pattern", Biome.Forest, 5, 8, 32f,
                    ChunkEdge.Ground, ChunkEdge.Ground,
                    RunnerSkill.Jump | RunnerSkill.LaneChange, ChunkTag.SetPiece, 0.8f,
                    obstacles: new[]
                    {
                        Block(-1, 8f), Low(0, 8f), Low(1, 8f),
                        Low(-1, 16f), Low(0, 16f), Low(1, 16f),
                        Block(0, 24f), Block(1, 24f)
                    },
                    coins: new[] { Coin(1, 12f), Coin(-1, 24f) }),

                new ChunkDefinition(
                    "tunnel_dash", Biome.Forest, 3, 7, 26f,
                    ChunkEdge.Ground, ChunkEdge.Ground,
                    RunnerSkill.LaneChange, ChunkTag.Tunnel | ChunkTag.Scenic, 0.7f,
                    obstacles: new[] { Block(-1, 8f), Block(1, 16f) },
                    coins: new[] { Coin(1, 8f), Coin(0, 12f), Coin(-1, 16f) }),

                new ChunkDefinition(
                    "coin_dash", Biome.Forest, 1, 6, 24f,
                    ChunkEdge.Ground, ChunkEdge.Ground,
                    RunnerSkill.LaneChange, ChunkTag.Coins | ChunkTag.RiskReward, 0.6f,
                    obstacles: new[] { Block(0, 12f) },
                    coins: new[] { Coin(-1, 6f), Coin(-1, 9f), Coin(-1, 12f), Coin(-1, 15f), Coin(1, 18f) }),

                // --- Bridge -------------------------------------------------------------
                // bridge_on is the only way onto a bridge and bridge_off the only way off, so
                // the generator's entry/exit matching plus its bridge-run cap fully control
                // how long the player stays up there.

                new ChunkDefinition(
                    "bridge_on", Biome.Forest, 2, 8, 20f,
                    ChunkEdge.Ground, ChunkEdge.Bridge,
                    RunnerSkill.None, ChunkTag.Bridge | ChunkTag.Scenic, 0.7f,
                    coins: new[] { Coin(0, 8f), Coin(0, 12f) }),

                new ChunkDefinition(
                    "bridge_span", Biome.Forest, 2, 8, 28f,
                    ChunkEdge.Bridge, ChunkEdge.Bridge,
                    RunnerSkill.None, ChunkTag.Bridge | ChunkTag.Coins | ChunkTag.Scenic, 1f,
                    coins: new[] { Coin(0, 6f), Coin(0, 10f), Coin(0, 14f), Coin(0, 18f), Coin(0, 22f) }),

                new ChunkDefinition(
                    "bridge_gate", Biome.Forest, 4, 8, 28f,
                    ChunkEdge.Bridge, ChunkEdge.Bridge,
                    RunnerSkill.LaneChange, ChunkTag.Bridge | ChunkTag.RiskReward, 0.8f,
                    obstacles: new[] { Block(0, 8f), Block(-1, 18f) },
                    coins: new[] { Coin(1, 8f), Coin(1, 18f) }),

                new ChunkDefinition(
                    "bridge_off", Biome.Forest, 2, 8, 20f,
                    ChunkEdge.Bridge, ChunkEdge.Ground,
                    RunnerSkill.None, ChunkTag.Bridge | ChunkTag.Scenic, 1.2f,
                    coins: new[] { Coin(0, 8f), Coin(0, 12f) })
            };
        }
    }
}
