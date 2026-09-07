using MotionRunner.Pose;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// The raised-hand confirm, pinned against synthetic landmarks in the FaceSteeringTests style.
    /// A false confirm here unfreezes a paused run for a player who never asked, and the mirror
    /// mapping fails SILENTLY when it is wrong (the player raises the asked-for hand and nothing
    /// happens) — both are exactly the failures a playtest does not reliably catch, which is why
    /// every branch of the rule and both directions of the mirror are pinned rather than waved at
    /// a phone.
    public sealed class RaisedHandTests
    {
        // A standing person in the upright frame (normalized, origin top-left, y DOWN): nose at
        // 0.30, shoulder line at 0.42, so one head unit is 0.12 of the frame height.
        const float NoseY = 0.30f;
        const float ShoulderY = 0.42f;
        const float HeadUnit = ShoulderY - NoseY;

        /// Where the rule's bar sits for this body: the wrist must be strictly above this.
        const float Threshold = NoseY - 0.5f * HeadUnit;

        /// Comfortably overhead — half a head unit above the bar, nowhere near the boundary.
        const float WellAbove = Threshold - 0.5f * HeadUnit;

        /// A torso the rule trusts: nose and both shoulders tracked. Wrists are left untracked
        /// until a test places one, so what each test asserts is exactly the wrist it set.
        static PoseFrame Standing(float noseY = NoseY, float shoulderY = ShoulderY)
        {
            var frame = new PoseFrame();
            Set(frame, PoseJoint.Nose, 0.50f, noseY);
            Set(frame, PoseJoint.LeftShoulder, 0.58f, shoulderY);
            Set(frame, PoseJoint.RightShoulder, 0.42f, shoulderY);
            frame.MarkTracked(0.9f);
            return frame;
        }

        static void Set(PoseFrame frame, PoseJoint joint, float x, float y,
            float visibility = 0.9f, float presence = 0.9f)
        {
            frame.Set((int)joint, new PoseLandmark(x, y, 0f, visibility, presence));
        }

        // ---- the mirror mapping: the silent failure, pinned in both directions ----

        [Test]
        public void TheMirrorMappingIsPinnedInBothDirections()
        {
            // The upright frame is selfie-mirrored ONCE upstream, and BlazePose labels anatomy as
            // if it never was: the player's right hand lands on the image's right, which the model
            // calls the subject's LEFT. Flip this mapping and the gesture stops working with no
            // error anywhere — the player raises the hand the screen asked for and nothing fires.
            Assert.AreEqual(PoseJoint.LeftWrist, RaisedHand.WristFor(selfieMirrored: true),
                "on the mirrored selfie frame the player's right hand is the model's LEFT wrist");
            Assert.AreEqual(PoseJoint.RightWrist, RaisedHand.WristFor(selfieMirrored: false),
                "on an unmirrored frame anatomy and player agree");
        }

        [Test]
        public void PlayersRightHandOverheadConfirmsOnTheMirroredFrame()
        {
            PoseFrame frame = Standing();
            Set(frame, PoseJoint.LeftWrist, 0.60f, WellAbove);
            Assert.IsTrue(RaisedHand.IsRaised(frame, selfieMirrored: true));
        }

        [Test]
        public void TheWrongHandNeverConfirms()
        {
            // The player's LEFT hand overhead — on the mirrored frame that is the model's
            // RightWrist. The rule asks for the right hand specifically, so this must refuse.
            PoseFrame frame = Standing();
            Set(frame, PoseJoint.RightWrist, 0.40f, WellAbove);
            Assert.IsFalse(RaisedHand.IsRaised(frame, selfieMirrored: true),
                "the player's left hand must not confirm for their right");
        }

        [Test]
        public void AnUnmirroredFrameReadsTheAnatomicalRightWrist()
        {
            // The same physical gesture expressed without the selfie mirror (rear camera, most
            // Editor webcams): the player's right hand is now the model's RightWrist.
            PoseFrame frame = Standing();
            Set(frame, PoseJoint.RightWrist, 0.40f, WellAbove);
            Assert.IsTrue(RaisedHand.IsRaised(frame, selfieMirrored: false));

            PoseFrame wrongHand = Standing();
            Set(wrongHand, PoseJoint.LeftWrist, 0.60f, WellAbove);
            Assert.IsFalse(RaisedHand.IsRaised(wrongHand, selfieMirrored: false));
        }

        // ---- the geometric margin ----

        [Test]
        public void AWristAtShoulderHeightIsNotRaised()
        {
            PoseFrame frame = Standing();
            Set(frame, PoseJoint.LeftWrist, 0.60f, ShoulderY);
            Assert.IsFalse(RaisedHand.IsRaised(frame, selfieMirrored: true));
        }

        [Test]
        public void AWristAtNoseHeightIsNotRaised()
        {
            // The margin IS the rule: pushing hair back, a hand on an ear, a wave — all park a
            // wrist near nose level, and none of them may resume a run.
            PoseFrame frame = Standing();
            Set(frame, PoseJoint.LeftWrist, 0.60f, NoseY);
            Assert.IsFalse(RaisedHand.IsRaised(frame, selfieMirrored: true),
                "nose level is 'near the face', not 'above the head'");
        }

        [Test]
        public void TheThresholdItselfDoesNotCount()
        {
            // Strictly above, so the boundary sample belongs to "not raised" — the refusing side.
            PoseFrame frame = Standing();
            Set(frame, PoseJoint.LeftWrist, 0.60f, Threshold);
            Assert.IsFalse(RaisedHand.IsRaised(frame, selfieMirrored: true));
        }

        // ---- the trust gates ----

        [Test]
        public void ALowVisibilityWristDoesNotCount()
        {
            PoseFrame frame = Standing();
            Set(frame, PoseJoint.LeftWrist, 0.60f, WellAbove, visibility: 0.4f, presence: 0.9f);
            Assert.IsFalse(RaisedHand.IsRaised(frame, selfieMirrored: true));
        }

        [Test]
        public void AnOverheadWristStillCountsAsItsPresenceDips()
        {
            // The device finding that reshaped the gate (2026-09-02): a hand held properly
            // overhead parks the wrist at the top of the landmarker's crop, where presence
            // collapses while visibility holds — and a raise the player was faithfully holding
            // flickered out of the rule. The wrist is therefore gated on visibility alone, the
            // spec's original bar.
            PoseFrame frame = Standing();
            Set(frame, PoseJoint.LeftWrist, 0.60f, WellAbove, visibility: 0.9f, presence: 0.4f);
            Assert.IsTrue(RaisedHand.IsRaised(frame, selfieMirrored: true),
                "a crop-edge presence dip must not un-raise a hand the model can still see");
        }

        // ---- the elbow fallback: the hand left the frame entirely ----

        [Test]
        public void AnElbowAboveTheNoseVouchesForAHandTheModelLost()
        {
            // Arm straight up, hand past the top of the frame: no usable wrist at all, but an
            // elbow overhead means the upper arm is vertical — no near-face fidget produces that.
            PoseFrame frame = Standing();
            Set(frame, PoseJoint.LeftElbow, 0.60f, NoseY - 0.02f);
            Assert.IsTrue(RaisedHand.IsRaised(frame, selfieMirrored: true));
        }

        [Test]
        public void AnElbowAtNoseLevelDoesNotVouch()
        {
            // Strictly above: an elbow AT nose height is a hand somewhere near the head — the
            // hair-pushback family the wrist margin exists to refuse.
            PoseFrame frame = Standing();
            Set(frame, PoseJoint.LeftElbow, 0.60f, NoseY);
            Assert.IsFalse(RaisedHand.IsRaised(frame, selfieMirrored: true));
        }

        [Test]
        public void TheWrongArmsElbowNeverVouches()
        {
            // The player's LEFT arm overhead is the model's Right* on the mirrored frame — the
            // fallback must honour the same mirror mapping as the wrist.
            PoseFrame frame = Standing();
            Set(frame, PoseJoint.RightElbow, 0.40f, NoseY - 0.02f);
            Assert.IsFalse(RaisedHand.IsRaised(frame, selfieMirrored: true));
        }

        [Test]
        public void ALowPresenceElbowDoesNotVouch()
        {
            // Unlike the wrist, the elbow keeps the full IsTracked gate: elbows live mid-frame,
            // so an elbow the crop lost really is a regressed guess.
            PoseFrame frame = Standing();
            Set(frame, PoseJoint.LeftElbow, 0.60f, NoseY - 0.02f, visibility: 0.9f, presence: 0.4f);
            Assert.IsFalse(RaisedHand.IsRaised(frame, selfieMirrored: true));
        }

        [Test]
        public void NoPoseNeverRaises()
        {
            PoseFrame frame = Standing();
            Set(frame, PoseJoint.LeftWrist, 0.60f, WellAbove);
            frame.MarkLost(); // landmarks deliberately survive a loss — the rule must not read them
            Assert.IsFalse(RaisedHand.IsRaised(frame, selfieMirrored: true));
        }

        [Test]
        public void UntrackedShouldersRefuse()
        {
            PoseFrame frame = Standing();
            Set(frame, PoseJoint.LeftShoulder, 0.58f, ShoulderY, visibility: 0.4f);
            Set(frame, PoseJoint.LeftWrist, 0.60f, WellAbove);
            Assert.IsFalse(RaisedHand.IsRaised(frame, selfieMirrored: true),
                "no trusted shoulder line means no head unit to measure against");
        }

        [Test]
        public void AnUntrackedNoseRefuses()
        {
            PoseFrame frame = Standing();
            Set(frame, PoseJoint.Nose, 0.50f, NoseY, presence: 0.4f);
            Set(frame, PoseJoint.LeftWrist, 0.60f, WellAbove);
            Assert.IsFalse(RaisedHand.IsRaised(frame, selfieMirrored: true));
        }

        [Test]
        public void ADegenerateHeadUnitRefuses()
        {
            // Shoulders level with the nose (bent over the phone) — the scale has collapsed and
            // "above the head" would quietly mean "above the nose". Refuse instead.
            PoseFrame level = Standing(shoulderY: NoseY);
            Set(level, PoseJoint.LeftWrist, 0.60f, 0.05f);
            Assert.IsFalse(RaisedHand.IsRaised(level, selfieMirrored: true));

            // Shoulders ABOVE the nose (a negative unit, the landmarker mislabelling outright)
            // would flip the comparison — the guard must catch it before the arithmetic runs.
            PoseFrame inverted = Standing(shoulderY: NoseY - HeadUnit);
            Set(inverted, PoseJoint.LeftWrist, 0.60f, 0.05f);
            Assert.IsFalse(RaisedHand.IsRaised(inverted, selfieMirrored: true));
        }

        // ---- the temporal confirm ----

        [Test]
        public void ThreeConsecutiveSamplesConfirmOnExactlyTheThird()
        {
            var confirm = new RaisedHandConfirm();
            Assert.IsFalse(confirm.Submit(true), "one sample is one lucky frame");
            Assert.IsFalse(confirm.Submit(true), "two is one coincidence away from that");
            Assert.IsTrue(confirm.Submit(true),
                "three independent inferences agreeing is the spec's ~1 s hold");
        }

        [Test]
        public void AlternatingSamplesNeverConfirm()
        {
            // The flicker discipline after the 2026-09-02 retune: a miss winds the streak back by
            // one instead of zeroing it, so noise that flickers in and out of the rule oscillates
            // between 0 and 1 forever — net evidence, not luck, is what confirms.
            var confirm = new RaisedHandConfirm();
            for (int i = 0; i < 12; i++)
            {
                Assert.IsFalse(confirm.Submit(i % 2 == 0),
                    "a hand flickering in and out of the rule is not a held gesture");
                Assert.LessOrEqual(confirm.Streak, 1);
            }
        }

        [Test]
        public void AOneSampleDipDelaysTheConfirmInsteadOfRestartingIt()
        {
            // The device failure the wind-back exists for: the lite landmarker drops the overhead
            // wrist for a single sample routinely, and a hard reset made the prompt flip between
            // "hold it" and "raise your right hand" against an arm that never moved. One miss now
            // costs one sample of progress, not the whole hold.
            var confirm = new RaisedHandConfirm();
            confirm.Submit(true);
            confirm.Submit(true);
            confirm.Submit(false); // a blurred sample mid-hold
            Assert.AreEqual(1, confirm.Streak, "the dip pays back one sample, not all of them");
            confirm.Submit(true);
            Assert.IsTrue(confirm.Submit(true),
                "a real hold recovers from a one-sample dip without starting over");
        }

        [Test]
        public void ConfirmedLatchesThroughTheArmDropping()
        {
            var confirm = new RaisedHandConfirm();
            confirm.Submit(true);
            confirm.Submit(true);
            confirm.Submit(true);

            // The player drops their arm the instant the countdown appears; that must not cancel
            // the very thing the gesture just started.
            Assert.IsTrue(confirm.Submit(false));
            Assert.IsTrue(confirm.Confirmed);
            Assert.AreEqual(2, confirm.Streak,
                "the streak keeps telling the truth regardless — winding back, not resetting");
        }

        [Test]
        public void ResetArmsANewConfirm()
        {
            var confirm = new RaisedHandConfirm();
            confirm.Submit(true);
            confirm.Submit(true);
            confirm.Submit(true);
            confirm.Reset();

            Assert.IsFalse(confirm.Confirmed);
            Assert.AreEqual(0, confirm.Streak);
            confirm.Submit(true);
            confirm.Submit(true);
            Assert.IsTrue(confirm.Submit(true), "a reset confirm is a fresh one, not a dead one");
        }
    }
}
