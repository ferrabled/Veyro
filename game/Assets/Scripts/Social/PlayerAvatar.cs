namespace MotionRunner.Social
{
    /// A player's avatar, drawn from nothing but their handle: a small mirrored pixel critter on
    /// one of a fixed set of colour pairs. Engine-free, so "same name, same critter" is a unit
    /// test rather than something checked by rerolling on a phone.
    ///
    /// Why a critter and not initials or an identicon: the handle is ADJECTIVE-ANIMAL-NN, so the
    /// name already promises a creature, and a generated one reads as the player's own runner-
    /// world mascot where two letters on a disc read as a placeholder. Mirrored because a
    /// symmetric blob reads as a face; given eyes because a symmetric blob without them reads as
    /// a stain. Rerolling the name rerolls the critter, which makes the reroll feel like a
    /// reroll.
    ///
    /// Deterministic across devices and runs: FNV-1a over the handle's UTF-16 code units, then a
    /// fixed integer mixer - never string.GetHashCode, which is randomised per process on some
    /// runtimes. Changing the algorithm changes every player's face, so PlayerAvatarTests pins
    /// one known pattern.
    public readonly struct AvatarPattern
    {
        /// Cells per side. Odd, so the mirror has a centre column.
        public const int Size = 7;

        /// Row-major, Size x Size, true = drawn.
        readonly bool[] _cells;

        /// Index into the view's colour pairs, 0 .. PlayerAvatar.PaletteSize - 1.
        public readonly int Palette;

        public AvatarPattern(bool[] cells, int palette)
        {
            _cells = cells;
            Palette = palette;
        }

        public bool this[int column, int row] =>
            _cells != null && column >= 0 && column < Size && row >= 0 && row < Size &&
            _cells[row * Size + column];

        public int FilledCount
        {
            get
            {
                int count = 0;
                if (_cells == null) return 0;
                foreach (bool cell in _cells) if (cell) count++;
                return count;
            }
        }
    }

    public static class PlayerAvatar
    {
        /// How many colour pairs the view offers. The pattern only picks an index; the colours
        /// themselves are the menu's business (MenuTheme), so a palette tweak is not a face change.
        public const int PaletteSize = 8;

        /// The eye row and the eye column on the left half (mirrored to Size - 1 - EyeColumn).
        public const int EyeRow = 2;
        public const int EyeColumn = 2;

        const int Centre = AvatarPattern.Size / 2;

        /// The critter for a handle. Case and surrounding space are ignored - "brave-egret-74"
        /// and " BRAVE-EGRET-74" are the same player - and null is the empty name, which still
        /// gets a critter rather than a blank tile.
        public static AvatarPattern For(string handle)
        {
            uint state = Hash(Normalize(handle));
            int palette = (int)(Mix(ref state) % PaletteSize);

            var cells = new bool[AvatarPattern.Size * AvatarPattern.Size];

            // Ears or antennae on the top row: none, wide, or close to the middle.
            switch (Mix(ref state) % 3)
            {
                case 1: Set(cells, 1, 0); break;
                case 2: Set(cells, 2, 0); break;
            }

            // The body, rows 1-4: a solid three-wide core, shoulders that come and go row by row,
            // and the odd arm on the outer column - only off a shoulder, never floating free.
            // Eyes are holes, so they need something round them: the core covers above, below
            // and inside, and the shoulder beside them is forced on together with the one under
            // it, or that shoulder pixel would hang off the face by a corner.
            for (int row = 1; row <= 4; row++)
            {
                Set(cells, Centre, row);
                Set(cells, Centre - 1, row);
                bool face = row == EyeRow || row == EyeRow + 1;
                bool shoulder = Chance(ref state, 3, 4) || face;
                if (shoulder) Set(cells, Centre - 2, row);
                if (shoulder && row >= 2 && Chance(ref state, 1, 3)) Set(cells, Centre - 3, row);
            }
            Clear(cells, EyeColumn, EyeRow);

            // Sometimes a mouth: a notch in the centre column.
            if (Chance(ref state, 1, 3)) Clear(cells, Centre, 4);

            // Legs: two, splayed; two, close; or three.
            switch (Mix(ref state) % 3)
            {
                case 0: Set(cells, 1, 5); break;
                case 1: Set(cells, 2, 5); break;
                default: Set(cells, 1, 5); Set(cells, Centre, 5); break;
            }

            return new AvatarPattern(cells, palette);
        }

        static string Normalize(string handle) =>
            (handle ?? string.Empty).Trim().ToUpperInvariant();

        /// FNV-1a, 32-bit, over UTF-16 code units.
        static uint Hash(string text)
        {
            uint hash = 2166136261u;
            foreach (char c in text)
            {
                hash ^= c;
                hash *= 16777619u;
            }
            return hash;
        }

        /// lowbias32 (Chris Wellons) on an advancing Weyl counter: every draw is a full-avalanche
        /// mix, so neighbouring names do not give neighbouring critters.
        static uint Mix(ref uint state)
        {
            state += 0x9E3779B9u;
            uint x = state;
            x ^= x >> 16;
            x *= 0x7FEB352Du;
            x ^= x >> 15;
            x *= 0x846CA68Bu;
            x ^= x >> 16;
            return x;
        }

        static bool Chance(ref uint state, uint numerator, uint denominator) =>
            Mix(ref state) % denominator < numerator;

        /// Sets a left-half cell and its mirror.
        static void Set(bool[] cells, int column, int row) => Paint(cells, column, row, true);

        static void Clear(bool[] cells, int column, int row) => Paint(cells, column, row, false);

        static void Paint(bool[] cells, int column, int row, bool value)
        {
            int size = AvatarPattern.Size;
            cells[row * size + column] = value;
            cells[row * size + (size - 1 - column)] = value;
        }
    }
}
