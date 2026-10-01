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

namespace MotionRunner.EditorTools
{
    /// The last word on the exported Xcode project before it leaves Windows: fixes the few Info.plist
    /// and asset facts App Store Connect checks on upload, and fails the build when a fact the store
    /// listing promises (docs/store-kit/APP_STORE_LISTING.md §7) is not in the export. Everything it
    /// checks would otherwise surface only after a Mac archive + upload cycle, the slowest loop there is.
    ///
    ///   * Info.plist ITSAppUsesNonExemptEncryption = false: the game's only encryption is HTTPS
    ///     through the OS (Supabase, RevenueCat, OneSignal, Layers), which is exempt, so each upload
    ///     skips the export-compliance question;
    ///   * NSCameraUsageDescription equals BuildScript.IosCameraUsage word for word;
    ///   * CFBundleURLTypes carries the `veyro` scheme (challenge links, T-024);
    ///   * automatic signing with the owner's team on the app AND OneSignal's notification service
    ///     extension (OneSignal sets the team on its target but not the signing style);
    ///   * the App Store (ios-marketing) icon has no alpha channel (ITMS-90717), rewritten as opaque
    ///     RGB if Unity ever writes it as RGBA.
    ///
    /// Order: after every SDK post-processor - EDM4U's Podfile (40) and pod step (50), OneSignal's
    /// NSE/entitlements (45), Layers (100) and RevenueCat's `[PostProcessBuild(999)]` - so it sees
    /// the project they leave. The gap above it is deliberate: IosPrivacyPostProcess (Phase 2b)
    /// runs after this one, at CallbackOrder + 100.
    ///
    /// Compiled only with iOS as the active target (UnityEditor.iOS.Xcode lives in the iOS support
    /// module); StoreBuildGuard refuses an iOS build from an editor compiled for anything else, so
    /// this can never be silently absent from an export.
    public sealed class IosBuildPostProcess : IPostprocessBuildWithReport
    {
        public const int CallbackOrder = 1000;
        public int callbackOrder => CallbackOrder;

        /// OneSignal's BuildPostProcessor.ServiceExtensionTargetName (private there).
        public const string OneSignalExtensionTarget = "OneSignalNotificationServiceExtension";

        const string Tag = "[iOS export]";

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS) return;

