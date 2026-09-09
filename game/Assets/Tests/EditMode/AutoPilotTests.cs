using System.Collections.Generic;
using MotionRunner.Track;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// The menu's demo driver. It is on screen every time the app opens, so "the runner walked
    /// into a wall on the main menu" is the most-seen bug this project could ship - and the one
    /// least likely to be caught, because nobody watches a menu for a minute on purpose.
    public sealed class AutoPilotTests
    {
        static ObstacleCluster Block(float z, params int[] lanes) =>
            new ObstacleCluster(z, Mask(lanes), 0);

        static ObstacleCluster Barrier(float z, params int[] lanes) =>
            new ObstacleCluster(z, 0, Mask(lanes));

        static int Mask(int[] lanes)
        {
            int mask = 0;
            foreach (int lane in lanes) mask |= ObstacleCluster.LaneBit(lane);
            return mask;
        }

        static List<ObstacleCluster> Track(params ObstacleCluster[] clusters) =>
            new List<ObstacleCluster>(clusters);

        [Test]
        public void EmptyTrack_HoldsTheLane()
        {
            Assert.AreEqual(1, AutoPilot.ChooseLane(Track(), 1));
            Assert.AreEqual(0, AutoPilot.ChooseLane(null, 0));
        }

        [Test]
        public void ABlockInTheCurrentLane_MovesOut()
        {
            var track = Track(Block(15f, 0));
            int lane = AutoPilot.ChooseLane(track, 0);
            Assert.AreNotEqual(0, lane);
            Assert.IsTrue(lane == -1 || lane == 1);
        }

        [Test]
        public void ABlockInAnotherLane_IsNotAReasonToMove()
        {
            Assert.AreEqual(0, AutoPilot.ChooseLane(Track(Block(15f, 1)), 0));
        }

        [Test]
        public void TheOnlyOpenLaneIsTaken_HoweverFarItIs()
        {
            // Left and centre blocked, so the only lane left is the right one - two across.
            Assert.AreEqual(1, AutoPilot.ChooseLane(Track(Block(15f, -1, 0)), -1));
        }

        [Test]
        public void AClearLaneIsPreferredOverOneWithALowBarrier()
        {
            // Centre blocked, a low barrier on the right, the left clear. Both open lanes are one
            // step away, so the barrier is what breaks the tie: dodging into a hop is worse than
            // dodging into nothing.
            var track = Track(new ObstacleCluster(15f, ObstacleCluster.LaneBit(0),
                ObstacleCluster.LaneBit(1)));
            Assert.AreEqual(-1, AutoPilot.ChooseLane(track, 0));
        }

        [Test]
        public void AClusterBeyondTheHorizon_IsNotSteeredForYet()
        {
            Assert.AreEqual(0, AutoPilot.ChooseLane(
                Track(Block(AutoPilot.SteerAheadMetres + 5f, 0)), 0));
        }

        [Test]
        public void TheDecisionHoldsWhileTheClusterIsBeingPassed()
        {
            // Centre blocked, so the pilot commits to a side lane at 15 m out. It must keep that
            // lane the whole way past the block - the next cluster only becomes its problem once
            // this one is genuinely behind the runner.
            int lane = AutoPilot.ChooseLane(Track(Block(15f, 0)), 0);

            for (float z = 15f; z > AutoPilot.PassedMetres; z -= 1f)
            {
                var track = Track(Block(z, 0), Block(z + 8f, lane));
                Assert.AreEqual(lane, AutoPilot.ChooseLane(track, lane),
                    "changed lane while still passing the block at z=" + z);
            }
        }

        [Test]
        public void OnceAClusterIsBehind_TheNextOneDecides()
        {
            // The near block is past; the one after it blocks the lane the runner is in.
            var track = Track(Block(AutoPilot.PassedMetres - 0.5f, 0), Block(9f, 1));
            Assert.AreNotEqual(1, AutoPilot.ChooseLane(track, 1));
        }

        [Test]
        public void ALowBarrierInTheLane_IsJumpedInsideTheWindow()
        {
            Assert.IsTrue(AutoPilot.ShouldJump(Track(Barrier(AutoPilot.JumpAheadMetres - 0.5f, 0)), 0));
        }

        [Test]
        public void ALowBarrier_IsNotJumpedTooEarly()
        {
            Assert.IsFalse(AutoPilot.ShouldJump(Track(Barrier(AutoPilot.JumpAheadMetres + 3f, 0)), 0));
        }

        [Test]
        public void ALowBarrierInAnotherLane_IsNotJumped()
        {
            Assert.IsFalse(AutoPilot.ShouldJump(Track(Barrier(3f, 1)), 0));
        }

        [Test]
        public void AFullBlock_IsNeverJumped()
        {
            // Full blocks top out at 1.8 m and are unjumpable by design (TrackMetrics): the pilot
            // has to steer around them, and a hop into one would look like a bug rather than
            // a miss.
            Assert.IsFalse(AutoPilot.ShouldJump(Track(Block(3f, 0)), 0));
        }

        [Test]
        public void ABarrierAlreadyPassed_IsNotJumped()
        {
            Assert.IsFalse(AutoPilot.ShouldJump(Track(Barrier(-0.5f, 0)), 0));
        }

        [Test]
        public void EveryLibraryChunkIsDrivable()
        {
            // The end-to-end claim: play every chunk in the shipping library one cluster at a
            // time and assert the pilot always has somewhere to be. A chunk the demo cannot solve
            // would sit on the menu crashing through obstacles until the player tapped RUN.
            foreach (var definition in ChunkLibrary.Greybox())
            {
                int lane = 0;
                foreach (var cluster in definition.Clusters)
                {
                    var track = Track(new ObstacleCluster(12f, cluster.FullBlockLanes,
                        cluster.LowBarrierLanes));
                    lane = AutoPilot.ChooseLane(track, lane);

                    Assert.AreEqual(0, cluster.FullBlockLanes & ObstacleCluster.LaneBit(lane),
                        definition.ChunkId + ": pilot chose a fully blocked lane " + lane);
                }
            }
        }
    }
}
