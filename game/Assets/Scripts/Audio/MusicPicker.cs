namespace MotionRunner.Audio
{
    /// Which run track a run gets. Engine-free and pure so "the same seed always gets the same
    /// song" is a test rather than a hope: the Daily Run is one shared track for everybody, and
    /// a challenge rematch should sound like the run being rematched.
    public static class MusicPicker
    {
        /// Zero-based track index for a seed hash (RunSeed.RngState), or -1 with no tracks.
        ///
        /// The hash is re-mixed before the modulo. RunSeed's FNV output is already decent, but
        /// consecutive daily seeds differ in a couple of low bits of one field, and a modulo
        /// straight off a hash whose low bits are correlated with the input would hand the same
        /// two tracks to a whole week. The mix is lowbias32 - three shifts and two multiplies,
        /// every input bit reaches every output bit.
        public static int TrackIndex(uint seedHash, int trackCount)
        {
            if (trackCount <= 0) return -1;
            if (trackCount == 1) return 0;
            return (int)(Mix(seedHash) % (uint)trackCount);
        }

        /// The fallback for a run with no seed to speak of: round-robin through the tracks.
        /// Returns the index to play and advances the cursor.
        public static int NextCycled(ref int cursor, int trackCount)
        {
            if (trackCount <= 0) return -1;
            if (cursor < 0) cursor = 0;
            int index = cursor % trackCount;
            cursor = (cursor + 1) % trackCount;
            return index;
        }

        static uint Mix(uint x)
        {
            unchecked
            {
                x ^= x >> 16;
                x *= 0x7feb352dU;
                x ^= x >> 15;
                x *= 0x846ca68bU;
                x ^= x >> 16;
                return x;
            }
        }
    }
}
