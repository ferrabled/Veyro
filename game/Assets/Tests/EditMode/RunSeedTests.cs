using MotionRunner.Track;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// A published Daily Run seed has to keep generating the same track forever, so the seed
    /// hash and the PRNG stream are pinned here. If one of these fails, the change is either a
    /// bug or a content break that needs a WorldId bump - not a test to update in passing.
    public sealed class RunSeedTests
    {
        [Test]
        public void RngState_IsPinnedForAKnownSeed()
        {
            Assert.AreEqual(1209465041u, new RunSeed(12345, "0.1.0", "greybox").RngState());
        }

        [Test]
        public void RngState_DependsOnWorldId()
        {
            Assert.AreEqual(3459060643u, new RunSeed(12345, "0.1.0", "alps").RngState());
        }

        [Test]
        public void RngState_DependsOnSeed()
        {
            Assert.AreEqual(770786398u, new RunSeed(12346, "0.1.0", "greybox").RngState());
        }

        [Test]
        public void RngState_DependsOnGameVersion()
        {
            Assert.AreEqual(3474704280u, new RunSeed(12345, "0.2.0", "greybox").RngState());
        }

        [Test]
        public void RngState_IsNeverZero()
        {
            for (int seed = -50; seed <= 50; seed++)
                Assert.AreNotEqual(0u, new RunSeed(seed, "0.1.0", "greybox").RngState());
        }

        [Test]
        public void NullStrings_AreTreatedAsEmpty()
        {
            Assert.AreEqual(new RunSeed(7, string.Empty, string.Empty).RngState(),
                            new RunSeed(7, null, null).RngState());
        }

        [Test]
        public void XorShiftStream_IsPinned()
        {
            var random = new XorShiftRandom(1u);
            Assert.AreEqual(270369u, random.NextUInt());
            Assert.AreEqual(67634689u, random.NextUInt());
            Assert.AreEqual(2647435461u, random.NextUInt());
            Assert.AreEqual(307599695u, random.NextUInt());
            Assert.AreEqual(2398689233u, random.NextUInt());
        }

        [Test]
        public void XorShift_ZeroStateFallsBackToANonZeroState()
        {
            Assert.AreNotEqual(0u, new XorShiftRandom(0u).NextUInt());
        }

        [Test]
        public void NextFloat_StaysInUnitRange()
        {
            var random = new RunSeed(99, "0.1.0", RunSeed.DefaultWorldId).CreateRandom();
            for (int i = 0; i < 10000; i++)
            {
                float value = random.NextFloat();
                Assert.GreaterOrEqual(value, 0f);
                Assert.Less(value, 1f);
            }
        }

        [Test]
        public void NextInt_StaysInRange()
        {
            var random = new RunSeed(4242, "0.1.0", RunSeed.DefaultWorldId).CreateRandom();
            for (int i = 0; i < 10000; i++)
            {
                int value = random.NextInt(7);
                Assert.GreaterOrEqual(value, 0);
                Assert.Less(value, 7);
            }
            Assert.AreEqual(0, random.NextInt(1));
            Assert.AreEqual(0, random.NextInt(0));
        }

        [Test]
        public void SameSeed_ProducesTheSameStream()
        {
            var seed = new RunSeed(2026, "0.1.0", RunSeed.DefaultWorldId);
            var a = seed.CreateRandom();
            var b = seed.CreateRandom();
            for (int i = 0; i < 1000; i++) Assert.AreEqual(a.NextUInt(), b.NextUInt());
        }
    }
}
