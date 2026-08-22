using System;
using MotionRunner.Pose;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// T-010 reports a median, and T-013 turns camera mode on or off with it. Both numbers are
    /// therefore load-bearing, and both are computed here rather than by eye over a logcat dump.
    public sealed class LatencyStatsTests
    {
        static LatencyStats WithSamples(params double[] samples)
        {
            var stats = new LatencyStats(samples.Length == 0 ? 1 : samples.Length);
            foreach (double sample in samples) stats.Add(sample);
            return stats;
        }

        [Test]
        public void EmptyWindowReportsZeroRatherThanThrowing()
        {
            var stats = new LatencyStats();
            Assert.AreEqual(0d, stats.Median());
            Assert.AreEqual(0d, stats.Mean());
            Assert.AreEqual(0d, stats.Min());
            Assert.AreEqual(0d, stats.Max());
            Assert.AreEqual(0, stats.Count);
        }

        [Test]
        public void MedianOfOddCountIsTheMiddleSample()
        {
            Assert.AreEqual(20d, WithSamples(30d, 10d, 20d).Median(), 1e-9);
        }

        [Test]
        public void MedianOfEvenCountAveragesTheTwoMiddleSamples()
        {
            Assert.AreEqual(15d, WithSamples(10d, 20d).Median(), 1e-9);
            Assert.AreEqual(25d, WithSamples(40d, 10d, 20d, 30d).Median(), 1e-9);
        }

        [Test]
        public void MedianIgnoresOneHugeOutlierButTheMeanDoesNot()
        {
            // The reason the gate is a median: one thermal stall must not fail a good device.
            var stats = WithSamples(20d, 21d, 19d, 20d, 900d);
            Assert.AreEqual(20d, stats.Median(), 1e-9);
            Assert.Greater(stats.Mean(), 100d);
        }

        [Test]
        public void PercentileInterpolatesBetweenSamples()
        {
            var stats = WithSamples(10d, 20d, 30d, 40d, 50d);
            Assert.AreEqual(10d, stats.Percentile(0d), 1e-9);
            Assert.AreEqual(30d, stats.Percentile(50d), 1e-9);
            Assert.AreEqual(50d, stats.Percentile(100d), 1e-9);
            Assert.AreEqual(46d, stats.Percentile(90d), 1e-9);
        }

        [Test]
        public void WindowIsRollingAndDropsTheOldestSample()
        {
            var stats = new LatencyStats(3);
            stats.Add(100d);
            stats.Add(10d);
            stats.Add(10d);
            stats.Add(10d); // evicts the 100
            Assert.AreEqual(3, stats.Count);
            Assert.AreEqual(10d, stats.Max(), 1e-9);
        }

        [Test]
        public void GateIsUndecidedUntilTheWarmupWindowIsFull()
        {
            // A cold start must never read as "fast enough" and switch camera mode on.
            var stats = new LatencyStats(64);
            for (int i = 0; i < LatencyStats.DefaultWarmupSamples - 1; i++) stats.Add(5d);

            Assert.IsFalse(stats.TryGate(out bool passes));
            Assert.IsFalse(passes);

            stats.Add(5d);
            Assert.IsTrue(stats.TryGate(out passes));
            Assert.IsTrue(passes);
        }

        [Test]
        public void GateFailsAtOrAboveThirtyMilliseconds()
        {
            // Handoff 8.2 / T-013: strictly under 30 ms enables camera mode.
            var fast = new LatencyStats(64);
            var slow = new LatencyStats(64);
            for (int i = 0; i < LatencyStats.DefaultWarmupSamples; i++)
            {
                fast.Add(29.9d);
                slow.Add(30.0d);
            }

            Assert.IsTrue(fast.TryGate(out bool fastPasses));
            Assert.IsTrue(fastPasses);
            Assert.IsTrue(slow.TryGate(out bool slowPasses));
            Assert.IsFalse(slowPasses);
        }

        [Test]
        public void NegativeSamplesAreRejected()
        {
            var stats = new LatencyStats();
            Assert.Throws<ArgumentOutOfRangeException>(() => stats.Add(-1d));
        }
    }
}
