using System;

namespace MotionRunner.Pose
{
    /// One inference result: 33 landmarks plus whether the detector actually found a body.
    ///
    /// Mutable and reused frame to frame on purpose — this is written at up to 30 Hz on a phone,
    /// and allocating a fresh array per frame is exactly the GC-spike pattern the 60 FPS budget
    /// (CLAUDE.md rule 7) cannot afford. Callers read it, they never keep it.
    public sealed class PoseFrame
    {
        public const int JointCount = 33;

        readonly PoseLandmark[] _landmarks = new PoseLandmark[JointCount];

        /// False when the detector score fell below threshold — no body in frame. Consumers must
        /// check this before reading landmarks, which otherwise hold the last known pose.
        public bool HasPose { get; private set; }

        /// Detector confidence for the pose currently held, in [0,1].
        public float Score { get; private set; }

        public PoseLandmark this[int index] => _landmarks[index];

        public PoseLandmark this[PoseJoint joint] => _landmarks[(int)joint];

        public void Set(int index, in PoseLandmark landmark)
        {
            if (index < 0 || index >= JointCount)
                throw new ArgumentOutOfRangeException(nameof(index));
            _landmarks[index] = landmark;
        }

        public void MarkTracked(float score)
        {
            HasPose = true;
            Score = score;
        }

        /// Tracking loss. Landmarks are deliberately left untouched so a one-frame dropout can be
        /// bridged by holding the last pose rather than snapping the character to the centre.
        public void MarkLost()
        {
            HasPose = false;
            Score = 0f;
        }
    }
}
