# Status journal (newest at top)

Keep entries short: what changed, how it was verified, what needs a human. Durable operational
knowledge does **not** belong here — invariants go in code comments, recurring traps go in the
"Known gotchas" list in CLAUDE.md. Old entries may be pruned once their content lives elsewhere.

## 2026-08-23 — T-011/T-012/T-013: camera mode is in the game, on the `camera-feature` branch (camera-feature session)

**Headline: the game now boots to a mode menu, and camera mode drives it end-to-end on the
device.** Owner chose OPEN_QUESTIONS 7(d) on 22 Aug; this implements it. On the Nord 2 the full
chain ran unattended: speed gate **3.7 ms blocking median → PASS**, camera permission, orientation
probe settled at score 0.89 (again overruling the device's wrong rotation report, 270 → 90+flip),
face acquired, run started hands-free. `main` still holds v1.0 untouched; this branch's APK
installs as **`com.ferrabled.veyro.camdev`** ("Veyro Cam Dev", `builds/VeyroCamDev.apk`, 44.8 MB)
so it can never replace the working game on a phone.

**How it fits together** (all new code except the adapter lives outside Assembly-CSharp):
- `MotionRunner.Pose.FaceSteering` (engine-free, clock-free): face position → LEFT/RIGHT (neutral
  band + dead zone + low-pass), JUMP (upward velocity + refractory), SLIDE (sustained crouch +
  hold time), loss decay, slow neutral drift. 13 EditMode tests drive synthetic 30 Hz
  trajectories; writing them caught a real design bug — standing up from a crouch is a jump's
  exact velocity signature, so ending a slide now arms the jump refractory.
- `MotionRunner.CameraInput` (new ungated asmdef): `CameraFeed` + `BlazeAffine` moved here from
  the spike (spike asmdef now references this one), plus `FaceAnchors` (the 896 BlazeFace anchors
  generated in code), `FaceDetector` (BlazeFace short-range, CPU backend, argmax folded into the
  graph, startup self-check on the box tensor width), `FaceTrackingRig` (state machine:
  gate → permission → camera → probe → track; the gate runs *before* the permission ask so a
  too-slow device never gets prompted).
- `CameraFaceInput : IGameInput` (in Assembly-CSharp with the other adapters — asmdefs cannot
  reference Assembly-CSharp, so the adapter lives beside GyroTiltInput). Stale observations
  (>0.35 s) read as loss.
- `ModeSelectMenu` (code-first UGUI): TILT & TOUCH vs CAMERA (BETA), rig states narrated
  on-screen, every camera failure falls back to the menu with the reason. Camera mode keeps
  touch+keyboard in the composite as backup. Last choice remembered (`veyro.input_mode`), menu
  shown every launch.
- **No downloads, ever**: the 418 KB BlazeFace ONNX is committed at
  `Assets/Scripts/CameraInput/Resources/CameraInput/` and ships in the APK. `docs/cv-spike.sh off`
  no longer removes `com.unity.ai.inference` — on this branch it is a shipping dependency
  (committed in the manifest; the APK carries its ~8.8 MB and CAMERA by design).
- `BuildScript.BuildAndroidCamDev` builds the dev-flavoured APK; `BuildAndroid` is untouched and
  still produces the real package name for the eventual post-release update (D2).

**Verified:** 140 EditMode tests green (127 + 13 new). On device: menu renders; TILT & TOUCH
starts a normal run (screenshot); CAMERA (BETA) ran the full pipeline unattended — `[CAM]` logcat
lines show gate PASS at 3.7 ms, orientation settled, and the run started from a face sighting.
No runtime exceptions; memory 332 MB PSS with camera+inference live (game alone was ~231 MB).
One build-loop trap hit: launching an APK build seconds after a `-runTests` run on the same
scratch copy deadlocks Unity ("More than one copy of bee_backend running" → build waits forever
at Compiling Scripts). Leave a gap or check for lingering `bee_backend` before batchmode builds.

**Needs a human (the fun part)**
- **Play it.** `builds/VeyroCamDev.apk` is installed. Prop the phone up, pick CAMERA (BETA), step
  back 1.5–2.5 m: lean to steer, hop to jump, crouch to slide. The verdict this needs: does
  steering feel 1:1, do jumps fire when you hop (and only then), does the short-range model still
  see you at play distance in your room's light?
- Tuning knobs live in `FaceSteering` (all public fields, defaults are first guesses):
  `HalfRangeX` (lean sensitivity), `JumpVelocity`, `SlideDrop`. Report what feels wrong.
- T-012's false-positive AC (<1/min standing still) needs a few timed minutes in frame.

**Next:** owner playtest → tune → then T-020 RevenueCat (unstarted, still the critical path;
Play Console account requested and pending). Do not merge to `main` until v1.0 has shipped —
this branch deliberately trades the clean 29.6 MB/INTERNET-only baseline for camera mode.

## 2026-08-22 — T-010b feasibility sweep: **the 30 ms gate is beatable — 4.2 ms, but not with BlazePose** (track-b/cv-feasibility)

**Headline: YES, camera control clears 30 ms on the Nord 2 — by switching models, not backends.**
BlazeFace short-range (418 KB, HF `unity/inference-engine-blaze-face`, Apache-2.0) on the **CPU
(Burst) backend with a blocking readback medians 4.2 ms** isolated (p95 8.3), and **9.1 ms inside a
live 60 FPS loop at 59.4 Hz with 0 of 359 frames over 20 ms**. Face centre-x is LEFT/RIGHT, face-y
velocity is JUMP, sustained face-y drop is SLIDE — all three signals the game needs, from one model
36x smaller than the pose detector. Measured on-device over three runs with the same harness as
T-010 (`CvFeasibilitySweep`, logged as `[CV] FEAS` lines; T-010's own benchmark re-ran alongside as
the control and reproduced within run-to-run variance).

**The one table that matters** ("isolated" = uncapped, like T-010's table; "live" = running
continuously while the app renders under a 60 FPS cap, empty scene + camera feed):

| model | backend | readback | isolated med | live: inference / rate / frames >20 ms |
|---|---|---|---|---|
| **face** | **CPU** | **blocking** | **4.2 ms** (p95 8.3) | **9.1 ms / 59.4 Hz / 0/359** |
| face | CPU | async await | 33.6 ms (raw 8.4) | 17.0 ms / 50.4 Hz / 0/360, main thread 1.1 ms |
| face | GPUCompute | async | 100.8 ms (raw 23.5) | 66.9 ms / 15.7 Hz / 3/357 |
| face | GPUPixel | async | 58.3 ms (raw 71.9) | — |
| lite landmarker | CPU | blocking | 31.0 ms (p95 45.6) | — |
| lite landmarker | CPU | async | 65.7–67.3 ms | 50.7 ms / **18.3 Hz** / 3/356 |
| lite landmarker | GPUCompute | async | 184–211 ms | 150.4 ms / 6.8 Hz / **43/165** |
| lite landmarker | GPUCompute, ScheduleIterable | async | — | 151.9 ms / 6.4 Hz / 87/240 — slicing does not help |
| no-ML motion centroid 160×120 | CPU | — | 0.05 ms (+1.4 ms sync GPU readback) | — |

**Why T-010's numbers overstated the cost:** an awaited `ReadbackAndCloneAsync` resumes on a frame
tick, so async latency is *quantized to multiples of ~16.7 ms*. T-010's "67.9 ms lite on CPU" was
~43 ms of Burst work plus readback rounding; face-on-CPU reads 33.6 ms async but 4.2 ms blocking.
**T-013's startup gate must measure the blocking CPU cost, not awaited latency**, or it will fail
devices that are actually fine. (This also closes T-010's 118-vs-170 ms puzzle: GPU numbers swing
~±20% run to run with queue warmth and DVFS — the end-of-sweep repeat read 190 vs 210 at start,
same harness. Nothing was wrong with either measurement; the GPU path is just dead anyway.)

**Hypotheses killed, with evidence** (each cost minutes on device; none need revisiting):
- **Quantization (H2): broken in Inference Engine 2.6.1 for these models.** Every attempt threw —
  face+u8 `NullReferenceException`, lite+fp16/u8 `KeyNotFoundException '362'`. The docs also state
  quantization reduces size, not inference time, so this lever was doubly dead.
- **GPUPixel (H3): no rescue.** face 58.3 ms, lite 137.9 ms (raw 461 — worse than its own latency).
- **GPU anything, live (H4): unplayable.** GPUCompute inference alongside rendering hitches badly
  (frame p95 100.8 ms, 43/165 frames over 20 ms) and `ScheduleIterable` slicing makes it *worse*
  (87/240) — dispatch is spread but the GPU is still saturated.
- **The pose detector must never run on the CPU backend.** Creating that worker OOM-killed the
  whole process twice (`ApplicationExitInfo reason=3 LOW_MEMORY` at `importance=100` — foreground —
  with 5 GB free, no crash log, signal 9). A gate/adapter that ever tries it takes the game down
  with it. The face-based scheme doesn't need the detector at all.

**The three answers for OPEN_QUESTIONS 7:**
1. *Is there a configuration that clears 30 ms?* **Yes: BlazeFace short-range, CPU backend.**
   4.2 ms blocking / 17 ms async, measured by `CvFeasibilitySweep` on the device, three runs.
2. *Cheapest usable LEFT/RIGHT/JUMP, at what Hz, leaving 60 FPS?* Same configuration: 30–60 Hz with
   the game's frame rate intact (0 frames over 20 ms in 6 s soaks; async mode costs the main thread
   1.1 ms per inference). Full-pose (lite landmarker, CPU, async) coexists with 60 FPS at 18.3 Hz —
   fine for a skeleton *overlay demo*, still 50 ms latency as an *input*.
