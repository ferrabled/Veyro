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
  `docs/COSMETICS_CATALOG.md`. *Owner: move to DECISIONS.md.* The same ids must now be created
  character-for-character in Play Console (`docs/PLAY_CONSOLE_SETUP.md` §D1).

## Open

1. **Team size:** solo otherwise (art, video, testing)? Affects how many agent tracks run in
   parallel, and who approves store submissions.
2. **Devices:** which Android phone and iPhone are available? Any Galaxy / Fold access (owned,
   borrowed, or Samsung Remote Test Lab)?
3. **Orientation:** portrait or landscape? *Default: portrait for v1.0 — bigger mobile audience,
   simpler UI — revisit landscape for the TV story.*
4. **Art direction:** low-poly flat-colour, toon-shaded, neon? *Default: agents propose 2–3 style
   frames in T-006 for you to pick from.* Must respect the Veyron note above.
5. **Budget ceiling** for the ~$150–250 of unavoidable costs (Apple $99, Play $25, test devices)?
6. **Freeze the score formula before the first public build?** Currently `whole metres + coin points`,
   a coin worth 10 × a multiplier that steps every 3 coins and caps at ×5 (`ScoreState`). Once Daily
   Run (T-008) and challenges (T-024) are live, changing it makes old scores incomparable.
   *Default: leave it, play it once, treat it as frozen from the first closed-testing upload; balance
   via chunk difficulty and speed instead.*

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

10. **Ship a hidden "judge mode"?** Devpost promo codes are the primary judge path
    (`REVENUECAT_PLAN.md` §6), but if a code fails at 11pm on 30 Sep there is no recovery: our app
    user IDs are anonymous, so RevenueCat cannot grant an entitlement to a specific judge.
    Proposal: a code-entry on the title screen that calls `LogIn(<code>)`, so that ID can be granted
    the entitlements from the RevenueCat dashboard as a manual override.
    *Recommended default: **yes** — roughly 20 lines, no APK cost, and the alternative failure mode
    is an unscoreable submission.*

    **Codes must be random, one per judge, and absent from the APK** (`REVENUECAT_PLAN.md` §6.4,
    tightened 26 Aug): `judge-7f3a9c1e4b8d`-style, `openssl rand -hex 6`, generated out-of-band.
    A predictable `shipaton-judge-<n>` would let anyone who guesses or extracts it inherit the
    granted entitlement. The app validates nothing and ships no list — an unknown code just yields
    an app user ID with no entitlements. Leaked codes are revoked per-ID in the dashboard.

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
    judge mode keeps 10, which REVENUECAT_PLAN §6.4 references.)*

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
