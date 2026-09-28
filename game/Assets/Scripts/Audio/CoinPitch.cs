namespace MotionRunner.Audio
{
    /// The coin's pitch as a function of the combo. A rising pitch is the oldest way a game says
    /// "keep going" without a word of text; it rises by a small, even step so a long chain reads
    /// as a scale, and it stops rising where a sample starts sounding like a chirp.
    ///
    /// The reset "after a gap" is not here: ScoreState already drops Combo to 0 when
    /// ComboWindowSeconds pass without a coin, and RunSession hands that Combo straight in, so
    /// the pitch falls back to base exactly when the HUD's combo counter does.
    public static class CoinPitch
    {
        public const float Base = 1f;
        public const float StepPerCoin = 0.035f;
        public const float Max = 1.5f;

        public static float For(int combo)
        {
            if (combo <= 1) return Base;
            float pitch = Base + (combo - 1) * StepPerCoin;
            return pitch > Max ? Max : pitch;
        }
    }
}
