using System;
using System.Threading.Tasks;
using OneSignalSDK.Debug.Models;
using OneSignalSDK.Notifications;
using OneSignalSDK.User.Models;
using UnityEngine;
using Sdk = OneSignalSDK.OneSignal;

namespace MotionRunner.Notifications.OneSignal
{
    /// The only native SDK boundary. Construct once from GameBootstrap on Unity's main thread.
    public sealed class OneSignalPushService : MonoBehaviour, IPushService
    {
        // Public app identifier, never a REST key or Firebase service-account credential.
        public const string AppId = "1f6ba056-efe3-4bfe-a0cd-a9a26150720a";
        public PushStatus Status { get; private set; } = new PushStatus();
        public event Action Changed;
        public event Action<NotificationOpen> Opened;
        bool _initialized;
        bool _initializationAttempted;
        bool _requesting;
        int _requestVersion;

        public static IPushService Create()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var host = new GameObject("Notifications");
            DontDestroyOnLoad(host);
            return host.AddComponent<OneSignalPushService>();
#else
            return new FakePushService();
#endif
        }

        public void Initialize(bool enabled)
        {
            if (_initializationAttempted) return;
            _initializationAttempted = true;
            try
            {
                // No Task.Run: Android JNI and Unity objects belong to the main thread.
                Sdk.Debug.LogLevel = Debug.isDebugBuild ? LogLevel.Verbose : LogLevel.Warn;
                Sdk.Debug.AlertLevel = LogLevel.None;
                Sdk.Initialize(AppId);
                _initialized = true;

                // On Android <=32 OS permission alone is not the player's choice. Never call
                // OptIn at boot: it can itself prompt on Android 13 after permission revocation.
                if (!enabled) Sdk.User.PushSubscription.OptOut();

                // Instance methods keep observers alive; detach when this owner is destroyed.
                Sdk.User.PushSubscription.Changed += OnSubscriptionChanged;
                Sdk.Notifications.PermissionChanged += OnPermissionChanged;
                Sdk.Notifications.ForegroundWillDisplay += OnForegroundNotification;
                Sdk.Notifications.Clicked += OnNotificationClicked;
                // Campaign UI must never cover a run or the camera staging screen.
                Sdk.InAppMessages.Paused = true;

                Refresh(); // registration can already have completed before the observer attached
            }
            catch (Exception error)
            {
                Debug.LogWarning("[Push] Initialization unavailable: " + error.GetType().Name);
                Status = new PushStatus();
                Changed?.Invoke();
            }
        }

        public async Task<bool> EnableFromUserTapAsync()
        {
            if (!_initialized || _requesting) return false;
            _requesting = true;
            int requestVersion = ++_requestVersion;
            try
            {
                bool granted = Sdk.Notifications.Permission ||
                    await RequestAndroidPermission();
                if (granted && requestVersion == _requestVersion) Sdk.User.PushSubscription.OptIn();
                Refresh();
                return granted && requestVersion == _requestVersion;
            }
            catch (Exception error)
            {
                Debug.LogWarning("[Push] Permission request unavailable: " + error.GetType().Name);
                return false;
            }
            finally { _requesting = false; }
        }

        public void Disable()
        {
            ++_requestVersion; // invalidate any permission result still owned by Android
            WithSdk(() => { Sdk.User.PushSubscription.OptOut(); Refresh(); });
        }

        async Task<bool> RequestAndroidPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            const string permission = "android.permission.POST_NOTIFICATIONS";
            int sdk;
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                sdk = version.GetStatic<int>("SDK_INT");
            if (sdk < 33 || !Sdk.Notifications.CanRequestPermission)
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intent = new AndroidJavaObject("android.content.Intent", "android.settings.APP_NOTIFICATION_SETTINGS"))
                {
                    intent.Call<AndroidJavaObject>("putExtra", "android.provider.extra.APP_PACKAGE",
                        activity.Call<string>("getPackageName"));
                    activity.Call("startActivity", intent);
                }
                return false; // Refresh on returning; a subsequent Enable completes opt-in.
            }
            var completion = new TaskCompletionSource<bool>();
            var callbacks = new UnityEngine.Android.PermissionCallbacks();
            Action<string> allow = _ => completion.TrySetResult(true);
            Action<string> deny = _ => completion.TrySetResult(false);
            callbacks.PermissionGranted += allow;
            callbacks.PermissionDenied += deny;
            callbacks.PermissionDeniedAndDontAskAgain += deny;
            try
            {
                // Unity owns the runtime prompt. Avoid the Stable SDK's observed second-request
                // continuation hang, and bound waiting if Android never returns a callback.
                UnityEngine.Android.Permission.RequestUserPermission(permission, callbacks);
                var finished = await Task.WhenAny(completion.Task, Task.Delay(30000));
                return finished == completion.Task && await completion.Task;
            }
            finally
            {
                callbacks.PermissionGranted -= allow;
                callbacks.PermissionDenied -= deny;
                callbacks.PermissionDeniedAndDontAskAgain -= deny;
            }
#else
            await Task.CompletedTask;
            return false;
#endif
        }

        // Reserved wrapper operations for the next campaign increment. No Supabase linking,
        // email or SMS collection is enabled by this initial, anonymous-device integration.
        public void Login(string externalId) => WithSdk(() => Sdk.Login(externalId));
        public void Logout() => WithSdk(Sdk.Logout);
        public void SetTag(string key, string value) => WithSdk(() => Sdk.User.AddTag(key, value));
        public void SetEmail(string email) => WithSdk(() => Sdk.User.AddEmail(email));
        public void SetSmsNumber(string number) => WithSdk(() => Sdk.User.AddSms(number));

        void WithSdk(Action action)
        {
            if (!_initialized) return;
            try { action(); }
            catch (Exception error)
            {
                Debug.LogWarning("[Push] Operation unavailable: " + error.GetType().Name);
            }
        }

        void OnSubscriptionChanged(object sender, PushSubscriptionChangedEventArgs args) => WithSdk(Refresh);
        void OnPermissionChanged(object sender, NotificationPermissionChangedEventArgs args) => WithSdk(Refresh);
        // A reminder should not interrupt the game the player is already using.
        void OnForegroundNotification(object sender, NotificationWillDisplayEventArgs args) => args.PreventDefault();
        void OnNotificationClicked(object sender, NotificationClickEventArgs args) =>
            Opened?.Invoke(new NotificationOpen(args.Notification.NotificationId, args.Notification.AdditionalData));
        void OnApplicationFocus(bool focused) { if (focused) WithSdk(Refresh); }

        void Refresh()
        {
            var subscription = Sdk.User.PushSubscription;
            Status = new PushStatus(true, subscription.Id, Sdk.Notifications.Permission,
                subscription.OptedIn, !string.IsNullOrEmpty(subscription.Token),
                Sdk.Notifications.CanRequestPermission);
            Changed?.Invoke();
        }

        void OnDestroy()
        {
            if (!_initialized) return;
            WithSdk(() =>
            {
                Sdk.User.PushSubscription.Changed -= OnSubscriptionChanged;
                Sdk.Notifications.PermissionChanged -= OnPermissionChanged;
                Sdk.Notifications.ForegroundWillDisplay -= OnForegroundNotification;
                Sdk.Notifications.Clicked -= OnNotificationClicked;
            });
        }
    }
}
