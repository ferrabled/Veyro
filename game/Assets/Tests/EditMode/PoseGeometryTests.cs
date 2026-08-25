using System;
using MotionRunner.Pose;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// The engine-free half of the CV stack (T-010's architecture seam). Everything here runs
    /// headlessly, which is what lets T-012 state its acceptance criterion as a false-positive
    /// rate rather than "looked fine when I waved at it".
    public sealed class PoseGeometryTests
    {
        const float Visible = 0.9f;
        const float Hidden = 0.1f;

        static PoseFrame Frame(params (PoseJoint joint, float x, float y, float score)[] joints)
        {
            var frame = new PoseFrame();
            foreach ((PoseJoint joint, float x, float y, float score) in joints)
                frame.Set((int)joint, new PoseLandmark(x, y, 0f, score, score));
            frame.MarkTracked(0.9f);
            return frame;
        }

        static PoseFrame StandingUpright()
        {
            return Frame(
                (PoseJoint.LeftShoulder, 0.40f, 0.30f, Visible),
                (PoseJoint.RightShoulder, 0.60f, 0.30f, Visible),
                (PoseJoint.LeftHip, 0.42f, 0.60f, Visible),
                (PoseJoint.RightHip, 0.58f, 0.60f, Visible));
        }

        [Test]
        public void EveryModelIndexHasAName()
        {
            // The enum is a wire format: the landmarker emits 33 joints in this exact order.
            Assert.AreEqual(33, PoseFrame.JointCount);
            Assert.AreEqual(33, Enum.GetValues(typeof(PoseJoint)).Length);
            Assert.AreEqual(0, (int)PoseJoint.Nose);
            Assert.AreEqual(32, (int)PoseJoint.RightFootIndex);
        }

        [Test]
        public void SkeletonBonesAreWellFormedAndInRange()
        {
            Assert.AreEqual(0, PoseSkeleton.Bones.Length % 2, "bones are flat pairs");
            foreach (int index in PoseSkeleton.Bones)
                Assert.IsTrue(index >= 0 && index < PoseFrame.JointCount, "joint index " + index);

            for (int i = 0; i < PoseSkeleton.BoneCount; i++)
                Assert.AreNotEqual(PoseSkeleton.Bones[2 * i], PoseSkeleton.Bones[2 * i + 1],
                    "bone " + i + " joins a joint to itself");
        }

        [Test]
        public void LandmarkNeedsBothVisibilityAndPresence()
        {
            Assert.IsTrue(new PoseLandmark(0f, 0f, 0f, 0.6f, 0.6f).IsTracked);
            Assert.IsFalse(new PoseLandmark(0f, 0f, 0f, 0.6f, 0.1f).IsTracked, "absent joint");
            Assert.IsFalse(new PoseLandmark(0f, 0f, 0f, 0.1f, 0.6f).IsTracked, "occluded joint");
        }

        [Test]
        public void TorsoCenterSitsBetweenShouldersAndHips()
        {
            Assert.IsTrue(PoseGeometry.TryTorsoCenter(StandingUpright(), out PosePoint center));
            Assert.AreEqual(0.50f, center.X, 1e-5f);
            Assert.AreEqual(0.45f, center.Y, 1e-5f);
        }

        [Test]
        public void TorsoCenterFallsBackToShouldersWhenHipsAreOutOfFrame()
        {
            // Standing close to a phone on a table crops the hips; a reference point that
            // vanishes is worse than one that drifts.
            var frame = Frame(
                (PoseJoint.LeftShoulder, 0.40f, 0.30f, Visible),
                (PoseJoint.RightShoulder, 0.60f, 0.30f, Visible),
                (PoseJoint.LeftHip, 0.42f, 0.60f, Hidden),
                (PoseJoint.RightHip, 0.58f, 0.60f, Hidden));

            Assert.IsFalse(PoseGeometry.TryHipCenter(frame, out _));
            Assert.IsTrue(PoseGeometry.TryTorsoCenter(frame, out PosePoint center));
            Assert.AreEqual(0.50f, center.X, 1e-5f);
            Assert.AreEqual(0.30f, center.Y, 1e-5f);
        }

        [Test]
        public void TorsoCenterFailsWhenNoTorsoJointIsTracked()
        {
            var frame = Frame(
                (PoseJoint.LeftShoulder, 0.40f, 0.30f, Hidden),
                (PoseJoint.RightShoulder, 0.60f, 0.30f, Hidden),
                (PoseJoint.LeftHip, 0.42f, 0.60f, Hidden),
                (PoseJoint.RightHip, 0.58f, 0.60f, Hidden));

            Assert.IsFalse(PoseGeometry.TryTorsoCenter(frame, out _));
        }

        [Test]
        public void NothingIsReportedWhileTrackingIsLost()
        {
            // Landmarks survive a dropout so the pose can be held, but geometry must not pretend
            // the stale pose is current.
            PoseFrame frame = StandingUpright();
            frame.MarkLost();

            Assert.IsFalse(frame.HasPose);
            Assert.IsFalse(PoseGeometry.TryTorsoCenter(frame, out _));
            Assert.IsFalse(PoseGeometry.TryShoulderWidth(frame, out _));
            Assert.AreEqual(0.40f, frame[PoseJoint.LeftShoulder].X, 1e-5f, "last pose is retained");
        }

        [Test]
        public void ShoulderWidthShrinksAsThePlayerStepsBack()
        {
            // Every T-012 threshold is expressed as a fraction of this, so it must scale with
            // distance rather than stay fixed.
            Assert.IsTrue(PoseGeometry.TryShoulderWidth(StandingUpright(), out float near));
            Assert.AreEqual(0.20f, near, 1e-5f);

            var far = Frame(
                (PoseJoint.LeftShoulder, 0.45f, 0.30f, Visible),
                (PoseJoint.RightShoulder, 0.55f, 0.30f, Visible));
            Assert.IsTrue(PoseGeometry.TryShoulderWidth(far, out float distant));
            Assert.Less(distant, near);
        }

        [Test]
        public void FrameRejectsOutOfRangeJointIndices()
        {
            var frame = new PoseFrame();
            Assert.Throws<ArgumentOutOfRangeException>(
                () => frame.Set(33, new PoseLandmark(0f, 0f, 0f, 1f, 1f)));
        }
    }
}
