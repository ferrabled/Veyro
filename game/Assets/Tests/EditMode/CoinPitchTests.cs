using MotionRunner.Audio;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    public sealed class CoinPitchTests
    {
        [Test]
        public void FirstCoin_IsBasePitch()
        {
            Assert.AreEqual(CoinPitch.Base, CoinPitch.For(0));
            Assert.AreEqual(CoinPitch.Base, CoinPitch.For(1));
        }

        [Test]
        public void Pitch_RisesWithTheComboAndNeverFalls()
        {
            float previous = CoinPitch.For(1);
            for (int combo = 2; combo < 60; combo++)
            {
                float pitch = CoinPitch.For(combo);
                Assert.GreaterOrEqual(pitch, previous, "combo " + combo);
                previous = pitch;
            }
            Assert.Greater(CoinPitch.For(5), CoinPitch.For(2));
        }

        [Test]
        public void Pitch_IsCapped()
        {
            Assert.AreEqual(CoinPitch.Max, CoinPitch.For(1000));
            Assert.LessOrEqual(CoinPitch.For(30), CoinPitch.Max);
        }

        [Test]
        public void ALapsedCombo_ResetsThePitch()
        {
            // ScoreState drops Combo to 0 after the window; the pitch follows from that number.
            Assert.AreEqual(CoinPitch.Base, CoinPitch.For(0));
        }
    }
}
