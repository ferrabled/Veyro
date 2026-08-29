using System.Collections.Generic;
using System.Linq;
using MotionRunner.Commerce;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// Catalog integrity (REVENUECAT_PLAN §3.4) plus the unlock-rule truth table from
    /// COSMETICS_CATALOG §5: Default | Entitlement(id) | SeasonLevel(track, level).
    public sealed class CosmeticCatalogTests
    {
        static readonly string[] None = System.Array.Empty<string>();

        [Test]
        public void ItemIdsAreUnique()
        {
            var ids = CosmeticCatalog.Items.Select(i => i.Id).ToList();
            Assert.AreEqual(ids.Count, ids.Distinct().Count(), "two catalog items share an id");
        }

        [Test]
        public void EveryEntitlementRuleReferencesARealEntitlement()
        {
            foreach (var item in CosmeticCatalog.Items)
            {
                if (item.Rule.Kind != UnlockKind.Entitlement) continue;
                Assert.Contains(item.Rule.EntitlementId, (System.Collections.ICollection)Entitlements.All,
                    item.Id + " is gated on an entitlement the dashboard does not define");
            }
        }

        [Test]
        public void NoTwoItemsSellTheSameEntitlement()
        {
            // One product -> one entitlement -> one skin preset in v1.0. A second item on the
            // same entitlement would make "which skin did I buy" ambiguous in the store UI.
            var gated = CosmeticCatalog.Items
                .Where(i => i.Rule.Kind == UnlockKind.Entitlement)
                .Select(i => i.Rule.EntitlementId)
                .ToList();
            Assert.AreEqual(gated.Count, gated.Distinct().Count());
        }

        [Test]
        public void ExactlyOneDefaultSkinAndItIsTheOriginalOrange()
        {
            var defaults = CosmeticCatalog.Items.Where(i => i.Rule.Kind == UnlockKind.Default).ToList();
            Assert.AreEqual(1, defaults.Count);
            Assert.AreEqual(CosmeticCatalog.DefaultSkinId, defaults[0].Id);

            // Pinned to the pre-T-020 hard-coded tint so an entitlement-less build renders
            // exactly as every build before the store existed.
            Assert.AreEqual(1f, defaults[0].BodyColor.R);
            Assert.AreEqual(0.55f, defaults[0].BodyColor.G);
            Assert.AreEqual(0.15f, defaults[0].BodyColor.B);
        }

        [Test]
        public void BothPremiumSkinsExistAndAreEntitlementGated()
        {
            var ember = CosmeticCatalog.Find(CosmeticCatalog.EmberSkinId);
            var frost = CosmeticCatalog.Find(CosmeticCatalog.FrostSkinId);
            Assert.AreEqual(Entitlements.SkinEmber, ember.Rule.EntitlementId);
            Assert.AreEqual(Entitlements.SkinFrost, frost.Rule.EntitlementId);
        }

        [Test]
        public void UnknownIdIsLockedNotThrowing()
        {
            Assert.IsNull(CosmeticCatalog.Find("does_not_exist"));
            Assert.IsFalse(CosmeticCatalog.IsUnlocked("does_not_exist", new[] { Entitlements.SkinEmber }, 10));
        }

        [Test]
        public void PackageToEntitlementMapCoversTheOfferingAndNothingElse()
        {
            Assert.AreEqual(Entitlements.SkinEmber, Entitlements.ForPackage("skin_ember"));
            Assert.AreEqual(Entitlements.SkinFrost, Entitlements.ForPackage("skin_frost"));
            Assert.AreEqual(Entitlements.Season1, Entitlements.ForPackage("season1_pass"));
            Assert.IsNull(Entitlements.ForPackage("supporter_bundle"),
                "a package added to the dashboard later must degrade gracefully, not crash");
            Assert.IsNull(Entitlements.ForPackage(null));
        }

        [Test]
        public void EntitlementToPackageMapIsTheExactInverse()
        {
            // The direct-purchase tap (owner call, 29 Aug) turns a locked item's entitlement
            // into the package id it hands IStore.Purchase. If the two maps ever disagree,
            // tapping a skin buys the wrong thing or nothing at all.
            foreach (var entitlement in Entitlements.All)
            {
                var package = Entitlements.PackageFor(entitlement);
                Assert.IsNotNull(package, entitlement + " has no package to buy it through");
                Assert.AreEqual(entitlement, Entitlements.ForPackage(package));
            }
            Assert.IsNull(Entitlements.PackageFor("cosmetics"),
                "an entitlement this build does not sell must degrade to 'not for sale', not crash");
            Assert.IsNull(Entitlements.PackageFor(null));
        }

        [Test]
        public void EverySellableSkinHasAPackageBehindIt()
        {
            // The store panel derives each row's package from its unlock rule; a skin whose
            // entitlement maps to nothing would render as locked with no way to buy it.
            foreach (var item in CosmeticCatalog.Items)
            {
                if (item.Rule.Kind != UnlockKind.Entitlement) continue;
                Assert.IsNotNull(Entitlements.PackageFor(item.Rule.EntitlementId),
                    item.Id + " is locked behind an entitlement no package sells");
            }
        }

        [Test]
        public void SeasonLevelRule_FreeTrackNeedsOnlyTheLevel()
        {
            var rule = UnlockRule.SeasonLevel(SeasonTrack.Free, 3);
            Assert.IsFalse(rule.IsUnlocked(None, 2), "below the level");
            Assert.IsTrue(rule.IsUnlocked(None, 3), "at the level, no purchase needed");
            Assert.IsTrue(rule.IsUnlocked(None, 9), "above the level");
        }

        [Test]
        public void SeasonLevelRule_PassTrackNeedsLevelAndSeason1()
        {
            var rule = UnlockRule.SeasonLevel(SeasonTrack.Pass, 3);
            var owned = new[] { Entitlements.Season1 };

            Assert.IsFalse(rule.IsUnlocked(None, 5), "level without the pass stays locked");
            Assert.IsFalse(rule.IsUnlocked(owned, 2), "the pass without the level stays locked");
            Assert.IsTrue(rule.IsUnlocked(owned, 3));
        }

        [Test]
        public void SeasonLevelRule_OtherEntitlementsDoNotOpenThePassTrack()
        {
            var rule = UnlockRule.SeasonLevel(SeasonTrack.Pass, 1);
            Assert.IsFalse(rule.IsUnlocked(new[] { Entitlements.SkinEmber, Entitlements.SkinFrost }, 10));
        }

        [Test]
        public void RulesNeverThrowOnNullEntitlements()
        {
            // Fail-open all the way down: a store that never loaded hands the catalog null/empty.
            Assert.IsTrue(UnlockRule.Default().IsUnlocked(null, 0));
            Assert.IsFalse(UnlockRule.Entitlement(Entitlements.SkinEmber).IsUnlocked(null, 0));
            Assert.IsFalse(UnlockRule.SeasonLevel(SeasonTrack.Pass, 1).IsUnlocked(null, 5));
            Assert.IsTrue(UnlockRule.SeasonLevel(SeasonTrack.Free, 1).IsUnlocked(null, 5));
        }
    }
}
