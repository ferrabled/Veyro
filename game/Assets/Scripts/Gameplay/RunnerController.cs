using MotionRunner.Core;
using UnityEngine;

namespace MotionRunner.Gameplay
{
    /// Steers the runner across three lanes and handles a simple ballistic jump.
    /// The runner never moves forward — the world scrolls past it (handoff §3.1).
    public sealed class RunnerController : MonoBehaviour
    {
        public const float LaneWidth = 1.6f;

        const float SteerSpeed = 7f;       // lateral units/sec at full deflection
        const float MaxX = LaneWidth;      // edges of the 3-lane road
        const float JumpVelocity = 7.5f;
        const float Gravity = 22f;         // heavier than earth = snappier arc

        public IGameInput Input;

        float _verticalVelocity;
        bool _airborne;

        void Update()
        {
            if (Input == null) return;
            Input.Tick();

            // Analog steering: tilt maps to lateral velocity, clamped to road edges.
            float x = transform.position.x + Input.GetMoveAxis() * SteerSpeed * Time.deltaTime;
            x = Mathf.Clamp(x, -MaxX, MaxX);

            float y = transform.position.y;
            if (Input.IsJumpPressed() && !_airborne)
            {
                _verticalVelocity = JumpVelocity;
                _airborne = true;
            }

            if (_airborne)
            {
                _verticalVelocity -= Gravity * Time.deltaTime;
                y += _verticalVelocity * Time.deltaTime;
                if (y <= 0.5f) // capsule rest height
                {
                    y = 0.5f;
                    _airborne = false;
                    _verticalVelocity = 0f;
                }
            }

            transform.position = new Vector3(x, y, 0f);
        }
    }
}
