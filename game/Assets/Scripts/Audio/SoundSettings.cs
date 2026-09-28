using System;

namespace MotionRunner.Audio
{
    /// The persistence seam under SoundSettings - PlayerPrefs on device, an in-memory fake in
    /// tests (the PlayerPrefsScoreStore / IScoreStore shape). Save() is part of this seam,
    /// unlike IScoreStore's, because a slider release is the natural batch boundary and the
    /// panel should not need to know PlayerPrefs exists to flush one.
    public interface ISoundSettingsStore
    {
        float GetFloat(string key, float fallback);
        void SetFloat(string key, float value);
        int GetInt(string key, int fallback);
        void SetInt(string key, int value);
        void Save();
    }

    /// Music and sound-effect levels, engine-free. Named SoundSettings rather than AudioSettings
    /// because UnityEngine.AudioSettings already exists and a `using UnityEngine` would make every
    /// reference to ours ambiguous.
    ///
    /// Two numbers and two mutes. The sliders store what the player SET (0..1, linear); what
    /// reaches an AudioSource is Perceptual(level) - squared - so the slider's midpoint sounds
    /// like half as loud rather than like 80% (loudness is roughly logarithmic in amplitude, and
    /// x^2 is the cheap curve that tracks it well enough across the range a slider covers). The
    /// mutes exist for the pause menu's one-tap toggles: a mute must not destroy the level the
    /// player dialled in, so it is a separate flag, and moving a slider clears it.
    ///
    /// Setters change memory only; Save() writes. A slider drags through dozens of values per
    /// second and PlayerPrefs.Save is a synchronous disk write on Android - the panel saves on
    /// release, the pause toggles save on tap.
    public sealed class SoundSettings
    {
        public const string MusicKey = "veyro.audio.music";
        public const string SfxKey = "veyro.audio.sfx";
        public const string MusicMutedKey = "veyro.audio.music.muted";
        public const string SfxMutedKey = "veyro.audio.sfx.muted";

        public const float DefaultMusic = 0.8f;
        public const float DefaultSfx = 1.0f;

        readonly ISoundSettingsStore _store;

        /// Raised on any change, so a screen showing the levels can repaint without polling.
        public event Action Changed;

        /// Slider positions, 0..1 linear - what the player set, not what the speaker gets.
        public float MusicVolume { get; private set; }
        public float SfxVolume { get; private set; }

        public bool MusicMuted { get; private set; }
        public bool SfxMuted { get; private set; }

        /// What an AudioSource is actually given: the perceptual curve, gated by the mute.
        public float EffectiveMusic => MusicMuted ? 0f : Perceptual(MusicVolume);
        public float EffectiveSfx => SfxMuted ? 0f : Perceptual(SfxVolume);

        public SoundSettings(ISoundSettingsStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            MusicVolume = Clamp(_store.GetFloat(MusicKey, DefaultMusic));
            SfxVolume = Clamp(_store.GetFloat(SfxKey, DefaultSfx));
            MusicMuted = _store.GetInt(MusicMutedKey, 0) == 1;
            SfxMuted = _store.GetInt(SfxMutedKey, 0) == 1;
        }

        /// A slider move. Clears the mute: a player dragging the MUSIC slider wants to hear
        /// music, whatever a toggle said earlier.
        public void SetMusicVolume(float level)
        {
            level = Clamp(level);
            if (level == MusicVolume && !MusicMuted) return;
            MusicVolume = level;
            MusicMuted = false;
            Changed?.Invoke();
        }

        public void SetSfxVolume(float level)
        {
            level = Clamp(level);
            if (level == SfxVolume && !SfxMuted) return;
            SfxVolume = level;
            SfxMuted = false;
            Changed?.Invoke();
        }

        public void SetMusicMuted(bool muted)
        {
            if (muted == MusicMuted) return;
            MusicMuted = muted;
            Changed?.Invoke();
        }

        public void SetSfxMuted(bool muted)
        {
            if (muted == SfxMuted) return;
            SfxMuted = muted;
            Changed?.Invoke();
        }

        /// Writes everything and flushes the store. Idempotent; call it at a batch boundary.
        public void Save()
        {
            _store.SetFloat(MusicKey, MusicVolume);
            _store.SetFloat(SfxKey, SfxVolume);
            _store.SetInt(MusicMutedKey, MusicMuted ? 1 : 0);
            _store.SetInt(SfxMutedKey, SfxMuted ? 1 : 0);
            _store.Save();
        }

        /// Slider position -> AudioSource volume. Monotonic, 0 -> 0, 1 -> 1, and the midpoint
        /// lands at 0.25, which is about -12 dB - "half as loud" to most ears.
        public static float Perceptual(float level)
        {
            level = Clamp(level);
            return level * level;
        }

        /// NaN is treated as silence rather than propagated: a NaN volume on an AudioSource is
        /// undefined behaviour on some platforms, and a corrupted pref must never produce it.
        static float Clamp(float value) =>
            float.IsNaN(value) ? 0f : value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
