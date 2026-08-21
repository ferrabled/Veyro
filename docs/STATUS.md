# Status journal (append newest at top)

## 2026-08-21 — T-003 + T-005 done: chunk system, obstacles, scoring, result screen (track-a/chunks-session)

**What changed**
- New assembly `MotionRunner.Track` (`Assets/Scripts/Track/`, asmdef with `noEngineReferences: true`): `ChunkDefinition` (all handoff §4.3 metadata fields), `ChunkLibrary` (13 greybox chunks), `TrackGenerator`, `RunSeed` + `XorShiftRandom`, `DifficultyCurve`, `TrackMetrics`, `TrackGeometry`/`Aabb`, `ScoreState`. It has *no* Unity dependency on purpose: generation, collision and scoring are then testable headlessly and behave identically on Mono in the editor and IL2CPP on the phone.
- Unity side: `TrackDirector` (spawns ahead, recycles behind, pools views per chunkId), `ChunkView` (greybox geometry + per-pass coin state), `RunSession` (the run loop), `RunHud` (UGUI built from code). `RoadScroller` is deleted — `TrackDirector` replaces it.
- `RunnerController` no longer drives itself; `RunSession` calls `Step(dt, input)`, so a frame is always input → runner → world → collision → HUD. Its tuning constants are untouched, so the T-002 feel is preserved.
- `com.unity.ugui` 2.5.0 added to `Packages/manifest.json`.
- `RuntimeMaterials.Shared(color)` added: one cached material per colour. Chunk geometry creates hundreds of renderers, and a private material each would grow memory for no visual gain.
- **AC amendment (also noted in BACKLOG):** T-003 said "TrackChunk ScriptableObject". Chunk metadata is plain C# data instead. There is no visual content to author yet, and code data is diffable, headlessly testable and free of the scene/prefab merge hazards in CLAUDE.md rule 1. ScriptableObjects land with T-006, when chunks carry real art.

**Decisions worth knowing before touching this code**
- **Determinism** uses a hand-rolled FNV-1a seed hash plus xorshift32, not `System.Random`. Neither `string.GetHashCode()` nor `System.Random`'s sequence is contractually stable across runtimes, and Daily Run (T-008) and shared challenges (T-024) need the editor and the device to generate the same track. `RunSeedTests` pins both the hash and the first PRNG values: **a failure there is a content break that needs a WorldId bump, not a test to update in passing.**
- **Collision is AABB interval maths, no PhysX** (CLAUDE.md rule 4). Colliders that `CreatePrimitive` attaches are destroyed at creation. `Assets/link.xml` still has to preserve the physics module, because `CreatePrimitive` itself needs it.
- **Playability is enforced by validation, not by hope.** `ChunkDefinition.Validate()` rejects: a cluster blocking all three lanes, clusters closer than 6 m of reaction distance, obstacles inside the 3 m chunk-edge clearance (which is what makes the 6 m spacing hold *across* chunk seams too), a low barrier or full block whose chunk does not declare the matching `RequiredSkills`, and coins buried inside an obstacle. Every library chunk is validated by an EditMode test, so unplayable content cannot reach a build.
- Low barriers (top at y=0.5) are jumpable and can *only* be passed by jumping; full blocks (top at y=1.8) are deliberately unjumpable at the 1.78 m jump apex, so they are a steering test. `TrackGeometryTests` asserts both, plus that the jump arc clears a low barrier for ~78% of its airtime rather than by a hair.

