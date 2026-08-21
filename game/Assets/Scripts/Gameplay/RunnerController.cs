using MotionRunner.Core;
using MotionRunner.Track;
using UnityEngine;

namespace MotionRunner.Gameplay
{
    /// Steers the runner across three lanes and handles a simple ballistic jump.
    /// The runner never moves forward - the world scrolls past it (handoff 3.1).
    ///
    /// Driven by RunSession rather than its own Update, so one frame is always
    /// input -> runner -> world -> collision -> HUD, in that order.
    public sealed class RunnerController : MonoBehaviour
    {
        public const float LaneWidth = TrackMetrics.LaneWidth;

        // Tuned on device for T-002 ("feels fine for now"); do not change casually.
        const float SteerSpeed = 7f;       // lateral units/sec at full deflection
        const float MaxX = LaneWidth;      // edges of the 3-lane road
        const float JumpVelocity = 7.5f;
        const float Gravity = 22f;         // heavier than earth = snappier arc

        float _verticalVelocity;
        bool _airborne;

        public bool Airborne => _airborne;

        /// Collision box in world space (the runner's z is always 0).
        public Aabb Bounds => TrackGeometry.Runner(transform.position.x, transform.position.y);

        public void ResetState()
        {
            _verticalVelocity = 0f;
            _airborne = false;
            transform.position = new Vector3(0f, TrackMetrics.RunnerRestY, 0f);
        }

        public void Step(float deltaTime, IGameInput input)
        {
            if (input == null) return;

            // Analog steering: tilt maps to lateral velocity, clamped to road edges.
            float x = transform.position.x + input.GetMoveAxis() * SteerSpeed * deltaTime;
            x = Mathf.Clamp(x, -MaxX, MaxX);

            float y = transform.position.y;
            if (input.IsJumpPressed() && !_airborne)
            {
                _verticalVelocity = JumpVelocity;
                _airborne = true;
            }

            if (_airborne)
            {
                _verticalVelocity -= Gravity * deltaTime;
                y += _verticalVelocity * deltaTime;
                if (y <= TrackMetrics.RunnerRestY)
                {
                    y = TrackMetrics.RunnerRestY;
                    _airborne = false;
                    _verticalVelocity = 0f;
                }
            }

            transform.position = new Vector3(x, y, 0f);
        }
    }
}
