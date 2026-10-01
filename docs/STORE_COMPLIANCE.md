# Store compliance register — Google Play (App Store: see "iOS / App Store" at the end)

The invariant this file protects: **every release is one atomic story — binary ↔ Console
declarations ↔ privacy policy ↔ store listing all describe the same build.** Mismatches
between any two of these are the #1 cause of Play rejections/removals. Update this file in
the same change that ships a feature listed below. The same invariant binds the App Store build
(binary ↔ App Store Connect answers ↔ policy ↔ listing); its register is the last section.

## Declared state (Console, as of 25 Aug 2026 — versionCode 1/2, no SDKs)

| Declaration | Current answer | True because |
|---|---|---|
| App access (Datos de inicio de sesión) | No — nothing restricted | no login, no paid content |
| Data safety (Seguridad de los datos) | **No data collected or shared** | zero network code in the binary (verified via AAB string/permission dump); camera frames processed on-device in memory = not "collected" per Google's definition (only off-device transmission counts) |
| Content rating | Everything "No" (incl. digital purchases) → PEGI 3 / ESRB E | no violence/gambling/UGC/IAP in build |
| Target audience | **13-15, 16-17, 18+ only**; does not appeal to children | never tick an under-13 bracket — that triggers the Families Policy. Keep listing art non-childlike |
| Ads | No | none |
| Advertising ID (ID de publicidad — mandatory for API 33+ targets) | **No** | manifest verified: no `com.google.android.gms.permission.AD_ID` (Play machine-checks this declaration against the manifest — declaring No with the permission present blocks the release). Flips with any SDK that injects AD_ID via manifest merge: see T-021 entry |
| Government / Financial / Health / News | No | n/a |
| Privacy policy URL | https://veyro.ferrabled.com/privacy/ | must equal `GameLinks.PrivacyPolicyUrl` baked into the binary — see "URL coupling" below |
| Category / contact | Juego → Arcade · ferrabled+veyro@gmail.com · veyro.ferrabled.com | |
| AI-asset declaration (listing graphics) | **"Etiquetar"** — icon + feature graphic tagged as AI-created (owner generated both with an image model, 25 Aug 2026) | screenshots stay UNtagged: they are real gameplay captures. The two tagged assets carry a user-visible AI label on the listing. Editable per-asset in the Asset Library — if the graphics are ever redone as human-directed design work, untag them then. Rule stays: AI-made-but-unlabeled is the only wrong state |
| Binary permissions | INTERNET + CAMERA (+ camera uses-features, all `required=false`) | any new permission in an upload = explain it in the listing + re-check Data safety |

## Per-feature flip table (do ALL flips in the SAME release as the feature)

### T-020 — RevenueCat cosmetic purchases
> **Code landed 26 Aug 2026 (t020-unity):** the binary now carries `purchases-unity` 9.8.1 and
> the merged manifest adds **`com.android.vending.BILLING`** plus
> **`android.permission.ACCESS_NETWORK_STATE`** (both from Play Billing; normal-level,
> install-time — APK diff in STATUS 26 Aug). The activity's `launchMode` is patched
> singleTask → singleTop at build time (billing requirement, `AndroidLaunchModeFix.cs`).
> The first upload containing this build MUST do every flip below in the same release. Until
> that upload happens, the Console's declared state above stays truthful for the track it
> describes.
- App access → **Sí** (the form lists "pagos" explicitly). Instruction entry: "Optional
  cosmetic IAP via Google Play billing; no account/code exists; all gameplay accessible
  without purchase."
- Data safety → Financial info / purchase history = collected (RevenueCat is a service
  provider processing purchase tokens). **Also device or other IDs = collected**: the SDK is
  configured at boot and refreshed on every resume, so every install — not just a buyer — gets
  an anonymous RevenueCat app user ID sent off-device with app version, platform and store
  country. Declaring only purchase history would under-declare a launch-time transmission.
  Follow RevenueCat's own "Google Play data safety" guide; the privacy policy's Purchases
  section (corrected 30 Aug) is the text this row must agree with.