            string root = report.summary.outputPath;
            PatchInfoPlist(root);
            EnsureAutomaticSigning(root);
            EnsureOpaqueAppStoreIcon(root);
        }

        static void PatchInfoPlist(string root)
        {
            string path = Path.Combine(root, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(path);
            var dict = plist.root;

            dict.SetBoolean("ITSAppUsesNonExemptEncryption", false);

            string camera = (dict["NSCameraUsageDescription"] as PlistElementString)?.value;
            if (camera != BuildScript.IosCameraUsage)
                throw new BuildFailedException(
                    $"{Tag} Info.plist NSCameraUsageDescription is '{camera ?? "(missing)"}', expected the " +
                    $"exact App Store listing text '{BuildScript.IosCameraUsage}'. Build with BuildScript.BuildIOS.");

            if (!HasUrlScheme(dict, BuildScript.IosUrlScheme))
                throw new BuildFailedException(
                    $"{Tag} Info.plist CFBundleURLTypes has no '{BuildScript.IosUrlScheme}' scheme: challenge " +
                    "links would not open the game. Build with BuildScript.BuildIOS.");

            plist.WriteToFile(path);
            Debug.Log($"{Tag} Info.plist: ITSAppUsesNonExemptEncryption=false; NSCameraUsageDescription and " +
                      $"the '{BuildScript.IosUrlScheme}' URL scheme verified.");
        }

        static bool HasUrlScheme(PlistElementDict dict, string scheme) =>
            dict["CFBundleURLTypes"] is PlistElementArray types &&
            types.values.OfType<PlistElementDict>().Any(type =>
                type["CFBundleURLSchemes"] is PlistElementArray schemes &&
                schemes.values.OfType<PlistElementString>().Any(s => s.value == scheme));

        /// Unity writes the team and CODE_SIGN_STYLE=Automatic on Unity-iPhone from PlayerSettings;
        /// OneSignal's extension target gets the team from PlayerSettings too but no signing style.
        /// Setting both explicitly means the Mac only has to pick the account, never a toggle, and
        /// Xcode registers the extension's App ID and the App Group itself.
        static void EnsureAutomaticSigning(string root)
        {
            string path = PBXProject.GetPBXProjectPath(root);
            var project = new PBXProject();
            project.ReadFromFile(path);

            var targets = new List<(string name, string guid)> { ("Unity-iPhone", project.GetUnityMainTargetGuid()) };
            string extension = project.TargetGuidByName(OneSignalExtensionTarget);
            if (!string.IsNullOrEmpty(extension)) targets.Add((OneSignalExtensionTarget, extension));
            else Debug.LogWarning($"{Tag} No {OneSignalExtensionTarget} target in the export (OneSignal iOS package removed?).");

            var changes = new List<string>();
            foreach (var (name, guid) in targets)
            {
                if (project.GetBuildPropertyForAnyConfig(guid, "DEVELOPMENT_TEAM") != BuildScript.IosTeamId)
                {
                    project.SetBuildProperty(guid, "DEVELOPMENT_TEAM", BuildScript.IosTeamId);
                    changes.Add(name + " DEVELOPMENT_TEAM");
                }
                if (project.GetBuildPropertyForAnyConfig(guid, "CODE_SIGN_STYLE") != "Automatic")
                {
                    project.SetBuildProperty(guid, "CODE_SIGN_STYLE", "Automatic");
                    changes.Add(name + " CODE_SIGN_STYLE");
                }
            }

            if (changes.Count > 0) project.WriteToFile(path);
            Debug.Log($"{Tag} Automatic signing, team {BuildScript.IosTeamId}, on " +
                      string.Join(" + ", targets.Select(t => t.name)) +
                      (changes.Count > 0 ? $" (set: {string.Join(", ", changes)})." : " (already set)."));
        }

        static void EnsureOpaqueAppStoreIcon(string root)
        {
            string set = Path.Combine(root, "Unity-iPhone", "Images.xcassets", "AppIcon.appiconset");
            string contentsPath = Path.Combine(set, "Contents.json");
            if (!File.Exists(contentsPath))
                throw new BuildFailedException($"{Tag} {contentsPath} is missing: the export has no app icon set.");

            string file = MarketingIconFile(File.ReadAllText(contentsPath));
            if (file == null)
                throw new BuildFailedException(
                    $"{Tag} No App Store (ios-marketing) icon in {contentsPath}: Xcode refuses to archive " +
                    "without one. Run ProjectSetup.EnsureAppIcon with iOS as the active target.");

            string pngPath = Path.Combine(set, file);
            byte[] png = File.ReadAllBytes(pngPath);
            var before = PngInfo.Read(png);
            if (before == null)
                throw new BuildFailedException($"{Tag} App Store icon {pngPath} is not a PNG.");
            if (!before.HasAlpha)
            {
                Debug.Log($"{Tag} App Store icon {file}: {before}, no alpha channel.");
                return;
            }

            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!source.LoadImage(png, false))
                throw new BuildFailedException($"{Tag} Could not decode the App Store icon {pngPath}.");
            Color32[] pixels = source.GetPixels32();
            int translucent = pixels.Count(p => p.a < 255);
            var opaque = new Texture2D(source.width, source.height, TextureFormat.RGB24, false, false);
            opaque.SetPixels32(pixels);
            opaque.Apply(false, false);
            byte[] rgb = opaque.EncodeToPNG();
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(opaque);

            var after = PngInfo.Read(rgb);
            if (after == null || after.HasAlpha)
                throw new BuildFailedException($"{Tag} Rewriting the App Store icon {file} as RGB still left an alpha channel.");
            File.WriteAllBytes(pngPath, rgb);
            Debug.LogWarning($"{Tag} App Store icon {file} was {before}; rewrote it as opaque {after} " +
                             $"({translucent} non-opaque pixels had their alpha dropped).");
        }

        /// The filename of the `"idiom" : "ios-marketing"` entry in an asset catalog's Contents.json.
        /// Each image entry is a flat JSON object, so matching brace-free blocks is enough.
        static string MarketingIconFile(string contentsJson)
        {
            foreach (Match entry in Regex.Matches(contentsJson, @"\{[^{}]*\}"))
            {
                if (!Regex.IsMatch(entry.Value, "\"idiom\"\\s*:\\s*\"ios-marketing\"")) continue;
                var name = Regex.Match(entry.Value, "\"filename\"\\s*:\\s*\"([^\"]+)\"");
                if (name.Success) return name.Groups[1].Value;
            }
            return null;
        }

        /// Just enough of the PNG format to answer "does this file carry alpha": the IHDR colour type
        /// (4 = grey+alpha, 6 = RGBA) or a tRNS chunk before the image data (palette/grey/RGB with a
        /// transparency key).
        sealed class PngInfo
        {
            public int Width, Height, BitDepth, ColourType;
            public bool HasTransparencyChunk;
            public bool HasAlpha => ColourType == 4 || ColourType == 6 || HasTransparencyChunk;

            static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

            public static PngInfo Read(byte[] png)
            {
                if (png.Length < 33 || !png.Take(8).SequenceEqual(Signature)) return null;
                if (Encoding.ASCII.GetString(png, 12, 4) != "IHDR") return null;
                var info = new PngInfo
                {
                    Width = BigEndian(png, 16),
                    Height = BigEndian(png, 20),
                    BitDepth = png[24],
                    ColourType = png[25]
                };
                for (int offset = 8; offset + 8 <= png.Length;)
                {
                    int length = BigEndian(png, offset);
                    string type = Encoding.ASCII.GetString(png, offset + 4, 4);
                    if (type == "tRNS") info.HasTransparencyChunk = true;
                    if (type == "IDAT" || type == "IEND" || length < 0 || length > png.Length - offset - 12) break;
                    offset += 12 + length;
                }
                return info;
            }

            static int BigEndian(byte[] b, int i) => (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3];

            public override string ToString()
            {
                string colour = ColourType switch
                {
                    0 => "greyscale",
                    2 => "RGB",
                    3 => "indexed",
                    4 => "greyscale+alpha",
                    6 => "RGBA",
                    _ => "colour type " + ColourType
                };
                return $"{Width}x{Height} {colour} {BitDepth}-bit" + (HasTransparencyChunk ? " with tRNS" : "");
            }
        }
    }
}
#endif
