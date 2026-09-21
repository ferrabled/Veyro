using System;
using System.Collections.Generic;
using MotionRunner.Notifications;

namespace MotionRunner.Growth
{
    /// Analytics never controls gameplay. Context belongs to the clicked visit, not later visits.
    public sealed class GrowthTracker : IDisposable
    {
        readonly IAnalyticsService _analytics;
        readonly string _build;
        readonly bool _qa;
        readonly Func<DateTime> _clock;
        readonly HashSet<string> _clicks = new HashSet<string>();
        Dictionary<string, object> _campaign;
        Dictionary<string, object> _run;
        DateTime _expires;
        string _runId;
        public bool DailyEntryPending { get; private set; }

        public GrowthTracker(IAnalyticsService analytics, string build, bool qa, Func<DateTime> clock)
        {
            _analytics = analytics; _build = build; _qa = qa; _clock = clock;
            analytics.Changed += ConsentChanged;
        }

        public void NotificationOpened(NotificationOpen notification)
        {
            if (notification == null || string.IsNullOrEmpty(notification.Id)) return;
            if (_clicks.Contains(notification.Id)) return;
            // Bound memory even if a remote sender produces many unique IDs.
            if (_clicks.Count >= 64) _clicks.Clear();
            _clicks.Add(notification.Id);
            _campaign = null;
            var properties = Common();
            properties["notification_id"] = notification.Id;
            var data = notification.Data;
            bool daily = Token(data, "destination") == "daily" && Token(data, "schema_version") == "1";
            if (daily)
            {
                DailyEntryPending = true;
                if (_analytics.Enabled)
                {
                    _campaign = new Dictionary<string, object>();
                    foreach (string key in new[] { "campaign_id", "experiment_id", "variant" })
                    {
                        string value = Token(data, key);
                        if (value != null) _campaign[key] = value;
                    }
                    _expires = _clock().AddHours(24);
                    Copy(_campaign, properties);
                }
            }
            _analytics.Track("notification_opened", properties);
        }

        public void ConsumeDailyEntry() => DailyEntryPending = false;

        public void StartRun(string runId, string mode, string control, string day,
            string world, string contentVersion)
        {
            _run = null; _runId = null;
            if (!_analytics.Enabled || string.IsNullOrEmpty(runId)) return;
            _runId = runId;
            _run = Common();
            _run["run_id"] = runId; _run["mode"] = mode; _run["input_mode"] = control;
            _run["utc_day"] = day; _run["world_id"] = world; _run["content_version"] = contentVersion;
            if (_campaign != null && _clock() < _expires && mode == "daily") Copy(_campaign, _run);
            _analytics.Track(mode == "daily" ? "daily_run_started" : "free_run_started", _run);
        }

        public void CompleteRun(string runId, int score, int coins, int distance, float duration,
            string finalControl)
        {
            if (_run == null || _runId != runId || !_analytics.Enabled) return;
            var properties = _run;
            _run = null; _runId = null; // claim before a callback can re-enter
            properties["score"] = score; properties["coins"] = coins;
            properties["distance_m"] = distance; properties["duration_s"] = duration;
            properties["input_mode"] = finalControl;
            _analytics.Track((string)properties["mode"] == "daily" ? "daily_run_completed" :
                "free_run_completed", properties);
        }

        public void AbandonRun() { _run = null; _runId = null; }
        public void Backgrounded() { _campaign = null; }
        void ConsentChanged() { if (!_analytics.Enabled) { _campaign = null; AbandonRun(); } }
        public void Dispose() => _analytics.Changed -= ConsentChanged;
        Dictionary<string, object> Common() => new Dictionary<string, object>
            { ["app_version"] = _build, ["qa"] = _qa };
        static void Copy(Dictionary<string, object> from, Dictionary<string, object> to)
        { foreach (var pair in from) to[pair.Key] = pair.Value; }
        static string Token(IDictionary<string, object> data, string key)
        {
            if (data == null || !data.TryGetValue(key, out var raw)) return null;
            string value = raw as string;
            if (value == null && key == "schema_version" && (raw is int || raw is long)) value = raw.ToString();
            if (string.IsNullOrEmpty(value) || value.Length > 80) return null;
            foreach (char c in value)
                if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') &&
                    !(c >= '0' && c <= '9') && c != '_' && c != '-' && c != '.') return null;
            return value;
        }
    }
}
