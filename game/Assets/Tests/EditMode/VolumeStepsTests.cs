using MotionRunner.Audio;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// The profile tab's volume bars against the levels SoundSettings stores.
    public sealed class VolumeStepsTests
    {
        [Test]
        public void TheDefaults_LightTheBarsTheyShouldAndSurviveARoundTrip()
        {
            Assert.AreEqual(8, VolumeSteps.Lit(SoundSettings.DefaultMusic, false));
            Assert.AreEqual(10, VolumeSteps.Lit(SoundSettings.DefaultSfx, false));

            for (int index = 0; index < VolumeSteps.Count; index++)
                Assert.AreEqual(index + 1, VolumeSteps.Lit(VolumeSteps.LevelFor(index), false),
                    "tapping bar " + index + " must light exactly that many bars");
        }

        [Test]
        public void ALevelFromTheOldSlider_RoundsToTheNearestBar()
        {
            Assert.AreEqual(6, VolumeSteps.Lit(0.63f, false));
            Assert.AreEqual(1, VolumeSteps.Lit(0.05f, false));
            Assert.AreEqual(0, VolumeSteps.Lit(0.04f, false));
            Assert.AreEqual(0, VolumeSteps.Lit(0f, false));
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
        }

        [Test]
        public void TheQuietestBar_IsOneStep_NotSilence()
        {
            Assert.AreEqual(0.1f, VolumeSteps.LevelFor(0), 1e-6f);
            Assert.AreEqual(1f, VolumeSteps.LevelFor(VolumeSteps.Count - 1), 1e-6f);
            Assert.AreEqual(0.1f, VolumeSteps.LevelFor(-5), 1e-6f, "out of range taps clamp");
            Assert.AreEqual(1f, VolumeSteps.LevelFor(99), 1e-6f, "out of range taps clamp");
        }
    }
}
