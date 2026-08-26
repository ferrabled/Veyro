# Motion Runner — agent guide

Motion-controlled 3D endless runner for RevenueCat Shipaton 2026. Phone tilt (later: camera body-tracking) controls the character; optional TV mirroring. **Hard deadline: submission 30 Sep 2026 @ 11:45pm PDT. Store release target: v1.0 in review by ~12 Sep 2026.**

## Canonical documents (read before working)

- `shipaton_motion_runner_handoff_v2.md` — the full spec: product, architecture, sponsor strategy, phases. This is the source of truth for scope decisions.
- `docs/BACKLOG.md` — task list with acceptance criteria. Pick tasks from here.
- `docs/DECISIONS.md` — decisions already made. Do not relitigate them; append new ones.
- `docs/OPEN_QUESTIONS.md` — questions only the human owner can answer. If blocked, write your question there, mark the task blocked in the backlog, and move to another task. Never guess on store accounts, payments, hardware, or scope.
- `docs/STATUS.md` — append a dated entry when you finish or block a task.
- `docs/PREREQUISITES.md` — human-only setup checklist (accounts, hardware).
- `docs/TRACK_GENERATION.md` — how the track, chunks, obstacles, difficulty and assets fit together, and which knob does what. Read it *only* when changing track content or the art on top of it; skip it otherwise.
- `docs/COSMETICS_CATALOG.md` — the store catalog (products/entitlements/offering, Season 1 pass, item list) and the product-vs-entitlement-vs-item rules. Read before touching commerce, cosmetics, or either store dashboard. `docs/REVENUECAT_PLAN.md` is the companion integration plan (SDK version, wrapper seam, judge path).
- `docs/PLAY_CONSOLE_SETUP.md` — ordered human-only Play Console checklist (listing → testers → products → RevenueCat credentials). Agents: read it to know what the owner has/hasn't done; never assume a console step happened.
- `docs/LICENSING_REVENUE.md` — Unity Personal / RevenueCat / store-economics compliance analysis. Constraints there are binding (e.g., attribution line if a credits screen exists; no artificial revenue).
- `docs/STORE_COMPLIANCE.md` — Google Play declaration state + the per-release "flip table". Read it before any release that adds an SDK, permission, purchase, login, or network feature; update it in the same change.

## Repo layout

- `game/` — Unity project (Unity 6 LTS, 6000.x). Created by T-001; until it exists, nothing else can proceed.
- `docs/` — process files listed above.
- Root `.md` files — handoff spec.

## Engineering rules

1. **Code-first Unity.** Minimal scenes; wire systems from a single bootstrap scene in code (ScriptableObjects for data, prefabs only for visual assets). Scenes/prefabs are YAML merge hazards — one owner per scene, never two tracks editing the same scene file.
2. **Input goes through `IGameInput`** (GetMoveAxis / IsJumpPressed / IsSlidePressed / IsSpecialPressed). Adapters, as they actually exist: `GyroTiltInput`, `TouchTapInput`, `CameraFaceInput`, `KeyboardInput` (Editor), composed by `CompositeInput`. Gameplay code never touches sensors, cameras, or ML directly.
3. **CV never blocks release.** Camera mode is built behind a menu choice and always falls back to tilt+touch; a slow device is gated out before the permission ask. Shipping CV path: **BlazeFace** (face detector only, CPU backend, 418 KB ONNX committed under `game/Assets/Scripts/CameraInput/Resources/`) via Unity Inference Engine (`com.unity.ai.inference`). **BlazePose is abandoned** — it missed the 30 ms gate at 118 ms median on the Nord 2 (T-010); its ~20 MB weights stay gitignored and are used only by the benchmark spike. On-device only — no servers.
4. **Determinism.** Track generation must be reproducible from (seed, version, worldId). No `System.Random` without an injected seed; no time-based randomness in generation.
5. **Third-party SDKs**: RevenueCat `purchases-unity` pinned to **exactly `9.8.1`** — not "≥ 8.4.0"; the rationale and the drift it avoids are in `docs/REVENUECAT_PLAN.md` §1. Plus OneSignal Unity SDK and Layers `com.layers.analytics` (UPM). Keep each behind a thin wrapper so tests run without them.
6. **Verification.** Prefer Unity batchmode for headless checks: `Unity -batchmode -projectPath game -runTests -testPlatform EditMode` (PlayMode where needed). Anything requiring a physical device (gyro feel, camera, IAP, mirroring latency) → mark the task "needs human device test" in STATUS.md with exact steps to run.
7. **Performance budget**: 60 FPS on mid-tier Android; CV inference ≤ every 2nd–3rd frame, lite model, 640×480 camera feed.

## Running the game

- **In editor:** open `game/Assets/Scenes/Main.unity` (intentionally empty) and press Play — `GameBootstrap` builds everything at runtime. Keyboard: A/D or arrows steer, Space jumps. Never hand-edit scene content.
- **CV spike (T-010, off by default):** `docs/cv-spike.sh on`, then `-executeMethod MotionRunner.EditorTools.CvSpikeBuild.BuildAndroid` → `builds/VeyroCvSpike.apk`, a separate app (`com.ferrabled.veyro.cvspike`). `docs/cv-spike.sh off` when done. This is the **BlazePose benchmark only** — it is not the shipping camera path and it is not parked work. Camera mode itself is built (T-011/T-012/T-013, BlazeFace) and **ships in v1.0**, camera included, per the 24 Aug owner call (OPEN_QUESTIONS 12).
- **On device:** `Unity -batchmode -quit -projectPath game -buildTarget Android -executeMethod MotionRunner.EditorTools.BuildScript.BuildAndroid` → `builds/MotionRunner.apk` → adb install/launch/screencap (see STATUS 20 Aug for the proven loop). Materials: always `RuntimeMaterials.Lit(color)` — never primitive default materials (URP + stripping, see STATUS).

