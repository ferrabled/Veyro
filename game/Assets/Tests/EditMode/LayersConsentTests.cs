using System.Collections;
using System.Linq;
using Layers.Unity;
using Layers.Unity.Internal;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MotionRunner.Tests
{
    // Real managed SDK with its official native/network-free test seam.
    public sealed class LayersConsentTests
    {
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

        static void Initialize() => LayersSDK.Initialize(new LayersConfig
        {
            AppId = "test-veyro-consent",
            AutoTrackAppOpen = false,
            AutoCaptureLifecycle = false,
            AutoTrackDeepLinks = false,
            AutoTrackExceptions = false,
            CaptureLogErrors = false,
            AutoTrackPerformance = false,
            EnableAndroidAttribution = false
        });

        [Test] public void StartupDisablesAdvertisingBeforeAnyTrackedActivity()
        {
            Initialize();
            Assert.That(LayersSDK.IsInitialized, Is.True);
            Assert.That(LayersTestMode.ConsentCalls.First(), Does.Contain("\"advertising\":false"));
            Assert.That(LayersTestMode.ConsentCalls.First(), Does.Contain("\"analytics\":true"));
            Assert.That(LayersTestMode.TrackedEvents.Select(e => e.eventName),
                Is.EqualTo(new[] { "$first_open" }));
        }

        [Test] public void RevocationWithdrawsBothConsentsAndTearsDownIdentity()
        {
            Initialize();
            LayersSDK.Identify("test-consented-player");
            LayersSDK.Track("daily_run_started");
            LayersSDK.RevokeConsentAndShutdown();
            Assert.That(LayersSDK.IsInitialized, Is.False);
            Assert.That(LayersSDK.UserId, Is.Null);
            Assert.That(LayersTestMode.ConsentCalls.Last(), Does.Contain("\"analytics\":false"));
            Assert.That(LayersTestMode.ConsentCalls.Last(), Does.Contain("\"advertising\":false"));
            // Only normal Shutdown's crash-safety persist; no Reset() pre-flush.
            Assert.That(LayersTestMode.FlushCount, Is.EqualTo(1));
            LayersSDK.RevokeConsentAndShutdown();
            Assert.That(LayersTestMode.FlushCount, Is.EqualTo(1));
        }

        [Test] public void ReenableRequiresFreshInitializationAndDoesNotRestorePreviousUser()
        {
            Initialize();
            LayersSDK.Identify("old-player");
            LayersSDK.RevokeConsentAndShutdown();
            Initialize();
            Assert.That(LayersSDK.UserId, Is.Null);
            LayersSDK.Identify("new-player");
            Assert.That(LayersSDK.UserId, Is.EqualTo("new-player"));
            Assert.That(LayersTestMode.ConsentCalls.Last(), Does.Contain("\"advertising\":false"));
        }
    }
}
