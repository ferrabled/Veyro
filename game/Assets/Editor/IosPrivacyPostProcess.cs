using System;
#if UNITY_IOS
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEngine;
#endif

namespace MotionRunner.EditorTools
{
    /// The two native files IosPrivacyPostProcess writes into every iOS export in place of SDK
    /// sources that would make App Store Connect ask for a purpose string the game has no use for
    /// (ITMS-90683, IOS_HANDOFF decision 5). Compiled for every build target - plain strings, no iOS
    /// API - so LayersAttStubTests can hold them against the SDKs' own [DllImport] declarations from
    /// an EditMode run on either scratch copy. ASCII only, and free of the Apple API and plist-key
    /// names the export inspection greps for, so a scan of the export never trips on a stub.
    public static class IosNativeStubs
    {
        /// Where the stubs land, relative to the export root. Never Assets/Plugins/iOS: Unity would
        /// copy and compile a plugin from there as well, next to the generated file.
        public const string Folder = "Libraries/VeyroPrivacyStubs";

        public sealed class Stub
        {
            /// File name of the SDK source the stub replaces (removed from the export).
            public readonly string Replaces;
            /// UPM package, and package-relative C# file, holding the [DllImport] side.
            public readonly string Package, DeclaringScript;
            /// Every function the stub exports starts with this; the test compares the full sets.
            public readonly string SymbolPrefix;
            /// .m, not .c: Xcode then compiles it exactly like the Objective-C file it replaces,
            /// under the same UnityFramework prefix header the Layers bridges already build with.
            public readonly string FileName;
            public readonly string Source;
            public string ExportPath => Folder + "/" + FileName;

            internal Stub(string replaces, string package, string declaringScript, string symbolPrefix,
                string fileName, string source)
            {
                Replaces = replaces;
                Package = package;
                DeclaringScript = declaringScript;
                SymbolPrefix = symbolPrefix;
                FileName = fileName;
                Source = source;
            }
        }

        /// Layers 3.3.2's Plugins/iOS/LayersATTBridge.m imports Apple's tracking-authorization and
        /// advertising-identifier frameworks. The game never asks for tracking and never calls
        /// LayersSDK.RequestTrackingPermission; with this stub the SDK's InitIOSModules reads a
        /// null IDFV and skips it, so Layers sends no Apple identifier at all.
        public static readonly Stub LayersAtt = new Stub(
            "LayersATTBridge.m", "com.layers.analytics", "Runtime/Platform/iOS/ATTModule.cs", "layers_att_",
            "VeyroLayersAttStub.m", Lines(
                "/* VeyroLayersAttStub.m - written into every iOS export by",
                " * MotionRunner.EditorTools.IosPrivacyPostProcess; do not edit, the next export overwrites it.",
                " *",
                " * Stands in for the Layers SDK's LayersATTBridge.m, which that post-process deletes: the same six",
                " * C functions its Runtime/Platform/iOS/ATTModule.cs P/Invokes, answering 'no tracking",
                " * authorization, not determined, no identifiers' without linking Apple's tracking-authorization",
                " * or advertising-identifier frameworks. The binary then needs no tracking purpose string",
                " * (ITMS-90683), no prompt can ever appear, and Layers reads neither the IDFA nor the IDFV.",
                " *",
                " * Pure C without headers: syntax-checked on Windows (clang -ffreestanding), compiled by Xcode as",
                " * Objective-C, exactly like the file it replaces. */",
                "",
                "typedef void (*VeyroLayersAttCallback)(int status);",
                "void free(void *ptr);",
                "",
                "int layers_att_is_available(void);",
                "int layers_att_get_status(void);",
                "void layers_att_request_tracking(VeyroLayersAttCallback callback);",
                "const char *layers_att_get_idfa(void);",
                "const char *layers_att_get_idfv(void);",
                "void layers_att_free_string(char *ptr);",
                "",
                "/* C# declares bool with default marshalling, a 4-byte BOOL: answer a whole int. */",
                "int layers_att_is_available(void) { return 0; }",
                "",
                "/* 0 = not determined. */",
                "int layers_att_get_status(void) { return 0; }",
                "",
                "/* Shows nothing; answers 'not determined' at once. */",
                "void layers_att_request_tracking(VeyroLayersAttCallback callback)",
                "{",
                "    if (callback) callback(0);",
                "}",
                "",
                "/* NULL arrives as IntPtr.Zero, which ATTModule turns into a null identifier. */",
                "const char *layers_att_get_idfa(void) { return (const char *)0; }",
                "const char *layers_att_get_idfv(void) { return (const char *)0; }",
                "",
                "/* Nothing above allocates; kept so a caller passing a pointer still gets free(). */",
                "void layers_att_free_string(char *ptr) { free(ptr); }"));