- Content rating questionnaire → redo; "digital purchases" = yes.
- Listing → "contains in-app purchases" flag + a cosmetics paragraph. The exact paragraph to paste
  is held ready in `docs/store-kit/LISTING.md` §3 under "HELD BACK — the COSMETICS ONLY paragraph",
  together with the §5 Console rows (IAP flag, content rating) that flip in the same release.
  (Path updated 26 Aug: the old `listing.md` was merged into `LISTING.md`.)
- Privacy policy → add purchases section, bump effective date, redeploy site
  (docs/PRIVACY_POLICY.md header comment marks this).
- Console → set up **License Testing** emails so testers/judges buy without real money.
- Terms already cover purchases ("may offer") — ~~no site change needed beyond privacy~~ flipped to
  present tense ("offers optional…") 29 Aug so the terms match a build that actually sells things.
- **Text status (29 Aug 2026):**
  `docs/PRIVACY_POLICY.md` + `site/public/privacy/index.html` (purchases section, effective date
  29 Aug), `site/public/terms/index.html` (§3 present tense, date bumped),
  `site/public/support/index.html` (purchases/restore/offline/camera/data-deletion answers),
  `site/public/index.html` (privacy bullet + spec strip) and `docs/store-kit/LISTING.md` §3 (the
  COSMETICS ONLY paragraph is un-held) no longer carry the absolute "no network requests" claim.
  Owner runs `npx wrangler deploy` from `site/`; the deploy, the listing paste and every Console
  flip above go out with the versionCode-5 upload, not before. Declared state above is untouched.
- **Text status (15 Sep 2026, release-revenue-cat):** the 29 Aug text described a purchases
  launch that never reached testers; all four site pages + both doc sources revised, still
  undeployed. Effective dates now 15 Sep 2026. New corrections riding the same deploy:
  version-transition wording (closed-beta builds have no shop; the Purchases section scopes
  itself to shop-enabled versions), deletion answers no longer claim deleting the RevenueCat
  record prevents restoration (the Play purchase is a separate Google record and a later
  restore can recreate the data) and cover anonymous non-buyers honestly (their record cannot
  be individually identified; no in-app ID screen exists), uninstall/identifier claims hedge
  for Android Auto Backup (observed restoring the anonymous ID, 29 Aug device test), offline
  copy says owned cosmetics normally stay available from the on-device cache, restore route is
  the menu's SHOP → RESTORE PURCHASES, "no analytics" became "no gameplay analytics" with the
  purchase-statistics purpose stated (matches the Data safety "Analytics" purpose for purchase
  history), and undelivered "trails" left the sales copy. **The deploy must happen BEFORE the
  RevenueCat build reaches any tester**, not merely in the same window — testers must never
  hold a purchasing build while the live policy says "no collection".

### T-021 — OneSignal push notifications
> Initial Android SDK/permission integration added 20 Sep, not yet released. See
> `ONESIGNAL_SETUP.md` for exact versions and owner/device checks. This increment initializes
> anonymous OneSignal registration at boot; push delivery is off until the player enables it.
> **OS permission / TURN OFF do not disable registration and session collection.** No Supabase
> alias, email, SMS, custom gameplay tags or Layers collection is enabled in this increment.
- Data safety → device/push identifiers = collected from SDK initialization (required in this
  build, not optional just because receiving a notification is optional).
