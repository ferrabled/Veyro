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
    /// Handoff 8.3: geometry and temporal thresholds, no classifier. The neutral pose is
    /// calibrated from the first confident samples and then drifts slowly toward wherever the
    /// player actually stands, so nobody has to hold a military posture for a whole run.
    public sealed class FaceSteering
    {
        // ---- tunables (defaults are first guesses; T-012's device pass tunes them) ----

        /// Below this detector score the face is noise: steering decays to neutral and no
        /// discrete gesture can fire.
        public float MinScore = 0.6f;

        /// Confident samples averaged into the initial neutral pose.
        public int CalibrationSamples = 12;

        /// Horizontal offset from neutral (normalized) that maps to full deflection.
        public float HalfRangeX = 0.14f;

        /// Fraction of full deflection ignored around neutral, then rescaled so the usable
        /// range still reaches ±1.
        public float DeadZone = 0.15f;

        /// Axis low-pass sharpness; higher = snappier, noisier. Same shape as GyroTiltInput's.
        public float AxisSharpness = 14f;

        /// Upward speed (normalized units/sec, y decreasing) that reads as a jump.
        public float JumpVelocity = 0.75f;

        /// How long IsJumpActive stays true once triggered — long enough that a 60 Hz poll
        /// cannot miss it, short enough that it cannot double-fire after landing.
        public float JumpWindowSeconds = 0.18f;

        /// Minimum time between two jumps. Absorbs the head bobbing back up.
        public float JumpRefractorySeconds = 0.7f;

        /// How far below the calibrated baseline (y increasing) the face must sit to be a crouch.
        public float SlideDrop = 0.09f;

        /// How long the drop must be sustained before SLIDE fires — a fast bob is not a crouch.
        public float SlideHoldSeconds = 0.10f;

        /// Standing up from a crouch is fast upward motion — exactly a jump's signature. Ending a
        /// slide therefore arms the jump refractory, so the stand-up cannot double as a jump.
        public float SlideReleaseRefractorySeconds = 0.45f;

        /// Neutral drift-correction rate (per second). Slow on purpose: it should absorb the
        /// player shuffling sideways over a minute, not eat a deliberate lean.
        public float RecenterSharpness = 0.25f;

        /// How fast the axis returns to neutral when the face is lost.
        public float LossDecaySharpness = 6f;

        // ---- outputs ----

        public float MoveAxis { get; private set; }
        public bool IsJumpActive => _jumpTimer > 0f;
        public bool IsSlideActive { get; private set; }
        public bool IsTracking { get; private set; }
        public bool IsCalibrated { get; private set; }

        /// Where "straight ahead" currently is — exposed for debug overlays and tuning.
        public float NeutralX { get; private set; }
        public float BaselineY { get; private set; }

        float _calibSumX;
        float _calibSumY;
        int _calibCount;
        float _prevY;
        bool _hasPrev;
        float _jumpTimer;
        float _refractory;
        float _slideHeld;

        public void Reset()
        {
            MoveAxis = 0f;
            IsSlideActive = false;
            IsTracking = false;
            IsCalibrated = false;
            NeutralX = 0f;
            BaselineY = 0f;
            _calibSumX = 0f;
            _calibSumY = 0f;
            _calibCount = 0;
            _hasPrev = false;
            _jumpTimer = 0f;
            _refractory = 0f;
            _slideHeld = 0f;
        }

        /// One face observation. x/y normalized [0,1] (origin top-left, y down), score the
        /// detector's confidence, dt the seconds since the previous Submit.
        public void Submit(float x, float y, float score, float dt)
        {
            if (dt < 1e-4f) dt = 1e-4f;
            if (dt > 0.1f) dt = 0.1f; // a hitch must not read as instantaneous head teleport

            if (_jumpTimer > 0f) _jumpTimer -= dt;
            if (_refractory > 0f) _refractory -= dt;

            if (score < MinScore)
            {
                IsTracking = false;
                IsSlideActive = false;
                _hasPrev = false;
                _slideHeld = 0f;
                MoveAxis -= MoveAxis * Math.Min(1f, LossDecaySharpness * dt);
                return;
            }

            IsTracking = true;

            if (!IsCalibrated)
            {
                _calibSumX += x;
                _calibSumY += y;
                _calibCount++;
                if (_calibCount >= CalibrationSamples)
                {
                    NeutralX = _calibSumX / _calibCount;
                    BaselineY = _calibSumY / _calibCount;
                    IsCalibrated = true;
                }
                _prevY = y;
                _hasPrev = true;
                return;
            }

            float vy = _hasPrev ? (y - _prevY) / dt : 0f;
            _prevY = y;
            _hasPrev = true;

            // ---- LEFT / RIGHT ----
            float raw = Clamp((x - NeutralX) / HalfRangeX, -1f, 1f);
            float magnitude = Math.Abs(raw);
            raw = magnitude < DeadZone
                ? 0f
                : Math.Sign(raw) * (magnitude - DeadZone) / (1f - DeadZone);
            float t = 1f - (float)Math.Exp(-AxisSharpness * dt);
            MoveAxis += (raw - MoveAxis) * t;

            // ---- JUMP: sharp upward motion, rate-limited ----
            if (vy < -JumpVelocity && _refractory <= 0f && !IsSlideActive)
            {
                _jumpTimer = JumpWindowSeconds;
                _refractory = JumpRefractorySeconds;
            }

            // ---- SLIDE: sustained crouch below baseline ----
            if (y - BaselineY > SlideDrop)
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

            // ---- slow drift correction ----
            // x recenters only while the player is not deliberately leaning; y only while they
            // are not crouching or jumping, or the baseline would chase the gesture it detects.
            float rt = 1f - (float)Math.Exp(-RecenterSharpness * dt);
            if (Math.Abs(MoveAxis) < 0.25f) NeutralX += (x - NeutralX) * rt;
            if (!IsSlideActive && _jumpTimer <= 0f && Math.Abs(y - BaselineY) < SlideDrop * 0.5f)
                BaselineY += (y - BaselineY) * rt;
        }

        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
