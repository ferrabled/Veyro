# Open questions for the human owner

Agents: append questions here — context, plus a recommended default so nothing stalls waiting for an
answer. Owner: answer inline; move settled scope/architecture calls into DECISIONS.md, then delete
the question. Keep this file short; it is read every session.

## Answered

- **Play developer account** (20 Aug): none existed → new personal account needed, and the
  **12-testers × 14-days closed-testing rule applies**. See D11; P1 + H6 are the action items.
- **macOS** (20 Aug): no Mac. A friend's Apple Developer account is the iOS path — Windows exports
  the Xcode project, they sign and upload (T-032). *Follow-up: confirm they are actually willing,
  and can spare ~1–2 h per release.*
- **App name** (21 Aug): **"Veyro Run"**. Package **`com.ferrabled.veyro.run`**. *Owner: move to
  DECISIONS.md.* One live constraint survives the research: **VEYRON is a Bugatti/VW mark one letter
  away**, so keep the art direction clear of anything automotive or racing-branded — that is where a
  confusion argument would get traction. (Not legal advice; a clearance search is cheap pre-upload.)
- **RevenueCat catalog** (was item 9; answered 24 Aug): owner confirmed by creating the full
  catalog in the RevenueCat dashboard for both the Test Store and the Play Store app — 2 skins
  (`veyro.skin.ember`/`.frost`, €2.99) + Season 1 pass (`veyro.season1.pass`, €4.99), entitlements
  `skin_ember`/`skin_frost`/`season1`, offering `default`, paywall attached. Spec:
  `docs/COSMETICS_CATALOG.md`. *Owner: move to DECISIONS.md.* ~~The same ids must now be created
  character-for-character in Play Console~~ (done 27–28 Aug — with ids **without the `veyro.`
  prefix**: `skin.ember`/`skin.frost`/`season1.pass`, immutable and working; see the 15 Sep
  correction in `COSMETICS_CATALOG.md` §2).

## Open

1. **Team size:** solo otherwise (art, video, testing)? Affects how many agent tracks run in
   parallel, and who approves store submissions.
2. **Devices:** which Android phone and iPhone are available?
3. **Orientation:** portrait or landscape? *Default: portrait for v1.0 — bigger mobile audience,
   simpler UI — revisit landscape for the TV story.*
4. **Art direction:** low-poly flat-colour, toon-shaded, neon? *Default: agents propose 2–3 style
   frames in T-006 for you to pick from.* Must respect the Veyron note above.
   **19 Sep, feat-game-design:** owner authorized a free-asset art pass and phone installation.
   Working recommendation: sunlit low-poly park using Kenney CC0 props and an animated human,
   retaining teal/pink/paper as brand anchors. Rationale and alternatives: `docs/GAME_ART.md`.
   Final visual approval and the ten-second stranger test remain owner judgments, not recorded
   as a settled decision by the agent.
5. **Budget ceiling** for the ~$150–250 of unavoidable costs (Apple $99, Play $25, test devices)?
6. ~~**Freeze the score formula before the first public build?**~~ **ANSWERED by the owner,
   19 Sep: freeze as-is — recorded as D17.** `whole metres + coin points`, a coin worth 10 × a
   multiplier that steps every 3 coins and caps at ×5 (`ScoreState`); balance via chunk
   difficulty and speed instead. Binding consequence: the formula, `game_config` and
   `supabase/functions/_shared/validate.ts` now move together or not at all — changing one
   alone flags every honest run.

7. ~~Camera mode: park, invest, or drop?~~ **Answered by the owner, 22 Aug: build it** (option d,
   BlazeFace-on-CPU — 4.2 ms blocking median on the Nord 2, numbers in STATUS T-010b). Implemented
   on the `camera-feature` branch the owner created; v1.0 on `main` is untouched and still ships
   gyro+touch first (D2 holds; T-020 remains the critical path — the owner has requested the Play
   Console account). *Owner: move the decision to DECISIONS.md.* The 1.5–3 m range risk still
   needs the 10-minute human test — steps in STATUS.
