using System.Globalization;
using Unity.InferenceEngine;
using Unity.Mathematics;
using UnityEngine;

namespace MotionRunner.CameraInput
{
    /// Affine sampling and detector post-processing for BlazePose.
    ///
    /// Adapted from BlazeUtils.cs in Unity's official BlazeDetectionSample/Pose
    /// (github.com/Unity-Technologies/inference-engine-samples, Apache-2.0), which is the sample
    /// T-010 was told to run. Kept close to the original so it stays diffable against upstream;
    /// the only additions are SampleImageUpright and the colour-space flag.
    ///
    /// Purely the Unity-facing half. Anything that is maths rather than plumbing belongs in the
    /// engine-free MotionRunner.Pose assembly, where it can be tested without an editor.
    public static class BlazeAffine
    {
        public static float2x3 mul(float2x3 a, float2x3 b)
        {
            return new float2x3(
                a[0][0] * b[0][0] + a[1][0] * b[0][1],
                a[0][0] * b[1][0] + a[1][0] * b[1][1],
                a[0][0] * b[2][0] + a[1][0] * b[2][1] + a[2][0],
                a[0][1] * b[0][0] + a[1][1] * b[0][1],
                a[0][1] * b[1][0] + a[1][1] * b[1][1],
                a[0][1] * b[2][0] + a[1][1] * b[2][1] + a[2][1]
            );
        }

        public static float2 mul(float2x3 a, float2 b)
        {
            return new float2(
                a[0][0] * b.x + a[1][0] * b.y + a[2][0],
                a[0][1] * b.x + a[1][1] * b.y + a[2][1]
            );
        }

        public static float2x3 RotationMatrix(float theta)
        {
            var sinTheta = math.sin(theta);
            var cosTheta = math.cos(theta);
            return new float2x3(
                cosTheta, -sinTheta, 0,
                sinTheta, cosTheta, 0
            );
        }

        public static float2x3 TranslationMatrix(float2 delta)
        {
            return new float2x3(
                1, 0, delta.x,
                0, 1, delta.y
            );
        }

        public static float2x3 ScaleMatrix(float2 scale)
        {
            return new float2x3(
                scale.x, 0, 0,
                0, scale.y, 0
            );
        }

        static FunctionalTensor ScoreFiltering(FunctionalTensor rawScores, float scoreThreshold)
        {
            return Functional.Sigmoid(Functional.Clamp(rawScores, -scoreThreshold, scoreThreshold));
        }

        /// Folds "pick the highest-scoring anchor" into the detector model itself, so the 2254
        /// candidate boxes never cross the GPU-to-CPU boundary. Without this the readback alone
        /// would cost more than the inference.
        public static (FunctionalTensor, FunctionalTensor, FunctionalTensor) ArgMaxFiltering(
            FunctionalTensor rawBoxes, FunctionalTensor rawScores)
        {
            var detectionScores = ScoreFiltering(rawScores, 100f); // (1, 2254, 1)
            var bestScoreIndex = Functional.ArgMax(rawScores, 1).Squeeze();

            var selectedBoxes = Functional.IndexSelect(rawBoxes, 1, bestScoreIndex).Unsqueeze(0);
            var selectedScores = Functional.IndexSelect(detectionScores, 1, bestScoreIndex).Unsqueeze(0);

            return (bestScoreIndex, selectedScores, selectedBoxes);
        }

        const string ShaderPath = "CameraInput/ImageTransform";

        static ComputeShader s_Shader;
        static int s_ImageSample;
        static int s_ImageUpright;

        static readonly int s_Optr = Shader.PropertyToID("Optr");
        static readonly int s_X_tex2D = Shader.PropertyToID("X_tex2D");
        static readonly int s_O_height = Shader.PropertyToID("O_height");
        static readonly int s_O_width = Shader.PropertyToID("O_width");
        static readonly int s_O_channels = Shader.PropertyToID("O_channels");
        static readonly int s_X_height = Shader.PropertyToID("X_height");
        static readonly int s_X_width = Shader.PropertyToID("X_width");
        static readonly int s_affineMatrix = Shader.PropertyToID("affineMatrix");
        static readonly int s_O_srgbEncode = Shader.PropertyToID("O_srgbEncode");
        static readonly int s_U_out = Shader.PropertyToID("U_out");
        static readonly int s_U_width = Shader.PropertyToID("U_width");
        static readonly int s_U_height = Shader.PropertyToID("U_height");
        static readonly int s_U_row0 = Shader.PropertyToID("U_row0");
        static readonly int s_U_row1 = Shader.PropertyToID("U_row1");

