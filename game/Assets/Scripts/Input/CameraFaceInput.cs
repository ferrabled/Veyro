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
        /// frame, or the app was backgrounded. Steering must decay, not hold the last lean.
        const float StaleSeconds = 0.35f;

        readonly FaceTrackingRig _rig;
        public FaceSteering Steering { get; } = new FaceSteering();

        public CameraFaceInput(FaceTrackingRig rig) => _rig = rig;

        public bool IsTracking => Steering.IsTracking;

        public void Tick()
        {
            if (_rig == null || _rig.State != FaceTrackingRig.RigState.Tracking)
            {
                Steering.Submit(0f, 0f, 0f, Time.deltaTime); // score 0 -> decay to neutral
                return;
            }

            FaceObservation obs = _rig.Latest;
            float score = _rig.LatestAgeSeconds > StaleSeconds ? 0f : obs.Score;
            Steering.Submit(obs.X, obs.Y, score, Time.deltaTime);
        }

        public float GetMoveAxis() => Steering.MoveAxis;
        public bool IsJumpPressed() => Steering.IsJumpActive;
        public bool IsSlidePressed() => Steering.IsSlideActive;
        public bool IsSpecialPressed() => false;
    }
}
