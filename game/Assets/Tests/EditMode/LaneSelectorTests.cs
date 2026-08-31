using MotionRunner.Pose;
using MotionRunner.Track;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// Lane steering (owner call, 30 Aug): the axis names a lane and the runner animates to it,
    /// instead of the axis driving a lateral velocity that could park it between two lanes.
    ///
    /// Three things here are worth more than a playtest. The zone boundaries, because a threshold
    /// with no hysteresis fails exactly when a real hand trembles on it and never when an agent
    /// taps a key. The slide's duration, because "can the player physically make this dodge" is
    /// arithmetic against the track's own numbers. And what the collision box does mid-slide,
    /// because committing to a lane must not read as teleporting out of the one being left.
    public sealed class LaneSelectorTests
    {
        const float Dt = 1f / 60f;                 // the game's target frame time
        const float CameraDt = 1f / 30f;           // the camera path's sample rate

        /// The tightest dodge the generator is ever allowed to ask for: two obstacle clusters
        /// MinClusterSpacing apart, arriving at the top of the difficulty curve.
        static float TightestDodgeSeconds =>
            TrackMetrics.MinClusterSpacing / TrackMetrics.SpeedFor(DifficultyCurve.MaxDifficulty);

        /// What the analog steering this replaces managed: 7 m/s of lateral velocity, and only
        /// once the input filter had ramped the axis all the way up.
        const float OldSteerSpeed = 7f;

        static float SecondsToSettle(LaneSelector lanes, float axis, float dt)
        {
            float elapsed = 0f;
            for (int i = 0; i < 1000; i++)
            {
                lanes.Step(axis, dt);
                elapsed += dt;
                if (!lanes.IsSliding) return elapsed;
            }

            Assert.Fail("the slide never finished - a lane change has to end");
            return elapsed;
        }

        // ---- the zone mapping ---------------------------------------------------------------

        [Test]
        public void ARunStartsSettledInTheCentreLane()
        {
            var lanes = new LaneSelector();
            Assert.AreEqual(0, lanes.Lane);
            Assert.AreEqual(0f, lanes.X, 1e-6f);
            Assert.IsFalse(lanes.IsSliding);
        }

        [Test]
        public void ADeliberateLeanCommitsToThatLane()
        {
            Assert.AreEqual(TrackMetrics.MaxLane, LaneSelector.LaneFor(LaneSelector.EnterThreshold, 0));
            Assert.AreEqual(TrackMetrics.MinLane, LaneSelector.LaneFor(-LaneSelector.EnterThreshold, 0));
        }

        [Test]
        public void AHalfHeartedLeanNeverLeavesTheCentre()
        {
            Assert.AreEqual(0, LaneSelector.LaneFor(LaneSelector.EnterThreshold - 0.01f, 0));
            Assert.AreEqual(0, LaneSelector.LaneFor(-LaneSelector.EnterThreshold + 0.01f, 0));
            Assert.AreEqual(0, LaneSelector.LaneFor(0f, 0));
        }

        [Test]
        public void KeyboardsThreeValuesMapCrisply()
        {
            // KeyboardInput reports exactly -1 / 0 / +1: hold left, be in the left lane; release,
            // be in the centre. Nothing about the editor path may land in a hysteresis band.
            Assert.AreEqual(TrackMetrics.MinLane, LaneSelector.LaneFor(-1f, 0));
            Assert.AreEqual(TrackMetrics.MaxLane, LaneSelector.LaneFor(1f, 0));
            Assert.AreEqual(0, LaneSelector.LaneFor(0f, TrackMetrics.MinLane));
            Assert.AreEqual(0, LaneSelector.LaneFor(0f, TrackMetrics.MaxLane));
        }

        [Test]
        public void TheMappingIsAbsolute_SoLeaningHardCrossesTheWholeRoad()
        {
            // The axis names a lane; it does not nudge one lane over. Leaning hard from the left
            // lane goes to the right lane, and the slide is what makes the trip visible.
            Assert.AreEqual(TrackMetrics.MaxLane, LaneSelector.LaneFor(0.9f, TrackMetrics.MinLane));
            Assert.AreEqual(TrackMetrics.MinLane, LaneSelector.LaneFor(-0.9f, TrackMetrics.MaxLane));
        }

        [Test]
        public void LeaningBackTheOtherWayReturnsToTheCentreFirst()
        {
            // A lean of the wrong sign is never a pose to hold a lane with, however weak it is -
            // but it is not enough on its own to claim the far lane either.
            Assert.AreEqual(0, LaneSelector.LaneFor(-0.4f, TrackMetrics.MaxLane));
            Assert.AreEqual(0, LaneSelector.LaneFor(0.4f, TrackMetrics.MinLane));
        }

        // ---- hysteresis ---------------------------------------------------------------------

        [Test]
        public void EnteringALaneCostsMoreThanStayingInIt()
        {
            Assert.Greater(LaneSelector.EnterThreshold, LaneSelector.HoldThreshold,
                "without a gap there is no hysteresis and a hand on the boundary flaps");
            Assert.Greater(LaneSelector.HoldThreshold, 0f);
            Assert.Less(LaneSelector.EnterThreshold, 1f,
                "an unreachable threshold would leave the side lanes unusable");
        }

        [Test]
        public void ALaneIsHeldAgainstAnAxisThatWouldNotHaveEnteredIt()
        {
            int lane = LaneSelector.LaneFor(0.8f, 0);
            Assert.AreEqual(TrackMetrics.MaxLane, lane);

            foreach (float axis in new[] { 0.49f, 0.35f, LaneSelector.HoldThreshold, 0.42f })
                Assert.AreEqual(TrackMetrics.MaxLane, LaneSelector.LaneFor(axis, lane),
                    "axis " + axis + " should not have shaken the runner out of its lane");
        }

        [Test]
        public void RelaxingBelowTheHoldThresholdComesBackToTheCentre()
        {
            Assert.AreEqual(0, LaneSelector.LaneFor(LaneSelector.HoldThreshold - 0.01f, TrackMetrics.MaxLane));
            Assert.AreEqual(0, LaneSelector.LaneFor(-LaneSelector.HoldThreshold + 0.01f, TrackMetrics.MinLane));
        }

        [Test]
        public void AHandTremblingOnTheBoundaryChangesLaneOnce()
        {
            // The failure the hysteresis exists for: an axis oscillating across EnterThreshold.
            // With a single threshold this is one lane change per sample.
            var lanes = new LaneSelector();
            int changes = 0;
            int previous = lanes.Lane;

            for (int i = 0; i < 120; i++)
            {
                float axis = LaneSelector.EnterThreshold + (i % 2 == 0 ? 0.08f : -0.08f);
                lanes.Step(axis, Dt);
                if (lanes.Lane != previous)
                {
                    changes++;
                    previous = lanes.Lane;
                }
            }

            Assert.AreEqual(1, changes, "the runner flapped between lanes");
            Assert.AreEqual(TrackMetrics.MaxLane, lanes.Lane);
        }

        [Test]
        public void ASwayingPlayerHoldsTheLaneTheyLeanedInto()
        {
            // The same failure driven by the real thing rather than by a square wave: a calibrated
            // FaceSteering, a player leaning right hard enough to claim the lane, and a slow body
            // sway on top that repeatedly takes the axis back under EnterThreshold.
            var steering = new FaceSteering();
            const float neutralX = 0.5f;
            const float neutralY = 0.4f;

            // Detector BOX width as a fraction of the frame — the phone propped up a couple of
            // metres away, which is how camera mode is meant to be played. FaceSteering's
            // thresholds are in *face* widths and a box is FaceSizeFilter.BoxWidthsPerFace of one,
            // so a lean expressed in axis terms becomes a frame coordinate through both numbers.
            // Getting that conversion wrong here does not fail loudly - it quietly inflates the
            // lean until the sway below stops reaching the boundary this test exists to defend,
            // which is what the third assertion is for.
            const float faceSize = 0.08f;
            for (int i = 0; i < steering.CalibrationSamples; i++)
                steering.Submit(neutralX, neutralY, faceSize, 0.9f, CameraDt);
            Assert.IsTrue(steering.IsCalibrated);

            // Lean that reads as axis 0.55 once past the dead zone, swaying by +/- 0.15 of axis
            // at roughly 1.7 Hz.
            float toFrame = steering.HalfRangeX * faceSize / FaceSizeFilter.BoxWidthsPerFace;
            float meanLean = (steering.DeadZone + 0.55f * (1f - steering.DeadZone)) * toFrame;
            float swayLean = 0.15f * (1f - steering.DeadZone) * toFrame;

            var lanes = new LaneSelector();
            int changes = 0;
            int previous = lanes.Lane;
            float lowestAxisInLane = 1f;

            for (int i = 0; i < 120; i++)
            {
                float t = i * CameraDt;
                float sway = swayLean * (float)System.Math.Sin(2.0 * System.Math.PI * t / 0.6);
                steering.Submit(neutralX + meanLean + sway, neutralY, faceSize, 0.9f, CameraDt);
                lanes.Step(steering.MoveAxis, CameraDt);

                if (lanes.Lane != previous)
                {
                    changes++;
                    previous = lanes.Lane;
                }
                else if (lanes.Lane == TrackMetrics.MaxLane && steering.MoveAxis < lowestAxisInLane)
                {
                    lowestAxisInLane = steering.MoveAxis;
                }
            }

            Assert.AreEqual(TrackMetrics.MaxLane, lanes.Lane);
            Assert.AreEqual(1, changes, "the sway shook the runner out of the lane it had claimed");
            Assert.Less(lowestAxisInLane, LaneSelector.EnterThreshold,
                "the sway never actually challenged the boundary - this test would pass without hysteresis");
        }

        [Test]
        public void TheEnterThresholdIsALeanAPlayerCanActuallyReach()
        {
            // FaceSteering rescales past its dead zone, so EnterThreshold costs this fraction of
            // its HalfRangeX. Cheap enough not to have to reach the end of the range, dear enough
            // that idle drift does not get there.
            var steering = new FaceSteering();
            float leanFraction = steering.DeadZone + LaneSelector.EnterThreshold * (1f - steering.DeadZone);

            Assert.Less(leanFraction, 0.8f, "changing lane must not need a full-range lean");
            Assert.Greater(leanFraction, 0.35f, "a lane change must be a deliberate lean");
        }

        [Test]
        public void NoAxisCanPutTheRunnerOffTheRoad()
        {
            for (int lane = TrackMetrics.MinLane - 1; lane <= TrackMetrics.MaxLane + 1; lane++)
                for (float axis = -3f; axis <= 3f; axis += 0.05f)
                {
                    int result = LaneSelector.LaneFor(axis, lane);
                    Assert.GreaterOrEqual(result, TrackMetrics.MinLane);
                    Assert.LessOrEqual(result, TrackMetrics.MaxLane);
                }
        }

        [Test]
        public void AnAxisThatStoppedMakingSenseFallsBackToTheCentre()
        {
            Assert.AreEqual(0, LaneSelector.LaneFor(float.NaN, TrackMetrics.MaxLane));
            Assert.AreEqual(0, LaneSelector.LaneFor(float.NaN, 0));
        }

        // ---- the slide ----------------------------------------------------------------------

        [Test]
        public void TheRunnerNeverRestsBetweenLanes()
        {
            var lanes = new LaneSelector();
            float[] script = { 0.9f, 0.9f, -0.2f, -0.9f, 0.4f, 0f, 0.7f };

            foreach (float axis in script)
            {
                // Long enough for any slide to finish - a settled runner is on a lane centre.
                for (int i = 0; i < 40; i++) lanes.Step(axis, Dt);

                Assert.IsFalse(lanes.IsSliding);
                Assert.AreEqual(TrackMetrics.LaneCenterX(lanes.Lane), lanes.X, 1e-6f,
                    "axis " + axis + " left the runner parked between two lanes");
            }
        }

        [Test]
        public void TheSlideArrivesOnTheCentreWithoutOvershooting()
        {
            var lanes = new LaneSelector();
            float furthest = 0f;
            float previous = 0f;

            for (int i = 0; i < 40; i++)
            {
                lanes.Step(1f, Dt);
                Assert.GreaterOrEqual(lanes.X, previous, "the slide went backwards");
                previous = lanes.X;
                if (lanes.X > furthest) furthest = lanes.X;
            }

            float centre = TrackMetrics.LaneCenterX(TrackMetrics.MaxLane);
            Assert.AreEqual(centre, lanes.X, 1e-6f);
            Assert.LessOrEqual(furthest, centre + 1e-6f, "the slide overshot the lane centre");
        }

        [Test]
        public void HoldingTheAxisDoesNotRestartTheSlide()
        {
            // Re-aiming the slide every frame at a target it has not reached yet would make the
            // runner crawl in and never arrive.
            var lanes = new LaneSelector();
            float settled = SecondsToSettle(lanes, 1f, Dt);
            Assert.LessOrEqual(settled, LaneSelector.SecondsPerLane + Dt + 1e-4f);
        }

        [Test]
        public void ASlideBackIsAsQuickAsTheSlideOut()
        {
            var lanes = new LaneSelector();
            SecondsToSettle(lanes, 1f, Dt);
            float back = SecondsToSettle(lanes, 0f, Dt);
            Assert.LessOrEqual(back, LaneSelector.SecondsPerLane + Dt + 1e-4f);
            Assert.AreEqual(0f, lanes.X, 1e-6f);
        }

        [Test]
        public void ARetargetMidSlideStartsFromWhereTheRunnerActuallyIs()
        {
            // Flicking back after committing must not snap or rewind: the new slide begins at the
            // position on screen, and it is shorter because there is less road left to cover.
            var lanes = new LaneSelector();
            for (int i = 0; i < 4; i++) lanes.Step(1f, Dt);
            float caughtAt = lanes.X;
            Assert.Greater(caughtAt, 0f);
            Assert.Less(caughtAt, TrackMetrics.LaneCenterX(TrackMetrics.MaxLane));

            lanes.Step(0f, Dt);
            Assert.AreEqual(0, lanes.Lane);
            Assert.Less(lanes.X, caughtAt, "the slide back should already be under way");
            Assert.Greater(lanes.X, 0f, "and it should not have snapped to the centre");
        }

        // ---- can the player make the dodge the track asks for? ------------------------------

        [Test]
        public void AOneLaneDodgeFitsBetweenTheClosestObstacleClustersAtTopSpeed()
        {
            float settled = SecondsToSettle(new LaneSelector(), 1f, Dt);
            Assert.Less(settled, TightestDodgeSeconds,
                "the track can demand a lane change the runner cannot physically complete");
        }

        [Test]
        public void AFullWidthSweepAlsoFitsThatGap()
        {
            var lanes = new LaneSelector();
            lanes.Reset(TrackMetrics.MinLane);
            float settled = SecondsToSettle(lanes, 1f, Dt);

            Assert.AreEqual(TrackMetrics.LaneCenterX(TrackMetrics.MaxLane), lanes.X, 1e-6f);
            Assert.Less(settled, TightestDodgeSeconds,
                "crossing the whole road is the worst case a chunk seam can ask for");
        }

        [Test]
        public void DodgingIsNotSlowerThanTheAnalogSteeringItReplaces()
        {
            float oneLane = SecondsToSettle(new LaneSelector(), 1f, Dt);
            Assert.Less(oneLane, TrackMetrics.LaneWidth / OldSteerSpeed);

            var wide = new LaneSelector();
            wide.Reset(TrackMetrics.MinLane);
            float acrossTheRoad = SecondsToSettle(wide, 1f, Dt);
            Assert.Less(acrossTheRoad, 2f * TrackMetrics.LaneWidth / OldSteerSpeed);
        }

        // ---- collisions during the slide ----------------------------------------------------

        [Test]
        public void CommittingToALaneIsNotATeleportOutOfTheOneBeingLeft()
        {
            // Collision reads the runner's real x, so the frames at the start of a slide are still
            // frames spent inside the block the player is escaping. Deciding late still kills.
            var block = TrackGeometry.Obstacle(
                new ObstaclePlacement(ObstacleKind.FullBlock, 0, 0f), 0f);

            var lanes = new LaneSelector();
            lanes.Step(1f, Dt);

            Assert.IsTrue(RunnerAt(lanes.X).Intersects(block),
                "one frame into the slide the runner is still in the blocked lane");
        }

        [Test]
        public void TheBlockedLaneIsCleanlyLeftBeforeTheSlideEnds()
        {
            // And the other half of it: the player does not have to reach the next lane centre to
            // be safe, so a dodge is survivable before its animation has finished.
            var block = TrackGeometry.Obstacle(
                new ObstaclePlacement(ObstacleKind.FullBlock, 0, 0f), 0f);

            var lanes = new LaneSelector();
            lanes.Step(1f, Dt);
            float elapsed = Dt;
            float clearedAt = RunnerAt(lanes.X).Intersects(block) ? -1f : elapsed;

            for (int i = 0; i < 200 && lanes.IsSliding; i++)
            {
                lanes.Step(1f, Dt);
                elapsed += Dt;
                if (clearedAt < 0f && !RunnerAt(lanes.X).Intersects(block)) clearedAt = elapsed;
            }

            Assert.Greater(clearedAt, 0f, "the runner never got clear of the block");
            Assert.Less(clearedAt, elapsed, "the block was only cleared by arriving at the next lane");
        }

        [Test]
        public void ASettledRunnerInASideLaneIsClearOfACentreBlock()
        {
            var block = TrackGeometry.Obstacle(
                new ObstaclePlacement(ObstacleKind.FullBlock, 0, 0f), 0f);

            var lanes = new LaneSelector();
            SecondsToSettle(lanes, 1f, Dt);
            Assert.IsFalse(RunnerAt(lanes.X).Intersects(block));

            SecondsToSettle(lanes, -1f, Dt);
            Assert.IsFalse(RunnerAt(lanes.X).Intersects(block));
        }

        static Aabb RunnerAt(float x) => TrackGeometry.Runner(x, TrackMetrics.RunnerRestY);
    }
}
