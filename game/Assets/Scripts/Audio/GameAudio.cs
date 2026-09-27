using UnityEngine;

namespace MotionRunner.Audio
{
    /// The one audio component in the game. Built once by GameBootstrap; every call site goes
    /// through the static surface, which no-ops when there is no instance (EditMode tests,
    /// editor tooling), so gameplay and menu code can cue a sound without a null check and
    /// without owning a reference.
    ///
    /// Fail-open, in the store's spirit: a missing clip logs one warning and is skipped, a
    /// missing music folder means silence, and nothing here throws out of a caller. Audio is
    /// decoration; the run must never stop because of it.
    ///
    /// Music is two looping AudioSources that crossfade, so a track change is never a cut. Both
    /// ignore AudioListener.pause and every fade counts in UNSCALED time, because the whole
    /// pause flow happens at Time.timeScale = 0 (RunFlow.ApplyPhase) and the menu music has to
    /// keep breathing under it - which is what the duck is for. A track that is asked for while
    /// EITHER source still holds it RESUMES rather than restarts (MusicDeck decides, per slot):
    /// that is how RUN AGAIN on the same seed gets its song back mid-bar from under the result
    /// loop, and how the menu loop picks up where it left off.
    ///
    /// SFX are a small pool of one-shot voices. Identical cues are capped to one per frame
    /// (three coins in one frame is one coin sound, not a 3x louder one), and a plain UI tap
    /// yields to a stronger UI cue fired in the same frame, so a button that confirms does not
    /// also click.
    public sealed class GameAudio : MonoBehaviour
    {
        public const float DefaultFadeSeconds = 0.8f;
        public const float DuckLevel = 0.35f;
        const float DuckSeconds = 0.4f;
        const float MinFadeSeconds = 0.05f;
        const int SfxVoices = 8;

        public static GameAudio Instance { get; private set; }

        public SoundSettings Settings { get; private set; }

        // ---- music ----
        /// One AudioSource per deck slot; the deck decides, the sources obey.
        readonly AudioSource[] _music = new AudioSource[MusicDeck.Slots];
        readonly MusicDeck _deck = new MusicDeck();
        float _fadeSeconds = DefaultFadeSeconds;
        float _duck = 1f;
        float _duckTarget = 1f;
        readonly System.Collections.Generic.Dictionary<string, AudioClip> _musicClips =
            new System.Collections.Generic.Dictionary<string, AudioClip>();
        readonly System.Collections.Generic.HashSet<string> _missing =
            new System.Collections.Generic.HashSet<string>();
        int _runTrackCount = -1;
        int _cycleCursor;

        // ---- sfx ----
        AudioSource[] _voices;
        int _nextVoice;
        AudioClip[] _sfxClips;
        int[] _lastFrame;

        /// Builds the component. Idempotent: a second call returns the first instance.
        public static GameAudio Create(SoundSettings settings)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("GameAudio");
            var audio = go.AddComponent<GameAudio>();
            audio.Settings = settings ?? new SoundSettings(new PlayerPrefsSoundStore());
            audio.Build();
            Instance = audio;
            return audio;
        }

