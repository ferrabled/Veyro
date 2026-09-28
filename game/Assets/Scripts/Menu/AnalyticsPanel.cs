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
        /// A MenuSheet: X, tap outside and Android back all close without changing the choice.
        /// ENABLE and OFF are deliberately the SAME weight - both plain rounded buttons, neither
        /// pink - because this is a consent choice and the off-by-default one must not look like
        /// the lesser answer. The support id and the policy are quiet links under them.
        void Build()
        {
            // The body is the disclosure and runs to ten lines at 30 px; its band is that plus air,
            // so the copy - not empty card - fills the space between title and choices.
            var card = MenuSheet.Build(gameObject, 145, new Vector2(MenuSheet.Width, 1180f), Close);
            MenuSheet.Title(card, 104f, 70f).text = "HELP IMPROVE VEYRO";
            _body = MenuSheet.Body(card, 190f, 500f, 30);
            _enable = MenuSheet.Secondary(card, "Enable", 360f, "ENABLE ANALYTICS", () => _analytics.SetEnabled(true));
            _disable = MenuSheet.Secondary(card, "Disable", 252f, "KEEP ANALYTICS OFF", () => _analytics.SetEnabled(false));
            _copy = MenuSheet.Link(card, "CopyId", 150f, "COPY ANALYTICS SUPPORT ID", () =>
            {
                GUIUtility.systemCopyBuffer = _analytics.SupportId;
                _body.text = "Analytics support ID copied. Email it to ferrabled+veyro@gmail.com for access or deletion. " +
                    "We verify control before acting. Turning analytics off stops future collection and holds unsent events on this device; " +
                    "it does not delete past provider records.";
            });
            MenuSheet.Link(card, "Policy", 80f, "READ PRIVACY POLICY", () => Application.OpenURL(GameLinks.PrivacyPolicyUrl));
        }
        void Refresh()
        {
            if (_analytics.DisablePending)
            {
                _body.text = "Turning analytics off has not finished. We are retrying. " +
                    "Your OFF choice is saved, but collection may continue until shutdown succeeds. " +
                    "Restart the app if this message persists; analytics will stay off on launch.";
                _enable.interactable = false;
                _disable.GetComponentInChildren<Text>().text = "RETRY TURNING OFF";
                _copy.interactable = false; // copying must not replace the pending warning
                return;
            }
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
