using UnityEngine;

namespace MotionRunner.Art
{
    /// Only the curated assets referenced here are shipped, not the whole downloaded packs.
    public sealed class ParkAssets : ScriptableObject
    {
        public GameObject Runner;
        public Material RunnerMaterial;
        public AnimationClip Run;
        public AnimationClip Jump;
        public AnimationClip Idle;
        public AnimationClip DodgeLeft;
        public AnimationClip DodgeRight;

        /// Crash and result poses (Quaternius Universal Animation Library, CC0). Wired by
        /// RunnerCrashBuild; the world plays CrashWall/CrashTrip when the run ends, the result
        /// card plays Celebrate (new record) or Defeat.
        public AnimationClip CrashWall;
        public AnimationClip CrashTrip;
        public AnimationClip Celebrate;
        public AnimationClip Defeat;
        public Material Sky;
        public GameObject[] Props;
        public TrackChunkAsset[] Chunks;

        static ParkAssets _instance;
        public static ParkAssets Load() => _instance != null ? _instance :
            _instance = Resources.Load<ParkAssets>("Art/Park");
    }
}
