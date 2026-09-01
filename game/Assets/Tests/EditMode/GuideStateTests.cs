using System;
using MotionRunner.Track;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// The first-run guide's gate and its paging. Both are the kind of thing that looks right the
    /// one time it is tapped through on a phone and is wrong for ever afterwards: the flag is only
    /// ever read on a device that has already seen the guide, and the last page's button is only
    /// reached by someone who read all three.
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
                Assert.IsFalse(string.IsNullOrEmpty(GuideState.Pages[i].Body), "page " + i + " has no body");
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
    }
}
