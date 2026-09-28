using System.Collections.Generic;
using System.Text;
using MotionRunner.Social;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// The profile's generated critter. The promises worth pinning are the ones a player would
    /// notice as a bug: the same name always gives the same face (on every device, every launch),
    /// the face is a face (mirrored, two eyes), and different names mostly give different faces.
    public sealed class PlayerAvatarTests
    {
        const int Size = AvatarPattern.Size;

        [Test]
        public void TheSameName_AlwaysGivesTheSameCritter()
        {
            Assert.AreEqual(Draw(PlayerAvatar.For("BRAVE-EGRET-74")), Draw(PlayerAvatar.For("BRAVE-EGRET-74")));
            Assert.AreEqual(PlayerAvatar.For("BRAVE-EGRET-74").Palette, PlayerAvatar.For("BRAVE-EGRET-74").Palette);
        }

        [Test]
        public void AKnownName_KeepsItsKnownFace()
        {
            // Pinned: the hash and the mixer are the face. Changing either redraws every player's
            // avatar at once, which should be a decision rather than a side effect of a refactor.
            var pattern = PlayerAvatar.For("BRAVE-EGRET-74");
            Assert.AreEqual(0, pattern.Palette);
            Assert.AreEqual(
                ".#...#.\n" +
                ".#####.\n" +
                ".#.#.#.\n" +
                "#######\n" +
                ".#####.\n" +
                "..#.#..\n" +
                ".......\n",
                Draw(pattern));
        }

        [Test]
        public void CaseAndSurroundingSpace_DoNotChangeTheFace()
        {
            string canonical = Draw(PlayerAvatar.For("BRAVE-EGRET-74"));
            Assert.AreEqual(canonical, Draw(PlayerAvatar.For("brave-egret-74")));
            Assert.AreEqual(canonical, Draw(PlayerAvatar.For("  BRAVE-EGRET-74 ")));
        }

        [Test]
        public void NoName_StillGetsACritter()
        {
            Assert.Greater(PlayerAvatar.For(null).FilledCount, 0, "a null handle must not draw a blank tile");
            Assert.AreEqual(Draw(PlayerAvatar.For(null)), Draw(PlayerAvatar.For(string.Empty)));
        }

        [Test]
        public void EveryCritter_IsMirrored_AndHasTwoEnclosedEyes()
        {
            foreach (string name in Names(300))
            {
                var p = PlayerAvatar.For(name);
                for (int row = 0; row < Size; row++)
                    for (int column = 0; column < Size; column++)
                        Assert.AreEqual(p[column, row], p[Size - 1 - column, row], name + " is not mirrored");

                int eye = PlayerAvatar.EyeColumn, eyeRow = PlayerAvatar.EyeRow;
                Assert.IsFalse(p[eye, eyeRow], name + " has no eye");
                Assert.IsTrue(p[eye - 1, eyeRow] && p[eye + 1, eyeRow] &&
                              p[eye, eyeRow - 1] && p[eye, eyeRow + 1],
                    name + "'s eye is not surrounded, so it reads as a notch rather than an eye");
            }
        }

        [Test]
        public void ThePaletteIndex_IsAlwaysInRange()
        {
            foreach (string name in Names(300))
            {
                int palette = PlayerAvatar.For(name).Palette;
                Assert.GreaterOrEqual(palette, 0);
                Assert.Less(palette, PlayerAvatar.PaletteSize);
            }
        }

        [Test]
        public void DifferentNames_MostlyGiveDifferentFaces()
        {
            // Shape and colour together. A reroll that lands on the same face would read as
            // the button doing nothing.
            var faces = new HashSet<string>();
            foreach (string name in Names(200))
            {
                var p = PlayerAvatar.For(name);
                faces.Add(p.Palette + Draw(p));
            }
            Assert.Greater(faces.Count, 150, "200 names gave only " + faces.Count + " distinct faces");
        }

        static IEnumerable<string> Names(int count)
        {
            string[] adjectives = { "BRAVE", "SWIFT", "CALM", "LUCKY", "QUIET" };
            string[] animals = { "EGRET", "OTTER", "HERON", "FOX", "LYNX", "WREN" };
            for (int i = 0; i < count; i++)
                yield return adjectives[i % adjectives.Length] + "-" +
                             animals[(i / adjectives.Length) % animals.Length] + "-" + (i % 100).ToString("00");
        }

        static string Draw(AvatarPattern pattern)
        {
            var text = new StringBuilder();
            for (int row = 0; row < Size; row++)
            {
                for (int column = 0; column < Size; column++)
                    text.Append(pattern[column, row] ? '#' : '.');
                text.Append('\n');
            }
            return text.ToString();
        }
    }
}