3. *Park / invest / drop?* **A fourth option now exists and is the recommendation: build camera
   mode on BlazeFace-on-CPU** (updated in OPEN_QUESTIONS 7). Path B (MediaPipe plugin, H7) is no
   longer needed for performance and was not tested. No-ML optical flow (H6) is nearly free
   (1.5 ms) but unnecessary given the above; signal quality untested.

**Not tested, and why:** H7 MediaPipeUnityPlugin (obsolete — the need it addressed is gone);
BlazeFace *detection reliability at 1.5–3 m play distance* — the short-range variant is trained for
arm's-length faces, and **this is the one open risk in the recommendation**; it needs a person in
frame (below). The full-range BlazeFace variant is not in Unity's HF repo and would need ONNX
conversion — only worth it if the range test fails.

**Changed** (spike-side only; the release surface is verified below):
- `Assets/CV/CvFeasibilitySweep.cs` (new): the H1/H2/H3/H4/H6 harness, kept for re-running on other
  devices. Wired into `CvSpikeController` after the T-010 benchmark.
- `CvSpikeBuild` stages `blaze_face_short_range.onnx` (gitignored in `game/CvModels/`), and
  `docs/cv-spike.sh on` now fetches it alongside the pose models, so the sweep is reproducible.
- Main `Packages/manifest.json` was never touched: the inference package was enabled only in the
  batchmode scratch copy. The committed tree stays in the "spike off" state throughout.

