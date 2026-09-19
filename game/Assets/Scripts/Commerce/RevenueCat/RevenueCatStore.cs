using System;
using System.Collections;
using System.Collections.Generic;
using RevenueCatUI;
using UnityEngine;

namespace MotionRunner.Commerce.RevenueCat
{
    /// IStore over the RevenueCat SDK (REVENUECAT_PLAN §3.3). The only class in the game that
    /// sees a RevenueCat type. Never constructed in the Editor - the SDK NREs there (§2.5);
    /// GameBootstrap hands the Editor a FakeStore instead.
    ///
    /// Anonymous app user IDs are the boot state; T-009's profile backend then calls
    /// Identify(supabaseUserId) -> LogIn(), so RevenueCat, Supabase and (later, T-021)
    /// OneSignal share one stable player id (D14; supersedes the old Play-Games-id
    /// note). SetOnesignalUserID exists on Purchases for T-021; nothing here makes that awkward.
    ///
    /// Fail-open throughout: configuration failure, no network, empty offerings - every call
    /// completes with an outcome, entitlements just stay empty, and the game never blocks on
    /// commerce (the same shape camera mode uses for rule 3).
    public sealed class RevenueCatStore : Purchases.UpdatedCustomerInfoListener, IStore
    {
        /// RevenueCat error codes we branch on. The numbering is shared across every
        /// RevenueCat SDK (purchases-ios ErrorCode.swift is the generated source of truth);
        /// the Unity SDK only hands us the int, so name them here rather than in-line.
        const int ErrorPurchaseCancelled = 1;
        const int ErrorProductAlreadyPurchased = 6;
        const int ErrorPaymentPending = 20;

        Purchases _purchases;
        string _apiKey;
        bool _configured;
        readonly HashSet<string> _active = new HashSet<string>();

        /// Last offering the SDK handed us. Kept so a direct-purchase tap reaches the Google
        /// Play sheet without a second round trip - the store panel has already fetched it
        /// for the prices it shows. Packages are plain data (an id plus a product), so the
        /// only staleness risk is an offering edited in the dashboard mid-session.
        Purchases.Offerings _offerings;

        public bool IsReady { get; private set; }

        public IReadOnlyCollection<string> ActiveEntitlements => _active;

        public event Action EntitlementsChanged;

        /// One persistent GameObject carries both the SDK component and this adapter; the
        /// native side routes callbacks to it by name, so it must survive scene loads.
        public static RevenueCatStore Create(string apiKey)
        {
            var go = new GameObject("RevenueCat");
            DontDestroyOnLoad(go);
            var store = go.AddComponent<RevenueCatStore>();
            store._apiKey = apiKey;

            var purchases = go.AddComponent<Purchases>();
            // Must be set before Purchases.Start() runs, or the component self-configures
            // with its (empty) inspector keys.
            purchases.useRuntimeSetup = true;
            purchases.listener = store;
            store._purchases = purchases;

            store.StartCoroutine(store.ConfigureWhenWrapperReady());
            return store;
        }

        /// Purchases binds its platform wrapper in Start(); Configure before that throws.
        /// One frame is enough and keeps boot non-blocking.
        IEnumerator ConfigureWhenWrapperReady()
        {
            yield return null;
            try
            {
                // Null app user id -> RevenueCat generates an anonymous one (§3.3).
                var builder = Purchases.PurchasesConfiguration.Builder.Init(_apiKey);
                _purchases.Configure(builder.Build());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Store] RevenueCat configuration failed - store disabled, game playable: " + e.Message);
                yield break;
            }
            _configured = true;
            RefreshCustomerInfo();
            SyncIdentity();
        }

        /// Identify arriving before the SDK is configured (profile backend answered first) is
        /// held and applied right after Configure - the same one-frame dance as everything else.
        readonly StoreIdentity _identity = new StoreIdentity();
        float _identityRetryAt;

        public void Identify(string userId)
        {
            _identity.Identify(userId);
            SyncIdentity();
        }

        /// Account deletion's identity reset: LogOut returns the SDK to a fresh anonymous app
        /// user id, so the deleted player's UUID stops accruing provider data (the server-side
        /// customer record is deleted by the delete-account Edge Function). Failed operations
        /// remain pending and retry; an in-flight login must finish before logout starts.
        public void ResetIdentity()
        {
            _identity.Reset();
            _identityRetryAt = 0f;
            SyncIdentity();
        }

        void Update() => SyncIdentity();

        void SyncIdentity()
        {
            if (!SdkConfigured() || Time.realtimeSinceStartup < _identityRetryAt ||
                !_identity.TryBegin(out var userId, out var generation)) return;

            void Completed(Purchases.CustomerInfo customerInfo, Purchases.Error error)
            {
                bool current = _identity.Complete(userId, generation, error == null);
                if (error != null)
                {
                    _identityRetryAt = Time.realtimeSinceStartup + 5f;
                    Debug.LogWarning("[Store] Identity update failed - will retry: " + error.Message);
                    return;
                }
                _identityRetryAt = 0f;
                if (current && customerInfo != null) ApplyCustomerInfo(customerInfo);
                SyncIdentity();
            }

            if (userId == null && _purchases.IsAnonymous())
            {
                Completed(null, null);
                RefreshCustomerInfo();
            }
            else if (userId == null) _purchases.LogOut((info, error) => Completed(info, error));
            else _purchases.LogIn(userId, (info, created, error) => Completed(info, error));
        }

