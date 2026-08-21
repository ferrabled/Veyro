namespace MotionRunner.Track
{
    /// xorshift32, hand-rolled on purpose: System.Random's sequence is not contractually stable
    /// across runtimes, and a Daily Run has to produce the same track on Mono in the editor and
    /// on IL2CPP/ARM64 on the phone (handoff 3.3, and T-008 / T-024 depend on it).
    public sealed class XorShiftRandom : IRandomSource
    {
        const uint FallbackState = 0x9E3779B9u;

        uint _state;

        public XorShiftRandom(uint state) => _state = state == 0u ? FallbackState : state;

        public uint NextUInt()
        {
            unchecked
            {
                uint x = _state;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                _state = x;
                return x;
            }
        }

        public int NextInt(int exclusiveMax)
        {
            if (exclusiveMax <= 1) return 0;
            return (int)(NextUInt() % (uint)exclusiveMax);
        }

        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);
    }
}
