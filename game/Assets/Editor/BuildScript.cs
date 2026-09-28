using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using MotionRunner.Commerce.RevenueCat;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MotionRunner.EditorTools
{
    /// Headless build entry points, invoked via:
    ///   Unity -batchmode -quit -projectPath game -executeMethod MotionRunner.EditorTools.BuildScript.BuildAndroid
    ///   Unity -batchmode -quit -buildTarget iOS -projectPath game -executeMethod MotionRunner.EditorTools.BuildScript.BuildIOS
    public static class BuildScript
    {
        const string ScenePath = "Assets/Scenes/Main.unity";

        /// Compile-time switches for RevenueCat key selection (RevenueCatKeys.ActiveKey).
        /// StoreBuildDefine is present only while building the Play upload artifact (or the
        /// iOS App Store export); DevStoreDefine only in the debuggable Test-Store flavour.
        /// StoreBuildGuard enforces the pairing on every Android and iOS build, whoever started it.
        public const string StoreBuildDefine = "VEYRO_STORE_BUILD";
        public const string DevStoreDefine = "VEYRO_DEV_STORE";
        // Immutable once the first Play Console upload happens, changeable freely until then.
        // Reverse-DNS of ferrabled.com, a domain the owner holds, so the namespace cannot
        // collide with another developer's. Keep the iOS bundle ID identical (T-032):
        // RevenueCat verifies Shipaton eligibility by bundle ID.
        const string PackageName = "com.ferrabled.veyro.run";

        // ---- iOS (BuildIOS). One artifact only: the App Store / TestFlight Xcode export. ----
        // The values below are applied to PlayerSettings for the duration of the export and
        // restored afterwards, so ProjectSettings.asset carries none of them; StoreBuildGuard and
        // IosBuildPostProcess re-check them inside BuildPlayer.

        /// Identical to the Android package (T-032): RevenueCat checks Shipaton eligibility by
        /// bundle ID, and the App ID + App Store Connect record already exist under it.
        public const string IosBundleId = PackageName;
        /// CFBundleShortVersionString. Must equal the version of the App Store Connect record.
        public const string IosVersion = "1.0.0";
        /// Apple Team ID of the owner's individual developer account.
        public const string IosTeamId = "69J7W5URX7";
        /// OneSignal 5.4.0's floor, and Unity 6's.
        public const string IosMinimumOs = "15.0";
        /// NSCameraUsageDescription, word for word as declared in docs/store-kit/APP_STORE_LISTING.md
        /// §7 - the App Privacy answers rely on "never leaves it" being true.
        public const string IosCameraUsage =
            "Camera mode uses the front camera to follow your movements. Images are processed on your iPhone and never leave it.";
        /// CFBundleURLSchemes: the challenge-link scheme AndroidChallengeLinks registers on Android.
        public const string IosUrlScheme = AndroidChallengeLinks.Scheme;
        /// Kill switches (docs/IOS_BUILD_RUNBOOK.md): env VEYRO_IOS_NO_ONESIGNAL=1 / VEYRO_IOS_NO_LAYERS=1
        /// at export compile the define in, and the SDK's wrapper then returns its Fake.
        public const string NoOneSignalDefine = "VEYRO_NO_ONESIGNAL";
        public const string NoLayersDefine = "VEYRO_NO_LAYERS";
        /// Keeps App UI (a dependency of com.unity.ai.inference) out of the player: its runtime
        /// asmdefs are constrained to `UNITY_EDITOR || !APP_UI_EDITOR_ONLY`. Android carries it in
        /// ProjectSettings.asset; iOS gets it from here so both players compile the same set.
        const string AppUiEditorOnlyDefine = "APP_UI_EDITOR_ONLY";

        static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        static string RepoRoot => Path.GetFullPath(Path.Combine(ProjectRoot, ".."));

        public static void BuildAndroid() => Build(PackageName, "Veyro Run", "MotionRunner.apk", bundle: false);

        /// The RevenueCat device-test flavour: a DEVELOPMENT (debuggable) APK carrying the
        /// Test Store key. Debuggable is not optional - the SDK closes a release build that
        /// uses a test key ("Wrong API Key", seen on device 27 Aug). Same package name, so
        /// it installs over the game; never upload it anywhere.
        public static void BuildAndroidDev() =>
            Build(PackageName, "Veyro Run", "MotionRunnerDev.apk", bundle: false, development: true);

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

        /// Isolated fixture app: no RevenueCat/Supabase traffic or writes to the real player's save.
        public static void BuildAndroidCosmeticQa() =>
            Build("com.ferrabled.veyro.cosmeticqa", "Veyro Cosmetic QA", "VeyroCosmeticQA.apk", false, true, true);

        /// The App Store / TestFlight artifact: an Xcode project for the iPhone device SDK, exported
        /// to builds/ios/VeyroRun-xcode/ (wiped first) and archived on the cloud Mac per
        /// docs/IOS_BUILD_RUNBOOK.md. Release only, and the only iOS flavour: TestFlight is the one
        /// way onto an iPhone here, and Layers' native library is arm64-device-only, so there is no
        /// dev and no Simulator variant (IOS_HANDOFF decision 1). Environment:
        ///   VEYRO_IOS_BUILD_NUMBER  CFBundleVersion - required, a positive integer, one higher per
        ///                           upload (App Store Connect rejects a reused one)
        ///   VEYRO_IOS_NO_ONESIGNAL  "1" compiles VEYRO_NO_ONESIGNAL in (kill switch, optional)
        ///   VEYRO_IOS_NO_LAYERS     "1" compiles VEYRO_NO_LAYERS in (kill switch, optional)
        /// Needs -buildTarget iOS on the command line: RevenueCat's, OneSignal's and our own Xcode
        /// post-processors, and RevenueCatKeys.AppStoreKey, only compile under UNITY_IOS.
        public static void BuildIOS()
        {
#if UNITY_IOS
            BuildIos();
#else
            Debug.LogError("BuildIOS needs the editor compiled for iOS: add -buildTarget iOS to the command line. " +
                           "The SDK Xcode post-processors and the App Store key only exist under UNITY_IOS.");
            EditorApplication.Exit(1);
#endif
        }

        static void Build(string packageName, string productName, string outputName, bool bundle,
            bool development = false, bool cosmeticQa = false)
        {
            EnsureScene();

            string previousPackage = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            string previousProduct = PlayerSettings.productName;
            bool previousBundle = EditorUserBuildSettings.buildAppBundle;
            int previousVersionCode = PlayerSettings.Android.bundleVersionCode;
            string previousVersion = PlayerSettings.bundleVersion;
            bool previousTimings = PlayerSettings.enableFrameTimingStats;
            string previousDefines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android);

            // Everything that mutates the project goes inside the try, not before it: the
            // setup itself throws (a missing Play key, an unset VEYRO_KEYSTORE, a malformed
            // VEYRO_VERSION_CODE), and a throw before the try left the dev package name, the
            // bundle flag, the store define and — worst — the local keystore path serialized
            // into the committed ProjectSettings.asset. The finally is the only exit.
            try
            {
                // Launcher icon from Assets/Art/Icon/, re-applied on every build so a fresh checkout
                // (or a scratch copy) never ships Unity's default icon. Warns and continues if the
                // art is missing; it is a persistent setting, so it is deliberately not restored.
                ProjectSetup.EnsureAppIcon();

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
                PlayerSettings.enableFrameTimingStats = development;
                if (development) PlayerSettings.bundleVersion = DevVersion(previousVersion);
                ApplyStoreKeyPairing(bundle, development, previousDefines);
                if(cosmeticQa) PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android,
                    PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android)+";VEYRO_COSMETIC_QA");
                if (bundle)
                {
                    ApplyUploadKeystoreFromEnv();
                    string versionCode = Env("VEYRO_VERSION_CODE");
                    if (versionCode != null) PlayerSettings.Android.bundleVersionCode = int.Parse(versionCode);
                }

                string outPath = Path.Combine(RepoRoot, "builds", outputName);
                Directory.CreateDirectory(Path.GetDirectoryName(outPath));

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = outPath,
                    target = BuildTarget.Android,
                    options = development ? BuildOptions.Development : BuildOptions.None
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
                // The dev stamp is a property of one artifact, never of the project: bundleVersion
                // serializes into the committed ProjectSettings.asset, and a build timestamp left
                // there would ride into the next .aab as the version name Play shows.
                PlayerSettings.bundleVersion = previousVersion;
                PlayerSettings.enableFrameTimingStats = previousTimings;
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

#if UNITY_IOS
        const string IosExportFolder = "VeyroRun-xcode";

        /// BuildIOS's body, the sibling of Build() above: same shape (icon, settings, build, and a
        /// finally that restores everything it touched), iOS values instead of Android ones.
        static void BuildIos()
        {
            EnsureScene();

            var ios = NamedBuildTarget.iOS;
            string previousBundleId = PlayerSettings.GetApplicationIdentifier(ios);
            string previousProduct = PlayerSettings.productName;
            string previousVersion = PlayerSettings.bundleVersion;
            string previousBuildNumber = PlayerSettings.iOS.buildNumber;
            var previousDevice = PlayerSettings.iOS.targetDevice;
            var previousSdk = PlayerSettings.iOS.sdkVersion;
            string previousMinimumOs = PlayerSettings.iOS.targetOSVersionString;
            var previousBackend = PlayerSettings.GetScriptingBackend(ios);
            int previousArchitecture = PlayerSettings.GetArchitecture(ios);
            bool previousStatusBarHidden = PlayerSettings.statusBarHidden;
            bool previousFullScreen = PlayerSettings.iOS.requiresFullScreen;
            string previousTeam = PlayerSettings.iOS.appleDeveloperTeamID;
            bool previousAutomaticSigning = PlayerSettings.iOS.appleEnableAutomaticSigning;
            string previousCamera = PlayerSettings.iOS.cameraUsageDescription;
            string[] previousSchemes = PlayerSettings.iOS.iOSUrlSchemes;
            bool previousTimings = PlayerSettings.enableFrameTimingStats;
            string previousDefines = PlayerSettings.GetScriptingDefineSymbols(ios);
            var previousXcodeConfig = EditorUserBuildSettings.iOSXcodeBuildConfig;
            bool previousSymlink = EditorUserBuildSettings.symlinkSources;

            // Same rule as Build(): the setup itself throws (no build number, a malformed kill
            // switch), so every mutation sits inside the try and the finally is the only exit.
            try
            {
                // The environment first: a missing build number fails before anything is touched.
                int buildNumber = IosBuildNumberFromEnv();
                bool noOneSignal = KillSwitchFromEnv("VEYRO_IOS_NO_ONESIGNAL");
                bool noLayers = KillSwitchFromEnv("VEYRO_IOS_NO_LAYERS");

                // Default + Android + every iOS kind, including the 1024 px App Store icon.
                ProjectSetup.EnsureAppIcon();

                PlayerSettings.SetApplicationIdentifier(ios, IosBundleId);
                PlayerSettings.productName = "Veyro Run";
                PlayerSettings.bundleVersion = IosVersion;
                PlayerSettings.iOS.buildNumber = buildNumber.ToString(CultureInfo.InvariantCulture);
                PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;
                PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
                PlayerSettings.iOS.targetOSVersionString = IosMinimumOs;
                PlayerSettings.SetScriptingBackend(ios, ScriptingImplementation.IL2CPP);
                PlayerSettings.SetArchitecture(ios, (int)AppleMobileArchitecture.ARM64);
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
                PlayerSettings.statusBarHidden = true;
                PlayerSettings.iOS.requiresFullScreen = true;
                PlayerSettings.iOS.appleDeveloperTeamID = IosTeamId;
                PlayerSettings.iOS.appleEnableAutomaticSigning = true;
                PlayerSettings.iOS.cameraUsageDescription = IosCameraUsage;
                PlayerSettings.iOS.iOSUrlSchemes = new[] { IosUrlScheme };
                // Same as Android (LICENSING_REVENUE §2.12): no "Made with Unity" splash.
                PlayerSettings.SplashScreen.show = false;
                PlayerSettings.SplashScreen.showUnityLogo = false;
                PlayerSettings.enableFrameTimingStats = false;
                // Archive always uses Release; this pins the scheme's Run action to it as well.
                EditorUserBuildSettings.iOSXcodeBuildConfig = XcodeBuildConfig.Release;
                // Copies, never links back into this project: the export is zipped and moved to
                // another machine.
                EditorUserBuildSettings.symlinkSources = false;
                ApplyIosStoreDefines(previousDefines, noOneSignal, noLayers);

                string outPath = Path.Combine(RepoRoot, "builds", "ios", IosExportFolder);
                // A fresh export every time, never an append: a stale Podfile, NSE target or Pods/
                // from an earlier run must not ride into the next upload.
                if (Directory.Exists(outPath)) Directory.Delete(outPath, true);
                Directory.CreateDirectory(Path.GetDirectoryName(outPath));

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = outPath,
                    target = BuildTarget.iOS,
                    options = BuildOptions.None
                };

                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                {
                    Debug.LogError($"Build failed: {report.summary.result}");
                    EditorApplication.Exit(1);
                    return;
                }
                Debug.Log($"Build OK: {report.summary.outputPath} (Xcode project for {IosBundleId} " +
                          $"{IosVersion} ({buildNumber}); next: zip it and follow docs/IOS_BUILD_RUNBOOK.md " +
                          "on the Mac - chmod +x, pod install, open Unity-iPhone.xcworkspace)");
            }
            finally
            {
                // None of the iOS values may stick to ProjectSettings.asset: the build number in
                // particular is a property of one upload, never of the project.
                PlayerSettings.SetApplicationIdentifier(ios, previousBundleId);
                PlayerSettings.productName = previousProduct;
                PlayerSettings.bundleVersion = previousVersion;
                PlayerSettings.iOS.buildNumber = previousBuildNumber;
                PlayerSettings.iOS.targetDevice = previousDevice;
                PlayerSettings.iOS.sdkVersion = previousSdk;
                PlayerSettings.iOS.targetOSVersionString = previousMinimumOs;
                PlayerSettings.SetScriptingBackend(ios, previousBackend);
                PlayerSettings.SetArchitecture(ios, previousArchitecture);
                PlayerSettings.statusBarHidden = previousStatusBarHidden;
                PlayerSettings.iOS.requiresFullScreen = previousFullScreen;
                PlayerSettings.iOS.appleDeveloperTeamID = previousTeam;
                PlayerSettings.iOS.appleEnableAutomaticSigning = previousAutomaticSigning;
                PlayerSettings.iOS.cameraUsageDescription = previousCamera;
                PlayerSettings.iOS.iOSUrlSchemes = previousSchemes;
                PlayerSettings.enableFrameTimingStats = previousTimings;
                PlayerSettings.SetScriptingDefineSymbols(ios, previousDefines);
                EditorUserBuildSettings.iOSXcodeBuildConfig = previousXcodeConfig;
                EditorUserBuildSettings.symlinkSources = previousSymlink;
            }
        }

        /// The iOS half of the pairing rule (COSMETICS_CATALOG §6): the App Store export is a store
        /// artifact, so it always compiles with VEYRO_STORE_BUILD (-> RevenueCatKeys.AppStoreKey)
        /// and never with VEYRO_DEV_STORE. StoreBuildGuard re-checks inside BuildPlayer.
        static void ApplyIosStoreDefines(string currentDefines, bool noOneSignal, bool noLayers)
        {
            string key = RevenueCatKeys.AppStoreKey;
            if (string.IsNullOrEmpty(key) || !key.StartsWith("appl_"))
                throw new BuildFailedException(
                    "The App Store export ships the APP STORE public SDK key: set RevenueCatKeys.AppStoreKey " +
                    "(RevenueCat -> Veyro Run -> API keys -> App Store app, starts with 'appl_').");

            var defines = currentDefines
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(d => d != StoreBuildDefine && d != DevStoreDefine &&
                            d != NoOneSignalDefine && d != NoLayersDefine)
                .ToList();
            if (!defines.Contains(AppUiEditorOnlyDefine)) defines.Add(AppUiEditorOnlyDefine);
            defines.Add(StoreBuildDefine);
            if (noOneSignal) defines.Add(NoOneSignalDefine);
            if (noLayers) defines.Add(NoLayersDefine);

            string joined = string.Join(";", defines);
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.iOS, joined);
            Debug.Log($"iOS App Store export: scripting defines '{joined}'" +
                      (noOneSignal ? "; OneSignal switched OFF (VEYRO_IOS_NO_ONESIGNAL=1)" : "") +
                      (noLayers ? "; Layers switched OFF (VEYRO_IOS_NO_LAYERS=1)" : "") + ".");
        }

        /// CFBundleVersion for this upload. Required rather than defaulted: a silently reused
        /// number is rejected by App Store Connect only after the archive has been uploaded,
        /// which on a rented Mac is the most expensive place to find out.
        static int IosBuildNumberFromEnv()
        {
            string raw = Env("VEYRO_IOS_BUILD_NUMBER");
            if (raw == null)
                throw new BuildFailedException(
                    "VEYRO_IOS_BUILD_NUMBER is required: the CFBundleVersion of this upload, a positive " +
                    "integer one higher than the last build sent to App Store Connect (PowerShell: " +
                    "$env:VEYRO_IOS_BUILD_NUMBER='1'). Nothing was exported.");
            if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value <= 0)
                throw new BuildFailedException(
                    $"VEYRO_IOS_BUILD_NUMBER must be a positive integer, got '{raw}'. Nothing was exported.");
            return value;
        }

        /// "1" switches the SDK off, unset or "0" keeps it; anything else is a typo that must not
        /// silently ship the SDK the person meant to remove (or remove the one they meant to keep).
        static bool KillSwitchFromEnv(string name)
        {
            string raw = Env(name);
            if (raw == null || raw == "0") return false;
            if (raw == "1") return true;
            throw new BuildFailedException($"{name} must be 1 (switch the SDK off) or unset/0 (keep it), got '{raw}'.");
        }