        /// Listener path: fires on launch, after purchases, and after out-of-app changes
        /// (promo redemption, refund) once the SDK notices them.
        public override void CustomerInfoReceived(Purchases.CustomerInfo customerInfo)
        {
            ApplyCustomerInfo(customerInfo);
        }

        /// Play Billing requires processing purchases completed outside the app (a judge
        /// redeeming a promo code in the Play Store app - REVENUECAT_PLAN §6.3). RevenueCat
        /// applies the entitlement server-side; dropping the cache on resume makes this
        /// session see it now rather than eventually.
        void OnApplicationPause(bool paused)
        {
            if (paused || _purchases == null || !SdkConfigured()) return;
            _purchases.InvalidateCustomerInfoCache();
            RefreshCustomerInfo();
        }

        public void FetchOffers(Action<IReadOnlyList<StoreOffer>, StoreError> done)
        {
            if (!SdkConfigured())
            {
                done?.Invoke(Array.Empty<StoreOffer>(), NotConfigured());
                return;
            }

            _purchases.GetOfferings((offerings, error) =>
            {
                if (offerings != null) _offerings = offerings;
                if (error != null || offerings?.Current?.AvailablePackages == null)
                {
                    done?.Invoke(Array.Empty<StoreOffer>(), ToStoreError(error) ?? new StoreError("no_offering", "no current offering"));
                    return;
                }

                var offers = new List<StoreOffer>(offerings.Current.AvailablePackages.Count);
                foreach (var package in offerings.Current.AvailablePackages)
                {
                    offers.Add(new StoreOffer(
                        package.Identifier,
                        package.StoreProduct?.Title,
                        package.StoreProduct?.PriceString,
                        Entitlements.ForPackage(package.Identifier)));
                }
                done?.Invoke(offers, null);
            });
        }

        /// The direct-purchase path: one tap on a skin row raises the native Google Play
        /// sheet, no paywall in between (owner call, 29 Aug). The season pass still goes
        /// through PresentPaywall - that is the offering the Paywall Builder is designed for.
        public void Purchase(string offerId, Action<PurchaseOutcome> done)
        {
            if (!SdkConfigured())
            {
                done?.Invoke(PurchaseOutcome.Failed(NotConfigured()));
                return;
            }

            // Straight to the sheet when the offering is already in hand; otherwise fetch it
            // first, which is also what makes a cold tap (panel opened offline, network back)
            // work rather than fail.
            var known = FindPackage(_offerings, offerId);
            if (known != null)
            {
                Buy(known, done);
                return;
            }

            _purchases.GetOfferings((offerings, error) =>
            {
                if (offerings != null) _offerings = offerings;
                var package = FindPackage(offerings, offerId);
                if (error != null || package == null)
                {
                    done?.Invoke(PurchaseOutcome.Failed(ToStoreError(error) ?? new StoreError("unknown_offer", offerId)));
                    return;
                }
                Buy(package, done);
            });
        }

        /// PurchasePackage, never PurchaseProduct: the product overload defaults to type
        /// "subs" and all three of our SKUs are one-time non-consumables, and the SDK's own
        /// guidance is to use the package call whenever the Offerings system is in play.
        void Buy(Purchases.Package package, Action<PurchaseOutcome> done)
        {
            _purchases.PurchasePackage(package, result =>
            {
                // Entitlements before the callback, always: whoever handles the outcome
                // (the store rows, SkinService) must already see the unlock, so a bought
                // skin applies immediately instead of after a restart.
                if (result.CustomerInfo != null) ApplyCustomerInfo(result.CustomerInfo);

                int code = result.Error?.Code ?? 0;

                // Backing out of the sheet is a choice, not an error - report it silently and
                // leave the store usable. Android surfaces it as UserCancelled; code 1 is the
                // same event seen as an error, and either one can arrive.
                if (result.UserCancelled || code == ErrorPurchaseCancelled)
                {
                    done?.Invoke(PurchaseOutcome.Cancelled());
                    return;
                }

                if (result.Error == null)
                {
                    done?.Invoke(PurchaseOutcome.Purchased());
                    return;
                }

                if (code == ErrorProductAlreadyPurchased)
                {
                    // Ownership, not failure - the promo-code-redeemed-outside-the-app path
                    // lands here. This error carries no customer info, so the entitlement is
                    // not applied yet: report only once the refresh lands, or the caller
                    // equips a skin that is still locked and equipping never grants.
                    RefreshCustomerInfo(() => done?.Invoke(PurchaseOutcome.AlreadyOwned()));
                    return;
                }

                if (code == ErrorPaymentPending)
                {
                    // Order placed, money not taken (cash/voucher payment, parental approval,
                    // SCA). Granting here would hand out an unpaid skin; the entitlement
                    // arrives on its own and the resume re-read in OnApplicationPause is what
                    // picks it up once Google confirms.
                    done?.Invoke(PurchaseOutcome.Pending());
                    return;
                }

                done?.Invoke(PurchaseOutcome.Failed(ToStoreError(result.Error)));
            });
        }