**How it was verified**
- **EditMode: 69 tests, all green.** `Unity -batchmode -projectPath game -runTests -testPlatform EditMode`. Covers seed/PRNG pinning, same-seed-identical-sequence over 2000 chunks (and 11 more seeds over 200), different seed/world/version diverging, edges always connecting, difficulty window never relaxed, no chunk twice in a row, bounded bridge runs, obstacle spacing across seams over a whole 2000-chunk run, a 20 000-chunk run that never throws, the validator's rejections, all of `ScoreState`, and the collision geometry.
- **Build:** APK builds clean via `BuildScript.BuildAndroid`, no warnings.
- **Device (owner's OnePlus Nord 2 / DN2103):** the full T-005 loop confirmed by screenshot — run → coins/combo → crash → result screen with score/best/coins/best-combo → restart. HUD text renders (the built-in `LegacyRuntime.ttf` resolves on device). Best score survives across runs via PlayerPrefs.
- **10-minute soak** (adb tap loop, so the game runs and restarts continuously): TOTAL PSS 259 MB at startup, settling to **150 → 146 → 145.6 → 145.4 MB over the last five minutes** — flat, trending slightly down, no leak across dozens of `BeginRun` cycles. Native heap steady ~30 MB, graphics steady 12.6 MB, `Activities: 1` throughout, no exceptions or ANRs in logcat. A 5-minute confirmation soak on the final binary: PSS 249.7 -> 241.7 -> 232.0 -> **231.3 -> 231.2 MB**, native heap steady at 20.4 MB, graphics steady at 93.1 MB, 10 runs completed, no exceptions.
- **Cross-check that the maths matches the picture:** `RunSession` logs each run's distance, and it lands at 39 m — exactly the 40 m absolute z of the first obstacle minus the 0.75 m contact distance. Geometry, collision and visuals agree.

**Two real bugs the device caught, both fixed**
1. Every run opened with a **visible hole under the player**: the track started at z=0, but the camera sits at z=-6.2 and its bottom edge looks at roughly z=-3.4. Fixed with `TrackDirector.FirstChunkZ = -8f`, and the opener chunk lengthened to 36 m so the obstacle-free lead-in survives starting partly behind the runner.
2. The result screen's "tap anywhere" hint rendered **underneath the RUN AGAIN button** — it was anchored to the card instead of the screen.

**Gotchas for the next agent (continuing the 20 Aug list)**
4. **Unity refuses `-batchmode` on a project already open in the editor** ("another Unity instance is running with this project open"). If the owner has `game/` open, robocopy the project to a scratch dir and run tests/builds there, then copy the APK back. **Verify the sync afterwards** — a silently-failed robocopy here produced a build with none of the fixes in it and cost a full build + 10-minute soak cycle. Grep the copy for a line you just changed before you trust it.
5. The Android activity is `com.unity3d.player.UnityPlayerGameActivity` (Unity 6 GameActivity), *not* `UnityPlayerActivity`. Launch with `adb shell monkey -p com.motionrunner.game -c android.intent.category.LAUNCHER 1`.
6. Git Bash rewrites unix-looking arguments into Windows paths, which breaks `adb shell screencap /sdcard/x.png`. Prefix device-path commands with `MSYS_NO_PATHCONV=1`, but keep plain Windows paths for local `adb install` / `adb pull` destinations, since the same flag breaks those.
7. **UGUI is not in a fresh Unity 6 manifest.** `com.unity.modules.ui` only gives you `UnityEngine.UIModule` (Canvas, RectTransform); `Text`, `Image`, `Button` and `CanvasScaler` live in the `com.unity.ugui` package. It ships inside the editor install (`Editor/Data/Resources/PackageManager/BuiltInPackages`), so adding it resolves offline with no network.
8. `Debug.Log` *does* reach logcat under tag `Unity` in these release builds — `adb logcat -d -s Unity` is a usable diagnostic channel.

**Still needs a human**
- **Continuous 10-minute *played* run** (needs-device-test). The soak proves memory and the chunk pipeline, but it taps rather than steers, so runs are short. Steps: install `builds/MotionRunner.apk`, play for 10 minutes straight while watching for hitches at chunk boundaries, then check `adb shell dumpsys meminfo com.motionrunner.game` against the numbers above. During the soak one run did reach a score of 8017 (a couple of minutes unbroken), which is encouraging but not the same as a played run.
- **Feel of obstacle spacing and the difficulty ramp under tilt steering.** The 6 m reaction-distance rule and `DifficultyCurve` (one step per 45 s, capped at 8) are first guesses; T-007 is where they get tuned against real play.
- **OPEN_QUESTIONS 9:** whether to freeze the score formula before the first public build, since leaderboards and challenges make it hard to change later.

**Next**
T-007 (tune the pacing curve and chunk difficulty bands — the scaffolding and constraint tests already exist), then T-006 (art pass, which is also when chunk data becomes ScriptableObjects), then T-008 (Daily Run, which only needs to swap the seed source: `RunSession.StartRun` is the single place a run's seed is chosen).

**Follow-up the same day — Unity splash screen removed (owner request)**
`PlayerSettings.SplashScreen.show/showUnityLogo = false`, set in `BuildScript.BuildAndroid` rather than left to the editor UI so a headless build can never quietly ship it again, plus `m_ShowUnitySplashScreen/Logo: 0` in `ProjectSettings.asset` so the repo and the editor agree. Allowed for Unity 6 Personal per `docs/LICENSING_REVENUE.md` §2.12 — **the attribution line ("… was made with Unity®…") becomes required only if we later add a credits screen, so whoever builds that screen owns that line.** Verified on device with a burst of screenshots over the first ~2.5 s after launch: launch → brief black engine boot → gameplay, score already at 1 by the third frame, no logo. The short black frame before the first rendered frame is engine startup and cannot be removed; a custom static splash image would only replace it with artwork (worth considering in T-006).

## 2026-08-20 (night) — T-001 done; prototype live on device (orchestrator session)
- APK built headlessly, installed on the owner's OnePlus Nord 2 (DN2103, our mid-tier perf target), renders correctly (verified by adb screenshot). No runtime errors.
- **Three build gotchas fixed — future agents take note (all stem from our code-first/no-scene-content approach):**
  1. Shader stripping: nothing referenced the default shader → magenta. Fix: ship materials as assets.
  2. URP Global Settings: assigning a pipeline via script does NOT create the global settings asset the editor UI creates implicitly → black screen. Fix in `ProjectSetup.EnsureUrpGlobalSettings()` (reflection call to `UniversalRenderPipelineGlobalSettings.Ensure`).
  3. Player builds give `CreatePrimitive` objects the built-in Standard material (URP-incompatible) → use `RuntimeMaterials.Lit(color)` (clones `Assets/Resources/Materials/PrimitiveLit.mat`). Never rely on primitive default materials; `Assets/link.xml` preserves the physics module for CreatePrimitive colliders.
- Headless workflow proven end-to-end: `ProjectSetup.SetupUrp` → `BuildScript.BuildAndroid` → adb install/launch/screencap. Rebuild cycle ≈ 3–5 min.
- **Waiting on owner:** tilt-feel verdict on device (T-002 gate); Play Console registration (P1).

## 2026-08-20 (evening) — T-001/T-002 underway (orchestrator session)
- Unity 6000.5.9f1 installed by owner with Android + iOS modules ✅. Git repo initialized (`main`), Unity .gitignore added, no commits yet.
- Project created at `game/` (batchmode). Packages added at editor-recommended versions: URP 17.5.0, Input System 1.20.0, Test Framework 1.7.0; `com.unity.multiplayer.center` removed (D9). Android target switch: exit 0.
- T-002 prototype code written, fully code-first (no authored scene content): `IGameInput` + Gyro/Touch/Keyboard adapters + CompositeInput, `RunnerController` (3-lane steer + jump), `RoadScroller` (12-segment recycling road), `GameBootstrap` (RuntimeInitializeOnLoadMethod builds everything). `BuildScript.BuildAndroid` for headless APK builds (IL2CPP/ARM64, portrait; package name `com.motionrunner.game` is a PLACEHOLDER — finalize before first Play upload, it is immutable there).
- First APK build running headlessly → `builds/MotionRunner.apk`.
- **Needs human:** plug in the Android phone with USB debugging enabled (feel test = T-002 acceptance); Play Console signup (P1); gyro only testable on device.
- Note: URP packages installed but the render pipeline asset isn't configured yet — prototype renders via built-in pipeline defaults; URP asset setup is part of finishing T-001.

## 2026-08-20 (later) — owner answers to blocking questions
- No existing Play developer account (old Google account ≠ developer account) → 12-testers × 14-days closed testing applies. D11 recorded; P1 + H6 are now the most urgent human actions.
- No Mac; friend with an Apple Developer account is the iOS path (export Xcode project from Windows, friend uploads). iOS is optional, not blocking.
- Android phone available → Track A can start as soon as Unity is installed on this Windows machine (D1 in PREREQUISITES).

## 2026-08-20 — project scaffolding (orchestrator session)
- Verified all Shipaton 2026 facts against live Devpost/rules/docs; final spec: `shipaton_motion_runner_handoff_v2.md`.
- Created agent workspace: CLAUDE.md, docs/{PREREQUISITES,BACKLOG,OPEN_QUESTIONS,DECISIONS,STATUS}.md.
- Feasibility research done: Inference Engine + BlazePose confirmed as primary CV path (official Unity sample + Apache-2.0 models); Layers has a Unity UPM SDK; RevenueCat Unity SDK has Paywalls but NOT Galaxy Store billing.
- **Waiting on human:** PREREQUISITES P1–P3 (store accounts) and OPEN_QUESTIONS 1–3 (Play account age, Mac access, team size). Track A can start as soon as Unity is installed (D1 in prerequisites); no answer needed for T-001/T-002.
