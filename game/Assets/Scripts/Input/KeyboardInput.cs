using MotionRunner.Core;
using UnityEngine;

namespace MotionRunner.Inputs
{
    /// Editor/desktop testing only: arrows or A/D steer, space jumps, S/down slides.
    public sealed class KeyboardInput : IGameInput
    {
        public void Tick() { }

        public float GetMoveAxis()
        {
            float axis = 0f;
            if (UnityEngine.Input.GetKey(KeyCode.LeftArrow) || UnityEngine.Input.GetKey(KeyCode.A)) axis -= 1f;
            if (UnityEngine.Input.GetKey(KeyCode.RightArrow) || UnityEngine.Input.GetKey(KeyCode.D)) axis += 1f;
            return axis;
        }

        public bool IsJumpPressed() =>
            UnityEngine.Input.GetKeyDown(KeyCode.Space) || UnityEngine.Input.GetKeyDown(KeyCode.UpArrow);

        public bool IsSlidePressed() =>
            UnityEngine.Input.GetKeyDown(KeyCode.S) || UnityEngine.Input.GetKeyDown(KeyCode.DownArrow);

        public bool IsSpecialPressed() => false;
    }
}
