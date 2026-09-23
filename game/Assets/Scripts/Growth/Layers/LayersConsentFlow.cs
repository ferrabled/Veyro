using Layers.Unity;

namespace MotionRunner.Growth.Layers
{
    /// <summary>
    /// The handful of SDK operations the consent sequences need.
    ///
    /// <see cref="LayersSDK"/> is a static facade, so the ORDER in which
    /// <see cref="LayersConsentFlow"/> calls it cannot be observed from a
    /// test — and order is the whole privacy guarantee here (consent must be
    /// denied before <see cref="LayersSDK.Reset"/>, because Reset flushes
    /// before it drops the queue). This interface is the seam that makes the
    /// order assertable; production always uses <see cref="LayersSdkOps"/>,
    /// which forwards straight to the SDK.
    /// </summary>
    public interface ILayersSdkOps
    {
        bool IsInitialized { get; }
        void Initialize(LayersConfig config);
        void SetConsent(bool analytics, bool advertising);
        void Reset();
        void Identify(string userId);
        void Shutdown();
    }

    /// <summary>Straight pass-through to the real SDK.</summary>
    public sealed class LayersSdkOps : ILayersSdkOps
    {
        public static readonly ILayersSdkOps Default = new LayersSdkOps();

        public bool IsInitialized => LayersSDK.IsInitialized;
        public void Initialize(LayersConfig config) => LayersSDK.Initialize(config);
        public void SetConsent(bool analytics, bool advertising) =>
            LayersSDK.SetConsent(analytics: analytics, advertising: advertising);
        public void Reset() => LayersSDK.Reset();
        public void Identify(string userId) => LayersSDK.Identify(userId);
        public void Shutdown() => LayersSDK.Shutdown();
    }

    /// <summary>
    /// The enable/disable consent sequences, with no PlayerPrefs, no
    /// MonoBehaviour and no engine dependency — the caller owns the stored
    /// choice, the support ID and the purge flag.
    ///
    /// Stock SDK only: the three patch-only APIs the embedded fork added
    /// (PersistenceDirectory, EnableAndroidAttribution,
    /// RevokeConsentAndShutdown) do not exist upstream. The equivalent
    /// privacy behaviour is "purge on re-enable": while the choice is off the
    /// SDK is never initialized, so nothing it persisted can be sent, and the
    /// next enable denies consent, resets (dropping that persisted queue and
    /// rotating the device id) and only then grants analytics.
    /// </summary>
    public static class LayersConsentFlow
    {
        /// <summary>
        /// The one description of how this game configures the SDK: analytics
        /// only, no advertising, no auto-capture beyond the lifecycle events,
        /// small queue, slow flush.
        /// </summary>
        public static LayersConfig BuildConfig(string appId, bool development) => new LayersConfig
        {
            AppId = appId,
            Environment = development ? LayersEnvironment.Development : LayersEnvironment.Production,
            EnableDebug = false,
            Debug = false,
            AutoTrackAppOpen = false,
            AutoCaptureLifecycle = true,
            AutoTrackDeepLinks = false,
            AutoTrackExceptions = false,
            CaptureLogErrors = false,
            AutoTrackPerformance = false,
            FlushIntervalMs = 15000,
            FlushThreshold = 10,
            MaxQueueSize = 200,
            MaxBatchSize = 20
        };

        public static bool Enable(LayersConfig config, bool purgePending, string supportId) =>
            Enable(LayersSdkOps.Default, config, purgePending, supportId);

        /// <summary>
        /// Bring the SDK up with analytics consent. Returns false if the SDK
        /// refused to initialize, in which case nothing else ran and the
        /// caller must keep any pending purge pending.
        /// </summary>
        public static bool Enable(ILayersSdkOps sdk, LayersConfig config, bool purgePending, string supportId)
        {
            sdk.Initialize(config);
            // Initialize is synchronous and no HTTP transport runs before the
            // next frame, so every call below still precedes the first send.
            if (!sdk.IsInitialized) return false;

            if (purgePending)
            {
                // Denial FIRST: Reset() calls Flush() before it drops the
                // queue, and only a closed consent gate stops that flush from
                // delivering events collected while analytics were off.
                sdk.SetConsent(analytics: false, advertising: false);
                // Drops the persisted/queued events from the disabled period
                // and rotates the SDK's device and anonymous ids.
                sdk.Reset();
            }

            sdk.SetConsent(analytics: true, advertising: false);
            if (!string.IsNullOrEmpty(supportId)) sdk.Identify(supportId);
            return true;
        }

        public static bool Disable() => Disable(LayersSdkOps.Default);

        /// <summary>
        /// Withdraw consent and tear the SDK down. Returns false when the SDK
        /// was not running, so a second call is a no-op rather than a second
        /// shutdown flush.
        /// </summary>
        public static bool Disable(ILayersSdkOps sdk)
        {
            if (!sdk.IsInitialized) return false;
            // Consent first again: Shutdown persists the queue to disk, and
            // the gate decides whether anything may leave on the way out.
            sdk.SetConsent(analytics: false, advertising: false);
            sdk.Shutdown();
            return true;
        }
    }
}
