namespace MotionRunner.Pose
{
    /// Body geometry derived from landmarks. Handoff 8.3 is explicit that camera control is
    /// geometry and thresholds, not a classifier — this is where that geometry lives, engine-free
    /// so T-012 can measure its false-positive rate headlessly instead of by hand.
    ///
    /// Everything here is a pure function of one frame. Anything with memory — smoothing (T-011),
    /// neutral bands and jump velocity (T-012) — belongs in a stateful type, not in here.
    public static class PoseGeometry
    {
        /// Midpoint of the two shoulders, or NotFound when either is untracked.
        public static bool TryShoulderCenter(PoseFrame frame, out PosePoint center) =>
            TryMidpoint(frame, PoseJoint.LeftShoulder, PoseJoint.RightShoulder, out center);

        /// Midpoint of the two hips. This is also BlazePose's own depth origin.
        public static bool TryHipCenter(PoseFrame frame, out PosePoint center) =>
            TryMidpoint(frame, PoseJoint.LeftHip, PoseJoint.RightHip, out center);

        /// The steering reference point: the centre of the shoulder-hip quad.
        ///
        /// Shoulders and hips together rather than hips alone, because hips are the joints most
        /// often cropped out of frame when someone stands close to a phone on a table, and a
        /// reference point that vanishes is worse than one that drifts. Falls back to whichever
        /// pair is available.
        public static bool TryTorsoCenter(PoseFrame frame, out PosePoint center)
        {
            bool hasShoulders = TryShoulderCenter(frame, out PosePoint shoulders);
            bool hasHips = TryHipCenter(frame, out PosePoint hips);

            if (hasShoulders && hasHips)
            {
                center = new PosePoint(0.5f * (shoulders.X + hips.X), 0.5f * (shoulders.Y + hips.Y));
                return true;
            }

            if (hasShoulders)
            {
                center = shoulders;
                return true;
            }

            if (hasHips)
            {
                center = hips;
                return true;
            }

            center = default;
            return false;
        }

        /// Shoulder separation in normalised frame units. The natural scale for every distance
        /// threshold downstream: it shrinks as the player steps back from the camera, so a
        /// threshold expressed as a fraction of it is distance-invariant.
        public static bool TryShoulderWidth(PoseFrame frame, out float width)
        {
            PoseLandmark left = frame[PoseJoint.LeftShoulder];
            PoseLandmark right = frame[PoseJoint.RightShoulder];

            if (!frame.HasPose || !left.IsTracked || !right.IsTracked)
            {
                width = 0f;
                return false;
            }

            float dx = left.X - right.X;
            float dy = left.Y - right.Y;
            width = Sqrt(dx * dx + dy * dy);
            return true;
        }

        static bool TryMidpoint(PoseFrame frame, PoseJoint a, PoseJoint b, out PosePoint center)
        {
            PoseLandmark first = frame[a];
            PoseLandmark second = frame[b];

            if (!frame.HasPose || !first.IsTracked || !second.IsTracked)
            {
                center = default;
                return false;
            }

            center = new PosePoint(0.5f * (first.X + second.X), 0.5f * (first.Y + second.Y));
            return true;
        }

        // System.Math rather than UnityEngine.Mathf: this assembly has noEngineReferences.
        static float Sqrt(float value) => (float)System.Math.Sqrt(value);
    }
}
