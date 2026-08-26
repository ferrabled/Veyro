using System;
using System.Collections.Generic;

namespace MotionRunner.Commerce
{
    /// Everything gameplay is allowed to know about money.
    /// No RevenueCat type crosses this boundary - that is the whole point
    /// (CLAUDE.md rule 5; design in docs/REVENUECAT_PLAN.md §3).
    ///
    /// Callback-shaped, not async: it matches the SDK's own delegate style and keeps this
    /// assembly engine-free without pulling in a Task scheduler.
    public interface IStore
    {
        /// True once the backend has answered at least once. Until then, treat
        /// every entitlement as locked but never block the player from running.
        bool IsReady { get; }

        /// Active entitlement ids, e.g. { "skin_ember", "season1" }. Empty when unknown.
        IReadOnlyCollection<string> ActiveEntitlements { get; }

        /// Raised whenever ActiveEntitlements changes, including at startup and
        /// after a promo-code redemption made outside the app.
        event Action EntitlementsChanged;

        /// The current offering, flattened. Empty if offerings have not loaded.
        void FetchOffers(Action<IReadOnlyList<StoreOffer>, StoreError> done);

        void Purchase(string offerId, Action<PurchaseOutcome> done);

        void Restore(Action<PurchaseOutcome> done);

        /// Presents the dashboard-configured paywall for offering `default`.
        /// Deliberately takes NO required-entitlement argument: the catalog has three
        /// independent entitlements, so "does the user already have it" is never a single
        /// question - gate the call site instead (REVENUECAT_PLAN §3.3: Present, never
        /// PresentIfNeeded). Where no native paywall exists (Editor), completes as Cancelled.
        void PresentPaywall(Action<PurchaseOutcome> done);
    }
}