- App activity → App interactions = collected (sessions / notification interactions), with
  Analytics and Developer communications purposes. OneSignal's
  [declaration guide](https://documentation.onesignal.com/docs/en/google-play-data-safety-requirements)
  also requires purchase-history disclosure in an IAP app; retain the existing purchase row
  and review the SDK-enabled build alongside RevenueCat. This is more than an IDs-only flip.
- No location permission is requested; location sharing is disabled in the wrapper. Do not
  equate that with concealing the IP address used to connect to the provider.
- **Check the merged manifest for `com.google.android.gms.permission.AD_ID`** after adding
  the SDK — if present, the separate Advertising ID declaration becomes mandatory (targeting
  API 33+). Strip the permission if unused.
- **20 Sep APK verified:** no AD_ID, ACCESS_FINE_LOCATION or ACCESS_COARSE_LOCATION. New
  permissions are POST_NOTIFICATIONS, WAKE_LOCK, VIBRATE, RECEIVE_BOOT_COMPLETED, FCM RECEIVE,
  the app's signature-protected C2D_MESSAGE, launcher badge permissions and base
  FOREGROUND_SERVICE (the transitive WorkManager SystemForegroundService; no foreground-service
  type is declared). Reconcile any Console foreground-service declaration with actual use;
  this permission does not mean the game gained a background camera or tracking feature.
  Complete dump: `builds/onesignal-permissions.txt`. UnityPlayerGameActivity remains singleTop.
- POST_NOTIFICATIONS runtime permission → request in context, explain in listing if asked.
- Privacy policy → push section + notification-support/deletion explanation staged in the
  Markdown source and site privacy/support pages, dated 20 Sep. Deploy BEFORE this build reaches
  testers. TURN OFF is not deletion; the current anonymous subscription is not deleted by the
  Supabase DELETE ONLINE PROFILE path. Manual verified deletion uses the notification support
  ID and published support contact. Supabase linking requires server-side provider cleanup
  before enabling the alias. No site deploy or Console change has been performed by this task.

### Layers analytics
21 Sep 2026 implementation: consent-gated Layers 3.3.2 — the **official unmodified**
package, pinned by Git URL to upstream commit `7d28dcda555ab3ab3f0901c6c6e77f8710613f4b`
(tag v3.3.2); App ID `app_4cf32e54fc359326`. Advertising-ID and install-referrer
libraries are excluded project-wide in the Gradle templates, not by patching the SDK.
No Console changes or uploaded release are implied.
Use this table together with existing RevenueCat/OneSignal/Supabase declarations:

| Play category | Collection / requirement | Purposes and handling |
| --- | --- | --- |
| App activity → App interactions | Collected. Layers portion OPTIONAL; combined app's row remains REQUIRED where OneSignal session processing is automatic. | Analytics; retain Developer communications for OneSignal notifications. Runs used for leaderboards remain the existing Other actions row. |
| Device or other IDs | Collected. Layers portion OPTIONAL; combined row remains REQUIRED due to launch-time purchase/push identifiers. | Analytics plus existing app functionality. SDK installation/session IDs and random analytics support ID are pseudonymous, not anonymous data exempt from disclosure. |
| Location → Approximate location | Collected, OPTIONAL for Layers; not ephemeral because derived country/region supports analytics. | Analytics. Provider derives region from network IP, then drops raw IP. No GPS permission. OneSignal currently states it does not collect approximate location. |
| Personal info → User IDs | Keep existing REQUIRED profile declaration if profiles ship. | App functionality/account management/security as already recorded. Review Analytics purpose for the separate Layers custom user ID; do not remove this row merely because identifiers are random. |
| Purchase history | Preserve existing RevenueCat and OneSignal purchase declarations. | No new custom Layers purchase event in this increment. |
| App info/performance → Diagnostics | Collected, OPTIONAL for Layers. | Analytics and app functionality (delivery reliability). Public config enables native SDK health reporting; the bundled core contains delivery/queue/drop/retry/consent counters. Application crash/error reports and gameplay performance tracing remain disabled; do not declare crash logs merely because SDK delivery diagnostics exist. |

- Shared vs collected: retain the existing public leaderboard sharing. Provider-only
  processing may qualify for Google's service-provider sharing exception when done
  solely on our instructions. Confirm Layers advertising/CAPI destinations are off
  before relying on that exception; SDK consent alone is not account verification.
- Advertising ID: expected **No**, subject to final artifact verification. With the
  stock SDK, `mainTemplate.gradle` and `launcherTemplate.gradle` carry a
  `configurations.all { exclude ... }` block dropping
  `com.google.android.gms:play-services-ads-identifier` and
  `com.android.installreferrer:installreferrer` from every configuration, so EDM4U's
  resolution of the SDK's `Editor/LayersDependencies.xml` cannot reintroduce AD_ID and
  the SDK's JNI lookups return null. This matters because RevenueCat's Android SDK
  already pulls ads-identifier 17.0.1, so the class would otherwise be present and a
  real GAID readable on Android 8–12 (minSdk 26). **Verified 21 Sep** on the stock
  scratch build (`builds/layers-stock-verification.json`): permission set identical to
  the release baseline, AD_ID absent, no ads-identifier implementation or installreferrer
  classes in any dex. That artifact is the development flavour (release builds are
  guarded until the XP curve is approved); repeat the permission dump on the final
  signed AAB before answering the Console question.
  Inspect the merged release manifest for AD_ID, location and foreground-service
  changes; preserve the prior OneSignal permission baseline. No ads are added.
- Network transports use HTTPS. No camera frames, raw motion readings, recovery
  secrets, email or phone data are sent to Layers. Camera control labels are events,
  not image collection. Account creation/access answers remain those of profiles.
- Privacy policy effective **21 September 2026**, with separate opt-in, opt-out
  (unsent events are held on the device, never sent, and deleted on the next enable or
  when app data is cleared), US processing and support-ID deletion explanations. Public
  deletion URL remains `https://veyro.ferrabled.com/support/#delete`. DELETE ONLINE
  PROFILE does not delete the independent OneSignal/Layers records in this release.
- Full ordered release procedure, owner/vendor checks and evidence gaps:
  `SDK_PRIVACY_RELEASE.md`. The app now ships the stock SDK and redistributes no
  modified SDK source, so the courtesy request asking Layers to add a LICENSE or
  confirm terms is **not a release gate**. No fixed analytics retention period or
  one-click provider deletion is promised without an implemented process.

### T-009 — Supabase profiles + leaderboard (post-v1.0, Update 1)
> Redefined 3 Sep 2026: anonymous-first Supabase identity, **no Play Games sign-in in this
> phase** (that would be Phase 2 account linking). Spec: `docs/PROFILE_LEADERBOARD_PLAN.md`;
> paste-ready policy/support copy is HELD BACK in that plan's §8 — it must not ride the
> versionCode-5 T-020 flip, whose site text is already staged.
- App access → no new restricted content (profile is created silently, no login form, no
  credentials exist). Update the instruction note only: "anonymous player profile created
  automatically for leaderboards; no sign-in exists."
- Data safety → three rows, reconciled 18 Sep against Google's definitions (review R3/checklist):
  - **Personal info → User IDs = collected, REQUIRED** (Google's User IDs definition expressly
    includes account ids; the Supabase player UUID and the generated handle are that — do not
    assume the existing "Device or other IDs" row covers them). Purposes: app functionality,
    account management, fraud prevention/security.
  - **App activity → Other actions = collected, REQUIRED, and SHARED** (finished-run results
    uploaded automatically — there is no in-app choice, so "optional" is wrong; "shared"
    because handle+score are displayed publicly to other players, which is distinct from
    Supabase acting as processor). Purpose: app functionality.
  - Device/other IDs stays as declared since the T-020 flip.
- **Account-deletion policy is LIVE with this release** (server-side profiles exist):
  in-app PROFILE → DELETE ONLINE PROFILE (immediate; `delete-account` deletes the Supabase
  user + cascades AND requests RevenueCat customer deletion when the RC_API_KEY function
  secret is configured — owner action) **plus** the public web deletion URL
  `https://veyro.ferrabled.com/support/#delete`, which **exists in the repo site copy since
  18 Sep** (version-scoped, so it is truthful whenever deployed) — deploy the site BEFORE
  the profile build reaches any user, or the Data safety form points at a URL without the
  anchor. The in-app PROFILE tab shows a copyable Player ID as the support identifier for
  app-less requests (an ID locates a record; ownership is verified before acting on it).
- **19 Sep review correction:** deploy migration `0007` before the recovery/deletion Edge
  Functions. Provider cleanup now includes pre-recovery UUIDs and retains failed work in a
  service-only retry queue; the owner retry procedure is in PROFILE_LEADERBOARD_PLAN §5.
  Privacy copy dated 19 Sep describes the actual opportunistic flagged-run retention and
  pending provider-deletion identifiers. Redeploy that copy with the corrected profile build.
- Content rating → **unchanged**: handles are server-generated (no free text), so there is
  no UGC to declare. This is load-bearing — free-text names would reopen the questionnaire.
- Privacy policy → "Profiles and leaderboards" section + effective-date bump + redeploy
  (copy in the plan §8).
- Phase 2 (Google/Apple account linking, separate release): App access gains the optional-
  login note, Data safety adds account identifiers, Apple build must offer Sign in with
  Apple (guideline 4.8) the moment Google login exists on iOS.

### T-024 — Share a run / challenge links (21 Sep 2026, feat/share-run)

The result card's SHARE button opens the **system share sheet** (`Intent.ACTION_SEND` +
`Intent.createChooser`, plain `AndroidJavaObject` JNI — no SDK, no UPM package), and the game
registers two `<intent-filter>`s on the launcher activity so a challenge link opens the game
(`AndroidChallengeLinks.cs`, the same post-generate patch technique as the billing launchMode
fix — no committed `AndroidManifest.xml`).

