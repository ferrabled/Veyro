#!/usr/bin/env python3
"""Inspect a Veyro Run iOS Xcode export before it goes to the Mac (and again on the Mac).

Usage:
    python docs/tools/inspect_ios_export.py <export-dir> [--build-number N]
                                            [--clang <path>] [--nm <path>]
                                            [--expect-collected N]

<export-dir> is what BuildScript.BuildIOS writes (builds/ios/VeyroRun-xcode), before
`pod install`. Python 3 standard library only. Prints a PASS/FAIL table; exits 1 on any
FAIL, 0 otherwise. SKIP rows (a tool flag not given) never fail the run.

  --build-number N      the CFBundleVersion this export must carry; defaults to the env
                        VEYRO_IOS_BUILD_NUMBER, else only "a positive integer" is checked
  --clang <path>        syntax-check both privacy stubs (freestanding C, arm64-apple-ios15.0).
                        Windows: the Android NDK's clang.exe. Mac: "$(xcrun -f clang)"
  --nm <path>           `nm -u` over every .a in the export and UnityRuntime.framework, looking
                        for tracking / location / pedometer symbols. Windows: the NDK's
                        llvm-nm.exe. Mac: "$(xcrun -f nm)"
  --expect-collected N  NSPrivacyCollectedDataTypes rows expected in the manifest (default 7;
                        a kill-switch export has fewer: VEYRO_IOS_NO_LAYERS=1 -> 5, both off -> 4)

What it asserts is listed in docs/IOS_BUILD_RUNBOOK.md ("What the inspection checks"). The
facts come from docs/store-kit/APP_STORE_LISTING.md (§3 App Privacy, §7 Info.plist), the
OneSignal / Layers / privacy post-processes (IosBuildPostProcess, IosPrivacyPostProcess) and
IOS_HANDOFF decisions 1 and 5.
"""
import argparse
import json
import os
import plistlib
import re
import struct
import subprocess
import sys

CAMERA_TEXT = ("Camera mode uses the front camera to follow your movements. "
               "Images are processed on your iPhone and never leave it.")
VERSION = "1.0.0"
TEAM = "69J7W5URX7"
MIN_OS = "15.0"
APP_GROUP = "group.com.ferrabled.veyro.run.onesignal"
NSE = "OneSignalNotificationServiceExtension"
TARGETS = ("Unity-iPhone", "UnityFramework", NSE)
ONESIGNAL_POD = "5.7.0"
PURCHASES_HYBRID_COMMON = "18.31.0"

FORBIDDEN_PLIST_KEYS = ("NSUserTrackingUsageDescription", "NSMotionUsageDescription",
                        "SKAdNetworkItems", "NSAdvertisingAttributionReportEndpoint")
FORBIDDEN_PLIST_PREFIX = "NSLocation"
STUB_FOLDER = "Libraries/VeyroPrivacyStubs"
STUBS = ("VeyroLayersAttStub.m", "VeyroStepCounterStub.m")
STRIPPED_SOURCES = ("LayersATTBridge.m", "iOSStepCounter.mm")
WEAK_FRAMEWORKS = ("AdServices.framework", "StoreKit.framework")
FORBIDDEN_FRAMEWORK_TOKEN = re.compile(r"\b(AppTrackingTransparency|AdSupport|CoreLocation)\b")
FORBIDDEN_NATIVE_API = re.compile(
    r"\b(ATTrackingManager|ASIdentifierManager|CLLocationManager|CMPedometer|CMMotionActivityManager)\b")
NATIVE_SOURCE_EXTENSIONS = (".m", ".mm", ".c", ".cpp", ".h", ".swift")
FORBIDDEN_SYMBOL = re.compile(r"ATTracking|ASIdentifier|CLLocation|CMPedometer")

MANIFEST = "UnityFramework/PrivacyInfo.xcprivacy"
UNITY_RUNTIME_MANIFEST = "Frameworks/UnityRuntime.framework/PrivacyInfo.xcprivacy"
EXPECTED_REASONS = {
    "NSPrivacyAccessedAPICategoryFileTimestamp": {"0A2A.1", "C617.1"},
    "NSPrivacyAccessedAPICategoryUserDefaults": {"CA92.1"},
    "NSPrivacyAccessedAPICategorySystemBootTime": {"35F9.1"},
    "NSPrivacyAccessedAPICategoryDiskSpace": {"E174.1"},
}


