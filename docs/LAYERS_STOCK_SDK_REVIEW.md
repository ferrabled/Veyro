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
  set before any event can be transmitted, then identity is set to the separate random
  analytics support ID.

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
dependency resolution are the same. Still owed by the owner: the combined-build device
test of enable / off / relaunch / re-enable with Events-screen receipt (include a
first-batch payload check for `idfa` and the ad-consent fields), and confirmation that
no advertising or CAPI destinations are connected in the Layers dashboard.