## Known gotchas (each one cost real time — do not rediscover them)

1. **Shader stripping:** nothing referencing the default shader ⇒ magenta. Materials must ship as assets.
2. **URP Global Settings:** assigning a pipeline by script does *not* create the global settings asset the editor UI creates implicitly ⇒ black screen. `ProjectSetup.EnsureUrpGlobalSettings()` handles it.
3. **Never use primitive default materials.** Player builds give `CreatePrimitive` objects the built-in Standard material (URP-incompatible). Always `RuntimeMaterials.Lit/Shared(color)`. `Assets/link.xml` must keep preserving the physics module, because `CreatePrimitive` itself needs it.
4. **Batchmode fails while the owner has `game/` open in the editor.** Copy the project to a scratch dir and run there — then *verify the copy actually got your edits* before trusting a result. A silently-failed sync once produced a build containing none of the changes. If you copy `Library/` along (worth it — it turns a ~20 min cold import into ~20 s), **delete `Library/Bee/Android/Prj` in the copy before the first Android build**: the Gradle staging area holds absolute paths back to the original project, and `mergeDexRelease` dies with "file … is located outside the root directory". `Library/Bee/artifacts` (the IL2CPP object cache) is path-independent — keep it. Also sync with `robocopy /E`, never `/MIR`, or each sync deletes the copy's generated `.meta` files and forces a full reimport.
5. **Android activity is `com.unity3d.player.UnityPlayerGameActivity`** (Unity 6 GameActivity), not `UnityPlayerActivity`. Launch with `adb shell monkey -p com.ferrabled.veyro.run -c android.intent.category.LAUNCHER 1`.
6. **Git Bash rewrites unix-looking args into Windows paths**, breaking `adb shell screencap /sdcard/x.png`. Prefix device-path commands with `MSYS_NO_PATHCONV=1` — but keep plain Windows paths for local `adb install`/`adb pull` destinations, which the same flag breaks. Never `adb pull /sdcard/` (pulls the whole phone).
7. **UGUI is not in a fresh Unity 6 manifest.** `com.unity.modules.ui` gives only `UnityEngine.UIModule` (Canvas, RectTransform); `Text`/`Image`/`Button`/`CanvasScaler` need `com.unity.ugui`, which ships inside the editor install and resolves offline.
8. `Debug.Log` reaches logcat under tag `Unity` in release builds — `adb logcat -d -s Unity` is a usable diagnostic channel.
9. **`RunSeedTests` pins the seed hash and PRNG stream.** A failure there is a content break needing a `WorldId` bump — never "just update the expected value", or every already-played Daily Run seed changes.
10. **Adding a UPM package can change the release APK even if nothing references it.** `com.unity.ai.inference` put 8.8 MB of compute shaders into the game build while unreferenced, and having *any* compiled code mention `WebCamTexture` made Unity add `android.permission.CAMERA` to a game that has no camera. Guarding the entry point is not isolation. After adding a package, rebuild the release APK and diff it against the last one — `aapt2 dump permissions` plus the file size — and gate optional code with an asmdef `defineConstraints`, not just `#if`. The CV spike is toggled on and off with `docs/cv-spike.sh`.
11. **A reused scratch copy accumulates state that silently changes builds** — robocopy `/E` never deletes, and both Unity and packages write back into the project. Found 22 Aug: a leftover `Assets/Plugins/Android/AndroidManifest.xml` (debris from running a sample project there) switched the release APK's launcher activity to `AppUIGameActivity` and added VIBRATE, and `com.unity.ai.inference` had written a `SENTIS_ANALYTICS_ENABLED` define into the scratch's ProjectSettings that outlived the package's removal. Before trusting a *verification* build from a reused scratch, `diff -rq` its `Assets/` and `cmp` its `ProjectSettings/ProjectSettings.asset` against the repo and delete anything extra. Relatedly: **Unity ships everything under any `Resources/` folder in every build, gated asmdef or not** — spike assets live in `Assets/CV/Data/` and are staged into Resources per spike build by `CvSpikeBuild`, never committed there.

## Working protocol for agents

1. Read this file, then the backlog. Claim a task by putting your session/track name next to it.
2. Respect task acceptance criteria; if criteria are wrong or incomplete, fix them in the same change and note it in STATUS.md.
3. Blocked → OPEN_QUESTIONS.md (one question per line item, include context + your recommended default). Don't idle on a blocked task.
4. Finished → STATUS.md entry: what changed, how it was verified, what's next.
5. New irreversible/scope decision needed → propose it in OPEN_QUESTIONS.md; only the human moves it to DECISIONS.md.
6. Timebox spikes: if a spike exceeds its timebox, write down findings and stop — the schedule outranks completeness.
