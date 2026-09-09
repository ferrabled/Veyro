using MotionRunner.Core;
using MotionRunner.Track;

namespace MotionRunner.Menu
{
    /// The attract run's hands: an IGameInput whose axis and jump come from AutoPilot instead of
    /// from a sensor.
    ///
    /// It goes through IGameInput rather than driving the runner directly because that is the rule
    /// (CLAUDE.md rule 2: gameplay reads nothing but this interface) and because it is what makes
    /// the demo the real game - RunnerController and LaneSelector cannot tell this apart from a
    /// player, so the menu shows the same slide timing, the same jump arc and the same three lanes
    /// the run will.
    ///
    /// The axis is the LANE, not a nudge: LaneSelector.LaneFor maps -1/0/+1 straight onto the lane
    /// of the same index, so the pilot names a lane and the selector animates to it.
    public sealed class AttractInput : IGameInput
    {
        readonly ChunkClusters _clusters;

        int _lane;
        bool _jump;

        public AttractInput(ChunkClusters clusters) => _clusters = clusters;

        /// Recomputes the decision from the track as it stands. Called by AttractRun once a frame,
        /// before the runner steps, which is the same order RunSession uses.
        public void Decide(int currentLane)
        {
            var ahead = _clusters.Refresh();
            _lane = AutoPilot.ChooseLane(ahead, currentLane);
            _jump = AutoPilot.ShouldJump(ahead, _lane);
        }

        public float GetMoveAxis() => _lane;

        public bool IsJumpPressed() => _jump;

        /// The demo never slides and never uses the special: neither exists as a mechanic yet, and
        /// a menu is the wrong place to show off something the run cannot do.
        public bool IsSlidePressed() => false;

        public bool IsSpecialPressed() => false;

        public void Tick()
        {
        }
    }
}