**Evidence and sources: `docs/SHARE_COMPLIANCE.md`** (21 Sep research — Data safety definitions,
the user-initiated-transfer carve-out, the IARC "secondary apps" exclusion, App Links
requirements). This table is the register of record; that file is why each row says what it says.

| Declaration | Flip? | Why |
|---|---|---|
| Binary permissions | **No change** — still INTERNET + CAMERA | `ACTION_SEND` needs no permission; an `<intent-filter>` declares what the app can *open*, it grants nothing. **Verify, don't assume**: standing rule 3 applies because the manifest changed — `aapt2 dump permissions` diff + size diff against the previous release APK |
| Data safety | **No change — no data collected** | the game transmits nothing. It hands a string to an app *the user picks* in the system chooser; from there it is their message in their messenger. No contacts are read (no picker of ours, no `READ_CONTACTS`), no share event is logged anywhere off-device, and the link itself holds only a seed triple and a score — no profile id, no device id |
| App access | No change | nothing behind a share |
| Content rating | **No change, and this is load-bearing** | the shared text is **generated**, never typed: there is no free-text field anywhere in the flow, so no UGC and no "users can interact" answer changes. A future "add a message" field would reopen the questionnaire |
| Ads / Advertising ID | No change | none involved |
| Privacy policy | **"Sharing a run, and challenge links" section added** (28 Sep, from `SHARE_COMPLIANCE.md` §8) to BOTH `docs/PRIVACY_POLICY.md` and `site/public/privacy/index.html`, effective date bumped to 28 September 2026 in both. **Owner: redeploy** with or before the first build carrying SHARE — standing rule 4. Nothing new is *collected*; the section exists because the policy describes what the app does, and "we hand a line of text to an app you pick" is a thing it now does. If a later version ever *counts* shares server-side, that becomes app-interaction telemetry and the Data safety row above flips too |
| Store listing | **Only after it ships** (standing rule 5). The "challenge a friend" line may be added to the description in the same release that carries the button — not in a listing-only edit beforehand |

