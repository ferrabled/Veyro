using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// Pins the native stubs IosPrivacyPostProcess compiles into every iOS export (in place of
    /// Layers' LayersATTBridge.m and the Input System's iOSStepCounter.mm) to the C# side that
    /// P/Invokes them. The SDK files are parsed straight out of the package cache, so an SDK update
    /// that adds, renames or reshapes one of those externs fails here, on Windows, instead of as an
    /// undefined symbol in a Mac link or a silent ABI mismatch on a phone.
    ///
    /// The stubs live in Assembly-CSharp-Editor (Assets/Editor/IosPrivacyPostProcess.cs), which an
    /// asmdef cannot reference, so they are read by reflection - the same way ParkArtTests reaches
    /// editor-only types. IosNativeStubs compiles for every build target.
    public sealed class LayersAttStubTests
    {
        const string StubsType = "MotionRunner.EditorTools.IosNativeStubs";

        /// Apple API, framework and plist-key names a stub must not contain: the export inspection
        /// greps for them, and a stub mentioning one would make that scan meaningless.
        static readonly string[] ForbiddenInStub =
        {
            "ATTrackingManager", "ASIdentifierManager", "AppTrackingTransparency", "AdSupport",
            "NSUserTrackingUsageDescription", "CMPedometer", "CoreMotion", "NSMotionUsageDescription",
            "CLLocation", "#include", "#import", "@"
        };

        [Test]
        public void AttStubExportsExactlyTheExternsLayersDeclares() => AssertStubMatchesSdk("LayersAtt", 6);

        [Test]
        public void StepCounterStubExportsExactlyTheExternsTheInputSystemDeclares() =>
            AssertStubMatchesSdk("StepCounter", 5);

        [Test]
        public void AttStubNeverPromptsAndHandsOutNoIdentifier()
        {
            string source = SourceOf("LayersAtt");
            StringAssert.Contains("return 0;", Body(source, "layers_att_is_available"));
            StringAssert.Contains("return 0;", Body(source, "layers_att_get_status"));
            StringAssert.Contains("callback(0)", Body(source, "layers_att_request_tracking"),
                "request_tracking must answer 'not determined' through the callback, never show anything");
            StringAssert.Contains("return (const char *)0;", Body(source, "layers_att_get_idfa"));
            StringAssert.Contains("return (const char *)0;", Body(source, "layers_att_get_idfv"),
                "a null IDFV is what keeps Layers' InitIOSModules from sending one");
            StringAssert.Contains("free(ptr)", Body(source, "layers_att_free_string"));
            StringAssert.Contains("void free(void *ptr);", source,
                "free is declared by hand: the stub includes no header so it syntax-checks freestanding");
        }

        [Test]
        public void StepCounterStubReportsNoPedometer()
        {
            string source = SourceOf("StepCounter");
            StringAssert.Contains("return 0;", Body(source, "_iOSStepCounterIsAvailable"),
                "0 keeps iOSSupport.Initialize from adding a step-counter device at launch");
            StringAssert.Contains("return -1;", Body(source, "_iOSStepCounterEnable"));
            StringAssert.Contains("return 0;", Body(source, "_iOSStepCounterIsEnabled"));
        }

        [Test]
        public void StubsAreHeaderFreeAsciiAndLandOutsidePluginsFolder()
        {
            Type type = StubsTypeOrFail();
            string folder = (string)type.GetField("Folder", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            StringAssert.DoesNotContain("Plugins/iOS", folder,
                "a stub under Plugins/iOS would be compiled a second time by Unity itself");

            foreach (string name in new[] { "LayersAtt", "StepCounter" })
            {
                object stub = StubOrFail(name);
                string source = (string)Field(stub, "Source");
                Assert.IsTrue(source.All(c => c == '\n' || (c >= ' ' && c <= '~')), $"{name}: ASCII and LF only");
                foreach (string token in ForbiddenInStub)
                    StringAssert.DoesNotContain(token, source, $"{name} stub must not contain '{token}'");
                StringAssert.EndsWith(".m", (string)Field(stub, "FileName"),
                    "compiled as Objective-C, like the file it replaces, under UnityFramework's prefix header");
                Assert.AreNotEqual(Field(stub, "Replaces"), Field(stub, "FileName"));
            }
        }

        // ---- the comparison ----

        static void AssertStubMatchesSdk(string stubName, int expectedCount)
        {
            object stub = StubOrFail(stubName);
            string prefix = (string)Field(stub, "SymbolPrefix");
            string package = (string)Field(stub, "Package");
            string script = (string)Field(stub, "DeclaringScript");

            var declared = CSharpExterns(PackageFile(package, script), prefix);
            var exported = CFunctions((string)Field(stub, "Source"), prefix);

            Assert.AreEqual(expectedCount, declared.Count,
                $"{package}/{script} now declares {declared.Count} {prefix}* externs " +
                $"({string.Join(", ", declared.Keys)}): update the stub in IosPrivacyPostProcess.cs and this count.");
            CollectionAssert.AreEquivalent(declared.Keys, exported.Keys,
                $"stub exports [{string.Join(", ", exported.Keys)}] but the SDK P/Invokes " +
                $"[{string.Join(", ", declared.Keys)}]: a missing one is an undefined symbol at the Mac link.");
            foreach (var pair in declared)
                Assert.AreEqual(pair.Value, exported[pair.Key],
                    $"{pair.Key}: the C# side marshals {pair.Value}, the stub is {exported[pair.Key]}");
        }

        /// name -> ABI shape "ret(param,...)" of every `static extern` with the prefix.
        static Dictionary<string, string> CSharpExterns(string csharp, string prefix)
        {
            string code = Regex.Replace(csharp, @"//[^\n]*", "");
            var result = new Dictionary<string, string>();
            foreach (Match m in Regex.Matches(code,
                         @"static\s+extern\s+(?<ret>[\w\.]+)\s+(?<name>" + Regex.Escape(prefix) + @"\w*)\s*\((?<params>[^)]*)\)\s*;"))
            {
                var parameters = Params(m.Groups["params"].Value).Select(p => CsKind(DropName(p)));
                result[m.Groups["name"].Value] = CsKind(m.Groups["ret"].Value) + "(" + string.Join(",", parameters) + ")";
            }
            return result;
        }

        /// name -> ABI shape of every function DEFINITION (prototypes are skipped) with the prefix.
        static Dictionary<string, string> CFunctions(string c, string prefix)
        {
            string code = Regex.Replace(c, @"/\*.*?\*/", "", RegexOptions.Singleline);
            var result = new Dictionary<string, string>();
            foreach (Match m in Regex.Matches(code,
                         @"(?m)^(?<ret>[A-Za-z_][\w \t\*]*?)\s*\b(?<name>" + Regex.Escape(prefix) + @"\w*)\s*\((?<params>[^)]*)\)\s*\{"))
            {
                var parameters = Params(m.Groups["params"].Value).Where(p => p != "void").Select(p => CKind(DropName(p)));
                Assert.IsFalse(result.ContainsKey(m.Groups["name"].Value), $"{m.Groups["name"].Value} defined twice");
                result[m.Groups["name"].Value] = CKind(m.Groups["ret"].Value) + "(" + string.Join(",", parameters) + ")";
            }
            return result;
        }

        static IEnumerable<string> Params(string list) =>
            list.Split(',').Select(p => p.Trim()).Where(p => p.Length > 0);

        /// "int deviceId" -> "int", "void *callbacks" -> "void *", "ref Foo callbacks" -> "ref Foo".
        static string DropName(string parameter)
        {
            string p = Regex.Replace(parameter, @"\[[^\]]*\]", "").Trim();
            string type = Regex.Replace(p, @"\b[A-Za-z_]\w*$", "").Trim();
            return type.Length == 0 ? p : type;
        }

        /// What IL2CPP passes for a C# type: bool is a 4-byte BOOL by default, a delegate a function
        /// pointer, IntPtr / ref / string a pointer.
        static string CsKind(string type)
        {
            type = type.Trim();
            if (type.StartsWith("ref ") || type.StartsWith("out ") || type.StartsWith("in ")) return "ptr";
            switch (type)
            {
                case "void": return "void";
                case "int": case "uint": case "bool": case "Int32": case "System.Int32": return "int32";
                case "long": case "ulong": case "Int64": case "System.Int64": return "int64";
                case "IntPtr": case "System.IntPtr": case "string": return "ptr";
                default: return "fn";
            }
        }

        static string CKind(string type)
        {
            type = type.Trim();
            if (type.Contains("*")) return "ptr";
            type = Regex.Replace(type, @"\b(const|volatile|signed)\b", "").Trim();
            switch (type)
            {
                case "void": return "void";
                case "int": case "unsigned": case "unsigned int": case "int32_t": return "int32";
                case "long long": case "int64_t": return "int64";
                case "_Bool": case "bool": return "bool8"; // one byte: never what a C# bool marshals to
                default: return "fn"; // a typedef'd callback
            }
        }

        static string Body(string source, string function)
        {
            var m = Regex.Match(source, @"\b" + Regex.Escape(function) + @"\s*\([^)]*\)\s*\{(?<body>[^}]*)\}");
            Assert.IsTrue(m.Success, $"{function} has no definition in the stub");
            return m.Groups["body"].Value;
        }

        // ---- lookups ----

        static Type StubsTypeOrFail()
        {
            Type type = null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { type = assembly.GetType(StubsType, false); }
                catch (Exception) { type = null; }
                if (type != null) break;
            }
            Assert.IsNotNull(type, $"{StubsType} is not loaded: it lives in Assembly-CSharp-Editor " +
                                   "(game/Assets/Editor/IosPrivacyPostProcess.cs) and compiles for every target.");
            return type;
        }

        static object StubOrFail(string name)
        {
            FieldInfo field = StubsTypeOrFail().GetField(name, BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(field, $"{StubsType}.{name} is gone");
            return field.GetValue(null);
        }

        static string SourceOf(string name) => (string)Field(StubOrFail(name), "Source");

        static object Field(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(field, $"{target.GetType().Name}.{name} is gone");
            return field.GetValue(target);
        }

        /// The package's own copy wherever the Package Manager put it (git dependencies live under
        /// Library/PackageCache/<name>@<hash>), resolved three ways so it works in batchmode too.
        static string PackageFile(string package, string relative)
        {
            var candidates = new List<string>();
            var info = UnityEditor.PackageManager.PackageInfo.FindForPackageName(package);
            if (info != null && !string.IsNullOrEmpty(info.resolvedPath))
                candidates.Add(Path.Combine(info.resolvedPath, relative));
            candidates.Add(Path.GetFullPath(Path.Combine("Packages", package, relative)));
            string cache = Path.GetFullPath(Path.Combine("Library", "PackageCache"));
            if (Directory.Exists(cache))
                candidates.AddRange(Directory.GetDirectories(cache, package + "@*").Select(d => Path.Combine(d, relative)));

            string found = candidates.FirstOrDefault(File.Exists);
            Assert.IsNotNull(found, $"{package}/{relative} not found (tried {string.Join(", ", candidates)}): " +
                                    "the SDK moved its P/Invoke declarations - re-check the iOS stub before shipping.");
            return File.ReadAllText(found);
        }
    }
}