8. **Where should the BlazePose weights live?** Partially settled by 7: camera *control* ships on
   BlazeFace, whose 418 KB ONNX is now simply **committed** at
   `game/Assets/Scripts/CameraInput/Resources/CameraInput/` (small enough to not need LFS). The
   ~20 MB BlazePose weights are needed only by the benchmark spike and stay gitignored /
   fetched by `docs/cv-spike.sh`. Remaining question: if a pose-skeleton *overlay demo* is ever
   wanted for the pitch video, do those 20 MB move to Git LFS with T-006? *Default: yes, then.*

   Note for the camera-feature branch: `com.unity.ai.inference` is now IN the committed manifest
   there (the shipping camera code needs it), so that branch's APK carries the package's ~8.8 MB
   and the CAMERA permission by design. `main` keeps the clean 29.6 MB / INTERNET-only baseline.

9. **Privacy policy hosting + public contact email.** ✅ **ANSWERED by owner, 25 Aug 2026:**
   hosting is **Cloudflare Workers static assets** (free tier) at **https://veyro.ferrabled.com/privacy/**,
   deployed with wrangler from the new `site/` folder (marketing site + privacy/terms/support pages,
   built on branch `veyro-site`). Contact email is **ferrabled+veyro@gmail.com** — explicitly a
   temporary alias; the owner will replace it later (grep `site/` + `docs/PRIVACY_POLICY.md` when
   that happens). `GameLinks.PrivacyPolicyUrl` and the policy's TODO slots are updated. Remaining
   owner action: run `npx wrangler login && npx wrangler deploy` from `site/` — the URL 404s until
   that first deploy, and the Play Console privacy-policy field needs the new URL.

   **Merged 26 Aug — hosting is DONE; the live risk moved into the binary.** The site is deployed and
   verified at `https://veyro.ferrabled.com/` (privacy/terms/support), so the "404s until first
   deploy" caveat above is stale — STATUS 25 Aug (compliance session) records it live and passing the
   User Data policy checklist. What is *not* resolved is the **URL baked into the shipped build**:
   versionCode 1 went to internal testing with the dead GitHub Pages privacy URL compiled in
   (verified in the AAB's IL2CPP metadata), and the fix is a rebuild with `VEYRO_VERSION_CODE=2`.
   Closed testing went live **26 Aug** — so:

   **Owner: confirm the closed track is serving versionCode 2, not 1.** If it is versionCode 1, the
   live testers' in-app privacy link points at a dead URL while CAMERA — a sensitive permission — is
   in the build, which is precisely what a production reviewer checks. Also confirm the Play Console
   privacy-policy field and `GameLinks.PrivacyPolicyUrl` both read the `veyro.ferrabled.com` URL.
   The "URL coupling" rule in `docs/STORE_COMPLIANCE.md` is the standing version of this trap.


9b. ~~Paste the two RevenueCat public API keys~~ ✅ **ANSWERED by owner, 27 Aug:** both public
    keys are in `game/Assets/Scripts/Commerce/RevenueCat/RevenueCatKeys.cs` (`test_…` + `goog_…`).
    Key/artifact pairing since 27 Aug: `.aab` → Play key, `BuildAndroidDev` (debuggable) → Test
    Store key, every other APK → no key (store disabled, fail-open); wrong pairings fail the
    build. Reminder unchanged: never paste an `sk_` secret key anywhere in the repo.

10. **Resolved by owner, 23 Sep 2026: Google Play promo codes are the primary judge-access route.**
    The hidden judge-login proposal is retired; no custom login/code-entry feature is planned.
    Generate product-specific codes for `season1.pass`, `skin.ember` and `skin.frost`, test them
    through Google Play, and supply unused codes with redemption/restore instructions. Keep
    access available throughout judging. `REVENUECAT_PLAN.md` §6 is the current runbook; §6.4
    covers ordinary support without changing the existing profile/RevenueCat identity.
    Number retained for historical references. *Owner: move to DECISIONS.md.*

11. **Demo video: voiceover, and who is on camera?** (`docs/submission/VIDEO_SCRIPT.md` §"Owner
    decisions"). *Recommended default: voiceover — #BuildInPublic and Grand Prize reward a person
    with a story, and a silent captioned video reads as an ad. The hook shot needs a body but not a
    face; from behind or in silhouette works.* Music: owner picks and confirms the licence, or use
    game audio only — a copyright claim can make the video private mid-judging.

12. **Record the 24 Aug owner call in DECISIONS.md (owner-only):** closed testing and v1.0 ship
    the `camera-feature` branch — camera mode included — under the real package name. This amends
    D2's "camera merges post-release". Consequences already handled in this repo: CAMERA permission
    + ~8.8 MB inference package are in the Play build from day one; `docs/PRIVACY_POLICY.md` and
    the T-031 Data safety answers describe the camera as on-device-only; store description must
    mention camera mode (T-030). *(Was numbered 10 on `main`; renumbered 12 in the 24 Aug merge —
    judge-access question keeps 10, now resolved above.)*

13. **Hands-free flow defaults (2026-09-01, `pause-game-feature`).** The resume flow shipped with
    the spec's defaults; each is one constant. Confirm or retune on device:
    - Raise-hand hold: 3 consecutive probe samples (`RaisedHandConfirm.RequiredSamples`, ~1 s at
      the probe's ~3 Hz); margin 0.5 head units above the nose (`RaisedHand.HeadUnitsAboveNose`).
    - Countdown: 3 s (`ResumeCountdown.DefaultDurationSeconds`), camera resumes only. **Show it on
      tilt resumes too, for consistency?** *Default taken: no — tilt resumes stay instant.*
    - **Should the picker's initial "I can see you!" staging also use the gesture + countdown?**
      The spec recommends it (one mechanic, taught once; pairs with the deferred T-015 setup
      card) but lists it as an owner decision, so it is NOT built. *Recommended: yes, post-device
      test of the pause-resume version.* Camera runs currently still start right after the face
      hold, with no countdown — the run's initial calibration happens during staging as before.
    - **Picker shows both boards or only the selected mode's?** Bests split tilt/camera
      (`BestBoard`); the picker currently shows neither. *Default taken: neither (unchanged) —
      result screen + HUD show the active board.*
    - **Pose model size:** the shipped detector + lite landmarker are **20.6 MB on disk**
      (15.1 + 5.5), not the 5–10 MB the brief estimated. APK diff in STATUS. If that is too much:
      Inference Engine can serialize fp16-quantized assets (~half), or the gesture could drop the
      detector stage and crop from the face box (unproven). *Recommended default: ship as-is for
      closed testing, quantize before v1.0 if the .aab budget minds.*

14. **RevenueCat closed-test release (2026-09-15)** — ✅ **all three answered by the owner, 15 Sep:**
    - **versionCode 5.** Console max across tracks/drafts is 4. This and every future release
      keeps using the `VEYRO_VERSION_CODE` env var, which `BuildAndroidBundle` already reads
      (STORE_COMPLIANCE standing rule 1) — nothing new to build.
    - **Production access: en revisión.** Solicitud enviada lunes 23:05; Google says ≤7 days,
      answer arrives by email to the account owner. Nothing to do but wait; access approval alone
      publishes nothing.
    - **Season 1 pass stays on sale as-is.** The reward ladder is upcoming near-term work; the
      shop row's "reward ladder arrives with the Season 1 update" note and the paywall stay
      unchanged. *Owner: move this to DECISIONS.md if it should bind future sessions.*

15. ~~**Record the 3 Sep owner call in DECISIONS.md — T-009 redefinition.**~~ **DONE, 19 Sep:
    recorded as D13 (Supabase backend, anonymous-first identity, recovery code + manual
    import), D14 (Supabase UUID = RevenueCat app user ID, reset on delete), D15
    (server-generated handles only; unlimited rerolls per the 17 Sep amendment) and D16
    (Daily + All-time, keyed and split standard vs camera).**

16. ~~**Approve the deletion-page/support copy before the T-009 release.**~~ **ANSWERED by
    the owner, 19 Sep: ship as drafted.** Live in `site/public/privacy/` and
    `site/public/support/` — in-app delete path, the `/support/#delete` web path, the 30-day
    commitment, the Player-ID-locates / recovery-code-proves verification rule, and the
    disclosure that the recovery code can survive an uninstall. Still needs deploying with
    the Update-1 binary, never on the versionCode-5 T-020 flip.

17. ~~**CAPTCHA on anonymous sign-ups.**~~ **ANSWERED by the owner, 19 Sep: leave it off —
    recorded as D18.** Enabling enforcement would break sign-in for every shipped client and
    needs a WebView the client does not have. Current lid is throttling (per-IP anonymous
    rate limit, per-user quotas, flagged-run caps in migration `0004`), **not** bot
    prevention. Revisit only if board pollution is actually observed.

18. ~~**Attestation / replay validation — documented residual risk, out of scope.**~~
    **ANSWERED by the owner, 19 Sep: not a decision — tracked as work.** Filed as **T-041**
    (post-hackathon by default; pulled forward only if the schedule frees up). Deliberately
    NOT recorded in DECISIONS.md, because this is a "not yet", not a settled "never". The
    standing constraint survives either way: **no document, listing or pitch may describe the
    board as cheat-proof.** The cheapest first step lives in that task — a top-N sanity query
    before submission day, since nothing currently alerts on an implausible score.

19. **Sponsor-update release baseline (20 Sep, feat-implement-tracks).** The owner accepted
    the OneSignal-to-Layers Daily Run loop and requested an integration guide. The 16 Sep
    release plan keeps profiles out of that update, while this branch includes profiles and
    art. Which baseline should ship the SDKs, and is versionCode 5 now public or still in
    review? *Recommended default: keep the sponsor update on the existing release baseline
    until the owner explicitly includes the newer features; adapters can proceed independently.*
    P5/P6 account/FCM readiness and public SDK IDs remain owner prerequisites, with exact steps
    in `SPONSOR_SDK_INTEGRATION_GUIDE.md` §1. No production state or release-scope choice was assumed.

20. **Resolved: OneSignal FCM readiness (20 Sep, feat-implement-tracks / T-021).** The owner
    uploaded the Firebase service-account JSON directly to OneSignal and reached SDK onboarding.
    The same public App ID `1f6ba056-efe3-4bfe-a0cd-a9a26150720a` now returns a numeric
    `android_sender_id` in its public Android configuration. Credential setup is sufficient to
    proceed to the device test; this does not yet prove push delivery. No private credential
    entered chat or the repository. Keep the Unity SDK for Android; the dashboard's native
    Android prompt does not require a second SDK or Firebase database.

21. **T-021 remaining device checks (20 Sep evening, feat-implement-tracks).** The connected
    Nord 2 registered and is subscribed with a push token; SDK-enabled release/dev APKs were
    installed without uninstalling or clearing data. The owner restricted interaction to the
    game. **Test send answered:** the owner received the notification and tapping opened the
    game; the OneSignal icon needs replacing. Exact build/message ID and warm/cold state were
    not captured. Remaining owner action: coordinate a device slot and control only Veyro Run's
    notification permission in Android Settings for further
    diagnosis of the reproduced denial/retry hang: the phone rejects ADB permission revocation.
    *Recommended default: keep this installation, no credential sharing/data reset, schedule
    one coordinated device window, fix/retest the retry before SDK release.* Full test report:
    `ONESIGNAL_DEVICE_TEST.md`. Retry root cause remains unconfirmed; foreground and separate
    warm/cold checks remain. Campaign/Layers implementation can proceed separately under
    `REMINDER_AUTOMATION_PLAN.md`. **21 Sep:** P6 App ID received; icon and bounded permission
    retry changes implemented, physical retest remains required on the combined build.
    **23 Sep coordinated session:** current development build installed with data preserved;
    Android notification permission is OFF. May this game's permission be enabled and up to
    three labelled test pushes sent only to this phone? This was asked during the session;
    no approval is inferred from the request to install/test or from earlier receipt of a
    different push. Recommended default: keep the current permission until the owner answers,
    then check foreground suppression, warm/cold taps and the icon in the coordinated slot.
    Native denial/retry may need a separate owner-controlled permission reset.
    The current panel reports ALLOW IN ANDROID SETTINGS, so permission cannot be enabled
    within the owner's existing no-settings constraint. Final local reminder preference and
    SDK opt-in match the baseline; OS permission remains off. The 23 Sep `STATUS.md` entry
    records the accidental reminder-off tap and its verified restoration.

22. **Layers account and SDK distribution checks (21 Sep, feat-implement-tracks / T-022).**
    App ID `app_4cf32e54fc359326` received; SDK consent and real-run instrumentation implemented.
    **21 Sep update — stock migration implemented** (owner-approved same day): the patched
    embedded package was replaced by the official unmodified SDK pinned by Git URL to commit
    `7d28dcda555ab3ab3f0901c6c6e77f8710613f4b` (v3.3.2), with advertising-ID and install-referrer
    libraries excluded project-wide in the Gradle templates, and unsent events now purged on the
    next enable instead of at turn-off. Outcome and rationale: `LAYERS_STOCK_SDK_REVIEW.md`.
    Rebuilt-APK evidence landed the same evening (`builds/layers-stock-verification.json`:
    515/515 tests, no AD_ID, no ads-identifier/installreferrer classes, native hash unchanged);
    the physical device test is still pending.
    **22 Sep — device test PASSED on the Nord 2** (all 7 steps: OFF-by-default, enable with both
    GAID and install-referrer lookups failing, held-and-unsent run events, unchanged files across
    a relaunch, purge + device-id rotation on re-enable; no `idfa` anywhere; `builds/layers-stock-verification.json`
    → `device_test`). Only transport-level 2xx receipt was observable, so (b) below narrows to the
    Layers **Events-screen** confirmation.
    **Still owner actions:** (a) confirm no advertising/CAPI destinations are connected in the
    Layers dashboard; (b) confirm the events arrived on the Layers Events screen (the device side
    of enable / off / relaunch / re-enable is now verified). **License question downgraded:** upstream still ships
    no LICENSE or package license field, but we redistribute no modified SDK source any more, so
    asking Layers to add one is a courtesy request. *Recommended default: not a release gate;
    send the request, ship the stock SDK. SDK analytics only, advertising off, explicit opt-in,
    verified provider-deletion process.* Production feature baseline and unused Play
    versionCode are still owner decisions in Q19; no signed Play release was assumed.

23. **Production Season 1 XP curve (T-025, before release).** Owner requested a tiny temporary
    cap for testing. Development builds now use cumulative thresholds 0–9; actual server XP
    is unchanged. Choose production pacing after device acceptance. Suggested tuning starting
    point: 0, 10, 25, 50, 100, 180, 300, 450, 650, 900 XP, not yet approved. Production builds
    of the game are gated until this is resolved; development/device verification can proceed.
    Level-1 Mint and owned-pass Neon Lime retain their immediate catalog unlock.

24. **Android App Links verification for challenge links (T-024) — needs the release signing
    certificate's SHA-256, which only the owner can produce.** The game now registers
    `https://veyro.ferrabled.com/challenge…` with `android:autoVerify="true"`
    (`Assets/Editor/AndroidChallengeLinks.cs`). Android only honours that filter if
    `https://veyro.ferrabled.com/.well-known/assetlinks.json` serves the fingerprint of the cert
    the app is *actually signed with* — and because Play App Signing re-signs uploads, the right
    fingerprint is the one in **Play Console → Test and release → Setup → App signing → "App
    signing key certificate" SHA-256**, not the upload key's. (Play Console → *App Links* can also
    generate the file's contents; the same screen reports verification state per release.)
    **Recommended default, already implemented: ship without it.** The custom scheme
    `veyro://challenge?…` needs no verification and is what the landing page taps, so an
    unverified https link simply opens the web page, which then opens the app — the round trip
    works today. Adding assetlinks later is a *website* change, needs no rebuild, and only
    upgrades the experience (the link stops bouncing through the browser). Owner action when
    ready: paste the fingerprint into `site/public/.well-known/assetlinks.json` (see
    `docs/PLAY_CONSOLE_SETUP.md` §F) and `npx wrangler deploy`.
