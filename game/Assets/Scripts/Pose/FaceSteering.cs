using System;

namespace MotionRunner.Pose
{
    /// Face position -> LEFT/RIGHT/JUMP/SLIDE, the gesture half of camera control (T-012, on the
    /// BlazeFace path chosen in OPEN_QUESTIONS 7d).
    ///
    /// Clock-free and engine-free like the rest of this assembly: the caller passes normalized
    /// face coordinates plus the frame's dt, so every rule here is testable headlessly with
    /// synthetic trajectories — which is what makes a false-positive rate measurable rather than
    /// eyeballed (T-012's acceptance criterion).
    ///
    /// Coordinate convention matches PoseLandmark: normalized [0,1], origin top-left, y down.
    /// A jump therefore shows as y *decreasing* quickly; a crouch as y sitting *above* baseline.
    ///
    /// ---- units: face widths, not frame fractions --------------------------------------------
    ///
    /// Every threshold below is expressed in FACE WIDTHS, and that is the difference between a
    /// mode that only works with the phone in your hand and one that works with it on a table.
    ///
    /// The detector reports positions as fractions of the camera frame. A fraction of the frame is
    /// not a distance: the same 10 cm head movement is a large fraction of the frame at arm's
    /// length and a small one from across a room, so thresholds written in frame fractions are
    /// silently thresholds on *distance*. That is what the first tuning pass baked in. With the
    /// old HalfRangeX of 0.14 frame widths, committing to a side lane needed the face centre to
    /// move 0.0805 of the frame — 3.8 cm of head travel with the phone held at 40-50 cm (face
    /// ~0.30 of the frame), but 15 cm at 2 m and 20 cm at 2.5 m (face ~0.075 and ~0.06). Crossing
    /// straight from the left lane to the right one, which needs the full swing from one side to
    /// the other, went from ~10 cm handheld to ~40 cm at table distance: nobody does that in the
    /// 0.3 s a dodge allows, so the runner stopped in the middle and hit the wall.
    ///
    /// FaceObservation.Size — the detector's own box width, already produced every frame — is the
    /// scale that removes distance from the problem. A face is ~15 cm wide whoever is playing, so
    /// dividing by it turns every threshold into a physical head movement that means the same
    /// thing at 40 cm and at 2.5 m. Handheld is then simply "the face is large", not a special
    /// case.
    ///
    /// One correction on top of that, and it is the difference between the maths above being right
    /// on paper and right on a phone: the detector's box is NOT an anatomical face. BlazeFace
    /// regresses the box its training labels use, which reaches past the ears and above the
    /// hairline, so one box is roughly 1.35 faces (FaceSizeFilter.BoxWidthsPerFace). Dividing
    /// straight by Size therefore measured everything in boxes while calling them faces, and
    /// multiplied every centimetre target by that factor — entering a lane cost ~12.8 cm instead
    /// of 9.5, and crossing the road in one movement ~26 cm instead of 19. A 25 cm side-step from
    /// a metre away lands right on that boundary, which is why "it got worse at distance" and why
    /// nothing here caught it: the tests spoke the detector's units too, so the factor cancelled.
    /// ToFacesX / ToFacesY now divide by FaceSizeFilter.FaceWidth, which is the box converted
    /// once, in one place.
    ///
    /// Two axes, two normalizations, so the conversion is not symmetric: x and Size are fractions
    /// of the frame's WIDTH, y is a fraction of its HEIGHT. FrameAspect (width/height of the
    /// upright frame, 0.75 for the 480x640 portrait frame the 640x480 request turns into) is what
    /// puts them in the same unit — see ToFacesX / ToFacesY.
    ///
    /// The scale is recomputed from the smoothed size every sample rather than captured once at
    /// calibration. That is the self-correcting choice: a player who rolls their chair back or
    /// stands up mid-run keeps the same physical gesture, where a frozen reference would quietly
    /// rescale every threshold and need a recalibration nobody would know to ask for. The cost is
    /// that the divisor is a live measurement, which is why it goes through FaceSizeFilter (a
    /// low-pass plus a clamp) instead of being used raw.
    ///
    /// Handoff 8.3: geometry and temporal thresholds, no classifier. The neutral pose is
    /// calibrated from the first confident samples and then drifts slowly toward wherever the
    /// player actually stands, so nobody has to hold a military posture for a whole run.
    ///
    /// ---- losing the face: HOLD the lane, never recentre ---------------------------------------
    ///
    /// Owner's design call, 31 Aug, from live play: *"when no face is identified it goes back to
    /// the middle; it should stay on the last position identified — if I get too much to the left
    /// it makes me lose."* So a lost face freezes MoveAxis and Deflection at their last values
    /// **indefinitely**, and the run keeps the lane it was in. When the face comes back, the
    /// ordinary absolute mapping resumes from wherever the player is actually standing — there is
    /// no relative offset to carry, so a reacquisition somewhere else simply steers there.
    ///
    /// The previous contract decayed the axis to 0 after a 0.28 s grace window, and that decay was
    /// the defect: three lanes means the centre band is the *widest* target, so an axis sliding to
    /// zero walks the runner into the middle lane no matter which lane the player was holding, and
    /// walking into the middle is what kills you when the obstacle is there. It also broke hops in
    /// a side lane, because a hop is exactly when the face blurs: the detector dropped frames, the
    /// decay pulled the runner off the lane mid-air, and the hop's own displacement window lost the
    /// apex samples that would have fired the jump.
    ///
    /// A frozen axis is benign here in a way it would not be with analog steering, and that is what
    /// makes an unbounded hold acceptable against rule 3 (CV never blocks release):
    ///   * discrete lanes — a held axis holds one lane, it does not accelerate the runner anywhere;
    ///   * jump and slide still cannot FIRE while the face is lost (IsTracking gates them), so a
    ///     hold can never spend a life on a gesture nobody made;
    ///   * touch and keyboard stay composed in alongside the camera, and the pause button is UI, so
    ///     the player is never without a way to act;
    ///   * the overlay turns the stickman red and captions it, so the hold is legible rather than a
    ///     mystery, and Lift still releases (below) so the glyph cannot sit airborne forever.
    /// The one quirk it deliberately introduces: stepping out of frame is now a way to *keep* a
    /// lane. If that ever needs a cap, it is one constant (a bound on _lossHeld before the axis is
    /// released) — the owner asked for the uncapped version first.
    public sealed class FaceSteering
    {
        // ---- tunables (face widths; a face is ~15 cm, so 1.0 here is ~15 cm of head travel) ----

