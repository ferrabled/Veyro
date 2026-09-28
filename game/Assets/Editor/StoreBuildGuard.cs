using System;
using System.Linq;
using MotionRunner.Commerce.RevenueCat;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MotionRunner.EditorTools
{
    /// Runs inside every Android and iOS build, however it was started (BuildScript, editor GUI,
    /// anything), and fails the build on a RevenueCat key/artifact mismatch:
    ///
    ///   .aab  => VEYRO_STORE_BUILD set, Play key valid, dev define absent  (store artifact)
    ///   .apk  => VEYRO_STORE_BUILD absent; VEYRO_DEV_STORE allowed only on a
    ///            DEVELOPMENT build with a valid test_ key (the SDK closes a
    ///            non-debuggable build that uses a test key - seen on device 27 Aug)
    ///
    /// Together with BuildScript.ApplyStoreKeyPairing this makes shipping the Test Store
    /// key in a store artifact a build failure rather than a production incident
    /// (COSMETICS_CATALOG §6: "The test key must never ship in a store build").
    ///
    /// iOS has exactly one artifact - the App Store export from BuildScript.BuildIOS - so its
    /// rules are absolute rather than per flavour (see CheckIos). Every other target passes.
    public sealed class StoreBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.iOS)
            {
                CheckIos(report);
                return;
            }

            if (report.summary.platform != BuildTarget.Android) return;

            bool bundle = EditorUserBuildSettings.buildAppBundle;
            var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            bool storeDefine = defines.Contains(BuildScript.StoreBuildDefine);
            bool devDefine = defines.Contains(BuildScript.DevStoreDefine);

            if (bundle && !storeDefine)
                throw new BuildFailedException(
                    "An .aab is a Play upload artifact: build it with BuildScript.BuildAndroidBundle " +
                    "so VEYRO_STORE_BUILD selects the Play key. Without it this bundle would ship " +
                    "no key (or the wrong one).");

            if (bundle && devDefine)
                throw new BuildFailedException(
                    "VEYRO_DEV_STORE is set on an .aab build - the Test Store key must never reach " +
                    "a store artifact. Use BuildScript.BuildAndroidBundle.");

            if (!bundle && storeDefine)
                throw new BuildFailedException(
                    "VEYRO_STORE_BUILD is set on an APK build - sideload APKs never carry the Play key. " +
                    "Use BuildScript.BuildAndroid (keyless) or BuildAndroidDev (Test Store).");

            if (devDefine && (report.summary.options & BuildOptions.Development) == 0)
                throw new BuildFailedException(
                    "VEYRO_DEV_STORE needs a Development (debuggable) build: RevenueCat closes a " +
                    "release build that uses a Test Store key. Use BuildScript.BuildAndroidDev.");

            if (devDefine && (string.IsNullOrEmpty(RevenueCatKeys.TestStoreKey) ||
                              !RevenueCatKeys.TestStoreKey.StartsWith("test_")))
                throw new BuildFailedException(
                    "Dev-store build without a valid Test Store key: set RevenueCatKeys.TestStoreKey " +
                    "(starts with 'test_') before building.");

            if (bundle && (string.IsNullOrEmpty(RevenueCatKeys.PlayStoreKey) ||
                           !RevenueCatKeys.PlayStoreKey.StartsWith("goog_")))
                throw new BuildFailedException(
                    "Store build without a valid Play key: set RevenueCatKeys.PlayStoreKey " +
                    "(starts with 'goog_') before building an .aab.");
        }

        /// Every iOS build is a store upload (TestFlight is the only way onto an iPhone - no dev,
        /// no Simulator flavour), so every iOS build must be the one BuildIOS makes: App Store key
        /// selected, no Test Store key, not debuggable, the real bundle ID, a camera purpose string
        /// (Apple rejects the binary without one, the app uses WebCamTexture) and a real build number.
        static void CheckIos(BuildReport report)
        {
#if UNITY_IOS
            var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.iOS)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            if (!defines.Contains(BuildScript.StoreBuildDefine))
                throw new BuildFailedException(
                    "An iOS build is an App Store upload: build it with BuildScript.BuildIOS so " +
                    "VEYRO_STORE_BUILD selects the App Store key. Without it the app ships no key.");

            if (defines.Contains(BuildScript.DevStoreDefine))
                throw new BuildFailedException(
                    "VEYRO_DEV_STORE is set on an iOS build - the Test Store key must never reach " +
                    "an App Store upload, and iOS has no dev flavour. Use BuildScript.BuildIOS.");

            if (string.IsNullOrEmpty(RevenueCatKeys.AppStoreKey) || !RevenueCatKeys.AppStoreKey.StartsWith("appl_"))
                throw new BuildFailedException(
                    "iOS build without a valid App Store key: set RevenueCatKeys.AppStoreKey " +
                    "(starts with 'appl_').");

            if ((report.summary.options & BuildOptions.Development) != 0)
                throw new BuildFailedException(
                    "iOS builds are release only: a Development build cannot go to TestFlight, and " +
                    "there is no iOS dev flavour. Use BuildScript.BuildIOS.");

            string bundleId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);
            if (bundleId != BuildScript.IosBundleId)
                throw new BuildFailedException(
                    $"iOS bundle ID is '{bundleId}', expected '{BuildScript.IosBundleId}' (the App ID and " +
                    "App Store Connect record RevenueCat checks). Use BuildScript.BuildIOS.");

            if (string.IsNullOrWhiteSpace(PlayerSettings.iOS.cameraUsageDescription))
                throw new BuildFailedException(
                    "iOS build without NSCameraUsageDescription: camera mode uses WebCamTexture and " +
                    "App Store Connect rejects the binary without a purpose string. Use BuildScript.BuildIOS.");

            string buildNumber = PlayerSettings.iOS.buildNumber;
            if (!int.TryParse(buildNumber, System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out int number) || number <= 0)
                throw new BuildFailedException(
                    $"iOS build number is '{buildNumber}': it must be a positive integer, one higher per " +
                    "upload. Set VEYRO_IOS_BUILD_NUMBER and use BuildScript.BuildIOS.");
#else
            // The editor was not compiled for iOS, so neither RevenueCatKeys.AppStoreKey nor the
            // SDKs' Xcode post-processors (RevenueCat, OneSignal, IosBuildPostProcess) exist in
            // this domain: whatever came out would be missing all of them.
            throw new BuildFailedException(
                "iOS build from an editor compiled for another target: run BuildScript.BuildIOS " +
                "with -buildTarget iOS on the command line.");
#endif
        }
    }
}
