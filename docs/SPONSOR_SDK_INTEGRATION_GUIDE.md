# OneSignal + Layers: Unity feasibility and integration guide

**Reviewed 20 September 2026 — feat-implement-tracks.** Companion to
[the track strategy review](TRACKS_INTEGRATION_REVIEW.md). This guide supersedes its preliminary
SDK assumptions where the source audit below is more specific.

**Implementation update, later on 20 Sep:** the owner supplied the OneSignal App ID and the
vendor's minimal integration prompt. The initial Android integration now follows **5.1.15
Stable**, as that prompt requires, rather than the **5.3.5 Current** candidate audited below.
Use [ONESIGNAL_SETUP.md](ONESIGNAL_SETUP.md) for the installed configuration and current owner
steps. Campaign, identity linking and Layers phases below remain planned work.

**Later device checkpoint:** the owner confirmed a push arrived and tapping opened the game.
The branded notification icons, denial/retry fix and lifecycle-specific tests remain. The
owner-requested next process, including a protected Supabase sender and Layers measurement,
is in [REMINDER_AUTOMATION_PLAN.md](REMINDER_AUTOMATION_PLAN.md). No schedule is deployed.

**Verdict: feasible with our existing architecture.** OneSignal is a conventional integration;
Layers needs a more careful initialization/privacy adapter. No engine migration, authored game
scene, new player login, or custom campaign server is required for the proposed experiment.
Allow roughly **2–4 focused engineering days** for both adapters, UI, Android integration and
verification, assuming account setup is ready and the first combined build succeeds. This is a
planning estimate, not a commitment; a vendor issue or store review can extend the schedule.

This was a source and native-binary audit, **not a successful Unity import, Android build, push
delivery or dashboard test**. Those are explicit implementation gates below.

## 1. What the owner should do first

These steps unblock engineering without handing over private credentials.

### A. Confirm the release baseline

Tell the integration agent:

- Whether versionCode 5 is now public on Google Play or still in review, and its public URL.
- Whether the sponsor update should retain the September 16 plan (the existing release plus
  OneSignal/Layers) or include the newer art and profiles/backend work on this branch.
- The highest versionCode already used across Console tracks and drafts when we prepare an upload.

**Recommended baseline:** follow the September 16 sponsor-update plan until the owner explicitly
chooses a combined release. The current branch contains profiles, recovery and account-deletion
work; its production dependencies cannot be assumed complete. This choice does not prevent
building the independent adapters first.

### B. Configure OneSignal and Firebase

1. Create/select a OneSignal account and an app named **Veyro Run**. Select **Google Android
   (FCM)** and **Unity** when the setup flow asks for the platform/SDK.
2. Create/select a Firebase project you control. In **Project settings → Cloud Messaging**, check
   that **Firebase Cloud Messaging API (V1)** is enabled.
3. In **Project settings → Service accounts**, generate a private service-account key. Save it
   outside the repository. Upload it directly in OneSignal under **Settings → Push & In-App →
   Platforms → Google Android (FCM)**, then save and check that validation succeeds.
4. Copy the **OneSignal App ID**. That is the public identifier the game needs.

