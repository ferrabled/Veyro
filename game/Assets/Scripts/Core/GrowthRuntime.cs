using System;
using System.Threading;
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
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject _application;
        VisitLifecycle _lifecycle;
#endif

        public static GrowthRuntime Create()
        {
            var runtime = new GameObject("Growth lifecycle").AddComponent<GrowthRuntime>();
            DontDestroyOnLoad(runtime.gameObject);
            Analytics = LayersAnalyticsService.Create();
            runtime.ObserveVisits();
            Tracker = new GrowthTracker(Analytics, Application.version, Debug.isDebugBuild,
                () => DateTime.UtcNow, runtime.VisitVersion);
            return runtime;
        }
        public void Attach(IPushService push) { _push = push; push.Opened += Tracker.NotificationOpened; }
        void ObserveVisits()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    _application = activity.Call<AndroidJavaObject>("getApplication");
                    _lifecycle = new VisitLifecycle(activity.Call<int>("hashCode"));
                    _application.Call("registerActivityLifecycleCallbacks", _lifecycle);
                }
            }
            catch (Exception error)
            {
                _lifecycle = null;
                Debug.LogWarning("[Growth] Visit lifecycle unavailable: " + error.GetType().Name);
            }
#endif
        }

        int VisitVersion()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return _lifecycle?.Version ?? 0;
#else
            return 0;
#endif
        }

        void OnApplicationPause(bool paused)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Android pauses for permission dialogs too. Its activity STOP is
            // the visibility boundary: a dialog alone does not end the visit,
            // but Home/app switching during that dialog still does.
            if (_lifecycle != null) return;
#endif
            if (paused) Tracker?.Backgrounded();
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        sealed class VisitLifecycle : AndroidJavaProxy
        {
            readonly int _activityId;
            int _version;
            public int Version => Volatile.Read(ref _version);
            public VisitLifecycle(int activityId) : base("android.app.Application$ActivityLifecycleCallbacks")
                => _activityId = activityId;

            // Android invokes these on its UI thread. Only advance an atomic
            // visit counter here; the tracker reads it on Unity's main thread.
            // No delayed clear can erase a newer notification's attribution.
            public override AndroidJavaObject Invoke(string methodName, AndroidJavaObject[] args)
            {
                if (methodName == "onActivityStopped" && args[0].Call<int>("hashCode") == _activityId &&
                    !args[0].Call<bool>("isChangingConfigurations"))
                    Interlocked.Increment(ref _version);
                // Also accept the API 29+ default pre/post lifecycle callbacks.
                if (methodName.StartsWith("onActivity", StringComparison.Ordinal)) return null;
                return base.Invoke(methodName, args);
            }
        }
#endif
        void OnDestroy()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_application != null)
            {
                try
                {
                    if (_lifecycle != null) _application.Call("unregisterActivityLifecycleCallbacks", _lifecycle);
                }
                catch (Exception error) { Debug.LogWarning("[Growth] Visit cleanup: " + error.GetType().Name); }
                _application.Dispose();
            }
#endif
            if (_push != null && Tracker != null) _push.Opened -= Tracker.NotificationOpened;
            Tracker?.Dispose(); Tracker = null; Analytics = null;
        }
    }
}
