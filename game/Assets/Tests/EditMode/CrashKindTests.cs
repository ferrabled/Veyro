using MotionRunner.Track;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// The obstacle → crash-pose mapping the world and the result card both rely on.
    public class CrashKindTests
    {
        [Test]
        public void FullBlockIsAlwaysAWallSlam()
        {
            Assert.AreEqual(CrashKind.WallSlam, CrashKinds.For(ObstacleKind.FullBlock, airborne: false));
            Assert.AreEqual(CrashKind.WallSlam, CrashKinds.For(ObstacleKind.FullBlock, airborne: true));
        }

        [Test]
        public void LowBarrierTripsWhetherGroundedOrLandingOnIt()
        {
            Assert.AreEqual(CrashKind.Trip, CrashKinds.For(ObstacleKind.LowBarrier, airborne: false));
            Assert.AreEqual(CrashKind.Trip, CrashKinds.For(ObstacleKind.LowBarrier, airborne: true));
        }

        [Test]
        public void SummaryCarriesRecordFlagsAndCrash()
        {
            var seed = new RunSeed(42, ChunkLibrary.ContentVersion, RunSeed.DefaultWorldId);
            var s = new RunSummary(RunMode.Daily, ControlScheme.Tilt, 1200, 3, 4, 500, 1200, 1200, "2026-09-21",
                newAllTimeBest: true, newDailyBest: true, crash: CrashKind.Trip, seed: seed);
            Assert.IsTrue(s.IsNewRecord);
            Assert.AreEqual(CrashKind.Trip, s.Crash);
            Assert.AreEqual(42, s.Seed.Seed);

            var plain = new RunSummary(RunMode.Free, ControlScheme.Tilt, 10, 0, 0, 5, 900, 0, "");
            Assert.IsFalse(plain.IsNewRecord);
            Assert.AreEqual(CrashKind.WallSlam, plain.Crash);
        }
    }
}
