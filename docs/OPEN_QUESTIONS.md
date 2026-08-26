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

9. **Privacy policy hosting + public contact email.** Play requires a live privacy-policy URL on
   the store listing *and* reachable in-app — hard-required here because CAMERA is a sensitive
   permission, and **it gates publishing the closed-testing track, i.e. the 12×14 clock**
   (`PLAY_CONSOLE_SETUP.md` §A4). The policy text is drafted at `docs/PRIVACY_POLICY.md` and the
   app links to `https://ferrabled.github.io/Veyro/privacy/` (`GameLinks.cs`). *Recommended
   default: enable GitHub Pages on github.com/ferrabled/Veyro (Settings → Pages → deploy from
   branch → `main`, `/docs` folder) — requires the repo to be public; any other host works if
   `GameLinks.PrivacyPolicyUrl` is updated to match.* Also needed from the owner: a public contact
   email for the policy's two `[TODO]` slots and the Play listing (the day-job address is probably
   not the one to publish).

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
