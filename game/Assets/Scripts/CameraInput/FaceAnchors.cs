using Unity.Mathematics;

namespace MotionRunner.CameraInput
{
    /// The 896 SSD anchor centres for BlazeFace short-range (128x128 input), generated in code
    /// instead of shipped as a CSV: unlike the pose anchors they follow a two-line rule, and a
    /// generator cannot drift out of sync with a data file nobody re-exports.
    ///
    /// Layout per MediaPipe's SsdAnchorsCalculator config for face_detection_short_range:
    /// strides {8,16,16,16}, anchor_offset 0.5, fixed_anchor_size (so only centres matter for
    /// decoding). Stride 8 contributes a 16x16 grid with 2 anchors per cell (512); the three
    /// stride-16 layers merge into an 8x8 grid with 6 per cell (384). Cell order is row-major,
    /// which must match the model's output ordering exactly — verified by FaceDetector's startup
    /// self-check picking a plausible box for a synthetic input.
    public static class FaceAnchors
    {
        public const int Count = 896;
        public const int InputSize = 128;

        public static float2[] Generate()
        {
            var centres = new float2[Count];
            int i = 0;

            // Stride 8: 16x16 cells, 2 anchors per cell.
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    for (int a = 0; a < 2; a++)
                        centres[i++] = new float2((x + 0.5f) / 16f, (y + 0.5f) / 16f);

            // Strides 16,16,16 merged: 8x8 cells, 6 anchors per cell.
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    for (int a = 0; a < 6; a++)
                        centres[i++] = new float2((x + 0.5f) / 8f, (y + 0.5f) / 8f);

            return centres;
        }
    }
}
