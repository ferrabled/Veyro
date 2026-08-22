using System;
using System.Collections.Generic;
using System.Globalization;
using MotionRunner.CameraInput;
using MotionRunner.Pose;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MotionRunner.Cv
{
    /// T-010's harness: front camera -> BlazePose -> 33 landmarks on screen, with the inference
    /// cost measured. It exists to answer one question — does BlazePose clear 30 ms median on the
    /// test phone — and the answer decides whether camera mode is in the pitch at all.
    ///
    /// This is a spike, and it ships in its own APK under its own package name. It is not wired
    /// into the game and cannot be: the only future route from pose to gameplay is an IGameInput
    /// adapter (T-013), per CLAUDE.md rule 2 and handoff 8.5.
    public sealed class CvSpikeController : MonoBehaviour
    {
        /// CLAUDE.md rule 7: inference every 2nd-3rd frame at most, so the GPU is not saturated by
        /// the model alone. Measured latency is unaffected by the gap; frame budget is not.
        public const int FramesBetweenCycles = 3;

        /// Discarded before the statistics start. The first inferences on a device pay for compute
        /// kernel compilation and buffer allocation and run several times slower than steady
        /// state; handoff 8.2 step 1 asks for the median over *warm* inferences.
        public const int WarmupCycles = 12;

        /// Landmarker to benchmark. Lite is the shipping choice on mobile (handoff 8.2), so it is
        /// the number that decides the gate.
        public BlazePoseRunner.LandmarkerVariant Variant = BlazePoseRunner.LandmarkerVariant.Lite;

        /// How long to wait before re-probing after an inconclusive sweep. An inconclusive sweep
        /// means nobody was in frame, so the only useful response is to try again later.
        public const float ReprobeIntervalSeconds = 12f;

        CameraFeed _feed;
        BlazePoseRunner _runner;
        PoseOverlay _overlay;
        OrientationProbe _probe;
        ModelAsset _detectorAsset;
        ModelAsset _liteAsset;
        ModelAsset _fullAsset;

        bool _running;
        int _cycles;
        int _warmDone;
        float _nextLogTime;
        float _nextStatsTime;
        float _nextProbeTime;
        bool _orientationConfident;
        bool _verdictLogged;
        string _resolvedOrientation = "?";
        CvBenchmark.Result[] _bench;
        string _benchSummary = "";

        void Start()
        {
            TearDownGameScene();

            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            _overlay = PoseOverlay.Create();
            _overlay.transform.SetParent(transform, false);
            _overlay.SetBanner("Veyro Run — T-010 BlazePose spike\nstarting camera…");
            _overlay.SetStats("waiting for camera");

            LogEnvironment();
            _running = true;
            RunAsync();
        }

        /// GameBootstrap boots from any scene via RuntimeInitializeOnLoadMethod, so in this APK the
        /// runner, track and HUD are already standing by the time Start runs. Measuring inference
        /// with the game rendering behind the overlay would fold its GPU cost into every sample,
        /// so everything that is not the spike goes. Start (not a RuntimeInitialize hook) because
        /// it is the first point at which those objects are guaranteed to exist.
        void TearDownGameScene()
        {
            Scene scene = gameObject.scene;
            if (!scene.IsValid()) return;

            var roots = new List<GameObject>(scene.rootCount);
            scene.GetRootGameObjects(roots);

            int destroyed = 0;
            foreach (GameObject root in roots)
            {
                if (root == gameObject) continue;
                Destroy(root);
                destroyed++;
            }

            if (destroyed > 0) Debug.Log($"[CV] spike removed {destroyed} game objects from the scene.");
        }

        void LogEnvironment()
        {
            Debug.Log($"[CV] device={SystemInfo.deviceModel} gpu={SystemInfo.graphicsDeviceName} " +
                      $"api={SystemInfo.graphicsDeviceType} compute={SystemInfo.supportsComputeShaders} " +
                      $"colorSpace={QualitySettings.activeColorSpace} unity={Application.unityVersion}");
        }

        async void RunAsync()
        {
            try
            {
                if (!await AcquireCameraAsync()) return;
                if (!LoadModels()) return;

                // Before anything that depends on a body being present: what does one inference
                // cost? This is the number T-010 exists to produce, and T-013 will run the same
                // measurement at startup to decide whether to offer camera mode at all.
                _overlay.SetBanner("benchmarking BlazePose…");
                _bench = await CvBenchmark.RunSweepAsync(_detectorAsset, _liteAsset, _fullAsset);
                _benchSummary = CvBenchmark.Summarize(_bench);
                _overlay.SetStats(_benchSummary);

                // T-010b (OPEN_QUESTIONS 7): the extended matrix — cheaper model, quantization,
                // GPUPixel, live-under-60FPS runs, no-ML floor. Logs "[CV] FEAS" lines.
                _overlay.SetBanner("running feasibility sweep (T-010b)…");
                await CvFeasibilitySweep.RunAsync(_detectorAsset, _liteAsset,
                    Resources.Load<ModelAsset>("Cv/blaze_face_short_range"));

                StartProbe();

                while (_running)
                {
                    for (int i = 0; i < FramesBetweenCycles; i++) await Awaitable.NextFrameAsync();
                    if (!_running) break;

                    if (!_feed.TryUpdate()) continue;
                    _overlay.SetFeed(_feed.UprightTexture);

                    if (_probe != null && !_probe.IsComplete)
                    {
                        await ProbeStepAsync();
                        continue;
                    }

                    if (!_orientationConfident && Time.unscaledTime >= _nextProbeTime)
                    {
                        StartProbe();
                        continue;
                    }

                    bool warm = _warmDone >= WarmupCycles;
                    await _runner.RunCycleAsync(_feed.UprightTexture, warm);
                    _cycles++;
                    if (!warm)
                    {
                        _warmDone++;
                        _overlay.SetBanner($"warming up… {_warmDone}/{WarmupCycles}");
                        if (_warmDone == WarmupCycles)
                            _overlay.SetBanner("Veyro Run — T-010 BlazePose spike");
                    }

                    _overlay.Draw(_runner.Frame);
                    UpdateStats();
                }
            }
            catch (OperationCanceledException)
            {
                // Normal on quit: the awaitable is cancelled when the object goes away.
            }
            catch (Exception e)
            {
                Debug.LogError("[CV] spike failed: " + e);
                _overlay?.SetBanner("CV spike failed — see logcat");
                _overlay?.SetStats(e.Message);
            }
        }

        void StartProbe()
        {
            _probe = new OrientationProbe(_feed.ReportedRotation, _feed.ReportedVerticallyMirrored, 2);
            _feed.SetOrientation(_probe.Current.RotationDegrees, _probe.Current.VerticallyFlipped);
            _feed.Restraighten();
            _nextProbeTime = Time.unscaledTime + ReprobeIntervalSeconds;
            _overlay.SetBanner("finding which way up the camera is — stand in front of the phone");
        }

        /// One probe step: score the current candidate orientation, then straighten the same frame
        /// the other way round for the next one. Same frame, so the comparison measures the
        /// orientation and not the subject moving between shots.
        async Awaitable ProbeStepAsync()
        {
            await _runner.RunCycleAsync(_feed.UprightTexture, false);
            _probe.Submit(_runner.LastScore);
            _overlay.Draw(_runner.Frame);

            if (!_probe.IsComplete)
            {
                _feed.SetOrientation(_probe.Current.RotationDegrees, _probe.Current.VerticallyFlipped);
                _feed.Restraighten();
                _overlay.SetStats($"orientation probe {_probe.CandidatesTried}/{_probe.CandidateCount}: " +
                                  $"{_probe.Current}  best {_probe.BestScore:F2}");
                return;
            }

            _orientationConfident = _probe.IsConfident;

            // An unconfident sweep means nobody was in frame and the winner beat the others on
            // detector noise. Trusting it would lock in a rotation at random, which is exactly
            // what happened on the first device run; fall back to what the device says and retry.
            OrientationProbe.Candidate chosen = _orientationConfident
                ? _probe.Best
                : new OrientationProbe.Candidate(_feed.ReportedRotation, _feed.ReportedVerticallyMirrored);

            _feed.SetOrientation(chosen.RotationDegrees, chosen.VerticallyFlipped);
            _feed.Restraighten();

            bool matchedReport = chosen.RotationDegrees == _feed.ReportedRotation &&
                                 chosen.VerticallyFlipped == _feed.ReportedVerticallyMirrored;
            _resolvedOrientation = chosen + (!_orientationConfident
                ? " (unconfirmed — device report, nobody in frame)"
                : matchedReport ? " (confirmed, as reported)" : " (confirmed, OVERRULED report)");

            Debug.Log($"[CV] orientation {(_orientationConfident ? "settled" : "UNRESOLVED")} after " +
                      $"{_probe.CandidatesTried} candidate(s): {_resolvedOrientation} " +
                      $"bestScore={_probe.BestScore:F3} | {_feed.Describe()}");

            _overlay.SetBanner(_orientationConfident
                ? "Veyro Run — T-010 BlazePose spike"
                : "no body seen yet — stand in front of the phone");
        }

        async Awaitable<bool> AcquireCameraAsync()
        {
            _feed = new CameraFeed();

            if (!CameraFeed.HasPermission())
            {
                _overlay.SetBanner("waiting for camera permission…");
                CameraFeed.RequestPermission();
                // The permission dialog suspends the app; poll rather than assume a callback.
                for (int i = 0; i < 600 && !CameraFeed.HasPermission(); i++)
                    await Awaitable.NextFrameAsync();
            }

            if (!CameraFeed.HasPermission())
            {
                Debug.LogError("[CV] camera permission denied.");
                _overlay.SetBanner("camera permission denied");
                return false;
            }

            for (int i = 0; i < 600 && !_feed.TryStart(); i++) await Awaitable.NextFrameAsync();
            if (!_feed.TryStart())
            {
                Debug.LogError("[CV] no camera devices reported.");
                _overlay.SetBanner("no camera found");
                return false;
            }

            for (int i = 0; i < 600 && !_feed.HasFrame; i++) await Awaitable.NextFrameAsync();
            if (!_feed.HasFrame)
            {
                Debug.LogError("[CV] camera never delivered a frame.");
                _overlay.SetBanner("camera delivered no frames");
                return false;
            }

            _feed.UseDeviceReportedOrientation();
            _feed.Restraighten();
            _overlay.SetFeed(_feed.UprightTexture);
            Debug.Log("[CV] camera " + _feed.Describe());
            return true;
        }

        bool LoadModels()
        {
            var detector = Resources.Load<ModelAsset>("Cv/pose_detection");
            string landmarkerName = Variant == BlazePoseRunner.LandmarkerVariant.Full
                ? "Cv/pose_landmarks_detector_full"
                : "Cv/pose_landmarks_detector_lite";
            var landmarker = Resources.Load<ModelAsset>(landmarkerName);
            var anchors = Resources.Load<TextAsset>("Cv/anchors");

            if (detector == null || landmarker == null || anchors == null)
            {
                // The models are staged into Resources by the CV build only, so the shipping game
                // never carries 20 MB of weights it does not use.
                string missing = (detector == null ? "pose_detection " : "") +
                                 (landmarker == null ? landmarkerName + " " : "") +
                                 (anchors == null ? "anchors" : "");
                Debug.LogError("[CV] missing model assets in Resources/Cv: " + missing);
                _overlay.SetBanner("models missing from build");
                _overlay.SetStats("run CvSpikeBuild.StageModels before building");
                return false;
            }

            _detectorAsset = detector;
            _liteAsset = Resources.Load<ModelAsset>("Cv/pose_landmarks_detector_lite");
            _fullAsset = Resources.Load<ModelAsset>("Cv/pose_landmarks_detector_full");

            _runner = new BlazePoseRunner();
            if (!_runner.Load(detector, landmarker, anchors, Variant))
            {
                _overlay.SetBanner("BlazePose failed to load");
                return false;
            }

            return true;
        }

        void UpdateStats()
        {
            double landmarker = _runner.LandmarkerMs.Median();
            int samples = _runner.LandmarkerMs.Count;

            if (Time.unscaledTime >= _nextStatsTime)
            {
                _nextStatsTime = Time.unscaledTime + 0.25f;
                string verdict = samples == 0
                    ? "measuring…"
                    : landmarker < LatencyStats.GateMs ? "PASS < 30 ms" : "FAIL >= 30 ms";

                _overlay.SetStats(string.Format(CultureInfo.InvariantCulture,
                    "landmarker {0}  median {1:F1} ms   p95 {2:F1} ms   n={3}\n" +
                    "detector   median {4:F1} ms   full cycle median {5:F1} ms\n" +
                    "{6}   score {7:F2}   {8} fps\n" +
                    "camera {9}\ngate (T-013, <30 ms median): {10}",
                    _runner.Variant, landmarker, _runner.LandmarkerMs.Percentile(95d), samples,
                    _runner.DetectorMs.Median(), _runner.CycleMs.Median(),
                    _runner.Frame.HasPose ? "TRACKING" : "no pose", _runner.LastScore,
                    Mathf.RoundToInt(1f / Mathf.Max(Time.unscaledDeltaTime, 1e-4f)),
                    _resolvedOrientation, verdict, _benchSummary));
            }

            if (Time.unscaledTime < _nextLogTime) return;
            _nextLogTime = Time.unscaledTime + 3f;

            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[CV] STATS cycles={0} n={1} landmarker med={2:F2} p95={3:F2} min={4:F2} max={5:F2} " +
                "| detector med={6:F2} | cycle med={7:F2} | pose={8} score={9:F2} fps={10}",
                _cycles, samples, landmarker, _runner.LandmarkerMs.Percentile(95d),
                _runner.LandmarkerMs.Min(), _runner.LandmarkerMs.Max(),
                _runner.DetectorMs.Median(), _runner.CycleMs.Median(),
                _runner.Frame.HasPose, _runner.LastScore,
                Mathf.RoundToInt(1f / Mathf.Max(Time.unscaledDeltaTime, 1e-4f))));

            if (_verdictLogged || !_runner.LandmarkerMs.TryGate(out bool passes)) return;
            _verdictLogged = true;
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[CV] VERDICT landmarker={0} median={1:F2}ms over {2} warm inferences on {3}/{4} " +
                "-> camera mode gate {5}",
                _runner.Variant, landmarker, samples, SystemInfo.deviceModel,
                SystemInfo.graphicsDeviceName, passes ? "PASSES" : "FAILS"));
        }

        void OnDestroy()
        {
            _running = false;
            _runner?.Dispose();
            _feed?.Dispose();
        }
    }
}
