using System;

namespace MotionRunner.Pose
{
    /// The detected face's width, smoothed — the one number that makes camera control
    /// distance-invariant.
    ///
    /// Everything the detector reports is a fraction of the camera frame, and a fraction of the
    /// frame means nothing on its own: the identical head movement is a large fraction held at
    /// arm's length and a small one from across a table. Divide by the face's own width and the
    /// unit becomes physical — a face is ~15 cm wide whoever is playing, so "0.6 face widths" is
    /// ~9 cm of head travel at 40 cm from the lens and ~9 cm at 2.5 m. That conversion is the
    /// whole fix for "it works in my hand but not with the phone on the table"; this class is the
    /// scale factor it divides by.
    ///
    /// Two widths live here and they are not the same number: what the detector measures (Value,
    /// its regressed *box*) and what the thresholds are written in (FaceWidth, an anatomical
    /// face). BoxWidthsPerFace is the bridge, and getting it wrong scales every centimetre in
    /// FaceSteering — see its comment.
    ///
    /// Raw BlazeFace box widths jitter frame to frame (the box is regressed, and the frame is
    /// letterboxed down to 128 px before the model ever sees it), so the raw number is low-passed
    /// before anything divides by it. The filter also clamps what it will believe, because a
    /// divisor is exactly the place where one absurd sample turns into an absurd steering axis.
    ///
    /// Engine-free and clock-free like the rest of MotionRunner.Pose: the caller supplies dt.
    public sealed class FaceSizeFilter
    {
        /// Below this the mode is out of spec and the player should be asked to step closer.
        ///
        /// Size is the detector's box width as a fraction of the upright frame's width. The upright
        /// frame is the 640x480 request turned portrait, so 480 px across, covering roughly 53 deg
        /// of the front camera's field of view — about one metre of the world per metre of
        /// distance. A ~15 cm face therefore comes with a ~20 cm box (BoxWidthsPerFace) and reads
        /// about 0.20 at 1 m, 0.10 at 2 m and 0.081 at 2.5 m; 0.06 is reached at about 3.4 m.
        ///
        /// **That last number moved when the box factor arrived and the constant did not.** It was
        /// set believing 0.06 was a 15 cm face at 2.5 m; it is really a 20 cm box at ~3.4 m, so the
        /// cue now fires later — probably later than BlazeFace can hold a face at all, which makes
        /// "step a bit closer…" hard to reach and "stand where the phone can see you…" the message
        /// a player who has walked too far back actually gets. Left alone deliberately: erring long
        /// only ever lets a marginal player through, and a false "too far" is the expensive
        /// direction (it used to be a soft lock at the picker; back now cancels staging). The
        /// telemetry line reports `size`, so what a real 2.5 m looks like is one test session away.
        ///
        /// 0.06 is set by the *detector*, not by the steering maths: BlazeFace short-range is
        /// rated for faces within about 2 m, and by 2.5 m the face is ~29 px of the 480-wide frame
        /// and under 6 px of the model's own 128 px input. Past that, detection itself is what
        /// fails and no amount of gesture tuning brings it back — which is exactly why this is a
        /// staging cue ("step a bit closer") rather than a threshold anything gameplay-side reads.
        ///
        /// Deliberately half a metre *past* the model's rated range rather than at it: a false
        /// "too far" holds a player who could actually play at the staging screen, so the cue errs
        /// towards letting a marginal distance through and letting the run be the judge.
        public const float MinPlayableSize = 0.06f;

        /// The scale assumed before any sample has arrived — roughly a box at 1.3 m. Only ever the
        /// answer for the handful of frames before the first observation lands; a real value
        /// replaces it on the first usable sample rather than being averaged with it.
        public const float UnknownSize = 0.15f;

        /// Nothing outside this band is believed *at all* — an impossible width is dropped and the
        /// last good scale stands, rather than being clamped in. Clamping is the worse option in
        /// both directions: pulling a near-zero width up to the floor still divides by the
        /// smallest number allowed, which turns tracker noise into full-lock steering, and pulling
        /// a huge one down makes every gesture need a lunge. Dropping it keeps the scale the last
        /// believable observation established, which is the one thing that is certainly still
        /// roughly true.
        ///
        /// The floor is a face 2% of the frame wide — ~10 px, smaller than anything the detector
        /// can resolve. The ceiling is a face filling most of the frame, i.e. a phone about 25 cm
        /// from someone's nose.
        public const float MinTrustedSize = 0.02f;
        public const float MaxTrustedSize = 0.9f;

