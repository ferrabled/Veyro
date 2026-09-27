using MotionRunner.Track;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// When another run is allowed to start, and on which frame. Pure, which is the point: every
    /// bug this has produced is a frame-ordering bug that reads on a phone as "it sometimes jumps
    /// the moment the run starts" and reproduces on demand for nobody.
    ///
    /// The invariant all of these circle: the input that ASKS for a run must be spent before that
    /// run exists. On a phone the tap anywhere, the RUN AGAIN click and the SKINS & STORE click are
    /// all the same TouchPhase.Ended, and RunSession's Update reads it after the EventSystem has
    /// already dispatched the click.
    public sealed class RestartGateTests
    {
        const float Dt = 1f / 60f;

        RestartGate _gate;

        [SetUp]
        public void SetUp() => _gate = new RestartGate();

        [Test]
        public void ATapQueuesTheNextRunForALaterFrame()
        {
            Assert.IsFalse(_gate.Tick(Dt, restartPressed: true, overlayOpen: false),
                "the frame that read the tap must not also start the run");
            Assert.IsTrue(_gate.IsQueued);

            Assert.IsTrue(_gate.Tick(Dt, restartPressed: false, overlayOpen: false));
            Assert.IsFalse(_gate.IsQueued);
        }

        [Test]
        public void TheButtonQueuesTheRunRatherThanStartingItItself()
        {
            // The PR #5 defect in one assertion. The click runs inside the EventSystem's dispatch,
            // which DefaultExecutionOrder(100) puts before RunSession's own Update in the SAME
            // frame, so a run started here is live in time to be handed the click's own release as
            // a jump. The gate is the only thing that may start it, and it does so from Tick.
            Assert.IsTrue(_gate.RequestFromButton());
            Assert.IsTrue(_gate.IsQueued, "the click must not have started a run by itself");

            Assert.IsTrue(_gate.Tick(Dt, restartPressed: false, overlayOpen: false));
        }

        [Test]
        public void TheReleaseThatClickedTheButtonCannotAlsoQueueASecondRun()
        {
            // Same physical release, two readings: the EventSystem's click and TouchTapInput's
            // jump. It has to buy exactly one run.
            _gate.RequestFromButton();

            Assert.IsTrue(_gate.Tick(Dt, restartPressed: true, overlayOpen: false));
            Assert.IsFalse(_gate.IsQueued,
                "the release that clicked RUN AGAIN must not queue another run behind it");
            Assert.IsFalse(_gate.Tick(Dt, restartPressed: false, overlayOpen: false));
        }

        [Test]
        public void TwoClicksBuyOneRun()
        {
            Assert.IsTrue(_gate.RequestFromButton());
            Assert.IsFalse(_gate.RequestFromButton(), "a double tap is one restart");

            Assert.IsTrue(_gate.Tick(Dt, false, false));
            Assert.IsFalse(_gate.Tick(Dt, false, false));
        }

        [Test]
        public void ALongerLockoutCoversTheWholeCardReveal()
        {
            // The result card takes crash pose + fade to appear; a tap anywhere in that window
            // must not restart the run before the card was ever drawn.
            _gate.LockOut(1.25f);
            float waited = 0f;
            while (_gate.IsLockedOut)
            {
                Assert.IsFalse(_gate.Tick(Dt, restartPressed: true, overlayOpen: false));
                // The tick that ends the lockout may accept that same tap (existing contract);
                // no tick BEFORE it may.
                if (_gate.IsLockedOut) Assert.IsFalse(_gate.IsQueued);
                waited += Dt;
            }
            Assert.AreEqual(1.25f, waited, 3f * Dt);

            // Never shorter than the constant, whatever a caller passes.
            _gate.LockOut(0.01f);
            Assert.IsTrue(_gate.IsLockedOut);
            _gate.Tick(0.1f, false, false);
            Assert.IsTrue(_gate.IsLockedOut);
        }

        [Test]
        public void TheCrashLockoutIgnoresTheTapThatKilledThePlayer()
        {
            _gate.LockOut();
            Assert.IsTrue(_gate.IsLockedOut);

            Assert.IsFalse(_gate.Tick(Dt, restartPressed: true, overlayOpen: false));
            Assert.IsFalse(_gate.IsQueued,
                "the tap that killed the player must not also skip the result screen");

            float waited = Dt;
            while (_gate.IsLockedOut)
            {
                Assert.IsFalse(_gate.Tick(Dt, restartPressed: false, overlayOpen: false));
                waited += Dt;
            }

            Assert.AreEqual(RestartGate.LockoutSeconds, waited, 3f * Dt,
                "the lockout must last the half second the result screen is written for");

            Assert.IsFalse(_gate.Tick(Dt, restartPressed: true, overlayOpen: false));
            Assert.IsTrue(_gate.IsQueued, "and then the same tap starts the next run");
            Assert.IsTrue(_gate.Tick(Dt, restartPressed: false, overlayOpen: false));
        }

        [Test]
        public void TheCrashLockoutRefusesTheButtonToo()
        {
            // The RUN AGAIN button is on screen from the frame of the crash, so a player already
            // tapping when they died can land on it. Same rule as the tap, or the result screen
            // would flash past for exactly the players who crash mid-tap.
            _gate.LockOut();
            Assert.IsFalse(_gate.RequestFromButton());
            Assert.IsFalse(_gate.IsQueued);
        }

        [Test]
        public void AnOpenStoreEatsTheQueuedRestartInsteadOfHoldingIt()
        {
            // The 27 Aug device bug: SKINS & STORE opens the panel with the same release that would
            // restart the run behind it. Dropped rather than held, because a run appearing the
            // moment the store closes is the same surprise one frame later.
            _gate.Tick(Dt, restartPressed: true, overlayOpen: false);
            Assert.IsTrue(_gate.IsQueued);

            Assert.IsFalse(_gate.Tick(Dt, restartPressed: false, overlayOpen: true),
                "no run may start behind the store");
            Assert.IsFalse(_gate.IsQueued, "and none may be waiting for it to close");
        }

        [Test]
        public void AnOpenStoreNeverQueuesARestartAtAll()
        {
            Assert.IsFalse(_gate.Tick(Dt, restartPressed: true, overlayOpen: true));
            Assert.IsFalse(_gate.IsQueued);
        }

        [Test]
        public void Clear_DropsTheQueueAndTheLockout()
        {
            // RunSession.Stop: the run was abandoned, so nothing about it survives into the mode
            // picker - including a restart that was one frame from happening.
            _gate.Tick(Dt, restartPressed: true, overlayOpen: false);
            _gate.LockOut();

            _gate.Clear();
            Assert.IsFalse(_gate.IsQueued);
            Assert.IsFalse(_gate.IsLockedOut);
            Assert.IsFalse(_gate.Tick(Dt, restartPressed: false, overlayOpen: false),
                "an abandoned run must not restart itself");
        }
    }
}
