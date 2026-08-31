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
    ///
    /// X and Size are fractions of the frame's WIDTH, Y of its HEIGHT, which is why FrameAspect
    /// travels with them: it is the only thing that puts a horizontal and a vertical displacement
    /// in the same unit, and FaceSteering measures both in face widths.
    public readonly struct FaceObservation
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Size;   // face box width, normalized to frame width
        public readonly float Score;

        /// Upright frame width / height — 0.75 for the 480x640 portrait frame a portrait-locked
        /// phone turns the 640x480 request into. Measured, not assumed, so a driver that hands
        /// back another shape scales correctly.
        public readonly float FrameAspect;

        /// The observation cleared FaceDetector.ScoreThreshold — a face the pipeline is sure of,
        /// as opposed to one it is only sure enough of to keep following.
        ///
        /// The detector decides this rather than each consumer comparing Score against a threshold
        /// of its own, so "confident" means one thing everywhere. Two consumers must demand it and
        /// both would be actively dangerous without it: CameraStaging (handing a run over on a
        /// marginal frame starts a run the detector cannot really see) and FaceTrackingRig's
        /// first-confident-sighting rule (a mirrored orientation twin scores in exactly the
        /// marginal band, which is the 30 Aug backwards-steering bug).
        public readonly bool IsConfident;

        public FaceObservation(float x, float y, float size, float score, float frameAspect,
            bool isConfident)
        {
            X = x;
            Y = y;
            Size = size;
            Score = score;
            FrameAspect = frameAspect;
            IsConfident = isConfident;
        }

        /// A decoded position exists — at whichever of the detector's two tiers. NOT the same
        /// question as IsConfident: this one is "is there something to follow", which is what makes
        /// a hop in a side lane survive the two or three frames of blur at its apex.
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

        /// At or above this the detector is sure: the observation comes back marked
        /// FaceObservation.IsConfident, and everything that can act on its own — a gesture trigger,
        /// the neutral calibration, the orientation decision, handing a run over — needs one.
        ///
        /// Defaulted from FaceSteering.DefaultMinScore rather than written out again, because the
        /// number is only meaningful if the detector's "confident" and the gesture rules' "act on
        /// it" are the same number.
        public float ScoreThreshold { get; set; } = FaceSteering.DefaultMinScore;

        /// Below this the detector is reporting noise rather than a face, and nothing is decoded at
        /// all: the caller gets `default`, i.e. no face.
        ///
        /// Between the two thresholds the box IS decoded and returned with its real, low score —
        /// which is the change that made a hop in a side lane fire. Returning `default` for every
        /// sub-0.65 frame destroyed the position as well as the confidence, so the two or three
        /// blurred frames at a hop's apex became "the player vanished" instead of "the player is
        /// here, roughly, and moving fast". FaceSteering is where that distinction is spent (see its
        /// two tiers); this class's only job is to stop throwing the position away.
        ///
        /// Measured on the Nord 2, 31 Aug: an empty ceiling scores 0.09-0.12, a real face at half a
        /// metre to a metre scores 0.71-0.93. 0.45 is in the empty gap between them.
        public float PositionThreshold { get; set; } = FaceSteering.DefaultMinPositionScore;

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
            if (LastScore < PositionThreshold) return default;

            float2 anchorPx = FaceAnchors.InputSize * _anchors[idx[0]];
            var centreTensor = new float2(anchorPx.x + box[0, 0, 0], anchorPx.y + box[0, 0, 1]);
            float2 centrePx = BlazeAffine.mul(_letterbox, centreTensor);

            float sizeNorm = box[0, 0, 2] * (_letterbox[0][0]) / _frameWidth;

            // Same flip as the pose landmarks: normalized, origin top-left, y down. The real score
            // travels with the observation whichever tier it landed in — a consumer that needs
            // certainty reads IsConfident, and one that needs continuity reads the position.
            return new FaceObservation(
                centrePx.x / _frameWidth,
                1f - centrePx.y / _frameHeight,
                sizeNorm,
                LastScore,
                _frameWidth / _frameHeight,
                LastScore >= ScoreThreshold);
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
