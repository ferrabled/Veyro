using MotionRunner.Track;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// Who is allowed to unfreeze a run, and on whose say-so.
    ///
    /// The pause menu's camera resume ends in a 3-2-1 that hands a moving runner back to the
    /// player, so the question "what confirmed this?" has exactly one wrong answer: nothing did.
    /// PR #6 shipped that answer — an auto-pause from a camera outage began the resume staging by
    /// itself, and if the raise-hand probe failed to load, the fallback counted down the moment
    /// the player's face reappeared. Walking back into shot is not consent: the player may be
    /// crossing the room, answering the door, handing the phone to somebody. The run is lost by
    /// the time they look at it.
    ///
    /// The fix is this rule, and these are its four rows. They read as a table on purpose — the
    /// only interesting one is the last, and it is only interesting next to the other three.
    public sealed class ResumeConfirmTests
    {
        [Test]
        public void UserResume_WithProbe_AsksForTheGesture()
        {
            Assert.AreEqual(ResumeConfirmStep.Gesture,
                ResumeConfirm.For(ResumeOrigin.User, true));
        }

        /// The tap on RESUME (or Android back) IS the confirmation, so a device where the pose
        /// model will not load still resumes the way it did before the gesture existed. Rule 3:
        /// the gesture augments, it never gates.
        [Test]
        public void UserResume_WithoutProbe_CountsDownOnTheTapThatStartedIt()
        {
            Assert.AreEqual(ResumeConfirmStep.Countdown,
                ResumeConfirm.For(ResumeOrigin.User, false));
        }

        /// An auto-resume with a working probe is the whole hands-free loop: walk out, walk back,
        /// raise a hand. The hand is the confirmation, so this row is identical to the user one.
        [Test]
        public void AutoResume_WithProbe_AsksForTheGesture()
        {
            Assert.AreEqual(ResumeConfirmStep.Gesture,
                ResumeConfirm.For(ResumeOrigin.Auto, true));
        }

        /// The regression. Auto-initiated and no gesture available means nobody has confirmed
        /// anything at all, so the menu must ask for a touch rather than count down at a face it
        /// happens to see. If this ever flips back to Countdown, a broken model file turns a
        /// camera hiccup into an unattended unfreeze.
        [Test]
        public void AutoResume_WithoutProbe_WaitsForATouch_NeverCountsDown()
        {
            ResumeConfirmStep step = ResumeConfirm.For(ResumeOrigin.Auto, false);

            Assert.AreEqual(ResumeConfirmStep.TouchConfirm, step);
            Assert.AreNotEqual(ResumeConfirmStep.Countdown, step,
                "an auto-begun resume must never unfreeze the run without a confirmation");
        }

        /// The probe, when it is up, is the confirmation regardless of who began the resume —
        /// which is what keeps the hands-free path hands-free and the code down to one prompt.
        [Test]
        public void AWorkingProbe_MakesOriginIrrelevant()
        {
            Assert.AreEqual(ResumeConfirm.For(ResumeOrigin.User, true),
                ResumeConfirm.For(ResumeOrigin.Auto, true));
        }

        /// The origins only ever differ without a probe — stated as its own test so a future
        /// third origin cannot quietly inherit the countdown by being neither of these two.
        [Test]
        public void WithoutAProbe_OriginIsTheWholeDecision()
        {
            Assert.AreNotEqual(ResumeConfirm.For(ResumeOrigin.User, false),
                ResumeConfirm.For(ResumeOrigin.Auto, false));
        }
    }
}
