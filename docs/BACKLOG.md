# Backlog

Tracks can be worked by separate agent sessions in parallel. Claim a task by putting your session
name next to it. Full context for every task: `shipaton_motion_runner_handoff_v2.md`.

**Status:** ✅ done · 🚧 in progress · ⬜ todo · ⛔ blocked. A 🚧 or ⛔ must name an owner or a reason.

Schedule anchors: **v1.0 to store review by ~Sep 12 · submission Sep 30.**

## Track A — Core game

- ✅ **T-001** Unity 6 project under `game/`, Android target, portrait, URP (pipeline + global settings + shipped base material). AC: builds an APK that runs and renders correctly on the test phone. — verified on-device 20 Aug
- ✅ **T-002** Runner prototype: auto-run capsule, scrolling 3-lane road, `IGameInput` + Gyro/Touch/Keyboard via `CompositeInput`, steering + jump + slide stub. AC: playable on device, latency subjectively immediate. — owner verdict 20 Aug "feels fine for now" → GO
- ✅ **T-003** Chunk system: `ChunkDefinition` metadata (handoff §4.3), spawner keeps N chunks ahead / recycles behind, seeded deterministic sequence. AC: same seed ⇒ identical sequence twice; 10-min run with zero GC-spike hitches and flat memory. — 21 Aug. AC amended: metadata is plain C# data, not ScriptableObjects (SOs land with T-006). Soak-verified; a continuous 10-min *played* run still needs a human
- ✅ **T-005** Obstacles + collision + scoring (distance, coins, combo) + crash → result screen. AC: full loop playable start → run → crash → result → restart. — 21 Aug, verified on device. Obstacle-spacing feel still wants a human pass
- ⬜ **T-006** First biome art pass: stylized skybox, ground/props for 10–20 chunks, character v1 replaces the capsule. AC: a stranger watching 10 seconds of footage can tell what game this is. — also where chunk data becomes ScriptableObjects, and where a custom splash image would go. Needs Git LFS set up first (D2)
- ⬜ **T-007** Difficulty as a chunk property; pacing curve per handoff §3.2. AC: minute 1 easy, minute 5 hard, no impossible sequences (generator constraint tests). — scaffolding exists (`DifficultyCurve`, per-chunk difficulty bands, constraint tests); what remains is tuning against real play
- ✅ **T-008** Daily Run v0: seed from UTC date + content version; local best score. AC: two devices on the same date produce an identical run. — 21 Aug. `RunMode.Daily` is the default; `DailySeed` is clock-free (the caller passes the date in). AC shown on device: two cold launches produced byte-identical runs (same seed, score, distance, chunk count). 12 new tests, incl. pinned RNG states for known dates
- ⬜ **T-009** *(post-v1.0)* Cross-device progress without a backend: Google Play Games Services Saved Games (Android) + iCloud key-value (iOS) for best score / streak / XP. AC: uninstall + reinstall on a second device restores progress. — RevenueCat entitlements already survive a device change via the store account, so this covers only local progression. Deliberately not our own backend (D10); Play Games sign-in also yields a stable player ID that could later become the RevenueCat app user ID

## Track B — CV / camera mode (parallel learning track; never blocks Track A)

