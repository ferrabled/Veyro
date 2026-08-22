namespace MotionRunner.Pose
{
    /// Which landmarks to join up when drawing a stick figure (MediaPipe POSE_CONNECTIONS).
    ///
    /// Purely a display concern, but it lives here rather than in the Unity overlay so the
    /// topology is one table with one owner, and so a test can assert every index is a real joint.
    public static class PoseSkeleton
    {
        /// Flat pairs: Bones[2i] connects to Bones[2i+1]. A flat array rather than an array of
        /// tuples so drawing code can walk it without allocating.
        public static readonly int[] Bones =
        {
            // face
            (int)PoseJoint.Nose, (int)PoseJoint.LeftEyeInner,
            (int)PoseJoint.LeftEyeInner, (int)PoseJoint.LeftEye,
            (int)PoseJoint.LeftEye, (int)PoseJoint.LeftEyeOuter,
            (int)PoseJoint.LeftEyeOuter, (int)PoseJoint.LeftEar,
            (int)PoseJoint.Nose, (int)PoseJoint.RightEyeInner,
            (int)PoseJoint.RightEyeInner, (int)PoseJoint.RightEye,
            (int)PoseJoint.RightEye, (int)PoseJoint.RightEyeOuter,
            (int)PoseJoint.RightEyeOuter, (int)PoseJoint.RightEar,
            (int)PoseJoint.MouthLeft, (int)PoseJoint.MouthRight,

            // arms
            (int)PoseJoint.LeftShoulder, (int)PoseJoint.LeftElbow,
            (int)PoseJoint.LeftElbow, (int)PoseJoint.LeftWrist,
            (int)PoseJoint.LeftWrist, (int)PoseJoint.LeftPinky,
            (int)PoseJoint.LeftWrist, (int)PoseJoint.LeftIndex,
            (int)PoseJoint.LeftWrist, (int)PoseJoint.LeftThumb,
            (int)PoseJoint.LeftPinky, (int)PoseJoint.LeftIndex,
            (int)PoseJoint.RightShoulder, (int)PoseJoint.RightElbow,
            (int)PoseJoint.RightElbow, (int)PoseJoint.RightWrist,
            (int)PoseJoint.RightWrist, (int)PoseJoint.RightPinky,
            (int)PoseJoint.RightWrist, (int)PoseJoint.RightIndex,
            (int)PoseJoint.RightWrist, (int)PoseJoint.RightThumb,
            (int)PoseJoint.RightPinky, (int)PoseJoint.RightIndex,

            // torso — the quad T-012 steers by
            (int)PoseJoint.LeftShoulder, (int)PoseJoint.RightShoulder,
            (int)PoseJoint.LeftShoulder, (int)PoseJoint.LeftHip,
            (int)PoseJoint.RightShoulder, (int)PoseJoint.RightHip,
            (int)PoseJoint.LeftHip, (int)PoseJoint.RightHip,

            // legs
            (int)PoseJoint.LeftHip, (int)PoseJoint.LeftKnee,
            (int)PoseJoint.LeftKnee, (int)PoseJoint.LeftAnkle,
            (int)PoseJoint.LeftAnkle, (int)PoseJoint.LeftHeel,
            (int)PoseJoint.LeftHeel, (int)PoseJoint.LeftFootIndex,
            (int)PoseJoint.LeftAnkle, (int)PoseJoint.LeftFootIndex,
            (int)PoseJoint.RightHip, (int)PoseJoint.RightKnee,
            (int)PoseJoint.RightKnee, (int)PoseJoint.RightAnkle,
            (int)PoseJoint.RightAnkle, (int)PoseJoint.RightHeel,
            (int)PoseJoint.RightHeel, (int)PoseJoint.RightFootIndex,
            (int)PoseJoint.RightAnkle, (int)PoseJoint.RightFootIndex
        };

        public static int BoneCount => Bones.Length / 2;
    }
}
