using System;
using MotionRunner.Audio;
using MotionRunner.Core;
using MotionRunner.Notifications;
using UnityEngine;
using UnityEngine.UI;

namespace MotionRunner.Menu
{
    /// Code-built permission explanation/settings. Only a button tap can request permission.
    public sealed class NotificationPanel : MonoBehaviour
    {
        IPushService _push;
        Action _closed;
        Text _body;
        Text _primaryLabel;
        Button _primary;
        bool _verification;
        bool _busy;
        bool _cancelled;

        public static NotificationPanel Show(IPushService push, bool verification, Action closed)
        {
            var panel = new GameObject("Notification settings").AddComponent<NotificationPanel>();
            panel._push = push;
            panel._closed = closed;
            panel._verification = verification;
            panel.Build();
            push.Changed += panel.Refresh;
            panel.Refresh();
            return panel;
        }

        /// A MenuSheet: X, tap outside and Android back all close (Close already cancels a
        /// permission request in flight). ENABLE is the sheet's one pink action; TURN OFF sits
        /// under it as a plain one, and the support id is a quiet link.
        void Build()
        {
            // Sized to its copy: the longest state is four lines at 32 px, so the body band is
            // that plus a little air, and the buttons follow straight on.
            var card = MenuSheet.Build(gameObject, 140, new Vector2(MenuSheet.Width, 860f), Close);
            MenuSheet.Title(card, 104f, _verification ? 130f : 80f).text = _verification
                ? "Your OneSignal SDK integration is complete!"
                : "DAILY RUN NOTIFICATIONS";
            _body = MenuSheet.Body(card, _verification ? 250f : 200f, 250f);

            // Vendor verification has exactly one action, so it takes the bottom slot.
            _primary = MenuSheet.Primary(card, "Enable", _verification ? 120f : 300f,
                _verification ? "Got it" : "ENABLE NOTIFICATIONS", Enable);
            _primaryLabel = _primary.GetComponentInChildren<Text>();

            if (_verification) return;
            MenuSheet.Secondary(card, "Disable", 176f, "TURN OFF", Disable);
            MenuSheet.Link(card, "CopyId", 70f, "COPY NOTIFICATION SUPPORT ID", CopyId);
        }

        void Refresh()
        {
            var state = _push.Status;
            if (_verification)
                _body.text = "You can now send Push Notifications & In-App Messages through OneSignal. Tap below to enable push notifications.";
#if UNITY_IOS
            // iOS copy names Settings and the iPhone, never another platform (App Review
            // 2.3.10), and a denied state always reads ALLOW IN SETTINGS: iOS never asks twice.
            else
                _body.text = IosPushFlow.Body(state);
#else
            else if (!state.Initialized)
                _body.text = "Notifications are unavailable here. You can keep playing. On Android, reopen the game and try again.";
            else if (!state.Registered)
                _body.text = "Connecting notifications… Keep playing and check again when you're online.";
            else if (state.ReadyForPush)
                _body.text = "Notifications are on. You can turn them off here at any time. Turning them off does not delete your notification record.";
            else if (PlayerPrefs.GetInt(PushPromptPolicy.EnabledKey, 0) == 1 && !state.PermissionGranted)
                _body.text = "Notifications are blocked in Android settings. Tap below to allow them. The full game works without notifications.";
            else if (state.PermissionGranted && state.OptedIn && !state.HasToken)
                _body.text = "Permission is allowed; device registration is still finishing. Check your connection and try again later.";
            else
                _body.text = "Enable notifications about Daily Runs. You can turn them off at any time. Android will ask for permission if needed; declining changes nothing about the game.";
#endif

            _primary.interactable = !_busy && state.Initialized && state.Registered && (_verification || !state.ReadyForPush);
            _primaryLabel.text = _busy ? "PLEASE WAIT…" : _verification ? "Got it" :
#if UNITY_IOS
                IosPushFlow.Button(state);
#else
                state.ReadyForPush ? "NOTIFICATIONS ON" : !state.CanRequestPermission && !state.PermissionGranted
                    ? "ALLOW IN ANDROID SETTINGS" : "ENABLE NOTIFICATIONS";
#endif
        }

        async void Enable()
        {
            if (_busy) return;
            _cancelled = false;
            _busy = true;
            PlayerPrefs.SetInt(PushPromptPolicy.EnabledKey, 1);
            PlayerPrefs.Save();
            Refresh();
            await _push.EnableFromUserTapAsync();
            // Closing the panel or turning notifications off while the OS owns the screen
            // must not let a late permission result silently re-enable the subscription.
            if (_cancelled || PlayerPrefs.GetInt(PushPromptPolicy.EnabledKey, 0) == 0) _push.Disable();
            if (this == null) return;
            _busy = false;
            if (_verification) Close();
            else Refresh();
        }

        void Disable()
        {
            _cancelled = true;
            PlayerPrefs.SetInt(PushPromptPolicy.EnabledKey, 0);
            PlayerPrefs.Save();
            _push.Disable();
            Refresh();
        }

        void CopyId()
        {
            if (!_push.Status.Registered) return;
            GUIUtility.systemCopyBuffer = _push.Status.SubscriptionId;
            _body.text = "Notification support ID copied. Include it when asking support to delete your notification record.";
        }

        public void Close()
        {
            if (_busy)
            {
                _cancelled = true;
                PlayerPrefs.SetInt(PushPromptPolicy.EnabledKey, 0);
                PlayerPrefs.Save();
                _push.Disable();
            }
            gameObject.SetActive(false);
            _closed?.Invoke();
            Destroy(gameObject);
        }

        void OnDestroy() { if (_push != null) _push.Changed -= Refresh; }
    }
}
