namespace MotionRunner.Pose
{
    /// The 33 BlazePose landmarks, in the order the landmarker model emits them
    /// (MediaPipe pose topology). The numeric values are the model's output indices —
    /// they are a wire format, not an internal choice, so never reorder them.
    ///
    /// Left/right are the *subject's* left and right, not the viewer's. On a selfie camera
    /// the image is mirrored, so the subject's left hand appears on the right of the frame.
    /// T-012 must steer by torso position in image space, not by joint name, or camera mode
    /// will steer backwards.
    public enum PoseJoint
    {
        Nose = 0,
        LeftEyeInner = 1,
        LeftEye = 2,
        LeftEyeOuter = 3,
        RightEyeInner = 4,
        RightEye = 5,
        RightEyeOuter = 6,
        LeftEar = 7,
        RightEar = 8,
        MouthLeft = 9,
        MouthRight = 10,
        LeftShoulder = 11,
        RightShoulder = 12,
        LeftElbow = 13,
        RightElbow = 14,
        LeftWrist = 15,
        RightWrist = 16,
        LeftPinky = 17,
        RightPinky = 18,
        LeftIndex = 19,
        RightIndex = 20,
        LeftThumb = 21,
        RightThumb = 22,
        LeftHip = 23,
        RightHip = 24,
        LeftKnee = 25,
        RightKnee = 26,
        LeftAnkle = 27,
        RightAnkle = 28,
        LeftHeel = 29,
        RightHeel = 30,
        LeftFootIndex = 31,
        RightFootIndex = 32
    }
}
