using System;
using System.Collections.Generic;
using System.IO;
using Layers.Unity;
using UnityEngine;

namespace MotionRunner.Growth.Layers
{
    public sealed class LayersAnalyticsService : MonoBehaviour, IAnalyticsService
    {
        public const string AppId = "app_4cf32e54fc359326";
        public const string ChoiceKey = "veyro.analytics.choice.v1";
        const string SupportKey = "veyro.analytics.support_id";
        const string PurgeKey = "veyro.analytics.purge_pending";
        // The embedded fork kept the SDK's files here via its patch-only
        // PersistenceDirectory. Stock always uses persistentDataPath itself.
        const string LegacyDirectory = "veyro-layers";
        ILayersSdkOps _sdk = LayersSdkOps.Default;
        bool _ready;
        float _retryAt;
        public bool Enabled => PlayerPrefs.GetInt(ChoiceKey, 0) == 1;
        public bool IsReady => Enabled && _ready && !DisablePending && _sdk.IsInitialized;
        public bool DisablePending { get; private set; }
        public string SupportId => PlayerPrefs.GetString(SupportKey, string.Empty);
        public event Action Changed;

        /// The real SDK on Android and on iOS, unless BuildIOS compiled the VEYRO_NO_LAYERS kill
        /// switch in (env VEYRO_IOS_NO_LAYERS=1); the Fake everywhere else. Either way it starts
        /// only after the player's opt-in (off by default). On iOS the export's ATT bridge is
        /// replaced by IosPrivacyPostProcess's stub, so the SDK reads no IDFA or IDFV and can
        /// show no tracking prompt: never call LayersSDK.RequestTrackingPermission - the build
        /// carries no NSUserTrackingUsageDescription.
        public static IAnalyticsService Create()
        {
#if !UNITY_EDITOR && (UNITY_ANDROID || (UNITY_IOS && !VEYRO_NO_LAYERS))
            var host = new GameObject("Optional analytics");
            DontDestroyOnLoad(host);
            var service = host.AddComponent<LayersAnalyticsService>();
            DeleteLegacyDirectory();
            if (service.Enabled) service.StartSdk();
            return service;
#else
            return new FakeAnalyticsService();
#endif
        }

        public void SetEnabled(bool enabled)
        {
            // Finish the previous opt-out before allowing another opt-in to
            // bypass its required purge or reuse a half-stopped SDK.
            if (enabled && DisablePending)
            {
                StopSdk();
                Changed?.Invoke();
                if (DisablePending) return;
            }
            PlayerPrefs.SetInt(ChoiceKey, enabled ? 1 : 2);
            if (!enabled) PlayerPrefs.SetInt(PurgeKey, 1);
            PlayerPrefs.Save(); // choice survives a crash even if native shutdown fails
            if (enabled) StartSdk();
            else StopSdk();
            Changed?.Invoke();
        }

        void StopSdk()
        {
            _ready = false; // gameplay events stop immediately, including during retries
            DisablePending = true;
            _retryAt = Time.realtimeSinceStartup + 1f;
            try { LayersConsentFlow.Disable(_sdk); }
            catch (Exception error) { Unavailable(error); }
            // A failed consent call can still be followed by a successful
            // shutdown. Conversely, never announce OFF while the SDK is alive.
            DisablePending = _sdk.IsInitialized;
            if (!DisablePending)
                Debug.Log("[Analytics] Optional analytics disabled; unsent events held until the next enable.");
        }

        void Update()
        {
            if (DisablePending && Time.realtimeSinceStartup >= _retryAt) RetryDisable();
        }

        void OnApplicationFocus(bool focused)
        {
            if (focused && DisablePending) RetryDisable();
        }

        void RetryDisable()
        {
            StopSdk();
            if (!DisablePending) Changed?.Invoke();
        }

        void StartSdk()
        {
            if (!Enabled || DisablePending || IsReady) return;
            try
            {
                if (string.IsNullOrEmpty(SupportId))
                {
                    // Independent installation ID: never a recovery code, email or push token.
                    PlayerPrefs.SetString(SupportKey, "veyro-" + Guid.NewGuid().ToString("N"));
                    PlayerPrefs.Save();
                }
                // Everything the previous session queued while analytics were
                // off is discarded before consent is granted again.
                bool purgePending = PlayerPrefs.GetInt(PurgeKey, 0) == 1;
                var config = LayersConsentFlow.BuildConfig(AppId, Debug.isDebugBuild);
                if (!LayersConsentFlow.Enable(_sdk, config, purgePending, SupportId)) return;
                if (purgePending)
                {
                    // Only cleared once the purge has actually run; a failed
                    // initialize leaves it pending for the next attempt.
                    PlayerPrefs.DeleteKey(PurgeKey);
                    PlayerPrefs.Save();
                }
                _ready = true;
                Debug.Log("[Analytics] Optional analytics initialized; advertising disabled.");
            }
            catch (Exception error)
            {
                Unavailable(error);
                // An incomplete enable may have initialized the SDK with its
                // default consent. Persist OFF and shut it down just as on opt-out.
                PlayerPrefs.SetInt(ChoiceKey, 2);
                PlayerPrefs.SetInt(PurgeKey, 1);
                PlayerPrefs.Save();
                StopSdk();
            }
        }

        public void Track(string name, Dictionary<string, object> properties)
        {
            if (!IsReady) return;
            try { LayersSDK.Track(name, properties); }
            catch (Exception error) { Unavailable(error); }
        }

        /// <summary>
        /// One-shot cleanup of the forked SDK's private folder. It held only
        /// that build's event queue and identity record — the consent choice
        /// and the support ID live in PlayerPrefs and are untouched.
        /// </summary>
        static void DeleteLegacyDirectory()
        {
            try
            {
                // Only this SDK's dedicated child directory; never delete game/profile data.
                string parent = Path.GetFullPath(Application.persistentDataPath);
                string target = Path.GetFullPath(
                    Path.Combine(Application.persistentDataPath, LegacyDirectory));
                if (Path.GetDirectoryName(target) != parent || Path.GetFileName(target) != LegacyDirectory)
                    return;
                if (Directory.Exists(target)) Directory.Delete(target, true);
            }
            catch (Exception error) { Unavailable(error); }
        }

        static void Unavailable(Exception error) =>
            Debug.LogWarning("[Analytics] Unavailable: " + error.GetType().Name);
    }
}
