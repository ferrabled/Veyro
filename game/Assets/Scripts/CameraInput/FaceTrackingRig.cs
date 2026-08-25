using System;
using MotionRunner.Pose;
using UnityEngine;

namespace MotionRunner.CameraInput
{
    /// Owns the whole camera-input stack: permission, the front camera (CameraFeed), the startup
    /// speed gate, the empirical orientation probe, and the continuous BlazeFace loop. Publishes
    /// the latest FaceObservation for CameraFaceInput to poll; nothing here touches gameplay
    /// (CLAUDE.md rule 2 — the only route into the game is the IGameInput adapter).
    ///
    /// Startup order is deliberate:
    ///   gate -> permission -> camera -> probe -> track
    /// The gate needs no camera and fails in under a second on a device that is too slow, so the
    /// player is never asked for camera permission by a mode that then cannot run (handoff 8.2's
    /// low-end policy).
    public sealed class FaceTrackingRig : MonoBehaviour
    {
        public enum RigState
        {
            Idle,
            Gating,
            RequestingPermission,
            StartingCamera,
            Probing,
            Tracking,
            Failed
        }

        /// Camera frames arrive at 30 fps; inferring faster than that reads the same pixels
        /// twice. The sweep showed the detector keeps up at ~50 Hz, so the camera is the limiter.
        const float ReprobeIntervalSeconds = 12f;

        public RigState State { get; private set; } = RigState.Idle;
        public string FailReason { get; private set; } = string.Empty;
        public double GateMedianMs { get; private set; }
        public FaceObservation Latest { get; private set; }

        /// Seconds since Latest was updated — steering treats a stale observation as loss.
        public float LatestAgeSeconds =>
            _latestAt < 0f ? float.MaxValue : Time.unscaledTime - _latestAt;

        CameraFeed _feed;
        FaceDetector _detector;
        OrientationProbe _probe;
        float _latestAt = -1f;
        float _nextProbeTime;
        bool _orientationConfident;
        bool _running;

        public static FaceTrackingRig Create()
        {
            var go = new GameObject("FaceTrackingRig");
            DontDestroyOnLoad(go);
            return go.AddComponent<FaceTrackingRig>();
        }

        /// Kicks off the async pipeline; watch State to follow it.
        public void Begin()
        {
            if (State != RigState.Idle && State != RigState.Failed) return;
            _running = true;
            RunAsync();
        }

        async void RunAsync()
        {
            try
            {
                // ---- 1. Speed gate, before anything asks for permissions ----
                State = RigState.Gating;
                _detector = new FaceDetector();
                if (!_detector.Load())
                {
                    Fail("face model failed to load");
                    return;
                }

                bool passes = _detector.TryGate(out double medianMs);
                GateMedianMs = medianMs;
                if (!passes)
                {
                    Fail($"this device is too slow for camera mode ({medianMs:F0} ms per look, needs <{LatencyStats.GateMs:F0})");
                    return;
                }

                // ---- 2. Permission + camera ----
                State = RigState.RequestingPermission;
                _feed = new CameraFeed();
                if (!CameraFeed.HasPermission())
                {
                    CameraFeed.RequestPermission();
                    // The permission dialog suspends the app; poll rather than assume a callback.
                    for (int i = 0; i < 900 && !CameraFeed.HasPermission(); i++)
                        await Awaitable.NextFrameAsync();
                }

                if (!CameraFeed.HasPermission())
                {
                    Fail("camera permission denied");
                    return;
                }

                State = RigState.StartingCamera;
                for (int i = 0; i < 600 && !_feed.TryStart(); i++) await Awaitable.NextFrameAsync();
                for (int i = 0; i < 600 && !_feed.HasFrame; i++) await Awaitable.NextFrameAsync();
                if (!_feed.HasFrame)
                {
                    Fail("camera delivered no frames");
                    return;
                }

                _feed.UseDeviceReportedOrientation();
                _feed.Restraighten();
                Debug.Log("[CAM] camera " + _feed.Describe());

                // ---- 3. Orientation, then continuous tracking ----
                StartProbe();

                while (_running)
                {
                    await Awaitable.NextFrameAsync();
                    if (!_running) break;
                    if (!_feed.TryUpdate()) continue;

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

                    _detector.Schedule(_feed.UprightTexture);
                    FaceObservation obs = await _detector.ReadAsync();
                    Latest = obs;
                    _latestAt = Time.unscaledTime;

                    // The first confident sighting settles an unconfident orientation: if the
                    // device-reported guess can see a face, it was right all along.
                    if (!_orientationConfident && obs.HasFace) _orientationConfident = true;
                }
            }
            catch (OperationCanceledException)
            {
                // Normal on teardown.
            }
            catch (Exception e)
            {
                Debug.LogError("[CAM] rig failed: " + e);
                Fail("camera mode hit an error — see logcat");
            }
        }

        void StartProbe()
        {
            _probe = new OrientationProbe(_feed.ReportedRotation, _feed.ReportedVerticallyMirrored, 2);
            _feed.SetOrientation(_probe.Current.RotationDegrees, _probe.Current.VerticallyFlipped);
            _feed.Restraighten();
            _nextProbeTime = Time.unscaledTime + ReprobeIntervalSeconds;
            State = RigState.Probing;
        }

        /// Same-frame candidate comparison, exactly like the T-010 spike: re-straighten the one
        /// frame each way so the score measures orientation, not the player moving between shots.
        async Awaitable ProbeStepAsync()
        {
            _detector.Schedule(_feed.UprightTexture);
            await _detector.ReadAsync();
            _probe.Submit(_detector.LastScore);

            if (!_probe.IsComplete)
            {
                _feed.SetOrientation(_probe.Current.RotationDegrees, _probe.Current.VerticallyFlipped);
                _feed.Restraighten();
                return;
            }

            _orientationConfident = _probe.IsConfident;
            OrientationProbe.Candidate chosen = _orientationConfident
                ? _probe.Best
                : new OrientationProbe.Candidate(_feed.ReportedRotation, _feed.ReportedVerticallyMirrored);

            _feed.SetOrientation(chosen.RotationDegrees, chosen.VerticallyFlipped);
            _feed.Restraighten();

            Debug.Log($"[CAM] orientation {(_orientationConfident ? "settled" : "UNRESOLVED")}: " +
                      $"{chosen} bestScore={_probe.BestScore:F2} | {_feed.Describe()}");
            State = RigState.Tracking;
        }

        void Fail(string reason)
        {
            FailReason = reason;
            State = RigState.Failed;
            Debug.LogWarning("[CAM] " + reason);
            TearDown();
        }

        void TearDown()
        {
            _running = false;
            _detector?.Dispose();
            _feed?.Dispose();
            _detector = null;
            _feed = null;
        }

        void OnDestroy() => TearDown();
    }
}
