using System;
using MotionRunner.Track;
using UnityEngine;

namespace MotionRunner.Art
{
    /// Unity authoring boundary. The generator and collision model remain engine-free.
    [CreateAssetMenu(menuName = "Veyro/Track chunk")]
    public sealed class TrackChunkAsset : ScriptableObject
    {
        [Serializable] public struct Obstacle
        {
            public ObstacleKind Kind;
            public int Lane;
            public float Z;
        }
        [Serializable] public struct Coin { public int Lane; public float Z; }

        public string ChunkId;
        public Biome Biome;
        public int DifficultyMin, DifficultyMax;
        public float Length;
        public ChunkEdge EntryType, ExitType;
        public RunnerSkill RequiredSkills;
        public ChunkTag Tags;
        public float Rarity;
        public Obstacle[] Obstacles;
        public Coin[] Coins;
        [Tooltip("Decorative variation only; never consumes the gameplay PRNG.")]
        public int SceneryVariant;

        public ChunkDefinition ToDefinition()
        {
            var obstacles = new ObstaclePlacement[Obstacles.Length];
            for (int i = 0; i < obstacles.Length; i++)
                obstacles[i] = new ObstaclePlacement(Obstacles[i].Kind, Obstacles[i].Lane, Obstacles[i].Z);
            var coins = new CoinPlacement[Coins.Length];
            for (int i = 0; i < coins.Length; i++) coins[i] = new CoinPlacement(Coins[i].Lane, Coins[i].Z);
            return new ChunkDefinition(ChunkId, Biome, DifficultyMin, DifficultyMax, Length,
                EntryType, ExitType, RequiredSkills, Tags, Rarity, obstacles, coins);
        }

        public void Import(ChunkDefinition source, int variant)
        {
            ChunkId = source.ChunkId; Biome = source.Biome;
            DifficultyMin = source.DifficultyMin; DifficultyMax = source.DifficultyMax;
            Length = source.Length; EntryType = source.EntryType; ExitType = source.ExitType;
            RequiredSkills = source.RequiredSkills; Tags = source.Tags; Rarity = source.Rarity;
            SceneryVariant = variant;
            Obstacles = new Obstacle[source.Obstacles.Length];
            for (int i = 0; i < Obstacles.Length; i++) Obstacles[i] = new Obstacle {
                Kind = source.Obstacles[i].Kind, Lane = source.Obstacles[i].Lane, Z = source.Obstacles[i].Z };
            Coins = new Coin[source.Coins.Length];
            for (int i = 0; i < Coins.Length; i++) Coins[i] = new Coin { Lane = source.Coins[i].Lane, Z = source.Coins[i].Z };
        }
    }
}
