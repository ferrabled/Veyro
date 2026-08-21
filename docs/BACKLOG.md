# Backlog

Tasks are grouped by track. Each track can be worked by a separate agent session in parallel. Format: ID, title, acceptance criteria (AC), status (todo / in-progress:<owner> / blocked / needs-device-test / done). Full context for every task: `shipaton_motion_runner_handoff_v2.md`.

Schedule anchors: **go/no-go on tilt-fun by Aug 22 · v1.0 to store review by ~Sep 12 · submission Sep 30.**

## Track A — Core game (owner scene: `Game.unity`)

- **T-001** Create Unity 6 LTS project under `game/`, Android build target, portrait orientation, URP configured (pipeline + global settings + shipped base material). AC: project builds an APK that runs and renders correctly on the test phone. — **done** (verified via on-device screenshot, 20 Aug)
- **T-002** Runner prototype: auto-run character (capsule), scrolling 3-lane road, `IGameInput` + GyroTiltInput + TouchTapInput + KeyboardInput via CompositeInput, steering + jump + slide stub. AC: playable on device; input latency subjectively immediate. — **done** (owner verdict 20 Aug: "feels fine for now" → GO. Fine-tuning of FilterSharpness/FullDeflection continues alongside T-005 gameplay)
- **T-003** Chunk system: `ChunkDefinition` metadata (handoff §4.3), spawner keeps N chunks ahead / recycles behind, seeded deterministic sequence. AC: same seed ⇒ identical sequence twice; 10-minute run with zero GC-spike hitches and flat memory. — **done** (track-a/chunks-session, 21 Aug; AC amended: metadata is plain C# data in `MotionRunner.Track`, not ScriptableObjects — the greybox library has no visual content to author, so SOs land with T-006. 69 EditMode tests green; 10-min device soak flat. Continuous 10-min *played* run still needs a human — see STATUS.)
- **T-005** Obstacles + collision + scoring (distance, coins, combo) + crash → result screen. AC: full loop playable: start → run → crash → result → restart. — **done** (track-a/chunks-session, 21 Aug; full loop verified on the OnePlus Nord 2 by screenshot. Tilt-steered feel of the obstacle spacing still wants a human pass.)
- **T-006** First biome art pass: stylized skybox, ground/props for 10–20 chunks, character v1 replaces capsule. AC: a stranger watching 10 seconds of footage can tell what game this is. — todo
- **T-007** Difficulty as chunk property; pacing curve per handoff §3.2. AC: minute 1 easy, minute 5 hard, no impossible sequences (generator constraint tests). — todo (T-003 landed the scaffolding: `DifficultyCurve.At`, per-chunk difficultyMin/Max, and the constraint tests. What is left is tuning the curve shape and the chunk difficulty bands against real play.)
- **T-008** Daily Run v0: seed derived from UTC date + version; local best score. AC: two devices on the same date produce the identical run. — todo

## Track B — CV / camera mode (parallel learning track; never blocks Track A)

- **T-010** Inference Engine spike: import `com.unity.ai.inference`, BlazePose models (HF `unity/inference-engine-blaze-pose`), run official sample on device. AC: 33 landmarks drawn over live camera on the Android phone; median inference ms logged. Timebox: 3 days. — todo
- **T-011** WebCamTexture handling: rotation/mirroring correct on the test phone front camera, 640×480 feed, detector-on-loss + landmarker-per-Nth-frame loop, one-euro smoothing. AC: landmarks stable while a person sways; no drift on device rotation. — todo
- **T-012** Gesture detector: neutral-band LEFT/RIGHT via torso center, JUMP via vertical velocity threshold. AC: <1 false positive per minute standing still in normal room lighting; logged confusion counts. — todo
- **T-013** `CameraPoseInput : IGameInput` + startup micro-benchmark gate (enable only if median inference < 30 ms). AC: full game playable hands-free on device; ships in Update 2 only if T-012 quality bar holds. — todo

## Track C — Monetization, retention, growth SDKs

- **T-020** RevenueCat: `purchases-unity` ≥ 8.4.0, 1–2 cosmetic SKUs, Paywall Builder paywall, entitlement-driven cosmetic unlock, judge promo-code/free-trial path. AC: sandbox purchase completes on device; entitlement survives reinstall. — todo
- **T-021** OneSignal Unity SDK: push permission UX, player tags (streak, best score). AC: test notification received on device; first campaign drafted for release day. — todo
- **T-022** Layers UPM SDK: init + events (run_end, share_tapped, challenge_open, install attribution). AC: events visible in Layers dashboard. — todo
- **T-023** RevenueCat Ads spike (Catvertising): does RevenueCat Ads work from Unity today? AC: written yes/no + evidence in STATUS.md. Timebox: 1 day. If yes: one rewarded "revive" placement behind a feature flag. — todo
- **T-024** Share/challenge payload: deep link or code embedding seed + score; opening it starts that run. AC: round-trip works between two devices. — todo

## Track D — Release pipeline & submission (mostly human + agent-prepared assets)

- **T-030** Store listing kit: name, description, 1024×1024 icon, screenshots incl. 1179×2556 frameless (Devpost requirement), feature graphic. AC: assets in `docs/store-kit/`, human-approved. — todo
- **T-031** Closed-testing build to Play (if P1 requires it) — as early as an APK exists, ugly is fine. AC: 12 testers opted in, clock running. — todo
- **T-032** iOS build path decided (P/H3) and first TestFlight build. AC: game runs on the iPhone via TestFlight. — todo
- **T-033** Galaxy Store submission (after v1.0 live): purchases disabled or web-link variant per handoff §11.1 caveat; foldable layout check. AC: app listed on Galaxy Store. — todo
- **T-034** TV mirroring latency test (T-004 moved here): measure iPhone→AppleTV and Android→Chromecast while playing. AC: written ms estimate + verdict on whether the pitch leads with TV. — needs-device-test — todo
- **T-035** Submission package: ≤2-min video (script per handoff §14.6), category answers for targeted categories only, OneSignal App ID, Layers evidence, #BuildInPublic links, promo codes. AC: Devpost form complete ≥24h before deadline. — todo

## Content track (continuous, human-led, agent-assisted)

- **T-040** #BuildInPublic cadence: ≥3 posts/week tagged #Shipaton #BuildInPublic; every milestone above is a post. Agents: draft posts + capture clips when finishing tasks. — todo
