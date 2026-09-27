using System;
using MotionRunner.Audio;
using MotionRunner.Core;
using MotionRunner.Growth;
using UnityEngine;
using UnityEngine.UI;

namespace MotionRunner.Menu
{
    public sealed class AnalyticsPanel : MonoBehaviour
    {
        IAnalyticsService _analytics;
        Action _closed;
        Text _body;
        Button _enable;
        Button _disable;
        Button _copy;

        public static AnalyticsPanel Show(IAnalyticsService analytics, Action closed)
        {
            var panel = new GameObject("Gameplay analytics settings").AddComponent<AnalyticsPanel>();
            panel._analytics = analytics; panel._closed = closed;
            panel.Build(); analytics.Changed += panel.Refresh; panel.Refresh();
            return panel;
        }
        void Build()
        {
            RuntimeUi.PortraitCanvas(gameObject, 145);
            RuntimeUi.FullScreenPanel("Dim", transform, new Color(.04f, .05f, .09f, .94f));
            var card = RuntimeUi.Card("Card", transform, new Vector2(940f, 1320f), MenuTheme.Card).transform;
            Label("Title", card, 510f, 130f, 45).text = "HELP IMPROVE VEYRO";
            _body = Label("Body", card, 190f, 450f, 31);
            _enable = Button(card, "Enable", -140f, "ENABLE ANALYTICS", () => _analytics.SetEnabled(true));
            _disable = Button(card, "Disable", -250f, "KEEP ANALYTICS OFF", () => _analytics.SetEnabled(false));
            _copy = Button(card, "CopyId", -360f, "COPY ANALYTICS SUPPORT ID", () =>
            {
                GUIUtility.systemCopyBuffer = _analytics.SupportId;
                _body.text = "Analytics support ID copied. Email it to ferrabled+veyro@gmail.com for access or deletion. " +
                    "We verify control before acting. Turning analytics off stops future collection and holds unsent events on this device; " +
                    "it does not delete past provider records.";
            });
            Button(card, "Policy", -470f, "READ PRIVACY POLICY", () => Application.OpenURL(GameLinks.PrivacyPolicyUrl));
            Button(card, "Close", -580f, "CLOSE", Close, Sfx.UiBack);
        }
        Button Button(Transform parent, string name, float y, string text, Action action, Sfx sound = Sfx.UiTap) =>
            RuntimeUi.TextButton(name, parent, new Vector2(.5f, .5f), new Vector2(0f, y),
                new Vector2(800f, 88f), MenuTheme.Bar, text, 28, MenuTheme.Text, () => action(), sound);
        static Text Label(string name, Transform parent, float y, float height, int size)
        {
            var text = RuntimeUi.Label(name, parent, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(-400f, y - height / 2), new Vector2(400f, y + height / 2),
                size, TextAnchor.MiddleCenter, MenuTheme.Text);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true; text.resizeTextMinSize = 24; text.resizeTextMaxSize = size;
            return text;
        }
        void Refresh()
        {
            _body.text = (_analytics.Enabled ? "Analytics is " + (_analytics.IsReady ? "ON." : "enabled but unavailable right now.") : "Analytics is OFF by default.") +
                "\n\nIf enabled, Layers receives app visits, notification opens and run results, plus an installation ID, " +
                "device/app details and approximate region from your connection. This helps us improve Daily Run reminders." +
                "\n\nNo camera images, motion readings or advertising ID. This choice is separate from reminders. " +
                "The full game works with analytics off. Turning it off stops uploads; unsent events stay on your device, never sent, " +
                "and are deleted when you re-enable. Past records need a deletion request.";
            _enable.interactable = !_analytics.Enabled || !_analytics.IsReady;
            _disable.GetComponentInChildren<Text>().text = _analytics.Enabled ? "TURN ANALYTICS OFF" : "KEEP ANALYTICS OFF";
            _copy.interactable = !string.IsNullOrEmpty(_analytics.SupportId);
        }
        public void Close()
        {
            gameObject.SetActive(false); _closed?.Invoke(); Destroy(gameObject);
        }
        void OnDestroy() { if (_analytics != null) _analytics.Changed -= Refresh; }
    }
}