        /// A sampler only returns linear values when the project renders in Linear space; in Gamma
        /// it hands back the stored sRGB directly. BlazePose wants sRGB either way, so the encode
        /// step in the kernel is needed in exactly one of the two cases.
        static int SrgbEncode =>
            QualitySettings.activeColorSpace == ColorSpace.Linear ? 1 : 0;

        static ComputeShader Shader_()
        {
            if (s_Shader != null) return s_Shader;

            s_Shader = Resources.Load<ComputeShader>(ShaderPath);
            if (s_Shader == null)
            {
                Debug.LogError("BlazeAffine: Resources/" + ShaderPath + ".compute is missing.");
                return null;
            }

            s_ImageSample = s_Shader.FindKernel("ImageSample");
            s_ImageUpright = s_Shader.FindKernel("ImageUpright");
            return s_Shader;
        }

        static int IDivC(int v, int div) => (v + div - 1) / div;

        /// Samples a source texture through an affine straight into an Inference Engine tensor.
        public static void SampleImageAffine(Texture srcTexture, Tensor<float> dstTensor, float2x3 M)
        {
            ComputeShader shader = Shader_();
            if (shader == null) return;

            var tensorData = ComputeTensorData.Pin(dstTensor, false);

            shader.SetTexture(s_ImageSample, s_X_tex2D, srcTexture);
            shader.SetBuffer(s_ImageSample, s_Optr, tensorData.buffer);

            shader.SetInt(s_O_height, dstTensor.shape[1]);
            shader.SetInt(s_O_width, dstTensor.shape[2]);
            shader.SetInt(s_O_channels, dstTensor.shape[3]);
            shader.SetInt(s_X_height, srcTexture.height);
            shader.SetInt(s_X_width, srcTexture.width);
            shader.SetInt(s_O_srgbEncode, SrgbEncode);

            shader.SetMatrix(s_affineMatrix, new Matrix4x4(
                new Vector4(M[0][0], M[0][1]),
                new Vector4(M[1][0], M[1][1]),
                new Vector4(M[2][0], M[2][1]),
                Vector4.zero));

            shader.Dispatch(s_ImageSample, IDivC(dstTensor.shape[1], 8), IDivC(dstTensor.shape[1], 8), 1);
        }

        /// Straightens the raw camera frame into `dst` using the affine computed (and unit-tested)
        /// by MotionRunner.Pose.FrameOrientation. Everything downstream then shares one space.
        public static void SampleImageUpright(Texture srcTexture, RenderTexture dst,
            in MotionRunner.Pose.PoseAffine2x3 sourceUvFromUpright)
        {
            ComputeShader shader = Shader_();
            if (shader == null) return;

            shader.SetTexture(s_ImageUpright, s_X_tex2D, srcTexture);
            shader.SetTexture(s_ImageUpright, s_U_out, dst);
            shader.SetInt(s_U_width, dst.width);
            shader.SetInt(s_U_height, dst.height);
            shader.SetVector(s_U_row0, new Vector4(
                sourceUvFromUpright.M00, sourceUvFromUpright.M01, sourceUvFromUpright.M02, 0f));
            shader.SetVector(s_U_row1, new Vector4(
                sourceUvFromUpright.M10, sourceUvFromUpright.M11, sourceUvFromUpright.M12, 0f));

            shader.Dispatch(s_ImageUpright, IDivC(dst.width, 8), IDivC(dst.height, 8), 1);
        }

        public static float[,] LoadAnchors(string csv, int numAnchors)
        {
            var anchors = new float[numAnchors, 4];
            var anchorLines = csv.Split('\n');

            for (var i = 0; i < numAnchors; i++)
            {
                var anchorValues = anchorLines[i].Split(',');
                for (var j = 0; j < 4; j++)
                    anchors[i, j] = float.Parse(anchorValues[j], CultureInfo.InvariantCulture);
            }

            return anchors;
        }
    }
}
