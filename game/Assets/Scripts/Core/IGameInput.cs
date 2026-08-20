namespace MotionRunner.Core
{
    /// Gameplay-facing input contract. Gameplay code must never read sensors,
    /// touches, or ML output directly — only this interface (CLAUDE.md rule 2).
    public interface IGameInput
    {
        /// Horizontal steering in [-1, 1]. Negative = left.
        float GetMoveAxis();

        bool IsJumpPressed();
        bool IsSlidePressed();
        bool IsSpecialPressed();

        /// Called once per frame by the runner before reads.
        void Tick();
    }
}
