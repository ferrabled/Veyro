# OneSignal + Layers release checklist

21 September 2026 — feat/implement-tracks. This records the implementation and the
remaining release gates, not an uploaded Play release.

## Accounts and the project URL

Layers SDK App ID: `app_4cf32e54fc359326`.
OneSignal App ID: `1f6ba056-efe3-4bfe-a0cd-a9a26150720a`.
Both are public application identifiers, not API credentials.

To share the Layers project URL: open the Veyro Run project in Layers and copy the
browser address. The URL identifies the dashboard/project for verification and
support; Unity SDK initialization needs only the App ID already supplied. Do not
substitute the Supabase URL, game website URL or an API secret. Open the project's
Events screen to check delivery after the game test.

## What this increment implements

- OneSignal Unity 5.1.15 Stable; Layers Unity 3.3.2 as the **official unmodified**
  package, referenced by Git URL at upstream commit
  `7d28dcda555ab3ab3f0901c6c6e77f8710613f4b` (tag v3.3.2). No SDK source is patched or
  redistributed. Native binaries are unchanged. RevenueCat stays 9.8.1.
- PROFILE → GAMEPLAY ANALYTICS is a separate explicit choice, off by default.
  Layers is not initialized before acceptance. An accepted choice survives restart.
  On enable the adapter initializes the SDK and sets consent analytics=true,
  advertising=false before any event can be transmitted, then identifies with the
  separate random analytics support ID. Turning it off revokes both consents and shuts
  the SDK down; unsent events stay on the device, can never be sent while analytics is
  off, and are deleted the next time analytics is enabled — the adapter denies consent,
  calls the SDK's Reset (which discards the queue and rotates the SDK device ID), then
  grants analytics consent again — or when the player clears app data. The copyable
  support ID is retained for deletion requests.
- SDK persistence is Unity's `persistentDataPath`, which on this project is the
  app-specific *external* folder `Android/data/com.ferrabled.veyro.run/files/`; the stock
  core writes its own `layers_sdk/` subfolder there (`identity_state`,
  `events_shutdown_<epoch>`, `super_properties` observed on device 22 Sep). Other apps
  cannot read it, but the device owner can via USB or a file manager. No page claims
  isolation beyond that. The old `veyro-layers`
  directory from the patched build is deleted once on first launch of the new build.
- Advertising-ID and install-referrer collection is prevented at the project level: a
  `configurations.all { exclude ... }` block in `mainTemplate.gradle` and
  `launcherTemplate.gradle` drops `com.google.android.gms:play-services-ads-identifier`
  and `com.android.installreferrer:installreferrer` from every configuration, so EDM4U's
  resolution of the stock `Editor/LayersDependencies.xml` cannot add AD_ID and the SDK's
  JNI lookups fail and return null (the behaviour upstream's README describes for the
  no-EDM4U case). This exclusion is required rather than cosmetic: RevenueCat's Android
  SDK already depends on ads-identifier 17.0.1, so `AdvertisingIdClient` is present in
  today's release APK and the stock SDK's automatic lookup would read a real GAID on
  Android 8–12 (minSdk 26); Android 13+ returns zeros without the permission.
  **Verified 21 Sep** on a scratch development build (`builds/layers-stock-verification.json`):
  permission set identical to the release baseline, AD_ID absent, no ads-identifier
  implementation or installreferrer classes in any dex, native library hash unchanged.
- Advertising consent, automatic application error and gameplay performance collection,
  and automatic deep-link/clipboard attribution are off.
  Consented app lifecycle and native SDK delivery-health reporting remain on. The stock
  SDK always emits an initialization-timing event (`layers_init_timing`) plus
  `$sdk_health` delivery diagnostics; both fall under the declared delivery-diagnostics
  disclosure. Delivery diagnostics are included in the policy and Data Safety form;
  disabling the app-performance module does not disable native SDK health counters.
- Real runs emit `daily_run_started`, `daily_run_completed`, `free_run_started` and
  `free_run_completed`. Attract/demo runs do not. Abandoning a run is not completion;
  enabling analytics midway through a run does not backfill that run.
- OneSignal taps emit `notification_opened` only when analytics is enabled. Safe,
  bounded campaign properties can accompany the next Daily Run. Arbitrary message
  data, text, tokens and secrets are not forwarded. A new visit or 24 hours expires
  pending attribution; a run already started keeps its start context for its result.
