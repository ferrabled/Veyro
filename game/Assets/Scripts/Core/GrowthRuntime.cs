using System;
using MotionRunner.Growth;
using MotionRunner.Growth.Layers;
using MotionRunner.Notifications;
using UnityEngine;

namespace MotionRunner.Core
{
    /// Composition/lifecycle only. Native SDKs remain behind their separate adapters.
    public sealed class GrowthRuntime : MonoBehaviour
    {
        public static IAnalyticsService Analytics { get; private set; }
        public static GrowthTracker Tracker { get; private set; }
        IPushService _push;

        public static GrowthRuntime Create()
        {
            var runtime = new GameObject("Growth lifecycle").AddComponent<GrowthRuntime>();
            DontDestroyOnLoad(runtime.gameObject);
            Analytics = LayersAnalyticsService.Create();
            Tracker = new GrowthTracker(Analytics, Application.version, Debug.isDebugBuild, () => DateTime.UtcNow);
            return runtime;
        }
        public void Attach(IPushService push) { _push = push; push.Opened += Tracker.NotificationOpened; }
        void OnApplicationPause(bool paused) { if (paused) Tracker?.Backgrounded(); }
        void OnDestroy()
        {
            if (_push != null && Tracker != null) _push.Opened -= Tracker.NotificationOpened;
            Tracker?.Dispose(); Tracker = null; Analytics = null;
        }
    }
}
