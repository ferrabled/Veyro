# Status journal (append newest at top)

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
