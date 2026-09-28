using System.Collections.Generic;
using MotionRunner.Audio;
using MotionRunner.Track;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// The run track is a function of the seed (T-045): the Daily Run is one shared song a day,
    /// a challenge rematch sounds like the run it rematches, and RUN AGAIN never changes key.
    public sealed class MusicPickerTests
    {
        static uint Hash(int seed) => new RunSeed(seed, "greybox-1", RunSeed.DefaultWorldId).RngState();

        [Test]
        public void SameSeed_SameTrack()
        {
            for (int seed = -50; seed < 50; seed++)
                Assert.AreEqual(MusicPicker.TrackIndex(Hash(seed), 3), MusicPicker.TrackIndex(Hash(seed), 3));
        }

        [Test]
        public void IndexIsAlwaysInRange()
        {
            foreach (int count in new[] { 1, 2, 3, 4, 7, 8 })
                for (int seed = 0; seed < 500; seed++)
                {
                    int index = MusicPicker.TrackIndex(Hash(seed), count);
                    Assert.GreaterOrEqual(index, 0);
                    Assert.Less(index, count);
                }
        }

        [Test]
        public void DifferentSeeds_SpreadAcrossTheTracks()
        {
            // Consecutive daily seeds are the realistic input: DailySeed derives them from
            // consecutive day numbers, so they differ in the low bits of one field. Every track
            // must get a fair share of a season's worth of days, not two tracks all the weeks.
            foreach (int count in new[] { 3, 4 })
            {
                var hits = new int[count];
                const int days = 120;
                for (int day = 0; day < days; day++)
                    hits[MusicPicker.TrackIndex(Hash(20000 + day), count)]++;

                for (int track = 0; track < count; track++)
                    Assert.Greater(hits[track], days / (count * 3),
                        "track " + track + " of " + count + " is starved: " + string.Join(",", hits));
            }
        }

        [Test]
        public void SeedsThatDifferByOne_DoNotAllShareATrack()
        {
            var seen = new HashSet<int>();
            for (int seed = 0; seed < 12; seed++) seen.Add(MusicPicker.TrackIndex(Hash(seed), 3));
            Assert.AreEqual(3, seen.Count, "twelve consecutive seeds must reach all three tracks");
        }

        [Test]
        public void OneTrack_IsAlwaysTrackZero()
        {
            for (int seed = 0; seed < 20; seed++)
                Assert.AreEqual(0, MusicPicker.TrackIndex(Hash(seed), 1));
        }

        [Test]
        public void NoTracks_IsMinusOne()
        {
            Assert.AreEqual(-1, MusicPicker.TrackIndex(Hash(1), 0));
            Assert.AreEqual(-1, MusicPicker.TrackIndex(Hash(1), -3));
        }

        [Test]
        public void Cycling_WalksEveryTrackInOrderAndWraps()
        {
            int cursor = 0;
            Assert.AreEqual(0, MusicPicker.NextCycled(ref cursor, 3));
            Assert.AreEqual(1, MusicPicker.NextCycled(ref cursor, 3));
            Assert.AreEqual(2, MusicPicker.NextCycled(ref cursor, 3));
            Assert.AreEqual(0, MusicPicker.NextCycled(ref cursor, 3));
            Assert.AreEqual(-1, MusicPicker.NextCycled(ref cursor, 0));
        }
    }
}
