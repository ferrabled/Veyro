using System.Collections.Generic;
using MotionRunner.Track;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// The library is the content, so these tests are the content review: every chunk has to be
    /// well-formed, and the set as a whole has to leave the generator a way forward at every
    /// difficulty and off every edge.
    public sealed class ChunkLibraryTests
    {
        static IReadOnlyList<ChunkDefinition> Library => ChunkLibrary.Greybox();

        [Test]
        public void EveryChunk_IsWellFormed()
        {
            foreach (var chunk in Library)
            {
                string problem = chunk.Validate();
                Assert.IsNull(problem, problem);
            }
        }

        [Test]
        public void ChunkIds_AreUnique()
        {
            var seen = new HashSet<string>();
            foreach (var chunk in Library)
                Assert.IsTrue(seen.Add(chunk.ChunkId), "duplicate chunkId " + chunk.ChunkId);
        }

        [Test]
        public void Library_HasExactlyOneStartChunk()
        {
            int starts = 0;
            foreach (var chunk in Library)
            {
                if (!chunk.HasTag(ChunkTag.Start)) continue;
                starts++;
                Assert.AreEqual(ChunkEdge.Ground, chunk.EntryType, "the opener must start on the ground");
                Assert.AreEqual(0, chunk.Obstacles.Length, "the opener must be obstacle-free");
            }
            Assert.AreEqual(1, starts);
        }

        [Test]
        public void EveryDifficulty_HasAtLeastTwoGroundCandidates()
        {
            for (int difficulty = DifficultyCurve.MinDifficulty; difficulty <= DifficultyCurve.MaxDifficulty; difficulty++)
            {
                int candidates = 0;
                foreach (var chunk in Library)
                {
                    if (chunk.HasTag(ChunkTag.Start)) continue;
                    if (chunk.EntryType != ChunkEdge.Ground || chunk.ExitType != ChunkEdge.Ground) continue;
                    if (chunk.AcceptsDifficulty(difficulty)) candidates++;
                }
                Assert.GreaterOrEqual(candidates, 2,
                    "difficulty " + difficulty + " needs two ground candidates so the generator never repeats a chunk");
            }
        }

        [Test]
        public void EveryEdge_CanBeEnteredAndLeft()
        {
            foreach (ChunkEdge edge in System.Enum.GetValues(typeof(ChunkEdge)))
            {
                bool entered = false;
                bool left = false;
                foreach (var chunk in Library)
                {
                    if (chunk.ExitType == edge) entered = true;
                    if (chunk.EntryType == edge) left = true;
                }
                Assert.IsTrue(entered, "no chunk ever produces edge " + edge);
                Assert.IsTrue(left, "no chunk accepts edge " + edge + " - it is a dead end");
            }
        }

        [Test]
        public void ABridgeCanAlwaysBeLeftAtAnyDifficultyItCanBeEnteredAt()
        {
            for (int difficulty = DifficultyCurve.MinDifficulty; difficulty <= DifficultyCurve.MaxDifficulty; difficulty++)
            {
                bool reachable = false;
                for (int earlier = DifficultyCurve.MinDifficulty; earlier <= difficulty; earlier++)
                    foreach (var chunk in Library)
                        if (chunk.EntryType == ChunkEdge.Ground && chunk.ExitType == ChunkEdge.Bridge &&
                            chunk.AcceptsDifficulty(earlier))
                            reachable = true;

                if (!reachable) continue;

                bool escapable = false;
                foreach (var chunk in Library)
                    if (chunk.EntryType == ChunkEdge.Bridge && chunk.ExitType == ChunkEdge.Ground &&
                        chunk.AcceptsDifficulty(difficulty))
                        escapable = true;

                Assert.IsTrue(escapable,
                    "a bridge is reachable by difficulty " + difficulty + " but nothing leads back down there");
            }
        }

        [Test]
        public void EveryChunkWithCoins_KeepsThemReachable()
        {
            // Covered by Validate(), asserted separately because it is the rule most likely to
            // be broken while hand-tuning coin lines.
            foreach (var chunk in Library)
            {
                if (chunk.Coins.Length == 0) continue;
                Assert.IsNull(chunk.Validate(), chunk.ChunkId + " has an unreachable coin");
            }
        }

        // --- the validator itself -------------------------------------------------------

        static ChunkDefinition Fixture(
            RunnerSkill skills = RunnerSkill.Jump | RunnerSkill.LaneChange,
            ObstaclePlacement[] obstacles = null,
            CoinPlacement[] coins = null,
            float length = 32f)
        {
            return new ChunkDefinition(
                "fixture", Biome.Forest, 1, 8, length,
                ChunkEdge.Ground, ChunkEdge.Ground,
                skills, ChunkTag.Straight, 1f, obstacles, coins);
        }

        [Test]
        public void Validator_RejectsAClusterThatBlocksAllThreeLanes()
        {
            var chunk = Fixture(obstacles: new[]
            {
                new ObstaclePlacement(ObstacleKind.FullBlock, -1, 10f),
                new ObstaclePlacement(ObstacleKind.FullBlock, 0, 10f),
                new ObstaclePlacement(ObstacleKind.FullBlock, 1, 10f)
            });
            StringAssert.Contains("impossible", chunk.Validate());
        }

        [Test]
        public void Validator_RejectsClustersWithoutReactionDistance()
        {
            var chunk = Fixture(obstacles: new[]
            {
                new ObstaclePlacement(ObstacleKind.FullBlock, 0, 10f),
                new ObstaclePlacement(ObstacleKind.FullBlock, 0, 13f)
            });
            StringAssert.Contains("reaction distance", chunk.Validate());
        }

        [Test]
        public void Validator_RejectsObstaclesInsideTheEdgeClearance()
        {
            var atEntry = Fixture(obstacles: new[] { new ObstaclePlacement(ObstacleKind.FullBlock, 0, 1f) });
            StringAssert.Contains("entry clearance", atEntry.Validate());

            var atExit = Fixture(obstacles: new[] { new ObstaclePlacement(ObstacleKind.FullBlock, 0, 31f) });
            StringAssert.Contains("exit clearance", atExit.Validate());
        }

        [Test]
        public void Validator_RejectsUndeclaredSkills()
        {
            var needsJump = Fixture(
                skills: RunnerSkill.LaneChange,
                obstacles: new[] { new ObstaclePlacement(ObstacleKind.LowBarrier, 0, 10f) });
            StringAssert.Contains("RunnerSkill.Jump", needsJump.Validate());

            var needsSteering = Fixture(
                skills: RunnerSkill.Jump,
                obstacles: new[] { new ObstaclePlacement(ObstacleKind.FullBlock, 0, 10f) });
            StringAssert.Contains("RunnerSkill.LaneChange", needsSteering.Validate());
        }

        [Test]
        public void Validator_RejectsACoinBuriedInAnObstacle()
        {
            var chunk = Fixture(
                obstacles: new[] { new ObstaclePlacement(ObstacleKind.FullBlock, 0, 10f) },
                coins: new[] { new CoinPlacement(0, 10.2f) });
            StringAssert.Contains("unreachable", chunk.Validate());
        }

        [Test]
        public void Validator_RejectsStackedObstaclesInOneLane()
        {
            var chunk = Fixture(obstacles: new[]
            {
                new ObstaclePlacement(ObstacleKind.FullBlock, 0, 10f),
                new ObstaclePlacement(ObstacleKind.LowBarrier, 0, 10.2f)
            });
            StringAssert.Contains("stacks two obstacles", chunk.Validate());
        }

        [Test]
        public void Validator_RejectsABridgeEdgeWithoutTheBridgeTag()
        {
            var chunk = new ChunkDefinition(
                "untagged_bridge", Biome.Forest, 1, 8, 24f,
                ChunkEdge.Ground, ChunkEdge.Bridge,
                RunnerSkill.None, ChunkTag.Straight, 1f);
            StringAssert.Contains("Bridge tag", chunk.Validate());
        }

        [Test]
        public void Validator_RejectsBadDifficultyRanges()
        {
            var inverted = new ChunkDefinition(
                "inverted", Biome.Forest, 5, 2, 24f,
                ChunkEdge.Ground, ChunkEdge.Ground, RunnerSkill.None, ChunkTag.Straight, 1f);
            StringAssert.Contains("difficultyMin > difficultyMax", inverted.Validate());

            var outOfRange = new ChunkDefinition(
                "out_of_range", Biome.Forest, 0, 99, 24f,
                ChunkEdge.Ground, ChunkEdge.Ground, RunnerSkill.None, ChunkTag.Straight, 1f);
            StringAssert.Contains("outside", outOfRange.Validate());
        }

        [Test]
        public void Clusters_GroupObstaclesThatShareAZ()
        {
            var chunk = Fixture(obstacles: new[]
            {
                new ObstaclePlacement(ObstacleKind.LowBarrier, 1, 20f),
                new ObstaclePlacement(ObstacleKind.FullBlock, -1, 10f),
                new ObstaclePlacement(ObstacleKind.LowBarrier, 0, 10.3f)
            });

            Assert.AreEqual(2, chunk.Clusters.Length);
            Assert.AreEqual(10f, chunk.Clusters[0].Z, 0.001f);
            Assert.AreEqual(ObstacleCluster.LaneBit(-1), chunk.Clusters[0].FullBlockLanes);
            Assert.AreEqual(ObstacleCluster.LaneBit(0), chunk.Clusters[0].LowBarrierLanes);
            Assert.AreEqual(20f, chunk.Clusters[1].Z, 0.001f);
        }
    }
}
