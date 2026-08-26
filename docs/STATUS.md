# Status journal (newest at top)

Keep entries short: what changed, how it was verified, what needs a human. Durable operational
knowledge does **not** belong here — invariants go in code comments, recurring traps go in the
"Known gotchas" list in CLAUDE.md. Old entries may be pruned once their content lives elsewhere.

## 2026-08-26 — T-020 code complete: RevenueCat 9.8.1 in, Commerce seam + store UI + key guard; needs keys + device test (t020-unity session)

**Headline: the whole Unity half of T-020 is built and green — SDK 9.8.1 via OpenUPM, the
`MotionRunner.Commerce` seam with 22 new EditMode tests (162/162 total), a store panel wired to
the dashboard paywall, entitlement-driven skins, and a build guard that makes shipping the Test
Store key in an .aab a build failure. Two things stand between this and done: the owner must
paste the two public API keys (OPEN_QUESTIONS 9b — the handoff's key placeholders were never
filled in), then run the Test-Store device test below.**

**Install route: OpenUPM (plan §1.1), EDM4U in mainTemplate-patch mode.** `manifest.json` pins
`com.revenuecat.purchases-unity` + `com.revenuecat.purchases-ui-unity` **9.8.1** exactly and
`com.google.external-dependency-manager` **1.2.188** (latest on OpenUPM; ≥ 1.2.187 keeps the iOS
SPM path open — the plan's 1.2.186 was a verify-at-install placeholder). Instead of letting EDM4U
download AARs into `Assets/Plugins/Android` (Compose has dozens of transitive deps — exactly the
gotcha-11 debris class), the three gradle templates are committed under `Assets/Plugins/Android/`
and EDM4U patches `mainTemplate.gradle` with 4 dependency lines
(`purchases-hybrid-common[-ui] 18.31.0`, androidx.activity/annotation); Gradle pulls transitives
at build time. EDM4U settings are pinned project-side in `ProjectSettings/GvhProjectSettings.xml`
(not per-machine EditorPrefs), resolution record in `ProjectSettings/AndroidResolverDependencies.xml`
— all committed deliberately (plan §2.7). The templates are copies of 6000.5.9f1's defaults: on a
Unity version bump, re-copy them and let EDM4U re-patch.

**APK diff (gotcha 10), release `BuildAndroid`, same tree before/after** (the old 29.6 MB figure
was the pre-camera baseline; `main` ships camera mode since PR #1, so the honest baseline was
rebuilt first):

| | bytes | permissions |
|---|---|---|
| baseline (this tree, pre-RevenueCat) | 47,024,127 (44.8 MB) | INTERNET, CAMERA |
| with RevenueCat 9.8.1 + wrapper + UI | 58,205,601 (55.5 MB) | + `com.android.vending.BILLING`, + `ACCESS_NETWORK_STATE` |

+10.7 MB — the Jetpack-Compose-for-paywalls line item the plan budgeted for (§1.2); acceptable,
UGUI-fallback question closed. BILLING is required; ACCESS_NETWORK_STATE rides in with Play
Billing (normal-level, install-time). Both noted in STORE_COMPLIANCE with the flip table.

**Found and fixed while checking §2.2:** the merged manifest gave `UnityPlayerGameActivity`
`launchMode="singleTask"` (Unity's default) — RevenueCat requires standard/singleTop or a
purchase dies when the player is bounced to a banking/3DS app mid-payment. New
`AndroidLaunchModeFix` (IPostGenerateGradleAndroidProject) patches it to **singleTop** at build
time; verified `launchMode=1` in the rebuilt APK's manifest. No committed AndroidManifest.xml
shadows Unity's template.

**The seam (plan §3):** `MotionRunner.Commerce` (engine-free, `noEngineReferences: true`):
`IStore` (callback-shaped; `PresentPaywall()` takes no entitlement argument — Present, never
PresentIfNeeded), `StoreOffer`, `PurchaseOutcome` (status + optional `StoreError`),
`Entitlements` (`skin_ember`/`skin_frost`/`season1` + the package→entitlement map),
`CosmeticCatalog` with unlock rules **Default | Entitlement(id) | SeasonLevel(track, level)**
per COSMETICS_CATALOG §5 — the SeasonLevel shape exists and is tested, but no season items and
no XP ship: that is T-025, deliberately untouched — and `FakeStore`. 22 new EditMode tests cover
the §3.4 list: locked→purchase→unlocked, restore-after-reinstall shape, same-set re-announce,
cancel ≠ failure, AlreadyOwned (the out-of-app promo path), fail-open (not-ready grants nothing
and throws nothing), catalog integrity, SeasonLevel truth table. **162/162 EditMode tests pass**
(was 140), run in batchmode against this tree (owner's editor closed — no scratch copy needed).

**The adapter:** `MotionRunner.Commerce.RevenueCat` / `RevenueCatStore` — the only assembly that
sees an SDK type. Runtime setup on a persistent GameObject (`useRuntimeSetup` set before the
component's `Start()`; `Configure` deferred one frame because `Purchases` binds its platform
wrapper in `Start()` and configuring earlier NREs), **anonymous app user id** (null → SDK
generates, §3.3), `UpdatedCustomerInfoListener` → `EntitlementsChanged`, purchase error code 6
(ProductAlreadyPurchased) mapped to `AlreadyOwned`, and **entitlements re-read on
`OnApplicationPause(false)`** with `InvalidateCustomerInfoCache` — the §6.3 out-of-app promo
redemption path. Fail-open throughout: no key / no network / no offering → locked entitlements,
playable game, no exceptions. In the Editor (where the SDK NREs, §2.5) GameBootstrap hands the
game a ready `FakeStore`, so the store UI is exercisable in Play mode.

**UI (code-first UGUI, matching RunHud/ModeSelectMenu):** the result screen gained a
**SKINS & STORE** button (RunHud card 780→920) opening `StorePanel`: catalog rows with live lock
state and localized prices from `GetOfferings`, tap-to-equip for owned skins, the dashboard
paywall for locked ones (the call site gates — it only opens for items the player does not own),
a Season 1 pass row honestly labelled "reward ladder arrives with the Season 1 update", a visible
**RESTORE PURCHASES** button (§6.3), and fail-open status text. Skins apply through
`SkinService` → `CosmeticCatalog` → `RuntimeMaterials.Shared` at the GameBootstrap colour site;
the default skin pins the original orange (test-enforced), Ember/Frost are colour swaps until
T-025 builds the full presets. Selection persists in `veyro.skin`; a revoked entitlement falls
back to the default on the next apply.

**Key handling (the door that must not open the wrong way):** `RevenueCatKeys.cs` holds both
**public** SDK keys — currently **empty: the session handoff's key placeholders were never
filled** (→ OPEN_QUESTIONS 9b). Selection is compile-time: `BuildAndroidBundle` sets
`VEYRO_STORE_BUILD` for the duration of the build (restored in `finally`) → `ActiveKey =
PlayStoreKey`; APK builds never set it → `ActiveKey = TestStoreKey`. `ApplyStoreKeyPairing` plus
`StoreBuildGuard` (IPreprocessBuildWithReport — catches editor-GUI builds too) fail the build on
any mismatch: .aab without the define, .aab with a non-`goog_` key, APK with a non-`test_` key.
Consequence today: **dev APKs build with the store disabled (fail-open, loud build-log warning);
`BuildAndroidBundle` refuses to build until the Play key is pasted** — deliberate, an .aab with
a missing/test key would be a dead store build.

**T-031 AAB path:** `BuildAndroidBundle` already existed (24 Aug) and gains only the key guard;
signing unchanged via `VEYRO_KEYSTORE`/`VEYRO_KEYSTORE_PASS` (+ optional `VEYRO_KEYALIAS`,
`VEYRO_KEYALIAS_PASS`, `VEYRO_VERSION_CODE`) env vars. The upload keystore exists — owner created
it 25 Aug (D5 ✅; the keytool runbook stays in PREREQUISITES D5 for recovery). The next .aab
carries BILLING + the purchases SDK, so **every STORE_COMPLIANCE T-020 flip rides that same
release** (note added there).

**Also:** `companyName` DefaultCompany → **Ferrabled** (`productName` was already "Veyro Run") —
plan §7B item 13. Rename in ProjectSettings if a different publisher label is wanted; cheap now,
awkward after store screenshots.

**Needs the owner (in order):**
1. **Paste the two public API keys** into `game/Assets/Scripts/Commerce/RevenueCat/RevenueCatKeys.cs`
   (OPEN_QUESTIONS 9b) and commit — public keys, safe in git; never the `sk_` secret ones.
2. **Test-Store device test** (~15 min). Rebuild + install:
   `Unity -batchmode -quit -projectPath game -buildTarget Android -executeMethod MotionRunner.EditorTools.BuildScript.BuildAndroid`,
   `adb install -r builds/MotionRunner.apk`. Then:
   - crash a run → **SKINS & STORE** → tap Ember → the dashboard paywall renders (it cannot
     render in the Editor) → buy via the Test Store sheet → row turns owned → equip → runner
     turns ember; relaunch → still owned, still equipped.
   - background the app → grant/revoke an entitlement on the customer in the RevenueCat
     dashboard → foreground → state updates without a restart (§6.3 resume re-read).
   - **RESTORE PURCHASES** on the same install returns the entitlements.
   - uninstall → reinstall → Restore: **expect empty on the Test Store** — a fresh anonymous app
     user id has no Test Store receipts behind it. The real reinstall→Restore acceptance test
     (T-020's AC) only means something against Play sandbox (plan §7C item 16), once the Play
     products (§D1) + service-account JSON (§D3) exist.
3. **Play side, unchanged:** create the 3 products (PLAY_CONSOLE_SETUP §D1), upload the
   service-account JSON (§D3), then `BuildAndroidBundle` → internal track (unlocks in-app product
   creation) / closed track as the next update, with the STORE_COMPLIANCE flips in the same
   release.

**Next:** keys + device test → T-020 🔷→✅; after that the paywall is pure dashboard iteration
(plan §5, no store review). T-025 (season pass content) now has its rule shapes and seam waiting.

## 2026-08-26 — PR #2 review pass: entitlement model, judge-code hardening, season-window fix, timeline recomputed (doc session)

Worked the Copilot review on PR #2 (17 inline + 7 suppressed comments — all 24 verified against the
repo, none spurious). No runtime code touched; `game/Assets/Scripts/Commerce/` still does not exist,
which is exactly why these mattered: the T-020 Unity session reads these docs as the spec.

**Design changes, not just wording:**

- **Three entitlements, everywhere.** `REVENUECAT_PLAN.md` still specified a single `cosmetics`
  entitlement in five places (§3.2 const, §3.3 paywall row, §7 checklist steps 1/3, step 11) — dead
  since the 23 Aug catalog redesign. Implementing it would have made *every* entitlement check fail.
  The §7 checklist was the dangerous one: it told the owner to create **two** products and one
  `cosmetics` entitlement in Play Console, where ids are immutable. Now `skin_ember` / `skin_frost` /
  `season1` throughout, matching `PLAY_CONSOLE_SETUP.md` §D (which was already correct).
- **`PresentIfNeeded` → `Present`.** With three independent entitlements no single
  `requiredEntitlementIdentifier` answers "already owned": any one of them shows the paywall to
  someone who owns the other two. `IStore.PresentPaywall` lost its entitlement argument; gate the
  call site instead.
- **`PurchaseOutcome` is a struct, not an enum.** It was sketched as
  `enum { … Failed(reason) }`, which does not compile — a C# enum cannot carry a payload, and the
  reason is needed for §6 diagnostics. Now `PurchaseStatus` enum + optional `StoreError`.
- **Judge codes are random and absent from the APK.** The proposed `LogIn("shipaton-judge-<n>")`
  (OPEN_QUESTIONS 10) was a guessable shared identifier — `strings` on the APK confirms the pattern
  and the next person to try `-2` inherits the granted entitlement. Now one high-entropy code per
  judge (`openssl rand -hex 6`), generated out-of-band, validated by nothing, revocable per-ID.
  Blast radius was only a cosmetic, but the fix is free at design time and impossible afterwards.
- **Season 1 earning never closes for pass owners.** The catalog claimed paid items "stay owned"
  after 31 Oct because the entitlement is non-consumable. Not true: pass-track items unlock on
  *level*, level derives from local XP (D10), so reinstall → Restore returns `season1` but resets the
  ladder → after 31 Oct the items are permanently unreachable. v1.0 ships no rollover, expiry or
  Season 2 code, so nothing technical depended on the window closing — 31 Oct is now an *event* date
  only. **T-025 constraint: the earn path must not be date-gated.** T-009 stays post-v1.0 as the fix
  for progress durability itself, and the paywall copy should disclose device-local progress.

**Stale facts corrected:** `AGENTS.md` in four places (adapter names → `GyroTiltInput` /
`TouchTapInput` / `CameraFaceInput`; BlazePose → BlazeFace, since BlazePose missed the gate at 118 ms;
`≥ 8.4.0` → exact `9.8.1` pin; camera "parked pending OPEN_QUESTIONS 7" → ships in v1.0). Five stale
"blocked on P1" markers, incl. `BACKLOG` T-020 which read ⛔ on the same line as "Unblocked 24 Aug".
`CHECKLIST` privacy-policy ref pointed at OPEN_QUESTIONS 12 (the camera decision) instead of 9.

**Camera-in-v1.0 reached the store kit.** `LISTING.md` §4 was a "paste only when the camera update is
LIVE" delta — folded into the shipping copy: short description now `Play hands-free with the camera,
or tilt to steer. A new endless track daily.` (77/80) and the full description carries the
`HANDS-FREE CAMERA MODE (BETA)` block and the FEATURES bullet (2,828/4,000). Verified by
`check-lengths.sh`, which now also measures both §2 alternates. `SCREENSHOTS.md` Shot 5 (mode-select
menu) reversed from "hold for v1.0" to ship — it is the visual half of explaining the CAMERA
permission to a reviewer. Shot 7 likewise. `VIDEO_SCRIPT.md` contingency retired.

**Timeline recomputed — closed testing went LIVE 26 Aug**, five days ahead of the ~1 Sep plan:

| | Was | Now |
|---|---|---|
| 14 days elapse | ~15 Sep | **~9 Sep** (from the day the **12th** tester is opted in, not from upload) |
| Apply for production | ~7–8 Sep (impossible) / ~15 Sep | **~9 Sep** |
| Production live | ~22 Sep | **~16 Sep** |
| Slack before 30 Sep | ~8 days | **~10 days**, all of it after production access |

`PLAY_CONSOLE_SETUP.md` §E had "apply ~7–8 Sep", which was arithmetically impossible against a ~1 Sep
start; gone. BACKLOG T-031 is now 🚧, and the 🔷 marker (code complete, awaiting device test) was
added to the legend — it was in use on T-011/12/13 but undocumented.

**Needs the owner:** create the **3 in-app products in Play Console** — they exist in RevenueCat but
not in Play, so no real purchase can be verified and each package's Play row is empty
(`PLAY_CONSOLE_SETUP.md` §D1) — then the service-account JSON (§D3, ~36 h propagation). And **get to
12 testers opted in**: the 14-day clock starts at the 12th, not at upload, so this is now the single
uncompressible item on the critical path. Recruit 16–18.

**Not addressed (owner's call):** `VIDEO_SCRIPT.md` §0:00–0:18 still says "one continuous take" *and*
"cut in a tight insert at ~0:10" *and* "no cuts inside the lean-swerve beat" — three rules that cannot
all hold. Low stakes, but ambiguous for the shoot.

## 2026-08-25 — T-036 marketing website built; privacy policy gets a real home (veyro-site session)

**Headline: the complete marketing site exists at `site/`, ready to deploy to veyro.ferrabled.com —
which also resolves OPEN_QUESTIONS 9 (privacy-policy hosting + contact email).** Owner decisions
this session: Cloudflare free tier + Wrangler (not GitHub Pages), subdomain `veyro.ferrabled.com`,
contact email `ferrabled+veyro@gmail.com` (temporary alias, will be replaced), and the design —
"Riso Print" (two-ink risograph poster, teal #12454C + fluoro pink #EE3D87 on paper #FBF5E9),
picked from a 2-round pitch (owner explicitly wants nothing that reads AI-generated; camera mode
is the headline feature, tilt secondary). Pitch artifact:
https://claude.ai/code/artifact/c0ca2d3c-4582-4968-8592-8b2a9cfb46be

- `site/public/`: index (7 independent sections: hero / how-it-plays / camera-privacy / daily-ticket
  / cosmetics-pledge / spec-strip / about), `/privacy/` (mirrors `docs/PRIVACY_POLICY.md`, whose two
  email TODOs are now filled), `/terms/` (cosmetic-IAP + physical-safety terms), `/support/`
  (FAQ + data-deletion statement), 404, favicon, `_headers` (CSP), robots.txt. No framework, no
  build step; fonts Anybody / Atkinson Hyperlegible / Fragment Mono. Hero CTA is in pre-launch
  state (closed-test mailto); the Google Play CTA is in the HTML, commented, for launch day.
- `GameLinks.PrivacyPolicyUrl` → `https://veyro.ferrabled.com/privacy/` (was GitHub Pages).
  **The URL 404s until the first deploy — deploy before any build that ships this constant.**
- Copy compliance checked: grep for the ART_DIRECTION banned words (race/speed/drive/circuit/turbo)
  is clean across `site/`; camera copy carries the BETA label + on-device lines matching the policy.
- Verified via local static server + headless DOM checks: all pages/assets 200, unknown path 404s,
  zero console errors, no horizontal overflow at 1280px or 375px (panels stack on mobile), daily
  ticket regenerates from today's UTC date (JS), CSS/fonts load. **Not visually inspected by a
  human yet — needs the owner to look at it** (`npx serve site/public` or `npx wrangler dev`).
- **Needs human:** (1) `cd site && npx wrangler login && npx wrangler deploy` — first deploy
  auto-creates the custom domain in the ferrabled.com zone; (2) eyeball the design on desktop +
  phone; (3) when the Play listing goes live, flip the hero CTA (marked in `site/README.md`).
- Owner reviewed the site 25 Aug: approved except the capsule-person figures — "once we have the
  main model and skins, we will replicate those." Filed as **T-037** (blocked on the character
  model); the exact spots to swap are tabled under "Placeholder figures" in `site/README.md`.

## 2026-08-25 — First Play upload done; site live + compliance register created (compliance session)

**Headline: versionCode 1 is on the internal track, the marketing/legal site is live at
veyro.ferrabled.com, and store compliance now has a canonical doc.**

- Owner registered the Play account, created the upload keystore (D5 ✅), built and uploaded
  `MotionRunner.aab` (44.8 MB, target SDK 36, ARM64, jarsigner-verified) to **internal testing**.
- **Rebuild required before closed testing:** versionCode 1 has the dead GitHub Pages privacy
  URL baked in (verified in the AAB's IL2CPP metadata). Rebuild with `VEYRO_VERSION_CODE=2`
  and upload that to the closed track. Recorded as the "URL coupling" rule in STORE_COMPLIANCE.
- Site live and verified (privacy/terms/support): passes the User Data policy checklist; added
  "Your rights" + "About this website" sections (effective date 25 Aug), hedged the support
  purchases FAQ ("coming in a future update"); owner redeployed. `site/.wrangler/` gitignored
  (held the Cloudflare account id). Terms already said "may offer" purchases — left as-is.
- Copilot PR review triaged: kept `APP_UI_EDITOR_ONLY` (it *strips* App UI runtime from player
  builds — asmdef constraint `UNITY_EDITOR || !APP_UI_EDITOR_ONLY`; removing it would bloat the
  APK), reverted the dangling `com.unity.dt.app-ui` EditorBuildSettings entry (GUID exists
  nowhere), documented `AndroidKeystoreName: '{inproject}: '` as the deliberate post-build wipe
  and made only bundle builds touch keystore fields.
- **`docs/STORE_COMPLIANCE.md` created** (owner request): current Console declaration state +
  per-release flip table (T-020 purchases, T-021 push/AD_ID check, Layers, T-009 login/
  leaderboard incl. the account-deletion policy trap) + standing rules. Linked from CLAUDE.md
  canonical docs. Store-listing text drafted in `docs/store-kit/listing.md`.
- Console App content answers agreed (all recorded in STORE_COMPLIANCE): App access "No",
  Data safety "no collection", content rating all-No → PEGI 3, target audience 13+, Arcade.

**Needs a human:** rebuild + upload versionCode 2 to closed testing; finish Console items
(content rating, target audience, Data safety, category/contact, listing); **store graphics
missing** (512×512 icon, 1024×500 feature graphic, ≥2 screenshots — specs in
docs/store-kit/listing.md); recruit 12 testers (H6); commit + merge this branch (PR open).
## 2026-08-24 (merge) — `main` reconciled into `revenueCat-prep`: AAB path + privacy policy + camera-in-v1.0 meet the RevenueCat prep

Merging `main` (camera-feature + the compliance session below) changed facts the RevenueCat-side
docs of this branch assumed; reconciled in this merge, not just spliced:

- **The AAB path already exists** (`BuildScript.BuildAndroidBundle`, keystore runbook
  PREREQUISITES D5) → `PLAY_CONSOLE_SETUP.md` §B updated (the T-020 session only adds the SDK, not
  the build path; item 8 of the T-020 handoff is mostly done already).
- **Camera mode ships in v1.0** (owner call; record in DECISIONS = OPEN_QUESTIONS **12**) →
  PLAY_CONSOLE_SETUP §A updated: Data safety declares camera on-device-only from build one; the
  listing description must mention camera mode (T-030).
- **The privacy policy is drafted** (`docs/PRIVACY_POLICY.md`, linked in-app via `GameLinks`) —
  remaining is hosting + contact email, merged into OPEN_QUESTIONS **9** (was 12 on this branch;
  judge mode keeps 10).
- **T-031 sequencing improved:** an AAB exists *now*, so the 12×14 clock starts with the current
  build; the RevenueCat build follows as a track update — which is also what unlocks Play in-app
  product creation. Backlog T-030/T-031 rewritten as the union of both branches.

## 2026-08-24 (later) — RevenueCat dashboard COMPLETE for both apps; Play Console checklist written; Unity handoff issued (owner dashboard session)

- Owner finished the RevenueCat side: entitlements, Test Store *and* Play Store products, offering
  `default`, paywall — the dashboard half of T-020 is done. What remains dashboard-side is only
  §D of the new checklist (Play products, service credentials, attach).
- **`docs/PLAY_CONSOLE_SETUP.md` written** (claims verified against live Google/RevenueCat docs
  today): ordered owner checklist — §A (payments profile, app creation, listing + declarations +
  privacy policy, 12 testers) can all happen **now, before any build**; §B is the agent-built
  signed AAB; §C uploads (internal track to unlock product creation instantly, closed track for
  the 12×14 clock); §D products + service-account JSON (~36 h propagation) + attach in RevenueCat.
- Doc sync: PREREQUISITES P1 ✅ / P4 mostly-✅ / P10 urgent; OPEN_QUESTIONS 9 moved to Answered
  (catalog is owner-confirmed; ids now committed in the RevenueCat dashboard); the privacy-policy
  question (now OPEN_QUESTIONS 9) marked urgent — it gates the clock; T-030 asset deadline moved up (rough
  512×512 icon + 1024×500 feature graphic + 2 screenshots gate the track publish); T-031 points at
  the checklist; CLAUDE.md canonical-docs list gained COSMETICS_CATALOG + PLAY_CONSOLE_SETUP.
- **A copy-pasteable T-020 Unity-session handoff was given to the owner in chat** (scope: SDK
  9.8.1 via OpenUPM, APK diff, Commerce asmdefs + tests, RevenueCatStore, store UI + Restore,
  entitlement-applied skins, resume re-read, product/company name fix, AAB build path, Test-Store
  device test). T-025 explicitly out of its scope.

**Needs the owner:** run PLAY_CONSOLE_SETUP §A today (privacy policy first — it gates everything);
recruit the 12 testers; create the upload keystore when the agent posts the command; paste the two
RevenueCat public API keys into the Unity session handoff.

## 2026-08-24 — P1 cleared; RevenueCat project live with Test Store; T-020 Unity half unblocked (owner dashboard session)

- **Owner reports the Play developer account identity verification passed** → P1 no longer blocks.
  The 12-testers × 14-days clock is now the schedule's critical path: it only runs while a closed
  test is live, so T-031 (first closed-testing upload — private, invite-only) should happen as soon
  as a build exists, with polish landing as track updates. Backlog T-031 updated with the three
  ordering traps (AAB required; billing-enabled build must be uploaded before Play products can be
  created; app-content declarations incl. privacy policy needed to publish the track).
- **RevenueCat project "Veyro Run" exists** with the Play Store app + a **Test Store** app. Owner
  created a test product (`test.season1.pass`, wired to an entitlement), an offering with a
  `$rc_lifetime` package, and a paywall. Test Store means the whole Unity integration can be
  device-tested (purchase → entitlement → unlock, paywall render) before Play credentials exist —
  T-020's Unity half is unblocked today; only real-purchase verification still waits on the Play
  side. Convention recorded in `COSMETICS_CATALOG.md` §6 (`test.*` mirror products, same
  entitlements, same packages).
- **Dashboard fix-ups flagged to the owner:** the offering was created with identifier
  `default.current` (immutable; looks like a misreading of "mark it as current") — recreate as
  `default`, mark Current, re-attach the paywall, delete the odd one, while nothing depends on it.
  Also still missing: the two skin entitlements/test products/packages.

**Needs the owner:** offering fix; add `skin_ember`/`skin_frost` entitlements + test products +
packages; then the Play Console sequence (app creation, payments profile, privacy-policy URL,
upload keystore, 12 testers).

**Next:** Unity session for T-020 (SDK 9.8.1 + wrapper + paywall call, tested via Test Store) and
first AAB → closed testing (T-031). T-025/T-006/T-007 are the "improve the app" work that lands as
track updates without touching the clock.

## 2026-08-24 — Play submission prep: AAB build path, upload signing, privacy policy (compliance session)

**Headline: the repo can now produce a Play-uploadable artifact, and the policy paperwork the
Console will demand is drafted.** Owner decided in-session: **closed testing ships the
`camera-feature` branch** (camera in v1.0 — amends D2; recorded as OPEN_QUESTIONS 12 after the
merge renumbering, for the owner to move into DECISIONS.md). Researched against the live Play
policies (24 Aug 2026):

- **`BuildScript.BuildAndroidBundle`** (new): `builds/MotionRunner.aab` — Play only accepts App
  Bundles for new apps. Signs with the upload keystore from `VEYRO_KEYSTORE*` env vars (creation
  runbook: PREREQUISITES **D5**, human-only), optional `VEYRO_VERSION_CODE` override; keystore
  settings wiped in `finally` so no local path/secret lands in ProjectSettings.asset.
- **Target SDK pinned to 36** in `Build()` — Play's floor for new apps from **31 Aug 2026** (was
  "highest installed", which happened to resolve to 36; now it can't drift).
- **Privacy policy**: drafted at `docs/PRIVACY_POLICY.md` (truthful for current state: zero
  collection, camera on-device-only, no network calls). Required because CAMERA is a *sensitive
  permission*. Linked in-app (Play requires in-app reachability): new `GameLinks.cs` +
  "privacy policy" link on `ModeSelectMenu`. Hosting + contact email = **OPEN_QUESTIONS 9**.
- **T-030/T-031 acceptance criteria extended** with the Console App content forms (Data safety
  "no collection" — must change with T-020/T-021; content rating; target audience **13+**; ads
  "no"), and T-031 now says AAB, not APK.
- Checked and fine as-is: camera `uses-feature` entries are all `required=false` in the built APK
  (aapt2 on `VeyroCamDev.apk`) so camera-less devices keep store access; runtime permission is
  requested in-context; the three Console declarations (program policies / Play App Signing ToS /
  US export laws) are all safe to accept — no proprietary crypto, engine-standard TLS only.

**Verified:** EditMode suite in a fresh batchmode scratch copy (owner's editor held the repo
project): **140/140 passed**, sync of edited files into the scratch cmp-verified first. Robocopy
note for the gotcha file: exclude `game/Temp` (`/XD`) — the open editor's `UnityLockfile` lives
there and, if copied, makes the scratch look locked too.

**Needs a human (all gate T-031):** P1 Play account (register + verify — the 12×14 closed-test
clock can't start before it; **done per the entries above, owner-reported same day**), D5 upload
keystore, OPEN_QUESTIONS 9 (privacy hosting + contact email — fill the two `[TODO]`s in
`docs/PRIVACY_POLICY.md`), OPEN_QUESTIONS 12 (owner moves the camera-in-v1.0 decision into
DECISIONS.md). First Console upload: accept Play App Signing ToS, let Google generate the app
signing key, upload `builds/MotionRunner.aab`.

## 2026-08-23 — Catalog redesigned with the owner: 2 skins + Season 1 pass; dashboard setup unblocked (owner dashboard session)

Owner direction during RevenueCat dashboard setup: the two-SKU colour-pack catalog is replaced by
**2 premium skins sold individually + a Season 1 pass (free + paid cosmetic reward tracks, XP from
run score)**. Still cosmetics-only — D6 unchanged; retention was the gap the pass fills. Written up
in **`docs/COSMETICS_CATALOG.md`**: product/entitlement/offering tables (3 products, 3 entitlements,
all NON-CONSUMABLE), the 10-level Season 1 ladder, item feasibility notes (everything composes from
`RuntimeMaterials` + TrailRenderer + ParticleSystem + primitives — no new art tech), and the owner
sign-off checklist (§7). OPEN_QUESTIONS 9 rewritten to point there; REVENUECAT_PLAN §4 marked
superseded (rest of the plan stands); T-020 backlog line updated; **T-025 added** (XP + pass +
locker — pure C#/UGUI, deliberately *not* blocked on P1, the right work while Play Console approval
is pending). Also fixed an accidental paste that had corrupted source [S22] in REVENUECAT_PLAN.md.

Verified: docs only, no Unity, no build. Cross-references checked by hand.

**Needs the owner:** tick `COSMETICS_CATALOG.md` §7 (ids/prices/names are the one-way doors — ids
must match Play Console character-for-character later); then the RevenueCat dashboard can be filled
today without the Play Console (project, Play Store app by package name, API key, 3 entitlements,
3 products *manually by id, non-consumable*, offering `default`, paywall). Service-account JSON,
Play-side products, license testers and promo codes still wait on P1.

**Next:** T-025 can start immediately in Unity; T-020 the day P1 + P4 exist.

## 2026-08-23 — T-020 prep: RevenueCat integration plan written, ready to execute in one session (no-unity-prep session)

No Unity opened, no code written, no build made — by instruction. Produced
**`docs/REVENUECAT_PLAN.md`**: version pin + rationale, install method, Unity/Android gotchas, the
wrapper design, the catalog proposal, Paywall-Builder server-vs-app split, the judge path, and an
ordered checklist. Every external claim carries a link, checked live today.

**The three findings that change the plan:**

1. **Pin `9.8.1`, not `≥ 8.4.0`.** 9.8.1 shipped **20 Aug 2026** — three days ago. 8.4.x is four
   minors behind. 9.x brings Play Billing 8.3.0 (vs 8.0.0), min SDK 23 (ours is 26), multipage
   paywalls, and the ad primitives T-023 needed to evaluate. Pin exactly — RevenueCat ships ~weekly
   and an unpinned bump between the closed-testing build and the production build would move the
   store's Billing dependency underneath us. Backlog AC updated.
2. **One dashboard toggle can permanently break T-020's acceptance criterion.** From SDK 9.0.0
   RevenueCat removed the workaround that allowed restoring consumed one-time products: a one-time
   product misconfigured as **consumable** is consumed and can never be restored. We have no login
   system and our AC is literally *"entitlement survives reinstall"*. **Every SKU must be
   non-consumable**, set before the first purchase.
3. **The Paywall Builder is the best schedule lever in the project.** The entire paywall — layout,
   copy, colours, packages — is server-configured; the app contributes one `PaywallsPresenter`
   call. So the paywall can be redesigned throughout the judging window with no store review. Get
   the *call* into v1.0; do not spend September polishing pixels in Unity.

**Wrapper seam** (CLAUDE.md rule 5), mirroring the existing `MotionRunner.Track`/`.Pose` shape:
`MotionRunner.Commerce` (engine-free, `noEngineReferences: true`) holds `IStore`, `Entitlements`,
`CosmeticCatalog` and a `FakeStore`; `MotionRunner.Commerce.RevenueCat` holds the adapter and is the
only assembly that sees the SDK. The SDK ships its own asmdefs, so — unlike `CameraFaceInput` — the
adapter does **not** have to live in Assembly-CSharp. EditMode tests reference only the engine-free
assembly, so `-runTests` keeps working with or without the package (and the SDK cannot run in the
Editor at all, so this is a requirement, not a nicety).

**Owner must review/decide:** OPEN_QUESTIONS **9** (SKU ids, entitlement name, prices — product ids
are immutable once created) and **10** (ship a hidden judge-mode login as a promo-code fallback;
recommended yes).

**Two things found while reading the repo, both cheap, both embarrassing if missed:** Player Settings
still say `productName: Motion Runner` / `companyName: DefaultCompany` (that string is the launcher
label), and adding the SDK will pull Play Billing + Jetpack Compose into the APK — per gotcha 10 the
release APK must be size- and permission-diffed immediately after the package lands, before any
paywall work, so the UGUI fallback is still an option if Compose blows the budget.

**Blocked on:** P1 (Play Console — requested, pending) and P4. Nothing else.

## 2026-08-23 — T-023 RevenueCat Ads spike: **works from Unity, still a NO for v1.0** (no-unity-prep session)

Desk research only, no integration, ~2 h of a 1-day timebox. Verdict as required by the AC:

**Technically YES.** Unity is a supported platform for RevenueCat ad monetization —
`purchases-unity` **9.1.0+** for the `AdTracker` API (the docs carry a Unity C# sample), and
**9.8.0+** for reward verification. Confirmed in the source, not just the docs:
`GenerateRewardVerificationToken` and `PollRewardVerification` are public on `Purchases` at
`RevenueCat/Scripts/Purchases.cs:729` and `:746` @ 9.8.1.

**Strategically NO for v1.0**, four reasons:

1. **RevenueCat Ads does not serve ads.** It is impression/revenue *tracking* that sits alongside an
   existing ad stack via ILRD callbacks — "we aren't replacing your mediation platform". Shipping it
   means first shipping a real ad SDK: AdMob, AppLovin MAX, ironSource/LevelPlay or Unity Ads.
2. **Rewards need AdMob SSV, and Unity has no adapter.** Server-verified rewards are verified
   through AdMob Server-Side Verification. RevenueCat's `loadAndTrack` helper exists for Swift and
   Kotlin only — the AdMob integration page has no Unity samples. Unity is the manual path: Google
   Mobile Ads Unity plugin + hand-plumbed `AdTracker` calls + reward tokens threaded through AdMob
   SSV custom data.
3. **Cost against the calendar.** An AdMob/AdSense account (approval takes days), a second ad SDK in
   the APK, an advertising-ID permission, a Data-safety redeclaration, and a UMP consent flow — all
   before ~12 Sep, with the Play Console not yet in existence. The feature is also in **beta** (ad
   revenue does not count toward MTR yet).
4. **It would read as box-checking.** The category rewards "RevenueCat Ads as a monetization
   method"; screeners explicitly penalise category overreach. One rewarded revive bolted on in the
   final week is the canonical example.

**Consequence:** Catvertising is **not** entered (D7 already said "only if the spike passes").
Removed from the target list in `docs/submission/DEVPOST_ANSWERS.md`. The v1.0 listing declares
**no ads**, which is also a positioning asset — "no ads interrupting a run" is in the store copy.

**If it is ever revisited (Update 2, only if T-020 shipped and production is live by ~20 Sep):**
one rewarded placement, on the result screen, offering a single revive per run — never mid-run, never
auto-playing. Behind `IStore`-style seam + a remote feature flag, defaulting off. A revive is the one
ad in a runner that players ask for rather than tolerate; it must never be purchasable, or the
cosmetics-only promise (D6) breaks and the Daily Run stops being comparable between players.

Evidence: RevenueCat ad monetization overview and rewards docs
(https://www.revenuecat.com/docs/ad-monetization, /rewards, /manual-integration, /admob), Unity SDK
9.1.0 and 9.8.0 changelog entries, and `Purchases.cs` @ 9.8.1.

## 2026-08-23 — T-030 store listing kit: copy, shot list, art brief, tester pack (no-unity-prep session)

Copy only, no binary assets — by instruction. New `docs/store-kit/`:

- **`LISTING.md`** — app title, short and full descriptions, plus alternates, Console field values
  (category, content rating, ads=no, target audience) and a camera-mode delta block to paste **only**
  once that update is live. Written around "motion-controlled endless runner" search terms.
- **`check-lengths.sh`** — asserts the copy against Google's 30/80/4000 limits. Currently
  24 / 78 / 2077. Run it before pasting anything into the Console.
- **`SCREENSHOTS.md`** — 7-shot list with the exact game state per shot, the capture commands
  (including the `MSYS_NO_PATHCONV=1` trap), and the arithmetic for the Devpost **1179 × 2556
  frameless** shot: from 1080 × 2400, scale to width 1179 → height 2620, crop 64 px off the *top*.
- **`ART_DIRECTION.md`** — icon (512/1024) and feature-graphic (1024 × 500) brief, three icon
  concepts, the in-game palette as hex (`#FF8C26` runner orange, `#171A29` background — taken from
  the code so the icon and the first screenshot match for free), and three style frames for
  OPEN_QUESTIONS 4. Leads with the non-automotive constraint from the VEYRON note, including the
  subtle version: the three-lane strip must read as a *path*, not a road.
- **`CLOSED_TESTING.md`** — the 12×14 rule re-verified, dates worked backwards from 30 Sep,
  recruitment message, opt-in instructions written for someone who has never done it, and the five
  questions to ask testers. **Recruit 16–18, not 12**, and upload before the kit is finished — the
  clock is the scarce resource, not polish.

**Two overpromises caught by checking the copy against the code, both removed:**

- **Free Run is not reachable.** `RunMode.Free` exists and `RunHud` would render "FREE RUN", but
  `RunSession.Mode` is hard-coded to `Daily` (`RunSession.cs:33`) and nothing sets it. Worth a few
  lines in the T-020 session to expose; until then it is out of the listing.
- **Sliding does not exist.** `IsSlidePressed()` is plumbed through `CompositeInput`,
  `TouchTapInput`, `KeyboardInput` and `FaceSteering` — and `RunnerController.Step()` reads only
  `GetMoveAxis()` and `IsJumpPressed()`, so the input arrives and is dropped. Both "crouch to slide"
  claims are gone from the listing and the camera copy.

**Owner must review** all copy before it is pasted anywhere. **Still open:** the binary assets (icon,
feature graphic, captures) need a device and a designer. **New blocker surfaced:** Play requires a
hosted **privacy-policy URL** even for an app that collects nothing → OPEN_QUESTIONS 9 (renumbered
in the 24 Aug merge).

## 2026-08-23 — T-035 submission prep: video script, Devpost answers, artefact checklist (no-unity-prep session)

New `docs/submission/`:

- **`VIDEO_SCRIPT.md`** — timecoded to 1:50 against a hard 2:00 cap, opening 0:00–0:18 on the
  hands-free shot in **one continuous take** (a cut in the lean-swerve beat reads as a fake, and this
  is the only 18 seconds a screener is guaranteed to watch). Includes a contingency if camera mode
  misses production: open on the tilt shot and demote the camera footage to a labelled BETA segment,
  rather than headlining an unreleased feature.
- **`DEVPOST_ANSWERS.md`** — a category audit first (four solid entries; five conditional on SDKs
  that are not integrated yet; Catvertising dropped per T-023), then the description draft. It leads
  with the CV failure and the pivot, because that is the most interesting true thing about the
  project and every number in it is traceable to this journal. Placeholders are marked `[N]`.
- **`CHECKLIST.md`** — every artefact the form needs, with producer, status and blocker, plus a
  dated timeline. **Slack in the whole chain is about 8 days, all of it in Play's production
  review.**

**Rule found in the rules text and worth flagging loudly:** *"A Project may have existed before the
Submission Period, but it must not have been publicly released on any eligible store before the
Submission Period. Updates to previously released apps are not eligible."* We are clear — Veyro Run
has never been released — but this is why nothing gets pushed to **production** to "test the
pipeline". Closed testing is not a public release; a production rollout is, and it would consume the
one first-release event the submission depends on.

**Blocked on P1** for everything downstream (store URL, promo codes, real numbers).

## 2026-08-23 — T-040: first three #BuildInPublic posts drafted (no-unity-prep session)

`docs/posts/2026-08-camera-mode.md` — three posts, X and LinkedIn variants each, covering the CV
pivot, the hands-free demo, and the input-seam engineering story. **Nothing posted; nothing will be
by an agent.**

Every number is traced to a STATUS entry in a table at the bottom of the file, along with an explicit
do-not-claim-yet list (monetization, install numbers, leaderboards, sliding, Free Run). One
discrepancy resolved there so it never leaks into two different posts: the 22 Aug feasibility sweep
measured **4.2 ms**, the shipping in-game gate reads **3.7 ms** — posts use 3.7 and say what it is.

The lead post is the failure: BlazePose at 118 ms against a 30 ms gate, the surprise that CPU beat
GPUCompute by 2.5×, and the pivot to a 3.7 ms face detector — "a much worse model of a human and a
much better input device". The negative result is the strongest content this project has.

**Blocked on P9** — the social account does not exist yet. That is minutes of work and it is the
actual constraint on a category worth $30k that costs no engineering time.

## 2026-08-23 — T-011/T-012/T-013: camera mode is in the game, on the `camera-feature` branch (camera-feature session)

**Headline: the game now boots to a mode menu, and camera mode drives it end-to-end on the
device.** Owner chose OPEN_QUESTIONS 7(d) on 22 Aug; this implements it. On the Nord 2 the full
chain ran unattended: speed gate **3.7 ms blocking median → PASS**, camera permission, orientation
probe settled at score 0.89 (again overruling the device's wrong rotation report, 270 → 90+flip),
face acquired, run started hands-free. `main` still holds v1.0 untouched; this branch's APK
installs as **`com.ferrabled.veyro.camdev`** ("Veyro Cam Dev", `builds/VeyroCamDev.apk`, 44.8 MB)
so it can never replace the working game on a phone.

**How it fits together** (all new code except the adapter lives outside Assembly-CSharp):
- `MotionRunner.Pose.FaceSteering` (engine-free, clock-free): face position → LEFT/RIGHT (neutral
  band + dead zone + low-pass), JUMP (upward velocity + refractory), SLIDE (sustained crouch +
  hold time), loss decay, slow neutral drift. 13 EditMode tests drive synthetic 30 Hz
  trajectories; writing them caught a real design bug — standing up from a crouch is a jump's
  exact velocity signature, so ending a slide now arms the jump refractory.
- `MotionRunner.CameraInput` (new ungated asmdef): `CameraFeed` + `BlazeAffine` moved here from
  the spike (spike asmdef now references this one), plus `FaceAnchors` (the 896 BlazeFace anchors
  generated in code), `FaceDetector` (BlazeFace short-range, CPU backend, argmax folded into the
  graph, startup self-check on the box tensor width), `FaceTrackingRig` (state machine:
  gate → permission → camera → probe → track; the gate runs *before* the permission ask so a
  too-slow device never gets prompted).
- `CameraFaceInput : IGameInput` (in Assembly-CSharp with the other adapters — asmdefs cannot
  reference Assembly-CSharp, so the adapter lives beside GyroTiltInput). Stale observations
  (>0.35 s) read as loss.
- `ModeSelectMenu` (code-first UGUI): TILT & TOUCH vs CAMERA (BETA), rig states narrated
  on-screen, every camera failure falls back to the menu with the reason. Camera mode keeps
  touch+keyboard in the composite as backup. Last choice remembered (`veyro.input_mode`), menu
  shown every launch.
- **No downloads, ever**: the 418 KB BlazeFace ONNX is committed at
  `Assets/Scripts/CameraInput/Resources/CameraInput/` and ships in the APK. `docs/cv-spike.sh off`
  no longer removes `com.unity.ai.inference` — on this branch it is a shipping dependency
  (committed in the manifest; the APK carries its ~8.8 MB and CAMERA by design).
- `BuildScript.BuildAndroidCamDev` builds the dev-flavoured APK; `BuildAndroid` is untouched and
  still produces the real package name for the eventual post-release update (D2).

**Verified:** 140 EditMode tests green (127 + 13 new). On device: menu renders; TILT & TOUCH
starts a normal run (screenshot); CAMERA (BETA) ran the full pipeline unattended — `[CAM]` logcat
lines show gate PASS at 3.7 ms, orientation settled, and the run started from a face sighting.
No runtime exceptions; memory 332 MB PSS with camera+inference live (game alone was ~231 MB).
One build-loop trap hit: launching an APK build seconds after a `-runTests` run on the same
scratch copy deadlocks Unity ("More than one copy of bee_backend running" → build waits forever
at Compiling Scripts). Leave a gap or check for lingering `bee_backend` before batchmode builds.

**Needs a human (the fun part)**
- **Play it.** `builds/VeyroCamDev.apk` is installed. Prop the phone up, pick CAMERA (BETA), step
  back 1.5–2.5 m: lean to steer, hop to jump, crouch to slide. The verdict this needs: does
  steering feel 1:1, do jumps fire when you hop (and only then), does the short-range model still
  see you at play distance in your room's light?
- Tuning knobs live in `FaceSteering` (all public fields, defaults are first guesses):
  `HalfRangeX` (lean sensitivity), `JumpVelocity`, `SlideDrop`. Report what feels wrong.
- T-012's false-positive AC (<1/min standing still) needs a few timed minutes in frame.

**Next:** owner playtest → tune → then T-020 RevenueCat (unstarted, still the critical path;
Play Console account requested and pending). Do not merge to `main` until v1.0 has shipped —
this branch deliberately trades the clean 29.6 MB/INTERNET-only baseline for camera mode.

## 2026-08-22 — T-010b feasibility sweep: **the 30 ms gate is beatable — 4.2 ms, but not with BlazePose** (track-b/cv-feasibility)

**Headline: YES, camera control clears 30 ms on the Nord 2 — by switching models, not backends.**
BlazeFace short-range (418 KB, HF `unity/inference-engine-blaze-face`, Apache-2.0) on the **CPU
(Burst) backend with a blocking readback medians 4.2 ms** isolated (p95 8.3), and **9.1 ms inside a
live 60 FPS loop at 59.4 Hz with 0 of 359 frames over 20 ms**. Face centre-x is LEFT/RIGHT, face-y
velocity is JUMP, sustained face-y drop is SLIDE — all three signals the game needs, from one model
36x smaller than the pose detector. Measured on-device over three runs with the same harness as
T-010 (`CvFeasibilitySweep`, logged as `[CV] FEAS` lines; T-010's own benchmark re-ran alongside as
the control and reproduced within run-to-run variance).

**The one table that matters** ("isolated" = uncapped, like T-010's table; "live" = running
continuously while the app renders under a 60 FPS cap, empty scene + camera feed):

| model | backend | readback | isolated med | live: inference / rate / frames >20 ms |
|---|---|---|---|---|
| **face** | **CPU** | **blocking** | **4.2 ms** (p95 8.3) | **9.1 ms / 59.4 Hz / 0/359** |
| face | CPU | async await | 33.6 ms (raw 8.4) | 17.0 ms / 50.4 Hz / 0/360, main thread 1.1 ms |
| face | GPUCompute | async | 100.8 ms (raw 23.5) | 66.9 ms / 15.7 Hz / 3/357 |
| face | GPUPixel | async | 58.3 ms (raw 71.9) | — |
| lite landmarker | CPU | blocking | 31.0 ms (p95 45.6) | — |
| lite landmarker | CPU | async | 65.7–67.3 ms | 50.7 ms / **18.3 Hz** / 3/356 |
| lite landmarker | GPUCompute | async | 184–211 ms | 150.4 ms / 6.8 Hz / **43/165** |
| lite landmarker | GPUCompute, ScheduleIterable | async | — | 151.9 ms / 6.4 Hz / 87/240 — slicing does not help |
| no-ML motion centroid 160×120 | CPU | — | 0.05 ms (+1.4 ms sync GPU readback) | — |

**Why T-010's numbers overstated the cost:** an awaited `ReadbackAndCloneAsync` resumes on a frame
tick, so async latency is *quantized to multiples of ~16.7 ms*. T-010's "67.9 ms lite on CPU" was
~43 ms of Burst work plus readback rounding; face-on-CPU reads 33.6 ms async but 4.2 ms blocking.
**T-013's startup gate must measure the blocking CPU cost, not awaited latency**, or it will fail
devices that are actually fine. (This also closes T-010's 118-vs-170 ms puzzle: GPU numbers swing
~±20% run to run with queue warmth and DVFS — the end-of-sweep repeat read 190 vs 210 at start,
same harness. Nothing was wrong with either measurement; the GPU path is just dead anyway.)

**Hypotheses killed, with evidence** (each cost minutes on device; none need revisiting):
- **Quantization (H2): broken in Inference Engine 2.6.1 for these models.** Every attempt threw —
  face+u8 `NullReferenceException`, lite+fp16/u8 `KeyNotFoundException '362'`. The docs also state
  quantization reduces size, not inference time, so this lever was doubly dead.
- **GPUPixel (H3): no rescue.** face 58.3 ms, lite 137.9 ms (raw 461 — worse than its own latency).
- **GPU anything, live (H4): unplayable.** GPUCompute inference alongside rendering hitches badly
  (frame p95 100.8 ms, 43/165 frames over 20 ms) and `ScheduleIterable` slicing makes it *worse*
  (87/240) — dispatch is spread but the GPU is still saturated.
- **The pose detector must never run on the CPU backend.** Creating that worker OOM-killed the
  whole process twice (`ApplicationExitInfo reason=3 LOW_MEMORY` at `importance=100` — foreground —
  with 5 GB free, no crash log, signal 9). A gate/adapter that ever tries it takes the game down
  with it. The face-based scheme doesn't need the detector at all.

**The three answers for OPEN_QUESTIONS 7:**
1. *Is there a configuration that clears 30 ms?* **Yes: BlazeFace short-range, CPU backend.**
   4.2 ms blocking / 17 ms async, measured by `CvFeasibilitySweep` on the device, three runs.
2. *Cheapest usable LEFT/RIGHT/JUMP, at what Hz, leaving 60 FPS?* Same configuration: 30–60 Hz with
   the game's frame rate intact (0 frames over 20 ms in 6 s soaks; async mode costs the main thread
   1.1 ms per inference). Full-pose (lite landmarker, CPU, async) coexists with 60 FPS at 18.3 Hz —
   fine for a skeleton *overlay demo*, still 50 ms latency as an *input*.
3. *Park / invest / drop?* **A fourth option now exists and is the recommendation: build camera
   mode on BlazeFace-on-CPU** (updated in OPEN_QUESTIONS 7). Path B (MediaPipe plugin, H7) is no
   longer needed for performance and was not tested. No-ML optical flow (H6) is nearly free
   (1.5 ms) but unnecessary given the above; signal quality untested.

**Not tested, and why:** H7 MediaPipeUnityPlugin (obsolete — the need it addressed is gone);
BlazeFace *detection reliability at 1.5–3 m play distance* — the short-range variant is trained for
arm's-length faces, and **this is the one open risk in the recommendation**; it needs a person in
frame (below). The full-range BlazeFace variant is not in Unity's HF repo and would need ONNX
conversion — only worth it if the range test fails.

**Changed** (spike-side only; the release surface is verified below):
- `Assets/CV/CvFeasibilitySweep.cs` (new): the H1/H2/H3/H4/H6 harness, kept for re-running on other
  devices. Wired into `CvSpikeController` after the T-010 benchmark.
- `CvSpikeBuild` stages `blaze_face_short_range.onnx` (gitignored in `game/CvModels/`), and
  `docs/cv-spike.sh on` now fetches it alongside the pose models, so the sweep is reproducible.
- Main `Packages/manifest.json` was never touched: the inference package was enabled only in the
  batchmode scratch copy. The committed tree stays in the "spike off" state throughout.

**Correction to T-010's "release untouched" claim, found by the verification diff:**
`Assets/CV/Resources/Cv/` (81 KB anchors.csv + the affine compute shader) was shipping in every
release APK — Unity includes all `Resources/` content regardless of the asmdef gate. Fixed by
moving them to `Assets/CV/Data/` and having `CvSpikeBuild` stage them per spike build exactly like
the models (unstaged in the same `finally`); `.gitignore` now blocks the whole staged tree.

**Verified:** release APK rebuilt from this tree (clean scratch): permissions INTERNET-only,
launcher activity `UnityPlayerGameActivity`, 31,027,111 bytes vs the 21 Aug baseline's 31,007,023 —
the **only** delta is `MotionRunner.Pose.dll` (+20 KB, the ungated engine-free Pose assembly, which
is intended to ship with T-011+; gate it too if zero footprint matters before then). A control
rebuild of the pure 21 Aug tree came out byte-identical to the baseline, so the toolchain itself is
stable. Three instrumented device runs; runs 1–2 died to the detector-on-CPU OOM (that's how it was
found), run 3 completed the full sweep in 53 s with logcat streamed to disk. The verification also
caught a polluted scratch copy masquerading as a tree change (a leftover
`Assets/Plugins/Android/AndroidManifest.xml` flipping the launcher activity and adding VIBRATE, and
a `SENTIS_ANALYTICS_ENABLED` define the inference package wrote into ProjectSettings that outlived
its removal) — now CLAUDE.md gotcha 11.

**Needs a human**
- **OPEN_QUESTIONS 7, updated in place** — the options changed materially in camera mode's favour.
- **The range test (10 min, needs a body):** install `builds/VeyroCvSpike.apk`, stand 1.5–3 m from
  the front camera in game-typical light, and check the detector score stays high — the spike's
  overlay currently tracks *pose*, so treat this as approximate until a face overlay exists
  (~half a day, only worth it if OPEN_QUESTIONS 7 resolves to "build it").
- Sanity-check the SLIDE mapping (sustained face-y drop = crouch) feels acceptable before T-012
  encodes it.

**Next:** nothing in Track B until OPEN_QUESTIONS 7 is answered. T-020 RevenueCat remains the
critical path and is untouched by all of this.

## 2026-08-22 — T-010 done: BlazePose runs on device, and **misses the 30 ms gate by 4x** (track-b/cv-spike)

**Headline: NO.** On the OnePlus Nord 2 (Dimensity 1200, Mali-G77 MC9, Vulkan), the BlazePose
**lite landmarker medians 118 ms** per inference — the T-013 gate is 30 ms. The detector medians
217 ms, so a full detect+track cycle is ~334 ms (**~3 Hz**). Camera mode as a real-time input is
not viable on this hardware over Path A. Measured over 57 warm inferences; warm-up discarded.

AC met: 33 landmarks drawn over the live front-camera feed, tracking a person, verified by
screenshot. `[CV] STATS` / `[CV] VERDICT` lines are in logcat under tag `Unity`.

**Isolated benchmark** (no body needed — this is the same measurement T-013's startup gate will
run). "latency" = schedule → await readback, one at a time, which is what a frame pays. "raw" =
20 scheduled back to back with one readback, which is the GPU cost with await and frame-boundary
overhead removed. Frame rate uncapped during the sweep, or every number would round up to a
multiple of 16.7 ms:

| model | backend | latency median | p95 | raw per inference |
|---|---|---|---|---|
| pose detector | GPUCompute | 183.8 ms | 187.0 | 145.7 |
| landmarker lite | GPUCompute | 169.9 ms | 205.7 | 110.9 |
| landmarker full | GPUCompute | 233.9 ms | 253.2 | 123.2 |
| landmarker lite | **CPU (Burst)** | **67.9 ms** | 93.5 | **43.6** |

Two things fall out of that table, and both matter more than the headline number:

1. **Raw ≈ latency.** Removing per-await overhead saves ~35 %, not 4x. The model really is this
   expensive here; no amount of pipelining or frame-skipping rescues it.
2. **The CPU backend is 2.5x faster than GPUCompute** on the same model. Inference Engine's generic
   compute shaders are badly suited to this Mali GPU — exactly the risk handoff 8.2 flagged. Worth
   knowing before anyone spends a day optimising the GPU path. Even so, CPU at 43.6 ms raw is still
   above 30 ms, so switching backend does not by itself clear the gate.

**Changed** (nothing in Track A, C or D; deleting `Assets/CV`, `Assets/Scripts/Pose` and
`Assets/Editor/CvSpikeBuild.cs` would leave the game exactly as it was):
- New engine-free assembly **`MotionRunner.Pose`** (`noEngineReferences: true`, same pattern as
  `MotionRunner.Track`): `PoseJoint`/`PoseLandmark`/`PoseFrame`, `PoseSkeleton`, `PoseGeometry`
  (torso centre, shoulder width — T-012's seam), `LatencyStats` (median/p95 + the `<30 ms` gate),
  `PoseAffine2x3`/`FrameOrientation`, `OrientationProbe`. Clock-free and injected-input, like
  `DailySeed`, so T-012 can state a false-positive rate and measure it headlessly.
- New Unity-side `MotionRunner.Cv` (`Assets/CV/`): `BlazeAffine` (ported from Unity's Apache-2.0
  `BlazeDetectionSample/Pose`), `BlazePoseRunner`, `CameraFeed`, `PoseOverlay` (UGUI), `CvBenchmark`,
  `CvSpikeBootstrap`/`CvSpikeController`.
- `com.unity.ai.inference` **2.6.1** (sample targets 2.4.0; no API drift, and 2.6.0 fixes a
  GPU-compute convolution-padding crash worth having). **Not left in the manifest** —
  `docs/cv-spike.sh on|off` toggles it, for the reason under "keeping the spike out of the game".
- `Assets/Editor/CvSpikeBuild.cs` builds a **separate** `builds/VeyroCvSpike.apk`, package
  `com.ferrabled.veyro.cvspike`, so it installs beside the game instead of replacing it.

**Keeping the spike out of the game** needed all four of the measures below, and the last two were
found only by rebuilding the release APK and diffing it against the 21 Aug baseline. Guarding the
entry point was *not* enough, and nothing in the editor warned about it:

| release APK | size | CAMERA permission |
|---|---|---|
| 21 Aug baseline, pre-CV | 29.6 MB | no |
| CV code compiled in | 44.6 MB | **yes** |
| + `MotionRunner.Cv` behind `defineConstraints` | 38.0 MB | no |
| + package out of the manifest | 29.6 MB | no |

1. Its own package name, so the spike installs beside the game.
2. The `RuntimeInitializeOnLoadMethod` hook is behind `VEYRO_CV_SPIKE`.
3. **`MotionRunner.Cv` is gated on that define via `defineConstraints`.** Unity scans compiled
   assemblies for `WebCamTexture` and had added CAMERA to the manifest of a game that does not use
   a camera, plus 15 MB. Cost: the editor no longer type-checks the CV code by default — a spike
   build is what compiles it, and it fails loudly.
4. **The package is not in the committed manifest.** Inference Engine ships a large compute-shader
   library in its own Resources, which Unity puts in the APK whenever the package is present,
   referenced or not — 8.8 MB for nothing. `docs/cv-spike.sh on|off` toggles the package and
   fetches the weights, and is how anyone reproduces this spike.

The 20 MB of weights are separately staged into `Resources` by the spike build and unstaged in a
`finally`, so they never reach a release APK either.

**Verified:** 127 EditMode tests green (81 before, 46 new). Spike APK built headless in a scratch
copy and run on the phone. Release APK rebuilt from this tree is byte-for-byte the baseline size
with no extra permissions, so Track A is untouched.

**Two bugs the device caught**
- *The orientation probe trusted noise.* With nobody in frame all eight candidates scored ~0.14 and
  it locked onto `rot=0`, overruling the device's `rot=270`, because 0.147 beat 0.146. It now needs
  the winner to clear a confidence floor, otherwise it keeps the device report and re-probes.
  Three tests cover it, one replaying that exact noise pattern.
- *The device's reported rotation is wrong here anyway.* With a person in frame the probe settled
  confidently (score 0.75) on `rot=90` while `videoRotationAngle` reports 270. Building the probe
  instead of trusting the API was the right call, and T-011 should keep it.

**Needs a human**
- **The scope call this number forces** — OPEN_QUESTIONS 7. Recommended default: park camera mode
  as a *demo* (a menu toggle at ~3 Hz, hands-free but not playable) and do not put it in the pitch
  as an input mode. T-011–T-013 stay unstarted until that is answered.
- Where the BlazePose weights should live once camera mode ships — OPEN_QUESTIONS 8.
- A device with a different GPU would be worth 10 minutes: this verdict is one SoC wide.

**Next:** nothing in Track B until OPEN_QUESTIONS 7 is answered. T-020 RevenueCat remains the
critical path.

## 2026-08-21 — T-003, T-005, T-008 done; named "Veyro Run" (track-a/chunks-session)

**T-003 chunk system + T-005 obstacles/scoring/result screen.**
- New engine-free assembly `MotionRunner.Track` (`Assets/Scripts/Track/`, `noEngineReferences: true`):
  chunk metadata + library, generator, seeded PRNG, difficulty curve, collision geometry, scoring.
  No Unity dependency, so it is testable headlessly and identical on Mono and IL2CPP.
- Unity side: `TrackDirector`, `ChunkView`, `RunSession`, `RunHud` (UGUI from code). `RoadScroller`
  deleted. `RunnerController` is now driven by `RunSession.Step(dt, input)` for a fixed frame order;
  its tuning constants are untouched, so the T-002 feel is preserved.
- `com.unity.ugui` 2.5.0 added to the manifest. `RuntimeMaterials.Shared(color)` added (cached).
- AC amendment: chunk metadata is plain C# data, not ScriptableObjects — nothing visual to author
  yet. SOs land with T-006. Noted in BACKLOG.

**Verified:** 69 EditMode tests green; APK builds clean; full loop confirmed on the OnePlus Nord 2
by screenshot. Memory over a 10-min tap soak plus a 5-min confirmation soak was flat-to-declining
(231.3 → 231.2 MB PSS on the last two samples), no exceptions. Baseline for future comparison.

**Two bugs the device caught, both fixed:** a visible hole under the player at run start (track began
at z=0; the camera sees back to ≈ -3.4, hence `TrackDirector.FirstChunkZ`), and the result-screen
hint rendering under the RUN AGAIN button.

**Same day:** Unity splash screen removed (`PlayerSettings.SplashScreen.show/showUnityLogo = false`
in `BuildScript`, mirrored in `ProjectSettings.asset`; permitted for Unity 6 Personal per
LICENSING_REVENUE §2.12 — the attribution line is required only if a credits screen is ever added).
Name settled as **Veyro Run**, package **`com.ferrabled.veyro.run`** (reverse-DNS of ferrabled.com,
which the owner holds); verified via `aapt2 dump badging`. Old `com.motionrunner.game` uninstalled.

**T-008 Daily Run (same day).** `RunMode.Daily` is now the default: the seed is the UTC date
(`DailySeed`, clock-free — the caller passes the date in, so generation stays deterministic).
Best scores split into all-time and today-only under `veyro.*` PlayerPrefs keys; the daily bucket
self-resets on date rollover, so it stays one key rather than one per day for ever.

Found and fixed while doing it: the seed mixed in `Application.version`, so **a bugfix release would
have forked the day's players onto different tracks**. The seed now carries
`ChunkLibrary.ContentVersion` instead — bump that, and only that, when the chunk library changes.
`RunSeed.GameVersion` was renamed `ContentVersion` so nobody wires the app version back in.

**Verified:** 81 EditMode tests green (12 new, incl. pinned RNG states for known dates). On device,
two cold launches produced byte-identical runs — same seed `20260821/greybox-1/greybox`, score,
distance and chunk count — the AC demonstrated on real hardware. HUD shows `DAILY · <date>`.

**Needs a human**
- A continuous 10-minute *played* run. The soak taps rather than steers, so its runs are short.
  Install `builds/MotionRunner.apk`, play 10 min watching for hitches at chunk boundaries, then
  compare `adb shell dumpsys meminfo com.ferrabled.veyro.run` against the numbers above.
- Feel of obstacle spacing and the difficulty ramp under tilt. Both are first guesses; T-007 tunes
  them against real play.
- Move the settled app name into DECISIONS.md (owner-only). Answer OPEN_QUESTIONS 6 (score freeze).
- Whether to rename the `MotionRunner.*` C# namespace to match the product. Cosmetic and invisible
  to players; safe while everything is code-first; cheapest before T-006/T-020 grow the surface.
  (The old `motionrunner.best_score` key is gone — T-008 restructured best-score storage while there
  are still no players, which was the free moment to do it.)

**Next:** T-020 RevenueCat as soon as P4 exists (it is the contest eligibility gate), then T-007
(curve + difficulty bands — scaffolding and constraint tests exist, needs the owner's played-run
feedback) and T-006 (art pass, and where chunk data becomes ScriptableObjects).

## 2026-08-20 — T-001 + T-002 done; prototype live on device (orchestrator session)

- Unity 6000.5.9f1 (owner-installed, Android + iOS modules). Project created at `game/` in batchmode:
  URP 17.5.0, Input System 1.20.0, Test Framework 1.7.0; `com.unity.multiplayer.center` removed (D9).
- T-002 prototype, fully code-first: `IGameInput` + Gyro/Touch/Keyboard adapters + `CompositeInput`,
  `RunnerController` (3-lane steer + jump), `GameBootstrap` via `RuntimeInitializeOnLoadMethod`.
  `BuildScript.BuildAndroid` for headless APKs (IL2CPP/ARM64, portrait).
- APK installed on the owner's OnePlus Nord 2 (DN2103, the mid-tier perf target), renders correctly,
  no runtime errors. Headless loop proven end-to-end: `ProjectSetup.SetupUrp` →
  `BuildScript.BuildAndroid` → adb install/launch/screencap. Rebuild cycle ≈ 3–5 min.
- Three rendering traps found and fixed here; they are now gotchas 1–3 in CLAUDE.md.
- **Owner verdict 20 Aug on tilt feel: "feels fine for now" → GO**, which cleared the T-002 gate.

## 2026-08-20 — project scaffolding + owner answers (orchestrator session)

- Shipaton 2026 facts verified against live Devpost/rules/docs; spec is
  `shipaton_motion_runner_handoff_v2.md`. Agent workspace created (CLAUDE.md + docs/).
- Feasibility: Inference Engine + BlazePose confirmed as the primary CV path (official Unity sample,
  Apache-2.0 models); Layers has a Unity UPM SDK; RevenueCat Unity SDK has Paywalls but **not**
  Galaxy Store billing.
- Owner answers: no existing Play *developer* account → 12-testers × 14-days closed testing applies
  (D11; P1 + H6 are the urgent human actions). No Mac — a friend's Apple Developer account is the
  iOS path. Android phone available.
