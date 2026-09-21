# Store compliance register — Google Play

The invariant this file protects: **every release is one atomic story — binary ↔ Console
declarations ↔ privacy policy ↔ store listing all describe the same build.** Mismatches
between any two of these are the #1 cause of Play rejections/removals. Update this file in
the same change that ships a feature listed below.

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
- Data safety → app interactions / diagnostics = collected. Privacy policy section. Same
  AD_ID check as OneSignal.

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
