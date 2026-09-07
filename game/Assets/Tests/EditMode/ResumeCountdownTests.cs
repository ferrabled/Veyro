using System.Collections.Generic;
using MotionRunner.Track;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// The 3-2-1 that stands between a confirmed resume and the world moving again. Pure, because
    /// every way it can be wrong is a way the player loses a run they had already saved: completing
    /// twice unfreezes a run that is already running, completing after a cancel unfreezes one whose
    /// player has walked away from the phone, and a single enormous frame swallowing the whole
    /// countdown drops them into a moving run with no warning — which is exactly what the frame
    /// Android hands back after the app was in the background looks like.
    public sealed class ResumeCountdownTests
    {
        const float Dt = 1f / 60f;

        ResumeCountdown _countdown;

        [SetUp]
        public void SetUp() => _countdown = new ResumeCountdown();

        /// Runs the countdown for a stretch of frames at a steady 60 fps. Returns how many times it
        /// reported completion inside that stretch — the number that has to be exactly one.
        int Run(float seconds)
        {
            int completions = 0;
            for (float t = 0f; t < seconds; t += Dt)
                if (_countdown.Tick(Dt)) completions++;
            return completions;
        }

        [Test]
        public void NothingCountsUntilTheResumeIsConfirmed()
        {
            Assert.IsFalse(_countdown.IsRunning);
            Assert.IsFalse(_countdown.Tick(Dt), "a countdown nobody started must never unfreeze a run");
            Assert.AreEqual(0, _countdown.DisplayDigit, "there is no numeral to draw");
            Assert.AreEqual(0f, _countdown.RemainingSeconds);
        }

        [Test]
        public void TheNumeralsReadThreeThenTwoThenOne()
        {
            _countdown.Begin();

            var shown = new List<int>();
            for (int frame = 0; frame < 400 && _countdown.IsRunning; frame++)
            {
                int digit = _countdown.DisplayDigit;
                if (shown.Count == 0 || shown[shown.Count - 1] != digit) shown.Add(digit);
                _countdown.Tick(Dt);
            }

            CollectionAssert.AreEqual(new[] { 3, 2, 1 }, shown,
                "each numeral owns a whole second - a '3' that flashes for one frame, or a '0' at " +
                "the end, is the countdown reading as a glitch rather than as a beat");
        }

        [Test]
        public void ItCompletesExactlyOnce()
        {
            _countdown.Begin();

            Assert.AreEqual(0, Run(ResumeCountdown.DefaultDurationSeconds - 0.1f),
                "the world must not start moving before the numerals have run out");

            Assert.AreEqual(1, Run(0.5f), "and then it hands the caller its one FinishResume");

            Assert.AreEqual(0, Run(10f),
                "ever after: a second completion would unfreeze a run that is already running");
            Assert.IsFalse(_countdown.IsRunning);
            Assert.AreEqual(0, _countdown.DisplayDigit);
            Assert.AreEqual(0f, _countdown.RemainingSeconds);
        }

        [Test]
        public void Cancel_MidCountNeverCompletes()
        {
            // Back, the face lost again, or QUIT during the countdown: all three land back on the
            // paused menu, and a stray completion would unfreeze the run underneath it.
            _countdown.Begin();
            Run(1.5f);

            _countdown.Cancel();
            Assert.IsFalse(_countdown.IsRunning);
            Assert.AreEqual(0, _countdown.DisplayDigit, "the numerals come off the screen with it");

            Assert.AreEqual(0, Run(30f), "a cancelled countdown must never resume the run");
        }

        [Test]
        public void Cancel_IsIdempotent()
        {
            // RunFlow cancels defensively whenever it leaves the resuming phase, and more than one
            // cancel path can arrive in the same frame (back pressed during a face loss).
            _countdown.Cancel();
            _countdown.Begin();
            Run(1f);
            _countdown.Cancel();
            _countdown.Cancel();

            Assert.IsFalse(_countdown.IsRunning);
            Assert.AreEqual(0, Run(10f));
        }

        [Test]
        public void Begin_WorksAgainAfterACancel()
        {
            _countdown.Begin();
            Run(1f);
            _countdown.Cancel();

            _countdown.Begin();
            Assert.IsTrue(_countdown.IsRunning, "a cancelled resume is retried, not blocked");
            Assert.AreEqual(3, _countdown.DisplayDigit, "and it starts from the top");
            Assert.AreEqual(1, Run(ResumeCountdown.DefaultDurationSeconds + 0.5f));
        }

        [Test]
        public void Begin_WhileCountingRestartsFromTheTop()
        {
            // A second confirm is not a queued second countdown; it is this one, again.
            _countdown.Begin();
            Run(2.5f);
            Assert.AreEqual(1, _countdown.DisplayDigit, "down to the last numeral");

            _countdown.Begin();
            Assert.AreEqual(3, _countdown.DisplayDigit);
            Assert.AreEqual(ResumeCountdown.DefaultDurationSeconds, _countdown.RemainingSeconds, 1e-4f);
            Assert.AreEqual(0, Run(2.8f), "the full three seconds again, not the half second left");
            Assert.AreEqual(1, Run(0.5f));
        }

        [Test]
        public void AHitchCannotSwallowTheCountdown()
        {
            // The frame Android hands back after the app has been in the background is both the
            // largest delta in the app and the one most likely to land here, because a backgrounded
            // run is a paused run. Unclamped it would spend the whole countdown in a single frame.
            _countdown.Begin();

            Assert.IsFalse(_countdown.Tick(30f), "thirty seconds of being elsewhere is not a resume");
            Assert.AreEqual(ResumeCountdown.DefaultDurationSeconds - ResumeCountdown.MaxStepSeconds,
                _countdown.RemainingSeconds, 1e-4f,
                "one frame may contribute exactly MaxStepSeconds and no more");

            Assert.IsTrue(_countdown.IsRunning);
            Assert.AreEqual(3, _countdown.DisplayDigit, "the player still gets their three numerals");
        }

        [Test]
        public void NonPositiveDeltasContributeNothing()
        {
            // The first frame after a pause can report a zero or garbage delta; time must not run
            // backwards into a countdown that then never ends.
            _countdown.Begin();

            Assert.IsFalse(_countdown.Tick(-1f));
            Assert.IsFalse(_countdown.Tick(0f));
            Assert.IsFalse(_countdown.Tick(-30f));

            Assert.AreEqual(ResumeCountdown.DefaultDurationSeconds, _countdown.RemainingSeconds, 1e-6f,
                "a negative frame must neither spend the countdown nor extend it");
            Assert.AreEqual(3, _countdown.DisplayDigit);
        }

        [Test]
        public void AZeroLengthCountdownStillCompletesOnItsNextTick()
        {
            // The degenerate duration stays on the one code path: still started, still completed by
            // a Tick, so the caller's "wait for the true" contract needs no special case.
            _countdown.Begin(0f);
            Assert.IsTrue(_countdown.IsRunning);
            Assert.AreEqual(1, _countdown.DisplayDigit, "and never draws a 0");

            Assert.IsTrue(_countdown.Tick(Dt));
            Assert.IsFalse(_countdown.IsRunning);
        }

        [Test]
        public void ANegativeDurationIsTreatedTheSameWay()
        {
            _countdown.Begin(-5f);
            Assert.IsTrue(_countdown.IsRunning, "never a countdown that can never end");
            Assert.IsTrue(_countdown.Tick(Dt));
        }

        [Test]
        public void ACustomDurationIsHonoured()
        {
            // The override exists because the owner may still shorten this, or skip it on tilt
            // resumes (CAMERA_TUNING.md, open owner decisions).
            _countdown.Begin(1f);
            Assert.AreEqual(1, _countdown.DisplayDigit, "one second is one numeral");
            Assert.AreEqual(0, Run(0.8f));
            Assert.AreEqual(1, Run(0.4f));
        }

        [Test]
        public void TheHitchClampIsTheSameOneTheRestOfTheClocksUse()
        {
            // Pinned across the two engine-free clocks on purpose: they are riding out the same
            // stall, and one of them quietly tolerating a bigger frame than the other is how the
            // countdown ends up outliving the outage detector that paused the run in the first
            // place.
            Assert.AreEqual(CameraOutage.MaxStepSeconds, ResumeCountdown.MaxStepSeconds,
                "the countdown and the outage detector clamp a hitch identically");
        }
    }
}