Two follow-ups the owner owns, neither blocking the release:

- **`https://veyro.ferrabled.com/.well-known/assetlinks.json`** with the *release* signing
  certificate's SHA-256 — that is what makes the `autoVerify="true"` https filter actually take
  over the link instead of the browser (**OPEN_QUESTIONS 24**). Shipping without it is safe and
  degrades correctly: Android fails verification, the link opens the landing page, and the page's
  "Open in Veyro" button hands the same query to `veyro://challenge?…`, which needs no
  verification at all.
- **Deploy `site/public/challenge/`** (`npx wrangler deploy` from `site/`) **before** any build
  with a SHARE button reaches a tester — a shared link that 404s is worse than no share button.
  The page is `noindex` and sets no cookie; it only reads its own query string.

### If camera data EVER leaves the device (any form, any reason)
- Prominent disclosure + runtime consent before the transmission, Data safety camera
  collection, privacy policy rewrite. Today's "never transmitted" claims are load-bearing
  in the policy, the listing draft, the support page, and the Data safety form — all four
  change together or none.

## Standing rules (every release)

1. versionCode strictly increments (`VEYRO_VERSION_CODE` env var); Play permanently burns
   used codes.
2. Target API floor rises every Aug 31 (36 since 31 Aug 2026) — `BuildScript` pins it; bump
   the pin each summer.
3. New SDK/UPM package → CLAUDE.md gotcha #10: rebuild, `aapt2 dump permissions` diff vs the
   previous release, size diff. A package can add permissions while unreferenced.
4. Policy text changes → bump effective date in BOTH docs/PRIVACY_POLICY.md and
   site/public/privacy/index.html (kept in sync by hand), then `npx wrangler deploy` from site/.
5. Listing describes shipped features only — never roadmap.
6. Content-rating questionnaire must be redone whenever a feature changes any answer.

## URL coupling (learned the hard way, 25 Aug 2026)