        /// The CONFIDENT tier. At or above this detector score the observation is a face the
        /// pipeline will act on: tracking state, calibration, neutral drift, and every discrete
        /// gesture trigger need a sample this good.
        ///
        /// Two tiers, not one, because BlazeFace's score collapses for two or three frames in the
        /// middle of exactly the movements this mode is made of — a fast side-step, and the ascent
        /// of a hop — and a single threshold forces one answer to two different questions. "Is
        /// there a face here?" and "am I sure enough to fire a jump?" want different bars: the
        /// first is about *position continuity*, where a blurred box is still within a centimetre
        /// or two of the head, and the second is about false positives, which cost a life.
        public const float DefaultMinScore = 0.65f;

        /// The POSITION tier, and the floor under everything: below this the detector is reporting
        /// noise rather than a face, and a lost face is a lost face (see the loss contract above).
        ///
        /// Between the two tiers the observation carries POSITION ONLY: the axis, the deflection,
        /// the lift and the jump displacement window all follow it, so a blur streak mid-gesture no
        /// longer erases the gesture — but nothing fires from it on its own, IsTracking stays
        /// false, and neither the calibration, the neutral drift nor the size scale will believe it.
        ///
        /// **This threshold on its own does NOT separate a blurred face from an empty room, and the
        /// device says so.** The measurement that was available when this was first set said noise
        /// scored 0.09-0.12 (an empty ceiling, 31 Aug), which made 0.45 look like the middle of a
        /// wide gap. Pointed at an ordinary room the same afternoon, the same phone reported raw
        /// scores of **0.42-0.47 with nobody in front of it**, and one of those frames landed above
        /// this floor. The detector's noise level is a property of whatever is in shot — furniture
        /// and face-like patterns score, ceilings do not — so no fixed number here is safe on its
        /// own. Raising the floor to clear the worst scene observed would just move the problem and
        /// cost the blurred frames the tier exists for, because how far under 0.65 a blurred real
        /// face falls has never been measured.
        ///
        /// PositionCarrySeconds is what actually does the separating: a marginal frame is only
        /// followed as the CONTINUATION of a face seen confidently a moment ago. Noise in an empty
        /// room has no such predecessor, so it is loss whatever it scores.
        public const float DefaultMinPositionScore = 0.45f;