- A recognized notification presents the Daily Run menu when safe, including after
  cold launch. It never starts gameplay or requests camera permission automatically.
- Notifications use a monochrome Veyro V, replacing OneSignal's sample small/large
  icons. Permission requests now use Unity's Android callback with a 30-second bound
  to address the observed second-request hang. Physical retry testing remains needed.

The analytics identity is currently a separate random installation support ID.
There is no automatic OneSignal/Supabase/Layers identity linkage or new purchase
analytics event in this increment. DELETE ONLINE PROFILE does not delete the two
separate SDK records. Their support/deletion routes are disclosed in the app/site.

## Prove the two-track loop

1. Install the approved combined development build over the game in a coordinated
   phone slot. Never uninstall, clear data or alter other apps/settings. Preserve
   the owner's equipped items. First confirm the exact build includes both SDKs.
2. Before enabling analytics, launch and play: no Layers initialization or Layers
   event transport should occur. OneSignal/RevenueCat/Supabase are separate services.
3. Open PROFILE → GAMEPLAY ANALYTICS, read the explanation and enable. Copy the
   analytics support ID. In Layers Events, filter this test installation/build.
   A development build marks custom events `qa=true`; exclude these from real results.
   If the Events screen has an environment filter, include development for this build.
4. Play one Daily Run to completion and one abandoned run. Confirm one start/result
   pair for the completed run and no completion for the abandoned one. No demo runs.
5. Send a OneSignal **test push to the test subscription only**, using Additional
   Data entries: `schema_version=1`, `destination=daily`,
   `campaign_id=daily-reminder-test`, `experiment_id=return-v1`, `variant=a`.
   Leave Launch URL empty. These are string values. Do not target Total Subscriptions
   once real players exist. Sending still requires the owner's explicit send action.
6. Tap it from the background: the Daily Run menu appears. Play a real Daily Run.
   Verify the same campaign values on `notification_opened`, `daily_run_started`
   and `daily_run_completed`. Repeat cold launch; a foreground push must not cover
   gameplay. Verify the V icon in Android's notification display.
7. Turn analytics off. Verify no further Layers traffic, including across a relaunch
   while off. Then re-enable and verify that nothing from the disabled period is
   delivered: the adapter discards the previously queued events before granting consent
   again, so no backlog replays. Previously uploaded events remain until a provider
   deletion request. Leave the owner's preferred analytics choice intact.

Compare the same analytics-consenting cohort across providers. Do not divide Layers
completions by all OneSignal recipients while silently treating nonconsenting players
as non-returners. Campaign association is not causal uplift without a valid control.
No synthetic success/revenue or fake users should be presented as growth evidence.
Use the measured funnel as the hackathon demonstration, then document the hypothesis
and real result in Layers. Dashboard receipt and phone verification are distinct
from unit tests and must not be claimed until observed.

## Privacy and account setup before distribution

Updated source: `PRIVACY_POLICY.md`; public site privacy, support and home pages.
The policy is version-scoped, so publishing it before the SDK build is truthful.
The website itself does not gain the Layers SDK or a new tracker.

Owner/account checks:

1. Keep Layers advertising destinations/CAPI forwarding disconnected or disabled;
   this release is product analytics, not advertising attribution. Confirm the
   account's retention/deletion arrangements and applicable processor/transfer terms.
   Do not invent a fixed retention period or assume SDK advertising consent proves
   every server-side integration is disabled.
2. Upstream's SDK tag still has no LICENSE or package license field, and upstream
   publishes no releases or changelog. Since the migration to the stock package, no
   modified SDK source is distributed in the app or the repository, so this is a
   courtesy request — ask Layers to add a LICENSE or confirm terms — and **not a
   release gate**. It remains a real upstream gap, not a claim that Layers forbids
   application integration.
3. Enable a verified deletion procedure: end users contact ferrabled+veyro@gmail.com
   with the separate analytics/notification support ID. Layers currently directs
   project owners to request user-event deletion through security@layers.com with
   project ID and user ID. Do not promise a deletion API that is only planned.
4. Review the concrete Play declaration table in `STORE_COMPLIANCE.md`, and publish
   the updated policy before distributing the SDK build. The owner submits Console
   declarations; local documentation changes do not change the Console.

## Release order and outstanding inputs

