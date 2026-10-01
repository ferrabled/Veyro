using MotionRunner.Track;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// Who is allowed to unfreeze a run, and on whose say-so.
    ///
    /// The pause menu's camera resume ends in a 3-2-1 that hands a moving runner back to the
    /// player, so the question "what confirmed this?" has exactly one wrong answer: nothing did.
    /// PR #6 shipped that answer — an auto-pause from a camera outage began the resume staging by
    /// itself, and if the resume gesture was unavailable (then: a raise-hand pose model that failed
    /// to load), the fallback counted down the moment the player's face reappeared. Walking back
    /// into shot is not consent: the player may be crossing the room, answering the door, handing
    /// the phone to somebody. The run is lost by the time they look at it.
    ///
    /// The fix is this rule, and these are its four rows. They read as a table on purpose — the
    /// only interesting one is the last, and it is only interesting next to the other three. The
    /// gesture is two hops now (DoubleHopConfirm), which is always available once a face is held;
    /// the rows without it stay pinned because the rule is about consent, not about the gesture.
    public sealed class ResumeConfirmTests
    {
        [Test]
        public void UserResume_WithTheGesture_AsksForTheGesture()
        {
            Assert.AreEqual(ResumeConfirmStep.Gesture,
                ResumeConfirm.For(ResumeOrigin.User, true));
        }

        /// The tap on RESUME (or Android back) IS the confirmation, so a resume without the
        /// gesture still goes the way it did before the gesture existed. Rule 3: the gesture
        /// augments, it never gates.
        [Test]
        public void UserResume_WithoutTheGesture_CountsDownOnTheTapThatStartedIt()
        {
            Assert.AreEqual(ResumeConfirmStep.Countdown,
                ResumeConfirm.For(ResumeOrigin.User, false));
        }

        /// An auto-resume with the gesture up is the whole hands-free loop: walk out, walk back,
        /// stand still, hop twice. The hops are the confirmation, so this row is identical to the
        /// user one.
        [Test]
        public void AutoResume_WithTheGesture_AsksForTheGesture()
        {
            Assert.AreEqual(ResumeConfirmStep.Gesture,
                ResumeConfirm.For(ResumeOrigin.Auto, true));
        }

        /// The regression. Auto-initiated and no gesture available means nobody has confirmed
        /// anything at all, so the menu must ask for a touch rather than count down at a face it
        /// happens to see. If this ever flips back to Countdown, a missing gesture turns a camera
        /// hiccup into an unattended unfreeze.
        [Test]
        public void AutoResume_WithoutTheGesture_WaitsForATouch_NeverCountsDown()
        {
            ResumeConfirmStep step = ResumeConfirm.For(ResumeOrigin.Auto, false);

            Assert.AreEqual(ResumeConfirmStep.TouchConfirm, step);
            Assert.AreNotEqual(ResumeConfirmStep.Countdown, step,
                "an auto-begun resume must never unfreeze the run without a confirmation");
        }

        /// The gesture, when it is up, is the confirmation regardless of who began the resume —
        /// which is what keeps the hands-free path hands-free and the code down to one prompt.
        [Test]
        public void AnAvailableGesture_MakesOriginIrrelevant()
        {
            Assert.AreEqual(ResumeConfirm.For(ResumeOrigin.User, true),
                ResumeConfirm.For(ResumeOrigin.Auto, true));
        }

        /// The origins only ever differ without the gesture — stated as its own test so a future
        /// third origin cannot quietly inherit the countdown by being neither of these two.
        [Test]
        public void WithoutTheGesture_OriginIsTheWholeDecision()
        {
            Assert.AreNotEqual(ResumeConfirm.For(ResumeOrigin.User, false),
                ResumeConfirm.For(ResumeOrigin.Auto, false));
        }
    }
}
