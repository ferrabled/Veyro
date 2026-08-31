using System;
using MotionRunner.Pose;
using MotionRunner.Track;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// T-012's acceptance criterion is a false-positive *rate*, so the gesture rules must be
    /// drivable by synthetic trajectories: stand still, sway, hop, crouch, disappear. Every test
    /// here is one of those trajectories at a fixed 30 Hz — the camera's actual frame rate.
    ///
    /// Trajectories are written in FACE WIDTHS and converted to frame fractions at the last
    /// moment, which is the same order the real pipeline works in reverse. That is what lets the
    /// identical movement be replayed at two distances — Handheld (the phone in a hand, face ~30%
    /// of the frame) and Table (the phone propped up ~2 m away, face ~8%) — and asserted to behave
    /// the same. Before the face-width rewrite it did not: every threshold was a fraction of the
    /// frame, so the same head movement produced a quarter of the signal at table distance and the
    /// runner ignored it.
    public sealed class FaceSteeringTests
    {
        const float Dt = 1f / 30f;
        const float NeutralX = 0.5f;
        const float NeutralY = 0.4f;

        /// The upright frame is the 640x480 request turned portrait: 480 wide, 640 tall.
        const float Aspect = 480f / 640f;

        /// Detector BOX width as a fraction of frame width — what FaceObservation.Size carries.
        /// ~0.30 is a phone held at about two thirds of a metre; ~0.08 is a phone propped up about
        /// two and a half metres away, which is the way the mode is meant to be played and the case
        /// the original tuning silently excluded. (Both were quoted as nearer distances before the
        /// box factor arrived: a box is ~1.35 faces, so a given Size is further away than a
        /// 15 cm-face reading of it suggests.)
        const float Handheld = 0.30f;
        const float Table = 0.08f;

        /// Face widths -> a horizontal offset in frame widths (x and Size share a normalization).
        ///
        /// Note the division by BoxWidthsPerFace, and note that it is the whole reason these tests
        /// did not catch the bug they exist for. The trajectories here are written in ANATOMICAL
        /// face widths — a real head moving a real number of centimetres — while `size` is the
        /// detector's box. Before the factor existed both sides of every threshold were in box
        /// units, so it cancelled out and every test passed while the phone asked for 1.35x the
        /// movement the comments promised. Converting here, at the edge, is what makes a test that
        /// says "0.8 face widths" mean 12 cm of head travel.
        static float Dx(float faceWidths, float size) =>
            faceWidths * size / FaceSizeFilter.BoxWidthsPerFace;

        /// Face widths -> a vertical offset in frame heights (y is normalized to frame height, so
        /// the aspect ratio comes back in).
        static float Dy(float faceWidths, float size) =>
            faceWidths * size * Aspect / FaceSizeFilter.BoxWidthsPerFace;

        static FaceSteering Calibrated(float size = Handheld)
        {
            var steering = new FaceSteering();
            for (int i = 0; i < steering.CalibrationSamples; i++)
                steering.Submit(NeutralX, NeutralY, size, 0.9f, Dt);
            Assert.IsTrue(steering.IsCalibrated, "calibration should complete");
            return steering;
        }

        static void Hold(FaceSteering s, float x, float y, float seconds, float size,
            float score = 0.9f, float dt = Dt)
        {
            for (float t = 0f; t < seconds; t += dt) s.Submit(x, y, size, score, dt);
        }

        /// A calibrated, settled steering at an arbitrary sample rate — the cadence is the point
        /// of several tests below, so it cannot be baked into the fixture.
        static FaceSteering CalibratedAt(float dt, float size = Handheld)
        {
            var steering = new FaceSteering();
            for (int i = 0; i < steering.CalibrationSamples; i++)
                steering.Submit(NeutralX, NeutralY, size, 0.9f, dt);
            Hold(steering, NeutralX, NeutralY, 0.5f, size, dt: dt);
            Assert.IsTrue(steering.IsCalibrated);
            return steering;
        }

        /// One hop: the face rises `riseFaces` face widths over `upSeconds`, then comes back down
        /// over `downSeconds`, sampled at `dt`. Returns how many *separate* jumps fired, so a test
        /// can demand exactly one. `scoreAt` lets a test black out part of the ascent the way
        /// motion blur does on the phone. 0.7 face widths is ~10 cm of head lift — an ordinary
        /// hop, and the same ordinary hop at every distance.
        static int Hop(FaceSteering s, float dt, float size, float riseFaces = 0.7f,
            float upSeconds = 0.2f, float downSeconds = 0.4f, Func<float, float> scoreAt = null)
        {
            int fires = 0;
            bool wasActive = s.IsJumpActive;
            float rise = Dy(riseFaces, size);

            for (float t = 0f; t < upSeconds; t += dt)
                Step(s, NeutralY - rise * (t / upSeconds), size,
                    scoreAt == null ? 0.9f : scoreAt(t), dt, ref fires, ref wasActive);

            for (float t = 0f; t < downSeconds; t += dt)
                Step(s, NeutralY - rise * (1f - t / downSeconds), size, 0.9f, dt,
                    ref fires, ref wasActive);

            return fires;
        }

        static void Step(FaceSteering s, float y, float size, float score, float dt,
            ref int fires, ref bool wasActive)
        {
            s.Submit(NeutralX, y, size, score, dt);
            if (s.IsJumpActive && !wasActive) fires++;
            wasActive = s.IsJumpActive;
        }

        /// Slides the head from one lateral offset to another over `seconds`, the way a player
        /// changing lanes does. Offsets are face widths from neutral, right positive.
        static void Sweep(FaceSteering s, float fromFaces, float toFaces, float seconds, float size,
            float dt = Dt)
        {
            for (float t = 0f; t < seconds; t += dt)
            {
                float faces = fromFaces + (toFaces - fromFaces) * (t / seconds);
                s.Submit(NeutralX + Dx(faces, size), NeutralY, size, 0.9f, dt);
            }
        }

        [Test]
        public void CalibrationSetsNeutralToWhereThePlayerActuallyStands()
        {
            var steering = new FaceSteering();
            for (int i = 0; i < steering.CalibrationSamples; i++)
                steering.Submit(0.62f, 0.35f, Table, 0.9f, Dt);

            Assert.IsTrue(steering.IsCalibrated);
            Assert.AreEqual(0.62f, steering.NeutralX, 1e-3f);
            Assert.AreEqual(0.35f, steering.BaselineY, 1e-3f);
            Assert.AreEqual(Table, steering.CalibratedSize, 1e-3f,
                "the distance the player calibrated at should be recorded");
        }

        [Test]
        public void LowScoreSamplesDoNotCalibrate()
        {
            var steering = new FaceSteering();
            for (int i = 0; i < 100; i++) steering.Submit(0.5f, 0.4f, Handheld, 0.1f, Dt);
            Assert.IsFalse(steering.IsCalibrated);
            Assert.IsFalse(steering.IsTracking);
        }

        [Test]
        public void LeaningRightSteersRightAndSettlesInsideTheClamp()
        {
            var steering = Calibrated();
            Hold(steering, NeutralX + Dx(steering.HalfRangeX, Handheld), NeutralY, 1f, Handheld);
            Assert.Greater(steering.MoveAxis, 0.7f);
            Assert.LessOrEqual(steering.MoveAxis, 1f);
        }

        [Test]
        public void LeaningLeftSteersLeft()
        {
            var steering = Calibrated();
            Hold(steering, NeutralX - Dx(steering.HalfRangeX, Handheld), NeutralY, 1f, Handheld);
            Assert.Less(steering.MoveAxis, -0.7f);
        }

        [Test]
        public void SmallWobbleInsideTheDeadZoneDoesNotSteer()
        {
            var steering = Calibrated();
            for (int i = 0; i < 60; i++)
            {
                float wobble = (i % 2 == 0 ? 1f : -1f) *
                               Dx(steering.HalfRangeX * steering.DeadZone * 0.5f, Handheld);
                steering.Submit(NeutralX + wobble, NeutralY, Handheld, 0.9f, Dt);
            }
            Assert.AreEqual(0f, steering.MoveAxis, 0.05f);
        }

        [Test]
        public void FastUpwardMotionFiresExactlyOneJump()
        {
            var steering = Calibrated();

            // A hop: three quarters of a face width (~11 cm) in ~100 ms, then back down.
            int fires = 0;
            for (int i = 1; i <= 3; i++)
            {
                steering.Submit(NeutralX, NeutralY - Dy(0.25f * i, Handheld), Handheld, 0.9f, Dt);
                if (steering.IsJumpActive) fires++;
            }
            Assert.Greater(fires, 0, "the hop should read as a jump");

            bool stillActiveLater = false;
            for (int i = 0; i < 20; i++)
            {
                steering.Submit(NeutralX, NeutralY - Dy(0.75f - 0.0375f * i, Handheld),
                    Handheld, 0.9f, Dt);
                if (i > 8 && steering.IsJumpActive) stillActiveLater = true;
            }
            Assert.IsFalse(stillActiveLater, "coming back down must not fire a second jump");
        }

        [Test]
        public void SlowSwayNeverJumps()
        {
            // Breathing / idle sway, at both distances: +-0.2 of a face width (~3 cm) over two
            // full seconds, which is a 0.25 s excursion of about 0.16 face widths against a
            // JumpRise of 0.40. Physical sway is the same size whoever is playing and however far
            // away they stand, which is the whole reason the threshold is in face widths.
            foreach (float size in new[] { Handheld, Table })
            {
                var steering = Calibrated(size);
                for (int i = 0; i < 120; i++)
                {
                    float y = NeutralY + Dy(0.2f * (float)Math.Sin(i * Dt * Math.PI), size);
                    steering.Submit(NeutralX, y, size, 0.9f, Dt);
                    Assert.IsFalse(steering.IsJumpActive,
                        "sway at frame " + i + " read as a jump (size " + size + ")");
                }
            }
        }

        [Test]
        public void RefractoryPeriodBlocksImmediateSecondJump()
        {
            var steering = Calibrated();
            Hold(steering, NeutralX, NeutralY, 0.2f, Handheld);

            steering.Submit(NeutralX, NeutralY - Dy(0.8f, Handheld), Handheld, 0.9f, Dt); // sharp hop
            Assert.IsTrue(steering.IsJumpActive);

            // Immediately hop again inside the refractory window: the window must stay shut
            // once the first jump's active period has expired.
            Hold(steering, NeutralX, NeutralY, steering.JumpWindowSeconds + 0.05f, Handheld);
            steering.Submit(NeutralX, NeutralY - Dy(0.8f, Handheld), Handheld, 0.9f, Dt);
            Assert.IsFalse(steering.IsJumpActive, "second hop inside refractory fired");
        }

        [Test]
        public void SustainedCrouchSlides_BriefDipDoesNot()
        {
            var steering = Calibrated();
            float crouch = NeutralY + Dy(steering.SlideDrop + 0.2f, Handheld);

            steering.Submit(NeutralX, crouch, Handheld, 0.9f, Dt);
            Assert.IsFalse(steering.IsSlideActive, "one dipped frame is not a crouch");

            Hold(steering, NeutralX, crouch, 0.3f, Handheld);
            Assert.IsTrue(steering.IsSlideActive, "a held crouch should slide");

            Hold(steering, NeutralX, NeutralY, 0.2f, Handheld);
            Assert.IsFalse(steering.IsSlideActive, "standing back up should end the slide");
        }

        [Test]
        public void LosingTheFaceHoldsTheSteeringItHadAndFiresNothing()
        {
            // REWRITTEN 31 Aug, and it used to assert the opposite (LosingTheFaceDecaysSteeringTo-
            // NeutralAndFiresNothing). The owner's call from live play: "when no face is identified
            // it goes back to the middle; it should stay on the last position identified — if I get
            // too much to the left it makes me lose." Decaying to neutral is decaying INTO THE
            // CENTRE LANE, which is a lane like any other and the one an obstacle is most likely to
            // be in. What has to survive from the old test — and does — is that a lost face cannot
            // *act*: no jump, no slide, no tracking.
            var steering = Calibrated();
            Hold(steering, NeutralX + Dx(steering.HalfRangeX, Handheld), NeutralY, 1f, Handheld);
            Assert.Greater(steering.MoveAxis, 0.7f);
            float held = steering.MoveAxis;

            for (int i = 0; i < 30; i++)
            {
                steering.Submit(0f, 0f, 0f, 0f, Dt); // gone
                Assert.IsFalse(steering.IsJumpActive);
                Assert.IsFalse(steering.IsSlideActive);
            }
            Assert.AreEqual(held, steering.MoveAxis, 1e-6f,
                "a lost face must leave the steering command exactly where it was");
            Assert.IsFalse(steering.IsTracking);
            Assert.IsFalse(steering.HasPosition);
        }

        [Test]
        public void ReappearingAfterLossDoesNotReadTheReturnAsAJump()
        {
            var steering = Calibrated();
            for (int i = 0; i < 15; i++) steering.Submit(0f, 0f, 0f, 0f, Dt);

            // Face pops back in far from the last seen position: with no previous sample there
            // is no velocity, so nothing may fire on the first frame back.
            steering.Submit(NeutralX, NeutralY - Dy(1.2f, Handheld), Handheld, 0.9f, Dt);
            Assert.IsFalse(steering.IsJumpActive, "reacquisition frame fired a jump");
        }

        [Test]
        public void StandingUpFromASlideDoesNotReadAsAJump()
        {
            var steering = Calibrated();
            float drop = steering.SlideDrop + 0.3f;
            Hold(steering, NeutralX, NeutralY + Dy(drop, Handheld), 0.4f, Handheld);
            Assert.IsTrue(steering.IsSlideActive);

            // Stand back up over three frames — fast upward motion, a jump's exact signature.
            for (int i = 1; i <= 3; i++)
                steering.Submit(NeutralX, NeutralY + Dy(drop * (3 - i) / 3f, Handheld),
                    Handheld, 0.9f, Dt);
            for (int i = 0; i < 10; i++)
            {
                steering.Submit(NeutralX, NeutralY, Handheld, 0.9f, Dt);
                Assert.IsFalse(steering.IsJumpActive, "stand-up read as a jump");
            }
        }

        // ---- the jump misses the owner reported from the device ----

        [Test]
        public void AHopFiresExactlyOnceAtEveryFrameRateTheGameCanRun()
        {
            // The bug behind "sometimes the jump is not identified". The rule used to measure a
            // frame-to-frame velocity, while the rig only finishes an observation every second or
            // third frame — so the number it read was the real speed times
            // (observation interval / deltaTime), which is 1x at 30 fps and 3x at 90. The same
            // hop therefore cleared the threshold on a fast frame and missed on a slow one.
            // Travel across a wall-clock window does not care how the samples land.
            foreach (float dt in new[] { 1f / 15f, 1f / 24f, 1f / 30f, 1f / 60f, 1f / 90f })
            {
                var steering = CalibratedAt(dt);
                Assert.AreEqual(1, Hop(steering, dt, Handheld),
                    "one hop should be one jump at " + (int)(1f / dt) + " Hz");
            }
        }

        [Test]
        public void AHopStillFiresWhenTheDetectorLosesTheFaceMidAscent()
        {
            // Motion blur takes BlazeFace under its 0.65 threshold exactly during the fast part
            // of a hop, and FaceDetector reports that as score 0. The old rule dropped its
            // previous sample on every such frame and reported zero velocity on the one after,
            // so the entire ascent went unseen. The take-off sample is still inside the window,
            // so the rise is still there when the face comes back.
            var steering = CalibratedAt(Dt);
            int fires = Hop(steering, Dt, Handheld,
                scoreAt: t => t >= 0.06f && t < 0.15f ? 0f : 0.9f);
            Assert.AreEqual(1, fires, "a hop with a blind patch through the middle should fire");
        }

        [Test]
        public void RepeatingTheSameObservationBetweenDetectorFramesChangesNothing()
        {
            // Exactly what CameraFaceInput does: submit every game frame at 60 Hz while the rig
            // finishes an observation every second one, so half the frames repeat the previous
            // face position.
            const float FrameDt = 1f / 60f;
            var steering = CalibratedAt(FrameDt);
            float rise = Dy(0.7f, Handheld);

            int fires = 0;
            bool wasActive = false;
            float y = NeutralY;

            for (int frame = 0; frame < 60; frame++)
            {
                if (frame % 2 == 0)
                {
                    float t = frame * FrameDt;
                    y = t < 0.2f
                        ? NeutralY - rise * (t / 0.2f)
                        : NeutralY - rise * Math.Max(0f, 1f - (t - 0.2f) / 0.4f);
                }
                Step(steering, y, Handheld, 0.9f, FrameDt, ref fires, ref wasActive);
            }

            Assert.AreEqual(1, fires, "one hop, one jump, whatever the observation cadence");
        }

        [Test]
        public void TwoHopsBackToBackBothFire()
        {
            // The track puts jumpable clusters 6 m apart (TrackMetrics.MinClusterSpacing) at
            // 12-19.7 m/s, and RunnerController's arc is airborne 0.68 s and deaf to jump input
            // the whole time — so the gesture rule must never be the thing that blocks the second
            // jump. Two complete hops in a row, two jumps.
            var steering = CalibratedAt(Dt);
            Assert.AreEqual(1, Hop(steering, Dt, Handheld), "first hop");
            Assert.AreEqual(1, Hop(steering, Dt, Handheld),
                "second hop, straight after the first landed");
        }

        [Test]
        public void ACounterMovementHopStillFires()
        {
            // People dip before they hop. The dip must not cancel the jump that follows it.
            var steering = CalibratedAt(Dt);

            for (int i = 1; i <= 3; i++)
                steering.Submit(NeutralX, NeutralY + Dy(0.4f * i / 3f, Handheld), Handheld, 0.9f, Dt);

            bool fired = false;
            for (int i = 1; i <= 5; i++)
            {
                steering.Submit(NeutralX, NeutralY + Dy(0.4f - 1.1f * i / 5f, Handheld),
                    Handheld, 0.9f, Dt);
                fired |= steering.IsJumpActive;
            }

            Assert.IsTrue(fired, "a hop out of a counter-movement dip should still fire");
        }

        [Test]
        public void BobbingBackUpOutOfAShallowDipIsNotAJump()
        {
            // The false positive a displacement rule invites and a velocity rule could not see: a
            // dip too shallow to be a slide, then a return to standing, is as much net upward
            // travel as a hop. It ends level with where the player stands rather than above it,
            // which is what JumpApexAboveBaseline is for.
            var steering = CalibratedAt(Dt);
            float dip = steering.SlideDrop - 0.05f; // just under a slide

            for (int i = 1; i <= 4; i++)
                steering.Submit(NeutralX, NeutralY + Dy(dip * i / 4f, Handheld), Handheld, 0.9f, Dt);
            Assert.IsFalse(steering.IsSlideActive, "the dip is under SlideDrop — not a slide");

            for (int i = 3; i >= 0; i--)
            {
                steering.Submit(NeutralX, NeutralY + Dy(dip * i / 4f, Handheld), Handheld, 0.9f, Dt);
                Assert.IsFalse(steering.IsJumpActive, "a bob back to standing fired a jump");
            }

            Hold(steering, NeutralX, NeutralY, 0.4f, Handheld);
            Assert.IsFalse(steering.IsJumpActive);
        }

        [Test]
        public void NeutralDriftsTowardWhereThePlayerSettles()
        {
            var steering = Calibrated();
            // The player shuffles a little to one side and stays there. Small on purpose: a
            // sustained deliberate lean (large offset) must NOT recenter, or long curves die.
            Hold(steering, NeutralX + Dx(0.3f, Handheld), NeutralY, 12f, Handheld);
            Assert.Greater(steering.NeutralX, NeutralX + Dx(0.15f, Handheld),
                "neutral should follow a sustained new stance");
        }

        // ---- the table-distance failures the owner reported from the device ----

        [Test]
        public void ALaneLeanCrossesTheEnterThresholdAtEveryDistance()
        {
            // "It is by leaving the phone on the table and being minimum from chest up visible in
            // the camera." A deliberate lean of 0.8 of a face width is ~12 cm of head travel, and
            // it has to commit to a lane — LaneSelector enters at |axis| >= 0.5 — whether the face
            // fills a third of the frame or a twelfth of it. Under the old frame-fraction
            // thresholds this same 12 cm gave ~0.99 handheld and ~0.26 at table distance, which is
            // the runner refusing to leave the middle lane.
            foreach (float size in new[] { Handheld, Table })
            {
                var steering = Calibrated(size);
                Hold(steering, NeutralX + Dx(0.8f, size), NeutralY, 0.6f, size);
                Assert.Greater(steering.MoveAxis, LaneEnter,
                    "a 12 cm lean should commit to a lane at size " + size);
            }
        }

        /// LaneSelector.EnterThreshold and HoldThreshold, restated here so this assembly's tests do
        /// not depend on the Track assembly's constants for a gesture-side assertion.
        const float LaneEnter = 0.5f;
        const float LaneHold = 0.3f;

        [Test]
        public void ADirectLeftToRightSweepReachesTheOppositeLaneAtTableDistance()
        {
            // The owner's exact report: "if I try to move from the left to the right lane
            // directly, it stays in the left/middle one, losing a life as we hit a wall". The lane
            // logic always supported the direct crossing (LaneSelectorTests covers it); what
            // failed was upstream — the axis never got near +0.5, because at table distance
            // reaching it needed ~40 cm of head travel. In face widths the crossing is the same
            // ~19 cm at every distance.
            foreach (float size in new[] { Table, Handheld })
            {
                var steering = Calibrated(size);

                Hold(steering, NeutralX - Dx(1.0f, size), NeutralY, 1f, size);
                Assert.Less(steering.MoveAxis, -LaneEnter, "should start in the left lane");

                // Across to the other side in the time a dodge allows, then a beat to settle:
                // the axis is low-passed at 14 Hz, so it trails a fast sweep by ~70 ms.
                Sweep(steering, -1.0f, 0.8f, 0.3f, size);
                Hold(steering, NeutralX + Dx(0.8f, size), NeutralY, 0.2f, size);

                Assert.Greater(steering.MoveAxis, LaneEnter,
                    "a direct left-to-right sweep should reach the right lane at size " + size);
            }
        }

        [Test]
        public void AHopFiresAtTableDistance()
        {
            // ~10 cm of head lift with the phone two metres away. In frame heights that is 0.042 —
            // under the old 0.045 threshold, which is why hops stopped being seen as soon as the
            // player stepped back. In face widths it is 0.7 against a threshold of 0.40.
            var steering = CalibratedAt(Dt, Table);
            Assert.AreEqual(1, Hop(steering, Dt, Table), "a real hop at table distance should fire");
        }

        [Test]
        public void TheSameGestureProducesTheSameAxisHandheldAndOnATable()
        {
            // The invariant the whole rewrite exists for: identical head movement, identical
            // output, four times the distance.
            var near = Calibrated(Handheld);
            var far = Calibrated(Table);

            foreach (float faces in new[] { 0.1f, 0.3f, 0.5f, 0.8f, 1.1f, -0.8f })
            {
                Hold(near, NeutralX + Dx(faces, Handheld), NeutralY, 0.5f, Handheld);
                Hold(far, NeutralX + Dx(faces, Table), NeutralY, 0.5f, Table);
                Assert.AreEqual(near.MoveAxis, far.MoveAxis, 2e-3f,
                    faces + " face widths should steer the same at both distances");
            }
        }

        [Test]
        public void MovingAwayMidRunRescalesGesturesInsteadOfBreakingThem()
        {
            // Why the scale is recomputed from the smoothed size every sample instead of being
            // captured once at calibration: a player who rolls the chair back keeps the same
            // physical gesture. A frozen reference would quietly halve their steering.
            var steering = Calibrated(Handheld);

            // Walk from 0.30 to 0.08 over a second and a half, then settle.
            for (int i = 0; i < 45; i++)
            {
                float size = Handheld + (Table - Handheld) * (i / 44f);
                steering.Submit(NeutralX, NeutralY, size, 0.9f, Dt);
            }
            Hold(steering, NeutralX, NeutralY, 1.5f, Table);
            Assert.AreEqual(Table, steering.FaceSize, 5e-3f, "the size filter should have caught up");

            Hold(steering, NeutralX + Dx(0.8f, Table), NeutralY, 0.6f, Table);
            Assert.Greater(steering.MoveAxis, LaneEnter,
                "the same 12 cm lean should still reach a lane after backing away");
        }

        [Test]
        public void AnAbsurdOrMissingSizeCannotBlowUpTheAxis()
        {
            // The size is a divisor, so it is exactly the place where one bad sample becomes
            // full-lock steering. A face reported with no width at all must leave the last good
            // scale standing.
            var steering = Calibrated(Table);
            float lean = NeutralX + Dx(0.15f, Table); // well inside the dead zone

            for (int i = 0; i < 30; i++) steering.Submit(lean, NeutralY, 0f, 0.9f, Dt);
            Assert.AreEqual(0f, steering.MoveAxis, 0.05f, "a zero size must not amplify a wobble");

            for (int i = 0; i < 30; i++) steering.Submit(lean, NeutralY, float.NaN, 0.9f, Dt);
            Assert.IsFalse(float.IsNaN(steering.MoveAxis), "a NaN size leaked into the axis");
            Assert.AreEqual(0f, steering.MoveAxis, 0.05f);
        }

        [Test]
        public void SteeringKnowsWhenThePlayerIsOutOfRange()
        {
            var near = Calibrated(Handheld);
            Assert.IsFalse(near.IsTooFar);

            var far = Calibrated(FaceSizeFilter.MinPlayableSize * 0.5f);
            Assert.IsTrue(far.IsTooFar, "half the minimum playable size should read as too far");
        }

        // ---- the side-step at a metre: the same rules stated in centimetres --------------------
        //
        // The tests above are written in face widths, which is the unit the code thinks in — and
        // that is exactly why they all passed while the phone asked for a third more movement than
        // the design says. A face width was silently a *box* width on both sides of every
        // assertion. Everything below is written in CENTIMETRES OF REAL HEAD TRAVEL and converts
        // once, through the same two constants the pipeline uses, so a wrong BoxWidthsPerFace
        // fails here instead of on someone's living-room floor.

        /// The anatomical face width every threshold in FaceSteering is written against.
        const float FaceCm = 15f;

        /// The detector box a FaceCm face produces, as a fraction of the frame width, at a given
        /// distance. The upright frame spans roughly one metre of world per metre of distance (a
        /// ~53 deg horizontal field of view), so the box covers its own centimetres out of the
        /// 100 x metres the frame is wide.
        static float SizeAt(float metres) =>
            FaceCm * FaceSizeFilter.BoxWidthsPerFace / (100f * metres);

        /// A real sideways head movement of `cm`, as an offset in frame widths, at that distance.
        static float StepCm(float cm, float metres) => cm / (100f * metres);

        /// The distances the mode is played at: phone in a hand, phone propped on a table, and the
        /// far end of what BlazeFace can hold.
        static readonly float[] Distances = { 0.6f, 1f, 1.5f, 2f, 2.5f };

        [Test]
        public void ATwelveCentimetreSideStepEntersALaneAtEveryDistance()
        {
            // The owner's actual gesture: standing about a metre back from a propped phone and
            // side-stepping — translating the whole body, not tilting the head. 12 cm is a modest
            // half-step; a real one is 20-30 cm. Under the box-as-face reading this needed 17 cm
            // and a comfortable side-step landed right on the edge, which is the whole report.
            foreach (float metres in Distances)
            {
                float size = SizeAt(metres);
                var steering = Calibrated(size);
                Hold(steering, NeutralX + StepCm(12f, metres), NeutralY, 0.6f, size);
                Assert.Greater(steering.MoveAxis, LaneEnter,
                    "12 cm of side-step should commit to a lane at " + metres + " m");
            }
        }

        [Test]
        public void SixCentimetresOfSwayIsNotALaneChangeAtAnyDistance()
        {
            // The other half, and the one a bigger gesture budget is spent against: shifting
            // weight, breathing, turning to look at the screen. It must not steer, and it must not
            // even hold a lane it did not enter.
            foreach (float metres in Distances)
            {
                float size = SizeAt(metres);
                var steering = Calibrated(size);
                Hold(steering, NeutralX + StepCm(6f, metres), NeutralY, 0.6f, size);
                Assert.Less(steering.MoveAxis, LaneHold,
                    "6 cm of sway should not steer at " + metres + " m");
            }
        }

        [Test]
        public void ALaneStartsAtTheDesignedNineAndAHalfCentimetres()
        {
            // The pin on BoxWidthsPerFace itself. The design says entering a lane costs ~9.5 cm of
            // head travel; dividing by the detector's box instead of by a face made it ~12.8 cm.
            // Bracketing the boundary is what makes that a test rather than a comment: 10.5 cm has
            // to be enough and 8.5 cm has to not be, which no factor other than ~1.35 satisfies.
            const float metres = 1f;
            float size = SizeAt(metres);

            var enough = Calibrated(size);
            Hold(enough, NeutralX + StepCm(10.5f, metres), NeutralY, 0.6f, size);
            Assert.Greater(enough.MoveAxis, LaneEnter, "10.5 cm should be past the lane boundary");

            var notEnough = Calibrated(size);
            Hold(notEnough, NeutralX + StepCm(8.5f, metres), NeutralY, 0.6f, size);
            Assert.Less(notEnough.MoveAxis, LaneEnter, "8.5 cm should be short of it");
        }

        [Test]
        public void CrossingTheRoadInOneMoveCostsAboutTwentyCentimetresAtAMetre()
        {
            // "If I try to move from the left to the right lane directly, it stays in the
            // left/middle one" — restated at the distance it was reported from. Holding the left
            // lane and stepping 24 cm to the other side, inside the 0.3 s a dodge allows.
            const float metres = 1f;
            float size = SizeAt(metres);
            var steering = Calibrated(size);

            Hold(steering, NeutralX - StepCm(12f, metres), NeutralY, 1f, size);
            Assert.Less(steering.MoveAxis, -LaneEnter, "should start in the left lane");

            for (float t = 0f; t < 0.3f; t += Dt)
            {
                float cm = -12f + 24f * (t / 0.3f);
                steering.Submit(NeutralX + StepCm(cm, metres), NeutralY, size, 0.9f, Dt);
            }
            Hold(steering, NeutralX + StepCm(12f, metres), NeutralY, 0.2f, size);

            Assert.Greater(steering.MoveAxis, LaneEnter,
                "24 cm of travel should reach the far lane");
        }

        // ---- dropouts during the movement itself ----------------------------------------------

        [Test]
        public void ADropoutInTheMiddleOfASideStepDoesNotCancelTheLaneChange()
        {
            // The second half of "a side-step is not recognised". A body translating sideways blurs,
            // and a blurred face at playing distance goes under BlazeFace's 0.65 threshold for two
            // or three frames — which FaceDetector reports as no face at all. Decaying the axis
            // from the first lost frame took a committed lean back under the 0.3 that holds a lane
            // in about 0.17 s, so the runner gave the lane back mid-step and the player saw
            // nothing happen.
            const float metres = 1f;
            float size = SizeAt(metres);
            var steering = Calibrated(size);

            Hold(steering, NeutralX + StepCm(12f, metres), NeutralY, 0.5f, size);
            Assert.Greater(steering.MoveAxis, LaneEnter);

            for (int i = 0; i < 5; i++) steering.Submit(0f, 0f, 0f, 0f, Dt); // ~0.17 s of blur
            Assert.Greater(steering.MoveAxis, LaneHold,
                "a blur gap in the middle of the movement handed the lane back");
        }

        [Test]
        public void SteppingOutOfFrameKeepsTheLaneTheRunnerWasIn()
        {
            // REWRITTEN 31 Aug from TheLossHoldIsBoundedSoWalkingAwayStillGivesTheControlsBack,
            // which pinned the bounded 0.28 s grace plus a decay to neutral. The bound is exactly
            // what the owner asked to be removed, so the old assertion is now the bug and this is
            // its replacement: the hold is UNBOUNDED, and the thing that has to be true is stated
            // in the unit the complaint was made in — LANES, not axis magnitude, via LaneSelector's
            // own rule. (This assembly's other gesture tests deliberately restate LaneEnter /
            // LaneHold locally rather than depend on the Track assembly; this one takes the
            // dependency on purpose, because "the runner must stay in its lane" is the whole claim
            // and paraphrasing it as a float comparison is how the claim would rot.)
            const float metres = 1f;
            float size = SizeAt(metres);
            var steering = Calibrated(size);

            Hold(steering, NeutralX - StepCm(12f, metres), NeutralY, 0.6f, size);
            int lane = LaneSelector.LaneFor(steering.MoveAxis, 0);
            Assert.AreEqual(-1, lane, "12 cm to the left should be the left lane");

            // Out of frame for three whole seconds — an order of magnitude past the old grace
            // window, and past the 0.35 s CameraFaceInput calls a stale observation.
            for (int i = 0; i < 90; i++)
            {
                steering.Submit(0f, 0f, 0f, 0f, Dt);
                lane = LaneSelector.LaneFor(steering.MoveAxis, lane);
                Assert.AreEqual(-1, lane, "the lane was handed back at lost frame " + i);
                Assert.IsFalse(steering.IsJumpActive, "a lost face fired a jump");
                Assert.IsFalse(steering.IsSlideActive, "a lost face fired a slide");
            }

            Assert.IsFalse(steering.IsTracking);
            Assert.IsFalse(steering.HasPosition);
        }

        [Test]
        public void ReacquiringSomewhereElseSteersToWhereThePlayerNowStands()
        {
            // The other half of the unbounded hold, and what makes it safe: the mapping from face
            // position to axis is ABSOLUTE, so coming back has nothing to unwind. A player who held
            // the right lane, walked out of frame and came back standing on the left is steered
            // left — the frozen command is replaced by a measurement, not blended with one.
            const float metres = 1f;
            float size = SizeAt(metres);
            var steering = Calibrated(size);

            Hold(steering, NeutralX + StepCm(12f, metres), NeutralY, 0.6f, size);
            Assert.Greater(steering.MoveAxis, LaneEnter, "should start in the right lane");

            Hold(steering, 0f, 0f, 2f, 0f, score: 0f);
            Assert.Greater(steering.MoveAxis, LaneEnter, "the hold should have kept the lane");

            // Back in frame on the other side. The axis is low-passed at 14 Hz, so it crosses the
            // road in about 0.1 s; a quarter of a second is a settled reading.
            Hold(steering, NeutralX - StepCm(12f, metres), NeutralY, 0.25f, size);
            Assert.Less(steering.MoveAxis, -LaneEnter,
                "reacquisition should snap to the absolute mapping, not to the held command");
            Assert.IsTrue(steering.IsTracking);
            Assert.IsTrue(steering.IsCalibrated, "a loss must not throw the calibration away");
        }

        [Test]
        public void TheTwoTiersReportThemselvesSoAHoldIsDistinguishableFromABlur()
        {
            // What the overlay and the telemetry line read to tell three situations apart, and the
            // reason there are two flags rather than one: a confident face, a real face the detector
            // is unsure of (still followed), and nothing at all (frozen).
            var steering = Calibrated();

            Hold(steering, NeutralX, NeutralY, 0.2f, Handheld, score: 0.9f);
            Assert.IsTrue(steering.IsTracking, "0.9 is a confident face");
            Assert.IsTrue(steering.HasPosition);

            Hold(steering, NeutralX, NeutralY, 0.2f, Handheld, score: 0.5f);
            Assert.IsFalse(steering.IsTracking, "0.5 is not confident enough to act on");
            Assert.IsTrue(steering.HasPosition, "0.5 is still a position worth following");

            Hold(steering, NeutralX, NeutralY, 0.2f, Handheld, score: 0.3f);
            Assert.IsFalse(steering.IsTracking, "0.3 is noise");
            Assert.IsFalse(steering.HasPosition, "0.3 is noise");
        }

        // ---- the side-lane hop: two tiers of detector score -----------------------------------
        //
        // The owner's second report, 31 Aug: "when jumping in one of the two side lanes, it is not
        // recognised that well; I sometimes fail because of that." A hop is the worst case for
        // BlazeFace — the fast part of the ascent is where a face at playing distance blurs past its
        // score threshold — and until now FaceDetector answered a sub-threshold frame with
        // `default`, destroying the POSITION as well as the confidence. The apex is both the only
        // sample where the rise is large and the sample most likely to be thrown away, so the jump
        // went missing. These tests are written in samples-at-a-score because that is the shape of
        // the failure.

        /// The score of a blurred-but-real frame: between MinPositionScore and MinScore, which is
        /// the band the detector now reports instead of swallowing.
        const float Blurred = 0.5f;

        /// Below MinPositionScore — an empty room, which measured 0.09-0.12 on device.
        const float Noise = 0.3f;

        /// One hop performed while standing `offsetFaces` face widths to one side. `scoreAt(t)` is
        /// the detector score at that point in the trajectory (t runs 0 .. up+down), so a test can
        /// black out the apex the way motion blur does; `each` runs after every submit, which is how
        /// a test asserts the lane was never handed back mid-air. Returns separate jumps fired.
        static int SideLaneHop(FaceSteering s, float size, float offsetFaces,
            Func<float, float> scoreAt, float riseFaces = 0.7f, float upSeconds = 0.2f,
            float downSeconds = 0.4f, Action<FaceSteering> each = null)
        {
            int fires = 0;
            bool wasActive = s.IsJumpActive;
            float x = NeutralX + Dx(offsetFaces, size);
            float rise = Dy(riseFaces, size);

            for (float t = 0f; t < upSeconds + downSeconds; t += Dt)
            {
                float up = t < upSeconds
                    ? t / upSeconds
                    : 1f - (t - upSeconds) / downSeconds;
                s.Submit(x, NeutralY - rise * up, size, scoreAt(t), Dt);
                if (s.IsJumpActive && !wasActive) fires++;
                wasActive = s.IsJumpActive;
                each?.Invoke(s);
            }

            return fires;
        }

        /// A third of the samples blurred, the apex among them — one frame early in the ascent plus
        /// the five around the top, which is 6 of the 18 samples a 0.6 s hop takes at 30 Hz.
        static float BlurredApex(float t) =>
            (t >= 0.03f && t < 0.06f) || (t >= 0.13f && t < 0.27f) ? Blurred : 0.9f;

        /// Calibrated, then settled in a side lane at `offsetFaces` — a player who has already
        /// committed to the lane, which is the situation both of the owner's reports are about.
        static FaceSteering InSideLane(float offsetFaces, float size = Handheld)
        {
            var steering = Calibrated(size);
            Hold(steering, NeutralX + Dx(offsetFaces, size), NeutralY, 0.5f, size);
            Assert.Greater(steering.MoveAxis, LaneEnter, "the side lane should be committed to");
            return steering;
        }

        [Test]
        public void ASideLaneHopFiresThroughTheBlurAndKeepsTheLane()
        {
            // Both of the owner's 31 Aug reports in one trajectory: standing 1.1 face widths off
            // neutral (a committed side lane) and hopping, with a third of the samples — the apex
            // included — at a score the detector used to throw away.
            //
            // Why the fix had to reach the trigger and not just the history: work the numbers at
            // this cadence and the confident frame after the blur measures a rise of ~0.175 face
            // widths against a JumpRise of 0.40, because the take-off sample has aged out of the
            // 0.25 s window by then. Keeping the blurred samples does not help — they sit above the
            // take-off, so they never become the window's low point. Only a rise NOTICED on a
            // blurred sample and CONFIRMED by the next confident one fires this hop.
            var steering = InSideLane(1.1f);

            int fires = SideLaneHop(steering, Handheld, 1.1f, BlurredApex,
                each: s =>
                {
                    Assert.Greater(s.MoveAxis, LaneHold,
                        "the blur pulled the runner out of the side lane mid-hop");
                    Assert.Greater(s.Deflection, 0.9f,
                        "the overlay lost the player's position during the blur");
                });

            Assert.AreEqual(1, fires, "a side-lane hop through a blurred apex should fire once");
        }

        [Test]
        public void TheSameSideLaneTrajectoryStandingStillFiresNothing()
        {
            // The control for the test above, and the false-positive half of the two-tier design:
            // identical position, identical score pattern, no hop — just 2 cm of sway. Nothing may
            // fire, or "recognise the hop better" would just mean "jump more often".
            var steering = InSideLane(1.1f);

            int fires = SideLaneHop(steering, Handheld, 1.1f, BlurredApex, riseFaces: 0.15f,
                each: s => Assert.IsFalse(s.IsJumpActive, "sway in a side lane fired a jump"));

            Assert.AreEqual(0, fires);
            Assert.Greater(steering.MoveAxis, LaneHold, "the lane should still be held");
        }

        [Test]
        public void BlurredFramesAloneNeverFireAJump()
        {
            // The discipline the position tier is only acceptable with. A whole hop seen at nothing
            // better than 0.5 arms the rise but never gets the confident frame that confirms a real
            // face is there, so it fires nothing — while still following the player's position the
            // entire time, which is the point of keeping the frames at all.
            var steering = InSideLane(1.1f);

            int fires = SideLaneHop(steering, Handheld, 1.1f, _ => Blurred);

            Assert.AreEqual(0, fires, "a jump fired without a single confident frame");
            Assert.IsFalse(steering.IsTracking, "0.5 is not tracking");
            Assert.Greater(steering.MoveAxis, LaneHold, "and the lane is held either way");
        }

        [Test]
        public void AConfidentSightingVouchesForTheBlurredFramesRightBehindIt()
        {
            // The rule that makes the marginal tier safe, in both directions. A blur streak that
            // follows a confident sighting IS the player and is followed; the same score sustained
            // long enough that no confident frame is behind it any more is not distinguishable from
            // furniture and becomes a loss — at which point the lane is simply held.
            const float metres = 1f;
            float size = SizeAt(metres);
            var steering = Calibrated(size);
            float side = NeutralX + StepCm(12f, metres);

            Hold(steering, side, NeutralY, 0.6f, size);
            Assert.Greater(steering.MoveAxis, LaneEnter);

            Hold(steering, side, NeutralY, steering.PositionCarrySeconds * 0.5f, size,
                score: Blurred);
            Assert.IsTrue(steering.HasPosition, "a blur streak right after a confident frame is a face");
            Assert.IsFalse(steering.IsTracking, "…but not a confident one");

            Hold(steering, side, NeutralY, 1f, size, score: Blurred);
            Assert.IsFalse(steering.HasPosition,
                "the vouch has to expire, or a sustained marginal score would steer forever");
            Assert.Greater(steering.MoveAxis, LaneHold, "and from there the lane is simply held");
        }

        [Test]
        public void DetectorNoiseWithNoConfidentFaceBehindItIsNeverFollowed()
        {
            // Measured on device, 31 Aug, and the reason PositionCarrySeconds exists at all: the
            // shipping phone pointed at an ordinary empty room reported raw detector scores of
            // 0.42-0.47, and one of those frames cleared MinPositionScore. (An empty ceiling the
            // same week scored 0.09-0.12 — the noise level is whatever is in shot, so no fixed
            // threshold can be trusted to exclude it.) The box also lands somewhere different every
            // frame, which is what steering off it would look like.
            var steering = new FaceSteering();

            for (int i = 0; i < 200; i++)
                steering.Submit(i % 2 == 0 ? 0.2f : 0.8f, 0.5f, 0.15f, 0.47f, Dt);

            Assert.IsFalse(steering.IsCalibrated, "noise must never calibrate a neutral pose");
            Assert.IsFalse(steering.HasPosition, "noise must never read as a position");
            Assert.IsFalse(steering.IsTracking);
            Assert.AreEqual(0f, steering.MoveAxis, 1e-6f, "noise must never steer");
        }

        [Test]
        public void AHopBlackedOutBelowTheNoiseFloorHoldsTheLaneAndInventsNothing()
        {
            // Pure noise is still a loss, deliberately, and this test pins what that costs: a hop
            // whose apex falls under 0.45 does not fire, because there is no evidence a face was
            // there. That is the right trade — the alternative is a jump conjured out of detector
            // noise, which spends a life — and the compensation is the other assertion here: the
            // lane is held throughout, so the failed hop no longer also loses the lane. Before this
            // session both went wrong at once, which is why the owner's two reports are one bug.
            var steering = InSideLane(1.1f);

            int fires = SideLaneHop(steering, Handheld, 1.1f,
                t => t >= 0.13f && t < 0.27f ? Noise : 0.9f,
                each: s => Assert.Greater(s.MoveAxis, LaneHold,
                    "a blackout at the apex handed the side lane back"));

            Assert.AreEqual(0, fires, "nothing may be invented from sub-floor frames");
        }

        [Test]
        public void ABlurStreakInTheMiddleOfACrouchDoesNotStandTheRunnerUp()
        {
            // The slide is a state, not a trigger, so it freezes on a blurred frame for the same
            // reason the axis does: three frames of blur is not a player standing up. A real loss
            // still ends it, because a slide is a pose the game has to be able to stop believing in.
            var steering = Calibrated();
            float crouch = NeutralY + Dy(steering.SlideDrop + 0.2f, Handheld);

            Hold(steering, NeutralX, crouch, 0.3f, Handheld);
            Assert.IsTrue(steering.IsSlideActive, "a held crouch should slide");

            for (int i = 0; i < 3; i++) steering.Submit(NeutralX, crouch, Handheld, Blurred, Dt);
            Assert.IsTrue(steering.IsSlideActive, "a blur streak cancelled a held crouch");

            steering.Submit(0f, 0f, 0f, Noise, Dt);
            Assert.IsFalse(steering.IsSlideActive, "a real loss must end the slide");
        }

        [Test]
        public void ASlideCannotStartOutOfBlurredFramesAlone()
        {
            // The other direction: blurred frames keep a slide, they never begin one. SlideHold is
            // 0.10 s, so half a second of crouching at a marginal score is five times over the bar
            // and must still not fire.
            var steering = Calibrated();
            float crouch = NeutralY + Dy(steering.SlideDrop + 0.2f, Handheld);

            Hold(steering, NeutralX, crouch, 0.5f, Handheld, score: Blurred);
            Assert.IsFalse(steering.IsSlideActive, "a marginal crouch started a slide by itself");
        }

        // ---- what the overlay reads ------------------------------------------------------------

        [Test]
        public void DeflectionReportsThePlayerWhereMoveAxisReportsTheRunner()
        {
            // The overlay exists to separate "I did not move far enough" from "I moved and the game
            // did not follow", and it cannot do that from MoveAxis alone: inside the dead zone the
            // axis is zero by design, and a panel driven by it would show a player who has moved as
            // standing perfectly still.
            const float metres = 1f;
            float size = SizeAt(metres);
            var steering = Calibrated(size);

            Hold(steering, NeutralX + StepCm(1.5f, metres), NeutralY, 0.5f, size);
            Assert.AreEqual(0f, steering.MoveAxis, 1e-3f, "1.5 cm is inside the dead zone");
            Assert.Greater(steering.Deflection, 0.05f, "the overlay must still see the movement");

            Hold(steering, NeutralX - StepCm(30f, metres), NeutralY, 0.5f, size);
            Assert.AreEqual(-1f, steering.Deflection, 1e-3f, "a big step clamps at full deflection");
        }

        [Test]
        public void LiftIsPositiveUpAndNegativeDown()
        {
            const float metres = 1f;
            float size = SizeAt(metres);
            var steering = Calibrated(size);

            // A crouch: below the calibrated baseline, so y increases and Lift goes negative.
            Hold(steering, NeutralX, NeutralY + Dy(steering.SlideDrop * 0.5f, size), 0.3f, size);
            Assert.Less(steering.Lift, -0.3f, "a crouch should read as negative lift");

            // And back up past it.
            Hold(steering, NeutralX, NeutralY - Dy(steering.SlideDrop * 0.5f, size), 0.3f, size);
            Assert.Greater(steering.Lift, 0.3f, "a raised head should read as positive lift");
        }

        [Test]
        public void LosingTheFaceLetsGoOfLiftTooRatherThanLatchingIt()
        {
            // Lift is held through the grace window like Deflection - a blur gap at the top of a
            // hop must not flatten the glyph - and released after it, for the same reason the axis
            // is. Left latched, a face lost mid-hop kept lift at +1 forever: every telemetry line
            // printed `lift=+1.000` and the overlay drew the stickman airborne right next to its
            // own "no face" caption (seen on device, 31 Aug).
            const float metres = 1f;
            float size = SizeAt(metres);
            var steering = Calibrated(size);

            Hold(steering, NeutralX, NeutralY - Dy(steering.SlideDrop * 0.8f, size), 0.3f, size);
            Assert.Greater(steering.Lift, 0.5f, "a raised head reads as positive lift");

            Hold(steering, 0f, 0f, steering.LossGraceSeconds * 0.5f, 0f, score: 0f);
            Assert.Greater(steering.Lift, 0.5f, "inside the grace window the lift holds");

            Hold(steering, 0f, 0f, 1.5f, 0f, score: 0f);
            Assert.AreEqual(0f, steering.Lift, 0.02f,
                "a face that is gone must not leave the glyph hanging in the air");
        }

        [Test]
        public void ResetForgetsTheOverlayOutputsToo()
        {
            var steering = Calibrated(Handheld);
            Hold(steering, NeutralX + Dx(1f, Handheld), NeutralY, 0.5f, Handheld);
            Assert.Greater(steering.Deflection, 0.5f);

            steering.Reset();
            Assert.AreEqual(0f, steering.Deflection, 1e-6f);
            Assert.AreEqual(0f, steering.Lift, 1e-6f);
            Assert.AreEqual(0f, steering.MoveAxis, 1e-6f);
        }
    }
}
