using System;
using System.Collections.Generic;

namespace MotionRunner.Commerce
{
    /// In-memory IStore for EditMode tests and for the Editor, where the RevenueCat SDK
    /// cannot run at all (REVENUECAT_PLAN §2.5). Purchases grant the offer's entitlement
    /// immediately; knobs below script the outcomes the tests in §3.4 need.
    public sealed class FakeStore : IStore
    {
        readonly HashSet<string> _active = new HashSet<string>();
        readonly List<StoreOffer> _offers = new List<StoreOffer>();

        /// What Restore() brings back - the entitlements this "store account" already owns.
        public HashSet<string> Restorable { get; } = new HashSet<string>();

        /// Scripted result for the next Purchase call; null means "complete normally".
        public PurchaseOutcome? NextPurchaseOutcome { get; set; }

        /// Scripted result for PresentPaywall; defaults to Cancelled like the Editor path.
        public PurchaseOutcome? PaywallOutcome { get; set; }

        public bool IsReady { get; set; }

        public IReadOnlyCollection<string> ActiveEntitlements => _active;

        public event Action EntitlementsChanged;

        public FakeStore(params StoreOffer[] offers)
        {
            _offers.AddRange(offers);
        }

        public static FakeStore WithDefaultCatalog() => new FakeStore(
            new StoreOffer("skin_ember", "Ember Skin", "€2.99", Entitlements.SkinEmber),
            new StoreOffer("skin_frost", "Frost Skin", "€2.99", Entitlements.SkinFrost),
            new StoreOffer("season1_pass", "Season 1 Pass", "€4.99", Entitlements.Season1));

        public void FetchOffers(Action<IReadOnlyList<StoreOffer>, StoreError> done)
        {
            if (!IsReady)
            {
                done?.Invoke(Array.Empty<StoreOffer>(), new StoreError("not_ready", "store has not loaded"));
                return;
            }
            done?.Invoke(_offers, null);
        }

        public void Purchase(string offerId, Action<PurchaseOutcome> done)
        {
            if (NextPurchaseOutcome is PurchaseOutcome scripted)
            {
                NextPurchaseOutcome = null;
                // AlreadyOwned still means the entitlement is active - the out-of-app
                // promo-redemption path (REVENUECAT_PLAN §6.3).
                if (scripted.Status == PurchaseStatus.AlreadyOwned) GrantByOffer(offerId);
                done?.Invoke(scripted);
                return;
            }

            if (!IsReady)
            {
                done?.Invoke(PurchaseOutcome.Failed(new StoreError("not_ready", "store has not loaded")));
                return;
            }

            if (!GrantByOffer(offerId))
            {
                done?.Invoke(PurchaseOutcome.Failed(new StoreError("unknown_offer", offerId)));
                return;
            }
            done?.Invoke(PurchaseOutcome.Purchased());
        }

        public void Restore(Action<PurchaseOutcome> done)
        {
            bool changed = false;
            foreach (var id in Restorable) changed |= _active.Add(id);
            if (changed) EntitlementsChanged?.Invoke();
            done?.Invoke(PurchaseOutcome.Restored());
        }

        public void PresentPaywall(Action<PurchaseOutcome> done)
        {
            done?.Invoke(PaywallOutcome ?? PurchaseOutcome.Cancelled());
        }

        /// Last id handed to Identify, for tests. The fake has no server to alias on.
        public string Identity { get; private set; }

        public void Identify(string userId) => Identity = userId;

        public void ResetIdentity() => Identity = null;

        /// Backdoor for tests and Editor debugging: grant/revoke directly, as the dashboard can.
        public void SetEntitlement(string entitlementId, bool active)
        {
            bool changed = active ? _active.Add(entitlementId) : _active.Remove(entitlementId);
            if (changed) EntitlementsChanged?.Invoke();
        }

        /// Re-announce the current set unchanged - the "listener fired again" case tests cover.
        public void RaiseEntitlementsChanged() => EntitlementsChanged?.Invoke();

        bool GrantByOffer(string offerId)
        {
            foreach (var offer in _offers)
            {
                if (offer.Id != offerId) continue;
                if (_active.Add(offer.EntitlementId)) EntitlementsChanged?.Invoke();
                return true;
            }
            return false;
        }
    }
}