#endif

        /// The version name a DEVELOPMENT artifact carries, and the string BuildInfo puts under
        /// the menu title: `1.0.0-dev.20260902-1422.5aeab74`. Three questions, one line —
        /// which release line (semver, straight from ProjectSettings), when it was built, and
        /// from which commit.
        ///
        /// It is a semver pre-release suffix on purpose: `1.0.0-dev.…` sorts *below* `1.0.0`, so a
        /// dev build can never be mistaken for the release it was cut from. It also lands in
        /// android:versionName, which is what makes "what is actually installed on this phone?"
        /// answerable over adb without launching the app.
        ///
        /// Never applied to the .aab or the release APK: only Build(development: true) reaches
        /// here, and the finally restores ProjectSettings either way.
        static string DevVersion(string baseVersion)
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
            return baseVersion + "-dev." + stamp + "." + GitShortSha();
        }

        /// The commit the working tree is on, `nogit` when git cannot answer (a source zip, a
        /// scratch copy made without .git — see CLAUDE.md gotcha #4). A build must never fail
        /// because of a label, so every failure path returns a placeholder rather than throwing;
        /// the date half of the stamp still identifies the artifact.
        static string GitShortSha()
        {
            try
            {
                var info = new ProcessStartInfo("git", "rev-parse --short HEAD")
                {
                    WorkingDirectory = RepoRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var process = Process.Start(info))
                {
                    if (process == null) return "nogit";
                    // Wait before reading: ReadToEnd blocks until git closes stdout, which would
                    // make the timeout unreachable if git hangs. Reading after exit is safe here —
                    // a short SHA is a few bytes, nowhere near filling the pipe buffer.
                    if (!process.WaitForExit(5000))
                    {
                        try { process.Kill(); } catch { /* may have exited already */ }
                        return "nogit";
                    }
                    string output = process.StandardOutput.ReadToEnd().Trim();
                    return process.ExitCode == 0 && output.Length > 0 ? output : "nogit";
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Build stamp: git short SHA unavailable (" + e.Message + ")");
                return "nogit";
            }
        }

        /// The pairing rule that must never break (COSMETICS_CATALOG §6): the .aab store build
        /// compiles with the Play key, the debuggable dev flavour compiles with the Test Store
        /// key, every other artifact ships no key at all (store disabled, fail-open). This
        /// validates the keys and sets the matching define for exactly the duration of the
        /// build (defines restored in the finally above). StoreBuildGuard re-checks inside
        /// BuildPlayer, so an editor-GUI build cannot slip by.
        static void ApplyStoreKeyPairing(bool storeBuild, bool devStore, string currentDefines)
        {
            var defines = currentDefines
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(d => d != StoreBuildDefine && d != DevStoreDefine)
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
            else if (devStore)
            {
                string key = RevenueCatKeys.TestStoreKey;
                if (string.IsNullOrEmpty(key) || !key.StartsWith("test_"))
                    throw new BuildFailedException(
                        "The dev-store flavour exists to test against the Test Store: set " +
                        "RevenueCatKeys.TestStoreKey (starts with 'test_') before BuildAndroidDev.");
                defines.Add(DevStoreDefine);
            }
            else
            {
                // The keyless flavour, and the one a store device-test lands in by accident:
                // AGENTS.md's "On device" loop USED to say BuildAndroid, so "build it and check
                // the store" produced an APK that boots with the store disabled — logcat says
                // "[Store] No RevenueCat API key in this build flavour", the panel says "store
                // unavailable right now", and nothing in the build output hinted why (cost a
                // debugging session, 31 Aug; the doc line now says BuildAndroidDev). Say it here
                // too, where the build log is already being read — the doc is one fix, this is
                // the one that survives the next person not reading it.
                Debug.Log(
                    "Build flavour ships NO RevenueCat key: the store will be disabled on device " +
                    "(a release APK must not carry the test_ key — the SDK force-closes it). " +
                    "To test the store use BuildAndroidDev (debuggable, Test Store key) or " +
                    "BuildAndroidBundle (.aab, Play key).");
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
