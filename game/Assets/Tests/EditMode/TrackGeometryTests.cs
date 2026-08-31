using MotionRunner.Track;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// Collision is a handful of AABB interval tests (CLAUDE.md rule 4), so the design promises
    /// - low barriers are jumpable, full blocks are not, and a free lane is genuinely free -
    /// are checked here rather than by feel on a phone.
    public sealed class TrackGeometryTests
    {
        // RunnerController: JumpVelocity 7.5, Gravity 22 -> apex = rest + v^2 / 2g.
        const float JumpApexY = TrackMetrics.RunnerRestY + (7.5f * 7.5f) / (2f * 22f);

        static Aabb GroundedRunner(float x = 0f) => TrackGeometry.Runner(x, TrackMetrics.RunnerRestY);

        static Aabb Obstacle(ObstacleKind kind, int lane, float z, float chunkStartZ = 0f) =>
            TrackGeometry.Obstacle(new ObstaclePlacement(kind, lane, z), chunkStartZ);

        [Test]
        public void ObstaclesRestOnTheDeck()
        {
            Assert.AreEqual(0f, Obstacle(ObstacleKind.LowBarrier, 0, 0f).MinY, 0.0001f);
            Assert.AreEqual(0f, Obstacle(ObstacleKind.FullBlock, 0, 0f).MinY, 0.0001f);
        }

        [Test]
        public void ChunkOffset_MovesObstaclesIntoWorldSpace()
        {
            var box = Obstacle(ObstacleKind.FullBlock, 1, 12f, chunkStartZ: 40f);
            Assert.AreEqual(52f, box.CenterZ, 0.0001f);
            Assert.AreEqual(TrackMetrics.LaneWidth, box.CenterX, 0.0001f);
        }

        [Test]
        public void GroundedRunner_HitsALowBarrier()
        {
            Assert.IsTrue(GroundedRunner().Intersects(Obstacle(ObstacleKind.LowBarrier, 0, 0f)));
        }

        [Test]
        public void JumpingRunner_ClearsALowBarrier()
        {
            var atApex = TrackGeometry.Runner(0f, JumpApexY);
            Assert.IsFalse(atApex.Intersects(Obstacle(ObstacleKind.LowBarrier, 0, 0f)));
        }

        [Test]
        public void ALowBarrierIsClearedForMostOfTheJumpArc()
        {
            // Anything less than a generous window makes low barriers feel like coin flips.
            var barrier = Obstacle(ObstacleKind.LowBarrier, 0, 0f);
            int clearSamples = 0;
            const int samples = 40;

            // Sample the ballistic arc y(t) = rest + v t - g t^2 / 2 over its whole airtime.
            const float velocity = 7.5f;
            const float gravity = 22f;
            float airtime = 2f * velocity / gravity;
            for (int i = 0; i <= samples; i++)
            {
                float t = airtime * i / samples;
                float y = TrackMetrics.RunnerRestY + velocity * t - 0.5f * gravity * t * t;
                if (!TrackGeometry.Runner(0f, y).Intersects(barrier)) clearSamples++;
            }

            Assert.Greater(clearSamples, samples / 2, "the jump arc barely clears a low barrier");
        }

        [Test]
        public void AFullBlockCannotBeJumped()
        {
            var atApex = TrackGeometry.Runner(0f, JumpApexY);
            Assert.IsTrue(atApex.Intersects(Obstacle(ObstacleKind.FullBlock, 0, 0f)),
                "a full block must stay lethal at the top of the jump, otherwise it is not a steering test");
        }

        [Test]
        public void AFreeLaneIsFree()
        {
            var block = Obstacle(ObstacleKind.FullBlock, 0, 0f);
            Assert.IsFalse(GroundedRunner(TrackMetrics.LaneCenterX(-1)).Intersects(block));
            Assert.IsFalse(GroundedRunner(TrackMetrics.LaneCenterX(1)).Intersects(block));
            Assert.IsTrue(GroundedRunner(TrackMetrics.LaneCenterX(0)).Intersects(block));
        }

        [Test]
        public void SteeringOutOfALaneClearsItWellBeforeTheNextLaneCentre()
        {
            // A lane change is animated, so safety must arrive before the slide finishes - the
            // player must not have to be standing on the next lane centre to be out of this one.
            // LaneSelectorTests pins when in the slide that happens.
            var block = Obstacle(ObstacleKind.FullBlock, 0, 0f);
            float halfwayOut = TrackMetrics.LaneWidth * 0.75f;
            Assert.IsFalse(GroundedRunner(halfwayOut).Intersects(block));
        }

        [Test]
        public void ObstaclesOutOfReachInZ_DoNotHit()
        {
            Assert.IsFalse(GroundedRunner().Intersects(Obstacle(ObstacleKind.FullBlock, 0, 2f)));
            Assert.IsFalse(GroundedRunner().Intersects(Obstacle(ObstacleKind.FullBlock, 0, -2f)));
        }

        [Test]
        public void CoinsAreCollectedWhileGroundedAndWhileJumping()
        {
            var coin = TrackGeometry.Coin(new CoinPlacement(0, 0f), 0f);
            Assert.IsTrue(GroundedRunner().Intersects(coin), "a coin must be reachable on the ground");
            Assert.IsTrue(TrackGeometry.Runner(0f, JumpApexY).Intersects(coin), "a coin must be reachable mid-jump");
        }

        [Test]
        public void CoinsInAnotherLane_AreNotCollected()
        {
            var coin = TrackGeometry.Coin(new CoinPlacement(0, 0f), 0f);
            Assert.IsFalse(GroundedRunner(TrackMetrics.LaneCenterX(1)).Intersects(coin));
        }

        [Test]
        public void TouchingBoxes_AreNotAHit()
        {
            var a = new Aabb(0f, 0f, 0f, 1f, 1f, 1f);
            var b = new Aabb(2f, 0f, 0f, 1f, 1f, 1f);
            Assert.IsFalse(a.Intersects(b));
        }

        [Test]
        public void SpeedRisesWithDifficultyAndIsClamped()
        {
            Assert.AreEqual(TrackMetrics.BaseSpeed, TrackMetrics.SpeedFor(DifficultyCurve.MinDifficulty), 0.0001f);
            Assert.Greater(TrackMetrics.SpeedFor(DifficultyCurve.MaxDifficulty), TrackMetrics.SpeedFor(1));
            Assert.AreEqual(TrackMetrics.SpeedFor(DifficultyCurve.MaxDifficulty), TrackMetrics.SpeedFor(99), 0.0001f);
            Assert.AreEqual(TrackMetrics.SpeedFor(DifficultyCurve.MinDifficulty), TrackMetrics.SpeedFor(-5), 0.0001f);
        }

        [Test]
        public void DifficultyCurve_IsEasyInMinuteOneAndHardByMinuteFive()
        {
            Assert.AreEqual(1, DifficultyCurve.At(0f));
            Assert.LessOrEqual(DifficultyCurve.At(59f), 2, "minute one must stay easy (handoff 3.2)");
            Assert.GreaterOrEqual(DifficultyCurve.At(300f), 6, "minute five must be hard (handoff 3.2)");
            Assert.AreEqual(DifficultyCurve.MaxDifficulty, DifficultyCurve.At(100000f));
            Assert.AreEqual(1, DifficultyCurve.At(-10f));
        }

        [Test]
        public void DifficultyCurve_NeverGoesBackwards()
        {
            int previous = 0;
            for (float t = 0f; t < 900f; t += 0.5f)
            {
                int difficulty = DifficultyCurve.At(t);
                Assert.GreaterOrEqual(difficulty, previous);
                previous = difficulty;
            }
        }
    }
}
