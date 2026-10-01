using System;
using System.Collections.Generic;
using MotionRunner.Pose;
using MotionRunner.Track;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// The hop-twice resume confirm, driven through the REAL FaceSteering jump rule by synthetic
    /// trajectories, in the FaceSteeringTests style.
    ///
    /// Half of these are the happy path at the distances and frame rates the game runs at. The
    /// other half are the reason the class exists: a player walking back into frame, which makes
    /// the bare jump detector fire (the premise is asserted, so a test cannot pass by being too
    /// gentle to matter), and must never resume the run.
    ///
    /// Walks are written in the world — metres from the lens, metres above it — and projected with
    /// the same camera model FaceSizeFilter's constants are written in: the 480x640 portrait frame
    /// spans about one metre of world per metre of distance across, and the detector's box is
    /// ~20 cm wide (a 15 cm face times BoxWidthsPerFace). Hops and stances are written in face
    /// widths, like FaceSteeringTests.
    public sealed class DoubleHopConfirmTests
    {
        const float Frame60 = 1f / 60f;
        const float Frame30 = 1f / 30f;
        const float NeutralX = 0.5f;
        const float NeutralY = 0.4f;
        const float Aspect = 480f / 640f;

        /// Detector box width as a fraction of frame width: a phone in the hand, and a phone
        /// propped up across the room — the way camera mode is meant to be played.
        const float Handheld = 0.30f;
        const float Table = 0.08f;

        /// A 15 cm face's detector box, in metres (FaceSizeFilter.BoxWidthsPerFace).
        const float BoxMetres = 0.15f * FaceSizeFilter.BoxWidthsPerFace;

        static float Dx(float faceWidths, float size) =>
            faceWidths * size / FaceSizeFilter.BoxWidthsPerFace;

        static float Dy(float faceWidths, float size) =>
            faceWidths * size * Aspect / FaceSizeFilter.BoxWidthsPerFace;

        /// A FaceSteering fed one observation per frame with the confirm ticked straight after it
        /// — exactly the order PauseMenu runs them in.
        ///
        /// Plus a second, BARE steering fed the identical observations and never reset by
        /// anybody: that is the run's own steering during the pause (RunFlow resets it once, at
        /// BeginResume), i.e. the plain jump rule with nothing standing in front of it. Its jump
        /// edges are what the walk-in tests' premise is asserted on — the confirm's own steering
        /// cannot serve, because resetting it is part of how the confirm refuses.
        sealed class Player
        {
            public readonly FaceSteering Steering = new FaceSteering();
            public readonly FaceSteering Bare = new FaceSteering();
            public readonly List<FaceSteering> AlsoFed = new List<FaceSteering>();
            public readonly DoubleHopConfirm Confirm;
            public readonly float Dt;
            public int JumpEdges;
            public bool EverConfirmed;
            bool _bareWasJumping;

            public Player(float dt = Frame60)
            {
                Dt = dt;
                Confirm = new DoubleHopConfirm(Steering);
            }

            public void Step(float x, float y, float size, float score = 0.9f)
            {
                Steering.Submit(x, y, size, score, Dt);
                Bare.Submit(x, y, size, score, Dt);
                foreach (FaceSteering other in AlsoFed) other.Submit(x, y, size, score, Dt);
                if (Bare.IsJumpActive && !_bareWasJumping) JumpEdges++;
                _bareWasJumping = Bare.IsJumpActive;
                EverConfirmed |= Confirm.Tick(Dt);
            }

            public void Stand(float seconds, float size, float x = NeutralX, float y = NeutralY)
            {
                for (float t = 0f; t < seconds; t += Dt) Step(x, y, size);
            }

            public void Gone(float seconds)
            {
                for (float t = 0f; t < seconds; t += Dt) Step(0f, 0f, 0f, 0f);
            }

            /// One hop in place: up `riseFaces` face widths over `upSeconds`, back down over
            /// `downSeconds` (FaceSteeringTests.Hop's shape). 0.7 face widths is ~10 cm of head
            /// lift — an ordinary hop.
            public void Hop(float size, float riseFaces = 0.7f, float upSeconds = 0.2f,
                float downSeconds = 0.35f, Func<float, float> scoreAt = null, float x = NeutralX)
            {
                float rise = Dy(riseFaces, size);
                for (float t = 0f; t < upSeconds; t += Dt)
                    Step(x, NeutralY - rise * (t / upSeconds), size,
                        scoreAt == null ? 0.9f : scoreAt(t));
                for (float t = 0f; t < downSeconds; t += Dt)
                    Step(x, NeutralY - rise * (1f - t / downSeconds), size);
                Step(x, NeutralY, size);
            }

            /// The same ordinary hop as Hop, from wherever the player is standing rather than from
            /// the fixture's neutral.
            public void HopFrom(float x, float y, float size, float riseFaces = 0.7f)
            {
                float rise = Dy(riseFaces, size);
                for (float t = 0f; t < 0.2f; t += Dt) Step(x, y - rise * t / 0.2f, size);
                for (float t = 0f; t < 0.35f; t += Dt) Step(x, y - rise * (1f - t / 0.35f), size);
                Step(x, y, size);
            }

            /// Stands long enough to arm, and says so.
            public void Settle(float size, float x = NeutralX)
            {
                Stand(DoubleHopConfirm.SettleSeconds + 0.5f, size, x);
                Assert.AreEqual(DoubleHopConfirm.Phase.Armed, Confirm.State,
                    "standing still should arm the confirm");
            }

            /// The camera's view of a head `distance` m from the lens, `up` m above it and `side`
            /// m to the player's right (the upright frame is already selfie-mirrored upstream).
            public void See(float distance, float up, float side)
            {
                float across = distance;          // metres of world across the frame
                float tall = distance / Aspect;   // and down it
                Step(0.5f + side / across, 0.5f - up / tall, BoxMetres / across);
            }
        }

        // ---- the happy path ------------------------------------------------------------------

        [Test]
        public void StandStillThenHopTwice_Confirms_AtEveryDistanceAndFrameRate()
        {
            foreach (float dt in new[] { Frame30, Frame60, 1f / 90f })
            foreach (float size in new[] { Handheld, Table })
            {
                var p = new Player(dt);
                p.Settle(size);

                p.Hop(size);
                Assert.AreEqual(1, p.Confirm.Hops, $"first hop should count ({size}, {1f / dt:F0} Hz)");
                Assert.IsFalse(p.Confirm.Confirmed, "one hop is not a confirm");

                p.Stand(0.1f, size);
                p.Hop(size);
                Assert.IsTrue(p.Confirm.Confirmed, $"two hops should confirm ({size}, {1f / dt:F0} Hz)");
            }
        }

        [Test]
        public void HopsAreJudgedAgainstWhereThePlayerStands_NotWhereTheyWalkedIn()
        {
            // The recalibration: arrive high in the frame (a player standing closer than before, or
            // taller than the last one), settle, and the baseline is HERE — so an ordinary hop from
            // this spot is a hop, and this spot itself is not "above the baseline".
            var p = new Player();
            p.Stand(0.3f, Table, y: NeutralY - Dy(1.5f, Table));
            p.Stand(DoubleHopConfirm.SettleSeconds + 0.5f, Table);
            Assert.AreEqual(DoubleHopConfirm.Phase.Armed, p.Confirm.State);
            Assert.AreEqual(NeutralY, p.Steering.BaselineY, Dy(0.05f, Table),
                "the settle should leave the baseline where the player now stands");

            p.Hop(Table);
            p.Hop(Table);
            Assert.IsTrue(p.Confirm.Confirmed);
        }

        [Test]
        public void AHopWithTheFaceBlurredMidAscentStillCounts()
        {
            // The detector loses the face through the fast part of a hop (FaceSteeringTests'
            // AHopStillFiresWhenTheDetectorLosesTheFaceMidAscent) — and the pause screen must not
            // treat that blink as the player leaving.
            var p = new Player(Frame30);
            p.Settle(Table);
            Func<float, float> blink = t => t >= 0.06f && t < 0.15f ? 0f : 0.9f;
            p.Hop(Table, scoreAt: blink);
            p.Stand(0.1f, Table);
            p.Hop(Table, scoreAt: blink);
            Assert.IsTrue(p.Confirm.Confirmed);
        }

        [Test]
        public void TheConfirmLatchesUntilRestart_AndRestartRecalibrates()
        {
            var p = new Player();
            p.Settle(Handheld);
            p.Hop(Handheld);
            p.Hop(Handheld);
            Assert.IsTrue(p.Confirm.Confirmed);

            // The player drops their arms, walks, leaves — the confirm has been spent, not undone.
            p.Gone(2f);
            Assert.IsTrue(p.Confirm.Confirmed, "a confirm latches");

            p.Confirm.Restart();
            Assert.AreEqual(DoubleHopConfirm.Phase.Settling, p.Confirm.State);
            Assert.AreEqual(0, p.Confirm.Hops);
            Assert.IsFalse(p.Steering.IsCalibrated, "Restart is the stand-still recalibration");
        }

        // ---- what must NOT confirm -----------------------------------------------------------

        [Test]
        public void OneHopNeverConfirms_AndLapsesBackToAskingForTwo()
        {
            var p = new Player();
            p.Settle(Table);
            p.Hop(Table);
            Assert.AreEqual(1, p.Confirm.Hops);

            p.Stand(10f, Table);
            Assert.IsFalse(p.EverConfirmed);
            Assert.AreEqual(0, p.Confirm.Hops, "a lone hop lapses once its window has gone");
            Assert.AreEqual(DoubleHopConfirm.Phase.Armed, p.Confirm.State,
                "standing still keeps the confirm armed");
        }

        [Test]
        public void TwoHopsFurtherApartThanTheWindowDoNotPair()
        {
            var p = new Player();
            p.Settle(Table);
            p.Hop(Table);
            p.Stand(DoubleHopConfirm.HopWindowSeconds + 0.5f, Table);
            p.Hop(Table);
            Assert.IsFalse(p.EverConfirmed, "hops 2.5 s apart are two single hops");
            Assert.AreEqual(1, p.Confirm.Hops, "the late hop opens a new window");

            p.Stand(0.1f, Table);
            p.Hop(Table);
            Assert.IsTrue(p.Confirm.Confirmed, "and pairs with the next one");
        }

        [Test]
        public void HopsBeforeThePlayerHasStoodStillDoNotCount()
        {
            // Straight in and hopping, before the settle: the baseline those hops would be judged
            // against is not the player's yet, so they count for nothing.
            var p = new Player();
            p.Stand(0.3f, Table);
            p.Hop(Table);
            p.Hop(Table);
            Assert.IsFalse(p.EverConfirmed);
            Assert.AreEqual(DoubleHopConfirm.Phase.Settling, p.Confirm.State);

            p.Settle(Table);
            Assert.AreEqual(0, p.Confirm.Hops, "nothing from before the settle carries over");
            p.Hop(Table);
            p.Hop(Table);
            Assert.IsTrue(p.Confirm.Confirmed);
        }

        [Test]
        public void StandingStillForAMinuteNeverConfirms()
        {
            // Breathing and idle sway (+-0.2 face widths, ~3 cm) for a minute, both distances.
            foreach (float size in new[] { Handheld, Table })
            {
                var p = new Player(Frame30);
                for (float t = 0f; t < 60f; t += p.Dt)
                {
                    float sway = 0.2f * (float)Math.Sin(t * Math.PI);
                    float drift = 0.15f * (float)Math.Sin(t * 0.7);
                    p.Step(NeutralX + Dx(drift, size), NeutralY + Dy(sway, size), size);
                }

                Assert.IsFalse(p.EverConfirmed, "sway confirmed at size " + size);
                Assert.AreEqual(DoubleHopConfirm.Phase.Armed, p.Confirm.State,
                    "sway is standing still, so the confirm should be waiting for hops");
            }
        }

        [Test]
        public void WalkingInTowardsAPhoneBelowFaceHeight_NeverConfirms()
        {
            // The headline case. The phone is propped on a table 45 cm below the player's face; they
            // walk back in from 3.2 m to 1.8 m at 1.1 m/s with an ordinary 4 cm head bob, stop, and
            // then just stand there. Walking TOWARDS a lens below you moves your face UP the frame,
            // and the bob rides on top: the bare jump rule reads that as hops.
            var p = new Player();
            const float up = 0.45f, from = 3.2f, to = 1.8f, speed = 1.1f;
            for (float t = 0f; from - speed * t > to; t += p.Dt)
                p.See(from - speed * t, up + 0.02f * (float)Math.Sin(2 * Math.PI * 1.8 * t), 0f);

            Assert.GreaterOrEqual(p.JumpEdges, 2,
                "premise: the walk-in should fire the bare jump rule at least twice — if it does " +
                "not, this test proves nothing");
            Assert.IsFalse(p.EverConfirmed, "walking in must never confirm");
            Assert.AreEqual(DoubleHopConfirm.Phase.Settling, p.Confirm.State,
                "still walking is never still enough to arm");

            // Arrived. Standing with a little sway for five seconds, no hops.
            for (float t = 0f; t < 5f; t += p.Dt)
                p.See(to, up + 0.008f * (float)Math.Sin(2 * Math.PI * 0.4 * t), 0f);
            Assert.IsFalse(p.EverConfirmed, "arriving and standing is not a confirm");
            Assert.AreEqual(DoubleHopConfirm.Phase.Armed, p.Confirm.State,
                "once they stand still the confirm is ready for their hops");

            // And their real hops still work from where they stopped.
            float size = BoxMetres / to;
            float y = 0.5f - up / (to / Aspect);
            p.HopFrom(NeutralX, y, size);
            p.HopFrom(NeutralX, y, size);
            Assert.IsTrue(p.Confirm.Confirmed, "two hops after arriving confirm");
        }

        [Test]
        public void TheRunsOwnSteering_CalibratedOnTheWalkIn_IsAimedStraightOnlyByTheResetAtCountdownStart()
        {
            // Why RunFlow resets the RUN's FaceSteering when the 3-2-1 starts (CountdownStarted),
            // pinned at the arithmetic level — the wiring itself is MonoBehaviour-only and is
            // device-verified. The run's steering is reset once at BeginResume and then calibrates
            // on the first dozen confident frames, which are the player walking back in from the
            // side of the frame. `Bare` is exactly that steering; `run` is the same one reset at
            // the moment the confirm lands, i.e. when the countdown starts.
            var p = new Player();
            var run = new FaceSteering();
            p.AlsoFed.Add(run);

            const float distance = 2.2f, up = 0.30f;
            for (float t = 0f; t < 1.05f; t += p.Dt) p.See(distance, up, -1.05f + t);

            float size = BoxMetres / distance;
            float y = 0.5f - up / (distance / Aspect);
            p.Stand(DoubleHopConfirm.SettleSeconds + 0.5f, size, NeutralX, y);
            p.HopFrom(NeutralX, y, size);
            p.HopFrom(NeutralX, y, size);
            Assert.IsTrue(p.Confirm.Confirmed, "the player stood still and hopped twice");

            run.Reset(); // CountdownStarted -> RunFlow.RecalibrateForResume
            p.Stand(ResumeCountdown.DefaultDurationSeconds, size, NeutralX, y);

            Assert.Greater(Math.Abs(p.Bare.MoveAxis), 0.5f,
                "premise: calibrated on the walk-in, the run's steering still holds a side lane " +
                "after the whole count — its drift cannot fix a neutral that far off");
            Assert.AreEqual(0f, run.MoveAxis, 0.05f,
                "reset at countdown start, the run resumes aimed straight at the player");
            Assert.AreEqual(NeutralX, run.NeutralX, Dx(0.05f, size));
        }

        [Test]
        public void WalkingInFromTheSide_NeverConfirms()
        {
            // Across the frame at 2.2 m, 1 m/s, the phone 30 cm below the face, a heavy 6 cm bob:
            // entering at the edge of the frame and stopping in the middle.
            var p = new Player();
            for (float t = 0f; t < 1.05f; t += p.Dt)
                p.See(2.2f, 0.30f + 0.03f * (float)Math.Sin(2 * Math.PI * 1.8 * t), -1.05f + t);

            p.Stand(0.6f, BoxMetres / 2.2f, x: 0.5f, y: 0.5f - 0.30f / (2.2f / Aspect));
            Assert.IsFalse(p.EverConfirmed, "a side walk-in must never confirm");
        }

        [Test]
        public void WalkingTowardsThePhoneAfterArming_DoesNotConfirm()
        {
            // Armed at 2.5 m, the player then walks up to the phone — to tap RESUME, say — at
            // 0.8 m/s with a heavy 7 cm bob. The lens is at face height, so only the bob moves the
            // face up and down; it is still enough to fire the bare jump rule more than once. The
            // box growing is what gives the walk away.
            var p = new Player();
            for (float t = 0f; t < DoubleHopConfirm.SettleSeconds + 0.6f; t += p.Dt) p.See(2.5f, 0f, 0f);
            Assert.AreEqual(DoubleHopConfirm.Phase.Armed, p.Confirm.State);

            int edgesBefore = p.JumpEdges;
            for (float t = 0f; 2.5f - 0.8f * t > 0.9f; t += p.Dt)
                p.See(2.5f - 0.8f * t, 0.035f * (float)Math.Sin(2 * Math.PI * 1.8 * t), 0f);

            Assert.GreaterOrEqual(p.JumpEdges - edgesBefore, 2,
                "premise: the walk should fire the bare jump rule at least twice");
            Assert.IsFalse(p.EverConfirmed, "walking up to the phone must not confirm");
        }

        [Test]
        public void SteppingSidewaysBetweenTheHops_StartsOver()
        {
            var p = new Player();
            p.Settle(Table);
            p.Hop(Table);
            Assert.AreEqual(1, p.Confirm.Hops);

            // A 15 cm side-step: well past the stray band.
            float step = Dx(1.0f, Table);
            for (float t = 0f; t < 0.3f; t += p.Dt) p.Step(NeutralX + step * t / 0.3f, NeutralY, Table);
            p.Hop(Table, x: NeutralX + step);

            Assert.IsFalse(p.EverConfirmed, "a hop, a step and a hop is not two hops");
            Assert.AreEqual(DoubleHopConfirm.Phase.Settling, p.Confirm.State);
            Assert.GreaterOrEqual(p.Confirm.Resettles, 1);
        }

        [Test]
        public void ARiseThatNeverComesBackDown_IsNotAHop()
        {
            // Up onto a step, or the face climbing the frame for any reason that is not a hop: the
            // jump rule fires, the head stays up, and nothing counts.
            var p = new Player();
            p.Settle(Table);
            float high = NeutralY - Dy(0.8f, Table);
            for (float t = 0f; t < 0.15f; t += p.Dt) p.Step(NeutralX, NeutralY - (NeutralY - high) * t / 0.15f, Table);
            Assert.GreaterOrEqual(p.JumpEdges, 1, "premise: the rise fires the jump rule");

            p.Stand(DoubleHopConfirm.LandTimeoutSeconds + 0.2f, Table, y: high);
            Assert.AreEqual(0, p.Confirm.Hops);
            Assert.AreEqual(DoubleHopConfirm.Phase.Settling, p.Confirm.State,
                "a take-off that never lands starts the confirm over");
        }

        [Test]
        public void ClimbingInStepsIsNotHopping()
        {
            // Two take-offs with no landing in between — a staircase, not a double hop.
            var p = new Player();
            p.Settle(Table);
            float y = NeutralY;
            for (int stair = 0; stair < 2; stair++)
            {
                float next = y - Dy(0.6f, Table);
                for (float t = 0f; t < 0.12f; t += p.Dt) p.Step(NeutralX, y + (next - y) * t / 0.12f, Table);
                y = next;
                p.Stand(0.5f, Table, y: y);
            }
            Assert.GreaterOrEqual(p.JumpEdges, 2, "premise: both rises fire the jump rule");
            Assert.IsFalse(p.EverConfirmed);
            Assert.AreEqual(0, p.Confirm.Hops);
        }

        [Test]
        public void LosingTheFaceStartsOver_ABlinkDoesNot()
        {
            var p = new Player();
            p.Settle(Table);
            p.Hop(Table);
            Assert.AreEqual(1, p.Confirm.Hops);

            // A blink: a few frames of nothing, the player still standing there.
            p.Gone(0.1f);
            p.Stand(0.1f, Table);
            Assert.AreEqual(DoubleHopConfirm.Phase.Armed, p.Confirm.State, "a blink is not a loss");
            Assert.AreEqual(1, p.Confirm.Hops);

            // Gone for real — walked out and back.
            p.Gone(1f);
            p.Stand(0.1f, Table);
            Assert.AreEqual(DoubleHopConfirm.Phase.Settling, p.Confirm.State,
                "a real loss starts over, stand-still included");
            Assert.AreEqual(0, p.Confirm.Hops);
            Assert.IsFalse(p.EverConfirmed);
        }

        [Test]
        public void CrouchingStartsOver()
        {
            // Bending down to the phone reads as a crouch; it is not standing still, and the
            // stand-up out of it must not become a hop.
            var p = new Player();
            p.Settle(Table);
            float crouch = NeutralY + Dy(p.Steering.SlideDrop + 0.3f, Table);
            p.Stand(0.4f, Table, y: crouch);
            p.Stand(0.3f, Table);
            Assert.AreEqual(DoubleHopConfirm.Phase.Settling, p.Confirm.State);
            Assert.IsFalse(p.EverConfirmed);
        }

        [Test]
        public void ANullSteeringIsRefused()
        {
            Assert.Throws<ArgumentNullException>(() => new DoubleHopConfirm(null));
        }
    }
}
