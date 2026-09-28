using System;
using MotionRunner.CameraInput;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// Why any caller of BlazeAffine.LoadAnchors has to catch.
    ///
    /// LoadAnchors is upstream sample code (Unity's BlazeDetectionSample, kept diffable — so it is
    /// NOT going to grow validation): it indexes a fixed number of lines and columns out of a CSV
    /// and parses every cell, checking none of it. A truncated, re-exported or half-written anchors
    /// file therefore throws. That once mattered in the shipping build: the pause screen's
    /// raise-hand probe loaded the BlazePose anchor table synchronously inside PauseMenu.Update,
    /// and before PR #6's review fix the throw escaped it and left the player frozen behind a
    /// pause card whose buttons were still disabled. The probe and its anchor table are gone from
    /// the shipping build since 28 Sep 2026 (hop-twice resume); the only caller left is the T-010
    /// CV spike's BlazePoseRunner, which reads its own committed copy (Assets/CV/Data/anchors.csv).
    ///
    /// These tests pin the throw rather than remove it, so the next caller knows the input really
    /// is hostile and nobody reads a catch around it as defensive noise. The happy-path test below
    /// is the real table's shape in miniature.
    public sealed class BlazeAnchorsTests
    {
        /// Written invariant by hand, never through a float formatter: LoadAnchors parses with
        /// CultureInfo.InvariantCulture, so a test that generated "0,02" on a comma-decimal
        /// machine would fail for a reason that has nothing to do with anchors.
        static string WellFormed(int rows)
        {
            var csv = new System.Text.StringBuilder();
            for (int i = 0; i < rows; i++)
                csv.Append("0.0").Append(i).Append(",0.1").Append(i).Append(",0.1,0.2\n");
            return csv.ToString();
        }

        [Test]
        public void WellFormedCsv_ParsesRowsAndColumns()
        {
            float[,] anchors = BlazeAffine.LoadAnchors(WellFormed(3), 3);

            Assert.AreEqual(3, anchors.GetLength(0));
            Assert.AreEqual(4, anchors.GetLength(1));
            Assert.AreEqual(0.01f, anchors[1, 0], 1e-5f);
            Assert.AreEqual(0.11f, anchors[1, 1], 1e-5f);
            Assert.AreEqual(0.2f, anchors[2, 3], 1e-5f);
        }

        /// A file that is shorter than the anchor count the model expects — the shape a truncated
        /// download or an interrupted write actually has. Cut at the last newline, because a file
        /// that still ends in one throws one line later and for the other reason (the empty tail
        /// row fails to parse); both are inside the same catch, and pinning the tidier one here
        /// keeps the two cases from looking like one test.
        [Test]
        public void TruncatedCsv_Throws()
        {
            Assert.Throws<IndexOutOfRangeException>(
                () => BlazeAffine.LoadAnchors(WellFormed(2).TrimEnd('\n'), 3));
        }

        /// The same truncation with its trailing newline intact — the way a real half-written
        /// file looks. Different exception, same need for the caller to catch it.
        [Test]
        public void TruncatedCsvWithTrailingNewline_Throws()
        {
            Assert.Catch<Exception>(() => BlazeAffine.LoadAnchors(WellFormed(2), 3));
        }

        /// A row with fewer than four columns: the same indexing walk, one level in.
        [Test]
        public void ShortRow_Throws()
        {
            Assert.Throws<IndexOutOfRangeException>(
                () => BlazeAffine.LoadAnchors("0.1,0.2\n", 1));
        }

        /// Anything that is not a float — a header line survived, a locale wrote decimal commas,
        /// the asset is not a CSV at all.
        [Test]
        public void NonNumericCell_Throws()
        {
            Assert.Throws<FormatException>(
                () => BlazeAffine.LoadAnchors("x,y,w,h\n", 1));
        }

        /// And the empty asset, which is what an import that quietly failed leaves behind.
        [Test]
        public void EmptyCsv_Throws()
        {
            Assert.Catch<Exception>(() => BlazeAffine.LoadAnchors(string.Empty, 1));
        }
    }
}
