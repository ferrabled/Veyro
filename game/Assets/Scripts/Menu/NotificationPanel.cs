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

        void Build()
        {
            RuntimeUi.PortraitCanvas(gameObject, 140);
            RuntimeUi.FullScreenPanel("Dim", transform, new Color(0.04f, 0.05f, 0.09f, 0.9f));
            var card = RuntimeUi.Card("Card", transform, new Vector2(920f, 1080f), MenuTheme.Card).transform;
            var title = Label("Title", card, 330f, 210f, 48);
            title.text = _verification
                ? "Your OneSignal SDK integration is complete!"
                : "DAILY RUN NOTIFICATIONS";
            _body = Label("Body", card, 70f, 300f, 34);
            _primary = RuntimeUi.TextButton("Enable", card, new Vector2(.5f, .5f),
                new Vector2(0f, -160f), new Vector2(790f, 100f), MenuTheme.Accent,
                _verification ? "Got it" : "ENABLE NOTIFICATIONS", 34, MenuTheme.Text, Enable);
            _primaryLabel = _primary.GetComponentInChildren<Text>();

            if (_verification) return; // vendor verification has exactly one action
            RuntimeUi.TextButton("Disable", card, new Vector2(.5f, .5f),
                new Vector2(0f, -280f), new Vector2(790f, 84f), MenuTheme.Bar,
                "TURN OFF", 30, MenuTheme.Text, Disable);
            RuntimeUi.TextButton("CopyId", card, new Vector2(.5f, .5f),
                new Vector2(0f, -378f), new Vector2(790f, 72f), MenuTheme.Bar,
                "COPY NOTIFICATION SUPPORT ID", 26, MenuTheme.Dim, CopyId);
            RuntimeUi.TextButton("Close", card, new Vector2(.5f, .5f),
                new Vector2(0f, -470f), new Vector2(790f, 72f), MenuTheme.Bar,
                "CLOSE", 30, MenuTheme.Text, Close, Sfx.UiBack);
        }

        static Text Label(string name, Transform parent, float y, float height, int size)
        {
            var text = RuntimeUi.Label(name, parent, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(-395f, y - height / 2f), new Vector2(395f, y + height / 2f),
                size, TextAnchor.MiddleCenter, MenuTheme.Text);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 24;
            text.resizeTextMaxSize = size;
            return text;
        }

        void Refresh()
        {
            var state = _push.Status;
            if (_verification)
                _body.text = "You can now send Push Notifications & In-App Messages through OneSignal. Tap below to enable push notifications.";
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

            _primary.interactable = !_busy && state.Initialized && state.Registered && (_verification || !state.ReadyForPush);
            _primaryLabel.text = _busy ? "PLEASE WAIT…" : _verification ? "Got it" :
                state.ReadyForPush ? "NOTIFICATIONS ON" : !state.CanRequestPermission && !state.PermissionGranted
                    ? "ALLOW IN ANDROID SETTINGS" : "ENABLE NOTIFICATIONS";
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
            // Closing the panel or turning notifications off while Android owns the screen
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
