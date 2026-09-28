using MotionRunner.Track;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// SHARE's "COPIED" beat on iOS (decision 4: SHARE copies to the clipboard there). The HUD
    /// only writes the label when Poll says so, so these pin both halves: what the button shows
    /// at a given time, and that it is redrawn exactly on the two transitions of each flash.
    public sealed class CopiedFlashTests
    {
        [Test]
        public void ReadsCopiedForAboutTwoSeconds()
        {
            Assert.AreEqual("COPIED", CopiedFlash.Label);
            Assert.AreEqual(2f, CopiedFlash.Seconds);
        }

        [Test]
        public void IdleUntilSomethingIsCopied()
        {
            var flash = new CopiedFlash();
            Assert.IsFalse(flash.Showing(0f));
            Assert.IsFalse(flash.Poll(0f, out _), "nothing to redraw on an untouched button");
        }

        [Test]
        public void ACopyShowsTheLabelThenRevertsOnce()
        {
            var flash = new CopiedFlash();
            flash.Report(sheetOpened: false, now: 10f);

            Assert.IsTrue(flash.Poll(10f, out bool copied));
            Assert.IsTrue(copied, "the tap's own frame flips to COPIED");
            Assert.IsFalse(flash.Poll(11.9f, out _), "no redraw while it keeps showing");
            Assert.IsTrue(flash.Showing(11.9f));

            Assert.IsTrue(flash.Poll(12f, out copied), "the window is [now, now + Seconds)");
            Assert.IsFalse(copied);
            Assert.IsFalse(flash.Poll(20f, out _), "reverted exactly once");
        }

        [Test]
        public void ARealShareSheetNeedsNoFlash()
        {
            var flash = new CopiedFlash();
            flash.Report(sheetOpened: true, now: 10f);

            Assert.IsFalse(flash.Showing(10f));
            Assert.IsFalse(flash.Poll(10f, out _));
        }

        [Test]
        public void ASheetOpeningStopsARunningFlash()
        {
            var flash = new CopiedFlash();
            flash.Report(false, 10f);
            flash.Poll(10f, out _);

            flash.Report(true, 10.5f);
            Assert.IsTrue(flash.Poll(10.5f, out bool copied));
            Assert.IsFalse(copied);
        }

        [Test]
        public void ASecondCopyRestartsTheWindow()
        {
            var flash = new CopiedFlash();
            flash.Report(false, 10f);
            flash.Poll(10f, out _);

            flash.Report(false, 11.5f);
            Assert.IsFalse(flash.Poll(12.5f, out _), "still COPIED past the first window");
            Assert.IsTrue(flash.Showing(13.4f));
            Assert.IsTrue(flash.Poll(13.5f, out bool copied));
            Assert.IsFalse(copied);
        }

        [Test]
        public void ClearPutsTheLabelBackOnTheNextPoll()
        {
            var flash = new CopiedFlash();
            flash.Report(false, 10f);
            flash.Poll(10f, out _);

            flash.Clear();
            Assert.IsFalse(flash.Showing(10.1f));
            Assert.IsTrue(flash.Poll(10.1f, out bool copied));
            Assert.IsFalse(copied);
            Assert.IsFalse(flash.Poll(10.2f, out _));
        }

        [Test]
        public void ClearOnAnIdleButtonRedrawsNothing()
        {
            var flash = new CopiedFlash();
            flash.Clear();
            Assert.IsFalse(flash.Poll(0f, out _));
        }
    }
}