class Report:
    def __init__(self):
        self.rows = []

    def check(self, area, name, ok, detail=""):
        self.rows.append(("PASS" if ok else "FAIL", area, name, detail))
        return ok

    def skip(self, area, name, detail):
        self.rows.append(("SKIP", area, name, detail))

    def fail(self, area, name, detail):
        return self.check(area, name, False, detail)

    @property
    def failed(self):
        return sum(1 for r in self.rows if r[0] == "FAIL")

    def print(self):
        wa = max(len(r[1]) for r in self.rows)
        wn = max(len(r[2]) for r in self.rows)
        print(f"{'RESULT':6}  {'AREA':{wa}}  {'CHECK':{wn}}  DETAIL")
        print(f"{'-' * 6}  {'-' * wa}  {'-' * wn}  {'-' * 6}")
        for status, area, name, detail in self.rows:
            print(f"{status:6}  {area:{wa}}  {name:{wn}}  {detail}")
        passed = sum(1 for r in self.rows if r[0] == "PASS")
        skipped = sum(1 for r in self.rows if r[0] == "SKIP")
        print(f"\n{passed} passed, {self.failed} failed, {skipped} skipped")


# ---------------------------------------------------------------- pbxproj (OpenStep plist)

class PbxParser:
    """Just enough of the old-style ASCII plist grammar Xcode writes project.pbxproj in."""
    UNQUOTED = re.compile(r"[A-Za-z0-9_$/:.\-+]+")
    ESCAPES = {"n": "\n", "t": "\t", "r": "\r", '"': '"', "\\": "\\", "'": "'"}

    def __init__(self, text):
        self.t, self.i, self.n = text, 0, len(text)

    def skip(self):
        t = self.t
        while self.i < self.n:
            c = t[self.i]
            if c in " \t\r\n":
                self.i += 1
            elif t.startswith("/*", self.i):
                end = t.find("*/", self.i + 2)
                self.i = self.n if end < 0 else end + 2
            elif t.startswith("//", self.i):
                end = t.find("\n", self.i)
                self.i = self.n if end < 0 else end + 1
            else:
                return

    def expect(self, ch):
        self.skip()
        if self.t[self.i] != ch:
            raise ValueError(f"pbxproj: expected '{ch}' at offset {self.i}, got '{self.t[self.i:self.i + 20]}'")
        self.i += 1

    def value(self):
        self.skip()
        c = self.t[self.i]
        if c == "{":
            self.i += 1
            d = {}
            while True:
                self.skip()
                if self.t[self.i] == "}":
                    self.i += 1
                    return d
                key = self.value()
                self.expect("=")
                d[key] = self.value()
                self.expect(";")
        if c == "(":
            self.i += 1
            a = []
            while True:
                self.skip()
                if self.t[self.i] == ")":
                    self.i += 1
                    return a
                a.append(self.value())
                self.skip()
                if self.t[self.i] == ",":
                    self.i += 1
        if c == '"':
            self.i += 1
            buf = []
            while self.t[self.i] != '"':
                if self.t[self.i] == "\\":
                    self.i += 1
                    buf.append(self.ESCAPES.get(self.t[self.i], "\\" + self.t[self.i]))
                else:
                    buf.append(self.t[self.i])
                self.i += 1
            self.i += 1
            return "".join(buf)
        m = self.UNQUOTED.match(self.t, self.i)
        if not m:
            raise ValueError(f"pbxproj: unexpected '{self.t[self.i:self.i + 20]}' at offset {self.i}")
        self.i = m.end()
        return m.group(0)


