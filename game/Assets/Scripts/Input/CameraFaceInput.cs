using MotionRunner.CameraInput;
using MotionRunner.Core;
using MotionRunner.Pose;
using UnityEngine;

namespace MotionRunner.Inputs
{
    /// T-013: the camera as an IGameInput. Polls the rig's latest face observation once per
    /// frame, runs it through the engine-free FaceSteering rules, and exposes exactly the four
    /// signals gameplay understands. Gameplay code never sees the camera, the model, or a
    /// coordinate — only this adapter (CLAUDE.md rule 2).
    public sealed class CameraFaceInput : IGameInput
    {
        /// An observation older than this is a lost face: the camera stalled, the player left
        /// frame, or the app was backgrounded.
        ///
        /// Forcing the score to 0 puts it under FaceSteering.MinPositionScore, so a stale
        /// observation is handled by exactly the same code path as a face the detector could not
        /// find — which since 31 Aug means the lane is HELD rather than given back (see the loss
        /// contract on FaceSteering). A stalled camera therefore keeps steering the runner into
        /// whatever lane it last saw, on purpose: the owner's call is that holding a lane is always
        /// better than walking into the middle one, and nothing can fire while it holds.
        const float StaleSeconds = 0.35f;

        readonly FaceTrackingRig _rig;
        public FaceSteering Steering { get; } = new FaceSteering();

        public CameraFaceInput(FaceTrackingRig rig) => _rig = rig;

        public bool IsTracking => Steering.IsTracking;

        /// Unscaled time, not Time.deltaTime. RunSession keeps ticking input while the pause menu
        /// is up, and pause means Time.timeScale = 0, so scaled deltaTime is zero there: every
        /// FaceSteering timer would stall and every clamped dt would report the player's head
        /// teleporting. Camera mode re-acquires the face *during* that frozen resume wait, which
        /// is exactly when it matters. Outside a pause the two are the same number.
        ///
        /// The same observation is resubmitted on frames between detector completions — the rig
        /// finishes one every second or third frame. That is harmless by construction: the
        /// gesture rules measure travel across a wall-clock window, so a repeated sample adds
        /// nothing and a missing one costs nothing.
        public void Tick()
        {
            float dt = Time.unscaledDeltaTime;

            if (_rig == null || _rig.State != FaceTrackingRig.RigState.Tracking)
            {
                Steering.Submit(0f, 0f, 0f, 0f, dt); // score 0 -> loss: freeze the axis, fire nothing
                return;
            }

            FaceObservation obs = _rig.Latest;
            float score = _rig.LatestAgeSeconds > StaleSeconds ? 0f : obs.Score;

            // The frame's shape comes from the pipeline rather than being assumed here: the
            // detector measured it on the texture it actually ran on. FaceSteering needs it to
            // express a vertical displacement (normalized to frame height) in the same face-width
            // unit as a horizontal one (normalized to frame width).
            if (obs.FrameAspect > 0f) Steering.FrameAspect = obs.FrameAspect;

            // Size is the face box width, and it is what makes the gesture thresholds physical
            // rather than a function of how far away the player happens to be standing.
            Steering.Submit(obs.X, obs.Y, obs.Size, score, dt);
        }

        public float GetMoveAxis() => Steering.MoveAxis;
        public bool IsJumpPressed() => Steering.IsJumpActive;
        public bool IsSlidePressed() => Steering.IsSlideActive;
        public bool IsSpecialPressed() => false;
    }
}
