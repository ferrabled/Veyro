using System;
using System.Collections.Generic;
using MotionRunner.Audio;
using NUnit.Framework;
using UnityEngine;

namespace MotionRunner.Tests
{
    /// The catalog is the contract between the code and the asset folder. Two halves: every
    /// enum entry names a path (pure), and every path resolves to a clip under Resources (an
    /// asset check, so a renamed or missing .ogg fails HERE, by name, rather than as a silent
    /// button on the phone - GameAudio itself only warns).
    public sealed class AudioCatalogTests
    {
        [Test]
        public void EverySfx_HasAPath()
        {
            foreach (Sfx id in AudioCatalog.All)
            {
                string path = AudioCatalog.Path(id);
                Assert.IsFalse(string.IsNullOrEmpty(path), "Sfx." + id + " has no Resources path");
                Assert.IsTrue(path.StartsWith(AudioCatalog.SfxFolder), "Sfx." + id + " lives outside " + AudioCatalog.SfxFolder + ": " + path);
            }
        }

        [Test]
        public void SfxPaths_AreUnique()
        {
            var seen = new HashSet<string>();
            foreach (Sfx id in AudioCatalog.All)
                Assert.IsTrue(seen.Add(AudioCatalog.Path(id)), "two Sfx share the path " + AudioCatalog.Path(id));
        }

        [Test]
        public void All_CoversTheWholeEnum()
        {
            Assert.AreEqual(Enum.GetValues(typeof(Sfx)).Length, AudioCatalog.All.Length);
            for (int i = 0; i < AudioCatalog.All.Length; i++)
                Assert.AreEqual(i, (int)AudioCatalog.All[i], "GameAudio indexes its per-frame cap by (int)Sfx; the enum must be dense from 0");
        }

        [Test]
        public void RunTrackPaths_AreZeroPaddedAndOneBased()
        {
            Assert.AreEqual("Audio/Music/run_01", AudioCatalog.RunTrackPath(1));
            Assert.AreEqual("Audio/Music/run_08", AudioCatalog.RunTrackPath(8));
        }

        [Test]
        public void ResultMusicPaths_AreTwoDistinctTracksInTheMusicFolder()
        {
            Assert.IsTrue(AudioCatalog.ResultBestMusic.StartsWith(AudioCatalog.MusicFolder));
            Assert.IsTrue(AudioCatalog.ResultLostMusic.StartsWith(AudioCatalog.MusicFolder));
            Assert.AreNotEqual(AudioCatalog.ResultBestMusic, AudioCatalog.ResultLostMusic,
                "a best and a lost run must not sound the same");
            Assert.AreNotEqual(AudioCatalog.MenuMusic, AudioCatalog.ResultLostMusic);
            Assert.AreNotEqual(AudioCatalog.MenuMusic, AudioCatalog.ResultBestMusic);
        }

        // ---- the asset half ----

        [Test]
        public void EverySfx_ResolvesToAClip()
        {
            var missing = new List<string>();
            foreach (Sfx id in AudioCatalog.All)
            {
                string path = AudioCatalog.Path(id);
                if (Resources.Load<AudioClip>(path) == null) missing.Add(id + " (Resources/" + path + ".ogg)");
            }
            Assert.IsEmpty(missing, "SFX clips missing under Assets/Resources/: " + string.Join(", ", missing));
        }

        [Test]
        public void MenuMusic_ResolvesToAClip()
        {
            Assert.IsNotNull(Resources.Load<AudioClip>(AudioCatalog.MenuMusic),
                "menu music missing: Resources/" + AudioCatalog.MenuMusic + ".ogg");
        }

        [Test]
        public void ResultMusic_ResolvesToClips()
        {
            Assert.IsNotNull(Resources.Load<AudioClip>(AudioCatalog.ResultBestMusic),
                "result loop for a new best missing: Resources/" + AudioCatalog.ResultBestMusic + ".ogg");
            Assert.IsNotNull(Resources.Load<AudioClip>(AudioCatalog.ResultLostMusic),
                "result loop for an ordinary run missing: Resources/" + AudioCatalog.ResultLostMusic + ".ogg");
        }

        [Test]
        public void RunMusic_HasAtLeastOneTrackAndNoGaps()
        {
            Assert.IsNotNull(Resources.Load<AudioClip>(AudioCatalog.RunTrackPath(1)),
                "no run music: Resources/" + AudioCatalog.RunTrackPath(1) + ".ogg is the first track the player probes for");

            // The player stops at the first miss, so a run_04 with no run_03 would never play.
            bool gap = false;
            for (int i = 1; i <= AudioCatalog.MaxRunTracks; i++)
            {
                bool present = Resources.Load<AudioClip>(AudioCatalog.RunTrackPath(i)) != null;
                if (gap && present) Assert.Fail("run track " + i + " exists but an earlier one is missing - GameAudio would never reach it");
                if (!present) gap = true;
            }
        }
    }
}
