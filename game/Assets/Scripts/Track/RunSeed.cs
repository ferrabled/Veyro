namespace MotionRunner.Track
{
    /// A run *is* a seed (handoff 3.3): Seed + ContentVersion + WorldId fixes the whole track.
    ///
    /// The middle field is the *content* version (ChunkLibrary.ContentVersion), not the application
    /// version. The spec calls for a "content manifest" here, and the distinction is load-bearing
    /// for the Daily Run: two builds shipping the same chunk library must generate the same track,
    /// or a bugfix release splits the day's players onto different tracks.
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
        public readonly string ContentVersion;
        public readonly string WorldId;

        public RunSeed(int seed, string contentVersion, string worldId)
        {
            Seed = seed;
            ContentVersion = contentVersion ?? string.Empty;
            WorldId = worldId ?? string.Empty;
        }

        /// Deterministic 32-bit state for this run's random source.
        public uint RngState()
        {
            unchecked
            {
                uint h = FnvOffsetBasis;
                h = HashUInt(h, (uint)Seed);
                h = HashString(h, ContentVersion);
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

        public override string ToString() => Seed + "/" + ContentVersion + "/" + WorldId;
    }
}
