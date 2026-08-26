using System;
using System.Linq;
using MotionRunner.Commerce.RevenueCat;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace MotionRunner.EditorTools
{
    /// Runs inside every Android build, however it was started (BuildScript, editor GUI,
    /// anything), and fails the build on a RevenueCat key/artifact mismatch:
    ///
    ///   .aab  => VEYRO_STORE_BUILD must be set and the Play key valid  (store artifact)
    ///   .apk  => VEYRO_STORE_BUILD must be absent                      (Test Store key)
    ///
    /// Together with BuildScript.ApplyStoreKeyPairing this makes shipping the Test Store
    /// key to Google Play a build failure rather than a production incident
    /// (COSMETICS_CATALOG §6: "The test key must never ship in a store build").
    public sealed class StoreBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;

            bool bundle = EditorUserBuildSettings.buildAppBundle;
            bool storeDefine = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Contains(BuildScript.StoreBuildDefine);

            if (bundle && !storeDefine)
                throw new BuildFailedException(
                    "An .aab is a Play upload artifact: build it with BuildScript.BuildAndroidBundle " +
                    "so VEYRO_STORE_BUILD selects the Play key. Without it this bundle would ship " +
                    "the Test Store key (or none).");

            if (!bundle && storeDefine)
                throw new BuildFailedException(
                    "VEYRO_STORE_BUILD is set on an APK build - dev builds ship the Test Store key. " +
                    "Use BuildScript.BuildAndroid, or clear the define from the Android player settings.");

            if (bundle && (string.IsNullOrEmpty(RevenueCatKeys.PlayStoreKey) ||
                           !RevenueCatKeys.PlayStoreKey.StartsWith("goog_")))
                throw new BuildFailedException(
                    "Store build without a valid Play key: set RevenueCatKeys.PlayStoreKey " +
                    "(starts with 'goog_') before building an .aab.");
        }
    }
}
