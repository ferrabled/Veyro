using System;
using System.Linq;
using MotionRunner.Commerce;
using MotionRunner.Social;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    public sealed class SeasonRewardsTests
    {
        static readonly string[] None=Array.Empty<string>();
        static readonly string[] Pass={Entitlements.Season1};
        [TestCase(-1,1)][TestCase(0,1)][TestCase(1,2)][TestCase(8,9)][TestCase(9,10)][TestCase(int.MaxValue,10)]
        public void TestingCurveUsesRecordedXpAndCapsLevel(int xp,int level) => Assert.AreEqual(level,SeasonCurve.Testing.Level(xp));
        [Test]
        public void EveryLevelHasOneFreeAndOnePassReward()
        {
            var rows=CosmeticCatalog.Items.Where(x=>x.Rule.Kind==UnlockKind.SeasonLevel).ToList();
            Assert.AreEqual(20,rows.Count);
            for(int level=1;level<=10;level++)foreach(var track in new[]{SeasonTrack.Free,SeasonTrack.Pass})
                Assert.AreEqual(1,rows.Count(x=>x.Rule.Level==level && x.Rule.Track==track));
        }
        [Test]
        public void StarterRewardsAreInstantButPassOwnershipStillRequired()
        {
            var inventory=new SeasonInventory(SeasonCurve.Testing,0);
            Assert.IsTrue(inventory.IsOwned("mint",None));
            Assert.IsFalse(inventory.IsOwned("neon",None));
            Assert.IsTrue(inventory.IsOwned("neon",Pass));
            Assert.IsFalse(inventory.Collect("neon",Pass));
        }
        [Test]
        public void BuyingPassCannotSkipXpAndEarningXpDoesNotAutoCollect()
        {
            var inventory=new SeasonInventory(SeasonCurve.Testing,0);
            Assert.IsFalse(inventory.Collect("prism",Pass));
            inventory.SetRecordedXp(9);
            Assert.IsTrue(inventory.CanCollect("prism",Pass));
            Assert.IsFalse(inventory.IsOwned("prism",Pass));
            Assert.IsFalse(inventory.Collect("prism",None));
            Assert.IsTrue(inventory.Collect("prism",Pass));
            Assert.IsTrue(inventory.IsOwned("prism",Pass));
            Assert.AreEqual(9,inventory.Xp,"Collection does not spend or manufacture XP.");
        }
        [Test]
        public void RepeatedClaimsAreIdempotentAcrossReloadAndRevocation()
        {
            var inventory=new SeasonInventory(SeasonCurve.Testing,6);
            Assert.IsTrue(inventory.Collect("crown",Pass));
            Assert.IsFalse(inventory.Collect("crown",Pass));
            var loaded=new SeasonInventory(SeasonCurve.Testing,6,inventory.SaveClaims());
            Assert.IsTrue(loaded.IsOwned("crown",Pass));
            Assert.IsFalse(loaded.IsOwned("crown",None));
            Assert.IsTrue(loaded.IsOwned("crown",Pass),"Restore exposes the already-collected item.");
            loaded.SetRecordedXp(0);
            Assert.IsFalse(loaded.IsOwned("crown",Pass),"Claims alone cannot grant a higher level.");
        }
        [Test]
        public void UnknownClaimsAndForgedLoadoutNeverGrantItems()
        {
            var inventory=new SeasonInventory(SeasonCurve.Testing,0,"unknown,crown,prism");
            Assert.IsFalse(inventory.IsOwned("unknown",Pass));
            Assert.IsFalse(inventory.IsOwned("crown",Pass));
            var selected=CosmeticLoadout.Read("prism|||||");
            Assert.AreEqual("runner",selected.Validated(id=>inventory.IsOwned(id,None)).Skin);
        }
        [Test]
        public void SlotChoicesComposeAndRoundTripButPresetPartsCannotBeUnbundled()
        {
            var selected=new CosmeticLoadout();
            foreach(string id in new[]{"mint","crown","aurora","firefly","confetti"})selected.Equip(CosmeticCatalog.Find(id));
            var restored=CosmeticLoadout.Read(selected.Serialize());
            Assert.AreEqual("mint",restored.Body);Assert.AreEqual("crown",restored.Headwear);
            Assert.AreEqual("aurora",restored.Trail);Assert.AreEqual("firefly",restored.Aura);Assert.AreEqual("confetti",restored.CrashFx);
            restored.Equip(CosmeticCatalog.Find("ember"));Assert.AreEqual("ember",restored.Aura);
            restored.Equip(CosmeticCatalog.Find("cap"));
            Assert.AreEqual("cap",restored.Headwear);Assert.IsEmpty(restored.Aura);Assert.IsEmpty(restored.Trail);
        }
        [Test]
        public void RevokingOneSlotDoesNotEraseOtherEarnedSlots()
        {
            var selected=new CosmeticLoadout();selected.Equip(CosmeticCatalog.Find("mint"));selected.Equip(CosmeticCatalog.Find("crown"));
            var resolved=selected.Validated(id=>id=="mint");
            Assert.AreEqual("mint",resolved.Body);Assert.IsEmpty(resolved.Headwear);
        }
        [Test]
        public void FakeAcceptedRunsRefreshXpOnceAndDrainOfflineRuns()
        {
            var profile=new FakeProfileService();
            var run=new RunSubmission { client_run_id="test-run",score=250 };
            SubmitOutcome result=default;profile.SubmitRun(run,o=>result=o);
            Assert.AreEqual(SubmitStatus.Queued,result.Status);
            profile.BecomeReady(new Profile("player","RUNNER",3));
            Assert.AreEqual(5,profile.Current.Xp);
            profile.SubmitRun(run,o=>result=o);
            Assert.AreEqual(SubmitStatus.Duplicate,result.Status);Assert.AreEqual(5,profile.Current.Xp);
        }
        [Test]
        public void TestAndProductionCurvesKeepSeparateStorageVersions()
        {
            Assert.AreNotEqual(SeasonCurve.Testing.Id,SeasonCurve.Production.Id);
            Assert.Greater(SeasonCurve.Production.Threshold(10),SeasonCurve.Testing.Threshold(10));
        }
    }
}
