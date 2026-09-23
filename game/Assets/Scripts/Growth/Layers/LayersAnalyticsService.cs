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
        public bool Enabled => PlayerPrefs.GetInt(ChoiceKey, 0) == 1;
        public bool IsReady => Enabled && LayersSDK.IsInitialized;
        public string SupportId => PlayerPrefs.GetString(SupportKey, string.Empty);
        public event Action Changed;

        public static IAnalyticsService Create()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
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
            PlayerPrefs.SetInt(ChoiceKey, enabled ? 1 : 2);
            if (!enabled) PlayerPrefs.SetInt(PurgeKey, 1);
            PlayerPrefs.Save(); // choice survives a crash even if native shutdown fails
            try
            {
                if (enabled) StartSdk();
                else if (LayersConsentFlow.Disable())
                    Debug.Log("[Analytics] Optional analytics disabled; unsent events held until the next enable."); // device-test signature
            }
            catch (Exception error) { Unavailable(error); }
            Changed?.Invoke();
        }

        void StartSdk()
        {
            if (!Enabled || LayersSDK.IsInitialized) return;
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
                if (!LayersConsentFlow.Enable(config, purgePending, SupportId)) return;
                if (purgePending)
                {
                    // Only cleared once the purge has actually run; a failed
                    // initialize leaves it pending for the next attempt.
                    PlayerPrefs.DeleteKey(PurgeKey);
                    PlayerPrefs.Save();
                }
                Debug.Log("[Analytics] Optional analytics initialized; advertising disabled.");
            }
            catch (Exception error) { Unavailable(error); }
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
