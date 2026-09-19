using MotionRunner.Commerce;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    public sealed class StoreIdentityTests
    {
        [Test]
        public void DeleteDuringLoginWaitsThenLogsOutWithoutPublishingStaleCustomer()
        {
            var state = new StoreIdentity();
            state.Identify("deleted-user");
            Assert.IsTrue(state.TryBegin(out var id, out var generation));
            state.Reset();
            Assert.IsFalse(state.TryBegin(out _, out _));
            Assert.IsFalse(state.Complete(id, generation, true));
            Assert.AreEqual("deleted-user", state.Applied);
            Assert.IsTrue(state.TryBegin(out id, out generation));
            Assert.IsNull(id);
            Assert.IsTrue(state.Complete(id, generation, true));
            Assert.IsNull(state.Applied);
            Assert.IsTrue(state.Settled);
        }

        [Test]
        public void FailedLogoutRetainsIdentityAndCanRetry()
        {
            var state = SignedIn();
            state.Reset();
            state.TryBegin(out var id, out var generation);
            Assert.IsFalse(state.Complete(id, generation, false));
            Assert.AreEqual("player", state.Applied);
            Assert.IsTrue(state.ResetPending);
            Assert.IsTrue(state.TryBegin(out id, out generation));
            Assert.IsNull(id);
            Assert.IsTrue(state.Complete(id, generation, true));
            Assert.IsTrue(state.Settled);
        }

        [Test]
        public void NewIdentityDuringLogoutWaitsUntilAnonymousResetCompletes()
        {
            var state = SignedIn();
            state.Reset();
            state.TryBegin(out var id, out var generation);
            state.Identify("replacement");
            Assert.IsFalse(state.Complete(id, generation, true));
            Assert.IsTrue(state.TryBegin(out id, out generation));
            Assert.AreEqual("replacement", id);
            Assert.IsTrue(state.Complete(id, generation, true));
        }

        [Test]
        public void ResetBeforeConfigurationCancelsPendingIdentification()
        {
            var state = new StoreIdentity();
            state.Identify("player");
            state.Reset();
            Assert.IsTrue(state.TryBegin(out var id, out var generation));
            Assert.IsNull(id);
            Assert.IsTrue(state.Complete(id, generation, true));
            Assert.IsFalse(state.TryBegin(out _, out _));
        }

        [Test]
        public void FailedLoginCanRetryButCannotOverrideNewDesiredIdentity()
        {
            var state = new StoreIdentity();
            state.Identify("old");
            state.TryBegin(out var id, out var generation);
            state.Identify("new");
            Assert.IsFalse(state.Complete(id, generation, false));
            Assert.IsTrue(state.TryBegin(out id, out generation));
            Assert.AreEqual("new", id);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void CommerceWaitsForLoginOrLogoutThenHoldsIdentityThroughItsCallback(bool logout)
        {
            var state = logout ? SignedIn() : new StoreIdentity();
            if (logout) state.Reset();
            else state.Identify("player");
            state.TryBegin(out var id, out var generation);
            Assert.IsFalse(state.TryBeginCommerce());
            Assert.IsFalse(state.CanApplyCustomerInfo);
            Assert.IsFalse(state.Complete(id, generation, false));
            Assert.IsFalse(state.TryBeginCommerce(), "failed identity update must keep commerce waiting");
            state.TryBegin(out id, out generation);
            state.Complete(id, generation, true);
            Assert.IsTrue(state.TryBeginCommerce());
            Assert.IsFalse(state.TryBeginCommerce(), "overlapping commerce operation");
            state.Identify("next-player");
            Assert.IsFalse(state.TryBegin(out _, out _), "identity changed before commerce completed");
            Assert.IsTrue(state.CanApplyCustomerInfo, "successful commerce callback would lose its entitlements");
            state.CompleteCommerce();
            Assert.IsTrue(state.TryBegin(out id, out generation));
            Assert.AreEqual("next-player", id);
            Assert.IsFalse(state.CanApplyCustomerInfo);
        }

        static StoreIdentity SignedIn()
        {
            var state = new StoreIdentity();
            state.Identify("player");
            state.TryBegin(out var id, out var generation);
            state.Complete(id, generation, true);
            return state;
        }
    }
}