class Project:
    def __init__(self, text):
        self.text = text
        root = PbxParser(text).value()
        self.objects = root["objects"]
        self.project = self.objects[root["rootObject"]]
        self.targets = {self.objects[g].get("name"): self.objects[g] for g in self.project.get("targets", [])}

    def configs(self, owner):
        """name -> buildSettings of an object with a buildConfigurationList."""
        lst = self.objects[owner["buildConfigurationList"]]
        return {self.objects[g]["name"]: self.objects[g].get("buildSettings", {})
                for g in lst.get("buildConfigurations", [])}

    def effective(self, target_name):
        """name -> settings, target overriding project-level, per configuration."""
        project = self.configs(self.project)
        result = {}
        for name, settings in self.configs(self.targets[target_name]).items():
            merged = dict(project.get(name, {}))
            merged.update(settings)
            result[name] = merged
        return result

    def phase(self, target_name, isa):
        for g in self.targets[target_name].get("buildPhases", []):
            if self.objects[g].get("isa") == isa:
                return self.objects[g]
        return None

    def phase_files(self, target_name, isa):
        """[(file name, build-file settings)] of one build phase."""
        phase = self.phase(target_name, isa)
        out = []
        for g in (phase or {}).get("files", []):
            build_file = self.objects[g]
            ref = self.objects.get(build_file.get("fileRef"), {})
            name = ref.get("name") or os.path.basename(ref.get("path", ""))
            out.append((name, ref.get("path", ""), build_file.get("settings", {})))
        return out


def tokens(value):
    if isinstance(value, list):
        return [str(v) for v in value]
    return str(value or "").split()


# ---------------------------------------------------------------- helpers

def load_plist(path):
    with open(path, "rb") as f:
        return plistlib.load(f)


def png_info(path):
    with open(path, "rb") as f:
        b = f.read()
    if b[:8] != b"\x89PNG\r\n\x1a\n" or b[12:16] != b"IHDR":
        return None
    width, height = struct.unpack(">II", b[16:24])
    depth, colour = b[24], b[25]
    trns, off = False, 8
    while off + 8 <= len(b):
        length = struct.unpack(">I", b[off:off + 4])[0]
        kind = b[off + 4:off + 8]
        if kind == b"tRNS":
            trns = True
        if kind == b"IEND":
            break
        off += 12 + length
    return width, height, depth, colour, trns


def podfile_targets(text):
    """target name -> block text (Podfile `target 'X' do ... end`, not nested here)."""
    blocks = {}
    for m in re.finditer(r"^target\s+'([^']+)'\s+do\s*$(.*?)^end\s*$", text, re.M | re.S):
        blocks[m.group(1)] = m.group(2)
    return blocks


def has_pod(block, name, version):
    return re.search(r"^\s*pod\s+'" + re.escape(name) + r"'\s*,\s*'" + re.escape(version) + r"'\s*$",
                     block, re.M) is not None


def run(cmd):
    try:
        p = subprocess.run(cmd, capture_output=True, text=True, errors="replace")
        return p.returncode, p.stdout, p.stderr
    except OSError as e:
        return -1, "", str(e)


# ---------------------------------------------------------------- checks

def check_plists(r, root, build_number):
    area = "Info.plist"
    path = os.path.join(root, "Info.plist")
    if not os.path.isfile(path):
        r.fail(area, "app Info.plist present", path)
        return
    info = load_plist(path)
    r.check(area, "NSCameraUsageDescription exact", info.get("NSCameraUsageDescription") == CAMERA_TEXT,
            repr(info.get("NSCameraUsageDescription")))
    r.check(area, "ITSAppUsesNonExemptEncryption false",
            info.get("ITSAppUsesNonExemptEncryption") is False, repr(info.get("ITSAppUsesNonExemptEncryption")))
    schemes = [s for t in info.get("CFBundleURLTypes", []) for s in t.get("CFBundleURLSchemes", [])]
    r.check(area, "CFBundleURLTypes has veyro", "veyro" in schemes, repr(schemes))
    modes = info.get("UIBackgroundModes", [])
    r.check(area, "UIBackgroundModes remote-notification, no location",
            "remote-notification" in modes and "location" not in modes, repr(modes))
    short = info.get("CFBundleShortVersionString")
    r.check(area, "CFBundleShortVersionString " + VERSION, short == VERSION, repr(short))
    version = info.get("CFBundleVersion")
    if build_number is not None:
        r.check(area, f"CFBundleVersion == {build_number}", version == str(build_number), repr(version))
    else:
        r.check(area, "CFBundleVersion positive integer",
                bool(re.fullmatch(r"[1-9][0-9]*", str(version or ""))), repr(version) + " (no --build-number given)")

    for rel in ("Info.plist", "UnityFramework/Info.plist", NSE + "/Info.plist"):
        p = os.path.join(root, rel)
        if not os.path.isfile(p):
            r.fail(area, f"{rel} present", "missing")
            continue
        keys = load_plist(p).keys()
        bad = sorted(k for k in keys if k in FORBIDDEN_PLIST_KEYS or k.startswith(FORBIDDEN_PLIST_PREFIX))
        r.check(area, f"{rel}: no tracking/motion/SKAN/location key", not bad, ", ".join(bad) or "none")

    nse_path = os.path.join(root, NSE, "Info.plist")
    if os.path.isfile(nse_path):
        nse = load_plist(nse_path)
        same = nse.get("CFBundleShortVersionString") == short and nse.get("CFBundleVersion") == version
        r.check(area, "NSE version == app version (ITMS-90473)", same,
                f"NSE {nse.get('CFBundleShortVersionString')!r} ({nse.get('CFBundleVersion')!r}), "
                f"app {short!r} ({version!r})")


