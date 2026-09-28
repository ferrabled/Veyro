# Stock Layers SDK migration assessment — 21 September 2026

Owner requested this assessment before replacing the SDK. This is an assessment,
not a completed migration or a change to the approved data-collection scope.

## Why the SDK was patched

The 20 September integration guide identified a mismatch between the proposed
analytics-only experience and upstream defaults. The 21 September implementation
chose a local consent/privacy patch. It was not needed to send our gameplay events
or to make Unity compatible with Layers, and no contest requirement calls for it.
Distribution permission remained unresolved in OPEN_QUESTIONS 22; an engineering
reason for a patch was not evidence of permission to distribute it.

Compared against upstream v3.3.2, commit
`7d28dcda555ab3ab3f0901c6c6e77f8710613f4b`, three SDK files differ:

| Local change | Purpose | Stock consequence |
| --- | --- | --- |
| Initial consent ordering and Android attribution switch | Set advertising consent false before managed transport starts; skip GAID/referrer modules. | Consent requires initialization; Android attribution starts automatically. |
| Dedicated persistence directory | Isolate SDK files from game saves. | Uses the shared application data directory. |
| Revoke-and-shutdown operation | Stop transports, reset pending data, then allow safe folder removal. | Public Reset attempts an HTTP flush first; Shutdown preserves pending data. Neither is an equivalent purge operation. |
| Conditional initialization-timing event | Honor disabled performance capture for this additional event. | Initialization timing is emitted even with automatic performance capture disabled. |
| Advertising-ID/referrer dependency removal | Keep these libraries and their AD_ID permission out of the app. | Dependency resolution includes them; audit the final Android manifest. |

These are source findings. Native consent enforcement, offline replay and actual
provider receipt must be verified on a device; a successful C# test is insufficient.

## What can stay

The game's event names, run start/completion hooks, notification-to-Daily routing,
campaign labels, separate support ID and default-off initialization can stay behind
the existing adapter. OneSignal and gameplay need not be rewritten. The current
run/campaign tests remain useful. Standard configuration can still disable the
automatic exception, deep-link and general performance modules.

## Work required for the official package

1. Use the official Git UPM dependency at an exact reviewed revision; remove the
   embedded package and only its two LFS rules. Preserve the art LFS rules.
2. Rewrite the adapter's three patch-only API references (persistence directory,
   attribution toggle, revoke-and-shutdown). Use documented public SDK operations.
3. Decide what behavior is promised for advertising attribution and unsent events.
   Public stock configuration does not reproduce the current guarantees. Merely
   calling advertising=false after initialization is not proof that no ID lookup
   occurred. Removing AD_ID alone does not disable install-referrer processing.
4. Replace patch-specific consent assertions with adapter and stock-SDK checks.
   Verify enable, disable, relaunch, offline queue and re-enable behavior. Preserve
   local player progress; never delete the game's shared persistent-data directory.
5. Handle any old `veyro-layers` directory deliberately, preserving the saved choice
   and support ID. Do not migrate previously discarded telemetry into a new queue.
6. Reconcile in-game copy, privacy/support pages, store checklist and Play declarations
   with the observed behavior. Rebuild Android, compare permissions/size, and verify
   Events receipt and the OneSignal-to-run measurement loop on the real device.

## Recommendation

Keep the official SDK unmodified and keep analytics opt-in. Before changing the
current no-advertising and discard-on-opt-out promises, obtain the owner's scope
decision. If those promises remain requirements, request supported controls or an
upstream fix from Layers. Project-level dependency exclusions are another avenue
to investigate, but are not equivalent to a supported SDK switch and have not been
validated here. Do not replace the fork with reflection/private-native workarounds.

The code migration is bounded; the unresolved part is the privacy behavior and its
verification. No SDK files were changed during this assessment.

