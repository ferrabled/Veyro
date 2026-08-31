using MotionRunner.Pose;
using UnityEngine;

namespace MotionRunner.CameraInput
{
    /// The on-screen half of FaceTrackingRig's startup: turns the rig's state into the line the
    /// player reads, and holds the "I can see you" moment for a beat before calling the camera
    /// ready. The rig is the only thing that knows how far along it is, so every screen that
    /// waits on the camera - the mode picker at launch, the pause menu on resume - stages it
    /// through here rather than growing its own copy of the same switch.
    ///
    /// It also owns the one distance rule left in camera mode. Steering is distance-invariant now
    /// (FaceSteering measures in face widths), but the detector is not: past BlazeFace
    /// short-range's rated ~2 m there is nothing to measure. A face that is found but too small
    /// therefore reads as "step a bit closer…" and does not hand the run over.
    ///
    /// Unscaled time on purpose: resuming from the pause menu stages the camera while the game
    /// is frozen at Time.timeScale = 0.
    public sealed class CameraStaging
    {
        /// A face has to stay found for this long before the run is handed the camera. Half a
        /// second of "I can see you" is what turns a lucky frame into a player who is standing
        /// in the right place.
        public const float FaceSeenHoldSeconds = 0.5f;

        /// An observation older than this is not a face on screen right now.
        const float FreshSeconds = 0.35f;

        public enum Stage
        {
            Waiting,
            Ready,
            Failed
        }

        readonly FaceTrackingRig _rig;
        readonly FaceSizeFilter _size = new FaceSizeFilter();
        float _faceSeenSince = -1f;

        /// What the player should be reading right now. On Failed this is the rig's own reason,
        /// with no advice attached - what to do about it is the screen's call.
        public string Status { get; private set; } = string.Empty;

        public CameraStaging(FaceTrackingRig rig) => _rig = rig;

        /// Call once per frame while waiting on the camera.
        public Stage Poll()
        {
            switch (_rig.State)
            {
                case FaceTrackingRig.RigState.Gating:
                    Status = "checking device speed…";
                    return Stage.Waiting;

                case FaceTrackingRig.RigState.RequestingPermission:
                    Status = "waiting for camera permission…";
                    return Stage.Waiting;

                case FaceTrackingRig.RigState.StartingCamera:
                    Status = $"starting camera…  (speed check: {_rig.GateMedianMs:F0} ms ✓)";
                    return Stage.Waiting;

                case FaceTrackingRig.RigState.Probing:
                    Status = "finding which way up the camera is —\nprop the phone up and step back";
                    return Stage.Waiting;

                case FaceTrackingRig.RigState.Tracking:
                    // IsConfident, not HasFace: the detector now also reports marginal-score
                    // observations so that a blurred face mid-gesture keeps its position (see
                    // FaceDetector.PositionThreshold), and staging is the one place where the strict
                    // bar is the whole point. Handing a run over on a 0.5-score frame would start a
                    // run on a face the detector cannot actually hold — the opposite of what half a
                    // second of "I can see you" is for.
                    if (!(_rig.Latest.IsConfident && _rig.LatestAgeSeconds < FreshSeconds))
                    {
                        // The size measurement deliberately survives a dropout: the player did not
                        // teleport in the third of a second the detector lost them, and reseeding
                        // the filter from one jittery box is how a marginal distance would flicker
                        // between "I can see you" and "step a bit closer". Only a suspended rig
                        // (below) is a new situation.
                        _faceSeenSince = -1f;
                        Status = "stand where the phone can see you…";
                        return Stage.Waiting;
                    }

                    // Seen, but how far away? Every gesture rule now measures head travel in face
                    // widths, so distance no longer changes how big a lean has to be - but it
                    // still decides whether there is a face to measure at all. BlazeFace
                    // short-range is rated for about 2 m, and past that the detector is what runs
                    // out (the face is under 6 px of its 128 px input), which no steering maths
                    // can fix. So the one thing staging still has to police is standing too far
                    // back, and it is worth policing here rather than letting the run be the
                    // place someone discovers it.
                    _size.Submit(_rig.Latest.Size, Time.unscaledDeltaTime);
                    if (_size.IsTooFar)
                    {
                        _faceSeenSince = -1f;
                        Status = "step a bit closer…";
                        return Stage.Waiting;
                    }

                    if (_faceSeenSince < 0f) _faceSeenSince = Time.unscaledTime;
                    Status = "I can see you!";
                    return Time.unscaledTime - _faceSeenSince >= FaceSeenHoldSeconds
                        ? Stage.Ready
                        : Stage.Waiting;

                case FaceTrackingRig.RigState.Failed:
                    Status = _rig.FailReason;
                    return Stage.Failed;

                default: // Idle - the rig has not been told to start yet, or has been suspended
                    _faceSeenSince = -1f;
                    _size.Reset();
                    Status = "starting camera…";
                    return Stage.Waiting;
            }
        }
    }
}
