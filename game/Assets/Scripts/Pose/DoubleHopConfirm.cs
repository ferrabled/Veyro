using System;

namespace MotionRunner.Pose
{
    /// "I'm ready" for a paused camera run: stand still, then hop twice. This is how a camera
    /// resume is confirmed without anyone reaching for the phone (docs/CAMERA_TUNING.md, "Resume
    /// gesture", has the dials). It replaced the raised-right-hand confirm on 28 Sep 2026 (owner
    /// call): the hand needed a second ML model — BlazePose, ~12 MiB of APK, and it errored on the
    /// OnePlus 6T — while a hop is the move every camera player already makes to jump, read by the
    /// same FaceSteering the run steers with. No model, no load step, nothing to fail.
    ///
    /// Engine-free and clock-free like the rest of this assembly: it reads a FaceSteering the
    /// caller has just fed, and the caller passes dt. That is what lets every trajectory that
    /// matters — above all a player WALKING BACK INTO FRAME — be replayed headlessly against the
    /// real jump rule instead of being eyeballed on a phone.
    ///
    /// ---- the one failure this class exists to prevent -----------------------------------------
    ///
    /// A false confirm unfreezes a run the player did not ask for, and the most likely source of
    /// one is not noise: it is the jump detector doing its job on someone who is walking. Walking
    /// towards a phone propped below face height moves the face UP the frame (the camera looks up
    /// at a head that is getting closer), and every step bobs the head a few centimetres on top of
    /// that. Together they clear FaceSteering's hop rule (0.40 face widths of rise inside 0.25 s,
    /// ending above the baseline) — which was calibrated on wherever the player was when the face
    /// first appeared. Counting IsJumpActive edges straight off the pause screen would therefore
    /// resume a run on a player who is still crossing the room. Three rules stand in the way, and a
    /// confirm needs all three:
    ///
    ///   1. SETTLE. Nothing counts until the player has stood still for SettleSeconds on a FRESH
    ///      calibration. The steering is Reset when the confirm starts and again every time the
    ///      player moves out of the still band, so its baseline ends up where the player now stands
    ///      rather than where they walked in — which is also what makes the hop rule's "above the
    ///      baseline" mean what it says. Someone still walking cannot stay inside a ~5 cm band, or
    ///      keep their box width inside ±12 %, for a second.
    ///   2. LAND. A hop is counted when it comes back DOWN to where the player settled: back near
    ///      the baseline, on the same spot, at the same distance. Walking produces rise, not rise
    ///      and return; a face climbing the frame because it is getting closer never lands.
    ///   3. TWICE, QUICKLY, FROM THE GROUND. Two landed hops whose take-offs are at most
    ///      HopWindowSeconds apart, with the face back on the ground between them — one big hop
    ///      can fire the jump rule twice on its own (see GroundLift), and that is still one hop.
    ///
    /// And any stray — sideways, towards or away from the phone, a crouch, a hop that never lands,
    /// a face lost for more than a blink — throws the whole thing back to SETTLE, recalibration
    /// included. Refusing costs the player one more second of standing still; accepting wrongly
    /// costs them a run.
    ///
    /// This only ever accelerates. RESUME by touch and Android back keep working at every step
    /// (rule 3 — the gesture augments, never gates), so a player whose hops never register loses
    /// nothing.
    ///
    /// Units are FaceSteering's published ones: Deflection in HalfRangeX (1.1 face widths), Lift in
    /// SlideDrop (0.85 face widths), sizes as the smoothed detector box. A face width is ~15 cm, so
    /// every centimetre quoted below is the same centimetre at every playing distance.
    public sealed class DoubleHopConfirm
    {
        /// Hops needed. One is what a player does by accident — landing from the walk back, a
        /// stumble, a sneeze; two in a row is a decision.
        public const int RequiredHops = 2;