        /// Input System 1.20's Plugins/iOS/iOSStepCounter.mm compiles Apple's pedometer into
        /// UnityFramework unconditionally: the package's only iOS setting (InputSettings.iOS.motionUsage)
        /// adds the motion purpose string, it never drops the source. iOSSupport.Initialize still asks
        /// "is a step counter available?" at every launch; the stub answers no, so no device is added.
        public static readonly Stub StepCounter = new Stub(
            "iOSStepCounter.mm", "com.unity.inputsystem", "InputSystem/Runtime/Plugins/iOS/iOSStepCounter.cs",
            "_iOSStepCounter", "VeyroStepCounterStub.m", Lines(
                "/* VeyroStepCounterStub.m - written into every iOS export by",
                " * MotionRunner.EditorTools.IosPrivacyPostProcess; do not edit, the next export overwrites it.",
                " *",
                " * Stands in for the Input System's iOSStepCounter.mm, which that post-process deletes: the same",
                " * five C functions its InputSystem/Runtime/Plugins/iOS/iOSStepCounter.cs P/Invokes, answering",
                " * 'no pedometer on this device' without touching Apple's motion-activity APIs, so the binary",
                " * needs no motion purpose string (ITMS-90683). The game has no use for a step counter; tilt",
                " * reads the accelerometer through Unity's own sensor code, which needs no purpose string.",
                " *",
                " * Pure C without headers: syntax-checked on Windows (clang -ffreestanding), compiled by Xcode as",
                " * Objective-C inside UnityFramework. */",
                "",
                "int _iOSStepCounterIsAvailable(void);",
                "int _iOSStepCounterGetAuthorizationStatus(void);",
                "int _iOSStepCounterEnable(int deviceId, void *callbacks, int sizeOfCallbacks);",
                "int _iOSStepCounterDisable(int deviceId);",
                "int _iOSStepCounterIsEnabled(int deviceId);",
                "",
                "/* 0 = unavailable: iOSSupport.Initialize never adds a step-counter device. */",
                "int _iOSStepCounterIsAvailable(void) { return 0; }",
                "",
                "/* 1 = restricted: this build can never read motion activity. */",
                "int _iOSStepCounterGetAuthorizationStatus(void) { return 1; }",
                "",
                "/* -1 = the C# side's kCommandFailure. callbacks is the marshalled iOSStepCounterCallbacks. */",
                "int _iOSStepCounterEnable(int deviceId, void *callbacks, int sizeOfCallbacks)",
                "{",
                "    (void)deviceId; (void)callbacks; (void)sizeOfCallbacks;",
                "    return -1;",
                "}",
                "",
                "int _iOSStepCounterDisable(int deviceId) { (void)deviceId; return -1; }",
                "",
                "int _iOSStepCounterIsEnabled(int deviceId) { (void)deviceId; return 0; }"));

        public static readonly Stub[] All = { LayersAtt, StepCounter };

        /// LF line endings whatever this .cs file is checked out with, so the export is byte-stable.
        static string Lines(params string[] lines) => string.Join("\n", lines) + "\n";
    }

#if UNITY_IOS
    /// Keeps the App Store export free of every purpose-string trigger the game has no use for
    /// (IOS_HANDOFF decision 5, Apple ITMS-90683) and writes the privacy manifest behind the App
    /// Privacy answers in docs/store-kit/APP_STORE_LISTING.md §3. In order:
    ///   1. refuses a LayersSettings asset (Layers' own post-processor would then write the tracking
    ///      string and SKAdNetwork keys and link the tracking frameworks);
    ///   2. deletes Layers' LayersATTBridge.m and the Input System's iOSStepCounter.mm from the
    ///      project and from disk, and compiles IosNativeStubs into UnityFramework in their place;
    ///   3. weak-links AdServices and StoreKit on UnityFramework for the Layers bridges that stay
    ///      (without this they arrive by module autolinking only);
    ///   4. removes AppTrackingTransparency / AdSupport / CoreLocation from the project and every
    ///      tracking, SKAdNetwork, motion and location key from every Info.plist;
    ///   5. rewrites UnityFramework/PrivacyInfo.xcprivacy: Unity's concatenation of its own and
    ///      Layers' manifests de-duplicated, the required-reason APIs the binary reaches added,
    ///      NSPrivacyTracking false, no tracking domains, collected data mirroring §3;
    ///   6. re-reads everything and fails the build if a forbidden key, framework, source or
    ///      trampoline flag survives, or the manifest collides with another one in a target.
    /// Order 1100: after every SDK post-processor (EDM4U 40/50, OneSignal 45, Layers 100,
    /// RevenueCat 999) and IosBuildPostProcess (1000), so nothing that runs later puts a key back.
    public sealed class IosPrivacyPostProcess : IPostprocessBuildWithReport
    {
        public const int CallbackOrder = IosBuildPostProcess.CallbackOrder + 100;
        public int callbackOrder => CallbackOrder;

