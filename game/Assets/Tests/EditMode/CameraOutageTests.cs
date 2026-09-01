using MotionRunner.Pose;
using MotionRunner.Track;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// The detector that turns "the camera has stopped answering" into a pause. Pure, because the
    /// two ways it can be wrong are both invisible in a playtest until they cost a run: pausing on
    /// a motion-blur streak (the run was fine), and never pausing at all (lateral control is gone
    /// for the rest of the run and nothing says so).
    public sealed class CameraOutageTests
    {
        const float Dt = 1f / 60f;

        CameraOutage _outage;

        [SetUp]
        public void SetUp() => _outage = new CameraOutage();

        /// Runs the detector for a stretch of frames at a steady 60 fps. Returns whether it asked
        /// for a pause at any point inside it.
        bool Run(float seconds, bool live, bool hasPosition, bool rigFailed = false)
        {
            bool asked = false;
            for (float t = 0f; t < seconds; t += Dt)
                asked |= _outage.Tick(Dt, live, hasPosition, rigFailed);
            return asked;
        }

        [Test]
        public void ARunTheCameraCanSeeIsNeverPaused()
        {
            Assert.IsFalse(Run(60f, live: true, hasPosition: true));
            Assert.AreEqual(0f, _outage.ElapsedSeconds);
        }

        [Test]
        public void BlurGapsUnderTheWindowNeverPause()
        {
            // The regime the pipeline deliberately rides out: FaceSteering carries a position
            // through a marginal-score streak for 0.30 s and CameraFaceInput calls an observation
            // stale at 0.35 s, so gaps this long are what a fast side-step and the ascent of a hop
            // look like. Twenty of them back to back must not pause anything.
            for (int i = 0; i < 20; i++)
            {
                Assert.IsFalse(Run(0.35f, live: true, hasPosition: false),
                    "a blur gap must never pause a run");
                Assert.IsFalse(Run(0.10f, live: true, hasPosition: true));
            }
        }

        [Test]
        public void ASustainedOutagePausesOnceAndOnlyOnce()
        {
            Assert.IsFalse(Run(CameraOutage.PauseAfterSeconds - 0.1f, true, hasPosition: false),
                "the window must run out before the run is taken away from the player");

            Assert.IsTrue(Run(0.2f, true, hasPosition: false),
                "a camera that has said nothing for the whole window pauses the run");

            Assert.IsFalse(Run(10f, true, hasPosition: false),
                "and asks exactly once - the pause menu owns the re-acquisition from there");
            Assert.IsTrue(_outage.HasAskedForPause);
        }

        [Test]
        public void ATerminalFailurePausesImmediately()
        {
            // RigState.Failed is terminal - the rig has torn its camera and model down - so there
            // is nothing for the window to wait for.
            Assert.IsTrue(_outage.Tick(Dt, cameraRunLive: true, hasPosition: false, rigFailed: true));
            Assert.IsFalse(Run(10f, true, hasPosition: false, rigFailed: true), "once, not per frame");
        }

        [Test]
        public void ATerminalFailurePausesEvenWithAFreshPositionInHand()
        {
            Assert.IsTrue(_outage.Tick(Dt, cameraRunLive: true, hasPosition: true, rigFailed: true));
        }

        [Test]
        public void ATiltRunIsNeverPaused()
        {
            Assert.IsFalse(Run(30f, live: false, hasPosition: false), "a tilt run has no camera to lose");
            Assert.IsFalse(_outage.Tick(Dt, cameraRunLive: false, hasPosition: false, rigFailed: true));
        }

        [Test]
        public void AFrozenRunDoesNotAccumulateOutageTime()
        {
            // live is false while the pause menu is up, while a resume is landing and on the result
            // screen. The camera is deliberately released for the first two, so time spent there
            // would otherwise guarantee a pause the moment the run came back - and pause-spam the
            // menu that is already re-acquiring the camera.
            Assert.IsFalse(Run(1.6f, live: true, hasPosition: false));

            Assert.IsFalse(Run(30f, live: false, hasPosition: false));
            Assert.AreEqual(0f, _outage.ElapsedSeconds, "a paused run is not an outage");

            Assert.IsFalse(Run(1.6f, live: true, hasPosition: false),
                "and the window starts again from zero when the run comes back");
        }

        [Test]
        public void AReacquiredFaceRearmsTheDetector()
        {
            Assert.IsTrue(Run(2f, true, hasPosition: false));

            Assert.IsFalse(Run(0.5f, true, hasPosition: true));
            Assert.IsFalse(_outage.HasAskedForPause, "the outage is over");

            Assert.IsTrue(Run(2f, true, hasPosition: false),
                "a second outage in the same run must pause it again");
        }

        [Test]
        public void AHitchCannotSpendTheWholeWindow()
        {
            // One enormous frame - an app coming back from the background, a stalled GC, an editor
            // breakpoint - is not a second and a half of a camera failing to see anybody.
            Assert.IsFalse(_outage.Tick(30f, cameraRunLive: true, hasPosition: false, rigFailed: false));
            Assert.AreEqual(CameraOutage.MaxStepSeconds, _outage.ElapsedSeconds, 1e-4f);
        }

        [Test]
        public void Reset_ForgetsAnOutageInFlight()
        {
            Run(1.6f, true, hasPosition: false);
            _outage.Reset();

            Assert.AreEqual(0f, _outage.ElapsedSeconds);
            Assert.IsFalse(Run(1.6f, true, hasPosition: false));
        }

        [Test]
        public void TheWindowSitsAboveTheBlurRegimeAndBelowWalkingAway()
        {
            // The two sides of the one dial, pinned across assemblies so raising the blur tolerance
            // in FaceSteering cannot silently swallow it. Below the low end, real runs start pausing
            // on a side-step; above the high end, stepping out of frame is uncapped again and the
            // frozen axis this exists to bound comes back.
            Assert.Greater(CameraOutage.PauseAfterSeconds,
                4f * FaceSteering.DefaultPositionCarrySeconds,
                "the window has to clear the blur streaks the position tier exists to bridge");
            Assert.GreaterOrEqual(CameraOutage.PauseAfterSeconds, 1.5f);
            Assert.LessOrEqual(CameraOutage.PauseAfterSeconds, 2f);
        }
    }
}