        /// How long a confident sighting keeps vouching for the marginal frames that follow it.
        ///
        /// The discriminator the score threshold cannot be (see above). A blurred face mid-gesture
        /// is by definition a face that was confidently seen two or three frames ago; detector noise
        /// off a piece of furniture is not. So a sub-MinScore sample is only promoted to the position
        /// tier while a confident sample is this recent, and a genuine loss clears the vouch
        /// immediately — which means an empty room can score 0.6 all day and still never steer.
        ///
        /// 0.30 s is nine frames at the camera's 30 Hz: several times the two-or-three-frame blur
        /// streak it exists to bridge, and comfortably longer than a hop's 0.2 s ascent, while
        /// bounding to under a third of a second the window in which a player who has just walked
        /// out of frame could have noise followed as if it were them.
        public const float DefaultPositionCarrySeconds = 0.30f;

        /// The confident bar. FaceDetector.ScoreThreshold defaults from this constant so the two
        /// cannot drift apart: the detector marks an observation confident with exactly the number
        /// the gesture rules use to act on one.
        public float MinScore = DefaultMinScore;

        /// The noise floor. FaceDetector.PositionThreshold defaults from this constant — below it
        /// the detector does not even decode a box, so nothing downstream has a position to carry.
        public float MinPositionScore = DefaultMinPositionScore;

        /// How recently a confident sample must have arrived for a marginal one to be followed.
        public float PositionCarrySeconds = DefaultPositionCarrySeconds;

        /// Confident samples averaged into the initial neutral pose.
        public int CalibrationSamples = 12;

        /// Upright frame width / height. x and Size are normalized to the frame's width, y to its
        /// height, so this is what converts a y displacement into the same unit as an x one.
        ///
        /// 0.75 is the 480x640 portrait frame the app actually runs on (CameraFeed requests
        /// 640x480 and FrameOrientation's quarter turn swaps it for a portrait-locked phone), but
        /// it is a default, not an assumption: CameraFaceInput overwrites it every frame from the
        /// real texture dimensions the detector measured, so a device that hands back a different
        /// shape is handled rather than mis-scaled.
        public float FrameAspect = 480f / 640f;

        /// Horizontal offset from neutral that maps to full deflection, in face widths.
        ///
        /// 1.1 face widths is ~16.5 cm of head travel — a decisive lean or a small side-step, and
        /// the most the geometry allows: with the phone held at 40 cm the frame is only ~40 cm
        /// wide, so a 16.5 cm lean already carries the face box to the frame edge. Reaching full
        /// deflection is not the ask, though. LaneSelector commits to a side lane at |axis| >= 0.5
        /// and holds it at 0.3, which after the dead zone rescale is 0.575 and 0.405 of this
        /// number:
        ///   * enter a lane  = 0.63 face widths = ~9.5 cm of head travel,
        ///   * hold it       = 0.45 face widths = ~6.7 cm,
        ///   * cross left lane -> right lane directly = ~1.3 face widths = ~19 cm.
        /// Those are the same centimetres at every playing distance, which is the point. The old
        /// 0.14 *frame widths* asked for 3.8 cm handheld and 20 cm at 2.5 m for the same lane.
        public float HalfRangeX = 1.1f;

        /// Fraction of full deflection ignored around neutral, then rescaled so the usable
        /// range still reaches ±1. 0.15 of HalfRangeX is 0.165 face widths, ~2.5 cm — above any
        /// tracker jitter (see the note on the hysteresis band below) and below any lean anyone
        /// means.
        public float DeadZone = 0.15f;

        /// Axis low-pass sharpness; higher = snappier, noisier. Same shape as GyroTiltInput's.
        public float AxisSharpness = 14f;

