namespace MotionRunner.Pose
{
    /// A 2D affine transform, row-major:  x' = M00*x + M01*y + M02,  y' = M10*x + M11*y + M12.
    ///
    /// Hand-rolled rather than Unity.Mathematics.float2x3 because this assembly is engine-free
    /// (noEngineReferences), which is the whole point of putting the orientation maths here: a
    /// front-camera rotation bug is a five-minute unit test and an hour of device round-trips.
    public readonly struct PoseAffine2x3
    {
        public readonly float M00;
        public readonly float M01;
        public readonly float M02;
        public readonly float M10;
        public readonly float M11;
        public readonly float M12;

        public PoseAffine2x3(float m00, float m01, float m02, float m10, float m11, float m12)
        {
            M00 = m00;
            M01 = m01;
            M02 = m02;
            M10 = m10;
            M11 = m11;
            M12 = m12;
        }

        public static PoseAffine2x3 Identity => new PoseAffine2x3(1f, 0f, 0f, 0f, 1f, 0f);

        public static PoseAffine2x3 Translation(float dx, float dy) =>
            new PoseAffine2x3(1f, 0f, dx, 0f, 1f, dy);

        public static PoseAffine2x3 Scale(float sx, float sy) =>
            new PoseAffine2x3(sx, 0f, 0f, 0f, sy, 0f);

        public PosePoint Apply(in PosePoint p) => new PosePoint(
            M00 * p.X + M01 * p.Y + M02,
            M10 * p.X + M11 * p.Y + M12);

        public PosePoint Apply(float x, float y) => new PosePoint(
            M00 * x + M01 * y + M02,
            M10 * x + M11 * y + M12);

        /// Function composition: (a * b).Apply(p) == a.Apply(b.Apply(p)).
        public static PoseAffine2x3 Multiply(in PoseAffine2x3 a, in PoseAffine2x3 b) =>
            new PoseAffine2x3(
                a.M00 * b.M00 + a.M01 * b.M10,
                a.M00 * b.M01 + a.M01 * b.M11,
                a.M00 * b.M02 + a.M01 * b.M12 + a.M02,
                a.M10 * b.M00 + a.M11 * b.M10,
                a.M10 * b.M01 + a.M11 * b.M11,
                a.M10 * b.M02 + a.M11 * b.M12 + a.M12);
    }
}