        /// The second take-off must come within this long of the first. A deliberate "hop, hop"
        /// is 0.5-1 s apart (FaceSteering's 0.5 s refractory is the floor); two seconds is room for
        /// a slow one, and short enough that two unrelated bounces a while apart are not a pair.
        public const float HopWindowSeconds = 2f;

        /// How long the player must stand still, on a fresh calibration, before a hop counts.
        /// Rule 1 above: the whole walk-in defence rests on this being longer than anyone can hold
        /// the still band while moving. Counted on confident frames only.
        public const float SettleSeconds = 1f;

        /// The still band during SETTLE, measured from the fresh calibration. 0.30 of HalfRangeX is
        /// ~5 cm sideways; 0.50 of SlideDrop is ~6.4 cm up or down — wider, because breathing and
        /// idle sway are mostly vertical (FaceSteeringTests stress it at +-3 cm) and the 12 samples
        /// the baseline averages can land anywhere in a sway; 12 % of the box width is ~25 cm of
        /// distance at 2 m. A walker leaves this band within a fraction of a second: sideways at
        /// once, and towards the phone through the box growing (tens of percent per second).
        public const float StillDeflection = 0.30f;
        public const float StillLift = 0.50f;
        public const float StillSizeRatio = 0.12f;

        /// Once ARMED, how far the player may stray before the confirm starts over. Wider than the
        /// still band, because a real hop drifts a little and blurs a little; vertical travel is
        /// left to the hop rule itself. 0.60 of HalfRangeX is ~10 cm sideways.
        public const float StrayDeflection = 0.60f;
        public const float StraySizeRatio = 0.20f;

        /// A hop has LANDED once the face is back within LandLift of the baseline (~4.5 cm) AND has
        /// come down at least LandDrop (~2 cm) from the highest point since take-off, and that
        /// highest point cleared MinPeakLift (~3.2 cm above standing). The drop is what a face
        /// rising because it is getting closer never shows; the peak is what idle sway after a
        /// stray take-off never reaches.
        public const float LandLift = 0.35f;
        public const float LandDrop = 0.15f;
        public const float MinPeakLift = 0.25f;

        /// A take-off only counts from the ground and into the air: after a landing the face must
        /// have been back down to GroundLift (~2 cm above standing) — the feet on the ground between
        /// two hops — and when the jump rule fires the face must be above it. Both exist because
        /// FaceSteering can fire its jump rule a second time on ONE hop: its arm outlives the 0.5 s
        /// refractory, so the echo lands either on the way down or on a player already standing.
        /// Counted, an echo would be the second hop and one hop would confirm; treated as a real
        /// take-off, it would time out and throw a genuine double hop back to SETTLE.
        public const float GroundLift = 0.15f;

        /// Where a landing must be for the hop to count: on the spot the player settled on
        /// (~7.5 cm sideways) and at the same distance (±8 % of the box width they armed at — ~16 cm
        /// at 2 m). Landing somewhere else is a step, not a hop, and starts the confirm over; the
        /// tight distance band is what catches a player shuffling towards the phone with a heavy
        /// bob, whose box grows a few percent per step.
        public const float LandDeflection = 0.45f;
        public const float LandSizeRatio = 0.08f;

        /// A take-off that has not landed by then was not a hop. A real one is back down in ~0.3 s.
        public const float LandTimeoutSeconds = 1f;

        /// How long the face may be gone (no position at all) before the confirm starts over. Long
        /// enough for the two or three frames motion blur costs the detector in a hop, short enough
        /// that walking out of shot and back is never mistaken for standing still.
        public const float LossGraceSeconds = 0.5f;

        /// The same per-frame clamp FaceSteering and ResumeCountdown put on dt: the first frame back
        /// from another app must not count as a second of standing still.
        const float MaxStepSeconds = 0.1f;

        public enum Phase
        {
            /// Waiting for the player to stand still — "stand still…".
            Settling,

            /// Still enough: counting hops — "hop twice when ready".
            Armed,

            /// Two hops landed. Latches until Restart().
            Confirmed
        }

        readonly FaceSteering _steering;