        /// How many of the detector's box widths fit in one anatomical face width.
        ///
        /// **This is the number that made a side-step at a metre stop working.** BlazeFace does
        /// not regress "the face": it regresses the SSD box its training data labels, which runs
        /// from about the chin to above the hairline and out past the ears — visibly wider than the
        /// ~15 cm of cheek-to-cheek head the thresholds in FaceSteering are written against. Every
        /// displacement in camera mode is divided by this width, so if one box is 1.35 faces then
        /// every centimetre target is silently multiplied by 1.35: the 9.5 cm designed for entering
        /// a lane becomes 12.8 cm, and the ~19 cm designed for crossing the road in one movement
        /// becomes ~26 cm. That is exactly the band where an ordinary 25 cm side-step stops being
        /// enough, which is what the phone reported and the unit tests could not see — they were
        /// written in the detector's own units, so the factor cancelled out of both sides.
        ///
        /// 1.35 is an ESTIMATE, not a measurement: it is the middle of the 1.2–1.5 the box's
        /// framing suggests. Calibrate it from the device with the `[CAM] telemetry` line
        /// (CameraTelemetry): step a measured distance sideways, read `size` and the change in `x`,
        /// and the true box width in centimetres is (step cm) × size / Δx — divide that by 15 and
        /// this constant is measured rather than guessed. It is deliberately the ONLY place the
        /// conversion happens (FaceWidth below), so re-calibrating is one edit.
        public const float BoxWidthsPerFace = 1.35f;

        /// Low-pass sharpness (per second); 3 is a ~0.33 s time constant. Slow enough to swallow
        /// per-frame box jitter and the box shrinking through the blurred middle of a hop, fast
        /// enough to follow a player who leans in to read the score and stays there.
        public float Sharpness = 3f;

        /// True once a usable observation has been seen. Until then Value is UnknownSize, which is
        /// a safe scale but not a measurement — "too far" must not be decided from a guess.
        public bool HasValue { get; private set; }

        /// The smoothed detector *box* width, in frame widths. Never leaves
        /// [MinTrustedSize, MaxTrustedSize], so callers may divide by it without a further guard.
        ///
        /// This is the raw measurement, not the anatomical face — divide displacements by
        /// FaceWidth, not by this. It stays exposed because it is what the "too far" rule is set
        /// in (a detector-pixel budget) and what the telemetry line has to report for
        /// BoxWidthsPerFace to be calibrated at all.
        public float Value { get; private set; } = UnknownSize;

        /// The smoothed *anatomical* face width, in frame widths — the scale every threshold in
        /// FaceSteering is measured in. The one place box units become face units.
        public float FaceWidth => Value / BoxWidthsPerFace;

        /// The player is further away than the pipeline can work with.
        ///
        /// Measured against the box, on purpose: this threshold is about how many pixels of the
        /// model's 128 px input the detector has left to work with, not about how big a gesture
        /// has to be. (One consequence worth knowing: because a box is ~1.35 faces,
        /// MinPlayableSize corresponds to a greater real distance than the ~2.5 m its comment was
        /// written for — see there.)
        public bool IsTooFar => HasValue && Value < MinPlayableSize;

        public void Reset()
        {
            HasValue = false;
            Value = UnknownSize;
        }

        /// One frame's raw box width. Unusable samples (a lost face reports 0, a broken one could
        /// report anything) leave the last good scale standing rather than dragging it towards
        /// nonsense — losing the face for a moment does not mean the player teleported.
        public float Submit(float rawSize, float dt)
        {
            // NaN and infinity both fail these comparisons, so this is the whole finiteness check
            // as well as the trusted-band one.
            if (!(rawSize >= MinTrustedSize && rawSize <= MaxTrustedSize)) return Value;

            if (!HasValue)
            {
                // Seeded, not ramped: the filter starts settled on the first real measurement, so
                // the first frames of a run are scaled correctly instead of easing out of a guess.
                Value = rawSize;
                HasValue = true;
                return Value;
            }

            if (dt < 0f) dt = 0f;
            Value += (rawSize - Value) * (1f - (float)Math.Exp(-Sharpness * dt));
            return Value;
        }
    }
}