`GameLinks.PrivacyPolicyUrl` is compiled into the binary. Changing it requires a REBUILD —
versionCode 1 shipped with a dead GitHub Pages URL baked in and had to be replaced by
versionCode 2. The constant, the Console privacy-policy field, and the live site must always
be the same URL.

## Closed testing / production access (first-app rules, new personal account)

- 12+ testers opted in continuously for 14 days on a CLOSED track (internal testing does not
  count), with genuine usage — then "Apply for production access."
- App updates during the window are fine and do NOT reset the clock; dropping below 12
  opted-in testers or pausing the track does hurt it.


### T-009 latest review follow-up (19 Sep)

Migration 0008 makes account deletion transactional with profile recovery; deploy it after
0007 before the changed deletion/recovery functions. The privacy source and site pages now
state the eight-entry offline queue and discarded older pending runs. A blocked foreign
recovery code is retained separately on the device for support until the current profile is
deleted or app data is cleared; the current profile receives its own code. Both are secrets
on the device, never public leaderboard data. No SDK, permission, or collected-data category
is added by these corrections. The existing T-009 atomic binary/Console/policy flip remains
required; this review made no Console or production deployment changes.

### 21 Sep 2026 — policy website deployment verified

Cloudflare `veyro-site` version `0242994e-c899-4634-a3cf-c188218cf2f1` is live.
Home, privacy, support and terms were fetched from `veyro.ferrabled.com` and matched
the prepared files byte-for-byte. Privacy/terms are effective 21 September 2026;
`/privacy/#analytics` and `/support/#delete` exist. This supersedes earlier
"not deployed" statements for website copy only. No Play declaration or upload was
performed. SDK release checks and the owner/vendor gates above remain open.

21 Sep Android artifact check: `MotionRunner-SponsorSDK-release.apk` is 73,469,525
bytes (+972,600). Its permission set equals the earlier OneSignal release check;
AD_ID, ACCESS_FINE_LOCATION and ACCESS_COARSE_LOCATION are absent. ARM64 native
Layers and APK ZIP alignment are 16 KiB compatible. This verifies the current SDK
increment only; repeat on the final combined/signed Play artifact. Code 5 was not
incremented and this local keyless-shop APK must not be uploaded as a new release.

### 21 Sep 2026 — stock Layers SDK migration (supersedes the patched-SDK entry above)

The embedded, locally patched `com.layers.analytics` package was removed and replaced
by the official unmodified package pinned by Git URL to upstream commit
`7d28dcda555ab3ab3f0901c6c6e77f8710613f4b` (tag v3.3.2, still the latest upstream
release). Advertising-ID and install-referrer collection is now prevented at the
project level by a `configurations.all { exclude ... }` block in `mainTemplate.gradle`
and `launcherTemplate.gradle` rather than by editing SDK source, because EDM4U would
otherwise resolve the stock SDK's `Editor/LayersDependencies.xml` and add AD_ID via
ads-identifier 18.1.0, and RevenueCat already ships ads-identifier 17.0.1 in today's
APK. Declarations in the table above are unchanged in substance; the Advertising ID
answer stays **No**, evidenced by the rebuilt stock artifact the same evening
(`builds/layers-stock-verification.json`: no AD_ID, permission set identical to the
previous release, no ads-identifier/installreferrer classes). Re-check on the final
signed artifact.

Opt-out semantics changed with the stock SDK and are already reflected in the policy
and in-game copy: turning analytics off revokes consent and shuts the SDK down, unsent
events are held on the device and can never be sent while analytics is off, and they
are deleted on the next enable (deny consent → SDK Reset → grant analytics consent) or
when the player clears app data. The stock SDK also always emits an initialization-
timing event, covered by the existing Diagnostics declaration. No isolated-storage
claim remains anywhere: SDK files sit in the app-specific external folder
`Android/data/<pkg>/files/layers_sdk/` (device-verified 22 Sep), unreadable by other apps.

Owner items that remain: confirm no advertising/CAPI destinations are connected in the
Layers dashboard; send the courtesy license request (not a gate); run the combined-build
device test; then the Console flips in the table above. No Console change was made here.


### T-025 — Season timeline and cosmetic locker (20 Sep implementation)

