using System.Collections.Generic;
using MotionRunner.Notifications;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// The iOS notification flow (IosPushFlow): the tap decision and the panel copy.
    public sealed class IosPushFlowTests
    {
        const string ServerId = "b21a7d0a-8d9b-4e89-851a-123456789abc";

        static PushStatus Registered(bool permission = false, bool optedIn = false, bool token = false,
            bool canRequest = false) =>
            new PushStatus(true, ServerId, permission, optedIn, token, canRequest);

        [TestCase(true, true, IosPushStep.AlreadyAllowed)]
        [TestCase(true, false, IosPushStep.AlreadyAllowed)]
        [TestCase(false, true, IosPushStep.Ask)]
        [TestCase(false, false, IosPushStep.OpenSettings)]
        public void TapAsksOnlyWhileTheOnePromptIsLeft(bool permission, bool canRequest, IosPushStep expected)
        {
            Assert.That(IosPushFlow.ForTap(permission, canRequest), Is.EqualTo(expected));
        }

        [Test]
        public void FirstVisitOffersEnableAndSaysTheIphoneWillAsk()
        {
            var state = Registered(canRequest: true);
            Assert.That(IosPushFlow.Button(state), Is.EqualTo(IosPushFlow.EnableButton));
            Assert.That(IosPushFlow.Body(state), Is.EqualTo(IosPushFlow.Ask));
        }

        [Test]
        public void DeniedRoutesToSettingsEvenIfThePlayerNeverTappedEnableHere()
        {
            // iOS will not ask again, so the button must not lead into a request that returns
            // false without showing anything (Android's CanRequestPermission dead end).
            var denied = Registered(canRequest: false);
            Assert.That(IosPushFlow.NeedsSettings(denied), Is.True);
            Assert.That(IosPushFlow.Button(denied), Is.EqualTo(IosPushFlow.SettingsButton));
            Assert.That(IosPushFlow.Body(denied), Is.EqualTo(IosPushFlow.Blocked));
            Assert.That(IosPushFlow.ForTap(denied.PermissionGranted, denied.CanRequestPermission),
                Is.EqualTo(IosPushStep.OpenSettings));
        }

        [Test]
        public void ReturningFromSettingsAllowedOffersEnableWhichOptsInWithoutAsking()
        {
            // Allowed in Settings, still opted out: the next ENABLE only opts in.
            var allowed = Registered(permission: true, canRequest: false);
            Assert.That(IosPushFlow.NeedsSettings(allowed), Is.False);
            Assert.That(IosPushFlow.Button(allowed), Is.EqualTo(IosPushFlow.EnableButton));
            Assert.That(IosPushFlow.ForTap(allowed.PermissionGranted, allowed.CanRequestPermission),
                Is.EqualTo(IosPushStep.AlreadyAllowed));
        }

        [Test]
        public void ReadyShowsOn()
        {
            var ready = Registered(true, true, true);
            Assert.That(IosPushFlow.Button(ready), Is.EqualTo(IosPushFlow.OnButton));
            Assert.That(IosPushFlow.Body(ready), Is.EqualTo(IosPushFlow.On));
        }

        [Test]
        public void AllowedAndOptedInWithoutATokenIsStillFinishing()
        {
            Assert.That(IosPushFlow.Body(Registered(true, true, false)), Is.EqualTo(IosPushFlow.Finishing));
        }

        [Test]
        public void UnavailableAndConnectingNeverPointAtSettings()
        {
            // Covers the VEYRO_NO_ONESIGNAL kill switch too: FakePushService never initializes.
            var off = new PushStatus();
            var connecting = new PushStatus(true, null, false, false, false, false);
            Assert.That(IosPushFlow.Body(off), Is.EqualTo(IosPushFlow.Unavailable));
            Assert.That(IosPushFlow.Button(off), Is.EqualTo(IosPushFlow.EnableButton));
            Assert.That(IosPushFlow.Body(connecting), Is.EqualTo(IosPushFlow.Connecting));
            Assert.That(IosPushFlow.Button(connecting), Is.EqualTo(IosPushFlow.EnableButton));
        }

        [Test]
        public void NoCopyNamesAnotherPlatform()
        {
            var copy = new List<string>
            {
                IosPushFlow.EnableButton, IosPushFlow.SettingsButton, IosPushFlow.OnButton,
                IosPushFlow.Unavailable, IosPushFlow.Connecting, IosPushFlow.On, IosPushFlow.Blocked,
                IosPushFlow.Finishing, IosPushFlow.Ask
            };
            foreach (string text in copy)
            {
                Assert.That(text, Does.Not.Contain("Android"), text);
                Assert.That(text, Does.Not.Contain("Google"), text);
                Assert.That(text, Does.Not.Contain("Play Store"), text);
                Assert.That(text, Is.Not.Empty);
            }
            Assert.That(IosPushFlow.SettingsButton, Is.EqualTo("ALLOW IN SETTINGS"));
            Assert.That(IosPushFlow.Blocked, Does.Contain("Settings").And.Contain("iPhone"));
        }
    }
}
