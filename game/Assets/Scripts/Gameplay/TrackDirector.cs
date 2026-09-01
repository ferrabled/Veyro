using System.Collections.Generic;
using MotionRunner.Track;
using UnityEngine;

namespace MotionRunner.Gameplay
{
    /// Keeps the road ahead of the player full and recycles what has gone past (T-003).
    /// Replaces the T-002 RoadScroller placeholder.
    ///
    /// Chunks are laid end to end from a single running z cursor, so a gap between chunks is
    /// not something this class avoids - it is something it cannot express. Views are pooled
    /// per chunkId, which means a ten-minute run builds each definition's geometry once.
    public sealed class TrackDirector : MonoBehaviour
    {
        /// Spawn until the far end of the last chunk is at least this far ahead. With 20-32m
        /// chunks that is 5-7 chunks live, comfortably past the camera's far plane.
        public const float SpawnAheadDistance = 130f;

        /// Recycle once a chunk's end is this far behind the runner.
        public const float RecycleBehindZ = -24f;

        /// The first chunk starts behind the runner, not at its feet: the camera sits at
        /// z = -6.2 and its bottom edge looks at roughly z = -3.4, so a track starting at 0
        /// opens every run with a visible hole under the player (seen on device, 21 Aug).
        public const float FirstChunkZ = -8f;

        readonly List<ChunkView> _active = new List<ChunkView>();
        readonly Dictionary<string, Stack<ChunkView>> _pool = new Dictionary<string, Stack<ChunkView>>();

        TrackGenerator _generator;
        float _nextChunkZ;

        public IReadOnlyList<ChunkView> ActiveChunks => _active;
        public int Difficulty { get; private set; } = DifficultyCurve.MinDifficulty;
        public float Speed { get; private set; } = TrackMetrics.SpeedFor(DifficultyCurve.MinDifficulty);
        public int ChunksSpawned { get; private set; }
        public RunSeed Seed { get; private set; }

        public void BeginRun(RunSeed seed)
        {
            Seed = seed;

            for (int i = _active.Count - 1; i >= 0; i--) Release(_active[i]);
            _active.Clear();

            _generator = new TrackGenerator(ChunkLibrary.Greybox(), seed.CreateRandom());
            _nextChunkZ = FirstChunkZ;
            ChunksSpawned = 0;
            Difficulty = DifficultyCurve.MinDifficulty;
            Speed = TrackMetrics.SpeedFor(Difficulty);

            FillAhead();
        }

        /// Empties the road when a run is left without finishing it. Views go back to the pool
        /// they came from rather than being destroyed: they are this director's, not the run's,
        /// and a chunk's geometry is built once per chunkId per session by design.
        public void EndRun()
        {
            for (int i = _active.Count - 1; i >= 0; i--) Release(_active[i]);
            _active.Clear();
            _nextChunkZ = FirstChunkZ;
            ChunksSpawned = 0;
        }

        /// Moves the world toward the camera and tops the track up. Returns the distance the
        /// world travelled this frame, which is also the distance the player "ran".
        public float Advance(float deltaTime, float elapsedSeconds)
        {
            Difficulty = DifficultyCurve.At(elapsedSeconds);
            Speed = TrackMetrics.SpeedFor(Difficulty);

            float deltaZ = Speed * deltaTime;
            for (int i = 0; i < _active.Count; i++) _active[i].Translate(-deltaZ);
            _nextChunkZ -= deltaZ;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i].EndZ >= RecycleBehindZ) continue;
                Release(_active[i]);
                _active.RemoveAt(i);
            }

            FillAhead();
            return deltaZ;
        }

        void FillAhead()
        {
            while (_nextChunkZ < SpawnAheadDistance)
            {
                var definition = _generator.Next(Difficulty);
                var view = Rent(definition);
                view.PlaceAt(_nextChunkZ);
                _active.Add(view);
                _nextChunkZ += definition.Length;
                ChunksSpawned++;
            }
        }

        ChunkView Rent(ChunkDefinition definition)
        {
            if (_pool.TryGetValue(definition.ChunkId, out var stack) && stack.Count > 0)
            {
                var reused = stack.Pop();
                reused.PrepareForReuse();
                reused.SetVisible(true);
                return reused;
            }
            return ChunkView.Create(definition, transform);
        }

        void Release(ChunkView view)
        {
            view.SetVisible(false);
            if (!_pool.TryGetValue(view.Definition.ChunkId, out var stack))
            {
                stack = new Stack<ChunkView>();
                _pool[view.Definition.ChunkId] = stack;
            }
            stack.Push(view);
        }
    }
}
