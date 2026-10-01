# iOS build runbook: Windows export → cloud Mac → TestFlight

How a Veyro Run iOS build gets from this repo to an iPhone. There is no Mac and no iPhone here
(IOS_HANDOFF decision 1). Windows exports and inspects the Xcode project; a rented cloud Mac
(remote desktop, no USB) archives and uploads it; a borrowed iPhone installs it **only through
TestFlight**. There is one iOS artifact: the App Store export. There is no dev flavour and no
Simulator build, because Layers' `liblayers_core.a` is arm64-device-only.

Build 1 (28 Sep 2026) is the first export verified end to end on Windows: STATUS, Phase 3 entry.

| Sections | What | Where | Who |
|---|---|---|---|
| §1 | export, inspect, zip | Windows, scratch copy | agent or owner |
| §2–§8 | unpack, pods, sign, archive, scan, upload | cloud Mac | owner |
| §9–§10 | processing email, TestFlight "Friends" | App Store Connect | owner |
| §11–§13 | fix loop, kill switches, one-line device fixes | Windows | agent or owner |
| last section | device checklist | borrowed iPhone | owner |

---

## 1. Windows: export, inspect, zip

Batchmode runs only in a scratch copy, never in a `game/` the editor has open (CLAUDE.md gotchas
#4 and #11). The iOS scratch is `C:\scratch\veyro-ios`: sync it with `robocopy /E` (never `/MIR`),
then SHA-check and `diff -rq` it against the worktree, as in CLAUDE.md. `-buildTarget iOS` is
mandatory: without it the SDKs' Xcode post-processors and `RevenueCatKeys.AppStoreKey` don't
compile, and `BuildIOS` / `StoreBuildGuard` refuse to run.

```powershell
# N = one higher than the last build uploaded to App Store Connect (build 1 = 28 Sep 2026)
$env:VEYRO_IOS_BUILD_NUMBER = 'N'
& 'C:\Program Files\Unity\Hub\Editor\6000.5.9f1\Editor\Unity.exe' -batchmode -quit -buildTarget iOS `
  -projectPath C:\scratch\veyro-ios\game `
  -executeMethod MotionRunner.EditorTools.BuildScript.BuildIOS -logFile C:\scratch\veyro-ios\build-ios-bN.log
Select-String C:\scratch\veyro-ios\build-ios-bN.log -Pattern '\[iOS privacy\]|\[iOS export\]|Build OK|Build failed|error CS'
```

- Output: `C:\scratch\veyro-ios\builds\ios\VeyroRun-xcode\`, wiped first on every run.
- A good log has the line `[iOS privacy] Verified: LayersATTBridge.m + iOSStepCounter.mm gone, …`
  and a `Build OK: …` line.
- Timing: about 4 min with a warm `Library/` (build 1: 224 s) and about 10 min cold. The export
  is ~1.4 GB (3,514 files). Its zip is ~300 MB and takes ~1.5 min. Build 1 (re-exported 29 Sep,
  review pass): 314,304,408 bytes, SHA-256
  `04907d5a0decb1c822e121b7fd100a2cd6294e41ccb039a8fed9dba4e3e398e2` (supersedes the 28 Sep
  build-1 zip, `d3b67f9c…`).
- `VEYRO_IOS_BUILD_NUMBER` is required and must be a positive integer. Without it the export
  stops before touching anything.
- Every iOS player setting (bundle id `com.ferrabled.veyro.run`, 1.0.0, iPhone only, iOS 15.0,
  team `69J7W5URX7`, automatic signing, camera string, `veyro` scheme) is applied from code and
  restored afterwards, so `ProjectSettings.asset` never carries them.

Inspect, then zip. Both scripts are Python 3 with the standard library only. The inspection
exits 1 on any FAIL; don't zip a failing export.

```powershell
$ndk = 'C:\Program Files\Unity\Hub\Editor\6000.5.9f1\Editor\Data\PlaybackEngines\AndroidPlayer\NDK\toolchains\llvm\prebuilt\windows-x86_64\bin'
python docs\tools\inspect_ios_export.py C:\scratch\veyro-ios\builds\ios\VeyroRun-xcode --build-number N `
  --clang "$ndk\clang.exe" --nm "$ndk\llvm-nm.exe"
python docs\tools\zip_ios_export.py C:\scratch\veyro-ios\builds\ios\VeyroRun-xcode `
  C:\scratch\veyro-ios\builds\ios\VeyroRun-xcode-bN.zip
```

`zip_ios_export.py` leaves out `Veyro Run_BurstDebugInformation_DoNotShip`. It marks every `*.sh`
and the il2cpp/bee tools executable inside the zip (Windows zips store no Unix modes), and prints
the size and SHA-256. Write both down, then upload the zip **and** `docs/tools/inspect_ios_export.py`
to the Mac.

## 2. Mac requirements

- **Xcode 26 or later with the iOS 26 SDK.** App Store Connect has rejected uploads from older
  Xcode since 28 Apr 2026. The export is a standard Unity 6000.5 project (`objectVersion = 46`,
  deployment target iOS 15.0). If the build misbehaves in a way that looks like the toolchain,
  check Unity 6000.5's release notes for the Xcode versions it was tested with.
- **CocoaPods ≥ 1.12:** `brew install cocoapods`. Check with `pod --version`. Not
  `sudo gem install cocoapods`: macOS's built-in Ruby 2.6 cannot install current CocoaPods.
- **The owner's Apple ID** signed in: Xcode → Settings → Accounts, team `69J7W5URX7`.
- **At least one iPhone registered on the team** (developer.apple.com → Devices → +, name +
  UDID). The export's Release configuration signs with Unity's stock development identity
  (`iPhone Developer`), so the archive needs an iOS App Development profile, which Apple issues
  only to a team with a registered device ("Your team has no devices from which to generate a
  provisioning profile"). Distribution signing happens later, at Distribute App.
- At least 20 GB free (plus ~8 GB if the iOS 26 platform still has to be downloaded): the export
  is ~1.4 GB, and Pods plus DerivedData add several more.

## 3. Transfer and unpack

```sh
mkdir -p ~/veyro && cd ~/veyro                # no spaces; not ~/Desktop (folder-access prompt)
shasum -a 256 VeyroRun-xcode-bN.zip           # must equal the SHA-256 printed on Windows
unzip -q VeyroRun-xcode-bN.zip                # -> VeyroRun-xcode/
xattr -dr com.apple.quarantine VeyroRun-xcode inspect_ios_export.py
cd VeyroRun-xcode
find . -name "*.sh" -exec chmod +x {} \;
chmod -R +x Il2CppOutputProject/IL2CPP/build/deploy_arm64 Il2CppOutputProject/IL2CPP/build/deploy_x86_64
```

The zip already carries these exec bits; the `chmod` lines make sure. The `GameAssembly` target's
script also runs `chmod +x` itself on `il2cpp`, `il2cpp-compile` and `bee_backend`. Anything
else it calls (the .NET host libraries next to them) needs no exec bit. The two `*.sh` files are
`process_symbols.sh` and `process_symbols_il2cpp.sh`.

Optional, before `pod install`: re-run the inspection on the Mac. It needs no Unity, and Apple's
own tools replace the NDK's:

```sh
python3 ../inspect_ios_export.py . --build-number N --clang "$(xcrun -f clang)" --nm "$(xcrun -f nm)"
```

## 4. CocoaPods

```sh
pod install --repo-update
```

This creates **`Unity-iPhone.xcworkspace`**, which doesn't exist before this step. From now on, always
open the workspace, never the `.xcodeproj`. Expected pods:

- `OneSignalXCFramework/OneSignal` and `/OneSignalInAppMessages` 5.7.0 on `UnityFramework` and
  `Unity-iPhone`;
- `OneSignalXCFramework/OneSignalExtension` 5.7.0 on `OneSignalNotificationServiceExtension`;
- `PurchasesHybridCommon` 18.31.0 and `PurchasesHybridCommonUI ~> 18.31.0` on `UnityFramework`.

There is no `OneSignalLocation` and no Swift Package Manager. If `pod install` fails on a CDN or
spec error, run `pod repo update` and retry once. Record any other error verbatim.

## 5. Open, sign, check capabilities

```sh
open Unity-iPhone.xcworkspace
```

- **Signing & Capabilities → `Unity-iPhone`:** Automatically manage signing ✔, Team
  `69J7W5URX7`, bundle id `com.ferrabled.veyro.run`. The export already sets
  `CODE_SIGN_STYLE = Automatic` and the team on every configuration.
- **Signing & Capabilities → `OneSignalNotificationServiceExtension`:** the same, bundle id
  `com.ferrabled.veyro.run.OneSignalNotificationServiceExtension`. Automatic signing registers
  this App ID and the App Group `group.com.ferrabled.veyro.run.onesignal` in the owner's account
  the first time.
- **Check that Push Notifications and App Groups show as capability cards** on `Unity-iPhone`,
  and App Groups on the extension. OneSignal's project write drops the target's
  `SystemCapabilities` attributes, although the entitlement files are right:
  - `Unity-iPhone/Unity-iPhone.entitlements`: `aps-environment = production` + the app group;
  - `OneSignalNotificationServiceExtension/OneSignalNotificationServiceExtension.entitlements`:
    the app group.

  If a card is missing, add it with **+ Capability** (Push Notifications; App Groups → tick
  `group.com.ferrabled.veyro.run.onesignal`). Xcode reuses the same entitlement file. Leave
  `aps-environment` at `production`: that is right for TestFlight and the App Store.
- `UnityFramework` needs no team of its own: it is re-signed when it is embedded. If Xcode
  insists, pick the same team.
- The **`GameAssembly`** target compiles the IL2CPP C++ inside Xcode (the export ships generated
  C++, not a prebuilt library). It is the slowest part of the first build.
- **`UnityRuntime.framework` is embedded with CodeSignOnCopy although its binary is a static `ar`
  archive.** This is Unity's stock layout, not ours. If archive validation or the upload complains
  about it, record the exact message and stop: don't hand-edit the embed phase without a
  re-export.

## 6. Archive

1. Scheme **Unity-iPhone**, destination **Any iOS Device (arm64)**.
2. Product → **Archive**. Archive always builds Release (the scheme's Run action uses Unity's
   `ReleaseForRunning` configuration).
3. When it finishes, the Organizer opens on the new archive. Don't distribute yet.

## 7. Pre-upload scan (stop on any hit; don't upload)

In Terminal. The first line picks the newest archive; if it isn't the one you just made, set
`APP` by hand (Organizer → right-click the archive → Show in Finder → Show Package Contents →
`Products/Applications/*.app`).

```sh
APP="$(ls -td ~/Library/Developer/Xcode/Archives/*/*.xcarchive/Products/Applications/*.app | head -1)"
echo "$APP"
# 1. No tracking / motion / location / ad-attribution key in any Info.plist
for p in "$APP/Info.plist" "$APP/Frameworks/UnityFramework.framework/Info.plist" \
         "$APP"/PlugIns/*.appex/Info.plist; do
  plutil -p "$p" | grep -E 'NSUserTracking|NSMotion|NSLocation|SKAdNetwork|NSAdvertisingAttribution' \
    && echo "STOP: $p" || echo "clean: $p"
done
# 2. + 3. No ATT / IDFA / location / pedometer symbols, no ATT / AdSupport / CoreLocation links
find "$APP" -type f | while read -r f; do
  file "$f" | grep -q 'Mach-O' || continue
  nm -u "$f" 2>/dev/null | grep -E 'ATTrackingManager|ASIdentifierManager|CLLocationManager|CMPedometer|CMMotionActivityManager' | sed "s|^|STOP nm: $f: |"
  otool -L "$f" | grep -E 'AppTrackingTransparency|AdSupport|CoreLocation' | sed "s|^|STOP otool: $f: |"
  # 4. No bitcode left in a linked binary (liblayers_core.a embeds it; ITMS-90482 if it survives)
  otool -l "$f" | grep -q 'segname __LLVM' && echo "STOP bitcode: $f"
done
echo "scan done"
```

Expected: three `clean:` lines, no `STOP` line, then `scan done`.
- `otool -L` does list `CoreMotion` (Unity's accelerometer, which tilt reads and which needs no
  purpose string), `AdServices` (weak) and `StoreKit`. Those are expected.
- Anything else means stop: record it and report it. Don't upload.

## 8. Upload

Organizer → **Distribute App** → **Custom** → **App Store Connect** → **Upload**. (Not the
one-click **App Store Connect** choice: since Xcode 15 it skips the options screen and uploads
with "Manage Version and Build Number" on.)
- **Untick "Manage Version and Build Number"**, so the upload keeps the `CFBundleVersion` =
  `VEYRO_IOS_BUILD_NUMBER` it was exported with.
- Symbols upload on.
- Automatically manage signing (this is where the Apple Distribution certificate and App Store
  profiles are created).

Export compliance is answered by `ITSAppUsesNonExemptEncryption = false` in Info.plist, so App
Store Connect asks no encryption question.

## 9. Expected emails

- "The following build has completed processing" — normal, usually 10–30 min after the upload.
- **Nothing about purpose strings is expected**: no ITMS-90683 (missing purpose string), no
  ITMS-91053 (missing privacy-manifest reason), no ITMS-90473 (extension version mismatch).
  That is what `IosPrivacyPostProcess` and the inspection exist for.
- If any ITMS email arrives anyway, copy its exact text into STATUS and OPEN_QUESTIONS before
  changing anything.

## 10. TestFlight

App Store Connect → Veyro Run → TestFlight:
- Once the build shows "Ready to Submit / Testing", add it to the internal group **`Friends`**
  (automatic distribution on; testers are App Store Connect users with role Developer, this app
  only; `APP_STORE_LISTING.md` §6).
- The borrowed iPhone installs **TestFlight**, accepts the invite with the tester's Apple ID, and
  installs Veyro Run.
- TestFlight purchases run in the sandbox and cost nothing; no sandbox account is needed.

## 11. Fix loop

Edit on Windows → sync the iOS scratch → `BuildIOS` with the **next** `VEYRO_IOS_BUILD_NUMBER`
(never reuse one: App Store Connect rejects it, and only after the upload) → inspect → zip →
upload to the Mac → unzip into a **fresh** folder → §3–§8 again. Keep one row per build in
STATUS: build number, zip SHA-256, what changed.

## 12. Kill switches (optional SDKs)

These are set as environment variables at export time. They need no code change.

| Env at export | Effect | Also do |
|---|---|---|
| `VEYRO_IOS_NO_ONESIGNAL=1` | compiles `VEYRO_NO_ONESIGNAL`: `OneSignalPushService.Create()` returns the Fake, the SDK never starts. The native pods and the extension are still in the export. PROFILE → NOTIFICATIONS then opens an "unavailable" panel (known loose end) | drop the OneSignal rows from App Privacy |
| `VEYRO_IOS_NO_LAYERS=1` | compiles `VEYRO_NO_LAYERS`: `LayersAnalyticsService.Create()` returns the Fake. `liblayers_core.a` is still linked (one package serves both platforms) but never initialized, and the manifest drops the Layers rows | drop the Layers rows from App Privacy; inspect with `--expect-collected 5` |

- Any other value than `1`, `0` or unset fails the export on purpose.
- Both switches on: `--expect-collected 4`. OneSignal off alone keeps 7 rows, because Layers
  still reports product interaction.
- **Removing OneSignal's native side entirely** (no pods, no extension, no push entitlement):
  1. delete the line `"com.onesignal.unity.ios": "5.4.0",` from `game/Packages/manifest.json`;
  2. **also delete** `game/Assets/OneSignal/Editor/OneSignaliOSDependencies.xml` (+ `.meta`),
     or EDM4U still writes the OneSignal pods into the Podfile;
  3. export with `VEYRO_IOS_NO_ONESIGNAL=1`.

  Android is unaffected: its package and dependency XML are separate. The inspection's
  extension, entitlement and OneSignal-pod rows then FAIL, which is expected for that
  configuration. This path is untested; don't use it without a fresh inspection.

## 13. One-line fixes if the device test fails

Both are decision 2's "trust Unity, test first". Each fix is one line, then a new export with the
next build number:

- **Camera steering inverted** (step left → lane right): in
  `game/Assets/Scripts/CameraInput/CameraFeed.cs`, set `const bool InvertReportedFlip = false;`
  (iOS player only, line 74) to **`true`**.
  - **Not** in `UseDeviceReportedOrientation()`: the rig and the orientation probe's fallback read
    `ReportedVerticallyMirrored` directly.
  - In portrait, the probe cannot catch this itself: the wrong flip is the same picture mirrored
    left-right, and BlazeFace scores it the same (`FrameOrientationTests`).
- **Tilt inverted** (tilt left → lane right): in `game/Assets/Scripts/Input/GyroTiltInput.cs`
  `Tick()`, negate `raw` under `UNITY_IOS` only:
  ```csharp
  #if UNITY_IOS
              float raw = -UnityEngine.Input.acceleration.x;
  #else
              float raw = UnityEngine.Input.acceleration.x;
  #endif
  ```

## 14. What the inspection checks

`docs/tools/inspect_ios_export.py`, one row per assertion. Build 1's table is in STATUS (Phase 3).

- **Info.plist (app):**
  - `NSCameraUsageDescription` is exactly the listing text (`APP_STORE_LISTING.md` §7);
  - `ITSAppUsesNonExemptEncryption` false;
  - URL scheme `veyro`;
  - `UIBackgroundModes` has `remote-notification` and not `location`;
  - `CFBundleShortVersionString` 1.0.0;
  - `CFBundleVersion` = `--build-number`.

  No `NSUserTrackingUsageDescription` / `NSMotionUsageDescription` / `SKAdNetworkItems` /
  `NSAdvertisingAttributionReportEndpoint` / `NSLocation*` in the app's, UnityFramework's or
  the extension's Info.plist. The extension's version and build equal the app's (ITMS-90473).
- **pbxproj** (parsed, not grepped):
  - targets `Unity-iPhone`, `UnityFramework`, `OneSignalNotificationServiceExtension`;
  - `LayersATTBridge.m` and `iOSStepCounter.mm` gone, and both stubs in UnityFramework's Sources;
  - `AdServices` and `StoreKit` weak on UnityFramework;
  - no `AppTrackingTransparency` / `AdSupport` / `CoreLocation` anywhere;
  - exactly one `PrivacyInfo.xcprivacy` in UnityFramework's resources and no target with two;
  - `ONESIGNAL_DISABLE_LOCATION=1` on UnityFramework;
  - team `69J7W5URX7` + Automatic on the app and the extension, every configuration;
  - `IPHONEOS_DEPLOYMENT_TARGET` 15.0 everywhere;
  - `TARGETED_DEVICE_FAMILY` 1 on `Unity-iPhone`. The extension's `"1,2"` is OneSignal's choice
    and accepted.
- **Entitlements:** `aps-environment` and the app group on the app, and the app group on the
  extension.
- **Podfile:**
  - `platform :ios, '15.0'`;
  - the OneSignal 5.7.0 subspecs per target, and no `OneSignalLocation` or bare `OneSignalXCFramework`;
  - `PurchasesHybridCommon` 18.31.0;
  - static `use_frameworks!`.
- **Disk:**
  - both stubs under `Libraries/VeyroPrivacyStubs/`, and neither stripped SDK source anywhere;
  - no `Libraries/` source mentioning `ATTrackingManager|ASIdentifierManager|CLLocationManager|CMPedometer|CMMotionActivityManager`;
  - `UNITY_USES_IAD 0` and `UNITY_USES_LOCATION 0`;
  - `lib_burst_generated.a` present. Without it the camera's CPU inference runs unoptimized and
    the speed gate fails;
  - the 1024 App Store icon is RGB with no tRNS (ITMS-90717).
- **Privacy manifest** (`UnityFramework/PrivacyInfo.xcprivacy`):
  - tracking false, with no tracking domains;
  - exactly FileTimestamp [0A2A.1, C617.1], UserDefaults [CA92.1], SystemBootTime [35F9.1] and
    DiskSpace [E174.1];
  - 7 collected types (`--expect-collected`), all linked and none used for tracking;
  - Unity's own `UnityRuntime.framework/PrivacyInfo.xcprivacy` present.
- **Tools** (with `--clang` / `--nm`):
  - both stubs syntax-check as freestanding C for `arm64-apple-ios15.0`;
  - `nm -u --no-llvm-bc` over every `.a` and `UnityRuntime` shows no
    `ATTracking|ASIdentifier|CLLocation|CMPedometer|CMMotionActivity`, and any `nm` error fails the
    check (`liblayers_core.a` embeds Rust/LLVM 22 bitcode that an older bitcode reader cannot parse;
    without `--no-llvm-bc` 269 of its 463 objects went unscanned while the check still passed).

---

## Device checklist (borrowed iPhone, TestFlight)

Fresh install from TestFlight. iOS asks each permission **once per install**: to repeat a
permission test, delete the app and reinstall it from TestFlight. Record pass/fail + a screenshot
per item in STATUS; stop and report on the first failure of items 1–2 (one-line fixes, §13).

1. **Tilt** (TILT & TOUCH): tilt left → **lane left**, tilt right → lane right; tap → jump.
2. **Camera** (CAMERA MODE, BETA):
   - No camera alert at launch. The iOS camera alert appears only after tapping CAMERA and
     "checking device speed…".
   - **Don't Allow** → at once (no 10 s wait) the picker reads `camera access is off` /
     `— allow it in Settings, or play with tilt`. Both buttons are enabled and TILT plays.
   - Tap CAMERA again → the same two lines, no alert.
   - Settings → Veyro Run → Camera on → back to the game → CAMERA works. iOS usually terminates an
     app when one of its privacy permissions changes in Settings, so the game may cold-start
     (menu, not the picker) on the way back: that is iOS, not a failure. CAMERA must then work
     with no alert.
   - **Step left → lane left** (the mirror check; §13 if it is inverted). Hop → jump.
   - Face tracking holds in normal indoor light. The speed gate passes (no "too slow" failure).
   - No BETA or copy problems. No error line mentions logcat.
3. **Pause / resume (hop twice):**
   - Step out of frame → auto-pause.
   - Walk back and stand still → `stand still…` → `hop twice when ready` → after one hop
     `one more hop…`, each with `— or tap RESUME`.
   - Two hops → 3-2-1 → the run resumes and steers from where you stand.
   - Walking back in without hopping **never** resumes (wait ~1 min).
   - Tapping RESUME works at any point.
4. **Purchases (sandbox, free):**
   - SHOP → Ember Skin → the status reads `opening the App Store…` → the Apple sheet → buy →
     owned and equipped.
   - Same for Frost Skin and Season 1 Pass.
   - RESTORE PURCHASES → everything is still owned.
   - No text anywhere says "Google".
5. **Push (OneSignal):**
   - **No notification prompt at launch.**
   - PROFILE → NOTIFICATIONS → ENABLE NOTIFICATIONS → the iOS prompt → **Don't Allow** → the body
     reads `Notifications are turned off in your iPhone's Settings. Tap below to open Settings and
     allow them. The full game works without them.` The button reads **ALLOW IN SETTINGS**.
   - Tap it → Settings opens on Veyro Run → allow → back → ENABLE → **NOTIFICATIONS ON** with no
     second alert.
   - Owner sends a OneSignal test push with the app backgrounded, then with it closed → the
     notification arrives, and a tap opens the game.
   - With the game in the foreground, no banner appears.
6. **Layers (gameplay analytics):**
   - PROFILE → GAMEPLAY ANALYTICS is off by default.
   - Enable → play a run → the events appear on the Layers dashboard's Events screen.
   - Disable.
   - **No App Tracking Transparency prompt, ever** (at launch, on enable, after a run).
7. **Challenge links:**
   - A `veyro://challenge?…` link tapped from Notes and from Safari opens the game, both from
     cold (app closed) and warm (app in the background). The pending-challenge modal behaves.
   - An `https://veyro.ferrabled.com/challenge…` link → the web page → OPEN → Safari's "Open in
     Veyro Run?" → the game. Known and deferred: the page's 1.5 s fallback may jump to the Play
     Store while that prompt is up (`SHARE_COMPLIANCE.md` §12). Note what happens.
8. **SHARE:**
   - On the result card, SHARE reads **COPIED** for about 2 s, then SHARE again.
   - Paste in Notes → the challenge text with its link.
   - No share sheet: that is expected (decision 4).
9. **Profile:**
   - A finished run reaches the leaderboard (submitted with platform `ios`).
   - PROFILE → RECOVERY CODE → COPY.
   - IMPORT PROFILE → PASTE → iOS shows its own **"Allow Paste"** alert → Allow → the profile
     imports.
   - Deleting the app deletes the local recovery file: expected (decision 3). COPY → IMPORT is the
     way back.
10. **Safe area:** the notch or Dynamic Island and the home indicator stay clear on the menu, the
    run HUD, the face overlay and the pause menu.
11. **Store screenshots** for App Store Connect: iPhone 6.9" **1290×2796** (or 1320×2868), RGB,
    3–10. Also replacement IAP review screenshots (1290×2796) instead of the resized Android
    captures (`APP_STORE_LISTING.md` §4–§5).
