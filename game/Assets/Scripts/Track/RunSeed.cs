namespace MotionRunner.Track
{
    /// A run *is* a seed (handoff 3.3): Seed + GameVersion + WorldId fixes the whole track.
    /// The hash is hand-written FNV-1a rather than string.GetHashCode(), which is stable
    /// neither across runtimes nor across process runs. RunSeedTests pins its output so a
    /// refactor cannot silently change every Daily Run seed that has already been played.
    public readonly struct RunSeed
    {
        public const string DefaultWorldId = "greybox";

        const uint FnvOffsetBasis = 2166136261u;
        const uint FnvPrime = 16777619u;
        const uint FieldSeparator = 0xFFu;
        const uint FallbackState = 0x9E3779B9u;

        public readonly int Seed;
        public readonly string GameVersion;
        public readonly string WorldId;

        public RunSeed(int seed, string gameVersion, string worldId)
        {
            Seed = seed;
            GameVersion = gameVersion ?? string.Empty;
            WorldId = worldId ?? string.Empty;
        }

        /// Deterministic 32-bit state for this run's random source.
        public uint RngState()
        {
            unchecked
            {
                uint h = FnvOffsetBasis;
                h = HashUInt(h, (uint)Seed);
                h = HashString(h, GameVersion);
                h = HashString(h, WorldId);
                return h == 0u ? FallbackState : h;
            }
        }

        public IRandomSource CreateRandom() => new XorShiftRandom(RngState());

        static uint HashUInt(uint h, uint value)
        {
            unchecked
            {
                for (int i = 0; i < 4; i++)
                {
                    h ^= (value >> (i * 8)) & 0xFFu;
                    h *= FnvPrime;
                }
                return h;
            }
        }

        static uint HashString(uint h, string s)
        {
            unchecked
            {
                // Separator so that ("a", "b") and ("ab", "") cannot collide.
                h ^= FieldSeparator;
                h *= FnvPrime;
                if (s == null) return h;
                for (int i = 0; i < s.Length; i++)
                {
                    uint c = s[i];
                    h ^= c & 0xFFu;
                    h *= FnvPrime;
                    h ^= (c >> 8) & 0xFFu;
                    h *= FnvPrime;
                }
                return h;
            }
        }

        public override string ToString() => Seed + "/" + GameVersion + "/" + WorldId;
    }
}