def check_pbxproj(r, root):
    area = "pbxproj"
    path = os.path.join(root, "Unity-iPhone.xcodeproj", "project.pbxproj")
    if not os.path.isfile(path):
        r.fail(area, "project.pbxproj present", path)
        return
    with open(path, encoding="utf-8") as f:
        text = f.read()
    try:
        proj = Project(text)
    except (ValueError, KeyError, IndexError) as e:
        r.fail(area, "project.pbxproj parses", str(e))
        return

    missing = [t for t in TARGETS if t not in proj.targets]
    r.check(area, "targets " + ", ".join(TARGETS), not missing,
            "all present (" + ", ".join(sorted(proj.targets)) + ")" if not missing else "missing " + ", ".join(missing))
    if missing:
        return

    for src in STRIPPED_SOURCES:
        r.check(area, f"no {src}", src not in text, "absent" if src not in text else "still referenced")

    sources = [f[0] for f in proj.phase_files("UnityFramework", "PBXSourcesBuildPhase")]
    for stub in STUBS:
        r.check(area, f"{stub} in UnityFramework Sources", stub in sources,
                "compiled" if stub in sources else "not in the Sources phase")

    linked = {f[0]: f[2] for f in proj.phase_files("UnityFramework", "PBXFrameworksBuildPhase")}
    for fw in WEAK_FRAMEWORKS:
        attrs = tokens(linked.get(fw, {}).get("ATTRIBUTES")) if fw in linked else None
        r.check(area, f"{fw} weak on UnityFramework", attrs is not None and "Weak" in attrs,
                "not linked" if attrs is None else f"ATTRIBUTES = ({', '.join(attrs)})")

    hits = sorted(set(m.group(0) for m in FORBIDDEN_FRAMEWORK_TOKEN.finditer(text)))
    r.check(area, "no AppTrackingTransparency/AdSupport/CoreLocation", not hits, ", ".join(hits) or "none")

    counts = {}
    for t in proj.targets:
        counts[t] = sum(1 for f in proj.phase_files(t, "PBXResourcesBuildPhase") if f[0] == "PrivacyInfo.xcprivacy")
    uf = [f[1] for f in proj.phase_files("UnityFramework", "PBXResourcesBuildPhase") if f[0] == "PrivacyInfo.xcprivacy"]
    r.check(area, "UnityFramework bundles exactly 1 PrivacyInfo", counts["UnityFramework"] == 1,
            f"{counts['UnityFramework']} ({', '.join(uf)})")
    dupes = {t: c for t, c in counts.items() if c > 1}
    r.check(area, "no target bundles 2+ PrivacyInfo", not dupes,
            ", ".join(f"{t}={c}" for t, c in sorted(counts.items())))

    def every_config(target, key, predicate, describe):
        cfg = proj.effective(target)
        values = {name: s.get(key) for name, s in cfg.items()}
        ok = bool(values) and all(predicate(v) for v in values.values())
        r.check(area, f"{target}: {describe}", ok, ", ".join(f"{n}={values[n]!r}" for n in sorted(values)))

    every_config("UnityFramework", "GCC_PREPROCESSOR_DEFINITIONS",
                 lambda v: "ONESIGNAL_DISABLE_LOCATION=1" in tokens(v), "ONESIGNAL_DISABLE_LOCATION=1")
    for t in ("Unity-iPhone", NSE):
        every_config(t, "DEVELOPMENT_TEAM", lambda v: v == TEAM, "DEVELOPMENT_TEAM " + TEAM)
        every_config(t, "CODE_SIGN_STYLE", lambda v: v == "Automatic", "CODE_SIGN_STYLE Automatic")
    for t in TARGETS:
        every_config(t, "IPHONEOS_DEPLOYMENT_TARGET", lambda v: v == MIN_OS, "IPHONEOS_DEPLOYMENT_TARGET " + MIN_OS)
    other = sorted({s["IPHONEOS_DEPLOYMENT_TARGET"] for o in proj.objects.values()
                    if o.get("isa") == "XCBuildConfiguration"
                    for s in [o.get("buildSettings", {})] if "IPHONEOS_DEPLOYMENT_TARGET" in s})
    r.check(area, "every IPHONEOS_DEPLOYMENT_TARGET is " + MIN_OS, other == [MIN_OS], ", ".join(other))
    every_config("Unity-iPhone", "TARGETED_DEVICE_FAMILY", lambda v: str(v) == "1", "TARGETED_DEVICE_FAMILY 1 (iPhone)")


