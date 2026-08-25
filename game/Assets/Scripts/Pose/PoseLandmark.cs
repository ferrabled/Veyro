namespace MotionRunner.Pose
{
    /// A 2D point in normalised frame space: (0,0) top-left, (1,1) bottom-right of the camera
    /// image. Normalised rather than pixels so gesture thresholds (T-012) survive a change of
    /// capture resolution.
    public readonly struct PosePoint
    {
        public readonly float X;
        public readonly float Y;

        public PosePoint(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

    /// One landmark as the BlazePose landmarker reports it.
    ///
    /// The model emits five floats per joint: x, y, z, visibility, presence
    /// (https://arxiv.org/pdf/2006.10204). Visibility is "is this joint occluded", presence is
    /// "is this joint inside the crop at all" — a joint can score high on one and low on the
    /// other, so both gate IsTracked. Z is depth relative to the hip midpoint in the same units
    /// as X, and is far noisier than X/Y; T-012 must not build a gesture on Z alone.
    public readonly struct PoseLandmark
    {
        /// Both scores are logits pushed through a sigmoid by the model, so 0.5 is the natural
        /// "more likely than not" cut. Kept as a named constant because T-011/T-012 will want to
        /// tune it against real footage rather than rediscover where the number came from.
        public const float TrackedThreshold = 0.5f;

        public readonly float X;
        public readonly float Y;
        public readonly float Z;
        public readonly float Visibility;
        public readonly float Presence;

        public PoseLandmark(float x, float y, float z, float visibility, float presence)
        {
            X = x;
            Y = y;
            Z = z;
            Visibility = visibility;
            Presence = presence;
        }

        public bool IsTracked =>
            Visibility >= TrackedThreshold && Presence >= TrackedThreshold;

        public PosePoint Point => new PosePoint(X, Y);
    }
}