- ✅ **T-010** Inference Engine spike: import `com.unity.ai.inference`, BlazePose models (HF `unity/inference-engine-blaze-pose`), run the official sample on device. AC: 33 landmarks over live camera on the Android phone; median inference ms logged. Timebox: 3 days — 22 Aug, done in ~1 day. AC met on the Nord 2. **Verdict: lite landmarker medians 118 ms vs the 30 ms gate** (detector 217 ms, full cycle ~334 ms ≈ 3 Hz); Unity's CPU backend is 2.5× faster than GPUCompute and still misses. Sample reimplemented code-first rather than run as-is — it is scene/prefab-based and the project is not (rule 1). Ships as a separate APK, `com.ferrabled.veyro.cvspike`
- 🔷 **T-011** Camera plumbing for the shipping input (camera-feature session). AC amended for the BlazeFace path the owner chose (OPEN_QUESTIONS 7 → d): face box stable while a person sways; no drift on device rotation; runs on the *detector alone* — there is no landmarker and no tracking-loss loop to rebuild, which deletes the detector-on-loss half of the old AC. Done in `MotionRunner.CameraInput`: `CameraFeed` + `OrientationProbe` reused from the spike, `FaceDetector` (BlazeFace, CPU backend, argmax folded into the graph), `FaceTrackingRig` state machine. **Smoothing lives in FaceSteering (T-012), not here.** Needs the human sway/rotation pass on device
- 🔷 **T-012** Gesture detector (camera-feature session). AC unchanged in spirit, source changed: neutral-band LEFT/RIGHT via **face centre** (not torso), JUMP via upward face velocity, SLIDE via sustained crouch; <1 false positive per minute standing still; measurable headlessly. Done as `FaceSteering` in `MotionRunner.Pose` (engine-free, clock-free): calibration, dead-zone + low-pass axis, jump refractory, slide-release refractory (stand-up ≠ jump), loss decay, slow neutral drift. 13 EditMode tests drive synthetic 30 Hz trajectories. **The false-positive-rate device pass is still owed** — needs a human standing in frame for a few minutes
- 🔷 **T-013** Camera as IGameInput + gate (camera-feature session). Done as `CameraFaceInput : IGameInput` (adapter name changed from `CameraPoseInput` — there is no pose) + `FaceDetector.TryGate`, which measures the **blocking** CPU median (T-010b: awaited latency frame-quantizes and would fail healthy devices). Gate runs before the permission ask, so a too-slow device never gets prompted. Mode chosen in `ModeSelectMenu` at boot; camera failure always falls back to the menu (rule 3). AC still open: **game playable hands-free on device — needs the human playtest**

## Track C — Monetization, retention, growth SDKs

- ⬜ **T-020** RevenueCat: `purchases-unity` ≥ 8.4.0, 1–2 cosmetic SKUs, Paywall Builder paywall, entitlement-driven unlock, judge promo-code/free-trial path. AC: sandbox purchase completes on device; entitlement survives reinstall. — **this is the contest eligibility gate**; needs P4
- ⬜ **T-021** OneSignal Unity SDK: push permission UX, player tags (streak, best score). AC: test notification received on device; first campaign drafted for release day. — needs P5
- ⬜ **T-022** Layers UPM SDK: init + events (run_end, share_tapped, challenge_open, install attribution). AC: events visible in the Layers dashboard. — needs P6
- ⬜ **T-023** RevenueCat Ads spike (Catvertising): does it work from Unity today? AC: written yes/no + evidence in STATUS. Timebox: 1 day. If yes, one rewarded "revive" behind a feature flag
- ⬜ **T-024** Share/challenge payload: deep link or code embedding seed + score; opening it starts that run. AC: round-trip works between two devices

## Track D — Release pipeline & submission (mostly human + agent-prepared assets)

- ⬜ **T-030** Store listing kit: name, description, 1024×1024 icon, screenshots incl. 1179×2556 frameless (Devpost requirement), feature graphic. AC: assets in `docs/store-kit/`, human-approved
- ⬜ **T-031** Closed-testing build to Play, as early as an APK exists — ugly is fine. AC: 12 testers opted in, clock running. — **critical path**; blocked on P1 + H6
- ⬜ **T-032** iOS build path decided (P2/H3) and first TestFlight build. AC: game runs on the iPhone via TestFlight. — bundle ID must match `com.ferrabled.veyro.run`
- ⬜ **T-033** Galaxy Store submission (after v1.0 live): purchases disabled or web-link variant per handoff §11.1; foldable layout check. AC: app listed on Galaxy Store
- ⬜ **T-034** TV mirroring latency test: measure iPhone→AppleTV and Android→Chromecast while playing. AC: written ms estimate + verdict on whether the pitch leads with TV. — needs H2/H4
- ⬜ **T-035** Submission package: ≤2-min video (script per handoff §14.6), category answers for targeted categories only, OneSignal App ID, Layers evidence, #BuildInPublic links, promo codes. AC: Devpost form complete ≥24 h before the deadline

## Content track (continuous, human-led, agent-assisted)

- ⬜ **T-040** #BuildInPublic cadence: ≥3 posts/week tagged #Shipaton #BuildInPublic; every milestone above is a post. Agents draft posts and capture clips when finishing tasks
