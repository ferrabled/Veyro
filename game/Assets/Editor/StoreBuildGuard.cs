using System;
using System.Linq;
using MotionRunner.Commerce.RevenueCat;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MotionRunner.EditorTools
{
    /// Runs inside every Android build, however it was started (BuildScript, editor GUI,
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
    public sealed class StoreBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
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
    }
}
