# Status journal (newest at top)

Keep entries short: what changed, how it was verified, what needs a human. Durable operational
knowledge does **not** belong here — invariants go in code comments, recurring traps go in the
"Known gotchas" list in CLAUDE.md. Old entries may be pruned once their content lives elsewhere.

## 2026-08-21 — T-003 + T-005 done; named "Veyro Run" (track-a/chunks-session)

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

**Needs a human**
- A continuous 10-minute *played* run. The soak taps rather than steers, so its runs are short.
  Install `builds/MotionRunner.apk`, play 10 min watching for hitches at chunk boundaries, then
  compare `adb shell dumpsys meminfo com.ferrabled.veyro.run` against the numbers above.
- Feel of obstacle spacing and the difficulty ramp under tilt. Both are first guesses; T-007 tunes
  them against real play.
- Move the settled app name into DECISIONS.md (owner-only). Answer OPEN_QUESTIONS 6 (score freeze).
- Whether to rename the `MotionRunner.*` C# namespace to match the product. Cosmetic and invisible
  to players; safe while everything is code-first; cheapest before T-006/T-020 grow the surface.
  Leave the `motionrunner.best_score` PlayerPrefs key alone regardless — changing it after release
  silently resets every player's best score.

**Next:** T-007 (curve + difficulty bands; scaffolding and constraint tests already exist), T-006
(art pass, and where chunk data becomes ScriptableObjects), T-008 (Daily Run — `RunSession.StartRun`
is the single place a seed is chosen).

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
