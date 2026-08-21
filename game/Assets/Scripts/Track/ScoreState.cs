namespace MotionRunner.Track
{
    /// Distance + coins + combo (T-005). Engine-free so the scoring rules are unit-tested
    /// rather than eyeballed on a phone.
    ///
    /// Score = whole metres travelled + coin points. A coin is worth CoinBaseValue times the
    /// current multiplier, and the multiplier climbs one step for every ComboCoinsPerStep
    /// coins picked up without letting the combo timer run out.
    public sealed class ScoreState
    {
        public const int CoinBaseValue = 10;
        public const float ComboWindowSeconds = 2.5f;
        public const int ComboCoinsPerStep = 3;
        public const int MaxComboMultiplier = 5;

        public float Distance { get; private set; }
        public int Coins { get; private set; }
        public int CoinPoints { get; private set; }
        public int Combo { get; private set; }
        public int BestCombo { get; private set; }
        public float ComboTimeRemaining { get; private set; }

        public int Multiplier
        {
            get
            {
                if (Combo <= 0) return 1;
                int m = 1 + (Combo - 1) / ComboCoinsPerStep;
                return m > MaxComboMultiplier ? MaxComboMultiplier : m;
            }
        }

        public int Score => (int)Distance + CoinPoints;

        public void Reset()
        {
            Distance = 0f;
            Coins = 0;
            CoinPoints = 0;
            Combo = 0;
            BestCombo = 0;
            ComboTimeRemaining = 0f;
        }

        public void AddDistance(float metres)
        {
            if (metres > 0f) Distance += metres;
        }

        /// Lets the combo window expire. Called once per frame while the run is live.
        public void Tick(float deltaTime)
        {
            if (Combo <= 0) return;
            ComboTimeRemaining -= deltaTime;
            if (ComboTimeRemaining > 0f) return;
            Combo = 0;
            ComboTimeRemaining = 0f;
        }

        /// Returns the points awarded for this coin.
        public int CollectCoin()
        {
            Coins++;
            Combo++;
            ComboTimeRemaining = ComboWindowSeconds;
            if (Combo > BestCombo) BestCombo = Combo;

            int points = CoinBaseValue * Multiplier;
            CoinPoints += points;
            return points;
        }
    }
}
