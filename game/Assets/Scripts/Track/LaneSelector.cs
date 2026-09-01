using System;

namespace MotionRunner.Track
{
    /// Which of the three lanes the runner is in, and the short eased slide that carries it to
    /// that lane's centre.
    ///
    /// The road has exactly three lanes (TrackMetrics.LaneCount), so coming to rest between two
    /// of them was never a place to be: it looked like indecision and it made "am I clear of that
    /// block" a question the player had to answer by eye. The steering axis therefore no longer
    /// drives a lateral *velocity* — it names a lane, and the runner animates to it. The runner is
    /// committed to exactly one lane at every instant; the slide is presentation, and collisions
    /// keep reading the real x while it runs (see the note on the slide below).
    ///
    /// Engine-free like PauseState and ScoreState, because the two things that can be quietly
    /// wrong here are both arithmetic and neither is reliably caught by playing:
    ///   * a zone boundary with no hysteresis, where a trembling hand or a frame of face-tracking
    ///     jitter sitting on the threshold makes the runner flap between two lanes;
    ///   * a slide too slow for a dodge the track actually demands at top speed.
    /// Both are unit tested in LaneSelectorTests.
    public sealed class LaneSelector
    {
        // ---- zone mapping -------------------------------------------------------------------

        /// |axis| that commits the runner to a side lane. Every adapter already hands over a
        /// normalized axis, so one absolute mapping covers all of them:
        ///   * KeyboardInput reports -1 / 0 / +1, so holding a key is unambiguously that lane and
        ///     releasing it is unambiguously the centre;
        ///   * GyroTiltInput divides by a 0.35 g full deflection (~20 deg of roll), so 0.5 is
        ///     ~10 deg of deliberate tilt from wherever the player calibrated neutral;
        ///   * FaceSteering rescales past a 0.15 dead zone, so 0.5 is a lean of
        ///     0.15 + 0.5 * 0.85 = 57.5% of its HalfRangeX (1.1 *face widths*, so ~9.5 cm of head
        ///     travel) — a real lean, well short of having to reach the end of the range, and the
        ///     same 9.5 cm whether the phone is in a hand or across a table.
        public const float EnterThreshold = 0.5f;

        /// |axis| needed to *stay* in a side lane once entered. The gap to EnterThreshold is the
        /// hysteresis: 0.2 of axis is ~4 deg of tilt, or ~0.19 of a face width (~2.8 cm) of lean,
        /// which is far more than either input's noise survives — a face box centre jitters by a
        /// few hundredths of a face width even at the far end of the playable range, and both
        /// inputs low-pass before this ever sees a number (GyroTiltInput at 12 Hz, FaceSteering at
        /// 14 Hz). Crossing the boundary once therefore costs one lane change, not a burst of them.
        public const float HoldThreshold = 0.3f;

        // ---- the slide ----------------------------------------------------------------------

        /// Seconds to cross one lane width. The slide is eased, so this is the whole gesture, not
        /// an average speed.
        ///
        /// The number the track sets: obstacle clusters can be TrackMetrics.MinClusterSpacing (6 m)
        /// apart and the world tops out at TrackMetrics.SpeedFor(DifficultyCurve.MaxDifficulty)
        /// = 19.7 m/s, so the tightest dodge the generator may ever ask for has 6 / 19.7 = 0.305 s
        /// in it. At 0.14 s per lane a one-lane dodge takes 46% of that window and a full-width
        /// sweep across all three lanes takes 0.28 s — 92%, so even the worst case fits.
        ///
        /// It is also strictly quicker than the analog steering it replaces, which moved at 7 m/s
        /// at full deflection: 1.6 / 7 = 0.229 s for one lane and 0.457 s across the road, and that
        /// was *after* the input filter had ramped the axis up to full. Nothing about dodging got
        /// slower.
        public const float SecondsPerLane = 0.14f;

        /// Floor on a slide's duration, so retargeting a few centimetres from a lane centre is a
        /// visible movement rather than a snap. Binds below 0.57 m of travel.
        public const float MinSlideSeconds = 0.05f;

