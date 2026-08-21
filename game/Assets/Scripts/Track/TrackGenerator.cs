using System;
using System.Collections.Generic;

namespace MotionRunner.Track
{
    /// Assembles chunks into a run (handoff 4.2). Deterministic by construction: the only
    /// entropy is the injected IRandomSource, and candidates are always scanned in library
    /// order, so (seed, version, worldId) fixes the entire sequence.
    public sealed class TrackGenerator
    {
        /// Longest run of consecutive bridge chunks before the generator is forced back down.
        public const int MaxBridgeRun = 3;

        readonly ChunkDefinition[] _library;
        readonly IRandomSource _random;
        readonly List<ChunkDefinition> _candidates = new List<ChunkDefinition>();

        ChunkEdge _openEdge = ChunkEdge.Ground;
        string _lastChunkId;
        int _bridgeRun;

        public TrackGenerator(IReadOnlyList<ChunkDefinition> library, IRandomSource random)
        {
            if (library == null || library.Count == 0) throw new ArgumentException("library is empty", nameof(library));
            _random = random ?? throw new ArgumentNullException(nameof(random));

            _library = new ChunkDefinition[library.Count];
            for (int i = 0; i < library.Count; i++) _library[i] = library[i];
        }

        /// Chunks emitted so far, i.e. the index of the next one.
        public int EmittedCount { get; private set; }

        /// How often the difficulty window had to be widened because nothing fitted. Healthy
        /// libraries stay at zero; GeneratorConstraintTests asserts that.
        public int RelaxedCount { get; private set; }

        /// The edge the next chunk has to start with.
        public ChunkEdge OpenEdge => _openEdge;

        public ChunkDefinition Next(int difficulty)
        {
            if (EmittedCount == 0)
            {
                var start = FindStartChunk();
                if (start != null) return Accept(start);
            }

            Collect(difficulty, ignoreDifficulty: false);
            if (_candidates.Count == 0)
            {
                RelaxedCount++;
                Collect(difficulty, ignoreDifficulty: true);
            }
            if (_candidates.Count == 0)
                throw new InvalidOperationException(
                    "No chunk can follow edge " + _openEdge + " at difficulty " + difficulty +
                    ". The chunk library has a dead end.");

            return Accept(WeightedPick());
        }

        ChunkDefinition FindStartChunk()
        {
            for (int i = 0; i < _library.Length; i++)
                if (_library[i].HasTag(ChunkTag.Start) && _library[i].EntryType == _openEdge)
                    return _library[i];
            return null;
        }

        void Collect(int difficulty, bool ignoreDifficulty)
        {
            _candidates.Clear();
            bool mustLeaveBridge = _bridgeRun >= MaxBridgeRun;

            for (int i = 0; i < _library.Length; i++)
            {
                var def = _library[i];
                if (def.EntryType != _openEdge) continue;
                if (def.HasTag(ChunkTag.Start)) continue;             // openers are for index 0 only
                if (mustLeaveBridge && def.ExitType == ChunkEdge.Bridge) continue;
                if (!ignoreDifficulty && !def.AcceptsDifficulty(difficulty)) continue;
                _candidates.Add(def);
            }

            // Avoid an immediate repeat, but never at the price of having no candidate at all.
            if (_candidates.Count > 1 && _lastChunkId != null)
            {
                for (int i = _candidates.Count - 1; i >= 0; i--)
                    if (_candidates[i].ChunkId == _lastChunkId && _candidates.Count > 1)
                        _candidates.RemoveAt(i);
            }
        }

        ChunkDefinition WeightedPick()
        {
            float total = 0f;
            for (int i = 0; i < _candidates.Count; i++) total += _candidates[i].Rarity;

            float roll = _random.NextFloat() * total;
            for (int i = 0; i < _candidates.Count; i++)
            {
                roll -= _candidates[i].Rarity;
                if (roll <= 0f) return _candidates[i];
            }
            return _candidates[_candidates.Count - 1];   // float slop only
        }

        ChunkDefinition Accept(ChunkDefinition def)
        {
            _openEdge = def.ExitType;
            _bridgeRun = def.ExitType == ChunkEdge.Bridge ? _bridgeRun + 1 : 0;
            _lastChunkId = def.ChunkId;
            EmittedCount++;
            return def;
        }
    }
}
