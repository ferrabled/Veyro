using System.Collections.Generic;
using MotionRunner.Audio;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// The levels are wire format on players' phones (PlayerPrefs keys) and the perceptual curve
    /// is what every AudioSource volume in the game passes through - both are arithmetic, so
    /// they are pinned here rather than judged by ear on a device.
    public sealed class SoundSettingsTests
    {
        sealed class FakeStore : ISoundSettingsStore
        {
            public readonly Dictionary<string, float> Floats = new Dictionary<string, float>();
            public readonly Dictionary<string, int> Ints = new Dictionary<string, int>();
            public int Saves;

            public float GetFloat(string key, float fallback) => Floats.TryGetValue(key, out var v) ? v : fallback;
            public void SetFloat(string key, float value) => Floats[key] = value;
            public int GetInt(string key, int fallback) => Ints.TryGetValue(key, out var v) ? v : fallback;
            public void SetInt(string key, int value) => Ints[key] = value;
            public void Save() => Saves++;
        }

        [Test]
        public void Defaults_WhenNothingIsStored()
        {
            var settings = new SoundSettings(new FakeStore());
            Assert.AreEqual(0.8f, settings.MusicVolume, 1e-6f);
            Assert.AreEqual(1.0f, settings.SfxVolume, 1e-6f);
            Assert.IsFalse(settings.MusicMuted);
            Assert.IsFalse(settings.SfxMuted);
        }

        [Test]
        public void Keys_AreTheVeyroAudioNamespace()
        {
            Assert.AreEqual("veyro.audio.music", SoundSettings.MusicKey);
            Assert.AreEqual("veyro.audio.sfx", SoundSettings.SfxKey);
        }

        [Test]
        public void Setters_ClampInto01()
        {
            var settings = new SoundSettings(new FakeStore());
            settings.SetMusicVolume(1.7f);
            settings.SetSfxVolume(-0.3f);
            Assert.AreEqual(1f, settings.MusicVolume);
            Assert.AreEqual(0f, settings.SfxVolume);
        }

        [Test]
        public void StoredGarbage_IsClampedOnLoad()
        {
            var store = new FakeStore();
            store.Floats[SoundSettings.MusicKey] = 4f;
            store.Floats[SoundSettings.SfxKey] = float.NaN;
            var settings = new SoundSettings(store);
            Assert.AreEqual(1f, settings.MusicVolume);
            Assert.AreEqual(0f, settings.SfxVolume, "NaN must never reach an AudioSource");
        }

        [Test]
        public void Save_RoundTripsThroughTheStore()
        {
            var store = new FakeStore();
            var settings = new SoundSettings(store);
            settings.SetMusicVolume(0.35f);
            settings.SetSfxVolume(0.6f);
            settings.SetMusicMuted(true);
            Assert.AreEqual(0, store.Saves, "setters are in-memory; a slider drags dozens of times a second");

            settings.Save();
            Assert.AreEqual(1, store.Saves);

            var reloaded = new SoundSettings(store);
            Assert.AreEqual(0.35f, reloaded.MusicVolume, 1e-6f);
            Assert.AreEqual(0.6f, reloaded.SfxVolume, 1e-6f);
            Assert.IsTrue(reloaded.MusicMuted);
            Assert.IsFalse(reloaded.SfxMuted);
        }

        [Test]
        public void MovingASlider_ClearsItsMute()
        {
            var settings = new SoundSettings(new FakeStore());
            settings.SetMusicMuted(true);
            settings.SetSfxMuted(true);
            settings.SetMusicVolume(0.5f);
            Assert.IsFalse(settings.MusicMuted, "dragging MUSIC means wanting music");
            Assert.IsTrue(settings.SfxMuted, "the other mute is untouched");
        }

        [Test]
        public void Mute_SilencesTheEffectiveLevelWithoutLosingTheSlider()
        {
            var settings = new SoundSettings(new FakeStore());
            settings.SetMusicVolume(0.7f);
            settings.SetMusicMuted(true);
            Assert.AreEqual(0f, settings.EffectiveMusic);
            Assert.AreEqual(0.7f, settings.MusicVolume, 1e-6f);
            settings.SetMusicMuted(false);
            Assert.AreEqual(SoundSettings.Perceptual(0.7f), settings.EffectiveMusic, 1e-6f);
        }

        [Test]
        public void Changed_FiresOnRealChangesOnly()
        {
            var settings = new SoundSettings(new FakeStore());
            int changes = 0;
            settings.Changed += () => changes++;
            settings.SetMusicVolume(0.8f); // the default, already
            Assert.AreEqual(0, changes);
            settings.SetMusicVolume(0.5f);
            settings.SetSfxMuted(true);
            settings.SetSfxMuted(true);
            Assert.AreEqual(2, changes);
        }

        [Test]
        public void Perceptual_EndpointsAreExact()
        {
            Assert.AreEqual(0f, SoundSettings.Perceptual(0f));
            Assert.AreEqual(1f, SoundSettings.Perceptual(1f));
        }

        [Test]
        public void Perceptual_IsMonotonicAndBelowLinearInTheMiddle()
        {
            float previous = -1f;
            for (int i = 0; i <= 100; i++)
            {
                float level = i / 100f;
                float mapped = SoundSettings.Perceptual(level);
                Assert.GreaterOrEqual(mapped, previous, "must never get quieter as the slider goes up");
                Assert.GreaterOrEqual(mapped, 0f);
                Assert.LessOrEqual(mapped, 1f);
                previous = mapped;
            }
            Assert.Less(SoundSettings.Perceptual(0.5f), 0.5f, "the midpoint should sound like half, not like 80%");
            Assert.AreEqual(0.25f, SoundSettings.Perceptual(0.5f), 1e-6f);
        }

        [Test]
        public void Perceptual_ClampsOutOfRangeInput()
        {
            Assert.AreEqual(1f, SoundSettings.Perceptual(3f));
            Assert.AreEqual(0f, SoundSettings.Perceptual(-1f));
            Assert.AreEqual(0f, SoundSettings.Perceptual(float.NaN));
        }
    }
}
