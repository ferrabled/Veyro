using System;
using System.Collections.Generic;
using System.Linq;
using MotionRunner.Growth;
using MotionRunner.Notifications;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    public sealed class GrowthTrackerTests
    {
        FakeAnalyticsService _analytics;
        GrowthTracker _tracker;
        DateTime _now;
        [SetUp] public void SetUp()
        {
            _now = new DateTime(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc);
            _analytics = new FakeAnalyticsService();
            _tracker = new GrowthTracker(_analytics, "test", true, () => _now);
        }
        [TearDown] public void TearDown() => _tracker.Dispose();
        static NotificationOpen Push(string id = "push1", string destination = "daily", string version = "1") =>
            new NotificationOpen(id, new Dictionary<string, object>
                { ["destination"] = destination, ["schema_version"] = version,
                  ["campaign_id"] = "daily-20260921", ["variant"] = "a" });
        void Start(string id = "r1", string mode = "daily") =>
            _tracker.StartRun(id, mode, "tilt", "2026-09-21", "greybox", "v1");
        void Finish(string id = "r1") => _tracker.CompleteRun(id, 123, 4, 50, 12f, "camera_fallback");

        [Test] public void DeclinedAnalyticsStillRoutesWithoutTracking()
        {
            _tracker.NotificationOpened(Push()); Start(); Finish();
            Assert.That(_tracker.DailyEntryPending, Is.True);
            Assert.That(_analytics.Events, Is.Empty);
            _analytics.SetEnabled(true); Start("later");
            Assert.That(_analytics.Events.Single().Value.ContainsKey("campaign_id"), Is.False);
        }
        [Test] public void ClickThenCompletedRunCarriesCampaignOnce()
        {
            _analytics.SetEnabled(true);
            _tracker.NotificationOpened(Push()); _tracker.NotificationOpened(Push());
            Start(); Finish(); Finish();
            Assert.That(_analytics.Events.Select(e => e.Key), Is.EqualTo(new[]
                { "notification_opened", "daily_run_started", "daily_run_completed" }));
            Assert.That(_analytics.Events.Last().Value["campaign_id"], Is.EqualTo("daily-20260921"));
            Assert.That(_analytics.Events.Last().Value["input_mode"], Is.EqualTo("camera_fallback"));
        }
        [Test] public void QuitDoesNotBecomeACompletion()
        {
            _analytics.SetEnabled(true); Start(); _tracker.AbandonRun(); Finish();
            Assert.That(_analytics.Events.Count, Is.EqualTo(1));
        }
        [Test] public void OldRunResultCannotCompleteNewRun()
        {
            _analytics.SetEnabled(true); Start(); Start("r2"); Finish();
            Assert.That(_analytics.Events.Count, Is.EqualTo(2)); Finish("r2");
            Assert.That(_analytics.Events.Last().Value["run_id"], Is.EqualTo("r2"));
        }
        [Test] public void OptOutDiscardsPendingAttributionAndRun()
        {
            _analytics.SetEnabled(true); _tracker.NotificationOpened(Push()); Start();
            _analytics.SetEnabled(false); Finish(); _analytics.SetEnabled(true); Finish(); Start("r2");
            Assert.That(_analytics.Events.Any(e => e.Key == "daily_run_completed"), Is.False);
            Assert.That(_analytics.Events.Last().Value.ContainsKey("campaign_id"), Is.False);
        }
        [Test] public void EnablingMidRunDoesNotBackfillAResult()
        {
            Start(); _analytics.SetEnabled(true); Finish();
            Assert.That(_analytics.Events, Is.Empty);
        }
        [TestCase("https://example.com", "1")]
        [TestCase("daily", "999")]
        public void UnrecognizedPayloadCannotRoute(string destination, string version)
        {
            _analytics.SetEnabled(true); _tracker.NotificationOpened(Push(destination: destination, version: version));
            Assert.That(_tracker.DailyEntryPending, Is.False);
            Start(); Assert.That(_analytics.Events.Last().Value.ContainsKey("campaign_id"), Is.False);
        }
        [Test] public void BackgroundEndsClickAttributionForLaterRuns()
        {
            _analytics.SetEnabled(true); _tracker.NotificationOpened(Push());
            _tracker.Backgrounded(); Start();
            Assert.That(_analytics.Events.Last().Value.ContainsKey("campaign_id"), Is.False);
        }
        [Test] public void AttributionExpiresAtTwentyFourHours()
        {
            _analytics.SetEnabled(true); _tracker.NotificationOpened(Push());
            _now = _now.AddHours(24); Start();
            Assert.That(_analytics.Events.Last().Value.ContainsKey("campaign_id"), Is.False);
        }
        [Test] public void FreeRunDoesNotClaimDailyCampaign()
        {
            _analytics.SetEnabled(true); _tracker.NotificationOpened(Push()); Start(mode: "free"); Finish();
            Assert.That(_analytics.Events.Last().Key, Is.EqualTo("free_run_completed"));
            Assert.That(_analytics.Events.Last().Value.ContainsKey("campaign_id"), Is.False);
        }
        [Test] public void NotificationDuringRunDoesNotAttributeTheAlreadyRunningRun()
        {
            _analytics.SetEnabled(true); Start(); _tracker.NotificationOpened(Push()); Finish();
            Assert.That(_analytics.Events.Last().Value.ContainsKey("campaign_id"), Is.False);
        }
        [Test] public void EmptyNotificationIdIsIgnored()
        {
            _analytics.SetEnabled(true); _tracker.NotificationOpened(Push(id: ""));
            Assert.That(_analytics.Events, Is.Empty); Assert.That(_tracker.DailyEntryPending, Is.False);
        }
        [Test] public void UnknownPropertiesDoNotLeaveTheApp()
        {
            _analytics.SetEnabled(true);
            var push = Push(); push.Data["recovery_code"] = "private"; push.Data["variant"] = "invalid value";
            _tracker.NotificationOpened(push);
            Assert.That(_analytics.Events.Single().Value.ContainsKey("recovery_code"), Is.False);
            Assert.That(_analytics.Events.Single().Value.ContainsKey("variant"), Is.False);
        }
    }
}
