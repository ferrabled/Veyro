using System;
using System.Collections.Generic;
using System.Reflection;
using Layers.Unity;
using MotionRunner.Growth.Layers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MotionRunner.Tests
{
    public sealed class LayersAnalyticsServiceTests
    {
        const string PurgeKey = "veyro.analytics.purge_pending";
        const string SupportKey = "veyro.analytics.support_id";
        GameObject _host;
        LayersAnalyticsService _service;
        FailingSdk _sdk;
        int _oldChoice, _oldPurge;
        string _oldSupport;

        sealed class FailingSdk : ILayersSdkOps
        {
            public bool IsInitialized { get; private set; } = true;
            public int DenialFailures, ShutdownFailures;
            public bool IdentifyFails;
            public readonly List<string> Steps = new List<string>();
            public void Initialize(LayersConfig config) { Steps.Add("initialize"); IsInitialized = true; }
            public void SetConsent(bool analytics, bool advertising)
            {
                Steps.Add(analytics ? "grant" : "deny");
                if (!analytics && DenialFailures-- > 0) throw new InvalidOperationException();
            }
            public void Reset() => Steps.Add("reset");
            public void Identify(string id)
            {
                Steps.Add("identify");
                if (IdentifyFails) throw new InvalidOperationException();
            }
            public void Shutdown()
            {
                Steps.Add("shutdown");
                if (ShutdownFailures-- > 0) throw new InvalidOperationException();
                IsInitialized = false;
            }
        }

        [SetUp] public void SetUp()
        {
            _oldChoice = PlayerPrefs.GetInt(LayersAnalyticsService.ChoiceKey, -1);
            _oldPurge = PlayerPrefs.GetInt(PurgeKey, -1);
            _oldSupport = PlayerPrefs.HasKey(SupportKey) ? PlayerPrefs.GetString(SupportKey) : null;
            PlayerPrefs.SetInt(LayersAnalyticsService.ChoiceKey, 1);
            PlayerPrefs.DeleteKey(PurgeKey);
            PlayerPrefs.SetString(SupportKey, "test-support");
            _host = new GameObject("Consent failure test");
            _service = _host.AddComponent<LayersAnalyticsService>();
            _sdk = new FailingSdk();
            SetField("_sdk", _sdk);
        }

        [TearDown] public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_host);
            RestoreInt(LayersAnalyticsService.ChoiceKey, _oldChoice);
            RestoreInt(PurgeKey, _oldPurge);
            if (_oldSupport == null) PlayerPrefs.DeleteKey(SupportKey);
            else PlayerPrefs.SetString(SupportKey, _oldSupport);
            PlayerPrefs.Save();
        }

        static void RestoreInt(string key, int previous)
        {
            if (previous < 0) PlayerPrefs.DeleteKey(key);
            else PlayerPrefs.SetInt(key, previous);
        }
        void SetField(string name, object value) => typeof(LayersAnalyticsService)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_service, value);
        void Invoke(string name, params object[] args) => typeof(LayersAnalyticsService)
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_service, args);
        static void ExpectFailure() => LogAssert.Expect(LogType.Warning, "[Analytics] Unavailable: InvalidOperationException");

        [Test] public void ConsentFailureStillStopsSdkAndPersistsTheOffChoice()
        {
            _sdk.DenialFailures = 1;
            ExpectFailure();
            _service.SetEnabled(false);
            Assert.That(_sdk.Steps, Is.EqualTo(new[] { "deny", "shutdown" }));
            Assert.That(_sdk.IsInitialized, Is.False);
            Assert.That(_service.Enabled, Is.False);
            Assert.That(_service.IsReady, Is.False);
            Assert.That(_service.DisablePending, Is.False);
            Assert.That(PlayerPrefs.GetInt(PurgeKey), Is.EqualTo(1));
        }

        [Test] public void FailedShutdownStaysPendingAndCannotBeBypassedByEnabling()
        {
            _sdk.DenialFailures = 2;
            _sdk.ShutdownFailures = 2;
            bool observedPending = false;
            _service.Changed += () => observedPending = _service.DisablePending;
            ExpectFailure(); _service.SetEnabled(false);
            Assert.That(observedPending, Is.True);
            Assert.That(_service.IsReady, Is.False);
            ExpectFailure(); _service.SetEnabled(true);
            Assert.That(_service.Enabled, Is.False);
            Assert.That(_service.DisablePending, Is.True);
            Assert.That(_sdk.Steps, Does.Not.Contain("grant"));

            // Exercise the runtime retry at timeScale zero, as in the menu.
            float previousScale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                SetField("_retryAt", -1f);
                Invoke("Update");
            }
            finally { Time.timeScale = previousScale; }
            Assert.That(_service.DisablePending, Is.False);
            Assert.That(observedPending, Is.False);
            Assert.That(_sdk.IsInitialized, Is.False);
            Assert.That(PlayerPrefs.GetInt(PurgeKey), Is.EqualTo(1));

            _sdk.Steps.Clear();
            _service.SetEnabled(true);
            Assert.That(_sdk.Steps, Is.EqualTo(new[] { "initialize", "deny", "reset", "grant", "identify" }));
            Assert.That(_service.IsReady, Is.True);
            Assert.That(PlayerPrefs.HasKey(PurgeKey), Is.False);
        }

        [Test] public void FocusReturnRetriesAnIncompleteShutdown()
        {
            _sdk.ShutdownFailures = 1;
            ExpectFailure(); _service.SetEnabled(false);
            Assert.That(_service.DisablePending, Is.True);
            Invoke("OnApplicationFocus", true);
            Assert.That(_service.DisablePending, Is.False);
            Assert.That(_sdk.IsInitialized, Is.False);
        }

        [Test] public void PartialEnableIsRolledBackAndRequiresPurgeBeforeRetry()
        {
            _sdk.Shutdown(); _sdk.Steps.Clear(); _sdk.IdentifyFails = true;
            ExpectFailure(); _service.SetEnabled(true);
            Assert.That(_sdk.Steps, Is.EqualTo(new[] { "initialize", "grant", "identify", "deny", "shutdown" }));
            Assert.That(_service.Enabled, Is.False);
            Assert.That(_service.IsReady, Is.False);
            Assert.That(_sdk.IsInitialized, Is.False);
            Assert.That(PlayerPrefs.GetInt(PurgeKey), Is.EqualTo(1));
        }
    }
}