def check_entitlements(r, root):
    area = "entitlements"
    app = os.path.join(root, "Unity-iPhone", "Unity-iPhone.entitlements")
    nse = os.path.join(root, NSE, NSE + ".entitlements")
    if not os.path.isfile(app):
        r.fail(area, "Unity-iPhone.entitlements present", "missing")
    else:
        e = load_plist(app)
        r.check(area, "app aps-environment", "aps-environment" in e, repr(e.get("aps-environment")))
        groups = e.get("com.apple.security.application-groups", [])
        r.check(area, "app group " + APP_GROUP, APP_GROUP in groups, repr(groups))
    if not os.path.isfile(nse):
        r.fail(area, NSE + ".entitlements present", "missing")
    else:
        groups = load_plist(nse).get("com.apple.security.application-groups", [])
        r.check(area, "NSE group " + APP_GROUP, APP_GROUP in groups, repr(groups))


def check_podfile(r, root):
    area = "Podfile"
    path = os.path.join(root, "Podfile")
    if not os.path.isfile(path):
        r.fail(area, "Podfile present", "missing (EDM4U off?)")
        return
    with open(path, encoding="utf-8") as f:
        text = f.read()
    r.check(area, f"platform :ios, '{MIN_OS}'", re.search(r"^platform :ios, '" + re.escape(MIN_OS) + r"'\s*$", text, re.M)
            is not None, "")
    blocks = podfile_targets(text)
    for t in ("UnityFramework", "Unity-iPhone"):
        block = blocks.get(t, "")
        ok = all(has_pod(block, "OneSignalXCFramework/" + s, ONESIGNAL_POD) for s in ("OneSignal", "OneSignalInAppMessages"))
        r.check(area, f"{t}: OneSignal + InAppMessages {ONESIGNAL_POD}", ok,
                "target block found" if t in blocks else "no target block")
    r.check(area, f"{NSE}: OneSignalExtension {ONESIGNAL_POD}",
            has_pod(blocks.get(NSE, ""), "OneSignalXCFramework/OneSignalExtension", ONESIGNAL_POD), "")
    r.check(area, "no OneSignalLocation", "OneSignalLocation" not in text, "")
    bare = re.search(r"^\s*pod\s+'OneSignalXCFramework'", text, re.M)
    r.check(area, "no bare pod 'OneSignalXCFramework'", bare is None, "subspecs only" if bare is None else bare.group(0).strip())
    r.check(area, f"PurchasesHybridCommon {PURCHASES_HYBRID_COMMON}",
            has_pod(blocks.get("UnityFramework", ""), "PurchasesHybridCommon", PURCHASES_HYBRID_COMMON), "on UnityFramework")
    r.check(area, "use_frameworks! static", re.search(r"^use_frameworks!\s*:linkage\s*=>\s*:static\s*$", text, re.M)
            is not None, "")