        /// Net upward travel (face widths, y decreasing) inside JumpLookbackSeconds that reads as
        /// a hop.
        ///
        /// Displacement over a window, not the frame-to-frame velocity this used to measure. The
        /// caller submits every game frame, but the rig only finishes an observation every second
        /// or third one (30 fps camera plus a frame of readback), so the same y repeats and a
        /// frame difference reports the real speed multiplied by observation-interval / deltaTime
        /// — 1x to 3x depending on what frame rate the phone happens to be holding. The identical
        /// hop therefore fired at 60 fps and did not at 30, which is the "sometimes the jump is
        /// not identified" report. Net travel across a wall-clock window does not care how many
        /// samples land inside it, or how many of them the detector dropped.
        ///
        /// 0.40 face widths is ~6 cm of head rise. A real hop lifts the head 8-15 cm (0.5-1.0 face
        /// widths) and a head-lift with the phone in your hand about 5-10 cm, so both clear it;
        /// idle breathing and weight-shift move the head under 2 cm in a quarter second (~0.13
        /// face widths), so it keeps 3x of margin over the sway floor the SlowSwayNeverJumps
        /// trajectory pins down. In the old frame-height units this threshold was ~3 cm handheld
        /// and ~11 cm at 2 m — i.e. it quietly stopped accepting real hops at exactly the distance
        /// the mode is meant to be played from.
        public float JumpRise = 0.40f;

        /// The window the rise is measured over. Long enough to span a hop's whole ascent (~200
        /// ms) and to bridge the two or three frames motion blur costs BlazeFace in the middle of
        /// one; short enough that a face reacquired after a real loss finds the window empty
        /// rather than measuring itself against wherever the player used to be.
        public float JumpLookbackSeconds = 0.25f;

        /// A hop ends with the face above where the player stands. Requiring that is what keeps
        /// bobbing back up out of a dip too shallow to count as a slide from reading as a jump —
        /// the false positive a displacement rule invites and a velocity rule did not.
        ///
        /// 0.15 face widths is ~2.3 cm: three times the vertical box jitter at the far end of the
        /// playable range, and a small fraction of any hop worth the name.
        public float JumpApexAboveBaseline = 0.15f;

        /// How long IsJumpActive stays true once triggered — long enough that a 60 Hz poll
        /// cannot miss it, short enough that it cannot double-fire after landing.
        public float JumpWindowSeconds = 0.18f;

        /// Minimum time between two jumps. Absorbs the head bobbing back up after landing.
        ///
        /// Half a second rather than the original 0.7: RunnerController's own arc is airborne for
        /// 2 * 7.5 / 22 = 0.68 s and ignores jump input the whole time, so anything at or above
        /// that made the gesture — not the character — the thing blocking a second jump, and a
        /// hop timed to land on touchdown was swallowed. The track can ask for jumps 6 m apart
        /// (TrackMetrics.MinClusterSpacing) at 12-19.7 m/s, i.e. 0.3-0.5 s, though one arc
        /// usually clears both. 0.5 s still covers the head settling after a hop.
        public float JumpRefractorySeconds = 0.5f;

        /// How far below the calibrated baseline (y increasing) the face must sit to be a crouch,
        /// in face widths.
        ///
        /// 0.85 face widths is ~13 cm — unmistakably a deliberate duck, well clear of the ~2 cm a
        /// standing player sways and of leaning forward to look at the screen, and well under the
        /// 20-40 cm a real crouch drops the head by. It doubles as the width of the y drift band
        /// below, which wants to be generous for the same reason.
        public float SlideDrop = 0.85f;

        /// How long the drop must be sustained before SLIDE fires — a fast bob is not a crouch.
        public float SlideHoldSeconds = 0.10f;

        /// Standing up from a crouch is fast upward motion — exactly a jump's signature. Ending a
        /// slide therefore arms the jump refractory, so the stand-up cannot double as a jump.
        public float SlideReleaseRefractorySeconds = 0.45f;

        /// Neutral drift-correction rate (per second). Slow on purpose: it should absorb the
        /// player shuffling sideways over a minute, not eat a deliberate lean.
        public float RecenterSharpness = 0.25f;