        // ---- state --------------------------------------------------------------------------

        /// -1, 0 or +1. The lane the runner belongs to right now — where it is, or where it is on
        /// its way to.
        public int Lane { get; private set; }

        /// The runner's lateral position. Equals TrackMetrics.LaneCenterX(Lane) whenever the slide
        /// has finished, which is every frame the player is not actively changing lanes.
        public float X { get; private set; }

        /// True while the slide is still running.
        public bool IsSliding => _elapsed < _duration;

        float _from;
        float _to;
        float _elapsed;
        float _duration;

        public LaneSelector() => Reset();

        /// Back to a settled runner. A fresh run starts in the centre lane.
        public void Reset(int lane = 0)
        {
            Lane = ClampLane(lane);
            X = TrackMetrics.LaneCenterX(Lane);
            _from = X;
            _to = X;
            _elapsed = 0f;
            _duration = 0f;
        }

        /// One frame: read the axis, keep or change lane, advance the slide. Returns the lane.
        ///
        /// The slide is only restarted when the lane actually changes, so an axis held steady
        /// inside a lane lets the current slide finish instead of being re-aimed every frame at a
        /// target it never reaches.
        public int Step(float axis, float deltaTime)
        {
            int next = LaneFor(axis, Lane);
            if (next != Lane)
            {
                Lane = next;
                _from = X;
                _to = TrackMetrics.LaneCenterX(next);
                _elapsed = 0f;
                _duration = SlideSeconds(Math.Abs(_to - _from));
            }

            if (deltaTime > 0f) _elapsed += deltaTime;

            if (_elapsed >= _duration)
            {
                // Landing on the centre exactly, rather than asymptotically close to it, is what
                // lets "the runner is never between lanes" be a fact instead of a tolerance.
                _elapsed = _duration;
                X = _to;
            }
            else
            {
                X = _from + (_to - _from) * Ease(_elapsed / _duration);
            }

            return Lane;
        }

        /// The zone mapping, pure so it is a table rather than a nest of ifs inside a MonoBehaviour.
        ///
        /// Absolute, not relative: the axis names a lane, it does not nudge the runner one lane
        /// over. Leaning hard from the left lane therefore goes straight to the right lane — the
        /// slide is what makes the trip through the middle visible — while a half-hearted lean the
        /// other way only ever returns to the centre.
        ///
        /// A non-finite axis compares false against every threshold and lands on the centre lane,
        /// which is the safe answer for an input that has stopped making sense.
        public static int LaneFor(float axis, int fromLane)
        {
            fromLane = ClampLane(fromLane);

            if (axis >= EnterThreshold) return TrackMetrics.MaxLane;
            if (axis <= -EnterThreshold) return TrackMetrics.MinLane;

            // Inside the band the runner keeps the lane it is in, but only against a lean of the
            // same sign: sitting in the left lane while leaning right is not a pose to hold.
            if (fromLane > 0) return axis >= HoldThreshold ? fromLane : 0;
            if (fromLane < 0) return axis <= -HoldThreshold ? fromLane : 0;
            return 0;
        }

        /// How long a slide of this distance takes. Proportional to distance, so lateral speed is
        /// the same whether the runner steps one lane or crosses the road.
        public static float SlideSeconds(float distance)
        {
            if (!(distance > 0f)) return 0f;
            float proportional = SecondsPerLane * distance / TrackMetrics.LaneWidth;
            return proportional < MinSlideSeconds ? MinSlideSeconds : proportional;
        }

        /// Smoothstep. Zero lateral speed at both ends, peaking at 1.5x the average in the middle:
        /// the runner leaves and arrives without a visible pop, which is the whole point of
        /// animating the change rather than snapping it.
        public static float Ease(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return t * t * (3f - 2f * t);
        }

        static int ClampLane(int lane) =>
            lane < TrackMetrics.MinLane ? TrackMetrics.MinLane :
            lane > TrackMetrics.MaxLane ? TrackMetrics.MaxLane : lane;
    }
}
