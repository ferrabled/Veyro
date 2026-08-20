using MotionRunner.Core;
using UnityEngine;

namespace MotionRunner.Inputs
{
    /// Tilt steering from the accelerometer's gravity vector.
    /// Neutral pose is calibrated at construction (players don't hold phones flat),
    /// then tilt is the low-pass-filtered delta from that neutral.
    public sealed class GyroTiltInput : IGameInput
    {
        // Degrees of tilt (approx, via g-fraction) that maps to full deflection.
        const float FullDeflection = 0.35f;   // ~20° of roll
        const float DeadZone = 0.04f;
        const float FilterSharpness = 12f;    // higher = snappier, noisier

        float _neutralX;
        float _filtered;
        bool _calibrated;

        public void Tick()
        {
            if (!SystemInfo.supportsAccelerometer) return;

            float raw = UnityEngine.Input.acceleration.x;
            if (!_calibrated)
            {
                _neutralX = raw;
                _filtered = 0f;
                _calibrated = true;
                return;
            }

            float delta = raw - _neutralX;
            float t = 1f - Mathf.Exp(-FilterSharpness * Time.deltaTime);
            _filtered = Mathf.Lerp(_filtered, delta, t);
        }

        public void Recalibrate() => _calibrated = false;

        public float GetMoveAxis()
        {
            if (!SystemInfo.supportsAccelerometer) return 0f;
            float v = _filtered;
            if (Mathf.Abs(v) < DeadZone) return 0f;
            return Mathf.Clamp(v / FullDeflection, -1f, 1f);
        }

        public bool IsJumpPressed() => false; // jump comes from TouchTapInput in MVP
        public bool IsSlidePressed() => false;
        public bool IsSpecialPressed() => false;
    }
}
