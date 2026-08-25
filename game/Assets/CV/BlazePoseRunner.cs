using System;
using System.Diagnostics;
using MotionRunner.CameraInput;
using MotionRunner.Pose;
using Unity.InferenceEngine;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MotionRunner.Cv
{
    /// The BlazePose two-stage pipeline: a detector finds the body and returns a bounding box plus
    /// two alignment keypoints, then a landmarker runs on a rotated crop around them and returns
    /// 33 landmarks. Ported from Unity's official BlazeDetectionSample/Pose (Apache-2.0).
    ///
    /// What T-010 adds to the sample is measurement. Each stage is timed separately, because the
    /// two have completely different duty cycles: the landmarker is the per-frame cost that has to
    /// fit inside a 16 ms budget, while the detector runs only when tracking is lost (T-011). A
    /// single combined number would answer neither question, and T-013's gate needs the landmarker
    /// figure specifically.
    ///
    /// Nothing here touches gameplay. The only route from pose to game is a future IGameInput
    /// adapter (T-013), per CLAUDE.md rule 2.
    public sealed class BlazePoseRunner : IDisposable
    {
        public enum LandmarkerVariant
        {
            Lite,
            Full
        }

        const int AnchorCount = 2254;
        const int DetectorInputSize = 224;
        const int LandmarkerInputSize = 256;
        const int LandmarkStride = 5; // x, y, z, visibility, presence

        /// Below this the detector is reporting noise rather than a body.
        public float ScoreThreshold { get; set; } = 0.6f;

        public PoseFrame Frame { get; } = new PoseFrame();

        /// Per-stage medians. The headline T-010 number is LandmarkerMs.Median().
        public LatencyStats DetectorMs { get; } = new LatencyStats(240);
        public LatencyStats LandmarkerMs { get; } = new LatencyStats(240);

        /// Detector plus landmarker plus the CPU glue between them — what one full cycle costs
        /// when tracking has to be re-established from scratch.
        public LatencyStats CycleMs { get; } = new LatencyStats(240);

        public bool IsLoaded => _detectorWorker != null && _landmarkerWorker != null;
        public LandmarkerVariant Variant { get; private set; }
        public BackendType Backend { get; private set; }

        /// Detector score of the most recent cycle, whether or not it passed the threshold. The
        /// orientation probe ranks candidate rotations by this.
        public float LastScore { get; private set; }

        float[,] _anchors;
        Worker _detectorWorker;
        Worker _landmarkerWorker;
        Tensor<float> _detectorInput;
        Tensor<float> _landmarkerInput;
        readonly Stopwatch _watch = new Stopwatch();

        public bool Load(ModelAsset detector, ModelAsset landmarker, TextAsset anchorsCsv,
            LandmarkerVariant variant, BackendType backend = BackendType.GPUCompute)
        {
            if (detector == null || landmarker == null || anchorsCsv == null)
            {
                Debug.LogError("[CV] BlazePoseRunner.Load: a model or the anchor table is missing.");
                return false;
            }

            Variant = variant;
            Backend = backend;
            _anchors = BlazeAffine.LoadAnchors(anchorsCsv.text, AnchorCount);

            // Fold "argmax over 2254 anchors" into the detector graph so only the winning box is
            // read back. Doing it on the CPU would cost more than the inference it follows.
            Model detectorModel = ModelLoader.Load(detector);
            var graph = new FunctionalGraph();
            FunctionalTensor input = graph.AddInput(detectorModel, 0);
            FunctionalTensor[] outputs = Functional.Forward(detectorModel, input);
            (FunctionalTensor idx, FunctionalTensor score, FunctionalTensor box) =
                BlazeAffine.ArgMaxFiltering(outputs[0], outputs[1]);
            detectorModel = graph.Compile(idx, score, box);

            _detectorWorker = new Worker(detectorModel, backend);
            _landmarkerWorker = new Worker(ModelLoader.Load(landmarker), backend);

            _detectorInput = new Tensor<float>(
                new TensorShape(1, DetectorInputSize, DetectorInputSize, 3));
            _landmarkerInput = new Tensor<float>(
                new TensorShape(1, LandmarkerInputSize, LandmarkerInputSize, 3));

            Debug.Log($"[CV] BlazePose loaded: landmarker={variant}, backend={backend}");
            return true;
        }

        /// One detector + landmarker pass over an already-upright frame.
        ///
        /// <param name="record">False during warm-up. The first inferences on a device include
        /// compute-kernel compilation and allocation and run several times slower than steady
        /// state, so folding them into the statistics would report a number no player ever
        /// experiences (handoff 8.2 step 1: measure over warm inferences).</param>
        /// <returns>True when a pose was found and Frame was updated.</returns>
        public async Awaitable<bool> RunCycleAsync(Texture upright, bool record)
        {
            if (!IsLoaded || upright == null) return false;

            float textureWidth = upright.width;
            float textureHeight = upright.height;
            float size = Mathf.Max(textureWidth, textureHeight);

            double cycleStart = Now();

            // Tensor coordinates -> texture pixels, letterboxing the frame into a square.
            float scale = size / DetectorInputSize;
            float2x3 M = BlazeAffine.mul(
                BlazeAffine.TranslationMatrix(
                    0.5f * (new float2(textureWidth, textureHeight) + new float2(-size, size))),
                BlazeAffine.ScaleMatrix(new float2(scale, -scale)));
            BlazeAffine.SampleImageAffine(upright, _detectorInput, M);

            _watch.Restart();
            _detectorWorker.Schedule(_detectorInput);

            Awaitable<Tensor<int>> idxAwaitable =
                (_detectorWorker.PeekOutput(0) as Tensor<int>).ReadbackAndCloneAsync();
            Awaitable<Tensor<float>> scoreAwaitable =
                (_detectorWorker.PeekOutput(1) as Tensor<float>).ReadbackAndCloneAsync();
            Awaitable<Tensor<float>> boxAwaitable =
                (_detectorWorker.PeekOutput(2) as Tensor<float>).ReadbackAndCloneAsync();

            using Tensor<int> outputIdx = await idxAwaitable;
            using Tensor<float> outputScore = await scoreAwaitable;
            using Tensor<float> outputBox = await boxAwaitable;
            _watch.Stop();
            if (record) DetectorMs.Add(_watch.Elapsed.TotalMilliseconds);

            LastScore = outputScore[0];
            if (LastScore < ScoreThreshold)
            {
                Frame.MarkLost();
                if (record) CycleMs.Add(Now() - cycleStart);
                return false;
            }

            int idx = outputIdx[0];
            float2 anchor = DetectorInputSize * new float2(_anchors[idx, 0], _anchors[idx, 1]);

            // Two alignment keypoints (hip midpoint and a point up the torso axis) define the
            // rotated square the landmarker actually sees.
            float2 kp1 = BlazeAffine.mul(M, anchor + new float2(outputBox[0, 0, 4], outputBox[0, 0, 5]));
            float2 kp2 = BlazeAffine.mul(M, anchor + new float2(outputBox[0, 0, 6], outputBox[0, 0, 7]));
            float2 delta = kp2 - kp1;

            const float cropScale = 1.25f;
            float radius = cropScale * math.length(delta);
            if (radius <= Mathf.Epsilon)
            {
                Frame.MarkLost();
                if (record) CycleMs.Add(Now() - cycleStart);
                return false;
            }

            float theta = math.atan2(delta.y, delta.x);
            var origin = new float2(0.5f * LandmarkerInputSize, 0.5f * LandmarkerInputSize);
            float cropPixelsPerTexel = radius / (0.5f * LandmarkerInputSize);

            float2x3 M2 = BlazeAffine.mul(
                BlazeAffine.mul(
                    BlazeAffine.mul(
                        BlazeAffine.TranslationMatrix(kp1),
                        BlazeAffine.ScaleMatrix(new float2(cropPixelsPerTexel, -cropPixelsPerTexel))),
                    BlazeAffine.RotationMatrix(0.5f * Mathf.PI - theta)),
                BlazeAffine.TranslationMatrix(-origin));

            BlazeAffine.SampleImageAffine(upright, _landmarkerInput, M2);

            _watch.Restart();
            _landmarkerWorker.Schedule(_landmarkerInput);
            Awaitable<Tensor<float>> landmarksAwaitable =
                (_landmarkerWorker.PeekOutput("Identity") as Tensor<float>).ReadbackAndCloneAsync();
            using Tensor<float> landmarks = await landmarksAwaitable; // (1, 195)
            _watch.Stop();
            if (record) LandmarkerMs.Add(_watch.Elapsed.TotalMilliseconds);

            for (int i = 0; i < PoseFrame.JointCount; i++)
            {
                float2 pixel = BlazeAffine.mul(M2, new float2(
                    landmarks[LandmarkStride * i + 0],
                    landmarks[LandmarkStride * i + 1]));

                // Normalised, origin top-left, matching MotionRunner.Pose's convention. The
                // landmarker's own y runs the other way, hence the flip.
                Frame.Set(i, new PoseLandmark(
                    pixel.x / textureWidth,
                    1f - pixel.y / textureHeight,
                    landmarks[LandmarkStride * i + 2] / textureHeight,
                    landmarks[LandmarkStride * i + 3],
                    landmarks[LandmarkStride * i + 4]));
            }

            Frame.MarkTracked(LastScore);
            if (record) CycleMs.Add(Now() - cycleStart);
            return true;
        }

        static double Now() => Stopwatch.GetTimestamp() * 1000d / Stopwatch.Frequency;

        public void Dispose()
        {
            _detectorWorker?.Dispose();
            _landmarkerWorker?.Dispose();
            _detectorInput?.Dispose();
            _landmarkerInput?.Dispose();
            _detectorWorker = null;
            _landmarkerWorker = null;
            _detectorInput = null;
            _landmarkerInput = null;
        }
    }
}
