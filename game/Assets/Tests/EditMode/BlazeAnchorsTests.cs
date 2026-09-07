using System;
using MotionRunner.CameraInput;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// Why PoseGestureProbe.Load has a try/catch around its body.
    ///
    /// LoadAnchors is the first fallible thing that load does, and it is upstream sample code
    /// (Unity's BlazeDetectionSample, kept diffable — so it is NOT going to grow validation): it
    /// indexes a fixed number of lines and columns out of a CSV and parses every cell, checking
    /// none of it. A truncated, re-exported or half-written anchors file therefore throws, and
    /// before PR #6's review fix that throw escaped PauseMenu.Update through
    /// FaceTrackingRig.BeginGestureProbe, skipping both branches of the resume handler and
    /// leaving the player frozen behind a pause card whose buttons were still disabled.
    ///
    /// These tests pin the throw rather than remove it. The contract that matters is one level
    /// up — Load turns any of this into "false", the caller falls back to touch, the run stays
    /// resumable (rule 3) — and it cannot be exercised here because it needs Resources and a
    /// working inference backend. What CAN be pinned engine-free is that the input really is
    /// hostile, so nobody later reads the catch as defensive noise and deletes it.
    ///
    /// The real file (Resources/CameraInput/pose_anchors.csv) is the well-formed case, and the
    /// happy-path test below is its shape in miniature.
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
        /// file looks. Different exception, same disabled gesture and same working RESUME button.
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
