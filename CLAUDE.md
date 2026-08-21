# Motion Runner — agent guide

Motion-controlled 3D endless runner for RevenueCat Shipaton 2026. Phone tilt (later: camera body-tracking) controls the character; optional TV mirroring. **Hard deadline: submission 30 Sep 2026 @ 11:45pm PDT. Store release target: v1.0 in review by ~12 Sep 2026.**

## Canonical documents (read before working)

- `shipaton_motion_runner_handoff_v2.md` — the full spec: product, architecture, sponsor strategy, phases. This is the source of truth for scope decisions.
- `docs/BACKLOG.md` — task list with acceptance criteria. Pick tasks from here.
- `docs/DECISIONS.md` — decisions already made. Do not relitigate them; append new ones.
- `docs/OPEN_QUESTIONS.md` — questions only the human owner can answer. If blocked, write your question there, mark the task blocked in the backlog, and move to another task. Never guess on store accounts, payments, hardware, or scope.
- `docs/STATUS.md` — append a dated entry when you finish or block a task.
- `docs/PREREQUISITES.md` — human-only setup checklist (accounts, hardware).
- `docs/LICENSING_REVENUE.md` — Unity Personal / RevenueCat / store-economics compliance analysis. Constraints there are binding (e.g., attribution line if a credits screen exists; no artificial revenue).

## Repo layout

- `game/` — Unity project (Unity 6 LTS, 6000.x). Created by T-001; until it exists, nothing else can proceed.
- `docs/` — process files listed above.
- Root `.md` files — handoff spec.

## Engineering rules

1. **Code-first Unity.** Minimal scenes; wire systems from a single bootstrap scene in code (ScriptableObjects for data, prefabs only for visual assets). Scenes/prefabs are YAML merge hazards — one owner per scene, never two tracks editing the same scene file.
2. **Input goes through `IGameInput`** (GetMoveAxis / IsJumpPressed / IsSlidePressed / IsSpecialPressed). Adapters: GyroInput, TouchInput, CameraPoseInput. Gameplay code never touches sensors, cameras, or ML directly.
3. **CV never blocks release.** Camera mode is a parallel track (handoff §8); v1.0 ships with gyro+touch regardless of CV status. Primary CV path: BlazePose via Unity Inference Engine (`com.unity.ai.inference`), models from Hugging Face `unity/inference-engine-blaze-pose`. On-device only — no servers.
4. **Determinism.** Track generation must be reproducible from (seed, version, worldId). No `System.Random` without an injected seed; no time-based randomness in generation.
5. **Third-party SDKs**: RevenueCat `purchases-unity` ≥ 8.4.0 (Paywalls supported), OneSignal Unity SDK, Layers `com.layers.analytics` (UPM). Keep each behind a thin wrapper so tests run without them.
6. **Verification.** Prefer Unity batchmode for headless checks: `Unity -batchmode -projectPath game -runTests -testPlatform EditMode` (PlayMode where needed). Anything requiring a physical device (gyro feel, camera, IAP, mirroring latency) → mark the task "needs human device test" in STATUS.md with exact steps to run. Batchmode **fails outright while the owner has `game/` open in the editor** — copy the project to a scratch dir and run there, then verify the copy really got your edits (see STATUS 21 Aug, gotcha 4).
7. **Performance budget**: 60 FPS on mid-tier Android; CV inference ≤ every 2nd–3rd frame, lite model, 640×480 camera feed.

## Running the game

- **In editor:** open `game/Assets/Scenes/Main.unity` (intentionally empty) and press Play — `GameBootstrap` builds everything at runtime. Keyboard: A/D or arrows steer, Space jumps. Never hand-edit scene content.
- **On device:** `Unity -batchmode -quit -projectPath game -buildTarget Android -executeMethod MotionRunner.EditorTools.BuildScript.BuildAndroid` → `builds/MotionRunner.apk` → adb install/launch/screencap (see STATUS 20 Aug for the proven loop). Materials: always `RuntimeMaterials.Lit(color)` — never primitive default materials (URP + stripping, see STATUS).

## Working protocol for agents

1. Read this file, then the backlog. Claim a task by putting your session/track name next to it.
2. Respect task acceptance criteria; if criteria are wrong or incomplete, fix them in the same change and note it in STATUS.md.
3. Blocked → OPEN_QUESTIONS.md (one question per line item, include context + your recommended default). Don't idle on a blocked task.
4. Finished → STATUS.md entry: what changed, how it was verified, what's next.
5. New irreversible/scope decision needed → propose it in OPEN_QUESTIONS.md; only the human moves it to DECISIONS.md.
6. Timebox spikes: if a spike exceeds its timebox, write down findings and stop — the schedule outranks completeness.