**Correction to T-010's "release untouched" claim, found by the verification diff:**
`Assets/CV/Resources/Cv/` (81 KB anchors.csv + the affine compute shader) was shipping in every
release APK — Unity includes all `Resources/` content regardless of the asmdef gate. Fixed by
moving them to `Assets/CV/Data/` and having `CvSpikeBuild` stage them per spike build exactly like
the models (unstaged in the same `finally`); `.gitignore` now blocks the whole staged tree.

**Verified:** release APK rebuilt from this tree (clean scratch): permissions INTERNET-only,
launcher activity `UnityPlayerGameActivity`, 31,027,111 bytes vs the 21 Aug baseline's 31,007,023 —
the **only** delta is `MotionRunner.Pose.dll` (+20 KB, the ungated engine-free Pose assembly, which
is intended to ship with T-011+; gate it too if zero footprint matters before then). A control
rebuild of the pure 21 Aug tree came out byte-identical to the baseline, so the toolchain itself is
stable. Three instrumented device runs; runs 1–2 died to the detector-on-CPU OOM (that's how it was
found), run 3 completed the full sweep in 53 s with logcat streamed to disk. The verification also
caught a polluted scratch copy masquerading as a tree change (a leftover
`Assets/Plugins/Android/AndroidManifest.xml` flipping the launcher activity and adding VIBRATE, and
a `SENTIS_ANALYTICS_ENABLED` define the inference package wrote into ProjectSettings that outlived
its removal) — now CLAUDE.md gotcha 11.

**Needs a human**
- **OPEN_QUESTIONS 7, updated in place** — the options changed materially in camera mode's favour.
- **The range test (10 min, needs a body):** install `builds/VeyroCvSpike.apk`, stand 1.5–3 m from
  the front camera in game-typical light, and check the detector score stays high — the spike's
  overlay currently tracks *pose*, so treat this as approximate until a face overlay exists
  (~half a day, only worth it if OPEN_QUESTIONS 7 resolves to "build it").
- Sanity-check the SLIDE mapping (sustained face-y drop = crouch) feels acceptable before T-012
  encodes it.

**Next:** nothing in Track B until OPEN_QUESTIONS 7 is answered. T-020 RevenueCat remains the
critical path and is untouched by all of this.

