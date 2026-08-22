using System;
using System.Diagnostics;
using MotionRunner.Pose;
using Unity.InferenceEngine;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MotionRunner.CameraInput
{
    /// One face observation in the upright frame's coordinates: normalized [0,1], origin
    /// top-left, y down — the PoseLandmark convention FaceSteering expects.
    public readonly struct FaceObservation
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Size;   // face box width, normalized to frame width
        public readonly float Score;

        public FaceObservation(float x, float y, float size, float score)
        {
            X = x;
            Y = y;
            Size = size;
            Score = score;
        }

        public bool HasFace => Score > 0f;
    }

    /// BlazeFace short-range on the CPU (Burst) backend — the configuration the T-010b sweep
    /// measured at 4.2 ms blocking / 17 ms awaited on the Nord 2, which is what clears the 30 ms
    /// gate (STATUS 22 Aug). The detector runs every camera frame; there is no landmarker and no
    /// tracking loop to lose, which is why the whole pose detector pipeline isn't here.
    ///
    /// The argmax-over-896-anchors is folded into the model graph (same trick as the pose spike)
    /// so a single box crosses the tensor boundary, not the whole anchor grid.
    public sealed class FaceDetector : IDisposable
    {
        public const string ModelResourcePath = "CameraInput/blaze_face_short_range";

        /// Below this the detector is reporting noise rather than a face.
        public float ScoreThreshold { get; set; } = 0.65f;

        /// Detector score of the most recent completed inference, thresholded or not — the
        /// orientation probe ranks candidate rotations by this.
        public float LastScore { get; private set; }

        public bool IsLoaded => _worker != null;

        Worker _worker;
        Tensor<float> _input;
        float2[] _anchors;
        float2x3 _letterbox;
        float _frameWidth;
        float _frameHeight;

        public bool Load()
        {
            var asset = Resources.Load<ModelAsset>(ModelResourcePath);
            if (asset == null)
            {
                Debug.LogError("[CAM] face model missing from Resources: " + ModelResourcePath);
                return false;
            }

            Model model = ModelLoader.Load(asset);

            // Outputs are (boxes, scores) in Unity's conversion, same as the pose detector; the
            // self-check below catches it if a future model re-export swaps them.
            var graph = new FunctionalGraph();
            FunctionalTensor input = graph.AddInput(model, 0);
            FunctionalTensor[] outputs = Functional.Forward(model, input);
            (FunctionalTensor idx, FunctionalTensor score, FunctionalTensor box) =
                BlazeAffine.ArgMaxFiltering(outputs[0], outputs[1]);
            model = graph.Compile(idx, score, box);

            // CPU on purpose: 2.5-25x faster than either GPU backend on the Mali target, and it
            // leaves the GPU entirely to rendering (T-010/T-010b).
            _worker = new Worker(model, BackendType.CPU);
            _anchors = FaceAnchors.Generate();
            _input = new Tensor<float>(
                new TensorShape(1, FaceAnchors.InputSize, FaceAnchors.InputSize, 3));

            if (!SelfCheck())
            {
                Debug.LogError("[CAM] face model self-check failed — camera mode disabled.");
                Dispose();
                return false;
            }

            return true;
        }

        /// The box output of the compiled graph must be 16 wide (4 box values + 6 keypoints x 2).
        /// If a re-exported model ever swaps its two outputs, this is what fails — loudly, at
        /// load, instead of as silently absurd steering.
        bool SelfCheck()
        {
            _worker.Schedule(_input);
            using Tensor box = _worker.PeekOutput(2).ReadbackAndClone();
            int width = box.shape[box.shape.rank - 1];
            if (width == 16) return true;
            Debug.LogError($"[CAM] unexpected box tensor width {width} (wanted 16) — output order swapped?");
            return false;
        }

        /// The startup gate (T-013): median *blocking* inference cost. Blocking, not awaited,
        /// because awaited readbacks quantize to frame ticks and would fail devices that are
        /// actually fine — measured on 22 Aug (33.6 ms awaited vs 4.2 ms blocking, same phone).
        public bool TryGate(out double medianMs, int warmup = 3, int iterations = 10)
        {
            var stats = new LatencyStats(iterations);
            var watch = new Stopwatch();
            for (int i = 0; i < warmup + iterations; i++)
            {
                watch.Restart();
                _worker.Schedule(_input);
                using (var _ = _worker.PeekOutput(2).ReadbackAndClone()) { }
                watch.Stop();
                if (i >= warmup) stats.Add(watch.Elapsed.TotalMilliseconds);
            }

            medianMs = stats.Median();
            stats.TryGate(out bool passes, iterations);
            Debug.Log($"[CAM] gate: face blocking median {medianMs:F1} ms over {iterations} -> " +
                      (passes ? "PASS" : "FAIL") + $" (<{LatencyStats.GateMs} ms)");
            return passes;
        }

        /// Schedules one inference on the current upright frame. Await the result with
        /// ReadAsync — split so the caller controls pacing.
        public void Schedule(Texture upright)
        {
            float w = upright.width;
            float h = upright.height;
            float size = Mathf.Max(w, h);
            float scale = size / FaceAnchors.InputSize;

            // Letterbox the frame into the model's square, exactly as the pose detector did.
            _letterbox = BlazeAffine.mul(
                BlazeAffine.TranslationMatrix(0.5f * (new float2(w, h) + new float2(-size, size))),
                BlazeAffine.ScaleMatrix(new float2(scale, -scale)));
            _frameWidth = w;
            _frameHeight = h;

            BlazeAffine.SampleImageAffine(upright, _input, _letterbox);
            _worker.Schedule(_input);
        }

        /// Awaits the scheduled inference and decodes the best box. Await-based (not blocking) in
        /// the game loop: the sweep measured it at ~1 ms of main-thread time per inference, with
        /// the Burst jobs running beside rendering.
        public async Awaitable<FaceObservation> ReadAsync()
        {
            Awaitable<Tensor<int>> idxA = (_worker.PeekOutput(0) as Tensor<int>).ReadbackAndCloneAsync();
            Awaitable<Tensor<float>> scoreA = (_worker.PeekOutput(1) as Tensor<float>).ReadbackAndCloneAsync();
            Awaitable<Tensor<float>> boxA = (_worker.PeekOutput(2) as Tensor<float>).ReadbackAndCloneAsync();

            using Tensor<int> idx = await idxA;
            using Tensor<float> score = await scoreA;
            using Tensor<float> box = await boxA;

            LastScore = score[0];
            if (LastScore < ScoreThreshold) return default;

            float2 anchorPx = FaceAnchors.InputSize * _anchors[idx[0]];
            var centreTensor = new float2(anchorPx.x + box[0, 0, 0], anchorPx.y + box[0, 0, 1]);
            float2 centrePx = BlazeAffine.mul(_letterbox, centreTensor);

            float sizeNorm = box[0, 0, 2] * (_letterbox[0][0]) / _frameWidth;

            // Same flip as the pose landmarks: normalized, origin top-left, y down.
            return new FaceObservation(
                centrePx.x / _frameWidth,
                1f - centrePx.y / _frameHeight,
                sizeNorm,
                LastScore);
        }

        public void Dispose()
        {
            _worker?.Dispose();
            _input?.Dispose();
            _worker = null;
            _input = null;
        }
    }
}