def check_disk(r, root):
    area = "disk"
    for stub in STUBS:
        p = os.path.join(root, STUB_FOLDER, stub)
        r.check(area, f"{STUB_FOLDER}/{stub}", os.path.isfile(p) and os.path.getsize(p) > 0,
                f"{os.path.getsize(p)} bytes" if os.path.isfile(p) else "missing")

    found, api_hits = [], []
    stub_dir = os.path.normcase(os.path.abspath(os.path.join(root, STUB_FOLDER)))
    for dirpath, _, files in os.walk(root):
        for name in files:
            full = os.path.join(dirpath, name)
            rel = os.path.relpath(full, root).replace("\\", "/")
            if name in STRIPPED_SOURCES:
                found.append(rel)
            if not rel.startswith("Libraries/") or os.path.normcase(os.path.abspath(full)).startswith(stub_dir):
                continue
            if os.path.splitext(name)[1].lower() not in NATIVE_SOURCE_EXTENSIONS:
                continue
            with open(full, encoding="utf-8", errors="replace") as f:
                m = FORBIDDEN_NATIVE_API.search(f.read())
            if m:
                api_hits.append(f"{rel}: {m.group(0)}")
    r.check(area, "no LayersATTBridge.m / iOSStepCounter.mm on disk", not found, ", ".join(found) or "none")
    r.check(area, "no purpose-string API in Libraries/ sources", not api_hits, "; ".join(api_hits[:5]) or "none")

    pre = os.path.join(root, "Classes", "Preprocessor.h")
    if not os.path.isfile(pre):
        r.fail(area, "Classes/Preprocessor.h present", "missing")
    else:
        with open(pre, encoding="utf-8", errors="replace") as f:
            text = f.read()
        for flag in ("UNITY_USES_IAD", "UNITY_USES_LOCATION"):
            m = re.search(r"^#define\s+" + flag + r"\s+(\d+)", text, re.M)
            r.check(area, f"Preprocessor.h {flag} 0", m is not None and m.group(1) == "0",
                    m.group(0) if m else "undefined")

    burst = os.path.join(root, "Libraries", "lib_burst_generated.a")
    if os.path.isfile(burst):
        with open(burst, "rb") as f:
            magic = f.read(8)
        size = os.path.getsize(burst)
        is_ar = magic == b"!<arch>\n"
        r.check(area, "Libraries/lib_burst_generated.a", is_ar and size >= 100_000,
                f"{size:,} bytes, {'ar archive' if is_ar else 'not an ar archive'}")
    else:
        r.fail(area, "Libraries/lib_burst_generated.a", "missing: Burst did not AOT-compile, the camera gate would fail")

    iconset = os.path.join(root, "Unity-iPhone", "Images.xcassets", "AppIcon.appiconset")
    try:
        with open(os.path.join(iconset, "Contents.json"), encoding="utf-8") as f:
            images = json.load(f).get("images", [])
        marketing = next((i.get("filename") for i in images if i.get("idiom") == "ios-marketing" and i.get("filename")), None)
    except (OSError, ValueError):
        marketing = None
    if not marketing:
        r.fail(area, "App Store icon (ios-marketing)", "no ios-marketing entry in AppIcon.appiconset/Contents.json")
    else:
        info = png_info(os.path.join(iconset, marketing))
        if info is None:
            r.fail(area, "App Store icon RGB, no alpha", f"{marketing} is not a PNG")
        else:
            w, h, depth, colour, trns = info
            r.check(area, "App Store icon RGB, no alpha", w == h == 1024 and colour == 2 and not trns,
                    f"{marketing}: {w}x{h}, colour type {colour}, {depth}-bit, tRNS={'yes' if trns else 'no'}")