        /// How fast **Lift** returns to zero when the face is lost. The steering axis does not
        /// decay at all any more — see the loss contract on the class.
        ///
        /// Lift is the one loss output that must still let go, because it is not a command: it is
        /// what the overlay draws and what the telemetry line prints. Left latched, a face lost at
        /// the top of a hop kept `lift=+1.000` in every subsequent log line and drew the stickman
        /// permanently airborne right next to its own "no face" caption — the panel contradicting
        /// itself in the one place it exists to be trusted (seen on device, 31 Aug).
        public float LossDecaySharpness = 6f;

        /// How long a lost face holds **Lift** before that decay starts.
        ///
        /// Same grace the axis used to get, and for the same reason, now that only Lift needs it: a
        /// blur gap at the top of a hop must not flatten the glyph. 0.28 s spans a blur gap plus the
        /// detector frames either side of it and stays under the 0.35 s CameraFaceInput already
        /// treats as a stale observation.
        public float LossGraceSeconds = 0.28f;

        // ---- outputs ----

        public float MoveAxis { get; private set; }
        public bool IsJumpActive => _jumpTimer > 0f;
        public bool IsSlideActive { get; private set; }

        /// A CONFIDENT face this frame (score >= MinScore). What gates every gesture trigger, the
        /// calibration and the drift, and what the overlay colours the glyph by — so a blur streak
        /// reads as "not sure" on screen even while the lane keeps being held.
        public bool IsTracking { get; private set; }

        /// A position this frame, confident or not (score >= MinPositionScore). The difference
        /// between the two is the whole two-tier design: `IsTracking == false && HasPosition ==
        /// true` is a blurred real face whose movement is still being followed, where
        /// `HasPosition == false` is a genuine loss and the axis is frozen.
        public bool HasPosition { get; private set; }

        public bool IsCalibrated { get; private set; }

        /// Where "straight ahead" currently is — exposed for debug overlays and tuning.
        public float NeutralX { get; private set; }
        public float BaselineY { get; private set; }

        /// Signed lateral displacement from neutral, in units of HalfRangeX and clamped to
        /// [-1, +1]: where the camera thinks the player's body is *before* the dead zone and
        /// before the axis low-pass. +1 is full deflection to the player's right.
        ///
        /// MoveAxis is what the runner obeys; this is what the player did. Keeping both is what
        /// lets the on-screen overlay separate "I did not move far enough" from "I moved and the
        /// game did not follow", which are the two halves of every camera-steering complaint so far
        /// and are indistinguishable from a single number.
        public float Deflection { get; private set; }

        /// Signed vertical displacement from the calibrated baseline in units of SlideDrop, clamped
        /// to [-1, +1], positive UP. +1 is a hop's worth of head lift, -1 a full crouch. Overlay
        /// and telemetry only — the gesture rules use their own thresholds in face widths.
        public float Lift { get; private set; }

        /// The smoothed face width every threshold is measured in, and what it was when the
        /// player calibrated. Not used to scale anything — the live value is — but the pair is
        /// what a distance problem looks like in a log line, so it is worth exposing.
        public float FaceSize => _size.Value;
        public float CalibratedSize { get; private set; }

        /// True once the smoothed face is smaller than the pipeline can work with: the player is
        /// further away than BlazeFace short-range's rated ~2 m. Staging uses the same test before
        /// a run starts (see FaceSizeFilter.MinPlayableSize); mid-run it is a diagnostic only,
        /// because taking the controls away from someone who drifted back is worse than steering
        /// them coarsely.
        public bool IsTooFar => _size.IsTooFar;

        /// Recent face heights and their ages, oldest first in a ring. Sized for 0.25 s of
        /// submits at any frame rate the game can reach (64 / 0.25 s = 256 Hz); if something ever
        /// outruns it the oldest sample is dropped, which only shortens the window.
        const int HistoryCapacity = 64;

        readonly float[] _historyY = new float[HistoryCapacity];
        readonly float[] _historyAge = new float[HistoryCapacity];
        int _historyStart;
        int _historyCount;

        readonly FaceSizeFilter _size = new FaceSizeFilter();

        float _calibSumX;
        float _calibSumY;
        int _calibCount;
        float _jumpTimer;
        float _refractory;
        float _slideHeld;
        float _lossHeld;

        /// Time left on a rise that cleared JumpRise on a POSITION-tier sample and is waiting for a
        /// confident frame to confirm it. See the jump block in Submit.
        float _jumpArmed;

