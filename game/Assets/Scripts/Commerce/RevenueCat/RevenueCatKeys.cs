namespace MotionRunner.Commerce.RevenueCat
{
    /// RevenueCat PUBLIC SDK keys (dashboard -> API keys). Public keys ship inside the binary
    /// by design and are safe to commit; the SECRET (sk_) keys must never appear in this repo.
    ///
    /// Key selection is compile-time: BuildScript defines VEYRO_STORE_BUILD for .aab (store)
    /// builds only, and build guards fail any wrong pairing - so the Test Store key cannot
    /// ship to Play (COSMETICS_CATALOG §6: "The test key must never ship in a store build").
    ///
    /// Empty keys are fail-open, not fatal, for dev builds: the store stays disabled and the
    /// game fully playable. A store (.aab) build REQUIRES a valid Play key and will not build
    /// without one.
    public static class RevenueCatKeys
    {
        /// RevenueCat -> Veyro Run -> API keys -> Test Store app. Starts with "test_".
        public const string TestStoreKey = "test_gzgHegyNkJUpVzThoFcQeCdaWtC";

        /// RevenueCat -> Veyro Run -> API keys -> Play Store app. Starts with "goog_".
        public const string PlayStoreKey = "goog_EFrxXlNiBEnmurJnEilutfuyGRo";

#if VEYRO_STORE_BUILD
        public const string ActiveKey = PlayStoreKey;
#else
        public const string ActiveKey = TestStoreKey;
#endif
    }
}
