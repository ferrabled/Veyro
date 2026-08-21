using MotionRunner.Track;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    public sealed class ScoreStateTests
    {
        ScoreState _score;

        [SetUp]
        public void SetUp() => _score = new ScoreState();

        [Test]
        public void Distance_CountsWholeMetres()
        {
            _score.AddDistance(12.7f);
            Assert.AreEqual(12, _score.Score);
            Assert.AreEqual(12.7f, _score.Distance, 0.001f);
        }

        [Test]
        public void Distance_IgnoresBackwardsMovement()
        {
            _score.AddDistance(10f);
            _score.AddDistance(-5f);
            Assert.AreEqual(10f, _score.Distance, 0.001f);
        }

        [Test]
        public void FirstCoin_IsWorthTheBaseValue()
        {
            Assert.AreEqual(ScoreState.CoinBaseValue, _score.CollectCoin());
            Assert.AreEqual(1, _score.Coins);
            Assert.AreEqual(1, _score.Combo);
            Assert.AreEqual(1, _score.Multiplier);
        }

        [Test]
        public void Multiplier_ClimbsEveryComboStep()
        {
            for (int i = 0; i < ScoreState.ComboCoinsPerStep; i++) _score.CollectCoin();
            Assert.AreEqual(1, _score.Multiplier, "the multiplier must not climb before the step is complete");

            _score.CollectCoin();
            Assert.AreEqual(2, _score.Multiplier);
            Assert.AreEqual(ScoreState.CoinBaseValue * 2, _score.CoinPoints - ScoreState.CoinBaseValue * 3);
        }

        [Test]
        public void Multiplier_IsCapped()
        {
            for (int i = 0; i < 200; i++) _score.CollectCoin();
            Assert.AreEqual(ScoreState.MaxComboMultiplier, _score.Multiplier);
        }

        [Test]
        public void Combo_ExpiresWhenTheWindowRunsOut()
        {
            _score.CollectCoin();
            _score.CollectCoin();
            Assert.AreEqual(2, _score.Combo);

            _score.Tick(ScoreState.ComboWindowSeconds - 0.1f);
            Assert.AreEqual(2, _score.Combo, "the combo must survive until the window is actually over");

            _score.Tick(0.2f);
            Assert.AreEqual(0, _score.Combo);
            Assert.AreEqual(1, _score.Multiplier);
        }

        [Test]
        public void Combo_IsRefreshedByEachCoin()
        {
            for (int i = 0; i < 10; i++)
            {
                _score.CollectCoin();
                _score.Tick(ScoreState.ComboWindowSeconds - 0.1f);
            }
            Assert.AreEqual(10, _score.Combo);
        }

        [Test]
        public void BestCombo_SurvivesTheComboExpiring()
        {
            for (int i = 0; i < 5; i++) _score.CollectCoin();
            _score.Tick(ScoreState.ComboWindowSeconds + 1f);
            _score.CollectCoin();

            Assert.AreEqual(1, _score.Combo);
            Assert.AreEqual(5, _score.BestCombo);
        }

        [Test]
        public void Tick_OnAnEmptyComboDoesNothing()
        {
            _score.Tick(10f);
            Assert.AreEqual(0, _score.Combo);
            Assert.AreEqual(0f, _score.ComboTimeRemaining, 0.001f);
        }

        [Test]
        public void Score_IsDistancePlusCoinPoints()
        {
            _score.AddDistance(250.9f);
            for (int i = 0; i < 4; i++) _score.CollectCoin();
            Assert.AreEqual(250 + _score.CoinPoints, _score.Score);
        }

        [Test]
        public void Reset_ClearsEverything()
        {
            _score.AddDistance(500f);
            for (int i = 0; i < 12; i++) _score.CollectCoin();
            _score.Reset();

            Assert.AreEqual(0, _score.Score);
            Assert.AreEqual(0, _score.Coins);
            Assert.AreEqual(0, _score.CoinPoints);
            Assert.AreEqual(0, _score.Combo);
            Assert.AreEqual(0, _score.BestCombo);
            Assert.AreEqual(0f, _score.Distance, 0.001f);
        }
    }
}
