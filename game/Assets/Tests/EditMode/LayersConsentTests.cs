using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Layers.Unity;
using Layers.Unity.Internal;
using MotionRunner.Growth.Layers;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MotionRunner.Tests
{
    // Stock managed SDK (v3.3.2) with its official native/network-free test
    // seam. The three patch-only APIs the embedded fork added are gone, so
    // what is asserted here is the replacement design: consent is granted
    // only after Initialize returns, and a re-enable that follows an opt-out
    // denies consent, Resets (dropping the disabled period's queue) and only
    // then grants analytics again.
    public sealed class LayersConsentTests
    {
        const string OldSupportId = "veyro-old-install";
        const string NewSupportId = "veyro-new-install";

        [UnitySetUp] public IEnumerator SetUp()
        {
            // The SDK owns a persistent MonoBehaviour even when transport is mocked.
            yield return new EnterPlayMode();
            LayersTestMode.Enable();
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            LayersSDK.Shutdown();
            LayersTestMode.Disable();
            yield return new ExitPlayMode();
        }

        // The production config shape, with a test app id — so a change to
        // BuildConfig is exercised here rather than silently diverging.
        static LayersConfig Config() =>
            LayersConsentFlow.BuildConfig("test-veyro-consent", development: true);

        /// <summary>
        /// Delegates every call to the real SDK and snapshots the mock's
        /// counters around the calls whose ORDER carries the privacy
        /// guarantee. Nothing here changes behaviour; it only makes the
        /// interleaving of consent, flush and reset observable, which the
        /// static <see cref="LayersSDK"/> facade alone is not.
        /// </summary>
        sealed class RecordingSdk : ILayersSdkOps
        {
            readonly ILayersSdkOps _inner = LayersSdkOps.Default;
            public readonly List<string> Steps = new List<string>();
            public int ResetCalls;
            public int FlushCountAtFirstConsent = -1;
            public int FlushCountBeforeReset = -1;
            public int FlushCountAfterReset = -1;
            public string ConsentAtReset;
            public string UserIdAfterReset = "<not captured>";

            public bool IsInitialized => _inner.IsInitialized;

            public void Initialize(LayersConfig config)
            {
                Steps.Add("initialize");
                _inner.Initialize(config);
            }

            public void SetConsent(bool analytics, bool advertising)
            {
                if (FlushCountAtFirstConsent < 0) FlushCountAtFirstConsent = LayersTestMode.FlushCount;
                Steps.Add("consent:" + analytics + "," + advertising);
                _inner.SetConsent(analytics, advertising);
            }

            public void Reset()
            {
                ResetCalls++;
                Steps.Add("reset");
                // Reset() flushes internally, so the counters taken either
                // side of this single call bracket that flush exactly.
                ConsentAtReset = LayersTestMode.ConsentCalls.LastOrDefault();
                FlushCountBeforeReset = LayersTestMode.FlushCount;
                _inner.Reset();
                FlushCountAfterReset = LayersTestMode.FlushCount;
                UserIdAfterReset = LayersSDK.UserId;
            }

            public void Identify(string userId)
            {
                Steps.Add("identify:" + userId);
                _inner.Identify(userId);
            }

            public void Shutdown()
            {
                Steps.Add("shutdown");
                _inner.Shutdown();
            }
        }

        [Test] public void EnableGrantsAnalyticsOnlyBeforeAnythingCanBeSent()
        {
            var sdk = new RecordingSdk();

            Assert.That(LayersConsentFlow.Enable(sdk, Config(), purgePending: false, OldSupportId), Is.True);

            Assert.That(LayersSDK.IsInitialized, Is.True);
            Assert.That(LayersTestMode.ConsentCalls, Has.Count.EqualTo(1));
            Assert.That(LayersTestMode.ConsentCalls.First(), Does.Contain("\"analytics\":true"));
            Assert.That(LayersTestMode.ConsentCalls.First(), Does.Contain("\"advertising\":false"));
            // Nothing had been flushed when consent was set, and nothing has
            // been flushed since: Initialize only queues, and the periodic
            // flush timer is not even constructed in test mode.
            Assert.That(sdk.FlushCountAtFirstConsent, Is.Zero);
            Assert.That(LayersTestMode.FlushCount, Is.Zero);
            Assert.That(LayersTestMode.IdentifyCalls.Last(), Is.EqualTo(OldSupportId));
            Assert.That(sdk.Steps, Is.EqualTo(new[]
            {
                "initialize", "consent:True,False", "identify:" + OldSupportId
            }));

            // What stock Initialize queues, per the mock: the Rust core's
            // once-per-install "$first_open" (emitted from inside
            // SetDeviceContext) and then "layers_init_timing", which stock
            // tracks unconditionally. A third event, "$app_open", comes from
            // LayersRunner.Start (AutoCaptureLifecycle) — a MonoBehaviour
            // Start that has not necessarily run by the time this synchronous
            // test body asserts, so it is allowed but not required.
            var events = LayersTestMode.TrackedEvents.Select(e => e.eventName).ToArray();
            Assert.That(events.Take(2), Is.EqualTo(new[] { "$first_open", "layers_init_timing" }));
            Assert.That(events, Is.SubsetOf(new[] { "$first_open", "layers_init_timing", "$app_open" }));
        }

        [Test] public void DisableWithdrawsBothConsentsThenShutsDownExactlyOnce()
        {
            var sdk = new RecordingSdk();
            LayersConsentFlow.Enable(sdk, Config(), purgePending: false, OldSupportId);

            Assert.That(LayersConsentFlow.Disable(sdk), Is.True);

            Assert.That(LayersTestMode.ConsentCalls.Last(), Does.Contain("\"analytics\":false"));
            Assert.That(LayersTestMode.ConsentCalls.Last(), Does.Contain("\"advertising\":false"));
            Assert.That(LayersSDK.IsInitialized, Is.False);
            Assert.That(LayersSDK.UserId, Is.Null);
            // Shutdown's crash-safety persist only — no Reset() pre-flush.
            Assert.That(LayersTestMode.FlushCount, Is.EqualTo(1));

            // Second call must not flush, shut down or re-deny anything.
            Assert.That(LayersConsentFlow.Disable(sdk), Is.False);
            Assert.That(LayersTestMode.FlushCount, Is.EqualTo(1));
            Assert.That(LayersTestMode.ConsentCalls, Has.Count.EqualTo(2));
            Assert.That(sdk.Steps.Count(s => s == "shutdown"), Is.EqualTo(1));
        }

        [Test] public void PurgingReenableDeniesConsentBeforeResetFlushesAndRotatesIdentity()
        {
            var sdk = new RecordingSdk();
            LayersConsentFlow.Enable(sdk, Config(), purgePending: false, OldSupportId);
            LayersConsentFlow.Disable(sdk);
            int flushesBefore = LayersTestMode.FlushCount;

            Assert.That(LayersConsentFlow.Enable(sdk, Config(), purgePending: true, NewSupportId), Is.True);

            // Denial before flush: Reset() is the only thing that flushes in
            // this sequence, and at the instant it did, the most recent
            // consent call the core had seen was the denial.
            Assert.That(sdk.ResetCalls, Is.EqualTo(1));
            Assert.That(sdk.FlushCountBeforeReset, Is.EqualTo(flushesBefore));
            Assert.That(sdk.FlushCountAfterReset, Is.EqualTo(flushesBefore + 1));
            Assert.That(sdk.ConsentAtReset, Does.Contain("\"analytics\":false"));
            Assert.That(sdk.ConsentAtReset, Does.Contain("\"advertising\":false"));

            // Reset cleared the identity (and, in the real core, rotated the
            // device id and dropped the queued events).
            Assert.That(sdk.UserIdAfterReset, Is.Null);

            Assert.That(LayersTestMode.ConsentCalls.Last(), Does.Contain("\"analytics\":true"));
            Assert.That(LayersTestMode.ConsentCalls.Last(), Does.Contain("\"advertising\":false"));
            Assert.That(LayersTestMode.IdentifyCalls.Last(), Is.EqualTo(NewSupportId));
            Assert.That(LayersSDK.UserId, Is.EqualTo(NewSupportId));
            Assert.That(sdk.Steps, Is.EqualTo(new[]
            {
                "initialize", "consent:True,False", "identify:" + OldSupportId,
                "consent:False,False", "shutdown",
                "initialize", "consent:False,False", "reset",
                "consent:True,False", "identify:" + NewSupportId
            }));
        }

        [Test] public void NonPurgingReenableNeitherResetsNorFlushes()
        {
            var sdk = new RecordingSdk();
            LayersConsentFlow.Enable(sdk, Config(), purgePending: false, OldSupportId);
            LayersConsentFlow.Disable(sdk);
            int flushesBefore = LayersTestMode.FlushCount;

            Assert.That(LayersConsentFlow.Enable(sdk, Config(), purgePending: false, OldSupportId), Is.True);

            Assert.That(sdk.ResetCalls, Is.Zero);
            Assert.That(LayersTestMode.FlushCount, Is.EqualTo(flushesBefore));
            Assert.That(LayersTestMode.ConsentCalls.Last(), Does.Contain("\"analytics\":true"));
            Assert.That(LayersSDK.UserId, Is.EqualTo(OldSupportId));
        }
    }
}
