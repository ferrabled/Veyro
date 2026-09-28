using System;
using MotionRunner.Track;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// The first-run guide's gate and its paging. Both are the kind of thing that looks right the
    /// one time it is tapped through on a phone and is wrong for ever afterwards: the flag is only
    /// ever read on a device that has already seen the guide, and the last page's button is only
    /// reached by someone who read all four. The content checks at the end are the layout
    /// contract with FirstRunGuide: the card is fixed pixels, so copy or a key that drifts breaks
    /// the screen on device and nowhere else.
    public sealed class GuideStateTests
    {
        GuideState _guide;

        [SetUp]
        public void SetUp() => _guide = new GuideState();

        // ---- the seen flag ----

        [Test]
        public void FirstLaunch_ShowsTheGuide()
        {
            Assert.IsTrue(GuideState.ShouldShowOnLaunch(0), "nothing stored means nobody has seen it");
        }

        [Test]
        public void ASeenGuide_NeverComesBackByItself()
        {
            Assert.IsFalse(GuideState.ShouldShowOnLaunch(GuideState.SeenValue));
        }

        [Test]
        public void AnyStoredValue_CountsAsSeen()
        {
            // A flag written by an older build, or by a key collision, must not turn the guide
            // into something that greets the player every single launch.
            Assert.IsFalse(GuideState.ShouldShowOnLaunch(7));
            Assert.IsFalse(GuideState.ShouldShowOnLaunch(-1));
        }

        [Test]
        public void EveryWayOut_MarksItSeen()
        {
            // Skipping included. This is the decision, not an accident: a guide that reappears
            // because it was skipped reads as a bug rather than as help.
            foreach (GuideExit exit in Enum.GetValues(typeof(GuideExit)))
                Assert.IsTrue(GuideState.MarksSeen(exit), "exit " + exit + " must mark the guide seen");
        }

        // ---- paging ----

        [Test]
        public void ItStartsOnTheFirstPage()
        {
            Assert.AreEqual(0, _guide.Index);
            Assert.IsTrue(_guide.IsFirst);
            Assert.IsFalse(_guide.IsLast, "a one-page guide would make the NEXT button unreachable");
        }

        [Test]
        public void Next_WalksToTheLastPageAndStopsThere()
        {
            for (int i = 1; i < GuideState.Pages.Length; i++)
            {
                Assert.IsTrue(_guide.Next(), "page " + i + " should be reachable");
                Assert.AreEqual(i, _guide.Index);
            }

            Assert.IsTrue(_guide.IsLast);
            Assert.IsFalse(_guide.Next(), "the last page's button dismisses instead of paging");
            Assert.AreEqual(GuideState.Pages.Length - 1, _guide.Index);
        }

        [Test]
        public void Back_WalksToTheFirstPageAndStopsThere()
        {
            while (_guide.Next()) { }

            for (int i = GuideState.Pages.Length - 2; i >= 0; i--)
            {
                Assert.IsTrue(_guide.Back());
                Assert.AreEqual(i, _guide.Index);
            }

            Assert.IsTrue(_guide.IsFirst);
            Assert.IsFalse(_guide.Back(), "there is nothing behind page one - the link is hidden, not dead");
            Assert.AreEqual(0, _guide.Index);
        }

        [Test]
        public void ThePrimaryButton_OnlyOffersTheWayOutOnTheLastPage()
        {
            string paging = _guide.PrimaryLabel;

            while (!_guide.IsLast)
            {
                Assert.AreEqual(paging, _guide.PrimaryLabel, "every page before the last pages forward");
                _guide.Next();
            }

            Assert.AreNotEqual(paging, _guide.PrimaryLabel,
                "the last page's button has to say it is the way out");
        }

        // ---- content ----

        [Test]
        public void EveryPageHasSomethingOnIt()
        {
            Assert.Greater(GuideState.Pages.Length, 0, "the screen would build an empty card");

            for (int i = 0; i < GuideState.Pages.Length; i++)
            {
                Assert.IsFalse(string.IsNullOrEmpty(GuideState.Pages[i].Title), "page " + i + " has no title");
                Assert.Greater(GuideState.Pages[i].Rows.Length, 0, "page " + i + " has no rows");
                foreach (GuideRow row in GuideState.Pages[i].Rows)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(row.Key), "page " + i + " has a row with no key");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(row.Text), "page " + i + ": " + row.Key + " has no text");
                }
            }
        }

        [Test]
        public void EveryPageIsReachableFromTheFirst()
        {
            int visited = 1;
            while (_guide.Next()) visited++;
            Assert.AreEqual(GuideState.Pages.Length, visited, "a page nobody can reach is a page nobody reads");
        }

        [Test]
        public void ThePagesLineUpWithTheIndex()
        {
            for (int i = 0; i < GuideState.Pages.Length; i++)
            {
                Assert.AreEqual(GuideState.Pages[i].Title, _guide.Page.Title,
                    "the card would show page " + _guide.Index + "'s copy under page " + i + "'s heading");
                _guide.Next();
            }
        }

        [Test]
        public void TheGuideIsTheChoice_ThenOnePagePerMode_ThenTheRun()
        {
            // The order is the argument: what the two modes are, how each one is played, then
            // what happens once the track is moving. Four pages, four progress dots.
            CollectionAssert.AreEqual(
                new[] { "TWO WAYS TO PLAY", "TILT & TOUCH", "CAMERA MODE", "DURING A RUN" },
                Array.ConvertAll(GuideState.Pages, p => p.Title));
        }

        // ---- illustrations ----

        [Test]
        public void EachModePage_CarriesItsOwnIllustration()
        {
            Assert.IsFalse(GuideState.Pages[0].HasIllustration, "the choice page gets the height for its tiles");
            Assert.AreEqual(GuideState.TiltIllustration, GuideState.Pages[1].Illustration);
            Assert.AreEqual(GuideState.CameraIllustration, GuideState.Pages[2].Illustration);
            Assert.AreEqual(GuideState.RunIllustration, GuideState.Pages[3].Illustration);
        }

        [Test]
        public void TheChoicePage_ShowsEachModeWithItsOwnPagesPicture()
        {
            // Met on the choice page, met again on that mode's page: the tile's thumbnail is the
            // first frame of the same sequence, so the two are one picture, not two.
            var tiles = GuideState.Pages[0].Rows;
            Assert.AreEqual(2, tiles.Length);
            Assert.IsTrue(tiles[0].IsTile && tiles[1].IsTile, "both choices are tiles");
            Assert.AreEqual(GuideState.Pages[1].Illustration, tiles[0].Thumbnail);
            Assert.AreEqual(GuideState.Pages[2].Illustration, tiles[1].Thumbnail);
            Assert.AreEqual("BETA", tiles[1].Tag, "camera mode is still labelled beta where it is chosen");
        }

        [Test]
        public void EveryThumbnail_NamesASequenceAPageShips()
        {
            var shipped = new System.Collections.Generic.HashSet<string>();
            foreach (GuidePage page in GuideState.Pages)
                if (page.HasIllustration) shipped.Add(page.Illustration);

            foreach (GuidePage page in GuideState.Pages)
                foreach (GuideRow row in page.Rows)
                    if (row.IsTile)
                        Assert.IsTrue(shipped.Contains(row.Thumbnail),
                            row.Key + "'s thumbnail '" + row.Thumbnail + "' has no frames to borrow");
        }

        [Test]
        public void EveryIllustratedPage_HasAtLeastOneFrameAndACaption()
        {
            foreach (GuidePage page in GuideState.Pages)
            {
                if (!page.HasIllustration)
                {
                    Assert.AreEqual(0, page.FrameCount, page.Title + " has frames but nothing to show them in");
                    continue;
                }

                Assert.GreaterOrEqual(page.FrameCount, 1, page.Title + " names a sequence with no frames");
                Assert.IsFalse(string.IsNullOrWhiteSpace(page.Caption),
                    page.Title + "'s placeholder would be a blank rectangle");
            }
        }

        [Test]
        public void IllustrationKeys_AreLowercaseAscii()
        {
            // They become file names on a case-insensitive Windows checkout and a case-sensitive
            // Android one, and Resources paths on both.
            foreach (GuidePage page in GuideState.Pages)
                if (page.HasIllustration)
                    Assert.IsTrue(GuideIllustration.IsValidKey(page.Illustration),
                        "key '" + page.Illustration + "' on " + page.Title);

            Assert.IsFalse(GuideIllustration.IsValidKey(""));
            Assert.IsFalse(GuideIllustration.IsValidKey(null));
            Assert.IsFalse(GuideIllustration.IsValidKey("Tilt"));
            Assert.IsFalse(GuideIllustration.IsValidKey("tilt_01"));
            Assert.IsFalse(GuideIllustration.IsValidKey("cámara"));
        }

        [Test]
        public void IllustrationKeys_AreUnique()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (GuidePage page in GuideState.Pages)
                if (page.HasIllustration)
                    Assert.IsTrue(seen.Add(page.Illustration), "two pages would show the same picture");
        }

        [Test]
        public void FramePath_IsTheFolderThenKeyThenTwoDigitIndex()
        {
            // The contract with the PNGs in docs/GUIDE_ILLUSTRATIONS.md: Art/Guide/<key>_NN.
            Assert.AreEqual("Art/Guide/tilt_01", GuideIllustration.FramePath("tilt", 1));
            Assert.AreEqual("Art/Guide/camera_05", GuideIllustration.FramePath("camera", 5));
            Assert.AreEqual("Art/Guide/run_12", GuideIllustration.FramePath("run", 12));
            Assert.AreEqual("tilt_04", GuideIllustration.FrameName("tilt", 4));
            Assert.AreEqual("Assets/Resources/Art/Guide/", GuideIllustration.AssetFolder,
                "the import postprocessor keys off this prefix");
        }

        [Test]
        public void FileRange_NamesWhatToDropIn()
        {
            Assert.AreEqual("tilt_01.png … tilt_04.png", GuideIllustration.FileRange("tilt", 4));
            Assert.AreEqual("run_01.png", GuideIllustration.FileRange("run", 1));
        }

        [Test]
        public void NextFrame_LoopsAndAStillNeverMoves()
        {
            Assert.AreEqual(1, GuideIllustration.NextFrame(0, 4));
            Assert.AreEqual(3, GuideIllustration.NextFrame(2, 4));
            Assert.AreEqual(0, GuideIllustration.NextFrame(3, 4), "the last frame wraps to the first");
            Assert.AreEqual(0, GuideIllustration.NextFrame(0, 1));
            Assert.AreEqual(0, GuideIllustration.NextFrame(5, 1), "a still is always frame zero");
            Assert.Greater(GuideIllustration.SecondsPerFrame, 0f);
        }

        // ---- the copy fits the card ----

        [Test]
        public void EveryPageIsOneShape()
        {
            // Steps sit under a picture, tiles take a page without one. A mixed page would put a
            // 470 px tile into the 530 px band under an illustration.
            foreach (GuidePage page in GuideState.Pages)
                foreach (GuideRow row in page.Rows)
                    Assert.AreEqual(!page.HasIllustration, row.IsTile,
                        page.Title + ": " + row.Key + " is the wrong shape for its page");
        }

        [Test]
        public void TheCopy_StaysInsideItsBudgets()
        {
            foreach (GuidePage page in GuideState.Pages)
            {
                int budget = page.HasIllustration ? GuideState.MaxSteps : GuideState.MaxTiles;
                Assert.LessOrEqual(page.Rows.Length, budget, page.Title + " has more rows than its band holds");

                if (page.Lead != null)
                    Assert.LessOrEqual(page.Lead.Length, GuideState.MaxLeadLength, page.Title + "'s lead");
                if (page.Note != null)
                    Assert.LessOrEqual(page.Note.Length, GuideState.MaxNoteLength, page.Title + "'s note");

                foreach (GuideRow row in page.Rows)
                {
                    int textBudget = row.IsTile ? GuideState.MaxTileTextLength : GuideState.MaxStepTextLength;
                    Assert.LessOrEqual(row.Text.Length, textBudget, page.Title + ": '" + row.Text + "'");
                    if (!row.IsTile)
                        Assert.LessOrEqual(row.Key.Length, GuideState.MaxKeyLength,
                            page.Title + ": chip '" + row.Key + "' is wider than the chip");
                }
            }
        }

        [Test]
        public void TheCopy_IsWrappedByTheCardNotByHand()
        {
            // Newlines were how the old body fixed its line count; now they would fight the
            // measured layout and leave the ragged right edge this layout exists to remove.
            foreach (GuidePage page in GuideState.Pages)
            {
                StringAssert.DoesNotContain("\n", page.Lead ?? string.Empty, page.Title);
                StringAssert.DoesNotContain("\n", page.Note ?? string.Empty, page.Title);
                foreach (GuideRow row in page.Rows)
                    StringAssert.DoesNotContain("\n", row.Text, page.Title + ": " + row.Key);
            }
        }
    }
}
