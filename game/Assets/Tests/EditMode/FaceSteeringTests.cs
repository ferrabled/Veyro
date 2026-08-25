using MotionRunner.Pose;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// T-012's acceptance criterion is a false-positive *rate*, so the gesture rules must be
    /// drivable by synthetic trajectories: stand still, sway, hop, crouch, disappear. Every test
    /// here is one of those trajectories at a fixed 30 Hz — the camera's actual frame rate.
    public sealed class FaceSteeringTests
    {
        const float Dt = 1f / 30f;
        const float NeutralX = 0.5f;
        const float NeutralY = 0.4f;

        static FaceSteering Calibrated()
        {
            var steering = new FaceSteering();
            for (int i = 0; i < steering.CalibrationSamples; i++)
                steering.Submit(NeutralX, NeutralY, 0.9f, Dt);
            Assert.IsTrue(steering.IsCalibrated, "calibration should complete");
            return steering;
        }

        static void Hold(FaceSteering s, float x, float y, float seconds, float score = 0.9f)
        {
            for (float t = 0f; t < seconds; t += Dt) s.Submit(x, y, score, Dt);
        }

        [Test]
        public void CalibrationSetsNeutralToWhereThePlayerActuallyStands()
        {
            var steering = new FaceSteering();
            for (int i = 0; i < steering.CalibrationSamples; i++)
                steering.Submit(0.62f, 0.35f, 0.9f, Dt);

            Assert.IsTrue(steering.IsCalibrated);
            Assert.AreEqual(0.62f, steering.NeutralX, 1e-3f);
            Assert.AreEqual(0.35f, steering.BaselineY, 1e-3f);
        }

        [Test]
        public void LowScoreSamplesDoNotCalibrate()
        {
            var steering = new FaceSteering();
            for (int i = 0; i < 100; i++) steering.Submit(0.5f, 0.4f, 0.1f, Dt);
            Assert.IsFalse(steering.IsCalibrated);
            Assert.IsFalse(steering.IsTracking);
        }

        [Test]
        public void LeaningRightSteersRightAndSettlesInsideTheClamp()
        {
            var steering = Calibrated();
            Hold(steering, NeutralX + steering.HalfRangeX, NeutralY, 1f);
            Assert.Greater(steering.MoveAxis, 0.7f);
            Assert.LessOrEqual(steering.MoveAxis, 1f);
        }

        [Test]
        public void LeaningLeftSteersLeft()
        {
            var steering = Calibrated();
            Hold(steering, NeutralX - steering.HalfRangeX, NeutralY, 1f);
            Assert.Less(steering.MoveAxis, -0.7f);
        }

        [Test]
        public void SmallWobbleInsideTheDeadZoneDoesNotSteer()
        {
            var steering = Calibrated();
            for (int i = 0; i < 60; i++)
            {
                float wobble = (i % 2 == 0 ? 1f : -1f) * steering.HalfRangeX * steering.DeadZone * 0.5f;
                steering.Submit(NeutralX + wobble, NeutralY, 0.9f, Dt);
            }
            Assert.AreEqual(0f, steering.MoveAxis, 0.05f);
        }

        [Test]
        public void FastUpwardMotionFiresExactlyOneJump()
        {
            var steering = Calibrated();

            // A hop: 8 cm of frame in ~100 ms, then back down over half a second.
            int fires = 0;
            for (int i = 1; i <= 3; i++)
            {
                steering.Submit(NeutralX, NeutralY - 0.027f * i, 0.9f, Dt);
                if (steering.IsJumpActive) fires++;
            }
            Assert.Greater(fires, 0, "the hop should read as a jump");

            bool stillActiveLater = false;
            for (int i = 0; i < 20; i++)
            {
                steering.Submit(NeutralX, NeutralY - 0.08f + 0.004f * i, 0.9f, Dt);
                if (i > 8 && steering.IsJumpActive) stillActiveLater = true;
            }
            Assert.IsFalse(stillActiveLater, "coming back down must not fire a second jump");
        }

        [Test]
        public void SlowSwayNeverJumps()
        {
            var steering = Calibrated();
            // Breathing / idle sway: ±2 cm of frame over two full seconds.
            for (int i = 0; i < 120; i++)
            {
                float y = NeutralY + 0.02f * (float)System.Math.Sin(i * Dt * System.Math.PI);
                steering.Submit(NeutralX, y, 0.9f, Dt);
                Assert.IsFalse(steering.IsJumpActive, "sway at frame " + i + " read as a jump");
            }
        }

        [Test]
        public void RefractoryPeriodBlocksImmediateSecondJump()
        {
            var steering = Calibrated();
            Hold(steering, NeutralX, NeutralY, 0.2f);

            steering.Submit(NeutralX, NeutralY - 0.08f, 0.9f, Dt); // sharp hop
            Assert.IsTrue(steering.IsJumpActive);

            // Immediately hop again inside the refractory window: the window must stay shut
            // once the first jump's active period has expired.
            Hold(steering, NeutralX, NeutralY, steering.JumpWindowSeconds + 0.05f);
            steering.Submit(NeutralX, NeutralY - 0.08f, 0.9f, Dt);
            Assert.IsFalse(steering.IsJumpActive, "second hop inside refractory fired");
        }

        [Test]
        public void SustainedCrouchSlides_BriefDipDoesNot()
        {
            var steering = Calibrated();

            steering.Submit(NeutralX, NeutralY + steering.SlideDrop + 0.03f, 0.9f, Dt);
            Assert.IsFalse(steering.IsSlideActive, "one dipped frame is not a crouch");

            Hold(steering, NeutralX, NeutralY + steering.SlideDrop + 0.03f, 0.3f);
            Assert.IsTrue(steering.IsSlideActive, "a held crouch should slide");

            Hold(steering, NeutralX, NeutralY, 0.2f);
            Assert.IsFalse(steering.IsSlideActive, "standing back up should end the slide");
        }

        [Test]
        public void LosingTheFaceDecaysSteeringToNeutralAndFiresNothing()
        {
            var steering = Calibrated();
            Hold(steering, NeutralX + steering.HalfRangeX, NeutralY, 1f);
            Assert.Greater(steering.MoveAxis, 0.7f);

            for (int i = 0; i < 30; i++)
            {
                steering.Submit(0f, 0f, 0f, Dt); // gone
                Assert.IsFalse(steering.IsJumpActive);
                Assert.IsFalse(steering.IsSlideActive);
            }
            Assert.AreEqual(0f, steering.MoveAxis, 0.05f);
            Assert.IsFalse(steering.IsTracking);
        }

        [Test]
        public void ReappearingAfterLossDoesNotReadTheReturnAsAJump()
        {
            var steering = Calibrated();
            for (int i = 0; i < 15; i++) steering.Submit(0f, 0f, 0f, Dt);

            // Face pops back in far from the last seen position: with no previous sample there
            // is no velocity, so nothing may fire on the first frame back.
            steering.Submit(NeutralX, NeutralY - 0.2f, 0.9f, Dt);
            Assert.IsFalse(steering.IsJumpActive, "reacquisition frame fired a jump");
        }

        [Test]
        public void StandingUpFromASlideDoesNotReadAsAJump()
        {
            var steering = Calibrated();
            Hold(steering, NeutralX, NeutralY + steering.SlideDrop + 0.04f, 0.4f);
            Assert.IsTrue(steering.IsSlideActive);

            // Stand back up over three frames — fast upward motion, a jump's exact signature.
            for (int i = 1; i <= 3; i++)
                steering.Submit(NeutralX, NeutralY + (steering.SlideDrop + 0.04f) * (3 - i) / 3f, 0.9f, Dt);
            for (int i = 0; i < 10; i++)
            {
                steering.Submit(NeutralX, NeutralY, 0.9f, Dt);
                Assert.IsFalse(steering.IsJumpActive, "stand-up read as a jump");
            }
        }

        [Test]
        public void NeutralDriftsTowardWhereThePlayerSettles()
        {
            var steering = Calibrated();
            // The player shuffles a little to one side and stays there. Small on purpose: a
            // sustained deliberate lean (large offset) must NOT recenter, or long curves die.
            Hold(steering, NeutralX + 0.035f, NeutralY, 12f);
            Assert.Greater(steering.NeutralX, NeutralX + 0.015f,
                "neutral should follow a sustained new stance");
        }
    }
}