        public void Restore(Action<PurchaseOutcome> done)
        {
            if (!SdkConfigured())
            {
                done?.Invoke(PurchaseOutcome.Failed(NotConfigured()));
                return;
            }

            _purchases.RestorePurchases((customerInfo, error) =>
            {
                if (error != null)
                {
                    done?.Invoke(PurchaseOutcome.Failed(ToStoreError(error)));
                    return;
                }
                if (customerInfo != null) ApplyCustomerInfo(customerInfo);
                done?.Invoke(PurchaseOutcome.Restored());
            });
        }

        /// The season pass's path (owner call, 29 Aug): the dashboard paywall is what sells
        /// the pass, and it stays server-side revisable (REVENUECAT_PLAN §5).
        /// Present, never PresentIfNeeded: with three independent entitlements no single id
        /// answers "already owned" - the call site gates instead (REVENUECAT_PLAN §3.3).
        public void PresentPaywall(Action<PurchaseOutcome> done)
        {
            if (!SdkConfigured())
            {
                done?.Invoke(PurchaseOutcome.Failed(NotConfigured()));
                return;
            }
            PresentPaywallAsync(done);
        }

        async void PresentPaywallAsync(Action<PurchaseOutcome> done)
        {
            PurchaseOutcome outcome;
            try
            {
                var result = await PaywallsPresenter.Present();
                switch (result.Result)
                {
                    case PaywallResultType.Purchased:
                        outcome = PurchaseOutcome.Purchased();
                        break;
                    case PaywallResultType.Restored:
                        outcome = PurchaseOutcome.Restored();
                        break;
                    case PaywallResultType.Cancelled:
                        outcome = PurchaseOutcome.Cancelled();
                        break;
                    case PaywallResultType.NotPresented:
                        outcome = PurchaseOutcome.Failed(new StoreError("not_presented", "paywall could not be shown"));
                        break;
                    default:
                        outcome = PurchaseOutcome.Failed(new StoreError("paywall_error", "paywall failed"));
                        break;
                }
            }
            catch (Exception e)
            {
                outcome = PurchaseOutcome.Failed(new StoreError("paywall_exception", e.Message));
            }

            // The paywall purchase happened outside our Purchase() path, so nothing has
            // applied its entitlements yet. Report only once they are in, or the UI refresh
            // that follows still draws the pass as locked.
            if (outcome.Succeeded) RefreshCustomerInfo(() => done?.Invoke(outcome));
            else done?.Invoke(outcome);
        }

        /// `done` runs when the refresh has settled, applied or not. Callers that owe
        /// IStore.Purchase's "entitlements are up to date by the time `done` runs" promise
        /// wait on it rather than firing their own callback a frame early.
        /// A failed refresh still completes: fail-open means the outcome is reported with
        /// whatever entitlements we have, never withheld - the listener may deliver later.
        void RefreshCustomerInfo(Action done = null)
        {
            _purchases.GetCustomerInfo((customerInfo, error) =>
            {
                if (error != null || customerInfo == null)
                    Debug.LogWarning("[Store] Could not refresh entitlements - reporting with what we have: " +
                                     (error != null ? error.Message : "no customer info"));
                else
                    ApplyCustomerInfo(customerInfo);
                done?.Invoke();
            });
        }

        void ApplyCustomerInfo(Purchases.CustomerInfo customerInfo)
        {
            if (!_identity.Settled) return;
            if (customerInfo?.Entitlements?.Active == null) return;

            bool firstAnswer = !IsReady;
            IsReady = true;

            var fresh = customerInfo.Entitlements.Active.Keys;
            bool changed = _active.Count != customerInfo.Entitlements.Active.Count;
            if (!changed)
                foreach (var id in fresh)
                    if (!_active.Contains(id)) { changed = true; break; }

            if (!changed && !firstAnswer) return;
            _active.Clear();
            foreach (var id in fresh) _active.Add(id);
            EntitlementsChanged?.Invoke();
        }

        static Purchases.Package FindPackage(Purchases.Offerings offerings, string offerId)
        {
            var packages = offerings?.Current?.AvailablePackages;
            if (packages == null) return null;
            foreach (var package in packages)
                if (package.Identifier == offerId)
                    return package;
            return null;
        }

        /// Our own flag, never Purchases.IsConfigured(): that call NREs before the SDK
        /// component's Start() binds its platform wrapper, and Unity delivers the first
        /// OnApplicationPause(false) earlier than that (found on device, 27 Aug).
        bool SdkConfigured() => _purchases != null && _configured;

        static StoreError NotConfigured() => new StoreError("not_configured", "store is not available yet");

        static StoreError ToStoreError(Purchases.Error error) =>
            error == null ? null : new StoreError(
                string.IsNullOrEmpty(error.ReadableErrorCode) ? error.Code.ToString() : error.ReadableErrorCode,
                error.Message);
    }
}