1. Confirm whether to combine the latest cosmetics/character fixes and online
   profiles. The design worktree contains additional uncommitted fixes beyond its
   branch head; merging only that head does not include the current phone build.
2. Confirm the highest versionCode ever uploaded and whether production is live.
   Last documented upload was code 5. Never assume code 6 is unused.
3. Finish SDK consent/device checks on that exact combined snapshot. Run EditMode
   tests, Android release/dev builds, check native libraries, permissions, size and
   notification UI. No AD_ID or location permission should be added. This includes
   the package even while consent is off, because packages can change manifests.
4. Publish and verify the policy/support URLs. Update Data Safety, Advertising ID,
   app access and deletion URL as applicable. Use release notes covering privacy
   controls and optional reminders/analytics, not a scheduled sender that does not exist.
5. Produce the signed Play AAB with the owner's external upload keystore, real Google
   Play RevenueCat key and a confirmed unused versionCode. Keep passwords out of chat
   and source. A development APK uses the RevenueCat Test Store and must never be uploaded.
6. Upload to an internal track first, verify via Play installation, then roll out the
   agreed release. No upload, production rollout or new version is claimed by this document.

## Next increment: programmatic reminders

The SDK portion measures manual test sends. The Supabase sender is still planned in
`REMINDER_AUTOMATION_PLAN.md`: consented recipient eligibility, last completed Daily
Run, local-time/quiet-hours gating, stable experiment assignment, an idempotent outbox,
server-only OneSignal API credentials, QA exclusions and scheduled Edge Function/Cron.
Identity linkage must include provider deletion before enabling shared profile aliases.
No API key belongs in Unity. No campaign, recurring job or automatic send is deployed.

References checked 21 September 2026:
[Layers SDK setup](https://layers.com/docs/sdk),
[Layers data protection](https://layers.com/docs/api/operational/data-protection),
[OneSignal Data Safety](https://documentation.onesignal.com/docs/en/google-play-data-safety-requirements),
[Google Play definitions](https://support.google.com/googleplay/android-developer/answer/10787469).

## Verified checkpoint — 21 Sep

The evidence below was produced against the **patched** SDK build, before the same-day
migration to the stock package. Re-run on the stock build the same evening
(`builds/layers-stock-verification.json`): 515/515 EditMode tests; development APK
`builds/MotionRunner-StockLayersDev.apk` (91,913,480 bytes) with a permission set
identical to the release baseline, no AD_ID, no ads-identifier/installreferrer classes,
`liblayers_core.so` sha256 unchanged; packages-lock records source `git` at hash
`7d28dcda…`. The release flavour could not be built because the Season 1 XP-curve guard
(OPEN_QUESTIONS 23) blocks non-development builds; repeat the permission/dex diff on the
final signed artifact.

- 468/468 tests pass in the isolated Unity scratch project, including real managed
  Layers SDK consent tests with its native/network-free mock. These tests enter
  Play Mode because the SDK owns a persistent MonoBehaviour. Initial test-only
  failures from creating that object outside Play Mode were corrected.
- Policy, support, home and terms deployed to Cloudflare and public responses match
  local files byte-for-byte. Deployment `0242994e-c899-4634-a3cf-c188218cf2f1`.
- Vendored Android native libraries have 16 KiB ELF LOAD alignment.
- Android ARM64 release-format verification APK passed: 73,469,525 bytes (+972,600
  versus the OneSignal-only check), no permission additions/removals, no AD_ID or
  location permission, Veyro small icon compiled and vendor large icon absent.
  Native bytes match upstream after Unity's debug-symbol stripping; 16 KiB ELF/ZIP
  alignment verified. Detailed hashes and dumps: `builds/layers-verification.json`.
  This is the uncombined code-5 keyless-shop check, not a Play release or phone build.
  The subsequent development build was cancelled and deferred to the combined snapshot.
- No phone calls, account changes, API-secret reads, campaign sends or Play uploads
  were performed during this increment. Physical SDK/Events verification is pending.

Public configuration check: `GET /config` for the supplied App ID and Android returned
HTTP 200 / success, with `health.enabled=true` and clipboard attribution disabled.
The bundled native library contains `$sdk_health` and queue/delivery/drop/retry/config/
consent fields. This is why Diagnostics is declared even though app exception and
performance modules are disabled. No synthetic event or write/probe endpoint was used.
This check proves a configuration response, not SDK ingestion or dashboard receipt.
