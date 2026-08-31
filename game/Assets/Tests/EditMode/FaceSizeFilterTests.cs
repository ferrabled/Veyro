using MotionRunner.Pose;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// The face's own width is the scale factor that makes camera control distance-invariant, and
    /// it is also a divisor — so the two things that can be quietly wrong here are "it believes a
    /// number it should not" and "it says the player is out of range when they are not". Both are
    /// arithmetic, and the second one is a soft lock at the staging screen (CameraStaging refuses
    /// to hand the run over while it reads too far), so neither is something to discover on a
    /// phone.
    public sealed class FaceSizeFilterTests
    {
        const float Dt = 1f / 30f;

        static FaceSizeFilter Settled(float size, float seconds = 2f)
        {
            var filter = new FaceSizeFilter();
            for (float t = 0f; t < seconds; t += Dt) filter.Submit(size, Dt);
            return filter;
        }

        [Test]
        public void StartsWithNoMeasurementAndASafeScale()
        {
            var filter = new FaceSizeFilter();
            Assert.IsFalse(filter.HasValue);
            Assert.AreEqual(FaceSizeFilter.UnknownSize, filter.Value, 1e-6f);
            Assert.IsFalse(filter.IsTooFar, "an unmeasured face must not read as too far");
        }

        [Test]
        public void TheFirstUsableSampleSeedsTheFilterInsteadOfBeingEasedInto()
        {
            var filter = new FaceSizeFilter();
            Assert.AreEqual(0.09f, filter.Submit(0.09f, Dt), 1e-6f);
            Assert.IsTrue(filter.HasValue);
        }

        [Test]
        public void JitterIsSmoothedAwayWhileTheAverageIsKept()
        {
            var filter = new FaceSizeFilter();
            for (int i = 0; i < 120; i++)
                filter.Submit(0.10f + (i % 2 == 0 ? 0.03f : -0.03f), Dt);

            // +-30% of raw box jitter frame to frame, and what comes out sits within 2% of the
            // true width — the residual ripple of a square wave through a 0.33 s time constant.
            Assert.AreEqual(0.10f, filter.Value, 2e-3f);
        }

        [Test]
        public void ItFollowsAPlayerWhoActuallyChangesDistance()
        {
            var filter = Settled(0.30f);
            for (float t = 0f; t < 2f; t += Dt) filter.Submit(0.08f, Dt);
            Assert.AreEqual(0.08f, filter.Value, 5e-3f);
        }

        [Test]
        public void UnusableSamplesLeaveTheLastGoodScaleStanding()
        {
            var filter = Settled(0.20f);

            foreach (float bad in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            {
                for (int i = 0; i < 30; i++) filter.Submit(bad, Dt);
                Assert.AreEqual(0.20f, filter.Value, 1e-3f, bad + " moved the scale");
            }
        }

        [Test]
        public void AnImpossibleWidthIsDroppedRatherThanClampedIn()
        {
            // The divisor guard. Clamping a near-zero width up to the floor would still divide by
            // the smallest number allowed — tracker noise as full-lock steering — so a width
            // outside the trusted band is not believed at all.
            var fresh = new FaceSizeFilter();
            fresh.Submit(1e-6f, Dt);
            Assert.IsFalse(fresh.HasValue, "a 0.5 px face is not a measurement");
            Assert.AreEqual(FaceSizeFilter.UnknownSize, fresh.Value, 1e-6f);

            var settled = Settled(0.20f);
            settled.Submit(1e-6f, Dt);
            settled.Submit(50f, Dt);
            Assert.AreEqual(0.20f, settled.Value, 1e-3f, "a good scale was thrown away");
            Assert.GreaterOrEqual(settled.Value, FaceSizeFilter.MinTrustedSize);
            Assert.LessOrEqual(settled.Value, FaceSizeFilter.MaxTrustedSize);
        }

        [Test]
        public void TooFarIsWhereTheDetectorRunsOut_NotWhereTheSteeringDoes()
        {
            // MinPlayableSize is the staging cue's threshold, and it is set by BlazeFace
            // short-range's ~2 m rating rather than by the gesture maths. Sanity-check both sides
            // of it with the distances it stands for: a ~15 cm face comes with a ~20 cm detection
            // box (BoxWidthsPerFace) and the upright frame spans roughly one metre of world per
            // metre of distance, so size ~= 0.2025 / metres.
            Assert.IsTrue(Settled(0.2025f).IsTooFar == false, "1 m must be playable");
            Assert.IsTrue(Settled(0.101f).IsTooFar == false, "2 m must be playable");
            Assert.IsTrue(Settled(0.081f).IsTooFar == false, "2.5 m must be playable");
            Assert.IsTrue(Settled(0.05f).IsTooFar, "4 m is past anything the detector can hold");

            // And the threshold itself is where it was left: past the model's rated range, so a
            // marginal player is let through rather than blocked. It was set believing 0.06 was a
            // face at 2.5 m; in box units it is nearer 3.4 m, which errs the same (safe) way and is
            // deliberately not being retuned from arithmetic — the telemetry line reports `size`,
            // so a real reading at a measured distance is what should move it.
            Assert.AreEqual(0.06f, FaceSizeFilter.MinPlayableSize, 1e-6f);
        }

        [Test]
        public void TheBoxIsWiderThanTheFaceItFound()
        {
            // The distinction the whole camera-distance story turns on. BlazeFace regresses its
            // training box — chin to hairline, out past the ears — not an anatomical face, so
            // dividing a displacement by Value measures it in boxes while calling it faces, and
            // multiplies every centimetre threshold in FaceSteering by this factor. FaceWidth is
            // the one place the conversion happens.
            var filter = Settled(0.20f);
            Assert.Greater(FaceSizeFilter.BoxWidthsPerFace, 1f,
                "a detection box is never narrower than the face inside it");
            Assert.AreEqual(0.20f / FaceSizeFilter.BoxWidthsPerFace, filter.FaceWidth, 1e-6f);
            Assert.Less(filter.FaceWidth, filter.Value);
        }

        [Test]
        public void FaceWidthIsAlwaysSafeToDivideBy()
        {
            // FaceSteering divides by it unguarded, on every sample, including before any
            // observation has arrived.
            var fresh = new FaceSizeFilter();
            Assert.Greater(fresh.FaceWidth, 0f, "the unmeasured default must be a legal divisor");

            foreach (float size in new[] { FaceSizeFilter.MinTrustedSize, FaceSizeFilter.MaxTrustedSize })
            {
                var filter = new FaceSizeFilter();
                filter.Submit(size, Dt);
                Assert.Greater(filter.FaceWidth, 0f, size + " gave a zero divisor");
            }
        }

        [Test]
        public void ResetForgetsTheMeasurementRatherThanKeepingAStaleOne()
        {
            var filter = Settled(0.30f);
            filter.Reset();
            Assert.IsFalse(filter.HasValue);
            Assert.IsFalse(filter.IsTooFar);
            Assert.AreEqual(0.09f, filter.Submit(0.09f, Dt), 1e-6f,
                "after a reset the next sample should seed, not blend with the old distance");
        }
    }
}
