using MotionRunner.Notifications;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    public sealed class PushPromptTests
    {
        static PushStatus Registered(bool permission = false, bool optedIn = false, bool token = false) =>
            new PushStatus(true, "b21a7d0a-8d9b-4e89-851a-123456789abc", permission, optedIn, token);

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        [TestCase("local-pending")]
        [TestCase("LOCAL-pending")]
        public void PlaceholderNeverTriggersRegistrationOrPrompt(string id)
        {
            var state = new PushStatus(true, id, true, true, true);
            Assert.That(state.Registered, Is.False);
            Assert.That(state.ReadyForPush, Is.False);
            Assert.That(new PushPromptPolicy(false).TryOffer(state, true, true, true), Is.False);
        }

        [Test]
        public void CachedRegistrationOffersOnceAndObserverDuplicateDoesNotRepeat()
        {
            var policy = new PushPromptPolicy(false);
            Assert.That(policy.TryOffer(Registered(), true, false, true), Is.True);
            Assert.That(policy.TryOffer(Registered(true, true, true), true, true, true), Is.False);
            Assert.That(new PushPromptPolicy(policy.Seen).TryOffer(Registered(), true, true, true), Is.False);
        }

        [Test]
        public void LateRegistrationWaitsForSafeMenuWithoutConsumingOffer()
        {
            var policy = new PushPromptPolicy(false);
            Assert.That(policy.TryOffer(new PushStatus(), true, true, true), Is.False);
            Assert.That(policy.TryOffer(Registered(), false, true, true), Is.False);
            Assert.That(policy.Seen, Is.False);
            Assert.That(policy.TryOffer(Registered(), true, true, true), Is.True);
        }

        [Test]
        public void PlayerBuildWaitsForCompletedDailyRatherThanInterruptingFirstLaunch()
        {
            var policy = new PushPromptPolicy(false);
            Assert.That(policy.TryOffer(Registered(), true, false, false), Is.False);
            Assert.That(policy.TryOffer(Registered(), true, true, false), Is.True);
        }

        [TestCase(false, true, true, false)]
        [TestCase(true, false, true, false)]
        [TestCase(true, true, false, false)]
        [TestCase(true, true, true, true)]
        public void RegistrationPermissionSubscriptionAndTokenAreSeparate(bool permission, bool optIn,
            bool token, bool expected)
        {
            Assert.That(Registered(permission, optIn, token).ReadyForPush, Is.EqualTo(expected));
        }
    }
}