## 2026-08-22 — T-010 done: BlazePose runs on device, and **misses the 30 ms gate by 4x** (track-b/cv-spike)

**Headline: NO.** On the OnePlus Nord 2 (Dimensity 1200, Mali-G77 MC9, Vulkan), the BlazePose
**lite landmarker medians 118 ms** per inference — the T-013 gate is 30 ms. The detector medians
217 ms, so a full detect+track cycle is ~334 ms (**~3 Hz**). Camera mode as a real-time input is
not viable on this hardware over Path A. Measured over 57 warm inferences; warm-up discarded.

AC met: 33 landmarks drawn over the live front-camera feed, tracking a person, verified by
screenshot. `[CV] STATS` / `[CV] VERDICT` lines are in logcat under tag `Unity`.

**Isolated benchmark** (no body needed — this is the same measurement T-013's startup gate will
run). "latency" = schedule → await readback, one at a time, which is what a frame pays. "raw" =
20 scheduled back to back with one readback, which is the GPU cost with await and frame-boundary
overhead removed. Frame rate uncapped during the sweep, or every number would round up to a
multiple of 16.7 ms:

| model | backend | latency median | p95 | raw per inference |
|---|---|---|---|---|
| pose detector | GPUCompute | 183.8 ms | 187.0 | 145.7 |
| landmarker lite | GPUCompute | 169.9 ms | 205.7 | 110.9 |
| landmarker full | GPUCompute | 233.9 ms | 253.2 | 123.2 |
| landmarker lite | **CPU (Burst)** | **67.9 ms** | 93.5 | **43.6** |

Two things fall out of that table, and both matter more than the headline number:

1. **Raw ≈ latency.** Removing per-await overhead saves ~35 %, not 4x. The model really is this
   expensive here; no amount of pipelining or frame-skipping rescues it.
2. **The CPU backend is 2.5x faster than GPUCompute** on the same model. Inference Engine's generic
   compute shaders are badly suited to this Mali GPU — exactly the risk handoff 8.2 flagged. Worth
   knowing before anyone spends a day optimising the GPU path. Even so, CPU at 43.6 ms raw is still
   above 30 ms, so switching backend does not by itself clear the gate.