def check_manifest(r, root, expect_collected):
    area = "privacy manifest"
    path = os.path.join(root, MANIFEST)
    if not os.path.isfile(path):
        r.fail(area, MANIFEST, "missing")
    else:
        m = load_plist(path)
        r.check(area, "NSPrivacyTracking false", m.get("NSPrivacyTracking") is False, repr(m.get("NSPrivacyTracking")))
        domains = m.get("NSPrivacyTrackingDomains")
        r.check(area, "NSPrivacyTrackingDomains empty", domains == [], repr(domains))
        api = m.get("NSPrivacyAccessedAPITypes", [])
        got = {}
        for entry in api:
            got.setdefault(entry.get("NSPrivacyAccessedAPIType"), []).extend(entry.get("NSPrivacyAccessedAPITypeReasons", []))
        short = lambda c: str(c).replace("NSPrivacyAccessedAPICategory", "")
        r.check(area, "exactly 4 API categories, no duplicate", len(api) == 4 and len(got) == 4,
                f"{len(api)} entries: " + ", ".join(short(c) for c in got))
        for cat, reasons in EXPECTED_REASONS.items():
            have = got.get(cat, [])
            r.check(area, f"{short(cat)} [{', '.join(sorted(reasons))}]",
                    set(have) == reasons and len(have) == len(reasons), repr(sorted(have)))
        rows = m.get("NSPrivacyCollectedDataTypes", [])
        linked_ok = all(e.get("NSPrivacyCollectedDataTypeLinked") is True and
                        e.get("NSPrivacyCollectedDataTypeTracking") is False for e in rows)
        names = [str(e.get("NSPrivacyCollectedDataType", "")).replace("NSPrivacyCollectedDataType", "") for e in rows]
        r.check(area, f"{expect_collected} collected types, linked, not tracking",
                len(rows) == expect_collected and linked_ok, f"{len(rows)}: " + ", ".join(names))
    r.check(area, UNITY_RUNTIME_MANIFEST, os.path.isfile(os.path.join(root, UNITY_RUNTIME_MANIFEST)),
            "present" if os.path.isfile(os.path.join(root, UNITY_RUNTIME_MANIFEST)) else "missing")


def check_tools(r, root, clang, nm):
    area = "tools"
    if not clang:
        r.skip(area, "stub syntax check (clang)", "pass --clang <path>")
    else:
        for stub in STUBS:
            p = os.path.join(root, STUB_FOLDER, stub)
            code, out, err = run([clang, "--target=arm64-apple-ios15.0", "-fsyntax-only", "-ffreestanding", "-x", "c", p])
            r.check(area, f"clang -fsyntax-only {stub}", code == 0,
                    "exit 0" if code == 0 else f"exit {code}: {(err or out).strip()[:200]}")
    if not nm:
        r.skip(area, "nm -u symbol scan", "pass --nm <path>")
        return
    binaries = []
    for dirpath, _, files in os.walk(root):
        binaries += [os.path.join(dirpath, f) for f in files if f.endswith(".a")]
    runtime = os.path.join(root, "Frameworks", "UnityRuntime.framework", "UnityRuntime")
    if os.path.isfile(runtime):
        binaries.append(runtime)
    else:
        r.fail(area, "UnityRuntime.framework/UnityRuntime present", "missing")
    hits, errors = [], []
    for b in sorted(binaries):
        rel = os.path.relpath(b, root).replace("\\", "/")
        code, out, err = run([nm, "-u", b])
        if code != 0 and not out:
            errors.append(f"{rel}: exit {code} {err.strip()[:120]}")
            continue
        for line in out.splitlines():
            if FORBIDDEN_SYMBOL.search(line):
                hits.append(f"{rel}: {line.strip()}")
    r.check(area, f"nm -u: no ATTracking/ASIdentifier/CLLocation/CMPedometer ({len(binaries)} binaries)",
            not hits and not errors,
            "; ".join((hits + errors)[:6]) or ", ".join(os.path.basename(b) for b in sorted(binaries)))


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("export_dir")
    ap.add_argument("--build-number", type=int, default=None)
    ap.add_argument("--clang")
    ap.add_argument("--nm")
    ap.add_argument("--expect-collected", type=int, default=7)
    args = ap.parse_args()

    root = os.path.abspath(args.export_dir)
    if not os.path.isdir(root):
        print(f"not a directory: {root}", file=sys.stderr)
        return 1
    build_number = args.build_number
    if build_number is None and re.fullmatch(r"[1-9][0-9]*", os.environ.get("VEYRO_IOS_BUILD_NUMBER", "")):
        build_number = int(os.environ["VEYRO_IOS_BUILD_NUMBER"])

    r = Report()
    print(f"Veyro Run iOS export inspection: {root}\n")
    for step in (lambda: check_plists(r, root, build_number), lambda: check_pbxproj(r, root),
                 lambda: check_entitlements(r, root), lambda: check_podfile(r, root), lambda: check_disk(r, root),
                 lambda: check_manifest(r, root, args.expect_collected),
                 lambda: check_tools(r, root, args.clang, args.nm)):
        try:
            step()
        except Exception as e:  # a crash in one area is a FAIL, never a silent pass
            r.fail("script", "inspection step crashed", f"{type(e).__name__}: {e}")
    r.print()
    return 1 if r.failed else 0


if __name__ == "__main__":
    sys.exit(main())
