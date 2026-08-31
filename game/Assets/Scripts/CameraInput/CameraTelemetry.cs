using System.Globalization;
using MotionRunner.Pose;
using UnityEngine;

namespace MotionRunner.CameraInput
{
    /// One logcat line that says what the camera thinks, so the next device session produces
    /// numbers instead of adjectives.
    ///
    /// Every camera-mode report so far ("it works in my hand but not on the table", "a side-step
    /// is not recognised") has had to be diagnosed by reasoning about geometry, because the only
    /// thing the phone actually said was whether the runner moved. The whole chain is
    /// arithmetic — box width, position, neutral, deflection, axis, lane — and one line carrying
    /// all of it turns a feel complaint into a subtraction. In particular it is what calibrates
    /// FaceSizeFilter.BoxWidthsPerFace, which is currently an estimate: `size` and the change in
    /// `x` across a *measured* side-step give the detector's box width in centimetres directly.
    ///
    /// Costs nothing between emissions: the string is built inside the throttle, so a frame that
    /// does not log does a float compare and returns. Debug.Log reaches logcat under tag `Unity`
    /// in release builds (CLAUDE.md gotcha 8), so `adb logcat -d -s Unity | grep telemetry` is the
    /// whole retrieval story.
    ///
    /// Format — deliberately one line of stable `key=value` pairs in a fixed order, invariant
    /// culture, so it parses with a split:
    ///
    ///   [CAM] telemetry why=tick ctx=run size=0.2031 x=0.5124 nx=0.4981 defl=+0.093 lift=+0.021
    ///         axis=+0.041 lane=0 score=0.93 raw=0.93 track=1 pos=1 toofar=0
    ///
    ///   why    tick = the periodic sample, lane = emitted because the lane changed this frame
    ///   ctx    which screen is asking: run, or framing (the picker / pause-menu staging preview)
    ///   size   smoothed detector BOX width, fraction of frame width (NOT an anatomical face)
    ///   x      raw observation x, fraction of frame width, mirrored as the player sees themselves
    ///   nx     FaceSteering.NeutralX — where "straight ahead" has drifted to
    ///   defl   signed displacement from nx in units of HalfRangeX, clamped +-1, before dead zone
    ///   lift   signed displacement from the baseline in units of SlideDrop, clamped +-1, up = +
    ///   axis   FaceSteering.MoveAxis, the number the runner actually obeys
    ///   lane   -1 / 0 / +1, the lane held right now
    ///   score  score of the latest observation the detector decoded at all — since the two-tier
    ///          change that reaches down to FaceDetector.PositionThreshold (0.45), so a marginal
    ///          frame now shows its real number here instead of 0
    ///   raw    the detector's score before any threshold — now differs from `score` only under
    ///          0.45, i.e. it says how far into the noise a genuinely lost frame fell
    ///   track  1 while FaceSteering has a CONFIDENT face (score >= 0.65): what gates gestures
    ///   pos    1 while FaceSteering has a position at all (score >= 0.45). `track=0 pos=1` is a
    ///          blurred real face still being followed; `track=0 pos=0` is the loss hold, where the
    ///          axis below is frozen at whatever it last was rather than measured this frame
    ///   toofar 1 once the smoothed box is under FaceSizeFilter.MinPlayableSize
    public sealed class CameraTelemetry
    {
        /// Roughly one line a second. Slow enough to read a whole run in one logcat dump, fast
        /// enough that a 20 s stretch of standing at a measured distance gives ~20 samples of
        /// `size` to average.
        public const float IntervalSeconds = 1f;

        readonly string _context;

        float _nextAt = -1f;
        int _lane;
        bool _hasLane;

        /// context is the `ctx=` field: "run" or "framing".
        public CameraTelemetry(string context) => _context = context;

        /// Call once per frame. Emits on the interval, and immediately whenever the lane changed —
        /// a lane change is the event every one of these reports is really about, and catching the
        /// sample that caused it matters more than the ones around it.
        public void Sample(in FaceObservation obs, FaceSteering steering, int lane, float rawScore)
        {
            if (steering == null) return;

            bool laneChanged = _hasLane && lane != _lane;
            _hasLane = true;
            _lane = lane;

            float now = Time.unscaledTime;
            if (!laneChanged && _nextAt >= 0f && now < _nextAt) return;
            _nextAt = now + IntervalSeconds;

            var c = CultureInfo.InvariantCulture;
            Debug.Log("[CAM] telemetry" +
                      " why=" + (laneChanged ? "lane" : "tick") +
                      " ctx=" + _context +
                      " size=" + steering.FaceSize.ToString("F4", c) +
                      " x=" + obs.X.ToString("F4", c) +
                      " nx=" + steering.NeutralX.ToString("F4", c) +
                      " defl=" + steering.Deflection.ToString("+0.000;-0.000;0.000", c) +
                      " lift=" + steering.Lift.ToString("+0.000;-0.000;0.000", c) +
                      " axis=" + steering.MoveAxis.ToString("+0.000;-0.000;0.000", c) +
                      " lane=" + lane.ToString(c) +
                      " score=" + obs.Score.ToString("F2", c) +
                      " raw=" + rawScore.ToString("F2", c) +
                      " track=" + (steering.IsTracking ? 1 : 0) +
                      " pos=" + (steering.HasPosition ? 1 : 0) +
                      " toofar=" + (steering.IsTooFar ? 1 : 0));
        }
    }
}
