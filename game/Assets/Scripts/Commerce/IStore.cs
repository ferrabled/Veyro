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

        /// Buys one package of the current offering directly - on Android this raises the
        /// native Google Play purchase sheet with no paywall in between (owner call,
        /// 29 Aug: the two skins buy this way, the season pass keeps the paywall). Always
        /// completes: Cancelled when the player backs out, Pending when Google has the
        /// order but not the money, AlreadyOwned when the store says they already have it.
        /// Entitlements are up to date by the time `done` runs, so the caller can unlock
        /// immediately without a restart or a second fetch.
        void Purchase(string offerId, Action<PurchaseOutcome> done);

        void Restore(Action<PurchaseOutcome> done);

        /// Aliases this install's store identity to a stable player id — the Supabase user
        /// UUID (T-009, D13 proposal; supersedes the Play-Games-id note in REVENUECAT_PLAN
        /// §3.3). Safe to call before the backend is configured (the adapter applies it once
        /// ready) and idempotent for the same id. Entitlements survive either way — receipts
        /// are the source of truth — this only makes the ids line up across services.
        void Identify(string userId);

        /// Presents the dashboard-configured paywall for offering `default`.
        /// Deliberately takes NO required-entitlement argument: the catalog has three
        /// independent entitlements, so "does the user already have it" is never a single
        /// question - gate the call site instead (REVENUECAT_PLAN §3.3: Present, never
        /// PresentIfNeeded). Where no native paywall exists (Editor), completes as Cancelled.
        void PresentPaywall(Action<PurchaseOutcome> done);
    }
}