        /// Time left on the last confident sighting's vouch for the marginal frames after it. See
        /// PositionCarrySeconds — this is what stops detector noise from ever being followed.
        float _confidentFor;

        public void Reset()
        {
            MoveAxis = 0f;
            Deflection = 0f;
            Lift = 0f;
            IsSlideActive = false;
            IsTracking = false;
            HasPosition = false;
            IsCalibrated = false;
            NeutralX = 0f;
            BaselineY = 0f;
            CalibratedSize = 0f;
            _calibSumX = 0f;
            _calibSumY = 0f;
            _calibCount = 0;
            _jumpTimer = 0f;
            _refractory = 0f;
            _slideHeld = 0f;
            _lossHeld = 0f;
            _jumpArmed = 0f;
            _confidentFor = 0f;
            _size.Reset();
            ClearHistory();
        }

        /// One face observation. x/y normalized [0,1] (origin top-left, y down), size the face box
        /// width as a fraction of the frame width, score the detector's confidence, dt the seconds
        /// since the previous Submit.
        ///
        /// Three tiers by score, and every rule below belongs to exactly one of them:
        ///   * score &lt; MinPositionScore      — LOSS. The axis freezes; nothing fires.
        ///   * MinPositionScore .. MinScore   — POSITION ONLY, and only while a confident sample is
        ///                                      less than PositionCarrySeconds old. The axis, the
        ///                                      deflection, the lift and the jump window follow the
        ///                                      face; no trigger, no calibration, no drift, no
        ///                                      rescale. Without that recent confident sample the
        ///                                      sample is indistinguishable from detector noise and
        ///                                      is treated as loss.
        ///   * score &gt;= MinScore              — CONFIDENT. Everything.
        public void Submit(float x, float y, float size, float score, float dt)
        {
            if (dt < 1e-4f) dt = 1e-4f;
            if (dt > 0.1f) dt = 0.1f; // a hitch must not read as instantaneous head teleport

            if (_jumpTimer > 0f) _jumpTimer -= dt;
            if (_refractory > 0f) _refractory -= dt;
            if (_jumpArmed > 0f) _jumpArmed -= dt;
            if (_confidentFor > 0f) _confidentFor -= dt;

            // A marginal sample is followed only as the continuation of a face that was just seen
            // properly. Noise in an empty room scores 0.42-0.47 on the shipping device (measured
            // 31 Aug, pointed at a room rather than a ceiling), so the score threshold alone cannot
            // tell "blurred player" from "chair" — this can. See PositionCarrySeconds.
            bool confident = score >= MinScore;
            if (!confident && _confidentFor <= 0f) score = 0f;

            // Aged whether or not this frame saw a face. Dropped samples therefore cost the jump
            // window nothing (motion blur takes BlazeFace under its threshold exactly during the
            // fast part of a hop), while a face genuinely lost for longer than the window leaves
            // it empty, so reacquiring higher up cannot read as a rise.
            AgeHistory(dt);

            if (score < MinPositionScore)
            {
                // ---- LOSS: hold the lane, drop everything that could act ----
                // MoveAxis and Deflection are deliberately UNTOUCHED — the whole loss contract, see
                // the note on the class. Everything that could act on its own is dropped instead:
                // tracking (so no gesture can fire), the slide state, and a rise that was waiting
                // for a confident frame to confirm it (a hop the detector never really saw must not
                // fire when the player walks back into frame).
                IsTracking = false;
                HasPosition = false;
                IsSlideActive = false;
                _slideHeld = 0f;
                _jumpArmed = 0f;
                _confidentFor = 0f; // a face that was really lost vouches for nothing after it

                // Lift is the exception, because it is a report and not a command: held through the
                // grace window so a blur gap at a hop's apex does not flatten the glyph, then
                // released. See LossDecaySharpness.
                _lossHeld += dt;
                if (_lossHeld > LossGraceSeconds)
                {
                    float decay = Math.Min(1f, LossDecaySharpness * dt);
                    Lift -= Lift * decay;
                }
                return;
            }

            // A real position: either confident, or vouched for by a confident one just behind it.
            HasPosition = true;
            IsTracking = confident;
            _lossHeld = 0f;
            if (confident) _confidentFor = PositionCarrySeconds;

            // The scale everything below is measured in, updated before it is used — from confident
            // samples only. A blurred box is a good enough *position* and a poor width (that is
            // usually why the score fell), and this number is a DIVISOR: one bad sample here
            // rescales every threshold in the class. Two or three frames cannot change how far away
            // the player is standing, so the last confident scale is still the true one.
            if (confident) _size.Submit(size, dt);

            if (!IsCalibrated)
            {
                // Calibration defines the frame of reference everything else is measured against,
                // so it takes confident samples only — the one place where waiting a few more
                // frames costs nothing.
                if (!confident) return;

                _calibSumX += x;
                _calibSumY += y;
                _calibCount++;
                if (_calibCount >= CalibrationSamples)
                {
                    NeutralX = _calibSumX / _calibCount;
                    BaselineY = _calibSumY / _calibCount;
                    CalibratedSize = _size.Value;
                    IsCalibrated = true;
                }
                PushHistory(y);
                return;
            }

            // ---- LEFT / RIGHT ----
            // Both tiers: this is the position continuity a blurred face is kept for. The mapping
            // is absolute, so a face reacquired anywhere simply steers to where it now is — which is
            // what makes the unbounded loss hold safe to come back from.
            float raw = Clamp(ToFacesX(x - NeutralX) / HalfRangeX, -1f, 1f);
            Deflection = raw; // before the dead zone and the low-pass, for the overlay
            float magnitude = Math.Abs(raw);
            raw = magnitude < DeadZone
                ? 0f
                : Math.Sign(raw) * (magnitude - DeadZone) / (1f - DeadZone);
            float t = 1f - (float)Math.Exp(-AxisSharpness * dt);
            MoveAxis += (raw - MoveAxis) * t;

            // ---- JUMP: net upward travel over the last JumpLookbackSeconds, rate-limited ----
            // Measured against the lowest the face has been inside the window, which is where the
            // hop pushed off from. Frames the detector lost in between are simply absent; the
            // takeoff sample is still there, so the rise is still visible when the face comes
            // back. An empty window (a real loss) reads as no rise at all.
            //
            // The window is at most 0.25 s long, so converting the *difference* with the current
            // scale is exact enough: the smoothed size cannot meaningfully change inside it.
            float rise = _historyCount > 0 ? ToFacesY(LowestInWindow() - y) : 0f;
            float belowBaseline = ToFacesY(y - BaselineY);
            bool aboveBaseline = -belowBaseline >= JumpApexAboveBaseline;
            Lift = Clamp(-belowBaseline / SlideDrop, -1f, 1f); // overlay only

            // ARM on any tier, FIRE only on a confident one. This is what makes a hop in a side
            // lane fire at all, and it is a two-tier rule because the arithmetic leaves no other
            // option: the apex is the only sample where the rise is large, and the apex is exactly
            // the sample motion blur takes away. Worked through at 30 Hz for a 0.7-face-width hop
            // with a 0.2 s ascent, the apex rejected and the next confident frame 0.1 s into the
            // descent — by then the take-off sample has aged out of the 0.25 s window, so the rise
            // that frame can measure is 0.175 face widths against a JumpRise of 0.40, and the jump
            // is simply lost. That is the owner's "when jumping in one of the two side lanes it is
            // not recognised that well". Merely *remembering* the blurred samples does not help:
            // they are above the take-off, so they never become the window's low point.
            //
            // So a position-tier sample may notice the rise, and then a confident sample has to
            // confirm the face is real before anything fires — a low-score frame never fires a jump
            // by itself, which is the false-positive discipline this mode is held to. The arm lives
            // JumpLookbackSeconds (the same 0.25 s the window does, refreshed while the rise is
            // still there) and dies instantly on a real loss, so the cost of the confirmation is
            // the two or three frames until the blur clears, not a jump that arrives whenever.
            if (rise >= JumpRise && aboveBaseline) _jumpArmed = JumpLookbackSeconds;

            if (_jumpArmed > 0f && confident && _refractory <= 0f && !IsSlideActive)
            {
                _jumpTimer = JumpWindowSeconds;
                _refractory = JumpRefractorySeconds;
                _jumpArmed = 0f;
                // Coming back down must not be measured against the take-off point.
                ClearHistory();
            }

            PushHistory(y);

            // ---- SLIDE: sustained crouch below baseline (confident samples only) ----
            // On a position-tier frame the slide state FREEZES, the same way the axis does and for
            // the same reason: a blur frame in the middle of a held crouch must not stand the runner
            // up. _slideHeld not accumulating is the other half of that — a slide can never *start*
            // out of blurred frames, only be kept.
            if (confident)
            {
                if (belowBaseline > SlideDrop)
                {
                    _slideHeld += dt;
                    IsSlideActive = _slideHeld >= SlideHoldSeconds;
                }
                else
                {
                    if (IsSlideActive && _refractory < SlideReleaseRefractorySeconds)
                        _refractory = SlideReleaseRefractorySeconds;
                    _slideHeld = 0f;
                    IsSlideActive = false;
                }
            }

            // ---- slow drift correction (confident samples only) ----
            // Both drifts are slow integrators over where the player is standing, so feeding them a
            // blurred box would move the frame of reference itself on the strength of the frames
            // least worth believing.
            if (!confident) return;

            // x recenters only while the player is not deliberately leaning; y only while they
            // are not crouching or jumping, or the baseline would chase the gesture it detects.
            //
            // The y band is the full SlideDrop, not half of it. At half, a player who ended up
            // standing between half and a whole SlideDrop below their calibrated baseline sat in a
            // gap where the drift was frozen and no slide fired, so the baseline never caught up —
            // and now that the jump rule asks where the face is relative to the baseline, that gap
            // would have meant no jumps for the rest of the run. Above SlideDrop the crouch is a
            // slide and IsSlideActive stops the drift anyway. The rate is slow enough (4 s time
            // constant) that a fraction of a second of crouching still moves the baseline by
            // nothing.
            float rt = 1f - (float)Math.Exp(-RecenterSharpness * dt);
            if (Math.Abs(MoveAxis) < 0.25f) NeutralX += (x - NeutralX) * rt;
            if (!IsSlideActive && _jumpTimer <= 0f && Math.Abs(belowBaseline) < SlideDrop)
                BaselineY += (y - BaselineY) * rt;
        }

