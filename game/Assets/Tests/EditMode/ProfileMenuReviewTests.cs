using System;
using System.Collections.Generic;
using System.Reflection;
using MotionRunner.Social;
using NUnit.Framework;
using UnityEngine;

namespace MotionRunner.Tests
{
    // Menu code lives in Assembly-CSharp, which an asmdef cannot reference. Reflection here
    // exercises the actual compiled menu classes without reorganizing runtime assemblies.
    public sealed class ProfileMenuReviewTests
    {
        static Type MenuType(string name) => Type.GetType("MotionRunner.Menu." + name + ", Assembly-CSharp", true);

        [TestCase("ember", "frost", "purchases restored")]
        [TestCase("ember", "ember", "everything you own is already unlocked")]
        [TestCase("ember", "", "nothing to restore on this account")]
        [TestCase("", "frost", "purchases restored")]
        public void RestoreReportsEntitlementMembershipChanges(string before, string after, string expected)
        {
            var method = MenuType("StoreCatalogView").GetMethod("DescribeRestore", BindingFlags.NonPublic | BindingFlags.Static);
            var previous = new HashSet<string>();
            if (before.Length > 0) previous.Add(before);
            var current = after.Length > 0 ? new[] { after } : Array.Empty<string>();
            Assert.AreEqual(expected, method.Invoke(null, new object[] { previous, current }));
        }

        [Test]
        public void FailedBoardFetchFinishesLoadingAndNotifiesView()
        {
            var service = new DeferredProfile();
            var type = MenuType("LiveLeaderboard");
            var board = Activator.CreateInstance(type, service);
            int changes = 0;
            type.GetEvent("Changed").AddEventHandler(board, (Action)(() => changes++));
            type.GetMethod("Show").Invoke(board, new object[] { Query(BoardScope.Daily) });
            Assert.IsTrue((bool)type.GetProperty("IsLoading").GetValue(board));
            service.Requests[0](null, new SocialError("offline", "offline"));
            Assert.IsFalse((bool)type.GetProperty("IsLoading").GetValue(board));
            Assert.IsFalse((bool)type.GetProperty("IsLive").GetValue(board));
            Assert.IsNotNull(type.GetProperty("LastError").GetValue(board));
            Assert.AreEqual(2, changes);
        }

        [Test]
        public void StaleBoardFailureCannotEndNewerRequest()
        {
            var service = new DeferredProfile();
            var type = MenuType("LiveLeaderboard");
            var board = Activator.CreateInstance(type, service);
            type.GetMethod("Show").Invoke(board, new object[] { Query(BoardScope.Daily) });
            type.GetMethod("Show").Invoke(board, new object[] { Query(BoardScope.AllTime) });
            service.Requests[0](null, new SocialError("offline", "old failure"));
            Assert.IsTrue((bool)type.GetProperty("IsLoading").GetValue(board));
            service.Requests[1](new BoardResult(new List<BoardRow>(), 0, 0), null);
            Assert.IsFalse((bool)type.GetProperty("IsLoading").GetValue(board));
            Assert.IsTrue((bool)type.GetProperty("IsLive").GetValue(board));
        }

        [Test]
        public void RevisitingProfileRequiresTwoFreshDeleteTaps()
        {
            var root = new GameObject("ProfileReviewTest");
            try
            {
                var service = new DeferredProfile();
                var menuType = MenuType("MainMenu");
                var menu = root.AddComponent(menuType);
                menuType.GetProperty("Profile").SetValue(menu, service);
                var type = MenuType("ProfilePage");
                var pageObject = new GameObject("Page");
                pageObject.transform.SetParent(root.transform);
                var page = pageObject.AddComponent(type);
                type.GetMethod("Attach").Invoke(page, new object[] { menu, root.transform });
                var tap = type.GetMethod("DeleteTapped", BindingFlags.Instance | BindingFlags.NonPublic);
                tap.Invoke(page, null);
                type.GetMethod("SetVisible").Invoke(page, new object[] { false });
                type.GetMethod("SetVisible").Invoke(page, new object[] { true });
                tap.Invoke(page, null);
                Assert.AreEqual(0, service.Deletions);
                tap.Invoke(page, null);
                Assert.AreEqual(1, service.Deletions);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static BoardQuery Query(BoardScope scope) =>
            new BoardQuery(scope, "2026-09-19", "greybox-1", "greybox", "standard", 6);

        sealed class DeferredProfile : IProfileService
        {
            public bool IsReady => true;
            public Profile Current => new Profile("player", "RUNNER", 0);
            public string RecoveryCode => "";
            public event Action ProfileChanged { add { } remove { } }
            public int Deletions;
            public readonly List<Action<BoardResult, SocialError>> Requests = new List<Action<BoardResult, SocialError>>();
            public void FetchBoard(BoardQuery query, Action<BoardResult, SocialError> done) => Requests.Add(done);
            public void DeleteAccount(Action<SocialError> done) { Deletions++; }
            public void ImportProfile(string code, Action<SocialError> done) { }
            public void RerollHandle(Action<Profile, SocialError> done) { }
            public void SubmitRun(RunSubmission run, Action<SubmitOutcome> done) { }
        }
    }
}