        const string Tag = "[iOS privacy]";
        const string ManifestFile = "UnityFramework/PrivacyInfo.xcprivacy";

        /// Info.plist keys the game must never carry: each asks the player for something the game
        /// does not do, or tells Apple an SDK attributes ads.
        static readonly string[] ForbiddenKeys =
        {
            "NSUserTrackingUsageDescription",         // App Tracking Transparency prompt
            "SKAdNetworkItems",                       // ad-network attribution
            "NSAdvertisingAttributionReportEndpoint", // SKAdNetwork postback copies
            "NSMotionUsageDescription",               // pedometer (stubbed)
        };
        const string ForbiddenKeyPrefix = "NSLocation"; // every location purpose string and option

        /// Every Info.plist in the export: the app's, UnityFramework's, OneSignal's extension's.
        static readonly string[] PlistFiles =
            { "Info.plist", "UnityFramework/Info.plist", IosBuildPostProcess.OneSignalExtensionTarget + "/Info.plist" };

        /// No target may link tracking authorization, the advertising identifier or location.
        static readonly string[] ForbiddenFrameworks =
            { "AppTrackingTransparency.framework", "AdSupport.framework", "CoreLocation.framework" };
        static readonly Regex ForbiddenFrameworkToken =
            new Regex(@"\b(AppTrackingTransparency|AdSupport|CoreLocation)\b");

        /// Linked weak on UnityFramework for the Layers bridges that stay (AdServices token read,
        /// SKAdNetwork). Both exist on every iOS 15 device; weak only relaxes the load command.
        static readonly string[] WeakFrameworks = { "AdServices.framework", "StoreKit.framework" };

        /// Apple API names no native plugin source under Libraries/ may mention once the two SDK
        /// files are gone: each one maps to a purpose string.
        static readonly Regex ForbiddenNativeApi = new Regex(
            @"\b(ATTrackingManager|ASIdentifierManager|CLLocationManager|CMPedometer|CMMotionActivityManager)\b");
        static readonly string[] NativeSourceExtensions = { ".m", ".mm", ".c", ".cpp", ".h", ".swift" };

        /// Trampoline switches Unity turns on when C# touches the advertising identifier or
        /// Input.location; either compiles tracking or location code into UnityFramework.
        static readonly string[] TrampolineFlags = { "UNITY_USES_IAD", "UNITY_USES_LOCATION" };

        const string UserDefaults = "NSPrivacyAccessedAPICategoryUserDefaults";
        const string FileTimestamp = "NSPrivacyAccessedAPICategoryFileTimestamp";
        const string SystemBootTime = "NSPrivacyAccessedAPICategorySystemBootTime";
        const string DiskSpace = "NSPrivacyAccessedAPICategoryDiskSpace";

        /// Required-reason APIs the UnityFramework binary reaches, whatever the SDK manifests say.
        /// Everything Unity compiles for the game - engine (UnityRuntime is a static archive linked
        /// into UnityFramework), il2cpp and our C# - ends up in that one binary, so its manifest
        /// has to cover all of it:
        static readonly (string category, string reason)[] RequiredReasons =
        {
            // PlayerPrefs is NSUserDefaults (UnityRuntime PlatformPlayerPrefs): settings, progress,
            // the analytics choice and support ID. Same app only.
            (UserDefaults, "CA92.1"),
            // File/Directory calls on persistentDataPath (stat/fstat in il2cpp and the engine): the
            // recovery file, Layers' queue. Files inside the app container only.
            (FileTimestamp, "C617.1"),
            // Stopwatch (the camera speed gate), Time.*: mach_absolute_time in il2cpp and the
            // engine, systemUptime in the engine's input. Elapsed time only (RunSession's free-run
            // salt, which leaves the device in challenge links, is a GUID on iOS for this reason).
            (SystemBootTime, "35F9.1"),
            // statvfs / NSFileSystemSize in the engine (PlatformLocalFileSystem, DeviceInfoWrapper):
            // free-space checks before writing. Unity's own manifest does not declare them.
            (DiskSpace, "E174.1"),
        };

        const string AppFunctionality = "NSPrivacyCollectedDataTypePurposeAppFunctionality";
        const string Analytics = "NSPrivacyCollectedDataTypePurposeAnalytics";

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS) return;
            string root = report.summary.outputPath;

