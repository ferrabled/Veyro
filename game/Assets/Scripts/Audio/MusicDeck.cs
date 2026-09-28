namespace MotionRunner.Audio
{
    /// The two-slot music deck, engine-free: which path each slot holds, which slot is active,
    /// where each slot's fade is, and - the decision that was easy to get wrong - whether a
    /// requested track is RESUMED or STARTED.
    ///
    /// GameAudio owns the two AudioSources and does what this class decides. Nothing here knows
    /// what a clip is; a slot is a path and a fade level, so the whole start-versus-resume
    /// contract can be exercised in EditMode without a listener.
    ///
    /// ---- the resume rule -------------------------------------------------------------------
    ///
    /// A track that is asked for while a slot still holds it resumes from where it stopped -
    /// the slot's source was paused at silence, never stopped - and crossfades back in. That
    /// used to be checked against the ACTIVE slot only, which was enough while a crash merely
    /// held the run track. With a result loop under the card the run track is now the OTHER,
    /// parked slot when RUN AGAIN asks for it, and the old rule would have restarted it from the
    /// top. So the rule is per slot: run_04 parked on slot 0 under result_lost on slot 1 resumes
    /// on slot 0, and result_lost fades out on 1. A new track always takes the slot that is not
    /// active, replacing whatever was parked there.
    public sealed class MusicDeck
    {
        public const int Slots = 2;

        /// What GameAudio must do for a requested path.
        public enum Action
        {
            /// Empty path: nothing to play.
            None,
            /// The slot already holds this path: un-pause it and fade it back in.
            Resume,
            /// Load the path into the slot (the one that is not active) and start it from the top.
            Start
        }

        readonly string[] _paths = new string[Slots];
        readonly float[] _level = new float[Slots];
        readonly float[] _target = new float[Slots];
        int _active;

        /// The slot the last request landed on. Its path is what FadeOut fades.
        public int Active => _active;

        public string ActivePath => _paths[_active];

        public string PathOf(int slot) => _paths[slot];

        /// 0..1 fade position of a slot, before the duck and the user's volume are applied.
        public float LevelOf(int slot) => _level[slot];

        public float TargetOf(int slot) => _target[slot];

        /// Silent AND meant to be: the source can stop decoding. Pause, not Stop, so a later
        /// Resume picks up mid-track.
        public bool IsParked(int slot) => _level[slot] <= 0f && _target[slot] <= 0f;

        /// The decision, without acting on it: GameAudio still has to load a clip for a Start,
        /// and a missing clip must leave the deck exactly as it was (see FadeOut, which is what
        /// it does instead).
        public Action Plan(string path, out int slot)
        {
            slot = _active;
            if (string.IsNullOrEmpty(path)) return Action.None;

            for (int i = 0; i < Slots; i++)
            {
                if (_paths[i] != path) continue;
                slot = i;
                return Action.Resume;
            }

            slot = 1 - _active;
            return Action.Start;
        }

        /// Commits a Resume: the slot fades back to full and becomes active; the other fades out.
        public void Resume(int slot)
        {
            _target[slot] = 1f;
            _target[1 - slot] = 0f;
            _active = slot;
        }

        /// Commits a Start: the slot now holds this path from silence, fading up; the other
        /// fades out.
        public void Start(int slot, string path)
        {
            _paths[slot] = path;
            _level[slot] = 0f;
            _target[slot] = 1f;
            _target[1 - slot] = 0f;
            _active = slot;
        }

        /// Fades the active slot out and holds it there (parked, resumable). Returns false when
        /// there was nothing to fade.
        public bool FadeOut()
        {
            if (_paths[_active] == null) return false;
            _target[_active] = 0f;
            return true;
        }

        /// One frame of fading, in whatever clock the caller keeps (GameAudio: unscaled, clamped).
        public void Tick(float deltaTime, float fadeSeconds)
        {
            float step = fadeSeconds > 0f ? deltaTime / fadeSeconds : 1f;
            for (int i = 0; i < Slots; i++)
            {
                if (_paths[i] == null) continue;
                float level = _level[i];
                float target = _target[i];
                if (level < target) level = level + step > target ? target : level + step;
                else if (level > target) level = level - step < target ? target : level - step;
                _level[i] = level;
            }
        }
    }
}
