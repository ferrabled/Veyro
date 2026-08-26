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
    /// Anonymous app user IDs are correct for v1.0: no backend, no login (D10). T-009 later
    /// swaps in the Play Games player id via LogIn() - do not invent an ID scheme before then.
    /// SetOnesignalUserID exists on Purchases for T-021; nothing here makes that awkward.
    ///
    /// Fail-open throughout: configuration failure, no network, empty offerings - every call
    /// completes with an outcome, entitlements just stay empty, and the game never blocks on
    /// commerce (the same shape camera mode uses for rule 3).
    public sealed class RevenueCatStore : Purchases.UpdatedCustomerInfoListener, IStore
    {
        Purchases _purchases;
        string _apiKey;
        readonly HashSet<string> _active = new HashSet<string>();

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
            RefreshCustomerInfo();
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

        public void Purchase(string offerId, Action<PurchaseOutcome> done)
        {
            if (!SdkConfigured())
            {
                done?.Invoke(PurchaseOutcome.Failed(NotConfigured()));
                return;
            }

            _purchases.GetOfferings((offerings, error) =>
            {
                var package = FindPackage(offerings, offerId);
                if (error != null || package == null)
                {
                    done?.Invoke(PurchaseOutcome.Failed(ToStoreError(error) ?? new StoreError("unknown_offer", offerId)));
                    return;
                }

                _purchases.PurchasePackage(package, result =>
                {
                    if (result.CustomerInfo != null) ApplyCustomerInfo(result.CustomerInfo);

                    if (result.UserCancelled)
                    {
                        done?.Invoke(PurchaseOutcome.Cancelled());
                    }
                    else if (result.Error != null)
                    {
                        // Code 6 = ProductAlreadyPurchased: ownership, not failure - the
                        // promo-code-redeemed-outside-the-app path lands here.
                        done?.Invoke(result.Error.Code == 6
                            ? PurchaseOutcome.AlreadyOwned()
                            : PurchaseOutcome.Failed(ToStoreError(result.Error)));
                        if (result.Error.Code == 6) RefreshCustomerInfo();
                    }
                    else
                    {
                        done?.Invoke(PurchaseOutcome.Purchased());
                    }
                });
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

            // The paywall purchase happened outside our Purchase() path; pull the fresh
            // entitlements before reporting, so the UI refresh that follows sees them.
            if (outcome.Succeeded) RefreshCustomerInfo();
            done?.Invoke(outcome);
        }

        void RefreshCustomerInfo()
        {
            _purchases.GetCustomerInfo((customerInfo, error) =>
            {
                if (error != null || customerInfo == null) return; // stay fail-open; listener may still deliver later
                ApplyCustomerInfo(customerInfo);
            });
        }

        void ApplyCustomerInfo(Purchases.CustomerInfo customerInfo)
        {
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

        bool SdkConfigured() => _purchases != null && _purchases.IsConfigured();

        static StoreError NotConfigured() => new StoreError("not_configured", "store is not available yet");

        static StoreError ToStoreError(Purchases.Error error) =>
            error == null ? null : new StoreError(
                string.IsNullOrEmpty(error.ReadableErrorCode) ? error.Code.ToString() : error.ReadableErrorCode,
                error.Message);
    }
}
