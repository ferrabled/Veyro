using System.Collections.Generic;

namespace MotionRunner.Core
{
    /// Merges several adapters (gyro + touch + keyboard) so any of them can act.
    public sealed class CompositeInput : IGameInput
    {
        readonly List<IGameInput> _sources = new();

        public CompositeInput(params IGameInput[] sources) => _sources.AddRange(sources);

        public void Tick()
        {
            foreach (var s in _sources) s.Tick();
        }

        public float GetMoveAxis()
        {
            float best = 0f;
            foreach (var s in _sources)
            {
                float a = s.GetMoveAxis();
                if (UnityEngine.Mathf.Abs(a) > UnityEngine.Mathf.Abs(best)) best = a;
            }
            return best;
        }

        public bool IsJumpPressed()
        {
            foreach (var s in _sources) if (s.IsJumpPressed()) return true;
            return false;
        }

        public bool IsSlidePressed()
        {
            foreach (var s in _sources) if (s.IsSlidePressed()) return true;
            return false;
        }

        public bool IsSpecialPressed()
        {
            foreach (var s in _sources) if (s.IsSpecialPressed()) return true;
            return false;
        }
    }
}
