using System;
using System.Collections;
using System.Reflection;
using MotionRunner.Commerce;
using NUnit.Framework;
using UnityEngine;

namespace MotionRunner.Tests
{
    public sealed class RevenueCatAdapterReviewTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject _root;
        object _store;
        object _sdk;
        Type _type;
        Type _sdkType;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("RevenueCatAdapterReviewTest");
            _type = Type.GetType("MotionRunner.Commerce.RevenueCat.RevenueCatStore, MotionRunner.Commerce.RevenueCat", true);
            _sdkType = Type.GetType("Purchases, revenuecat.purchases-unity", true);
            _store = _root.AddComponent(_type);
            _sdk = _root.AddComponent(_sdkType);
            // Real SDK callback plumbing, with its own inert wrapper: no native store/network.
            var noop = _sdkType.GetNestedType("PurchasesWrapperNoop", BindingFlags.NonPublic);
            _sdkType.GetField("_wrapper", Private).SetValue(_sdk, Activator.CreateInstance(noop, true));
            _type.GetField("_purchases", Private).SetValue(_store, _sdk);
            _type.GetField("_configured", Private).SetValue(_store, true);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_root);

        [TestCase(true)]
        [TestCase(false)]
        public void RestoreWaitsForIdentityAndPublishesEntitlementsBeforeCallback(bool logout)
        {
            var store = (IStore)_store;
            store.Identify("player");
            if (logout)
            {
                Callback("LogInCallback").DynamicInvoke(Info(), false, null);
                store.ResetIdentity();
            }
            int completed = 0;
            var routine = Commerce(done => Invoke("RestoreReady", done), outcome =>
            {
                Assert.IsTrue(outcome.Succeeded);
                CollectionAssert.Contains(store.ActiveEntitlements, "skin_ember");
                completed++;
            });
            Assert.IsTrue(routine.MoveNext(), "restore did not wait for identity settlement");
            Assert.IsNull(Callback("RestorePurchasesCallback"));
            if (logout) Callback("LogOutCallback").DynamicInvoke(Info(), null);
            else Callback("LogInCallback").DynamicInvoke(Info(), false, null);
            Assert.IsFalse(routine.MoveNext());
            Assert.IsNotNull(Callback("RestorePurchasesCallback"));
            store.Identify("next-player");
            var identity = (StoreIdentity)_type.GetField("_identity", Private).GetValue(_store);
            Assert.IsFalse(identity.Busy, "login overlapped the restore");
            Callback("RestorePurchasesCallback").DynamicInvoke(Info("skin_ember"), null);
            Assert.AreEqual(1, completed);
            Assert.IsTrue(identity.Busy, "next login never started after callback");
        }

        [Test]
        public void IdentityWaitTimesOutWithoutSendingRestoreOrClaimingSuccess()
        {
            ((IStore)_store).Identify("player");
            int completed = 0;
            var routine = Commerce(done => Invoke("RestoreReady", done), outcome =>
            {
                Assert.IsFalse(outcome.Succeeded);
                completed++;
            });
            Assert.IsTrue(routine.MoveNext());
            foreach (var field in routine.GetType().GetFields(Private | BindingFlags.Public))
                if (field.Name.StartsWith("<deadline>")) field.SetValue(routine, -1f);
            Assert.IsFalse(routine.MoveNext());
            Assert.AreEqual(1, completed);
            Assert.IsNull(Callback("RestorePurchasesCallback"));
        }

        IEnumerator Commerce(Action<Action<PurchaseOutcome>> operation, Action<PurchaseOutcome> done) =>
            (IEnumerator)Invoke("CommerceWhenReady", operation, done);

        object Invoke(string name, params object[] args) => _type.GetMethod(name, Private).Invoke(_store, args);
        Delegate Callback(string name) => (Delegate)_sdkType.GetProperty(name, Private).GetValue(_sdk);

        object Info(string entitlement = null)
        {
            var jsonType = _sdkType.Assembly.GetType("RevenueCat.SimpleJSON.JSON", true);
            var json = jsonType.GetMethod("Parse").Invoke(null, new object[] { "{\"entitlements\":{\"all\":{},\"active\":{}}}" });
            var info = Activator.CreateInstance(_sdkType.GetNestedType("CustomerInfo"), json);
            var entitlements = info.GetType().GetField("Entitlements").GetValue(info);
            var active = (IDictionary)entitlements.GetType().GetField("Active").GetValue(entitlements);
            if (entitlement != null) active.Add(entitlement, null);
            return info;
        }
    }
}
