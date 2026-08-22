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

7. **Camera mode: the T-010b sweep changed the options** (STATUS 22 Aug, T-010b entry — numbers
   there). BlazePose still misses the gate, but **BlazeFace short-range on the CPU backend medians
   4.2 ms** (9.1 ms inside a live 60 FPS loop at ~59 Hz, zero dropped frames) on the Nord 2 — the
   30 ms gate is cleared several times over by a 418 KB model that gives face-x (LEFT/RIGHT),
   face-y velocity (JUMP) and face-y drop (SLIDE). Updated ways forward:
   (a) **park camera mode as a demo** — still valid, now underselling what's measured;
   (b) **Path B, MediaPipeUnityPlugin — obsolete**: it existed as the performance escape hatch,
   and the performance problem is solved without it;
   (c) **drop camera mode** and reclaim the Track B time;
   (d) **build camera mode on BlazeFace-on-CPU** — T-011 becomes a face-tracking loop (no pose
   detector, which also can't run on CPU without OOM-killing the app — see STATUS), T-012's gesture
   layer works on face centre instead of torso centre (same PoseGeometry seam), T-013 gates on the
   *blocking* CPU latency (the async number is frame-quantized and misleading). Roughly 2–4 days,
   after T-020, still behind D2 so v1.0 is unaffected.
   *Default if you say nothing: (d), started only once T-020 is done.* **One risk before committing:
   the short-range face model is trained for arm's-length faces, and detection at 1.5–3 m play
   distance is untested — needs you in frame for ~10 minutes (steps in STATUS).* If that fails, the
   fallbacks are converting the full-range BlazeFace to ONNX, or (a).
   **T-011–T-013 stay unstarted until you answer.**
8. **Where should the BlazePose weights live?** ~20 MB of Apache-2.0 ONNX. Right now they are
   gitignored and fetched by `docs/cv-spike.sh on`; `CvSpikeBuild` stages them into `Resources`
   for the spike APK only, so the release APK is unaffected. That works while this is a spike, but
   if camera mode ever ships the weights must be in the APK and therefore on every build machine.
   *Default: leave it as-is now, and move them into Git LFS at the same time as T-006 sets LFS up
   (D2) — but only if question 7 resolves to (a) or (b).*

   Not a question, just so you know it was checked: the release APK is back to its 29.6 MB baseline
   with no CAMERA permission. Getting there took gating the CV assembly *and* keeping
   `com.unity.ai.inference` out of the committed manifest — see STATUS 22 Aug for the numbers.
