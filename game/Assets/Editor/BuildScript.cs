using System;
using System.IO;
using System.Linq;
using MotionRunner.Commerce.RevenueCat;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MotionRunner.EditorTools
{
    /// Headless build entry points, invoked via:
    ///   Unity -batchmode -quit -projectPath game -executeMethod MotionRunner.EditorTools.BuildScript.BuildAndroid
    public static class BuildScript
    {
        const string ScenePath = "Assets/Scenes/Main.unity";

        /// Compile-time switch for RevenueCat key selection (RevenueCatKeys.ActiveKey):
        /// present only while building the Play upload artifact. StoreBuildGuard enforces
        /// the pairing on every Android build, whoever started it.
        public const string StoreBuildDefine = "VEYRO_STORE_BUILD";
        // Immutable once the first Play Console upload happens, changeable freely until then.
        // Reverse-DNS of ferrabled.com, a domain the owner holds, so the namespace cannot
        // collide with another developer's. Keep the iOS bundle ID identical (T-032):
        // RevenueCat verifies Shipaton eligibility by bundle ID.
        const string PackageName = "com.ferrabled.veyro.run";

        static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        static string RepoRoot => Path.GetFullPath(Path.Combine(ProjectRoot, ".."));

        public static void BuildAndroid() => Build(PackageName, "Veyro Run", "MotionRunner.apk", bundle: false);

        /// The Play Store upload artifact. Google Play only accepts App Bundles for new apps,
        /// signed with the upload key (Play App Signing then re-signs for distribution). The
        /// keystore lives outside the repo and arrives via environment variables:
        ///   VEYRO_KEYSTORE       path to the upload keystore (.keystore / .jks)
        ///   VEYRO_KEYSTORE_PASS  keystore password
        ///   VEYRO_KEYALIAS       key alias            (optional, default "upload")
        ///   VEYRO_KEYALIAS_PASS  key password         (optional, default: keystore password)
        ///   VEYRO_VERSION_CODE   versionCode override (optional; Play rejects a reused code)
        public static void BuildAndroidBundle() =>
            Build(PackageName, "Veyro Run", "MotionRunner.aab", bundle: true);

        /// The camera-feature branch's sideload test build. Its own package name so installing it
        /// can never replace the working game on a device, and its own APK name so it can never
        /// overwrite builds/MotionRunner.apk. (Since the owner's 24 Aug call to ship camera mode
        /// in v1.0, the real package carries the camera too — this stays purely a dev flavour.)
        public static void BuildAndroidCamDev() =>
            Build("com.ferrabled.veyro.camdev", "Veyro Cam Dev", "VeyroCamDev.apk", bundle: false);

        static void Build(string packageName, string productName, string outputName, bool bundle)
        {
            EnsureScene();

            string previousPackage = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            string previousProduct = PlayerSettings.productName;
            bool previousBundle = EditorUserBuildSettings.buildAppBundle;
            int previousVersionCode = PlayerSettings.Android.bundleVersionCode;
            string previousDefines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android);

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, packageName);
            PlayerSettings.productName = productName;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            // Play's floor for new apps is API 36 from 31 Aug 2026. Pinned rather than left at
            // "highest installed" so the store requirement can't drift with the local SDK.
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel36;

            // No "Made with Unity" splash. Unity 6 Personal is allowed to turn it off
            // (docs/LICENSING_REVENUE.md §2.12) — the attribution line only becomes
            // required if the game ever shows a credits screen. Set here rather than left
            // to the editor UI so a headless build can never quietly ship it again.
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;

            EditorUserBuildSettings.buildAppBundle = bundle;
            ApplyStoreKeyPairing(bundle, previousDefines);
            if (bundle)
            {
                ApplyUploadKeystoreFromEnv();
                string versionCode = Env("VEYRO_VERSION_CODE");
                if (versionCode != null) PlayerSettings.Android.bundleVersionCode = int.Parse(versionCode);
            }

            string outPath = Path.Combine(RepoRoot, "builds", outputName);
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));

            try
            {
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = outPath,
                    target = BuildTarget.Android,
                    options = BuildOptions.None
                };

                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                {
                    Debug.LogError($"Build failed: {report.summary.result}");
                    EditorApplication.Exit(1);
                    return;
                }
                Debug.Log($"Build OK: {report.summary.outputPath} ({report.summary.totalSize / (1024 * 1024)} MB)");
            }
            finally
            {
                // A dev-flavoured package name must not stick to the project after the build.
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, previousPackage);
                PlayerSettings.productName = previousProduct;
                EditorUserBuildSettings.buildAppBundle = previousBundle;
                PlayerSettings.Android.bundleVersionCode = previousVersionCode;
                PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, previousDefines);
                if (bundle)
                {
                    // keystoreName/keyaliasName serialize into ProjectSettings.asset (the passwords
                    // don't) — wipe them so no local keystore path outlives the build in a committed
                    // file. Unity serializes the wiped name as "{inproject}: " — that string is the
                    // expected post-build state, not a stray keystore reference.
                    PlayerSettings.Android.useCustomKeystore = false;
                    PlayerSettings.Android.keystoreName = "";
                    PlayerSettings.Android.keystorePass = "";
                    PlayerSettings.Android.keyaliasName = "";
                    PlayerSettings.Android.keyaliasPass = "";
                }
            }
        }

        /// The pairing rule that must never break: APKs (dev/sideload) compile with the Test
        /// Store key, .aab store builds compile with the Play key (COSMETICS_CATALOG §6).
        /// Selection is the VEYRO_STORE_BUILD define; this validates the keys and sets the
        /// define for exactly the duration of the build (restored in the finally above).
        /// StoreBuildGuard re-checks inside BuildPlayer, so an editor-GUI build cannot slip by.
        static void ApplyStoreKeyPairing(bool storeBuild, string currentDefines)
        {
            var defines = currentDefines
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(d => d != StoreBuildDefine)
                .ToList();

            if (storeBuild)
            {
                string key = RevenueCatKeys.PlayStoreKey;
                if (string.IsNullOrEmpty(key) || !key.StartsWith("goog_"))
                    throw new BuildFailedException(
                        "Store builds ship the PLAY public SDK key: set RevenueCatKeys.PlayStoreKey " +
                        "(RevenueCat -> Veyro Run -> API keys -> Play Store app, starts with 'goog_'). " +
                        "An .aab must never carry the Test Store key.");
                defines.Add(StoreBuildDefine);
            }
            else
            {
                string key = RevenueCatKeys.TestStoreKey;
                if (!string.IsNullOrEmpty(key) && !key.StartsWith("test_"))
                    throw new BuildFailedException(
                        "Dev builds ship the Test Store key ('test_...'). RevenueCatKeys.TestStoreKey " +
                        "holds something else - a Play key here would hit the real backend from sideloads.");
                if (string.IsNullOrEmpty(key))
                    Debug.LogWarning("[Store] RevenueCatKeys.TestStoreKey is empty - this dev build's store is disabled (fail-open).");
            }

            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, string.Join(";", defines));
        }

        static void ApplyUploadKeystoreFromEnv()
        {
            string keystore = Env("VEYRO_KEYSTORE");
            string storePass = Env("VEYRO_KEYSTORE_PASS");
            if (keystore == null || storePass == null)
                throw new BuildFailedException(
                    "Play upload builds are signed with the upload key: set VEYRO_KEYSTORE and " +
                    "VEYRO_KEYSTORE_PASS (optional: VEYRO_KEYALIAS, VEYRO_KEYALIAS_PASS). Keep the " +
                    "keystore outside the repo — creation steps in docs/PREREQUISITES.md D5.");
            if (!File.Exists(keystore))
                throw new BuildFailedException($"VEYRO_KEYSTORE points at a missing file: {keystore}");

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass = storePass;
            PlayerSettings.Android.keyaliasName = Env("VEYRO_KEYALIAS") ?? "upload";
            PlayerSettings.Android.keyaliasPass = Env("VEYRO_KEYALIAS_PASS") ?? storePass;
        }

        static string Env(string name)
        {
            string value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrEmpty(value) ? null : value;
        }

        static void EnsureScene()
        {
            string absScene = Path.Combine(ProjectRoot, ScenePath);
            if (File.Exists(absScene)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(absScene));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
    }
}
