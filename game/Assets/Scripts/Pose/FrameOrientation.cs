using System;

namespace MotionRunner.Pose
{
    /// Turns a raw camera frame into an upright one.
    ///
    /// Handoff 8.2 names this as the fiddly part of the whole CV track: "the fiddly parts are not
    /// the ML: they are Android WebCamTexture rotation/mirroring (front cameras report rotated,
    /// mirrored frames per device)". BlazePose is trained on upright people, so a sideways frame
    /// does not degrade tracking, it simply fails to find anybody — and on a phone that failure
    /// looks identical to "the model is no good".
    ///
    /// The output of this class is a single affine that every stage downstream shares: the frame
    /// is straightened once, and after that detector input, landmarker crop, the on-screen preview
    /// and the gesture geometry are all in one coordinate space. Getting that wrong in four places
    /// independently is how a week disappears.
    ///
    /// Coordinates are normalised, origin top-left, x right and y down, in both spaces.
    public static class FrameOrientation
    {
        public readonly struct Result
        {
            /// Upright frame size in pixels. Width and height are swapped from the source for a
            /// quarter turn, which is the case that matters: a 640x480 landscape sensor feeding a
            /// portrait phone becomes a 480x640 upright frame.
            public readonly int Width;
            public readonly int Height;

            /// Maps a normalised point in the upright frame to the normalised source uv to sample.
            /// This is the direction a sampler needs — for each destination pixel, where to read.
            public readonly PoseAffine2x3 SourceUvFromUpright;

            public Result(int width, int height, in PoseAffine2x3 sourceUvFromUpright)
            {
                Width = width;
                Height = height;
                SourceUvFromUpright = sourceUvFromUpright;
            }
        }

        /// <param name="sourceWidth">WebCamTexture.width.</param>
        /// <param name="sourceHeight">WebCamTexture.height.</param>
        /// <param name="rotationDegrees">WebCamTexture.videoRotationAngle — the clockwise turn
        /// needed to display the raw texture upright. Must be a multiple of 90.</param>
        /// <param name="verticallyMirrored">WebCamTexture.videoVerticallyMirrored — the raw
        /// texture is already flipped top-to-bottom. A property of the source, so it is corrected
        /// in source space, before rotation is considered.</param>
        /// <param name="mirrorHorizontally">Show the player a mirror, as a selfie camera should.
        /// Cosmetic for the model — BlazePose reads a mirrored body fine — but it flips which side
        /// of the frame the subject's left arm appears on, so T-012 must steer by frame position
        /// and never by joint name.</param>
        public static Result ForCamera(int sourceWidth, int sourceHeight, int rotationDegrees,
            bool verticallyMirrored, bool mirrorHorizontally)
        {
            if (sourceWidth <= 0) throw new ArgumentOutOfRangeException(nameof(sourceWidth));
            if (sourceHeight <= 0) throw new ArgumentOutOfRangeException(nameof(sourceHeight));

            int turns = NormalizeTurns(rotationDegrees);
            bool quarterTurn = turns == 1 || turns == 3;

            int width = quarterTurn ? sourceHeight : sourceWidth;
            int height = quarterTurn ? sourceWidth : sourceHeight;

            // Read right to left: mirror the upright frame, undo the display rotation to land in
            // raw-source space, then undo the source's own vertical flip.
            PoseAffine2x3 m = Unrotate(turns);
            if (mirrorHorizontally) m = PoseAffine2x3.Multiply(m, FlipX);
            if (verticallyMirrored) m = PoseAffine2x3.Multiply(FlipY, m);

            return new Result(width, height, m);
        }

        static readonly PoseAffine2x3 FlipX = new PoseAffine2x3(-1f, 0f, 1f, 0f, 1f, 0f);
        static readonly PoseAffine2x3 FlipY = new PoseAffine2x3(1f, 0f, 0f, 0f, -1f, 1f);

        /// Inverse of "rotate the source clockwise by `turns` quarter turns": for a point in the
        /// upright frame, which source point it came from.
        static PoseAffine2x3 Unrotate(int turns)
        {
            switch (turns)
            {
                case 1: return new PoseAffine2x3(0f, 1f, 0f, -1f, 0f, 1f);  // (u,v) -> (v, 1-u)
                case 2: return new PoseAffine2x3(-1f, 0f, 1f, 0f, -1f, 1f); // (u,v) -> (1-u, 1-v)
                case 3: return new PoseAffine2x3(0f, -1f, 1f, 1f, 0f, 0f);  // (u,v) -> (1-v, u)
                default: return PoseAffine2x3.Identity;
            }
        }

        static int NormalizeTurns(int rotationDegrees)
        {
            if (rotationDegrees % 90 != 0)
                throw new ArgumentOutOfRangeException(nameof(rotationDegrees),
                    "camera rotation is always a multiple of 90, got " + rotationDegrees);

            int turns = (rotationDegrees / 90) % 4;
            return turns < 0 ? turns + 4 : turns;
        }
    }
}