Sources: [official installation](https://layers.com/docs/sdk/installation),
[SDK at the reviewed tag](https://github.com/layers/layers-sdk-unity/tree/v3.3.2).
Detailed comparisons are retained locally under `builds/layers-stock-audit/`.

## Outcome — 21 September 2026

The owner approved the migration the same day and it was implemented.

**Decision.** Ship the official unmodified package, referenced by Git URL at commit
`7d28dcda555ab3ab3f0901c6c6e77f8710613f4b` (tag v3.3.2, still the latest upstream
release). The embedded patched package and its two LFS rules are gone; the art LFS
rules stay. The three patch-only behaviours are replaced as follows:

- *No advertising ID / install referrer* → a project-level Gradle exclusion rather than
  SDK source. `configurations.all { exclude ... }` in `mainTemplate.gradle` and
  `launcherTemplate.gradle` drops `com.google.android.gms:play-services-ads-identifier`
  and `com.android.installreferrer:installreferrer` from every configuration, so EDM4U
  (installed, auto-resolve on build) cannot honour the stock
  `Editor/LayersDependencies.xml`, and the SDK's JNI lookups fail and return null —
  the behaviour upstream's own README describes for the no-EDM4U case.
- *Isolated persistence* → dropped as a promise. The stock SDK stores its files under
  Unity's `persistentDataPath`, which here is the app-specific external folder
  `Android/data/<pkg>/files/`; on device (22 Sep) the core created its own `layers_sdk/`
  subfolder there. Other apps cannot read it; the device owner can. No document claims
  more isolation than that. The old `veyro-layers` directory is deleted
  once on first launch of the new build, preserving the saved choice and support ID.
- *Revoke-and-shutdown purge* → purge-on-re-enable. Turning analytics off revokes both
  consents and shuts the SDK down, so queued events can never leave the device; they are
  deleted the next time analytics is enabled (deny consent → the SDK's `Reset`, which
  discards the queue and rotates the SDK device ID → grant analytics consent), or when
  the player clears app data. On enable, consent analytics=true / advertising=false is
  set synchronously after initialization, then identity is set to the separate random
  analytics support ID. Initialization follows the player's accepted analytics choice;
  it starts a configuration request and can queue startup events before SDK consent is
  applied. First-uploaded-batch fields still require verification (see the 23 Sep review
  in `SDK_PRIVACY_RELEASE.md`).

**Why the exclusion is load-bearing, not belt-and-braces.** RevenueCat's Android SDK
already depends on `play-services-ads-identifier` 17.0.1, so `AdvertisingIdClient` is
present in today's release APK independently of Layers. Without the exclusion the stock
SDK's automatic lookup would read a **real GAID on Android 8–12** (the game's minSdk is
26); only on Android 13+ does it return zeros without the AD_ID permission. EDM4U would
additionally have pulled ads-identifier 18.1.0 and its AD_ID manifest entry. "Call
advertising consent false after initialization" was never sufficient on its own.

**Upstream feature request to Layers.** The Rust core already exposes `consent_required`,
`respect_dnt` and `cookieless_mode` config keys and a `requires_explicit_consent` method
that the Unity wrapper does not surface. Surfacing them would give "no events before
consent" natively, instead of depending on the adapter never initializing the SDK. We do
not use these keys today; this is an ask, not a dependency. The missing LICENSE is a
separate courtesy request and, with no modified source redistributed, no longer a gate.

**Verified 21 Sep (scratch build, `builds/layers-stock-verification.json`).** The
resolved package is upstream 3.3.2 at the pinned hash with none of the patch APIs;
515/515 EditMode tests pass including the four rewritten consent tests. EDM4U did resolve
the stock `LayersDependencies.xml` into the generated `unityLibrary/build.gradle`
(ads-identifier 18.1.0 + installreferrer 2.2), and the `configurations.all` exclusion in
both generated Gradle files neutralised it: the built APK's permission set is identical
to the previous release baseline, AD_ID is absent, no ads-identifier implementation
class and no installreferrer class is in any dex (the only remaining mention is
RevenueCat's own type reference to `AdvertisingIdClient` in its never-called fetcher,
which was also present before), and `liblayers_core.so` hashes identically to the
previous build. The artifact is the development flavour because the release flavour is
blocked by the Season 1 XP-curve guard (OPEN_QUESTIONS 23); manifest merge and
dependency resolution are the same.

**Device follow-up — 22 Sep; review reconciled 23 Sep.** The stock development APK's
enable / off / relaunch / re-enable sequence passed on the Nord 2. A held queue stayed
unchanged while off, then was discarded on re-enable with SDK identity rotation and no
increase in delivered counters during purge. Six events had been accepted in three 2xx
batches while enabled. This is transport and persistence evidence, not Events-screen
receipt. The held shutdown queue had no advertising ID and had false ad-consent fields;
the first uploaded batch was not inspected. Remaining: dashboard receipt and first-batch
fields, failed-initialization recovery with an old queue, legacy-directory cleanup,
the combined OneSignal-to-Daily loop, final release-artifact checks, and owner confirmation
that no advertising or CAPI destinations are connected in Layers. See
`SDK_PRIVACY_RELEASE.md` for the current matrix rather than repeating the completed
22 Sep phone lifecycle test as wholly untested.

## iOS addendum — 28 September 2026 (feat/iOS-implementation, Phase 2b)

**The package stays the unmodified v3.3.2 at the same pinned commit on iOS.** Nothing in
`Library/PackageCache` is edited. The change happens in the *exported Xcode project*, at
build time, by `game/Assets/Editor/IosPrivacyPostProcess.cs` (IOS_HANDOFF decision 5b).

**Wrapper.** `LayersAnalyticsService.Create()` is now real on iOS too, unless BuildIOS
compiled the `VEYRO_NO_LAYERS` kill switch in (env `VEYRO_IOS_NO_LAYERS=1`). The Android
branch compiles to the same code as before. The opt-in is unchanged: off by default,
nothing is initialized before the player enables GAMEPLAY ANALYTICS, and the same
purge-on-re-enable sequence applies. The game never calls
`LayersSDK.RequestTrackingPermission`.

**What the stock package brings to an iOS export** (read from the package source and the
28 Sep export):

| Native piece | What it does | In our build |
| --- | --- | --- |
| `Plugins/iOS/LayersATTBridge.m` | ATT status and prompt, IDFA, IDFV; imports AppTrackingTransparency + AdSupport, the only Layers file that does | **removed at export**, replaced by a pure-C stub exporting the same six `layers_att_*` symbols: not available, status 0, `request_tracking(cb)` → `cb(0)`, IDFA/IDFV NULL, `free_string` → `free` |
| `LayersAdServicesBridge.m` | AdServices attribution token | linked (AdServices weak on UnityFramework), never called: the token is read only when `AutoTrackAppOpen` is true, and `LayersConsentFlow.BuildConfig` sets it false |
| `LayersSKANBridge.m` | SKAdNetwork registration / conversion values | linked (StoreKit weak), armed **only** by the Layers dashboard's remote config (`SKANModule.ConfigureFromRemoteConfig`). Keep SKAN off there (OPEN_QUESTIONS 22a). The export forbids `SKAdNetworkItems` and `NSAdvertisingAttributionReportEndpoint` |
| `LayersDeviceInfoBridge.m` | OS version, model, app version/build | used; no identifier, no purpose string |
| `LayersInstallTimeBridge.m` | Documents-folder creation date (install time) | used; a file-timestamp read inside the container, declared as C617.1 |
| `LayersBackgroundFlush.mm` | BGTaskScheduler flush | compiled, never registered: the game never calls `EnableBackgroundFlush()` |
| `liblayers_core.a` (62.6 MB, arm64 device only) | the Rust core | `llvm-nm -u` re-checked 28 Sep: no ATTracking / ASIdentifier / CLLocation symbols (only `sysctlbyname` for the model) |
| `PrivacyInfo.xcprivacy` | UserDefaults CA92.1, FileTimestamp C617.1, SystemBootTime 35F9.1; empty collected types | merged by Unity into `UnityFramework/PrivacyInfo.xcprivacy`, then rewritten by the post-process (de-duplicated, tracking false, collected types = the listing's App Privacy rows, which include Layers') |
| `Editor/LayersPostBuildProcessor.cs` (order 100) | ATT string, SKAdNetwork IDs, tracking frameworks, associated domains, from a `LayersSettings` asset | inert: there is no `LayersSettings` asset, and the export fails if one appears |

**Behaviour this changes on iOS, compared with the stock bridge.** `InitIOSModules` merges
no `idfv` into the device context: `GetVendorId()` gets NULL and skips it. So Layers sends
**no Apple identifier at all**. Its device identity is its own random SDK device ID plus the
game's `veyro-…` support ID. `ATTModule.GetStatus()` reads "not determined" and
`IsAvailable()` false. A stray `RequestTrackingPermission` call would answer
`NotDetermined` synchronously and show nothing. The ATT bridge was also the only
IDFV source in the package, so the App Privacy "Device ID" row now rests on installation IDs
only (listing §3).

**Storage.** As on Android, the SDK keeps its files under `Application.persistentDataPath`,
which on iOS is the app's sandboxed `Documents` folder. Other apps cannot read it. Like the rest
of `Documents`, it is included in the device's iCloud/computer backups. Uninstalling deletes it.

**SDK drift.** `LayersAttStubTests` (EditMode) parses the package's
`Runtime/Platform/iOS/ATTModule.cs` out of the package cache and asserts that the stub exports
exactly its `layers_att_*` externs, with the same ABI shape. The same test covers the Input
System's `iOSStepCounter.cs` and its stub. The export also fails if `LayersATTBridge.m` is
missing or duplicated. **When bumping Layers:** run EditMode, read the new ATT and SKAN
bridges, re-run the stub syntax check (`clang --target=arm64-apple-ios15.0 -fsyntax-only
-ffreestanding`), and redo the `llvm-nm -u` scan of `liblayers_core.a` for ATTracking /
ASIdentifier / CLLocation.

**Still open (unchanged by iOS):** dashboard receipt of iOS events, and the owner's
confirmation that no advertising, CAPI or SKAN destination is connected in Layers
(OPEN_QUESTIONS 22a). Add the iOS device check "enable → events arrive; disable; no ATT
prompt, ever" (IOS_HANDOFF §9 item 6).
