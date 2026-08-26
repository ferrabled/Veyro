using MotionRunner.Commerce;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// The purchase -> entitlement -> unlock chain from REVENUECAT_PLAN §3.4, run entirely
    /// against FakeStore. The real store cannot run here at all (the SDK NREs in the Editor),
    /// which is the reason the seam exists.
    public sealed class EntitlementGateTests
    {
        static FakeStore ReadyStore()
        {
            var store = FakeStore.WithDefaultCatalog();
            store.IsReady = true;
            return store;
        }

        [Test]
        public void LockedBeforePurchase()
        {
            var store = ReadyStore();
            Assert.IsFalse(CosmeticCatalog.IsUnlocked(CosmeticCatalog.EmberSkinId, store.ActiveEntitlements, 1));
            Assert.IsFalse(CosmeticCatalog.IsUnlocked(CosmeticCatalog.FrostSkinId, store.ActiveEntitlements, 1));
            Assert.IsTrue(CosmeticCatalog.IsUnlocked(CosmeticCatalog.DefaultSkinId, store.ActiveEntitlements, 1),
                "the default skin must never be locked");
        }

        [Test]
        public void UnlockedAfterPurchase()
        {
            var store = ReadyStore();
            PurchaseOutcome? outcome = null;
            store.Purchase("skin_ember", o => outcome = o);

            Assert.IsTrue(outcome.HasValue && outcome.Value.Succeeded);
            Assert.AreEqual(PurchaseStatus.Purchased, outcome.Value.Status);
            Assert.IsTrue(Entitlements.Has(store.ActiveEntitlements, Entitlements.SkinEmber));
            Assert.IsTrue(CosmeticCatalog.IsUnlocked(CosmeticCatalog.EmberSkinId, store.ActiveEntitlements, 1));
            Assert.IsFalse(CosmeticCatalog.IsUnlocked(CosmeticCatalog.FrostSkinId, store.ActiveEntitlements, 1),
                "buying one skin must not unlock the other");
        }

        [Test]
        public void StillUnlockedAfterRestore()
        {
            // A reinstall: nothing owned locally, the store account remembers the purchase.
            var store = ReadyStore();
            store.Restorable.Add(Entitlements.SkinEmber);

            PurchaseOutcome? outcome = null;
            store.Restore(o => outcome = o);

            Assert.IsTrue(outcome.HasValue && outcome.Value.Succeeded);
            Assert.AreEqual(PurchaseStatus.Restored, outcome.Value.Status);
            Assert.IsTrue(CosmeticCatalog.IsUnlocked(CosmeticCatalog.EmberSkinId, store.ActiveEntitlements, 1));
        }

        [Test]
        public void StillUnlockedWhenTheSameSetIsAnnouncedAgain()
        {
            // CustomerInfoReceived re-fires with an unchanged set (app resume, cache refresh).
            var store = ReadyStore();
            store.Purchase("skin_ember", _ => { });

            int raised = 0;
            store.EntitlementsChanged += () => raised++;
            store.RaiseEntitlementsChanged();

            Assert.AreEqual(1, raised);
            Assert.IsTrue(CosmeticCatalog.IsUnlocked(CosmeticCatalog.EmberSkinId, store.ActiveEntitlements, 1));
        }

        [Test]
        public void CancelIsNotAFailureAndUnlocksNothing()
        {
            var store = ReadyStore();
            store.NextPurchaseOutcome = PurchaseOutcome.Cancelled();

            PurchaseOutcome? outcome = null;
            store.Purchase("skin_ember", o => outcome = o);

            Assert.AreEqual(PurchaseStatus.Cancelled, outcome.Value.Status);
            Assert.IsFalse(outcome.Value.Succeeded);
            Assert.IsNull(outcome.Value.Error, "cancelling is a choice, not an error");
            Assert.IsFalse(Entitlements.Has(store.ActiveEntitlements, Entitlements.SkinEmber));
        }

        [Test]
        public void AlreadyOwnedUnlocks()
        {
            // The promo-code-redeemed-outside-the-app path (REVENUECAT_PLAN §6.3): the store
            // says "you have this already" and the app must treat that as ownership.
            var store = ReadyStore();
            store.NextPurchaseOutcome = PurchaseOutcome.AlreadyOwned();

            PurchaseOutcome? outcome = null;
            store.Purchase("skin_ember", o => outcome = o);

            Assert.IsTrue(outcome.Value.Succeeded);
            Assert.IsTrue(CosmeticCatalog.IsUnlocked(CosmeticCatalog.EmberSkinId, store.ActiveEntitlements, 1));
        }

        [Test]
        public void FailOpenWhenTheStoreNeverLoads()
        {
            // Network down / SDK unconfigured: every call completes, nothing throws,
            // nothing is granted, and the player is never blocked from running.
            var store = FakeStore.WithDefaultCatalog(); // IsReady stays false

            PurchaseOutcome? purchase = null;
            store.Purchase("skin_ember", o => purchase = o);
            Assert.AreEqual(PurchaseStatus.Failed, purchase.Value.Status);
            Assert.IsNotNull(purchase.Value.Error);

            var offers = default(System.Collections.Generic.IReadOnlyList<StoreOffer>);
            StoreError offersError = null;
            store.FetchOffers((o, e) => { offers = o; offersError = e; });
            Assert.AreEqual(0, offers.Count);
            Assert.IsNotNull(offersError);

            Assert.IsFalse(CosmeticCatalog.IsUnlocked(CosmeticCatalog.EmberSkinId, store.ActiveEntitlements, 1));
            Assert.IsTrue(CosmeticCatalog.IsUnlocked(CosmeticCatalog.DefaultSkinId, store.ActiveEntitlements, 1));
        }

        [Test]
        public void RevokedEntitlementLocksAgain()
        {
            // A refund revokes server-side; the next entitlement read must lock the item.
            var store = ReadyStore();
            store.Purchase("skin_frost", _ => { });
            Assert.IsTrue(CosmeticCatalog.IsUnlocked(CosmeticCatalog.FrostSkinId, store.ActiveEntitlements, 1));

            store.SetEntitlement(Entitlements.SkinFrost, false);
            Assert.IsFalse(CosmeticCatalog.IsUnlocked(CosmeticCatalog.FrostSkinId, store.ActiveEntitlements, 1));
        }

        [Test]
        public void SucceededCoversExactlyTheThreeOwnershipStatuses()
        {
            Assert.IsTrue(PurchaseOutcome.Purchased().Succeeded);
            Assert.IsTrue(PurchaseOutcome.Restored().Succeeded);
            Assert.IsTrue(PurchaseOutcome.AlreadyOwned().Succeeded);
            Assert.IsFalse(PurchaseOutcome.Cancelled().Succeeded);
            Assert.IsFalse(PurchaseOutcome.Failed(new StoreError("x", "y")).Succeeded);
        }

        [Test]
        public void FailedAlwaysCarriesAnError()
        {
            Assert.IsNotNull(PurchaseOutcome.Failed(null).Error,
                "diagnostics need a reason even when the adapter had none");
        }

        [Test]
        public void PaywallDefaultsToCancelledWhereNoNativePaywallExists()
        {
            var store = ReadyStore();
            PurchaseOutcome? outcome = null;
            store.PresentPaywall(o => outcome = o);
            Assert.AreEqual(PurchaseStatus.Cancelled, outcome.Value.Status);
        }
    }
}