Uses the existing `season1` entitlement, existing score-derived server XP and existing profile
requests; no new SDK, permission, product, login or telemetry. This build still carries every
T-009 deployment/declaration prerequisite above. Local collected markers and loadouts are
profile-scoped; recovered XP allows re-collection on a new install. Keep the UI and point-of-sale
copy clear about that distinction. Test-speed thresholds are development-only and cannot ship
through the release build guard until the production curve is approved. A separate Cosmetic QA
package uses a labelled fake profile/store and never generates RevenueCat revenue or server runs.

## iOS / App Store (register opened 28 Sep 2026, feat/iOS-implementation)

Field values the owner types into App Store Connect: `docs/store-kit/APP_STORE_LISTING.md`. The
build: `BuildScript.BuildIOS` → Xcode export → cloud Mac (`docs/IOS_BUILD_RUNBOOK.md`). Nothing has
been submitted; the state below is what the **first** submission (1.0.0, build 1) declares.
Owner decision behind it: IOS_HANDOFF decision 5 — no ITMS-90683 purpose-string warnings, no
rejection risk, the Layers package itself unmodified.

### Declared state (first submission)

| Declaration | Answer | True because |
|---|---|---|
| App Privacy → data collected | **Yes** — the seven rows of the listing §3, every one linked to the user, none used for tracking | RevenueCat (purchases, anonymous app user ID at launch), Supabase (player ID, leaderboard runs), OneSignal (subscription ID, sessions, at launch), Layers after opt-in (installation + support ID, events, IP-derived region, delivery health). Camera frames never leave the phone → not collected |
| App Privacy → tracking | **No** | no ATT prompt, no IDFA, no IDFV from Layers, no ad network or data broker; enforced at export (next table) |
| App Tracking Transparency | not used | `NSUserTrackingUsageDescription` absent and forbidden; Layers' ATT bridge replaced by a stub; the game never calls `LayersSDK.RequestTrackingPermission` |
| Purpose strings | only `NSCameraUsageDescription`, exact text in the listing §7 | asked only when the player picks CAMERA; denial falls back to tilt |
| Privacy manifest | `UnityFramework/PrivacyInfo.xcprivacy`, rewritten on each export | tracking false, no tracking domains, collected types = §3, reasons CA92.1 / C617.1 (+0A2A.1) / 35F9.1 / E174.1; RevenueCat and OneSignal pods bring their own (checked on the Mac) |
| Export compliance | `ITSAppUsesNonExemptEncryption = false` | HTTPS through the OS only; no own cryptography beyond hashing |
| Age rating | 4+ (listing §1 answers) | no UGC (server-generated handles), no chance mechanics, no ads |
| Account deletion (guideline 5.1.1(v)) | in app: PROFILE → DELETE ONLINE PROFILE; on the web: `/support/#delete` | the anonymous profile is created without a sign-up, and deletion is in the app as Apple requires |
| Sign in with Apple (4.8) | not applicable | no third-party or social login exists (Phase 2 linking would change this) |
| Other platforms named (2.3.10) | none in the iOS build's copy | Android/Google strings are split under `#if UNITY_IOS` |
| Privacy policy URL | `https://veyro.ferrabled.com/privacy/` | same URL as Play and as `GameLinks.PrivacyPolicyUrl` (URL coupling rule above) |

### What every export enforces (`IosPrivacyPostProcess`, callback order 1100)

Each item is a **build failure**, not a warning, so a regression is found on Windows before a Mac
cycle. The post-process runs after every SDK post-processor (EDM4U 40/50, OneSignal 45, Layers 100,
RevenueCat 999, `IosBuildPostProcess` 1000).

- No `LayersSettings` asset in any Resources folder (Layers' own post-build step would add the
  tracking string, SKAdNetwork keys and the tracking frameworks).