        public Phase State { get; private set; }

        /// Hops landed in the current window: 0 or 1 while Armed, RequiredHops once Confirmed.
        public int Hops { get; private set; }

        public bool Confirmed => State == Phase.Confirmed;

        /// 0-1 through the settle, for a prompt that wants to show it filling. 1 once Armed.
        public float SettleProgress =>
            State == Phase.Settling ? Math.Min(1f, _still / SettleSeconds) : 1f;

        /// How many times movement or loss has sent the confirm back to SETTLE since Restart().
        /// Diagnostics only (the pause menu logs it), because "why will it not take my hops" is
        /// otherwise invisible from a logcat.
        public int Resettles { get; private set; }

        float _still;
        float _lostFor;
        bool _wasJumping;
        bool _airborne;
        bool _grounded;
        float _airFor;
        float _peakLift;
        float _sinceFirstTakeoff;

        /// The smoothed box width at the moment the confirm armed — the distance the player
        /// settled at, and what every armed size check is measured against. Not the steering's
        /// CalibratedSize: that is captured a dozen samples after the reset, while the size
        /// low-pass (0.33 s) may still be catching up with a player who has only just stopped
        /// walking in, and a stale reference there would fail an honest hop's landing. By the time
        /// the confirm arms, the player has stood still for SettleSeconds and the filter has settled.
        float _armedSize;

        /// Reads — and owns the calibration of — the given steering. The caller keeps feeding it
        /// observations every frame (Submit, via CameraFaceInput); this class Resets it whenever a
        /// settle starts, which is the "stand still" recalibration.
        public DoubleHopConfirm(FaceSteering steering)
        {
            _steering = steering ?? throw new ArgumentNullException(nameof(steering));
            Restart();
        }

        /// Back to SETTLE with a fresh calibration, as if the confirm had just been created.
        public void Restart()
        {
            Resettles = 0;
            BeginSettle();
        }

        /// One frame, called right after the steering has been fed this frame's observation.
        /// Returns Confirmed as it stands afterwards, so the caller can act on the return alone.
        public bool Tick(float dt)
        {
            if (State == Phase.Confirmed) return true;
            if (!(dt > 0f)) dt = 0f; // NaN and negatives contribute nothing
            else if (dt > MaxStepSeconds) dt = MaxStepSeconds;

            bool jumping = _steering.IsJumpActive;
            bool takeoff = jumping && !_wasJumping;
            _wasJumping = jumping;

            if (State == Phase.Settling)
            {
                TickSettling(takeoff, dt);
                return false;
            }

            return TickArmed(takeoff, dt);
        }

        void TickSettling(bool takeoff, float dt)
        {
            FaceSteering s = _steering;

            if (!s.HasPosition)
            {
                // Nothing to judge. A blink holds the timer; a real loss starts over, calibration
                // included, because the player may come back somewhere else.
                _lostFor += dt;
                if (_lostFor > LossGraceSeconds && (_still > 0f || s.IsCalibrated)) Resettle();
                return;
            }
            _lostFor = 0f;

            // FaceSteering is still averaging its first confident samples into the new baseline.
            if (!s.IsCalibrated) return;

            if (takeoff || s.IsJumpActive || s.IsSlideActive ||
                !Within(s, StillDeflection, StillLift, StillSizeRatio, s.CalibratedSize))
            {
                Resettle();
                return;
            }

            // A blurred frame proves nothing either way: hold the timer, do not reset it.
            if (!s.IsTracking) return;

            _still += dt;
            if (_still >= SettleSeconds) Arm();
        }

