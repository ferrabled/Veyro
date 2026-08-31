using System;
using System.Collections.Generic;
using MotionRunner.Pose;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// The probe is what stops a wrong front-camera orientation from looking like "BlazePose does
    /// not work on this phone". Its search order and its stopping rule decide how long startup
    /// takes, so both are pinned here rather than tuned by feel on the device.
    public sealed class OrientationProbeTests
    {
        /// Drives a probe against a fake detector that scores one orientation highly.
        static OrientationProbe RunAgainst(OrientationProbe.Candidate truth,
            int reportedRotation, bool reportedFlip, int samplesPerCandidate = 3,
            float truthScore = 0.95f, float wrongScore = 0.05f)
        {
            var probe = new OrientationProbe(reportedRotation, reportedFlip, samplesPerCandidate);
            int guard = 0;
            while (!probe.IsComplete)
            {
                probe.Submit(probe.Current.Equals(truth) ? truthScore : wrongScore);
                if (++guard > 1000) Assert.Fail("probe never completed");
            }
            return probe;
        }

        [Test]
        public void TheReportedOrientationIsTriedFirst()
        {
            var probe = new OrientationProbe(90, true);
            Assert.AreEqual(90, probe.Current.RotationDegrees);
            Assert.IsTrue(probe.Current.VerticallyFlipped);
        }

        [Test]
        public void EveryRotationAndFlipCombinationIsReachable()
        {
            var probe = new OrientationProbe(0, false, 1);
            var seen = new HashSet<string>();
            int guard = 0;

            while (!probe.IsComplete)
            {
                seen.Add(probe.Current.ToString());
                probe.Submit(0.01f); // nothing ever looks upright
                if (++guard > 100) Assert.Fail("probe never completed");
            }

            Assert.AreEqual(8, probe.CandidateCount);
            Assert.AreEqual(8, seen.Count, "a candidate was skipped: " + string.Join(" ", seen));
        }

        [Test]
        public void BelievingTheDeviceCostsOnlyOneCandidate()
        {
            // The cheap path, and the one that runs on a well-behaved phone: report is right,
            // three inferences, done.
            var truth = new OrientationProbe.Candidate(270, false);
            var probe = RunAgainst(truth, 270, false);

            Assert.IsTrue(probe.IsComplete);
            Assert.AreEqual(truth, probe.Best);
            Assert.AreEqual(1, probe.CandidatesTried);
        }

        [Test]
        public void AVerticalFlipIsTheSecondThingTried()
        {
            // The UAV-origin case: right rotation, upside down. Must not cost a full sweep.
            var truth = new OrientationProbe.Candidate(90, false);
            var probe = RunAgainst(truth, 90, true);

            Assert.AreEqual(truth, probe.Best);
            Assert.AreEqual(2, probe.CandidatesTried);
        }

        [Test]
        public void AQuarterTurnOutIsStillFound()
        {
            var truth = new OrientationProbe.Candidate(0, false);
            var probe = RunAgainst(truth, 90, false);

            Assert.IsTrue(probe.IsComplete);
            Assert.AreEqual(truth, probe.Best);
            Assert.GreaterOrEqual(probe.BestScore, 0.9f);
        }

        [Test]
        public void TheBestOfAllBadCandidatesStillWins()
        {
            // Nobody usefully in frame during startup: no candidate is convincing, but the probe
            // must still settle on something and hand back a usable orientation rather than
            // nothing. Each candidate here is *clearly* better than the one before — clearly
            // meaning by more than ChallengerMargin — so the last one is the answer.
            float step = OrientationProbe.ChallengerMargin + 0.02f;
            var probe = new OrientationProbe(180, false, 2);
            float next = 0.01f;
            int guard = 0;

            while (!probe.IsComplete)
            {
                probe.Submit(next);
                probe.Submit(next);
                next += step;
                if (++guard > 100) Assert.Fail("probe never completed");
            }

            Assert.IsTrue(probe.IsComplete);
            Assert.Less(probe.BestScore, OrientationProbe.GoodEnoughScore);
            Assert.AreEqual(next - step, probe.BestScore, 1e-5f, "the last, highest candidate won");
        }

        [Test]
        public void TheMirrorTwinCannotUnseatTheDeviceReportOnNoise()
        {
            // Every candidate has a twin at (rotation + 180, !flip) that is upright but
            // horizontally MIRRORED, and a face is symmetric enough that the detector scores the
            // two within a hundredth of each other. Which of them wins decides whether leaning
            // left steers left, so a coin flip is not an acceptable answer: the reported
            // orientation is tried first and keeps its place unless something is genuinely better.
            //
            // This is the resume failure — the launch probe stops early on a clean 0.9+, but a
            // player leaning over the phone they just unpaused puts every candidate in this
            // contested band, and all eight then get compared.
            var reported = new OrientationProbe.Candidate(90, false);
            var twin = new OrientationProbe.Candidate(270, true);
            var probe = new OrientationProbe(90, false, 2);
            int guard = 0;

            while (!probe.IsComplete)
            {
                float score = 0.05f;
                if (probe.Current.Equals(reported)) score = 0.72f;
                else if (probe.Current.Equals(twin)) score = 0.75f; // a hair better, on noise
                probe.Submit(score);
                probe.Submit(score);
                if (++guard > 100) Assert.Fail("probe never completed");
            }

            Assert.AreEqual(reported, probe.Best, "the mirror twin won on noise");
            Assert.IsTrue(probe.IsConfident, "0.72 is a real sighting, not a contest between noise");
        }

        [Test]
        public void AGenuinelyBetterCandidateStillBeatsTheMargin()
        {
            // The margin must not turn into "always believe the device": a report that is a
            // quarter turn out scores nothing like an upright face, and losing that correction
            // would be worse than the mirror it prevents.
            var truth = new OrientationProbe.Candidate(180, true);
            var probe = RunAgainst(truth, 0, true, truthScore: 0.62f, wrongScore: 0.2f);

            Assert.AreEqual(truth, probe.Best);
            Assert.IsTrue(probe.IsConfident);
        }

        [Test]
        public void ScoresAreAveragedAcrossSamplesSoOneBlurredFrameCannotDecide()
        {
            var probe = new OrientationProbe(0, false, 3);

            // Candidate 1 catches one lucky frame but is mostly wrong.
            probe.Submit(0.99f);
            probe.Submit(0.0f);
            probe.Submit(0.0f);
            Assert.AreEqual(0.33f, probe.BestScore, 1e-5f);
            Assert.IsFalse(probe.IsComplete);

            // Candidate 2 is consistently better and takes the lead.
            var second = probe.Current;
            probe.Submit(0.6f);
            probe.Submit(0.6f);
            probe.Submit(0.6f);
            Assert.AreEqual(second, probe.Best);
            Assert.AreEqual(0.6f, probe.BestScore, 1e-5f);
        }

        [Test]
        public void AWinnerOnNoiseIsNotConfident()
        {
            // The failure the first device run actually produced: with nobody in front of the
            // camera every candidate scored ~0.14, and the winner beat the field by 0.001. The
            // probe completed, but its answer was worthless and must be reported as such.
            var probe = new OrientationProbe(270, false, 2);
            float next = 0.140f;
            int guard = 0;

            while (!probe.IsComplete)
            {
                probe.Submit(next);
                probe.Submit(next);
                next += 0.001f;
                if (++guard > 100) Assert.Fail("probe never completed");
            }

            Assert.IsTrue(probe.IsComplete);
            Assert.IsFalse(probe.IsConfident, "a contest between eight bad options is not an answer");
        }

        [Test]
        public void AWinnerThatActuallySawABodyIsConfident()
        {
            var truth = new OrientationProbe.Candidate(90, false);
            var probe = RunAgainst(truth, 90, false);

            Assert.IsTrue(probe.IsConfident);
            Assert.AreEqual(truth, probe.Best);
        }

        [Test]
        public void AnIncompleteProbeIsNeverConfident()
        {
            var probe = new OrientationProbe(0, false, 3);
            probe.Submit(0.99f);
            Assert.IsFalse(probe.IsComplete);
            Assert.IsFalse(probe.IsConfident);
        }

        [Test]
        public void SubmittingAfterCompletionIsIgnored()
        {
            var probe = RunAgainst(new OrientationProbe.Candidate(0, false), 0, false);
            OrientationProbe.Candidate settled = probe.Best;
            float settledScore = probe.BestScore;

            probe.Submit(1f);
            probe.Submit(1f);
            probe.Submit(1f);

            Assert.AreEqual(settled, probe.Best);
            Assert.AreEqual(settledScore, probe.BestScore, 1e-6f);
        }

        [Test]
        public void NonQuarterTurnReportsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new OrientationProbe(45, false));
        }

        [Test]
        public void NegativeReportedRotationIsNormalized()
        {
            var probe = new OrientationProbe(-90, false);
            Assert.AreEqual(270, probe.Current.RotationDegrees);
        }
    }
}
