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

9. **RevenueCat catalog: SKU ids, entitlement name, prices.** Owner decides — product ids are
   immutable once created in Play Console, and pricing is a business call. Full reasoning in
   `docs/REVENUECAT_PLAN.md` §4. *Recommended default:*
   - entitlement: **`cosmetics`** (one entitlement, so a second SKU later touches no gameplay code)
   - offering: **`default`**
   - SKU 1: `veyro.cosmetic.runner_pack` — "Runner Colours" — **€2.99 / $2.99**
   - SKU 2: `veyro.cosmetic.supporter` — "Supporter Bundle" (all colours + a visual trail) —
     **€5.99 / $5.99**
   - both **one-time, NON-CONSUMABLE** — this one is not a preference. A one-time product
     misconfigured as consumable is consumed by RevenueCat and can never be restored (SDK ≥ 9.0.0),
     which breaks T-020's "entitlement survives reinstall" criterion permanently.
   Two SKUs rather than one because HAMM is judged on the monetization *strategy*, and a single
   price point shows no thinking. D6 binds either way: cosmetics only, no pay-to-win.

10. **Ship a hidden "judge mode"?** Devpost promo codes are the primary judge path
    (`REVENUECAT_PLAN.md` §6), but if a code fails at 11pm on 30 Sep there is no recovery: our app
    user IDs are anonymous, so RevenueCat cannot grant an entitlement to a specific judge.
    Proposal: a code-entry on the title screen that calls `LogIn("shipaton-judge-<n>")`, so that ID
    can be granted the entitlement from the RevenueCat dashboard as a manual override.
    *Recommended default: **yes** — roughly 20 lines, no APK cost, and the alternative failure mode
    is an unscoreable submission.*

11. **Demo video: voiceover, and who is on camera?** (`docs/submission/VIDEO_SCRIPT.md` §"Owner
    decisions"). *Recommended default: voiceover — #BuildInPublic and Grand Prize reward a person
    with a story, and a silent captioned video reads as an ad. The hook shot needs a body but not a
    face; from behind or in silhouette works.* Music: owner picks and confirms the licence, or use
    game audio only — a copyright claim can make the video private mid-judging.

12. **Privacy-policy URL.** Google Play requires a hosted privacy policy for every app, including
    ones that collect nothing. Blocks the store listing, not just the submission.
    *Recommended default: a single GitHub Pages page under the existing `ferrabled/Veyro` repo —
    minutes of work, no hosting cost.* Content is genuinely short: no accounts, no analytics in
    v1.0, scores stored locally; once the camera update ships, add the on-device-only camera clause
    and mirror it in the Play Data safety form.