            RefuseLayersSettings();
            PatchProject(root);
            PatchInfoPlists(root);
            WritePrivacyManifest(root);
            Verify(root);
        }

        /// Layers' LayersPostBuildProcessor (order 100) does nothing without a LayersSettings asset
        /// in a Resources folder, and must stay that way: with one it writes the tracking string,
        /// SKAdNetwork keys and links the tracking frameworks. The game configures Layers in code
        /// (LayersConsentFlow.BuildConfig).
        static void RefuseLayersSettings()
        {
            var settings = Resources.Load("LayersSettings");
            if (settings == null) return;
            throw new BuildFailedException(
                $"{Tag} A LayersSettings asset exists ({AssetDatabase.GetAssetPath(settings)}): Layers' iOS " +
                "post-processor then writes NSUserTrackingUsageDescription and SKAdNetwork keys and links " +
                "AppTrackingTransparency/AdSupport. Delete it; Veyro configures Layers in code.");
        }

        static void PatchProject(string root)
        {
            string path = PBXProject.GetPBXProjectPath(root);
            var project = new PBXProject();
            project.ReadFromFile(path);
            string framework = project.GetUnityFrameworkTargetGuid();

            foreach (var stub in IosNativeStubs.All)
                ReplaceWithStub(root, project, framework, stub);

            var weak = new List<string>();
            foreach (string name in WeakFrameworks)
            {
                if (project.ContainsFramework(framework, name)) continue;
                project.AddFrameworkToProject(framework, name, true);
                weak.Add(name);
            }

            foreach (string name in ForbiddenFrameworks)
            {
                string guid = project.FindFileGuidByRealPath("System/Library/Frameworks/" + name, PBXSourceTree.Sdk)
                              ?? project.FindFileGuidByProjectPath("Frameworks/" + name);
                if (guid == null) continue;
                project.RemoveFile(guid); // out of every target's link phase, and out of the project
                Debug.LogWarning($"{Tag} Removed {name} from the Xcode project: some SDK or post-processor " +
                                 "linked it, and it would bring a purpose-string requirement the game does not meet.");
            }

            project.WriteToFile(path);
            Debug.Log($"{Tag} UnityFramework: " +
                      string.Join(", ", IosNativeStubs.All.Select(s => $"{s.Replaces} -> {s.FileName}")) + "; " +
                      (weak.Count > 0 ? "weak-linked " + string.Join(" + ", weak) : "AdServices/StoreKit already linked") + ".");
        }

        /// Takes the SDK file out of the project (every target) and off the disk, and compiles the
        /// stub into UnityFramework instead. Missing or ambiguous means the SDK changed: stop, so
        /// someone re-checks the stub against the new sources before an upload.
        static void ReplaceWithStub(string root, PBXProject project, string framework, IosNativeStubs.Stub stub)
        {
            var matches = project.GetRealPathsOfAllFiles(PBXSourceTree.Source)
                .Where(p => FileNameOf(p) == stub.Replaces)
                .ToList();
            if (matches.Count != 1)
                throw new BuildFailedException(
                    $"{Tag} Expected exactly one {stub.Replaces} in the Xcode project, found {matches.Count}" +
                    (matches.Count > 0 ? $" ({string.Join(", ", matches)})" : "") +
                    $": {stub.Package} changed. Re-check IosNativeStubs against its {stub.DeclaringScript} " +
                    "(LayersAttStubTests) before shipping iOS.");

            string realPath = matches[0];
            string guid = project.FindFileGuidByRealPath(realPath, PBXSourceTree.Source);
            if (guid == null)
                throw new BuildFailedException($"{Tag} {realPath} is listed but has no file reference.");
            project.RemoveFile(guid);
            string onDisk = Path.Combine(root, realPath);
            if (File.Exists(onDisk)) File.Delete(onDisk);

            string stubPath = Path.Combine(root, stub.ExportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(stubPath));
            File.WriteAllText(stubPath, stub.Source, new UTF8Encoding(false));
            if (project.FindFileGuidByProjectPath(stub.ExportPath) == null)
                project.AddFileToBuild(framework, project.AddFile(stub.ExportPath, stub.ExportPath, PBXSourceTree.Source));
        }

        static void PatchInfoPlists(string root)
        {
            foreach (string relative in PlistFiles)
            {
                string path = Path.Combine(root, relative);
                if (!File.Exists(path))
                {
                    if (relative == "Info.plist")
                        throw new BuildFailedException($"{Tag} {path} is missing.");
                    continue;
                }

                var plist = new PlistDocument();
                plist.ReadFromFile(path);
                var removed = ForbiddenKeysIn(plist.root);
                foreach (string key in removed) plist.root.values.Remove(key);
                if (RemoveLocationBackgroundMode(plist.root)) removed.Add("UIBackgroundModes/location");
                if (removed.Count == 0) continue;

                plist.WriteToFile(path);
                Debug.LogWarning($"{Tag} {relative}: removed {string.Join(", ", removed)} - some SDK or " +
                                 "post-processor added it, and the game neither tracks, locates nor counts steps.");
            }
        }

        static List<string> ForbiddenKeysIn(PlistElementDict dict) =>
            dict.values.Keys
                .Where(k => ForbiddenKeys.Contains(k) || k.StartsWith(ForbiddenKeyPrefix, StringComparison.Ordinal))
                .ToList();

        static bool RemoveLocationBackgroundMode(PlistElementDict dict)
        {
            if (!(dict["UIBackgroundModes"] is PlistElementArray modes)) return false;
            return modes.values.RemoveAll(m => m is PlistElementString s && s.value == "location") > 0;
        }

        /// Unity writes UnityFramework/PrivacyInfo.xcprivacy by concatenating its own manifest with
        /// every plugin's (Layers': UserDefaults, FileTimestamp, SystemBootTime), so FileTimestamp is
        /// listed twice and NSPrivacyTracking, NSPrivacyTrackingDomains and NSPrivacyCollectedDataTypes
        /// are absent. Rewritten here rather than shipped as Assets/Plugins/iOS/PrivacyInfo.xcprivacy,
        /// which Unity would only concatenate once more.
        static void WritePrivacyManifest(string root)
        {
            string path = Path.Combine(root, ManifestFile);
            if (!File.Exists(path))
                throw new BuildFailedException(
                    $"{Tag} {ManifestFile} is missing: Unity no longer writes the UnityFramework privacy " +
                    "manifest there. Find where it went before shipping; App Store Connect rejects uploads " +
                    "whose required-reason APIs are undeclared (ITMS-91053).");

            var plist = new PlistDocument();
            plist.ReadFromFile(path);
            var dict = plist.root;

            // A plugin declaring tracking means a tracking SDK is in the build: stop, never overwrite.
            if (dict["NSPrivacyTracking"] is PlistElementBoolean tracking && tracking.value)
                throw new BuildFailedException($"{Tag} {ManifestFile} declares NSPrivacyTracking = true: a tracking SDK is in the build.");
            if (dict["NSPrivacyTrackingDomains"] is PlistElementArray domains && domains.values.Count > 0)
                throw new BuildFailedException($"{Tag} {ManifestFile} lists tracking domains: a tracking SDK is in the build.");

            var reasons = new List<(string category, List<string> codes)>();
            int unityEntries = 0;
            if (dict["NSPrivacyAccessedAPITypes"] is PlistElementArray accessed)
                foreach (var entry in accessed.values.OfType<PlistElementDict>())
                {
                    string category = (entry["NSPrivacyAccessedAPIType"] as PlistElementString)?.value;
                    if (string.IsNullOrEmpty(category)) continue;
                    unityEntries++;
                    AddReasons(reasons, category, Strings(entry["NSPrivacyAccessedAPITypeReasons"]));
                }
            foreach (var (category, reason) in RequiredReasons)
                AddReasons(reasons, category, new[] { reason });

            var apiArray = dict.CreateArray("NSPrivacyAccessedAPITypes");
            foreach (var (category, codes) in reasons)
            {
                var entry = apiArray.AddDict();
                entry.SetString("NSPrivacyAccessedAPIType", category);
                var codeArray = entry.CreateArray("NSPrivacyAccessedAPITypeReasons");
                foreach (string code in codes) codeArray.AddString(code);
            }

            dict.SetBoolean("NSPrivacyTracking", false);
            dict.CreateArray("NSPrivacyTrackingDomains");
            int collected = WriteCollectedDataTypes(dict);

            plist.WriteToFile(path);
            Debug.Log($"{Tag} {ManifestFile}: tracking false, no tracking domains; {unityEntries} API entries " +
                      $"merged into {reasons.Count} categories (" +
                      string.Join("; ", reasons.Select(r => r.category.Replace("NSPrivacyAccessedAPICategory", "") +
                                                            " " + string.Join("+", r.codes))) +
                      $"); {collected} collected data types.");
        }

        static void AddReasons(List<(string category, List<string> codes)> reasons, string category, IEnumerable<string> codes)
        {
            var existing = reasons.FirstOrDefault(r => r.category == category);
            if (existing.codes == null)
            {
                existing = (category, new List<string>());
                reasons.Add(existing);
            }
            foreach (string code in codes)
                if (!string.IsNullOrEmpty(code) && !existing.codes.Contains(code)) existing.codes.Add(code);
        }

        /// The App Privacy answers of docs/store-kit/APP_STORE_LISTING.md §3, row for row: every row
        /// linked to the user, none used for tracking. Change both together. The listing's rule
        /// "drop the OneSignal/Layers rows if either SDK is switched off" follows the kill switches
        /// BuildIOS compiled in; RevenueCat and Supabase are always on.
        static IEnumerable<(string type, string[] purposes)> CollectedData(bool layers, bool oneSignal)
        {
            // RevenueCat: purchases and restores.
            yield return ("NSPrivacyCollectedDataTypePurchaseHistory", new[] { AppFunctionality, Analytics });
            // Supabase player ID, RevenueCat app user ID (and the Layers support ID).
            yield return ("NSPrivacyCollectedDataTypeUserID", new[] { AppFunctionality, Analytics });
            // RevenueCat, OneSignal and Layers installation IDs.
            yield return ("NSPrivacyCollectedDataTypeDeviceID", new[] { AppFunctionality, Analytics });
            // Runs and scores submitted to the leaderboard.
            yield return ("NSPrivacyCollectedDataTypeGameplayContent", new[] { AppFunctionality });
            // Layers events, OneSignal sessions and notification interactions.
            if (layers || oneSignal)
                yield return ("NSPrivacyCollectedDataTypeProductInteraction", new[] { Analytics });
            if (layers)
            {
                // Region Layers derives from the IP address; no location API is used.
                yield return ("NSPrivacyCollectedDataTypeCoarseLocation", new[] { Analytics });
                // Layers SDK delivery health counters.
                yield return ("NSPrivacyCollectedDataTypeOtherDiagnosticData", new[] { Analytics });
            }
        }

        /// Ours plus anything a plugin manifest already declared (types merged, purposes unioned);
        /// a plugin row used for tracking fails the build instead.
        static int WriteCollectedDataTypes(PlistElementDict dict)
        {
            var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.iOS)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            bool layers = !defines.Contains(BuildScript.NoLayersDefine);
            bool oneSignal = !defines.Contains(BuildScript.NoOneSignalDefine);

            var rows = new List<(string type, List<string> purposes)>();
            if (dict["NSPrivacyCollectedDataTypes"] is PlistElementArray existing)
                foreach (var entry in existing.values.OfType<PlistElementDict>())
                {
                    string type = (entry["NSPrivacyCollectedDataType"] as PlistElementString)?.value;
                    if (string.IsNullOrEmpty(type)) continue;
                    if (entry["NSPrivacyCollectedDataTypeTracking"] is PlistElementBoolean t && t.value)
                        throw new BuildFailedException($"{Tag} {ManifestFile} declares {type} as used for tracking.");
                    AddRow(rows, type, Strings(entry["NSPrivacyCollectedDataTypePurposes"]));
                }
            foreach (var (type, purposes) in CollectedData(layers, oneSignal))
                AddRow(rows, type, purposes);

            var array = dict.CreateArray("NSPrivacyCollectedDataTypes");
            foreach (var (type, purposes) in rows)
            {
                var entry = array.AddDict();
                entry.SetString("NSPrivacyCollectedDataType", type);
                entry.SetBoolean("NSPrivacyCollectedDataTypeLinked", true);
                entry.SetBoolean("NSPrivacyCollectedDataTypeTracking", false);
                var purposeArray = entry.CreateArray("NSPrivacyCollectedDataTypePurposes");
                foreach (string purpose in purposes) purposeArray.AddString(purpose);
            }
            if (!layers || !oneSignal)
                Debug.Log($"{Tag} Kill switch on ({(layers ? "" : "Layers ")}{(oneSignal ? "" : "OneSignal")}): " +
                          "its rows are left out of the manifest - drop them from App Privacy too.");
            return rows.Count;
        }

        static void AddRow(List<(string type, List<string> purposes)> rows, string type, IEnumerable<string> purposes)
        {
            var row = rows.FirstOrDefault(r => r.type == type);
            if (row.purposes == null)
            {
                row = (type, new List<string>());
                rows.Add(row);
            }
            foreach (string purpose in purposes)
                if (!string.IsNullOrEmpty(purpose) && !row.purposes.Contains(purpose)) row.purposes.Add(purpose);
        }

        static IEnumerable<string> Strings(PlistElement element) =>
            element is PlistElementArray array
                ? array.values.OfType<PlistElementString>().Select(s => s.value)
                : Enumerable.Empty<string>();

        // ---- 6. Everything re-read from disk; one exception listing every violation. ----

        static void Verify(string root)
        {
            var problems = new List<string>();
            VerifyProject(root, problems);
            VerifyNativeSources(root, problems);
            VerifyPlists(root, problems);
            VerifyManifest(root, problems);
            VerifyTrampolineAndPods(root, problems);

            if (problems.Count > 0)
                throw new BuildFailedException(
                    $"{Tag} The export would make App Store Connect ask for purpose strings or misstate privacy:\n  - " +
                    string.Join("\n  - ", problems));
            Debug.Log($"{Tag} Verified: {string.Join(" + ", IosNativeStubs.All.Select(s => s.Replaces))} gone, stubs " +
                      "compiled into UnityFramework, AdServices + StoreKit linked there; no tracking/SKAdNetwork/" +
                      "motion/location key in any Info.plist; no AppTrackingTransparency/AdSupport/CoreLocation in " +
                      "the project; one privacy manifest per target; UNITY_USES_IAD/UNITY_USES_LOCATION 0.");
        }

        static void VerifyProject(string root, List<string> problems)
        {
            string path = PBXProject.GetPBXProjectPath(root);
            string text = File.ReadAllText(path);
            var project = new PBXProject();
            project.ReadFromString(text);
            string framework = project.GetUnityFrameworkTargetGuid();

            string sources = ObjectBody(text, project.GetSourcesBuildPhaseByTarget(framework));
            string frameworks = ObjectBody(text, project.GetFrameworksBuildPhaseByTarget(framework));
            if (sources == null || frameworks == null)
            {
                problems.Add("could not read UnityFramework's Sources/Frameworks build phases from project.pbxproj");
                return;
            }

            foreach (var stub in IosNativeStubs.All)
            {
                if (text.Contains(stub.Replaces))
                    problems.Add($"project.pbxproj still references {stub.Replaces}");
                if (!sources.Contains($"/* {stub.FileName} in Sources */"))
                    problems.Add($"{stub.FileName} is not in UnityFramework's Sources phase");
            }

            foreach (string name in WeakFrameworks)
            {
                bool? weak = LinkedWeak(text, frameworks, name);
                if (weak == null) problems.Add($"{name} is not linked on UnityFramework");
                else if (weak == false)
                    Debug.LogWarning($"{Tag} {name} is linked strong on UnityFramework (some SDK added it first); " +
                                     "fine on iOS 15+, left as is.");
            }

            foreach (Match token in ForbiddenFrameworkToken.Matches(text))
                problems.Add($"project.pbxproj mentions {token.Value} (a framework link or linker flag)");

            // Two PrivacyInfo.xcprivacy in one target's Copy Bundle Resources is Xcode's "multiple
            // commands produce" error; UnityFramework's must be there exactly once to ship at all.
            foreach (Match target in Regex.Matches(text,
                         @"(?m)^\t\t(?<guid>[0-9A-F]{24}) /\* (?<name>[^\r\n]*?) \*/ = \{\r?\n\t\t\tisa = PBXNativeTarget;"))
            {
                string name = target.Groups["name"].Value;
                string body = ObjectBody(text, target.Groups["guid"].Value);
                var phase = body == null ? null : Regex.Match(body, @"(?<guid>[0-9A-F]{24}) /\* Resources \*/");
                int count = phase != null && phase.Success
                    ? Regex.Matches(ObjectBody(text, phase.Groups["guid"].Value) ?? "",
                        @"/\* PrivacyInfo\.xcprivacy in Resources \*/").Count
                    : 0;
                if (count > 1) problems.Add($"target {name} bundles {count} PrivacyInfo.xcprivacy files");
                if (target.Groups["guid"].Value == framework && count != 1)
                    problems.Add($"UnityFramework bundles {count} PrivacyInfo.xcprivacy files, expected 1 ({ManifestFile})");
            }
        }

        /// The body of one multi-line `GUID /* comment */ = { ... };` object (targets, build phases),
        /// as Unity's pbxproj writer lays them out: two tabs in, closing brace on its own line.
        static string ObjectBody(string pbxproj, string guid)
        {
            if (string.IsNullOrEmpty(guid)) return null;
            var match = Regex.Match(pbxproj,
                @"(?m)^\t\t" + Regex.Escape(guid) + @" (?:/\*[^\r\n]*?\*/ )?= \{\r?\n(?<body>.*?)\r?\n\t\t\};",
                RegexOptions.Singleline);
            return match.Success ? match.Groups["body"].Value : null;
        }

        /// Null when the Frameworks phase does not list `name`; otherwise whether its build file
        /// carries ATTRIBUTES = (Weak, ).
        static bool? LinkedWeak(string pbxproj, string phaseBody, string name)
        {
            bool? linked = null;
            foreach (Match entry in Regex.Matches(phaseBody,
                         @"(?<guid>[0-9A-F]{24}) /\* " + Regex.Escape(name) + @" in Frameworks \*/"))
            {
                var line = Regex.Match(pbxproj,
                    @"(?m)^\t\t" + entry.Groups["guid"].Value + @" /\*[^\r\n]*?\*/ = \{isa = PBXBuildFile;[^\r\n]*");
                if (line.Success && line.Value.Contains("Weak")) return true;
                linked = false;
            }
            return linked;
        }

        static void VerifyNativeSources(string root, List<string> problems)
        {
            foreach (var stub in IosNativeStubs.All)
            {
                string path = Path.Combine(root, stub.ExportPath);
                if (!File.Exists(path) || File.ReadAllText(path) != stub.Source)
                    problems.Add($"{stub.ExportPath} is missing or differs from IosNativeStubs");
            }

            string libraries = Path.Combine(root, "Libraries");
            if (!Directory.Exists(libraries)) return;
            string ownFolder = Path.GetFullPath(Path.Combine(root, IosNativeStubs.Folder));
            foreach (string file in Directory.GetFiles(libraries, "*", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);
                string relative = file.Substring(root.Length).TrimStart('/', '\\').Replace('\\', '/');
                if (IosNativeStubs.All.Any(s => s.Replaces == name))
                    problems.Add($"{relative} is still on disk");
                if (Path.GetFullPath(file).StartsWith(ownFolder, StringComparison.OrdinalIgnoreCase)) continue;
                if (!NativeSourceExtensions.Contains(Path.GetExtension(file).ToLowerInvariant())) continue;
                var api = ForbiddenNativeApi.Match(File.ReadAllText(file));
                if (api.Success)
                    problems.Add($"{relative} uses {api.Value}: a native plugin now needs a purpose string " +
                                 "(strip and stub it here, or drop the plugin)");
            }
        }

        static void VerifyPlists(string root, List<string> problems)
        {
            foreach (string relative in PlistFiles)
            {
                string path = Path.Combine(root, relative);
                if (!File.Exists(path)) continue;
                var plist = new PlistDocument();
                plist.ReadFromFile(path);
                foreach (string key in ForbiddenKeysIn(plist.root))
                    problems.Add($"{relative} carries {key}");
                if (plist.root["UIBackgroundModes"] is PlistElementArray modes &&
                    modes.values.OfType<PlistElementString>().Any(m => m.value == "location"))
                    problems.Add($"{relative} UIBackgroundModes contains location");
            }
        }

        static void VerifyManifest(string root, List<string> problems)
        {
            string path = Path.Combine(root, ManifestFile);
            if (!File.Exists(path))
            {
                problems.Add($"{ManifestFile} is missing");
                return;
            }
            var plist = new PlistDocument();
            plist.ReadFromFile(path);
            var dict = plist.root;

            if (!(dict["NSPrivacyTracking"] is PlistElementBoolean tracking) || tracking.value)
                problems.Add($"{ManifestFile}: NSPrivacyTracking is not false");
            if (!(dict["NSPrivacyTrackingDomains"] is PlistElementArray domains) || domains.values.Count > 0)
                problems.Add($"{ManifestFile}: NSPrivacyTrackingDomains is not an empty array");

            var categories = (dict["NSPrivacyAccessedAPITypes"] as PlistElementArray)?.values
                .OfType<PlistElementDict>()
                .Select(e => ((e["NSPrivacyAccessedAPIType"] as PlistElementString)?.value,
                              Strings(e["NSPrivacyAccessedAPITypeReasons"]).ToList()))
                .ToList() ?? new List<(string, List<string>)>();
            foreach (var duplicate in categories.GroupBy(c => c.Item1).Where(g => g.Count() > 1))
                problems.Add($"{ManifestFile}: {duplicate.Key} is listed {duplicate.Count()} times");
            foreach (var (category, reason) in RequiredReasons)
                if (!categories.Any(c => c.Item1 == category && c.Item2.Contains(reason)))
                    problems.Add($"{ManifestFile}: {category} lacks reason {reason}");

            var collected = (dict["NSPrivacyCollectedDataTypes"] as PlistElementArray)?.values
                .OfType<PlistElementDict>().ToList();
            if (collected == null || collected.Count == 0)
                problems.Add($"{ManifestFile}: NSPrivacyCollectedDataTypes is empty, App Privacy §3 declares data");
            else if (collected.Any(e => !(e["NSPrivacyCollectedDataTypeTracking"] is PlistElementBoolean t) || t.value))
                problems.Add($"{ManifestFile}: a collected data type is not marked tracking = false");
        }

        static void VerifyTrampolineAndPods(string root, List<string> problems)
        {
            string preprocessor = Path.Combine(root, "Classes", "Preprocessor.h");
            if (!File.Exists(preprocessor))
                problems.Add("Classes/Preprocessor.h is missing: cannot confirm the trampoline compiles no tracking/location code");
            else
            {
                string text = File.ReadAllText(preprocessor);
                foreach (string flag in TrampolineFlags)
                {
                    var value = Regex.Match(text, @"(?m)^#define\s+" + flag + @"\s+(\d+)");
                    if (!value.Success || value.Groups[1].Value != "0")
                        problems.Add($"Classes/Preprocessor.h: {flag} is {(value.Success ? value.Groups[1].Value : "undefined")}, " +
                                     "expected 0 - some C# now reads " +
                                     (flag == "UNITY_USES_IAD" ? "the advertising identifier" : "Input.location") +
                                     ", which compiles tracking/location code into UnityFramework");
                }
            }

            string podfile = Path.Combine(root, "Podfile");
            if (!File.Exists(podfile))
                Debug.LogWarning($"{Tag} No Podfile in the export (EDM4U off?): the OneSignal location pod check was skipped.");
            else if (File.ReadAllText(podfile).Contains("OneSignalLocation"))
                problems.Add("Podfile pulls OneSignalLocation: OneSignal's location module is back on " +
                             "(ProjectSettings/OneSignalSettings.json disableLocation must stay true)");
        }

        static string FileNameOf(string path) => path.Replace('\\', '/').Split('/').Last();
    }
#endif
}
