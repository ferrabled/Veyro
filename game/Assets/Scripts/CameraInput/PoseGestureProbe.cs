using System;
using MotionRunner.Pose;
using Unity.InferenceEngine;
using Unity.Mathematics;
using UnityEngine;

namespace MotionRunner.CameraInput
{
    /// The raised-hand confirm for the pause-resume flow: BlazePose detector + LITE landmarker,
    /// alive ONLY while the game is frozen at Time.timeScale = 0.
    ///
    /// That lifecycle rule is the whole design, and it comes straight from T-010's numbers: a
    /// full pose cycle costs ~334 ms on the Nord 2 (detector 217 + landmarker 118, CPU backend)
    /// against a 30 ms frame budget, which is why BlazePose was abandoned for steering and the
    /// game ships BlazeFace at 4.2 ms. Frozen, those 334 ms cost nothing anyone can feel — the
    /// probe simply answers at its natural ~3 Hz — so the heavy model gets to exist exactly
    /// where it is affordable and nowhere else. FaceTrackingRig owns the probe (rule 2: gameplay
    /// never touches sensors or models) and disposes it the moment the resume countdown starts
    /// or the pause menu closes; TickLifecycle logs and stops the loop if it ever finds itself
    /// running unfrozen, so a leak is a loud logcat line rather than a silent frame-rate cliff.
    ///
    /// It reads the rig's existing upright texture — never a second WebCamTexture client. The
    /// texture comes through a provider delegate re-read every cycle, because the rig suspends
    /// and rebuilds its feed across pauses: a cycle that starts after the feed went away sees
    /// null and idles instead of sampling a destroyed RenderTexture.
    ///
    /// The gesture arithmetic itself is not here: RaisedHand (engine-free, unit-tested) decides
    /// what "the player's right hand is up" means, including the selfie-mirror trap; this class
    /// only turns tensors into a PoseFrame and paces the samples.
    public sealed class PoseGestureProbe : IDisposable
    {
        public const string DetectorResourcePath = "CameraInput/pose_detection";
        public const string LandmarkerResourcePath = "CameraInput/pose_landmarks_detector_lite";
        public const string AnchorsResourcePath = "CameraInput/pose_anchors";

        /// The spike's pipeline constants (BlazePoseRunner / the Unity BlazeDetectionSample they
        /// were ported from). Wire format, not tuning: they belong to the models.
        const int AnchorCount = 2254;
        const int DetectorInputSize = 224;
        const int LandmarkerInputSize = 256;
        const int LandmarkStride = 5; // x, y, z, visibility, presence
        const float CropScale = 1.25f;

        /// Below this the pose detector is reporting noise rather than a body — the spike's
        /// threshold. A frame with no body submits "not raised", which resets the confirm streak:
        /// the conservative direction for a gesture that unfreezes the game.
        const float ScoreThreshold = 0.6f;

        /// The floor under the sample cadence. On the target device a cycle takes ~334 ms and
        /// this never engages; in the Editor a cycle can finish in a frame or two, and without a
        /// floor "three consecutive samples" would confirm off a tenth of a second of arm-swing.
        /// 0.25 s keeps RaisedHandConfirm.RequiredSamples meaning roughly the same deliberate
        /// ~1 s hold everywhere.
        const float MinSampleIntervalSeconds = 0.25f;

        /// The player's right hand, held for RaisedHandConfirm.RequiredSamples consecutive
        /// samples. Latches until the probe is disposed.
        public bool Confirmed => _confirm.Confirmed;

        /// The most recent sample's verdict — what the affordance UI reads to say "keep holding".
        public bool HandRaisedNow { get; private set; }

        public int Streak => _confirm.Streak;
        public bool IsLoaded => _detectorWorker != null && _landmarkerWorker != null;

        /// True while the sampling loop is alive. Cleared by Dispose and by the lifecycle guard.
        public bool IsRunning { get; private set; }

        readonly RaisedHandConfirm _confirm = new RaisedHandConfirm();
        readonly PoseFrame _frame = new PoseFrame();

        float[,] _anchors;
        Worker _detectorWorker;
        Worker _landmarkerWorker;
        Tensor<float> _detectorInput;
        Tensor<float> _landmarkerInput;
        int _generation;
        bool _disposed;