        // ---- units ----

        /// A horizontal frame-width offset in face widths. x and Size share a normalization, so
        /// this is one division — by FaceWidth, the detector's box converted to an anatomical face
        /// (see the note on units above and FaceSizeFilter.BoxWidthsPerFace).
        float ToFacesX(float frameWidths) => frameWidths / _size.FaceWidth;

        /// A vertical frame-height offset in face widths. y is normalized to the frame's height
        /// and Size to its width, so the aspect ratio has to come back in: one frame height is
        /// (1 / FrameAspect) frame widths.
        float ToFacesY(float frameHeights) => frameHeights / (_size.FaceWidth * FrameAspect);

        // ---- jump history ----

        void AgeHistory(float dt)
        {
            for (int i = 0; i < _historyCount; i++)
                _historyAge[(_historyStart + i) % HistoryCapacity] += dt;

            // Ages only ever increase from oldest to newest, so what expires is always a prefix.
            while (_historyCount > 0 && _historyAge[_historyStart] > JumpLookbackSeconds)
            {
                _historyStart = (_historyStart + 1) % HistoryCapacity;
                _historyCount--;
            }
        }

        void PushHistory(float y)
        {
            if (_historyCount == HistoryCapacity)
            {
                _historyStart = (_historyStart + 1) % HistoryCapacity;
                _historyCount--;
            }

            int end = (_historyStart + _historyCount) % HistoryCapacity;
            _historyY[end] = y;
            _historyAge[end] = 0f;
            _historyCount++;
        }

        void ClearHistory()
        {
            _historyStart = 0;
            _historyCount = 0;
        }

        /// The lowest the face has been inside the window — y points down, so that is the largest
        /// y. Callers check _historyCount first; an empty window has no answer.
        float LowestInWindow()
        {
            float lowest = float.MinValue;
            for (int i = 0; i < _historyCount; i++)
            {
                float y = _historyY[(_historyStart + i) % HistoryCapacity];
                if (y > lowest) lowest = y;
            }
            return lowest;
        }

        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