        bool TickArmed(bool takeoff, float dt)
        {
            FaceSteering s = _steering;
            if (Hops > 0 || _airborne) _sinceFirstTakeoff += dt;

            if (!s.HasPosition)
            {
                // The top of a hop is exactly where the detector blurs out, so a short loss is
                // normal mid-air; a long one is somebody leaving.
                _lostFor += dt;
                if (_airborne) _airFor += dt;
                if (_lostFor > LossGraceSeconds || (_airborne && _airFor > LandTimeoutSeconds))
                    Resettle();
                return false;
            }
            _lostFor = 0f;

            // Wandered off the spot — checked airborne or not. Vertical travel is the hop rule's
            // business; sideways, distance and a crouch are not a hop in any version.
            if (Math.Abs(s.Deflection) > StrayDeflection ||
                !SizeWithin(s, StraySizeRatio, _armedSize) || s.IsSlideActive)
            {
                Resettle();
                return false;
            }

            // A take-off counts only from the ground and into the air. One while already airborne,
            // after a landing but before the face has come back down, or on a face that is not up
            // at all, is the same hop firing the jump rule again (see GroundLift) — ignored, neither
            // counted nor punished. A real staircase of rises still never lands, and
            // LandTimeoutSeconds deals with that.
            if (takeoff && !_airborne && _grounded && s.Lift > GroundLift)
            {
                // A first hop whose window has run out no longer pairs with anything: this take-off
                // opens a new window instead.
                if (Hops > 0 && _sinceFirstTakeoff > HopWindowSeconds) Hops = 0;
                if (Hops == 0) _sinceFirstTakeoff = 0f;

                _airborne = true;
                _airFor = 0f;
                _peakLift = s.Lift;
                return false;
            }

            if (_airborne)
            {
                _airFor += dt;
                if (s.Lift > _peakLift) _peakLift = s.Lift;

                bool down = _peakLift >= MinPeakLift && _peakLift - s.Lift >= LandDrop &&
                            s.Lift <= LandLift && s.Lift >= -LandLift;
                if (down)
                {
                    _airborne = false;
                    _grounded = false; // until the face is all the way back down
                    if (!Within(s, LandDeflection, float.MaxValue, LandSizeRatio, _armedSize))
                    {
                        Resettle(); // came down somewhere else: a step, not a hop
                        return false;
                    }

                    Hops++;
                    if (Hops >= RequiredHops) State = Phase.Confirmed;
                    return Confirmed;
                }

                if (_airFor > LandTimeoutSeconds) Resettle(); // went up and stayed up
                return false;
            }

            if (!_grounded && s.Lift <= GroundLift) _grounded = true;

            // Standing between hops: a lone first hop that has run out of window lapses quietly,
            // so the prompt goes back to asking for two.
            if (Hops > 0 && _sinceFirstTakeoff > HopWindowSeconds) Hops = 0;
            return false;
        }

        void Arm()
        {
            State = Phase.Armed;
            Hops = 0;
            _armedSize = _steering.FaceSize;
            _airborne = false;
            _grounded = true; // it has just stood still for a second
            _airFor = 0f;
            _lostFor = 0f;
            _sinceFirstTakeoff = 0f;
        }

        void Resettle()
        {
            Resettles++;
            BeginSettle();
        }

        /// The recalibration: FaceSteering forgets its neutral, its baseline and its size scale, and
        /// re-learns them from the next confident samples — the player standing where they are now.
        void BeginSettle()
        {
            _steering.Reset();
            State = Phase.Settling;
            Hops = 0;
            _still = 0f;
            _lostFor = 0f;
            _wasJumping = false;
            _airborne = false;
            _grounded = true;
            _airFor = 0f;
            _peakLift = 0f;
            _sinceFirstTakeoff = 0f;
            _armedSize = 0f;
        }

        static bool Within(FaceSteering s, float deflection, float lift, float sizeRatio,
            float referenceSize) =>
            Math.Abs(s.Deflection) <= deflection && Math.Abs(s.Lift) <= lift &&
            SizeWithin(s, sizeRatio, referenceSize);

        /// The smoothed box against a reference width: moving towards or away from the phone.
        static bool SizeWithin(FaceSteering s, float ratio, float referenceSize) =>
            referenceSize > 0f && Math.Abs(s.FaceSize / referenceSize - 1f) <= ratio;
    }
}
