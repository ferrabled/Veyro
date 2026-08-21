using System.Collections.Generic;
using MotionRunner.Track;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// T-003's acceptance criterion (same seed produces the same sequence) plus the generator
    /// constraints that keep an assembled run playable.
    public sealed class TrackGeneratorTests
    {
        const int LongRun = 2000;

        /// Difficulty as it would rise over a real run: one chunk is roughly two seconds.
        static int DifficultyForIndex(int index) => DifficultyCurve.At(index * 2f);

        static TrackGenerator GeneratorFor(int seed, string worldId = RunSeed.DefaultWorldId, string version = "0.1.0") =>
            new TrackGenerator(ChunkLibrary.Greybox(), new RunSeed(seed, version, worldId).CreateRandom());

        static List<ChunkDefinition> Run(int seed, int count, string worldId = RunSeed.DefaultWorldId, string version = "0.1.0")
        {
            var generator = GeneratorFor(seed, worldId, version);
            var chunks = new List<ChunkDefinition>(count);
            for (int i = 0; i < count; i++) chunks.Add(generator.Next(DifficultyForIndex(i)));
            return chunks;
        }

        static void AssertSequencesDiffer(List<ChunkDefinition> a, List<ChunkDefinition> b, string because)
        {
            for (int i = 0; i < a.Count; i++)
                if (a[i].ChunkId != b[i].ChunkId) return;
            Assert.Fail(because);
        }

        [Test]
        public void SameSeed_ProducesAnIdenticalSequence()
        {
            var first = Run(20260821, LongRun);
            var second = Run(20260821, LongRun);

            Assert.AreEqual(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++)
                Assert.AreEqual(first[i].ChunkId, second[i].ChunkId, "sequences diverge at chunk " + i);
        }

        [Test]
        public void SameSeed_ProducesIdenticalSequences_ForManySeeds()
        {
            for (int seed = -5; seed <= 5; seed++)
            {
                var first = Run(seed, 200);
                var second = Run(seed, 200);
                for (int i = 0; i < first.Count; i++)
                    Assert.AreEqual(first[i].ChunkId, second[i].ChunkId, "seed " + seed + " diverges at chunk " + i);
            }
        }

        [Test]
        public void DifferentSeed_ProducesADifferentSequence()
        {
            AssertSequencesDiffer(Run(1, 200), Run(2, 200), "two different seeds produced the same 200 chunks");
        }

        [Test]
        public void DifferentWorldId_ProducesADifferentSequence()
        {
            AssertSequencesDiffer(Run(1, 200), Run(1, 200, worldId: "alps"),
                "two different worlds produced the same 200 chunks");
        }

        [Test]
        public void DifferentGameVersion_ProducesADifferentSequence()
        {
            AssertSequencesDiffer(Run(1, 200), Run(1, 200, version: "9.9.9"),
                "two different game versions produced the same 200 chunks");
        }

        [Test]
        public void FirstChunk_IsAlwaysTheOpener()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var chunks = Run(seed, 3);
                Assert.IsTrue(chunks[0].HasTag(ChunkTag.Start), "seed " + seed + " did not open with the start chunk");
            }
        }

        [Test]
        public void TheOpener_IsNeverEmittedTwice()
        {
            var chunks = Run(7, LongRun);
            for (int i = 1; i < chunks.Count; i++)
                Assert.IsFalse(chunks[i].HasTag(ChunkTag.Start), "the opener came back at chunk " + i);
        }

        [Test]
        public void EdgesAlwaysConnect()
        {
            var chunks = Run(31337, LongRun);
            Assert.AreEqual(ChunkEdge.Ground, chunks[0].EntryType);
            for (int i = 1; i < chunks.Count; i++)
                Assert.AreEqual(chunks[i - 1].ExitType, chunks[i].EntryType,
                    "chunk " + i + " (" + chunks[i].ChunkId + ") does not connect to " + chunks[i - 1].ChunkId);
        }

        [Test]
        public void EveryChunk_FitsTheRequestedDifficulty()
        {
            var generator = GeneratorFor(555);
            for (int i = 0; i < LongRun; i++)
            {
                int difficulty = DifficultyForIndex(i);
                var chunk = generator.Next(difficulty);
                Assert.IsTrue(chunk.AcceptsDifficulty(difficulty),
                    chunk.ChunkId + " (" + chunk.DifficultyMin + ".." + chunk.DifficultyMax +
                    ") was emitted at difficulty " + difficulty);
            }
            Assert.AreEqual(0, generator.RelaxedCount,
                "the generator had to widen the difficulty window - the library has a coverage hole");
        }

        [Test]
        public void NoChunk_IsEmittedTwiceInARow()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                var chunks = Run(seed, 300);
                for (int i = 1; i < chunks.Count; i++)
                    Assert.AreNotEqual(chunks[i - 1].ChunkId, chunks[i].ChunkId,
                        "seed " + seed + " repeated " + chunks[i].ChunkId + " at chunk " + i);
            }
        }

        [Test]
        public void BridgeRuns_AreBounded()
        {
            var chunks = Run(8675309, LongRun);
            int run = 0;
            for (int i = 0; i < chunks.Count; i++)
            {
                run = chunks[i].ExitType == ChunkEdge.Bridge ? run + 1 : 0;
                Assert.LessOrEqual(run, TrackGenerator.MaxBridgeRun, "bridge run got to " + run + " at chunk " + i);
            }
        }

        [Test]
        public void EveryDifficultyIsExercised_AndTheLibraryIsUsedBroadly()
        {
            var seen = new HashSet<string>();
            foreach (var chunk in Run(2026, LongRun)) seen.Add(chunk.ChunkId);

            // A generator that only ever picks two chunks would pass every other test here.
            Assert.GreaterOrEqual(seen.Count, ChunkLibrary.Greybox().Count - 1,
                "only " + seen.Count + " of " + ChunkLibrary.Greybox().Count + " chunks were ever used");
        }

        [Test]
        public void ObstacleClusters_KeepReactionDistanceAcrossChunkSeams()
        {
            // The per-chunk spacing rule is checked by the validator; this walks a whole
            // assembled run, where the risk is a chunk ending in an obstacle and the next one
            // starting with another (handoff 3.2: no impossible sequences).
            var chunks = Run(4711, LongRun);
            float chunkStart = 0f;
            float previousClusterZ = float.NegativeInfinity;
            string previousChunkId = "start";

            foreach (var chunk in chunks)
            {
                foreach (var cluster in chunk.Clusters)
                {
                    float absoluteZ = chunkStart + cluster.Z;
                    float gap = absoluteZ - previousClusterZ;
                    Assert.GreaterOrEqual(gap, TrackMetrics.MinClusterSpacing - 0.001f,
                        "only " + gap + "m between an obstacle in " + previousChunkId + " and one in " + chunk.ChunkId);
                    previousClusterZ = absoluteZ;
                    previousChunkId = chunk.ChunkId;
                }
                chunkStart += chunk.Length;
            }
        }

        [Test]
        public void EveryEmittedChunk_LeavesALaneOpen()
        {
            foreach (var chunk in Run(6060842, LongRun))
                foreach (var cluster in chunk.Clusters)
                    Assert.AreNotEqual(ObstacleCluster.AllLanes, cluster.FullBlockLanes,
                        chunk.ChunkId + " has a cluster with no way through");
        }

        [Test]
        public void ALongRun_NeverThrows()
        {
            // A ten-minute run at 12-20 m/s crosses roughly 300-400 chunks; 20k is deep enough
            // that a slow dead end in the library would have shown up.
            var generator = GeneratorFor(99999);
            for (int i = 0; i < 20000; i++) generator.Next(DifficultyForIndex(i));
            Assert.AreEqual(20000, generator.EmittedCount);
        }
    }
}
