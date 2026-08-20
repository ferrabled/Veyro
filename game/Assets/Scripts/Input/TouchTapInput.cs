using MotionRunner.Core;
using UnityEngine;

namespace MotionRunner.Inputs
{
    /// Tap anywhere = jump; swipe down = slide. Steering is left to gyro/keyboard.
    public sealed class TouchTapInput : IGameInput
    {
        const float SwipeMinPixelsPerInch = 0.2f; // fraction of screen height

        bool _jump;
        bool _slide;
        Vector2 _downPos;
        bool _tracking;

        public void Tick()
        {
            _jump = false;
            _slide = false;

            for (int i = 0; i < UnityEngine.Input.touchCount; i++)
            {
                Touch t = UnityEngine.Input.GetTouch(i);
                switch (t.phase)
                {
                    case TouchPhase.Began:
                        _downPos = t.position;
                        _tracking = true;
                        break;
                    case TouchPhase.Ended:
                        if (!_tracking) break;
                        _tracking = false;
                        float dy = t.position.y - _downPos.y;
                        if (dy < -Screen.height * SwipeMinPixelsPerInch) _slide = true;
                        else _jump = true;
                        break;
                }
            }
        }

        public float GetMoveAxis() => 0f;
        public bool IsJumpPressed() => _jump;
        public bool IsSlidePressed() => _slide;
        public bool IsSpecialPressed() => false;
    }
}
