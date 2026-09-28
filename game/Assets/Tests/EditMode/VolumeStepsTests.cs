using MotionRunner.Audio;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// The volume control's scale against the levels SoundSettings stores.
    public sealed class VolumeStepsTests
    {
        [Test]
        public void TheDefaults_LightTheBarsTheyShould()
        {
            Assert.AreEqual(8, VolumeSteps.Lit(SoundSettings.DefaultMusic, false));
            Assert.AreEqual(10, VolumeSteps.Lit(SoundSettings.DefaultSfx, false));
        }

        [Test]
        public void EveryPosition_SurvivesTheRoundTrip()
        {
            for (int position = 0; position <= VolumeSteps.Count; position++)
                Assert.AreEqual(position, VolumeSteps.Lit(VolumeSteps.LevelAt(position), false),
                    "landing on position " + position + " must light exactly that many bars");
        }

        [Test]
        public void ZeroIsAPosition_NotOnlyTheMute()
        {
            // The owner asked to drag or tap straight down to 0; the ON/OFF pill still mutes
            // without losing the level, but silence is reachable on the scale itself.
            Assert.AreEqual(0f, VolumeSteps.LevelAt(0));
            Assert.AreEqual(0, VolumeSteps.PositionAt(0.02f), "the zero mark's own cell");
            Assert.AreEqual(0, VolumeSteps.PositionAt(-0.4f), "dragging past the left edge");
        }

        [Test]
        public void ThePositionUnderTheFinger_IsTheCellItIsIn()
        {
            float cell = 1f / VolumeSteps.Positions;
            Assert.AreEqual(1, VolumeSteps.PositionAt(cell * 1.5f));
            Assert.AreEqual(5, VolumeSteps.PositionAt(cell * 5.01f));
            Assert.AreEqual(VolumeSteps.Count, VolumeSteps.PositionAt(0.999f));
            Assert.AreEqual(VolumeSteps.Count, VolumeSteps.PositionAt(1.7f), "dragging past the right edge");
            Assert.AreEqual(0, VolumeSteps.PositionAt(float.NaN));
        }

        [Test]
        public void ALevelFromTheOldSlider_RoundsToTheNearestBar()
        {
            Assert.AreEqual(6, VolumeSteps.Lit(0.63f, false));
            Assert.AreEqual(1, VolumeSteps.Lit(0.05f, false));
            Assert.AreEqual(0, VolumeSteps.Lit(0.04f, false));
        }

        [Test]
        public void AMutedChannel_LightsNothing_ButKeepsItsLevel()
        {
            Assert.AreEqual(0, VolumeSteps.Lit(0.8f, true));
        }

        [Test]
        public void GarbageLevels_AreClampedNotPropagated()
        {
            Assert.AreEqual(0, VolumeSteps.Lit(float.NaN, false));
            Assert.AreEqual(0, VolumeSteps.Lit(-3f, false));
            Assert.AreEqual(10, VolumeSteps.Lit(7f, false));
            Assert.AreEqual(0f, VolumeSteps.LevelAt(-5));
            Assert.AreEqual(1f, VolumeSteps.LevelAt(99));
        }
    }
}