- Layers' `LayersATTBridge.m` and the Input System's `iOSStepCounter.mm` are deleted from the
  project and the disk and replaced by `Libraries/VeyroPrivacyStubs/VeyroLayersAttStub.m` and
  `VeyroStepCounterStub.m` in UnityFramework (pure-C no-ops; `LayersAttStubTests` pins them to the
  SDKs' `DllImport` declarations). If either SDK file is missing or duplicated, the SDK changed:
  the export stops.
- `AdServices.framework` and `StoreKit.framework` linked (weak) on UnityFramework for the Layers
  bridges that remain; no target links `AppTrackingTransparency`, `AdSupport` or `CoreLocation`.
- No `NSUserTrackingUsageDescription`, `SKAdNetworkItems`, `NSAdvertisingAttributionReportEndpoint`,
  `NSMotionUsageDescription`, `NSLocation*` key or `location` background mode in any Info.plist.
- No native plugin source under `Libraries/` references `ATTrackingManager`, `ASIdentifierManager`,
  `CLLocationManager`, `CMPedometer` or `CMMotionActivityManager`.
- Unity's trampoline switches `UNITY_USES_IAD` and `UNITY_USES_LOCATION` are 0 (C# reading the
  advertising identifier or `Input.location` turns them on).
- The Podfile has no `OneSignalLocation`.
- The privacy manifest is de-duplicated, declares the reasons above, and is bundled exactly once in
  UnityFramework, with no second `PrivacyInfo.xcprivacy` in any target.

What Windows cannot check, the runbook's pre-upload scan does on the archived `.app` (`plutil`,
`nm -u`, `otool -L`), including the CocoaPods frameworks.

### iOS flip table (do every flip in the same submission as the change)

| Change | Flip |
|---|---|
| Kill switch `VEYRO_IOS_NO_LAYERS=1` | drop Coarse Location and Other Diagnostic Data (Product Interaction stays for OneSignal); the manifest drops them by itself. Device ID / User ID stay (RevenueCat) |
| Kill switch `VEYRO_IOS_NO_ONESIGNAL=1` | Product Interaction stays only if Layers is on; push copy leaves the listing/review notes |
| ATT, IDFA or IDFV ever wanted (ads, attribution, cross-app analytics) | owner decision first. Needs `NSUserTrackingUsageDescription`, a prompt, App Privacy "tracking: Yes" on the affected rows, policy rewrite, the ATT stub removed and the export rule relaxed — all in one release |
| Layers dashboard: SKAN or any advertising/CAPI destination switched on | not without an App Privacy review (OPEN_QUESTIONS 22a). SKAN conversion updates are driven by Layers' remote config and are not stubbed; the export still forbids `SKAdNetworkItems`/`NSAdvertisingAttributionReportEndpoint` |
| C# starts using `Input.location` or the advertising identifier | the export fails (`UNITY_USES_LOCATION`/`UNITY_USES_IAD`). Shipping it needs the purpose string, a Location or tracking App Privacy answer and a policy change |
| New SDK, UPM package or native plugin | re-export; the post-process fails if its sources use a purpose-string API (strip and stub it, or drop it). Add its data to §3 and to `IosPrivacyPostProcess.CollectedData` together; check its pod manifest on the Mac |
| Our C# gains a required-reason API (e.g. showing free disk space → DiskSpace 85F4.1; showing file dates → FileTimestamp DDA9.1) | add the reason to `IosPrivacyPostProcess.RequiredReasons` in the same change |
| New data collected (free-text name, email, crash reports, …) | App Privacy row + `CollectedData` + policy + (for free text) the age-rating UGC answer |
| Input System step counter / motion usage turned on | forbidden today; would need `NSMotionUsageDescription`, the step-counter stub removed and a Fitness/motion answer |
| Camera data ever leaves the device | as the Play rule above: App Privacy (Photos or Videos), camera string, listing, policy — all together |
| Universal links, Keychain recovery, native share sheet (deferred) | no App Privacy change; universal links add the associated-domains entitlement and the site's `apple-app-site-association`; Keychain changes the policy's "uninstall deletes the recovery file" wording |

### iOS standing rules (every submission)

1. `CFBundleVersion` strictly increments (`VEYRO_IOS_BUILD_NUMBER`); `CFBundleShortVersionString`
   equals the App Store Connect version (`BuildScript.IosVersion`).
2. Any SDK, package, define or native change → re-export and read the `[iOS privacy]` and
   `[iOS export]` log lines; run the runbook's pre-upload scan on the archive. If an ITMS email
   arrives anyway, record its exact text here.
3. Policy text changes → Play standing rule 4 (one policy serves both stores).
4. The listing and review notes describe shipped features only and name no other platform.
5. TestFlight builds use the sandbox: no real purchases, no revenue (LICENSING_REVENUE §5).
