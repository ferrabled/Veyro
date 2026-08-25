using System;
using System.Collections.Generic;
using MotionRunner.Pose;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// Handoff 8.2 warns that WebCamTexture rotation/mirroring, not the ML, is where the CV track
    /// loses days. These tests pin every corner of every orientation so that debugging happens
    /// here rather than through build-install-squint cycles on the phone.
    public sealed class FrameOrientationTests
    {
        const float Tol = 1e-5f;

        static readonly (float u, float v)[] Corners = { (0f, 0f), (1f, 0f), (0f, 1f), (1f, 1f) };

        static void AssertMaps(in FrameOrientation.Result r, float u, float v,
            float expectedU, float expectedV, string what)
        {
            PosePoint p = r.SourceUvFromUpright.Apply(u, v);
            Assert.AreEqual(expectedU, p.X, Tol, what + " u");
            Assert.AreEqual(expectedV, p.Y, Tol, what + " v");
        }

        [Test]
        public void NoRotationIsTheIdentity()
        {
            var r = FrameOrientation.ForCamera(640, 480, 0, false, false);
            Assert.AreEqual(640, r.Width);
            Assert.AreEqual(480, r.Height);
            AssertMaps(r, 0f, 0f, 0f, 0f, "top-left");
            AssertMaps(r, 1f, 1f, 1f, 1f, "bottom-right");
        }

        [Test]
        public void QuarterTurnSwapsWidthAndHeight()
        {
            // The case that actually happens: a 640x480 landscape sensor on a portrait phone.
            var r = FrameOrientation.ForCamera(640, 480, 90, false, false);
            Assert.AreEqual(480, r.Width);
            Assert.AreEqual(640, r.Height);
        }

        [Test]
        public void HalfTurnKeepsTheFrameSize()
        {
            var r = FrameOrientation.ForCamera(640, 480, 180, false, false);
            Assert.AreEqual(640, r.Width);
            Assert.AreEqual(480, r.Height);
        }

        [Test]
        public void NinetyDegreesPullsTheUprightTopLeftFromTheSourceBottomLeft()
        {
            // Rotating the source clockwise a quarter turn sends its bottom-left corner to the
            // top-left of what the player sees, so that is where the sampler must read from.
            var r = FrameOrientation.ForCamera(640, 480, 90, false, false);
            AssertMaps(r, 0f, 0f, 0f, 1f, "upright top-left");
            AssertMaps(r, 1f, 0f, 0f, 0f, "upright top-right");
            AssertMaps(r, 0f, 1f, 1f, 1f, "upright bottom-left");
            AssertMaps(r, 1f, 1f, 1f, 0f, "upright bottom-right");
        }

        [Test]
        public void TwoHundredSeventyDegreesIsTheOppositeQuarterTurn()
        {
            var r = FrameOrientation.ForCamera(640, 480, 270, false, false);
            AssertMaps(r, 0f, 0f, 1f, 0f, "upright top-left");
            AssertMaps(r, 1f, 0f, 1f, 1f, "upright top-right");
            AssertMaps(r, 0f, 1f, 0f, 0f, "upright bottom-left");
            AssertMaps(r, 1f, 1f, 0f, 1f, "upright bottom-right");
        }

        [Test]
        public void OppositeQuarterTurnsUndoOneAnother()
        {
            var cw = FrameOrientation.ForCamera(640, 480, 90, false, false);
            var ccw = FrameOrientation.ForCamera(480, 640, 270, false, false);

            // Round-tripping a point through both must land back where it started.
            PosePoint mid = cw.SourceUvFromUpright.Apply(0.25f, 0.75f);
            PosePoint back = ccw.SourceUvFromUpright.Apply(mid.X, mid.Y);
            Assert.AreEqual(0.25f, back.X, Tol);
            Assert.AreEqual(0.75f, back.Y, Tol);
        }

        [Test]
        public void HalfTurnFlipsBothAxes()
        {
            var r = FrameOrientation.ForCamera(640, 480, 180, false, false);
            AssertMaps(r, 0f, 0f, 1f, 1f, "top-left");
            AssertMaps(r, 1f, 1f, 0f, 0f, "bottom-right");
        }

        [Test]
        public void VerticallyMirroredSourceIsCorrectedInSourceSpace()
        {
            var r = FrameOrientation.ForCamera(640, 480, 0, true, false);
            AssertMaps(r, 0f, 0f, 0f, 1f, "top-left reads the source bottom");
            AssertMaps(r, 0f, 1f, 0f, 0f, "bottom-left reads the source top");
        }

        [Test]
        public void SelfieMirrorFlipsLeftAndRightOfWhatThePlayerSees()
        {
            var r = FrameOrientation.ForCamera(640, 480, 0, false, true);
            AssertMaps(r, 0f, 0f, 1f, 0f, "top-left");
            AssertMaps(r, 1f, 0f, 0f, 0f, "top-right");
            AssertMaps(r, 0.5f, 0.5f, 0.5f, 0.5f, "centre is fixed");
        }

        [Test]
        public void AcrossAQuarterTurnTheTwoMirrorsCancel()
        {
            // The real front-camera case: rotated 90, source reported as vertically flipped, and
            // shown as a selfie mirror. A horizontal flip on one side of a quarter turn is a
            // vertical flip on the other, so the two corrections annihilate and the result is the
            // bare rotation. Worth pinning, because "my mirror flags do nothing" looks like a bug
            // right up until you work out that here it is arithmetic.
            var r = FrameOrientation.ForCamera(640, 480, 90, true, true);
            AssertMaps(r, 0f, 0f, 0f, 1f, "upright top-left");
            AssertMaps(r, 1f, 0f, 0f, 0f, "upright top-right");
            AssertMaps(r, 0f, 1f, 1f, 1f, "upright bottom-left");
            AssertMaps(r, 1f, 1f, 1f, 0f, "upright bottom-right");
        }

        [Test]
        public void AcrossAHalfTurnTheTwoMirrorsCompoundIntoTheIdentity()
        {
            // Same two flags, half a turn instead of a quarter, and now they do not cancel each
            // other but cancel the rotation. Together with the test above this pins the placement
            // of each flip in the chain: swap them and one of these two fails.
            var r = FrameOrientation.ForCamera(640, 480, 180, true, true);
            foreach ((float u, float v) in Corners) AssertMaps(r, u, v, u, v, "corner");
            AssertMaps(r, 0.3f, 0.8f, 0.3f, 0.8f, "interior point");
        }

        [Test]
        public void EveryOrientationIsCornerToCorner()
        {
            // Whatever the combination, the four corners of the upright frame must map onto the
            // four distinct corners of the source. Catches a dropped term in any composition.
            foreach (int rotation in new[] { 0, 90, 180, 270 })
            foreach (bool flipped in new[] { false, true })
            foreach (bool mirrored in new[] { false, true })
            {
                var r = FrameOrientation.ForCamera(640, 480, rotation, flipped, mirrored);
                string label = rotation + "/" + flipped + "/" + mirrored;
                var seen = new HashSet<string>();

                foreach ((float u, float v) in Corners)
                {
                    PosePoint p = r.SourceUvFromUpright.Apply(u, v);
                    Assert.IsTrue(Math.Abs(p.X) < Tol || Math.Abs(p.X - 1f) < Tol,
                        label + " u off-corner");
                    Assert.IsTrue(Math.Abs(p.Y) < Tol || Math.Abs(p.Y - 1f) < Tol,
                        label + " v off-corner");
                    Assert.IsTrue(seen.Add(Math.Round(p.X) + "," + Math.Round(p.Y)),
                        label + " maps two corners onto the same source corner");
                }
            }
        }

        [Test]
        public void NegativeAndOverfullRotationsAreNormalized()
        {
            var minus90 = FrameOrientation.ForCamera(640, 480, -90, false, false);
            var plus270 = FrameOrientation.ForCamera(640, 480, 270, false, false);
            var plus630 = FrameOrientation.ForCamera(640, 480, 630, false, false);

            foreach ((float u, float v) in new[] { (0f, 0f), (1f, 0f), (0.3f, 0.8f) })
            {
                PosePoint a = minus90.SourceUvFromUpright.Apply(u, v);
                PosePoint b = plus270.SourceUvFromUpright.Apply(u, v);
                PosePoint c = plus630.SourceUvFromUpright.Apply(u, v);
                Assert.AreEqual(a.X, b.X, Tol);
                Assert.AreEqual(a.Y, b.Y, Tol);
                Assert.AreEqual(a.X, c.X, Tol);
                Assert.AreEqual(a.Y, c.Y, Tol);
            }
        }

        [Test]
        public void NonQuarterTurnRotationIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => FrameOrientation.ForCamera(640, 480, 45, false, false));
        }

        [Test]
        public void CompositionMatchesSequentialApplication()
        {
            var a = PoseAffine2x3.Translation(0.1f, 0.2f);
            var b = PoseAffine2x3.Scale(2f, 3f);
            PosePoint composed = PoseAffine2x3.Multiply(a, b).Apply(0.5f, 0.5f);
            PosePoint sequential = a.Apply(b.Apply(0.5f, 0.5f));
            Assert.AreEqual(sequential.X, composed.X, Tol);
            Assert.AreEqual(sequential.Y, composed.Y, Tol);
        }
    }
}