If credential validation fails, check the service account's `cloudmessaging.messages.create`
and `firebase.projects.get` permissions. This is a credential/configuration problem to fix
before testing Unity. [Official Firebase/OneSignal setup](https://documentation.onesignal.com/docs/en/android-firebase-credentials).

Use the existing package identifier **com.ferrabled.veyro.run** wherever registration asks for
the Android package. Do not create a new Play listing. We will install OneSignal's Unity packages;
there is no separate Firebase Unity SDK task in the documented OneSignal-only path.

**Send the agent:** the OneSignal App ID and “Android FCM configuration validated.”
**Keep private:** the service-account JSON, OneSignal sending/API keys, account password and
signing credentials. The Firebase service-account key is different from a public SDK App ID.

For early testing, one app with rigorously excluded test subscriptions is sufficient. A separate
test OneSignal app is useful isolation if desired, but is not a prerequisite. We will tag the test
installation before any real campaign is sent.

### C. Configure Layers

1. Create/select a Layers account and a **Veyro Run** project. Supply the app/site description and
   public Play URL when available: a motion-controlled runner with a shared Daily Run.
2. Open **Connections → Layers SDK → Get your App ID**. Copy the returned SDK App ID.
3. Confirm access to **Events** and note the project name/ID or dashboard URL, so test results can
   be matched to the right project. An empty Events screen is expected before integration.
   [Official Layers setup](https://layers.com/docs/sdk).
4. Keep advertising-platform forwarding/conversion integrations off for the initial retention
   experiment. Do not enable ad spending, publication automation or repository auto-install just
   to obtain the SDK ID. We will integrate it into our existing Unity build ourselves.
5. In Layers, save a brief for the Daily Run reminder experiment, or leave this until the first
   events arrive. The brief must identify audience, hypothesis, two messages, observation window,
   success measure and the next decision it will inform.

**Send the agent:** the Layers SDK App ID and project name/ID or dashboard URL. No Layers API
secret is needed inside the game. If an administrative operation later requires a secret, it
belongs in a server/secure local integration, not a Unity asset or chat message.

### D. Make one Android device available

Use the existing Nord 2 with USB debugging and Google Play services. We need a physical phone
for notification permission, delivery, background/cold-start taps, offline replay and purchases.
Tell the agent when it is connected and available for a test build. Prepare a few willing testers
for the eventual campaign; do not send a campaign while the consent/test separation is unfinished.

### E. Return this short handoff

- OneSignal App ID; FCM validation succeeded or the exact non-secret error.
- Layers SDK App ID; project name/ID or dashboard URL.
- Play release state and chosen sponsor-update baseline.
- Phone availability. Before uploading: highest versionCode already used.

The public IDs and FCM setup are enough to start real integration. A live public listing gates
the contest evidence, not local SDK work. The guide's proposed reminder and analytics choices
are defaults for implementation review; account signups do not themselves approve a campaign send.

## 2. Compatibility with this project

| Area inspected | Current game | Assessment |
|---|---|---|
| Unity | 6000.5.9f1 | Above OneSignal's 2022.3 minimum and Layers' 2021.3 minimum |
| Android | Minimum API 26; target 36; ARM64; IL2CPP | Fits the published platform floors; no version downgrade indicated |
| Native dependencies | EDM4U 1.2.188 already installed | Reuse one resolver for RevenueCat and both new SDKs |
| Android templates | Existing main Gradle/properties templates, AndroidX/Jetifier, Java 17 | Extend existing templates, then inspect actual generated build output |
| Game architecture | Runtime bootstrap, code-created UI, minimal Main scene | Both SDKs expose callable APIs and own their runtime helpers; sample scenes are unnecessary |
| SDK separation | Existing commerce/profile interfaces and adapters | Same pattern works for messaging and analytics |
| Editor | Windows | Use our fake adapters; native push and Layers Android behavior need a device |
| Rendering/camera | URP and on-device inference | Neither integration needs access to the render pipeline or camera frames |
| Android shrinking | Release and debug Minify currently off | No immediate R8 attribution stripping issue; add/test keep rules if enabling it later |
| Activity | UnityPlayerGameActivity, with our billing `singleTop` adjustment | Preserve it; verify notification resume and store purchase return together |

Vendor platform requirements: [OneSignal Unity setup](https://documentation.onesignal.com/docs/en/unity-sdk-setup)
and [Layers Unity installation](https://layers.com/docs/sdk/installation).

The template files exist, but the serialized custom-template flags are zero in this checkout.
That is a reason to inspect/ensure template activation during setup, not proof the existing
RevenueCat build is broken. The generated Gradle project and dependency report are authoritative.

## 3. What the package audit actually verified

The tagged vendor archives were downloaded outside the repository. No SDK was added to the game.

| Candidate | Pin/provenance checked | Result |
|---|---|---|
| OneSignal Unity | 5.3.5; commit bc83db7bc1c7e15b35e84eff0007893e27fbe7b9 | Android package is published in the npm registry and depends on core 5.3.5; native Android version is 5.10.2 |
| Layers Unity | v3.3.2; commit 7d28dcda555ab3ab3f0901c6c6e77f8710613f4b | UPM package with C# wrapper, Android ARM64 library and linker preservation metadata |
| Layers ARM64 binary | 1,857,944 bytes; SHA-256 8c1c5f1bb9f668a8b1260eb26586836b08037258de190b482c494c09d056f47b | ELF64 AArch64; all four LOAD segments aligned to 0x4000; all 62 declared native binding names present in dynamic symbols |

The native checks reduce the risk of an ABI mismatch and establish **16 KB ELF segment
alignment for this one library**. They do not prove final APK zip alignment, Android loader
behavior, compatibility of every library in the app, or final download-size growth.
[Android's validation guidance](https://developer.android.com/guide/practices/page-sizes).

Source provenance: [OneSignal 5.3.5](https://github.com/OneSignal/OneSignal-Unity-SDK/tree/5.3.5),
[Layers v3.3.2](https://github.com/layers/layers-sdk-unity/tree/v3.3.2).
The source observations in the next sections refer to those pins, not an assumed future release.

### OneSignal source findings

- The Android wrapper obtains Unity's current Activity instead of casting it to the legacy
  UnityPlayerActivity class. This supports our compatibility assessment; device taps still need testing.
- Notification callbacks are dispatched back to Unity's main thread. Subscribe promptly after
  initialization and before any permission prompt; queue destinations until our menu is ready.
- Android notification/user managers are created during initialization. Do not access their
  instance APIs earlier just because a generic integration example puts listener registration first.
- The location-disabled configuration generates core, notification and in-app-message dependencies
  at 5.10.2, omitting the native location dependency. Check the generated XML and Gradle graph.
- The SDK can display native in-app messages and enables window hardware acceleration to support
  them. Our first loop uses game-created UI; pause in-app messaging during gameplay and test that
  SDK initialization does not disturb the existing game view.

### Layers source findings that change the integration plan

1. **Defaults collect more than our proposed events.** Exception capture, performance capture,
   app-open and lifecycle capture default on. Explicitly disable automatic exception/performance
   reporting for the first experiment. Choose one app-session event family; `app_open` and
   `$app_open` both exist and must not be summed as two separate user visits.
2. **Initial consent is not an exposed configuration field.** The public consent method requires
   initialization. Delay initialization until optional analytics is accepted. This also avoids
   native first-open events/config requests before that choice. Do not backfill earlier play.
3. **Advertising needs an explicit engineering gate.** Android initialization calls the GAID
   lookup without a C# advertising-consent check. Its dependency XML includes advertising-ID
   18.1.0 and install-referrer 2.2. A later advertising=false call does not prove no lookup occurred.
   First verify a supported upstream way to disable this collection. If none exists at the pin,
   use a narrowly scoped, documented embedded-package adjustment to skip GAID initialization and
   remove the unused ads-identifier dependency, retaining the actual Layers SDK/event pipeline.
   Check reuse/licensing terms before redistributing a modified package. Prove the resulting
   dependency graph, manifest and outgoing payloads; do not rely on a permission removal alone.
4. **README examples drift from the C# API.** The source accepts analytics/advertising consent;
   the README also demonstrates a thirdPartySharing argument that this method does not expose.
   The source error callback takes two arguments, unlike a one-argument README example. Build
   the adapter against the pinned API. Keep server-side ad forwarding disabled separately.
5. **Editor and device are different.** The distributed native libraries are for mobile. The
   SDK handles a missing desktop library, but we should use our own fake in the Editor and tests.
6. **Queue management is present but is not a privacy shortcut.** Transport uses UnityWebRequest,
   unscaled-time scheduling and disk persistence. Reset attempts a flush before rotating identity;
   shutdown persists remaining events. Neither is automatically a safe opt-out-and-erase operation.
   Verify consent revocation, in-flight requests, disk queue and next-launch behavior explicitly.

Audit paths within the vendor archive: Runtime/LayersConfig.cs, Runtime/Layers.cs,
Runtime/Platform/Android/AndroidModule.cs, Runtime/Internal/FlushManager.cs,
Runtime/Internal/NativeBindings.cs and Editor/LayersDependencies.xml. These are source inspection
findings; native consent enforcement and production ingestion still require runtime evidence.

**Assessment:** the Layers issues require deliberate adapter/package configuration and tests.
They are not evidence that Unity or our runner architecture is incompatible. Stop and document
any unresolved collection behavior before shipping; do not describe an unverified build as analytics-only.

## 4. Integration work the agent can perform

### Phase 1 — packages and the first combined Android build

1. Work on the owner-selected baseline in an isolated checkout/scratch project as needed.
2. Add OneSignal's npm scoped registry for `com.onesignal`, preserving the existing registry.
   Pin `com.onesignal.unity.core` and `com.onesignal.unity.android` to 5.1.15 Stable for the
   initial integration (updated after the vendor prompt; the source audit above is of Current). iOS can be a later
   platform addition. Preserve RevenueCat and RevenueCat UI at exactly 9.8.1.
3. Add Layers `com.layers.analytics` at the reviewed commit, with any necessary GAID adjustment
   recorded as a small vendor patch. Never patch Unity's transient PackageCache as the solution.
4. For the installed Stable SDK, disable location sharing through the wrapper and verify no
   location permission is added. The newer package-level disable-location setting discussed
   in the 5.3.5 audit is not available in 5.1.15. Reuse
   EDM4U 1.2.188; let it resolve the combined graph and inspect the selected AndroidX/Kotlin/FCM
   versions. Resolve dependency issues based on the graph, not blanket forced downgrades.
5. Ensure the existing custom templates are active. Copy/configure OneSignal notification resources
   as needed, use our icon, preserve the launch activity and billing launch-mode patch, then build.
6. Record package locks, permission diff, native libraries, size and initial startup logs. A successful
   package import is a checkpoint; a successful release build and device startup are separate gates.

This phase needs no functioning campaign and can start before the public listing is live.
The SDK IDs are required for live service verification, not for writing the interfaces/tests.

### Phase 2 — runtime composition and game hooks

Proposed structure: a small provider-independent **Growth** assembly containing notification and
analytics interfaces, event/payload models and pure policy rules; separate **OneSignal** and
**Layers** adapters; a bootstrap-owned coordinator; and code-created menu/result controls.
Adapters reference the vendor assemblies. The domain assembly has no SDK dependency. Core/Menu/
Gameplay currently use Unity's default application assembly, so introduce only the new boundaries
needed instead of reorganizing the whole game.

| Existing integration point | Change |
|---|---|
| GameBootstrap | Construct fake/null or configured device adapters, load stored choices, initialize eligible services once |
| RunSession.StartRun | Assign an opaque run ID and record an actual player start |
| RunSession.Crash | After local results/streak persistence, emit one result, refresh reminder tags and offer opt-in when due |
| RunFlow/menu | Consume a queued Daily destination safely; preserve camera setup, pause/resume and active-run ownership |
| ProgressStore/BestBoard | Read existing state; do not introduce another streak or scoring implementation |
| Main menu/profile UI | Expose reminder and optional analytics controls plus support/privacy information |

A completed run currently means a crash/result, not leaving or restarting a run. An attract run
behind the menu must emit no player events. Telemetry must not change seed generation, scoring,
leaderboard splits, input behavior or the camera's on-device processing.

A frame loop must never wait for a network response. Send events at lifecycle/result boundaries,
bound queues, catch vendor errors and maintain a working null fallback. Do not call synchronous
disk persistence for every movement or coin pickup. Measure startup/resume time and frame hitches
on the Nord 2 instead of asserting that analytics is free.

### Phase 3 — independent choices and notification routing

Use two explicit choices: **Daily reminders** and **optional gameplay analytics**. Turning one on
does not silently turn the other on. Players declining either retain the complete game.

- Reminders: persist the app preference, apply OneSignal's pre-initialization consent gating where
  applicable, then request OS permission following a player's acceptance. Distinguish “Not now,”
  app opt-out, OS denial and OS settings revocation. Recheck on resume. Do not nag repeatedly.
- Analytics: initialize only after acceptance, with the reviewed reduced configuration. Treat
  withdrawal as a tested stop/queue-cleanup path, not just a UI switch. Past provider data needs
  its own deletion process. Do not track the analytics-decline event through Layers itself.
- In-run UX: defer new dialogs while running or in the hands-free pause/resume sequence. OneSignal
  foreground messages must not obscure obstacles; test foreground suppression and queued routing.

Use OneSignal notification **additional data** for the first loop: a versioned destination of
Daily Run, an experiment/variant and campaign identifier. Leave launch URLs empty. The SDK opens
the app; the click callback hands the destination to our coordinator. We therefore do not need
Android App Links, domain verification, a link shortener or a referral backend for this feature.

Validate the destination/field sizes, deduplicate message IDs and retain only one pending action.
On cold start wait for the menu; on warm start preserve the current game and present the Daily
entry at a safe point. An old message opens today's Daily entry. Starting play is still explicit,
particularly for camera mode; a notification must not immediately start motion gameplay.

### Phase 4 — one campaign measured by Layers

The SDKs do not require a server-to-server bridge. The app receives OneSignal's campaign context
and attaches it to its own Layers events. Use campaign/variant/date to compare aggregate results;
there is no need to send push tokens to Layers or analytics device IDs to OneSignal.

Proposed six-tag budget:

| OneSignal tag | Purpose |
|---|---|
| qa | Always exclude development/test subscriptions from real sends |
| daily_reminders | The explicit reminder preference |
| analytics_opt_in | Select only measurable, opted-in participants for the experiment |
| last_daily_day | Numeric UTC day of the last completed Daily Run |
| streak_length | Existing local streak value, read with its date |
| experiment_variant | Combined experiment/version/variant value; persist assignment |

Put build/control-mode details in Layers event properties instead of spending more tag slots.
OneSignal currently advertises six tags/six segments on Free and mobile push for organizations
up to 1,000 MAU. That can accommodate this pilot, subject to the actual account's limits. Do not
assume advanced experiments, custom events or platform frequency caps are free.
[Current OneSignal plans](https://onesignal.com/pricing).

Configure two mutually exclusive recipient segments among eligible players who accepted **both**
choices and completed a Daily Run recently. Assign A/B once locally without using or affecting
track-generation randomness. A receives the generic daily message; B receives a streak-oriented
message. Keep send time/destination constant. The owner can schedule one send per cohort per day
and maintain a send log, avoiding a dependency on advanced Journeys or paid frequency capping.
Refresh dated filters for each scheduled campaign. Offline play can leave tags stale; keep copy
accurate under that limitation. Use UTC for game-day boundaries, even if delivery uses local time.

Layers events: experiment assignment, reminder offer/choice when analytics is already enabled,
notification open, actual Daily start, and completed run. Include build, UTC day, run ID, Daily/Free,
content/world version, control/fallback mode and eligible campaign context. Select one vendor
lifecycle-event family for sessions and keep QA/dev traffic separate from production.

Measure unique completed return runners within a specified window, with assigned/sent, delivered
and clicked counts recorded separately. Record stable cohort assignment before the send. Define
click-to-run attribution as that foreground visit capped at 24 hours, expiring on a new visit;
do not attach the campaign to later unrelated runs. For an A/B comparison, count organic returns
in the assigned cohort too, not only notification clicks. Analyze only participants whose complete
observation window has elapsed. The result compares **two messages**, not push versus no push.

Use Layers' planning/review tools or app to save the hypothesis and interpretation. With a small
audience, a single pilot with honest observations is preferable to an unsupported uplift claim.
Do not connect RevenueCat purchase forwarding for this first retention question. If later added,
record confirmed purchases with transaction IDs and real currency/price; restoration and entitlement
refresh are not new sales. The generic vendor subscription helper does not describe our one-time
cosmetics correctly without deliberate mapping.

## 5. Identity, deletion and store declarations

### Keep the existing identity decision

For the sponsor-only release, SDK anonymous identities suffice. Where profiles are included,
use the existing authenticated Supabase UUID as the application user ID for OneSignal and Layers,
consistent with RevenueCat. Handle profile recovery, deletion, switching and service readiness
explicitly; a transient offline/profile-not-ready state must not reset the user's identity.

Do not equate provider logout/reset with server deletion. OneSignal supports provider deletion
through administrative APIs. Layers currently documents deletion by contacting its security
support with the project and user IDs; the per-user deletion endpoint is described as planned.
Therefore a promise of immediate automatic deletion from every provider would be incorrect.
Add an honest support workflow and retain the minimal identifiers needed to fulfill it securely.
[OneSignal SDK privacy controls](https://documentation.onesignal.com/docs/en/mobile-sdk-reference),
[Layers data protection](https://layers.com/docs/api/operational/data-protection).

### Declare the build that actually ships

Reconcile the provider defaults and our configuration, including identifiers, gameplay events,
notification activity and optional diagnostics. Layers' disclosure says IP is used to derive
country/region at ingestion before being discarded; evaluate approximate-location disclosure
as well. Advertising disabled does not mean there is no device data or inferred geography.

Update STORE_COMPLIANCE, the policy document, deployed privacy/support pages and Play Console
before the collecting build reaches testers. Remove “no gameplay analytics” from applicable
versions. Verify the final manifest's POST_NOTIFICATIONS and AD_ID state. Camera images/landmarks,
recovery keys, purchase tokens and raw logs must stay outside the event payloads.
[OneSignal disclosure guide](https://documentation.onesignal.com/docs/en/google-play-data-safety-requirements),
[Google Data safety guidance](https://support.google.com/googleplay/android-developer/answer/10787469).

## 6. Acceptance gates and device procedure

| Gate | Pass condition |
|---|---|
| Source/package | Exact pins, actual API names, documented vendor adjustments and resolved dependencies |
| Editor/EditMode | Fakes work; deterministic eligibility/cohort rules, UTC rollover, callback deduplication and run cardinality pass |
| Android build | IL2CPP release succeeds; correct GameActivity/launch mode, native exports/alignment, permissions and size checked |
| Consent/network | No initialization before the applicable choice; required advertising suppression, revocation, in-flight and offline queue behavior proven |
| OneSignal | Correct app/device subscription; actual delivery; safe cold/warm/foreground tap handling |
| Layers | Expected device events arrive once; no attract-run inflation; QA isolated; offline replay and selected identity path work |
| Regression | Tilt, camera, pause/hop-twice resume, store purchase/restore and normal offline play still work |
| Publication/evidence | SDKs verified in the public build; campaign deployed; dated experiment result and learning recorded |

**Needs human device test when implemented:**

1. Install the internal-track release bundle. Before accepting choices, verify allowed network
   behavior. Decline reminders/analytics and confirm full play; reopen preferences and accept.
2. Finish a Daily Run. Check subscription, permission, tags and a single matching Layers result.
3. From OneSignal send a test to that subscription only. Background the game, tap, and verify the
   Daily menu. Repeat after normal process closure, on warm resume and during an active run.
4. Change the OS permission and app switches. Test offline play, reconnection, analytics withdrawal
   with pending events, next launch, and an old notification crossing UTC midnight.
5. Where profiles ship, test identity availability, recovery/switch and deletion. Where they do
   not, verify that anonymous data does not become an invented account system.
6. Perform the real Play purchase/restore regression and a camera pause/resume pass. Capture logs,
   dashboard evidence, artifact version and test outcomes.

Android force-stop suppresses normal push delivery until the app is reopened; distinguish that
case from ordinary closure. Doze/network/power-saving delays must also be tested honestly.
[OneSignal delivery troubleshooting](https://documentation.onesignal.com/docs/en/notifications-show-successful-but-are-not-being-shown).

No fresh 14-day closed-test gate is required under the project's recorded production-access state.
Use internal testing and promote the verified bundle for production review. Do not assume a
sideloaded development APK proves Play install attribution, production consent, or purchase behavior.

## 7. What is easy, what is difficult, and what can wait

| Work | Expected difficulty | Why |
|---|---|---|
| Account/App IDs/FCM setup | Low; usually a short owner setup session | Dashboard work, mainly credential validation |
| OneSignal package + initial test push | Low to medium | Established Unity wrapper; our dependency infrastructure exists |
| Layers package + first event | Medium | Native import plus source/documentation mismatches to handle |
| Analytics-only/opt-out behavior | Medium; mandatory release gate | Startup defaults, GAID lookup and queue lifecycle need proof |
| Safe taps and game UI | Medium | Cold/warm boot, hands-free mode and purchase return must coexist |
| First experiment | Low technical effort | Manual dashboard sends and a clear event schema suffice |
| Production approval and real retention evidence | External timing risk | Neither compilation nor test traffic can substitute for them |

Can wait: friend challenges/deferred deep links, a backend campaign sender, automated streak
Journeys, revenue attribution, paid acquisition, iOS and advanced experimentation infrastructure.
For iOS later, add the OneSignal iOS package, APNs credentials, signing/App Groups/push capabilities,
and a Mac/Xcode device pass; review Layers' iOS attribution/privacy settings separately. Android
success does not verify iOS provisioning.

At submission, attach the public build/version and App ID/campaign description for OneSignal;
for Layers, confirm SDK verification and describe audience, hypothesis, experiment, response,
learning and next step. Store the dated screenshots/counts and use only shipped claims in the
demo. [Shipaton requirements](https://revenuecat-shipaton-2026.devpost.com/rules).