        void Build()
        {
            for (int i = 0; i < _music.Length; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                source.ignoreListenerPause = true;
                source.spatialBlend = 0f;
                source.volume = 0f;
                source.priority = 0; // never virtualised away by a burst of one-shots
                _music[i] = source;
            }

            _voices = new AudioSource[SfxVoices];
            for (int i = 0; i < SfxVoices; i++)
            {
                var voice = gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.loop = false;
                voice.ignoreListenerPause = true;
                voice.spatialBlend = 0f;
                voice.volume = 1f;
                _voices[i] = voice;
            }

            int count = AudioCatalog.All.Length;
            _sfxClips = new AudioClip[count];
            _lastFrame = new int[count];
            for (int i = 0; i < count; i++) _lastFrame[i] = -1;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---- static surface: what the rest of the game calls ----

        public static void Play(Sfx id, float pitch = 1f, float volumeScale = 1f) =>
            Instance?.PlaySfx(id, pitch, volumeScale);

        public static void Tap() => Play(Sfx.UiTap);
        public static void Back() => Play(Sfx.UiBack);
        public static void Confirm() => Play(Sfx.UiConfirm);
        public static void Deny() => Play(Sfx.UiDeny);

        /// A coin pickup at this combo (ScoreState.Combo AFTER CollectCoin). Pitch rises with the
        /// chain and falls back when ScoreState lets the combo lapse - see CoinPitch.
        public static void Coin(int combo) => Play(Sfx.Coin, CoinPitch.For(combo));

        public static void PlayMenuMusic() => Instance?.PlayMusic(AudioCatalog.MenuMusic);

        /// The run's track, chosen from its seed so the same run always gets the same song.
        public static void PlayRunMusic(uint seedHash) => Instance?.PlayRunTrack(seedHash);

        /// The loop under the result card: the "best" one when the run set an all-time OR daily
        /// best, the other one otherwise. RunHud cues it a beat after the stinger.
        public static void PlayResultMusic(bool newBest) =>
            Instance?.PlayMusic(newBest ? AudioCatalog.ResultBestMusic : AudioCatalog.ResultLostMusic);

        /// Fades the music out and holds it there. Asking for the same track again resumes it.
        public static void StopMusic(float fadeSeconds = DefaultFadeSeconds) =>
            Instance?.FadeOutMusic(fadeSeconds);

        /// Pause ducks the music to DuckLevel; resume brings it back. Unscaled, like the fades.
        public static void SetDucked(bool ducked)
        {
            if (Instance != null) Instance._duckTarget = ducked ? DuckLevel : 1f;
        }

        // ---- music ----

        /// How many `run_NN` tracks are on disk: probed upwards from 1 to the first miss, once.
        public int RunTrackCount
        {
            get
            {
                if (_runTrackCount >= 0) return _runTrackCount;
                int n = 0;
                while (n < AudioCatalog.MaxRunTracks &&
                       Resources.Load<AudioClip>(AudioCatalog.RunTrackPath(n + 1)) != null)
                    n++;
                _runTrackCount = n;
                if (n == 0) Debug.LogWarning("[Audio] no run music found under Resources/" + AudioCatalog.MusicFolder + "run_01 - runs will be silent");
                else Log("run tracks: " + n);
                return n;
            }
        }

        void PlayRunTrack(uint seedHash)
        {
            int count = RunTrackCount;
            int index = MusicPicker.TrackIndex(seedHash, count);
            if (index < 0)
            {
                FadeOutMusic(DefaultFadeSeconds);
                return;
            }
            PlayMusic(AudioCatalog.RunTrackPath(index + 1));
        }

        /// The seedless fallback: round-robin. Nothing in the game today starts a run without a
        /// RunSeed, so this is here for completeness and for the picker's own test.
        public void PlayNextRunTrack()
        {
            int index = MusicPicker.NextCycled(ref _cycleCursor, RunTrackCount);
            if (index < 0) return;
            PlayMusic(AudioCatalog.RunTrackPath(index + 1));
        }

        public void PlayMusic(string path, float fadeSeconds = DefaultFadeSeconds)
        {
            _fadeSeconds = Mathf.Max(MinFadeSeconds, fadeSeconds);

            switch (_deck.Plan(path, out int slot))
            {
                case MusicDeck.Action.Resume:
                {
                    // A slot still holds this track: pick it up where it stopped (RUN AGAIN on
                    // the same seed from under the result loop, or a menu loop that was faded
                    // under a run). Never a restart from the top. The source was Pause()d at
                    // silence by Update, so UnPause continues mid-bar.
                    var source = _music[slot];
                    if (source.clip != null && !source.isPlaying)
                    {
                        source.UnPause();
                        if (!source.isPlaying) source.Play();
                    }
                    _deck.Resume(slot);
                    Log("music resume " + path);
                    return;
                }
                case MusicDeck.Action.Start:
                {
                    var clip = LoadMusic(path);
                    if (clip == null)
                    {
                        // Whatever was playing belongs to the previous screen; silence beats the
                        // wrong song. The deck is untouched, so the parked track stays resumable.
                        FadeOutMusic(fadeSeconds);
                        return;
                    }

                    var incoming = _music[slot];
                    incoming.Stop();
                    incoming.clip = clip;
                    incoming.volume = 0f;
                    incoming.Play();
                    _deck.Start(slot, path);
                    Log("music start " + path);
                    return;
                }
            }
        }

        void FadeOutMusic(float fadeSeconds)
        {
            _fadeSeconds = Mathf.Max(MinFadeSeconds, fadeSeconds);
            string path = _deck.ActivePath;
            if (_deck.FadeOut()) Log("music stop " + path);
        }

        AudioClip LoadMusic(string path)
        {
            if (_musicClips.TryGetValue(path, out var cached)) return cached;
            if (_missing.Contains(path)) return null;
            var clip = Resources.Load<AudioClip>(path);
            if (clip == null)
            {
                _missing.Add(path);
                Debug.LogWarning("[Audio] missing music clip Resources/" + path + " - skipped");
                return null;
            }
            _musicClips[path] = clip;
            return clip;
        }

        void Update()
        {
            // Unscaled and clamped, like ResumeCountdown: the game is frozen at timeScale 0 for
            // the whole pause flow, and the first frame back from the background can be enormous.
            float dt = Time.unscaledDeltaTime;
            if (dt > 0.1f) dt = 0.1f;

            _duck = Mathf.MoveTowards(_duck, _duckTarget, dt / DuckSeconds);
            float music = Settings.EffectiveMusic;

            _deck.Tick(dt, _fadeSeconds);
            for (int i = 0; i < _music.Length; i++)
            {
                var source = _music[i];
                if (source.clip == null) continue;
                source.volume = _deck.LevelOf(i) * _duck * music;

                // Silent AND meant to be: stop decoding. Pause, not Stop, so a resume picks up
                // mid-track.
                if (_deck.IsParked(i) && source.isPlaying) source.Pause();
            }
        }

        // ---- sfx ----

        void PlaySfx(Sfx id, float pitch, float volumeScale)
        {
            int index = (int)id;
            if (index < 0 || index >= _sfxClips.Length) return;

            float level = Settings.EffectiveSfx * Mathf.Clamp01(volumeScale);
            if (level <= 0f) return;

            int frame = Time.frameCount;
            if (_lastFrame[index] == frame) return; // one of each per frame, never a stack
            if (id == Sfx.UiTap && StrongerUiCueThisFrame(frame)) return;

            var clip = LoadSfx(id);
            if (clip == null) return;

            _lastFrame[index] = frame;
            var voice = PickVoice();
            voice.pitch = pitch > 0f ? pitch : 1f;
            voice.PlayOneShot(clip, level);
            Log("play " + id + (pitch != 1f ? " pitch=" + pitch.ToString("0.00") : string.Empty));
        }

        /// A confirm, deny or back fired this frame already speaks for the tap that caused it.
        bool StrongerUiCueThisFrame(int frame) =>
            _lastFrame[(int)Sfx.UiConfirm] == frame ||
            _lastFrame[(int)Sfx.UiDeny] == frame ||
            _lastFrame[(int)Sfx.UiBack] == frame;

        /// An idle voice if there is one, else round-robin: a ninth simultaneous one-shot layers
        /// onto the oldest voice rather than being dropped.
        AudioSource PickVoice()
        {
            for (int i = 0; i < _voices.Length; i++)
            {
                int candidate = (_nextVoice + i) % _voices.Length;
                if (!_voices[candidate].isPlaying)
                {
                    _nextVoice = (candidate + 1) % _voices.Length;
                    return _voices[candidate];
                }
            }
            var voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            return voice;
        }

        AudioClip LoadSfx(Sfx id)
        {
            int index = (int)id;
            if (_sfxClips[index] != null) return _sfxClips[index];
            string path = AudioCatalog.Path(id);
            if (path == null || _missing.Contains(path)) return null;
            var clip = Resources.Load<AudioClip>(path);
            if (clip == null)
            {
                _missing.Add(path);
                Debug.LogWarning("[Audio] missing clip Resources/" + path + " (" + id + ") - skipped");
                return null;
            }
            _sfxClips[index] = clip;
            return clip;
        }

        /// Development builds only (CLAUDE.md gotcha #8: this reaches logcat under tag Unity),
        /// so a device run proves which hooks fired without anyone having to hear the phone.
        static void Log(string message)
        {
            if (Debug.isDebugBuild) Debug.Log("[Audio] " + message);
        }
    }
}
