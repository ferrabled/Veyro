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
        public bool Enabled => PlayerPrefs.GetInt(ChoiceKey, 0) == 1;
        public bool IsReady => Enabled && LayersSDK.IsInitialized;
        public string SupportId => PlayerPrefs.GetString(SupportKey, string.Empty);
        public event Action Changed;
        static string DataDirectory => Path.Combine(Application.persistentDataPath, "veyro-layers");

        public static IAnalyticsService Create()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var host = new GameObject("Optional analytics");
            DontDestroyOnLoad(host);
            var service = host.AddComponent<LayersAnalyticsService>();
            if (PlayerPrefs.GetInt(PurgeKey, 0) == 1) service.ClearQueueFiles();
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
                else
                {
                    LayersSDK.RevokeConsentAndShutdown();
                    ClearQueueFiles();
                }
            }
            catch (Exception error) { Unavailable(error); }
            Changed?.Invoke();
        }

        void StartSdk()
        {
            if (!Enabled || LayersSDK.IsInitialized) return;
            if (PlayerPrefs.GetInt(PurgeKey, 0) == 1 && !ClearQueueFiles()) return;
            try
            {
                if (string.IsNullOrEmpty(SupportId))
                {
                    // Independent installation ID: never a recovery code, email or push token.
                    PlayerPrefs.SetString(SupportKey, "veyro-" + Guid.NewGuid().ToString("N"));
                    PlayerPrefs.Save();
                }
                Directory.CreateDirectory(DataDirectory);
                LayersSDK.Initialize(new LayersConfig
                {
                    AppId = AppId,
                    Environment = Debug.isDebugBuild ? LayersEnvironment.Development : LayersEnvironment.Production,
                    PersistenceDirectory = DataDirectory,
                    EnableAndroidAttribution = false,
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
                });
                if (!LayersSDK.IsInitialized) return;
                LayersSDK.Identify(SupportId);
                LayersSDK.SetConsent(analytics: true, advertising: false);
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

        bool ClearQueueFiles()
        {
            try
            {
                // Only this SDK's dedicated child directory; never delete game/profile data.
                string parent = Path.GetFullPath(Application.persistentDataPath);
                string target = Path.GetFullPath(DataDirectory);
                if (Path.GetDirectoryName(target) != parent || Path.GetFileName(target) != "veyro-layers")
                    return false;
                if (Directory.Exists(target)) Directory.Delete(target, true);
                PlayerPrefs.DeleteKey(PurgeKey);
                PlayerPrefs.Save();
                return true;
            }
            catch (Exception error) { Unavailable(error); return false; }
        }

        static void Unavailable(Exception error) =>
            Debug.LogWarning("[Analytics] Unavailable: " + error.GetType().Name);
    }
}
