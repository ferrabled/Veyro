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

        /// The detector's last score *before* any threshold, which Latest.Score still cannot show
        /// at the bottom of the range: an inference under FaceDetector.PositionThreshold (0.45)
        /// reports 0 there, so noise and a total miss look identical.
        ///
        /// Only the telemetry line reads it. Since the two-tier change it is a narrower diagnostic
        /// than it was — Latest.Score now carries the true score all the way down to 0.45, so a
        /// dropout mid-gesture is visible in `score` itself — but it is still the only way to see
        /// how far under the floor a lost frame fell, which is what decides whether 0.45 is the
        /// right floor.
        public float DetectorScore => _detector != null ? _detector.LastScore : 0f;

        CameraFeed _feed;
        FaceDetector _detector;
        OrientationProbe _probe;
        float _latestAt = -1f;
        float _nextProbeTime;
        bool _orientationConfident;
        bool _running;

        /// The orientation this rig settled on, kept across Suspend/Begin so a resume never
        /// probes again. See RestoreOrientation for why re-probing is dangerous and why keeping
        /// the answer is safe. Cleared by TearDown, so a new rig starts from nothing.
        OrientationProbe.Candidate _settledOrientation;
        bool _hasSettledOrientation;

        /// Bumped by Suspend/TearDown. A loop awaiting an inference cannot be stopped mid-await,
        /// so it checks its own generation after every await and returns if a newer one started.
        int _generation;

        public static FaceTrackingRig Create()
        {
            var go = new GameObject("FaceTrackingRig");
            DontDestroyOnLoad(go);
            return go.AddComponent<FaceTrackingRig>();
        }

        /// Kicks off the async pipeline; watch State to follow it. Called again after Suspend it
        /// re-runs the same staging - permission, camera, face - minus the model load, the speed
        /// gate (the device already passed this session) and the orientation probe (already
        /// settled, and re-running it is what inverted steering on resume - see
        /// RestoreOrientation).
        public void Begin()
        {
            if (State != RigState.Idle && State != RigState.Failed) return;
            _running = true;
            RunAsync();
        }

        /// Releases the camera without tearing the rig down: the pause menu holds no camera, and
        /// Begin() picks the flow back up where the player can see the staging again. The model
        /// stays loaded, which is what makes a resume cost a second rather than five, and the
        /// settled orientation stays with it, which is what stops the resume steering backwards.
        public void Suspend()
        {
            if (State == RigState.Idle) return;
            _running = false;
            _generation++;

            _feed?.Dispose();
            _feed = null;
            _probe = null;
            _orientationConfident = false;
            Latest = default;
            _latestAt = -1f;
            FailReason = string.Empty;
            State = RigState.Idle;
        }

        async void RunAsync()
        {
            int generation = ++_generation;
            bool Stale() => !_running || generation != _generation;

            try
            {
                // ---- 1. Speed gate, before anything asks for permissions ----
                // Skipped on a resume: the detector is still loaded and the device's verdict
                // does not change between two halves of the same run.
                if (_detector == null)
                {
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
                }

                // ---- 2. Permission + camera ----
                State = RigState.RequestingPermission;
                _feed = new CameraFeed();
                if (!CameraFeed.HasPermission())
                {
                    CameraFeed.RequestPermission();
                    // The permission dialog suspends the app; poll rather than assume a callback.
                    for (int i = 0; i < 900 && !CameraFeed.HasPermission(); i++)
                    {
                        await Awaitable.NextFrameAsync();
                        if (Stale()) return;
                    }
                }

                if (!CameraFeed.HasPermission())
                {
                    Fail("camera permission denied");
                    return;
                }

                State = RigState.StartingCamera;
                for (int i = 0; i < 600 && !_feed.TryStart(); i++)
                {
                    await Awaitable.NextFrameAsync();
                    if (Stale()) return;
                }
                for (int i = 0; i < 600 && !_feed.HasFrame; i++)
                {
                    await Awaitable.NextFrameAsync();
                    if (Stale()) return;
                }
                if (!_feed.HasFrame)
                {
                    Fail("camera delivered no frames");
                    return;
                }

                _feed.UseDeviceReportedOrientation();
                _feed.Restraighten();
                Debug.Log("[CAM] camera " + _feed.Describe());

                // ---- 3. Orientation, then continuous tracking ----
                // Probed once per rig lifetime; a resume reuses the answer.
                if (_hasSettledOrientation) RestoreOrientation();
                else StartProbe();

                while (!Stale())
                {
                    await Awaitable.NextFrameAsync();
                    if (Stale()) break;
                    if (!_feed.TryUpdate()) continue;

                    if (_probe != null && !_probe.IsComplete)
                    {
                        await ProbeStepAsync();
                        if (Stale()) break;
                        continue;
                    }

                    if (!_orientationConfident && Time.unscaledTime >= _nextProbeTime)
                    {
                        StartProbe();
                        continue;
                    }

                    _detector.Schedule(_feed.UprightTexture);
                    FaceObservation obs = await _detector.ReadAsync();
                    if (Stale()) break;
                    Latest = obs;
                    _latestAt = Time.unscaledTime;

                    // The first confident sighting settles an unconfident orientation: if the
                    // device-reported guess can see a face, it was right all along.
                    //
                    // IsConfident, deliberately, not HasFace — which now includes the marginal tier
                    // the detector reports so that a blurred face keeps its position
                    // (FaceDetector.PositionThreshold). Every orientation candidate has a mirrored
                    // twin that scores within noise of the true one, and the contested band is
                    // exactly where those twins live: settling the orientation on a 0.5-score
                    // sighting is how steering ends up inverted for a whole run (see
                    // RestoreOrientation).
                    if (!_orientationConfident && obs.IsConfident)
                    {
                        _orientationConfident = true;
                        Remember(new OrientationProbe.Candidate(
                            _feed.Rotation, _feed.VerticallyFlipped));
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal on teardown.
            }
            catch (Exception e)
            {
                // Suspending or destroying the rig disposes the feed and the worker out from
                // under an inference that is still in flight. That throw is the teardown, not a
                // camera that broke, and must not surface as a failure the player reads.
                if (Stale()) return;
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
            if (_orientationConfident) Remember(chosen);

            Debug.Log($"[CAM] orientation {(_orientationConfident ? "settled" : "UNRESOLVED")}: " +
                      $"{chosen} bestScore={_probe.BestScore:F2} | {_feed.Describe()}");
            State = RigState.Tracking;
        }

        void Remember(OrientationProbe.Candidate chosen)
        {
            _settledOrientation = chosen;
            _hasSettledOrientation = true;
        }

        /// Puts an already-settled orientation onto the freshly reopened camera instead of
        /// probing again.
        ///
        /// Re-probing on resume is what made the second half of a run steer backwards. Every
        /// candidate has a twin at (rotation + 180, !flip) that is upright but horizontally
        /// MIRRORED, and BlazeFace scores a face and its mirror within noise of each other. The
        /// launch probe runs under the conditions the staging text asks for — phone propped up,
        /// player stepped back — so the true candidate clears GoodEnoughScore and the probe stops
        /// on the device's own report before the twin is ever tried. A resume probe runs with the
        /// player leaning over the phone they just tapped RESUME on: every candidate lands in the
        /// contested band, all eight get compared, and the twin wins about half the time. x is
        /// then inverted for the rest of the run while y survives, so jump and slide keep working
        /// — which is exactly how it was reported from the device.
        ///
        /// Keeping the answer across a pause is safe because the only input to it that could
        /// move is the device-reported rotation, and the app is portrait-locked (BuildScript
        /// forces UIOrientation.Portrait, ProjectSettings agrees), so the sensor's relationship
        /// to the screen is identical on both sides of a pause. A real teardown — quit to menu,
        /// or a failure — drops it, and the next rig probes from scratch.
        void RestoreOrientation()
        {
            _probe = null;
            _orientationConfident = true;
            _feed.SetOrientation(_settledOrientation.RotationDegrees,
                _settledOrientation.VerticallyFlipped);
            _feed.Restraighten();

            Debug.Log($"[CAM] orientation reused: {_settledOrientation} | {_feed.Describe()}");
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
            _generation++;
            _detector?.Dispose();
            _feed?.Dispose();
            _detector = null;
            _feed = null;

            // Unlike Suspend, this is the end of the rig: quitting to the menu or a failure both
            // land here, and whatever comes next re-does the whole staging, probe included.
            _hasSettledOrientation = false;
        }

        void OnDestroy() => TearDown();
    }
}
