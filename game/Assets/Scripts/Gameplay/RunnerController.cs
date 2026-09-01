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
    ///
    /// Steering is discrete (owner call, 30 Aug): the track has three lanes, so the axis picks
    /// *which* lane the runner is in and the runner animates to that lane's centre, rather than
    /// the axis driving a lateral velocity that could leave it parked between two of them. All of
    /// that decision - the zones, the hysteresis at their boundaries, and the slide itself - lives
    /// in LaneSelector, which is engine-free and unit tested. This class only lifts the result
    /// onto the transform and owns the jump arc.
    public sealed class RunnerController : MonoBehaviour
    {
        public const float LaneWidth = TrackMetrics.LaneWidth;

        // Tuned on device for T-002 ("feels fine for now"); do not change casually. In particular
        // the arc is airborne 2 * 7.5 / 22 = 0.682 s, and FaceSteering.JumpRefractorySeconds (0.5)
        // is deliberately set below that - retuning these two numbers silently retunes the camera
        // jump gesture with them.
        const float JumpVelocity = 7.5f;
        const float Gravity = 22f;         // heavier than earth = snappier arc

        readonly LaneSelector _lanes = new LaneSelector();

        float _verticalVelocity;
        bool _airborne;

        public bool Airborne => _airborne;

        /// -1, 0 or +1: the lane the runner is committed to - where it is, or where it is sliding.
        public int Lane => _lanes.Lane;

        /// True while the slide between two lanes is still running.
        public bool ChangingLane => _lanes.IsSliding;

        /// Collision box in world space (the runner's z is always 0).
        ///
        /// Deliberately built from the transform, not from the lane index: mid-slide the runner is
        /// genuinely between two lanes and must be hit by anything it is genuinely overlapping.
        /// Committing to a lane is not a teleport out of the one being left.
        public Aabb Bounds => TrackGeometry.Runner(transform.position.x, transform.position.y);

        public void ResetState()
        {
            _verticalVelocity = 0f;
            _airborne = false;
            _lanes.Reset();
            transform.position = new Vector3(_lanes.X, TrackMetrics.RunnerRestY, 0f);
        }

        public void Step(float deltaTime, IGameInput input)
        {
            if (input == null) return;

            // Lane steering: the axis names a lane, the slide carries the runner to its centre.
            _lanes.Step(input.GetMoveAxis(), deltaTime);
            float x = _lanes.X;

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