**Changed** (nothing in Track A, C or D; deleting `Assets/CV`, `Assets/Scripts/Pose` and
`Assets/Editor/CvSpikeBuild.cs` would leave the game exactly as it was):
- New engine-free assembly **`MotionRunner.Pose`** (`noEngineReferences: true`, same pattern as
  `MotionRunner.Track`): `PoseJoint`/`PoseLandmark`/`PoseFrame`, `PoseSkeleton`, `PoseGeometry`
  (torso centre, shoulder width — T-012's seam), `LatencyStats` (median/p95 + the `<30 ms` gate),
  `PoseAffine2x3`/`FrameOrientation`, `OrientationProbe`. Clock-free and injected-input, like
  `DailySeed`, so T-012 can state a false-positive rate and measure it headlessly.
- New Unity-side `MotionRunner.Cv` (`Assets/CV/`): `BlazeAffine` (ported from Unity's Apache-2.0
  `BlazeDetectionSample/Pose`), `BlazePoseRunner`, `CameraFeed`, `PoseOverlay` (UGUI), `CvBenchmark`,
  `CvSpikeBootstrap`/`CvSpikeController`.
- `com.unity.ai.inference` **2.6.1** (sample targets 2.4.0; no API drift, and 2.6.0 fixes a
  GPU-compute convolution-padding crash worth having). **Not left in the manifest** —
  `docs/cv-spike.sh on|off` toggles it, for the reason under "keeping the spike out of the game".
- `Assets/Editor/CvSpikeBuild.cs` builds a **separate** `builds/VeyroCvSpike.apk`, package
  `com.ferrabled.veyro.cvspike`, so it installs beside the game instead of replacing it.

**Keeping the spike out of the game** needed all four of the measures below, and the last two were
found only by rebuilding the release APK and diffing it against the 21 Aug baseline. Guarding the
entry point was *not* enough, and nothing in the editor warned about it:

| release APK | size | CAMERA permission |
|---|---|---|
| 21 Aug baseline, pre-CV | 29.6 MB | no |
| CV code compiled in | 44.6 MB | **yes** |
| + `MotionRunner.Cv` behind `defineConstraints` | 38.0 MB | no |
| + package out of the manifest | 29.6 MB | no |

1. Its own package name, so the spike installs beside the game.
2. The `RuntimeInitializeOnLoadMethod` hook is behind `VEYRO_CV_SPIKE`.
3. **`MotionRunner.Cv` is gated on that define via `defineConstraints`.** Unity scans compiled
   assemblies for `WebCamTexture` and had added CAMERA to the manifest of a game that does not use
   a camera, plus 15 MB. Cost: the editor no longer type-checks the CV code by default — a spike
   build is what compiles it, and it fails loudly.
4. **The package is not in the committed manifest.** Inference Engine ships a large compute-shader
   library in its own Resources, which Unity puts in the APK whenever the package is present,
   referenced or not — 8.8 MB for nothing. `docs/cv-spike.sh on|off` toggles the package and
   fetches the weights, and is how anyone reproduces this spike.

The 20 MB of weights are separately staged into `Resources` by the spike build and unstaged in a
`finally`, so they never reach a release APK either.

**Verified:** 127 EditMode tests green (81 before, 46 new). Spike APK built headless in a scratch
copy and run on the phone. Release APK rebuilt from this tree is byte-for-byte the baseline size
with no extra permissions, so Track A is untouched.

**Two bugs the device caught**
- *The orientation probe trusted noise.* With nobody in frame all eight candidates scored ~0.14 and
  it locked onto `rot=0`, overruling the device's `rot=270`, because 0.147 beat 0.146. It now needs
  the winner to clear a confidence floor, otherwise it keeps the device report and re-probes.
  Three tests cover it, one replaying that exact noise pattern.
- *The device's reported rotation is wrong here anyway.* With a person in frame the probe settled
  confidently (score 0.75) on `rot=90` while `videoRotationAngle` reports 270. Building the probe
  instead of trusting the API was the right call, and T-011 should keep it.

**Needs a human**
- **The scope call this number forces** — OPEN_QUESTIONS 7. Recommended default: park camera mode
  as a *demo* (a menu toggle at ~3 Hz, hands-free but not playable) and do not put it in the pitch
  as an input mode. T-011–T-013 stay unstarted until that is answered.
- Where the BlazePose weights should live once camera mode ships — OPEN_QUESTIONS 8.
- A device with a different GPU would be worth 10 minutes: this verdict is one SoC wide.

**Next:** nothing in Track B until OPEN_QUESTIONS 7 is answered. T-020 RevenueCat remains the
critical path.

## 2026-08-21 — T-003, T-005, T-008 done; named "Veyro Run" (track-a/chunks-session)

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

**T-008 Daily Run (same day).** `RunMode.Daily` is now the default: the seed is the UTC date
(`DailySeed`, clock-free — the caller passes the date in, so generation stays deterministic).
Best scores split into all-time and today-only under `veyro.*` PlayerPrefs keys; the daily bucket
self-resets on date rollover, so it stays one key rather than one per day for ever.

Found and fixed while doing it: the seed mixed in `Application.version`, so **a bugfix release would
have forked the day's players onto different tracks**. The seed now carries
`ChunkLibrary.ContentVersion` instead — bump that, and only that, when the chunk library changes.
`RunSeed.GameVersion` was renamed `ContentVersion` so nobody wires the app version back in.

**Verified:** 81 EditMode tests green (12 new, incl. pinned RNG states for known dates). On device,
two cold launches produced byte-identical runs — same seed `20260821/greybox-1/greybox`, score,
distance and chunk count — the AC demonstrated on real hardware. HUD shows `DAILY · <date>`.

**Needs a human**
- A continuous 10-minute *played* run. The soak taps rather than steers, so its runs are short.
  Install `builds/MotionRunner.apk`, play 10 min watching for hitches at chunk boundaries, then
  compare `adb shell dumpsys meminfo com.ferrabled.veyro.run` against the numbers above.
- Feel of obstacle spacing and the difficulty ramp under tilt. Both are first guesses; T-007 tunes
  them against real play.
- Move the settled app name into DECISIONS.md (owner-only). Answer OPEN_QUESTIONS 6 (score freeze).
- Whether to rename the `MotionRunner.*` C# namespace to match the product. Cosmetic and invisible
  to players; safe while everything is code-first; cheapest before T-006/T-020 grow the surface.
  (The old `motionrunner.best_score` key is gone — T-008 restructured best-score storage while there
  are still no players, which was the free moment to do it.)

**Next:** T-020 RevenueCat as soon as P4 exists (it is the contest eligibility gate), then T-007
(curve + difficulty bands — scaffolding and constraint tests exist, needs the owner's played-run
feedback) and T-006 (art pass, and where chunk data becomes ScriptableObjects).

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