        /// Loads both models from Resources on the CPU backend (2.5-25x faster than either GPU
        /// backend on the Mali target — T-010b, same reasoning as FaceDetector). A few hundred
        /// milliseconds of load are invisible behind a frozen frame.
        ///
        /// FALSE IS THE ONLY FAILURE THIS METHOD HAS, and that is a contract, not tidiness. It is
        /// called synchronously out of PauseMenu.Update (via FaceTrackingRig.BeginGestureProbe)
        /// at the one moment the pause card's buttons are disabled, waiting to be re-enabled by
        /// the branch this return value picks. An exception escaping here would skip that branch
        /// entirely and leave the player frozen behind a dead card with no way back in — the
        /// stranding rule 3 exists to forbid (PR #6 review). So everything fallible is inside the
        /// catch: a malformed anchors CSV (LoadAnchors parses and indexes without checking),
        /// ModelLoader on a truncated or re-exported .onnx, the functional graph compile, Worker
        /// construction on a backend that will not come up, and the self-check's own
        /// schedule/readback. Every one of them ends the same way — the reason in the log, the
        /// partial state disposed, false to the caller, touch resume unaffected (rule 3: gestures
        /// augment, never gate).
        public bool Load()
        {
            var detectorAsset = Resources.Load<ModelAsset>(DetectorResourcePath);
            var landmarkerAsset = Resources.Load<ModelAsset>(LandmarkerResourcePath);
            var anchorsCsv = Resources.Load<TextAsset>(AnchorsResourcePath);
            if (detectorAsset == null || landmarkerAsset == null || anchorsCsv == null)
            {
                Debug.LogWarning("[CAM] pose probe assets missing from Resources — " +
                                 "raise-hand confirm disabled, touch resume unaffected.");
                return false;
            }

            try
            {
                _anchors = BlazeAffine.LoadAnchors(anchorsCsv.text, AnchorCount);

                // Argmax over the 2254 anchors folded into the detector graph, exactly like the
                // face detector and the spike: one box crosses the tensor boundary, not the
                // anchor grid.
                Model detectorModel = ModelLoader.Load(detectorAsset);
                var graph = new FunctionalGraph();
                FunctionalTensor input = graph.AddInput(detectorModel, 0);
                FunctionalTensor[] outputs = Functional.Forward(detectorModel, input);
                (FunctionalTensor idx, FunctionalTensor score, FunctionalTensor box) =
                    BlazeAffine.ArgMaxFiltering(outputs[0], outputs[1]);
                detectorModel = graph.Compile(idx, score, box);

                _detectorWorker = new Worker(detectorModel, BackendType.CPU);
                _landmarkerWorker = new Worker(ModelLoader.Load(landmarkerAsset), BackendType.CPU);
                _detectorInput = new Tensor<float>(
                    new TensorShape(1, DetectorInputSize, DetectorInputSize, 3));
                _landmarkerInput = new Tensor<float>(
                    new TensorShape(1, LandmarkerInputSize, LandmarkerInputSize, 3));

                if (!SelfCheck())
                {
                    Debug.LogError("[CAM] pose detector self-check failed — raise-hand confirm disabled.");
                    Dispose();
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                // Dispose is safe on however far the load got: every field it touches is
                // null-conditional and it is idempotent, so a throw between the two Workers
                // releases the one that exists and nothing else.
                Debug.LogError("[CAM] pose probe failed to load: " + e +
                               " — raise-hand confirm disabled, touch resume unaffected.");
                Dispose();
                return false;
            }
        }

        /// The pose detector's box row must be 12 wide (4 box values + 4 keypoints x 2). Same
        /// load-time tripwire as FaceDetector.SelfCheck: a re-exported model with swapped outputs
        /// fails here, loudly, instead of as a gesture that never fires.
        bool SelfCheck()
        {
            _detectorWorker.Schedule(_detectorInput);
            using Tensor box = _detectorWorker.PeekOutput(2).ReadbackAndClone();
            int width = box.shape[box.shape.rank - 1];
            if (width == 12) return true;
            Debug.LogError($"[CAM] unexpected pose box tensor width {width} (wanted 12) — output order swapped?");
            return false;
        }

        /// Starts the sampling loop. uprightSource is re-read every cycle (the feed can go away
        /// under a suspend); selfieMirrored decides which anatomical wrist is the player's right
        /// hand — see RaisedHand.WristFor for the trap this dodges.
        public void Run(Func<Texture> uprightSource, bool selfieMirrored)
        {
            if (!IsLoaded || IsRunning) return;
            IsRunning = true;
            RunLoopAsync(uprightSource, selfieMirrored);
        }

        async void RunLoopAsync(Func<Texture> uprightSource, bool selfieMirrored)
        {
            int generation = ++_generation;
            bool Stale() => _disposed || generation != _generation;
            float lastSampleAt = float.MinValue;

            try
            {
                while (!Stale())
                {
                    await Awaitable.NextFrameAsync();
                    if (Stale()) return;
                    if (!TickLifecycle()) return;
                    if (Time.unscaledTime - lastSampleAt < MinSampleIntervalSeconds) continue;

                    Texture upright = uprightSource();
                    if (upright == null) continue; // feed suspended mid-wait: idle, don't die

                    bool raised = await SampleAsync(upright, selfieMirrored);
                    if (Stale()) return;

                    lastSampleAt = Time.unscaledTime;
                    HandRaisedNow = raised;
                    _confirm.Submit(raised);
                }
            }
            catch (Exception e)
            {
                // Disposal rips the workers out from under an inference in flight; that throw is
                // the teardown, not a probe that broke. Anything else is worth a log line, but
                // never a dialog: the touch path is the guaranteed one.
                if (Stale()) return;
                Debug.LogError("[CAM] pose probe failed: " + e);
                IsRunning = false;
            }
        }

        /// The lifecycle assertion the design rule asks for: this model must never be alive while
        /// the world moves. Finding timeScale back at 1 means an owner forgot to dispose the
        /// probe — stop sampling immediately and say so, loudly, so the leak is a logcat line
        /// during development rather than a mystery frame-time regression on device.
        bool TickLifecycle()
        {
            if (Time.timeScale == 0f) return true;
            Debug.LogError("[CAM] pose probe alive at timeScale " + Time.timeScale +
                           " — the heavy model may only run while the game is frozen. Stopping it.");
            IsRunning = false;
            return false;
        }

        /// One detector + landmarker cycle over the upright frame, ported from the spike's
        /// BlazePoseRunner (itself from Unity's BlazeDetectionSample/Pose, Apache-2.0), minus the
        /// latency bookkeeping. Returns the RaisedHand verdict for this sample.
        async Awaitable<bool> SampleAsync(Texture upright, bool selfieMirrored)
        {
            float width = upright.width;
            float height = upright.height;
            float size = Mathf.Max(width, height);

            // Letterbox the frame into the detector's square.
            float scale = size / DetectorInputSize;
            float2x3 M = BlazeAffine.mul(
                BlazeAffine.TranslationMatrix(0.5f * (new float2(width, height) + new float2(-size, size))),
                BlazeAffine.ScaleMatrix(new float2(scale, -scale)));
            BlazeAffine.SampleImageAffine(upright, _detectorInput, M);
            _detectorWorker.Schedule(_detectorInput);

            Awaitable<Tensor<int>> idxA =
                (_detectorWorker.PeekOutput(0) as Tensor<int>).ReadbackAndCloneAsync();
            Awaitable<Tensor<float>> scoreA =
                (_detectorWorker.PeekOutput(1) as Tensor<float>).ReadbackAndCloneAsync();
            Awaitable<Tensor<float>> boxA =
                (_detectorWorker.PeekOutput(2) as Tensor<float>).ReadbackAndCloneAsync();

            using Tensor<int> outputIdx = await idxA;
            using Tensor<float> outputScore = await scoreA;
            using Tensor<float> outputBox = await boxA;
            if (_disposed) return false;

            if (outputScore[0] < ScoreThreshold)
            {
                _frame.MarkLost();
                return false;
            }

            // The two alignment keypoints (hip midpoint + a point up the torso axis) define the
            // rotated square the landmarker sees.
            int idx = outputIdx[0];
            float2 anchor = DetectorInputSize * new float2(_anchors[idx, 0], _anchors[idx, 1]);
            float2 kp1 = BlazeAffine.mul(M, anchor + new float2(outputBox[0, 0, 4], outputBox[0, 0, 5]));
            float2 kp2 = BlazeAffine.mul(M, anchor + new float2(outputBox[0, 0, 6], outputBox[0, 0, 7]));
            float2 delta = kp2 - kp1;

            float radius = CropScale * math.length(delta);
            if (radius <= Mathf.Epsilon)
            {
                _frame.MarkLost();
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
            _landmarkerWorker.Schedule(_landmarkerInput);
            Awaitable<Tensor<float>> landmarksA =
                (_landmarkerWorker.PeekOutput("Identity") as Tensor<float>).ReadbackAndCloneAsync();
            using Tensor<float> landmarks = await landmarksA; // (1, 195) = 33 x 5
            if (_disposed) return false;

            for (int i = 0; i < PoseFrame.JointCount; i++)
            {
                float2 pixel = BlazeAffine.mul(M2, new float2(
                    landmarks[LandmarkStride * i + 0],
                    landmarks[LandmarkStride * i + 1]));

                // Normalized, origin top-left, y down — MotionRunner.Pose's convention, so the
                // engine-free rule reads the same space FaceSteering does.
                _frame.Set(i, new PoseLandmark(
                    pixel.x / width,
                    1f - pixel.y / height,
                    landmarks[LandmarkStride * i + 2] / height,
                    landmarks[LandmarkStride * i + 3],
                    landmarks[LandmarkStride * i + 4]));
            }

            _frame.MarkTracked(outputScore[0]);
            return RaisedHand.IsRaised(_frame, selfieMirrored);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _generation++;
            IsRunning = false;
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
