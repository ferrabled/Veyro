**MOTION RUNNER**

Shipaton 2026 — Product, Game Design, Technical Architecture & Sponsor Strategy

*Implementation handoff for the development agent.*

| **DECISION** This document is an implementation handoff. Official contest facts were re-verified against the live Devpost page and official docs on **20 August 2026** and are labeled as such; product/engineering choices are working decisions and may be changed after prototype validation. |
|---|

| **Item** | **Current value (verified 20 Aug 2026)** |
|---|---|
| Hackathon | RevenueCat Shipaton 2026 |
| Submission deadline | 30 September 2026 @ 11:45pm PDT |
| Days remaining | **41 days** (from 20 Aug 2026) |
| Total prize pool | $740,000+ in cash (per Devpost) |
| Release requirement | First public release of the app between **Aug 1 – Sep 30, 2026** on the App Store, Google Play, or (new this year) Samsung Galaxy Store. Building before Aug 1 was allowed; updates to previously-released apps do not qualify. |
| Core RevenueCat requirement | "uses the RevenueCat SDK to power at least one **in-app or web purchase**" — or, per the challenge summary, "serve ads through **RevenueCat Ads**" |
| Primary product target | Best Game ($20k) + Grand Prize traction + sponsor overlap |

Source basis: [S1] official Devpost overview (read directly, full prize list quoted below) and [S2] official Devpost rules.

# Contents

1. Executive summary and north-star product
2. Product concept and core user experience
3. Gameplay loop and controls
4. World/track generation
5. Characters, cosmetics, progression and season pass
6. Social, retention and growth mechanics
7. TV/display strategy and multiplatform approach
8. Computer-vision approach (parallel learning track)
9. Game engine decision
10. Recommended technical architecture
11. Sponsor strategy and implementation requirements
12. Release pipeline, MVP scope, roadmap and cut lines
13. Risks, assumptions and open technical questions
14. Handoff checklist for the implementation agent
15. Sources and research notes

# 1. Executive summary and north-star product

Working concept: a mobile motion-controlled endless runner in which the player uses the smartphone's motion sensors (and later, camera-based body tracking) to control a 3D character. The phone renders the game; the user can mirror/cast the phone screen to a TV so the TV acts as the large gameplay display. The core experience should feel like "a game console in your pocket."

The product deliberately avoids the scope trap of a huge procedural world. The illusion of an endless journey is created by continuously recycling a small library of hand-authored track chunks. The player stays roughly centered; chunks spawn ahead and recycle behind.

Two strategic differentiators:
1. **Physical input** — tilt/motion (v1.0), body tracking (post-release update).
2. **Daily Run** — a shared deterministic seed so everyone plays the same run each day: the engine for competition, notifications, sharing and growth.

The sponsor strategy is one coherent product loop that satisfies multiple categories: Best Game ($20k) through gameplay/art/monetization; **Grand Prize ($100k) through shipping early and demonstrating real growth**; #BuildInPublic ($30k) through a disciplined public posting cadence from day one; OneSignal ($25k) through Daily Run/streak/challenge notifications; Layers ($15k, Unity SDK confirmed) through a measured referral/challenge growth experiment; Noise ($15k) through repeatable social-video formats; RevenueCat Design ($20k) through polish; HAMM ($20k) through a genre-fit cosmetic economy; Catvertising ($20k) opportunistically through rewarded ads via RevenueCat Ads if the Unity spike passes; Samsung (non-cash but likely low competition) through a Galaxy Store release, foldable flex-mode optimization and the hands-free camera showcase; Stripe ($15k) skipped unless everything else lands (judged primarily on real web payment volume).

## North-star sentence

| **DECISION** "Turn your phone into a motion controller and your TV into a game console; run through procedurally assembled worlds, compete in a shared daily challenge, and unlock cosmetic rewards as you progress." |
|---|

## Product principles

- **Ship, then grow.** The Grand Prize and half the sponsor categories judge post-release traction. An app in the store on Sep 10 beats a better app in the store on Sep 29.
- **Fun before infrastructure.** Validate that tilting the device feels responsive and fun before building stores, social systems, or CV.
- **Perceived depth over actual scope.** Modular chunks, stylized art, camera tricks and recycling make a small technical world feel large.
- **One game, many sponsor benefits.** Prefer features that satisfy multiple categories over sponsor-specific bolt-ons.
- **Cosmetics, not pay-to-win.** Monetization must not invalidate competition.
- **Abstract the input layer.** Gyro, camera pose tracking and touch all map to the same internal game-input API.
- **Phone-first, TV-optional.** The game must be fully playable on the phone; TV mirroring is the social multiplier, not a dependency.
- **Build in public from day one.** Every milestone below is also a #BuildInPublic post: sensor prototype → first motion → first track → first TV mirror → store submission → first daily challenge → first stranger's high score.

# 2. Product concept and core user experience

## 2.1 Intended first-session flow

1. **Install/open.** Immediate visual demo of the runner and a clear "Play" CTA.
2. **Choose control mode.** Default Motion/Gyro; Touch always available. (Camera Mode appears only after the post-release update ships.)
3. **Optional TV step.** A short prompt explains AirPlay/casting/screen mirroring. No proprietary TV receiver.
4. **Choose a world.** One biome at launch; the world map is a post-release update.
5. **Start run.** Auto-run; the player controls lateral movement, jump (and later slide).
6. **Survive and collect.** Avoid obstacles, collect coins, build a multiplier.
7. **Finish / crash.** Score, distance, best, XP, rewards, and a direct "Share / Challenge" action. (Post-v1.0: rewarded-ad "revive" here if Catvertising spike passes.)
8. **Return loop.** Daily Run, streak, friend challenge, progression.

## 2.2 Core game loop

> PLAY -> MOVE / DODGE -> COLLECT -> COMBO -> SURVIVE -> SCORE -> XP -> UNLOCK -> SHARE / CHALLENGE -> PLAY AGAIN

## 2.3 Minimal gameplay vocabulary

| **Action** | **v1.0 input** | **Post-release input** | **Game result** |
|---|---|---|---|
| Move left | Tilt left / swipe | Body lean left | Switch lane / steer |
| Move right | Tilt right / swipe | Body lean right | Switch lane / steer |
| Jump | Tap / quick vertical flick | Detected body jump | Clear low obstacle / gap |
| Slide | Swipe down (post-v1.0) | Detected crouch | Avoid high obstacle |
| Power move | Post-v1.0 | Pose/gesture | Temporary multiplier or shield |

| **WATCHOUT** v1.0 ships with only left, right and jump. Slide and special moves come in updates after input quality is proven. |
|---|

# 3. Gameplay loop and controls

## 3.1 Player model

The character auto-runs. Apparent forward motion is produced mostly by moving/recycling the environment toward the camera; the player's local position stays roughly stable so chunk management is predictable and cheap. Subtle camera follow is fine; unpredictable drift is not.

## 3.2 Difficulty model

- **Difficulty 1–2:** wide lanes, low obstacle density, generous reaction time.
- **Difficulty 3–5:** faster world speed, more lane changes, mixed obstacle combinations.
- **Difficulty 6+:** patterned sequences, fake-outs, tighter timing, branch choices.

Difficulty is a property of chunks and obstacle patterns, not just a global speed multiplier. The generator constructs playable sequences instead of random valid-but-unpleasant combinations.

## 3.3 Deterministic runs

A run is a seed. Same seed + game version + content manifest → same chunk sequence and obstacle layout. Required for Daily Run leaderboards and challenges.

> RunSeed + GameVersion + WorldId -> deterministic chunk sequence -> reproducible challenge

## 3.4 Multiplayer strategy

No real-time networking. Asynchronous competition first: everyone plays the same seeded run and compares scores. A party mode is post-Shipaton at the earliest.

# 4. "World that seems to move" — world / track generation

## 4.1 Core decision

| **DECISION** Do not build a Minecraft-scale persistent world. Build a visually rich endless runner from a small library of reusable chunks, generated ahead and recycled behind. |
|---|

## 4.2 Chunk-based procedural track

A TrackChunk is a hand-authored segment with a known entry/exit shape and metadata. The generator assembles chunks into a run: straights, bends, jumps, bridges, tunnels, obstacle patterns, set-pieces. Environment around the road is decorative and biome-specific.

> START -> Straight -> Coins -> Bend -> Jump -> Obstacle -> Tunnel -> Bridge -> Special -> ...

## 4.3 TrackChunk metadata

| **Field** | **Purpose** |
|---|---|
| chunkId | Stable identifier for debugging and deterministic generation. |
| biome | Forest / Alps / Desert / Neon etc. |
| difficultyMin/Max | Generator constraints. |
| length | World-space length. |
| entryType / exitType | Ensures chunks connect cleanly. |
| requiredSkills | e.g. jump, lane-change, slide. |
| tags | Bridge, tunnel, scenic, coins, risk/reward, etc. |
| rarity | Controls how frequently special chunks appear. |

## 4.4 Biomes / global-map layer

One biome at launch. The global-map/journey layer (Alps, Neon City, Desert, Tropical…) is a post-release content update and the natural home for world-linked cosmetics. Stylized, not photorealistic.

## 4.5 "Minecraft-like" lesson

The useful part of the analogy is composition from reusable pieces, not voxels. Modular chunks + deterministic generation give procedural scalability without a voxel engine.

# 5. Characters, cosmetics, progression and season pass

## 5.1 Character architecture

One high-quality stylized 3D base character, built as modular cosmetic slots:

> Character
> |- Body
> |- Head / Hair
> |- Outfit
> |- Shoes
> |- Accessories
> |- Trail / VFX
> |- Emote / Animation set

## 5.2 3D asset workflow

Blender for custom assets and cleanup → Unity. Use licensed/CC0 assets where they accelerate; identity comes from the character, color language, materials, UI and a few recognizable props.

## 5.3 Cosmetic philosophy

- **No pay-to-win.** Cosmetics change appearance/VFX/emotes, never stats.
- **Theme-driven skins** (Explorer, Cyber Ninja, Alpine Runner, Robot…).
- **World-linked cosmetics** once the map layer exists.
- **Earn + buy.** Some earned, premium ones purchased.

## 5.4 Season pass concept

"Season 1 — World Runner", intentionally small: 10–15 reward nodes, 3–4 high-quality cosmetics plus trails/currency. This is a **post-v1.0 update** (Week 5); v1.0 needs only 1–2 purchasable cosmetic SKUs to satisfy eligibility and seed the HAMM story.

## 5.5 Progression

> Run -> Score -> XP -> Level -> Unlock -> New cosmetic -> Daily challenge

# 6. Social, retention and growth mechanics

## 6.1 Daily Run

One globally shared challenge seed per day. Same layout for everyone; comparable scores; the backbone of OneSignal campaigns and the Layers experiment.

## 6.2 Friend challenge loop

> Player A score -> share challenge -> Player B plays -> B beats score -> A gets notified -> A returns -> repeat

This one loop connects the game, sharing, OneSignal notifications and Layers measurement.

## 6.3 Streaks

- Daily Run streak (consecutive days played).
- Personal-best streak.
- Friend streak (both players compete on consecutive days).

## 6.4 OneSignal messaging examples

- "Today's Run is live — can you break 12,000?"
- "Alex just beat your score by 214 points."
- "Your 7-day run streak ends tonight."
- "Tomorrow: Neon City Daily Run."

## 6.5 Social content / Noise strategy

Repeatable short-form formats: "Can you beat me?", "I built a game controlled by tilting your phone", "One phone. One TV.", "The same world every day — who is #1?". Noise eligibility requires a live app, using the Noise platform for promotion, and providing the app URL + Noise account email.

# 7. TV/display strategy and multiplatform approach

## 7.1 Core decision

| **DECISION** The phone is the game machine. The TV is an optional large display via standard OS mirroring/casting. No separate TV client. |
|---|

## 7.2 iOS

Apple supports mirroring iPhone/iPad to Apple TV, AirPlay-compatible TVs or a Mac via Control Centre Screen Mirroring. [S9]

## 7.3 Android

MediaProjection can capture and cast the display; Android 14+ supports app-window sharing; user consent per session. [S8]

## 7.4 UX implication

TV mirroring is a "make this bigger" action. First-run prompt: "For the best experience, mirror this game to your TV." The game must remain fully playable on the phone.

## 7.5 Latency caveat

| **WATCHOUT** Mirroring latency varies wildly by TV/device. The Week-1 spike must measure actual gameplay latency on at least one real Android/TV and one iPhone/Apple TV combination. If latency is bad, the pitch pivots to "phone as console" without leaning on the TV — decide from data, not hope. |
|---|

## 7.6 Multiplatform target

iOS + Android from one Unity codebase. Galaxy Store as an additional distribution target for the Samsung category (see §11 for the billing caveat).

# 8. Computer-vision approach (parallel learning track)

## 8.1 Core decision

| **DECISION** Camera control is a **parallel track from Week 1** — it is one of the project's explicit learning goals (video analysis + body identification) and the strongest demo/marketing hook. It must never gate the v1.0 store release: gyro+touch ships regardless of CV status, and camera mode lands as a post-release update inside the Shipaton window. Everything runs **on-device — no server connection is involved anywhere in the CV stack.** |
|---|

## 8.2 Implementation paths, ranked (researched 20 Aug 2026)

**Path A (primary): BlazePose inside Unity via Unity Inference Engine (formerly Sentis).**
Unity Technologies publishes an official sample (`BlazeDetectionSample/Pose` in the sentis-samples / inference-engine-samples repos) and the converted models on Hugging Face (`unity/inference-engine-blaze-pose`, Apache 2.0). [S16] [S17] What you get:

- The exact MediaPipe pose model family (BlazePose): a 2-stage pipeline — pose **detector** (224×224 input → body bounding box) + pose **landmarker** (256×256 crop → **33 keypoints with x, y, z + visibility/presence**), in lite/full/heavy variants.
- Pure C# + ONNX: no native plugins, no .aar/.framework juggling, no IL2CPP surprises; same code path on Android and iOS.
- GPU compute inference with async tensor readback (`Awaitable`-based in the sample), fed straight from `WebCamTexture` via affine-transform compute shaders.
- Published benchmarks around BlazePose report ~28 ms inference in a Sentis-validated setup; use the **lite** variant on mobile and budget inference alongside rendering (run the landmarker every frame, the detector only on tracking loss).

What MediaPipe's runtime would give you that this doesn't, out of the box: the tracking loop (ROI reuse between frames) and landmark smoothing. Rebuilding a simple version — detector on loss, one-euro filter on landmarks — is exactly the kind of CV engineering this project is meant to teach, and it's a few hundred lines, not a framework.

**Effort estimate:** the official sample provides ~90% of the plumbing (camera texture → tensors → landmarks). Expect ~2–4 dev-days to on-device landmark visualization, plus a few more for the gesture detector and tuning. The fiddly parts are not the ML: they are Android `WebCamTexture` rotation/mirroring (front cameras report rotated, mirrored frames per device), the crop/affine transform, and gesture-threshold tuning against false positives.

**Weight:** the detector + lite landmarker ONNX files are a few MB each — negligible for app size and memory. FP16 quantization is available in Inference Engine if needed.

**Low-end phone policy:** the GPUCompute backend needs compute-shader support (Vulkan / GLES 3.1+ / Metal), which every Android 8+ phone and every iPhone this game targets has — compatibility is not the issue, throughput is. Inference Engine's generic compute shaders are typically somewhat slower than MediaPipe's hand-optimized native delegates on the same device (community reports bear this out), so budget roughly 15–40 ms per lite-landmarker inference on mid-tier hardware and worse on low-end, *concurrently with the game's own rendering*. Policy, not hope:

1. Startup micro-benchmark: run ~20 warm inferences, measure median latency.
2. Enable camera mode only below a threshold (e.g. < 30 ms); otherwise the option is hidden or marked "experimental".
3. Run the landmarker every 2nd–3rd frame and interpolate landmarks between inferences; run the detector only on tracking loss.
4. Feed the model from a 640×480 camera request, not full resolution.
5. Low-end devices simply keep gyro/touch — camera mode is additive by design, so nothing is lost. If camera-mode reach on weak devices ever becomes a priority, Path B (MediaPipe's optimized runtime) is the performance escape hatch.

**Path B (secondary): homuler/MediaPipeUnityPlugin.**
The full MediaPipe runtime in Unity (latest v0.16.3). It works and is used in production, but it's community-maintained with real friction: the iOS framework is no longer bundled in the .unitypackage (tarball or self-build required), and Android build/installation issues are recurrent in the issue tracker. [S18] Choose it only if Path A's tracking quality proves insufficient after the smoothing pass.

**Path C (last resort): platform-native MediaPipe Tasks / Apple Vision bridged into Unity.**
Best runtime quality, worst cost — two native codebases plus two bridges. Only if A and B both fail on performance, which is unlikely for 3-gesture detection.

## 8.3 Do not start with "AI understands running"

Pose landmarks → simple geometry and temporal thresholds. Torso/hip center left of a neutral band => LEFT; right => RIGHT; rapid vertical torso displacement => JUMP. No classifier, no training, no cloud. [S6] [S6b]

## 8.4 Perception architecture

> Camera frame (WebCamTexture)
> -> BlazePose detector + landmarker (Inference Engine, on-device GPU)
> -> 33 body landmarks (+ one-euro smoothing)
> -> lightweight movement state detector (neutral-band + thresholds)
> -> abstract GameInput (LEFT/RIGHT/JUMP/SLIDE)
> -> game

## 8.5 Input abstraction (this is what keeps CV off the critical path)

> interface IGameInput
>   GetMoveAxis()
>   IsJumpPressed()
>   IsSlidePressed()
>   IsSpecialPressed()

Adapters: GyroInput, TouchInput (v1.0); CameraPoseInput (update). The game never calls MediaPipe directly.

## 8.6 OpenCV / Roboflow

OpenCV only if a specific preprocessing need emerges [S7]; Roboflow only for a later bespoke gesture model [S10]. Neither is on the critical path.

# 9. Game engine decision

## 9.1 Recommended choice: Unity

| **Option** | **Strengths** | **Weaknesses** | **Recommendation** |
|---|---|---|---|
| Unity | Mobile 3D, animation, physics, asset ecosystem; **RevenueCat Unity SDK incl. Paywalls/Customer Center (8.4.0+)**; **OneSignal Unity SDK**; **Layers Unity UPM package** | No RevenueCat Galaxy-billing support yet; CV plugins need care | PRIMARY |
| Unreal | Excellent rendering | Heavy for a small mobile team; weaker sponsor-SDK story | Not necessary |
| Godot | Lightweight, open source | Smaller ecosystem; sponsor SDKs would need bridges | Not preferred |
| React Native + 3D | Strong TS ecosystem; RN is the *only* hybrid SDK with RevenueCat Galaxy billing today | Not ideal for a polished 3D game | Not preferred |
| Kotlin Multiplatform | Would unlock the JetBrains category ($15k) | Forces a different stack; no mature 3D game pipeline | SKIP |

## 9.2 Unity AI tooling

Unity's editor-integrated assistant/AI Gateway/MCP Server are development productivity tools — use them for boilerplate, ScriptableObjects, editor tools, chunk-manager scaffolding and tests, with code review. Note: Sentis has been renamed **Unity Inference Engine**; either way, do not run perception inside Unity's ML runtime for this project. [S11]

# 10. Recommended technical architecture

## 10.1 High-level

> MOBILE APP (Unity)
> ┌─────────────────────────────────────────┐
> │ Runner / World / UI / Audio             │
> │ Physics / Animation / Scoring           │
> │ Progression / Cosmetics                 │
> │ RevenueCat SDK (IAP + Paywalls)         │
> │ OneSignal SDK (push/journeys)           │
> │ Layers SDK (growth events)              │
> └──────────────┬──────────────────────────┘
>                │ IGameInput API
>     ┌──────────┴──────────┐
>     │                     │
> GyroInput  TouchInput  [CameraPoseInput — post-v1.0]
>     │                     │
>     └──────────┬──────────┘
>                │
>          User controls
>                │
>   Optional OS screen mirroring
>                ▼
>               TV

## 10.2 Game modules

| **Module** | **Responsibility** |
|---|---|
| Bootstrap / AppState | Launch, mode selection, device checks. |
| InputSystem | Normalizes gyro/touch (later camera) into GameInput. |
| RunnerController | Lane/axis movement, jump (later slide). |
| TrackGenerator | Chunks ahead, recycle behind, seeded RNG. |
| ChunkLibrary | ScriptableObjects/prefabs + metadata. |
| ObstacleSystem | Collision, timing, patterns, difficulty. |
| WorldTheme | Biome visuals, music, lighting, chunk pools. |
| ScoreSystem | Distance, coins, combo, multiplier. |
| Progression | XP, levels, unlocks, streaks. |
| Cosmetics | Customization, trails; store catalog. |
| Social | Daily seed, leaderboard, challenge share payload. |
| Monetization | RevenueCat products/entitlements/paywalls; promo-code path for judges. |
| Notifications | OneSignal tags, campaigns, Journeys. |
| Analytics/Growth | Layers events (share, install-from-challenge, return), run start/end, control mode, conversion. |

## 10.3 Backend philosophy

Minimal. Daily seed can be derived client-side from the date for v1.0 (e.g. hash of date+version) — zero backend. Supabase (or similar) enters only when the shared leaderboard ships (Week 5), for profiles/scores/seeds. No mandatory accounts for v1.0.

## 10.4 Data entities

> User: id, displayName, cosmeticLoadout, XP/level, seasonProgress
> DailyRun: date, worldId, seed, version
> RunResult: userId, dailyRunId, score, distance, timestamp
> Cosmetic: id, type, rarity, source (earned/store/season)

# 11. Sponsor strategy and implementation requirements

All amounts below are quoted from the live Devpost prize list, 20 Aug 2026. [S1] [S2]

## 11.1 Priority matrix

| **Target** | **1st prize** | **Why it fits** | **What to implement** | **Priority** |
|---|---|---|---|---|
| Best Game | **$20,000** | The product itself | Gameplay, art direction, genre-fit monetization | MUST |
| Grand Prize | **$100,000** | Judged on post-release traction & growth momentum | **Ship v1.0 by ~Sep 10–12**, then iterate; document growth efforts | MUST (as a strategy, not a feature) |
| #BuildInPublic | **$30,000** | Zero engineering cost; the dev journey is unusually visual | Post every milestone with #Shipaton #BuildInPublic from day 1; link posts in submission | MUST |
| Keep Them Coming Back — OneSignal | **$25,000** | Daily Run/streak loop is natural re-engagement | Integrate OneSignal Unity SDK; deploy ≥1 campaign (API/MCP/dashboard); ideally a Daily Run Journey; provide OneSignal App ID | MUST |
| RevenueCat Design | **$20,000** | Visual polish can differentiate | Motion, world transitions, progression UX, clean HUD | HIGH |
| Growth Loop — Layers | **$15,000** | Challenge/share loop is a real growth loop; **Unity UPM SDK confirmed (`com.layers.analytics`)** | Install SDK (verifiable before judging); define audience/message/channel/experiment/outcome; document learnings | HIGH |
| Most Viral — Noise | **$15,000** | Concept demos brilliantly in short video | Live app + promote via Noise platform; provide app URL + Noise account email | HIGH |
| HAMM | **$20,000** | Cosmetic economy fits genre | Thoughtful pricing/packaging, paywall via RevenueCat Paywall Builder, conversion results in write-up | MEDIUM-HIGH |
| Catvertising | **$20,000** | **NEW.** Rewarded ads (revive/double-coins) are genre-native for runners | Spike RevenueCat Ads in Unity; if it works, one tasteful rewarded placement; "an experience users don't hate" | OPPORTUNISTIC (spike first) |
| Best App for Galaxy — Samsung | Non-cash: 3 weeks featured Galaxy Store placement + trophy/billboard/conference | **Likely shallow competitor pool** (non-cash prize, Galaxy Store friction, first-year category) and the strongest device story: hands-free camera play with the Fold half-open in flex mode — the CV learning track doing double duty. Featured placement also feeds Grand-Prize traction | Publish on Galaxy Store; foldable flex-mode + multi-window + perf optimization; Fold hands-free camera demo if the CV track lands; exclusivity NOT required (bonus only). **Caveat: RevenueCat Galaxy billing is not in the Unity SDK yet** — Galaxy build ships with store purchases disabled/web-purchase link (the RevenueCat eligibility purchase lives in the Play/App Store builds), or re-verify SDK status at kickoff | HIGH |
| Funnel Vision — Stripe | **$15,000** | Judged primarily on real web payment volume — unrealistic for a 6-week-old game | RevenueCat Funnels + Stripe checkout, live URL + Stripe Project ID | SKIP unless everything else lands |
| Idea to Income — Replit | **$15,000** | Requires building with Replit | Conflicts with Unity stack | SKIP |
| Ship Kotlin Everywhere — JetBrains | **$15,000** | Requires KMP/Compose Multiplatform on both stores | Conflicts with Unity stack | SKIP |
| Influencer — Gaming (Lewis Blogs Gaming) | **$20,000** | It's a gaming *bucket-list* app brief, not a game | Different product | SKIP |
| Peace Prize / Next Gen | $20,000 each | Social-good app / active students only | Not applicable | SKIP |

## 11.2 Official requirements worth engineering time (verified)

- **Eligibility:** working app using the RevenueCat SDK to power ≥1 in-app **or web** purchase (challenge text also allows serving ads through RevenueCat Ads). Built for iOS/iPadOS/macOS/Android; first public release Aug 1–Sep 30, 2026 on App Store, Google Play, or Galaxy Store; accessible from the US.
- **Submission package:** text description; demo video **≤2 minutes of essential footage**, public on YouTube/Vimeo, no third-party trademarks/copyrighted music; store URL; **1024×1024 icon**; **≥1 screenshot at 1179×2556, no device frame**; **free trial or promo code so judges can unlock the IAP and test premium features**.
- **Samsung:** publish on Galaxy Store during Shipaton; 20% of category score is Galaxy optimization (foldables, multi-window, Samsung hardware); 80% standard criteria; exclusivity = bonus consideration only.
- **OneSignal:** integrate + deploy ≥1 campaign via API/MCP/dashboard; describe it; provide App ID.
- **Layers:** SDK installed and verifiable before judging; describe the growth loop (audience, message, channel/surface, experiment, intended outcome) and what was learned.
- **Stripe:** live web-to-app funnel via RevenueCat Funnels with Stripe checkout; live URL + Stripe Project ID; judged primarily on qualifying web payment volume.
- **Judging process:** [S3] submissions filtered for store publication, RevenueCat SDK verification (bundle ID), required fields, video, marketing assets; ≥2 screeners per project; judges read the description, watch up to 2 minutes, review screenshots, score 1–5 per applicable category; ~100 apps reach the final round. **Do not check every category box — screeners penalize category overreach; claim only the categories genuinely targeted.**

## 11.3 Sponsor integration blueprint

> BEST GAME + DESIGN: core gameplay + art + polish
> GRAND PRIZE: early release + growth iterations + metrics story
> #BUILDINPUBLIC: milestone posts from day 1 (this doc's checklist = the content calendar)
> ONESIGNAL: Daily Run + streak + "friend beat your score" Journey
> LAYERS: share → install → challenge → return, instrumented as one experiment
> NOISE: repeatable short-form formats ("beat my score", "phone = controller")
> HAMM: cosmetics + honest pricing + conversion write-up
> CATVERTISING (if spike passes): one rewarded-ad revive placement
> SAMSUNG: Galaxy Store build + foldable-aware UI + perf notes
> STRIPE: skip

# 12. Release pipeline, MVP scope, roadmap and cut lines

## 12.1 Workstream 0 — store & account pipeline (START TODAY, runs in parallel)

| **WATCHOUT** This workstream is the #1 schedule risk of the whole project. |
|---|

1. **Google Play:** determine account status immediately. A personal account created after 13 Nov 2023 requires a **closed test with 12 opted-in testers for 14 consecutive days**, then a production-access application (review ~7 days or less). Working backwards from Sep 30: closed testing must start by ~Sep 1 with a real build — recruit the 12 testers now (Shipaton Discord is full of mutual testers). If an organization account (or pre-Nov-2023 personal account) is available, this constraint disappears.
2. **Apple:** enroll/verify Apple Developer Program access now (enrollment can take ~48h). App Store review is typically 1–3 days but plan for a rejection cycle: submit v1.0 no later than ~Sep 12.
3. **Samsung:** register a Galaxy Store commercial seller account now (approval takes days and requires validation); app review adds more days.
4. **Accounts:** RevenueCat, OneSignal, Layers, Noise, Devpost registration, and the social accounts for #BuildInPublic — all this week.
5. **Store metadata early:** icon (1024×1024), screenshots (incl. the 1179×2556 no-frame shot Devpost wants), listing copy — drafted in Week 2, not Week 6.

## 12.2 v1.0 definition (what actually ships to stores ~Sep 8–12)

| **WATCHOUT** v1.0 success condition: one complete run feels responsive, visually coherent and replayable on a real phone — WITH a working RevenueCat purchase and OneSignal integration, because eligibility and category evidence live in the shipped build. |
|---|

- Unity mobile build (Android + iOS from one codebase; submit both if accounts allow, else iOS-first — see 12.1).
- One stylized biome; one character.
- Three controls: left, right, jump — gyro/tilt + touch. **No camera mode in the store build** (the CV track runs in parallel — §8 — and ships as an update when it's good).
- Endless chunked track with 10–20 reusable chunks; obstacle/collision system.
- Score + distance + coins + simple combo; post-run result screen.
- **Daily Run v0:** date-derived seed, local best score (no backend).
- **RevenueCat:** 1–2 cosmetic SKUs behind a Paywall-Builder paywall + promo-code/free-trial path for judges.
- **OneSignal SDK** integrated (first campaign fires at release).
- **Layers SDK** integrated (events: run_end, share_tapped, challenge_open).
- Optional TV mirroring validated and documented (not a feature to build — a workflow to test).

## 12.3 Post-release updates (still inside the window — updates to your own Shipaton app are allowed)

**Update 1 (~Sep 14–20):** shareable challenge card/deep link; leaderboard (Supabase); XP/levels + 3–4 earnable cosmetics; OneSignal Daily Run Journey + streak reminders; Layers growth experiment live; Galaxy Store submission.

**Update 2 (~Sep 21–27):** camera mode (if the CV track passed its quality bar) — headline feature of the update and of the submission video; slide action; small Season 1 pass (10–15 nodes); rewarded-ad revive if Catvertising spike passed; Fold flex-mode hands-free layout for the Samsung category; polish pass for the Design category; Noise content cadence continues.

## 12.4 Cut list — do not build during Shipaton

- Camera/CV control in v1.0 (post-release update at most; cut entirely if the schedule slips).
- Minecraft/voxel terrain; persistent real-world cities.
- Dedicated TV application.
- Real-time networked multiplayer.
- 100-tier battle pass; dozens of characters.
- Custom-trained vision models (Roboflow etc.).
- Stripe web funnel.
- Complex backend; mandatory accounts.

## 12.5 Calendar (dated, from 20 Aug 2026)

| **Window** | **Outcome** |
|---|---|
| Aug 20–22 | Workstream 0 (accounts, testers, seller registrations). Spike: Unity build on device + gyro input feel test + chunk recycling proof + TV-mirroring latency measurement. **Go/no-go on "tilt is fun."** |
| Aug 23–29 | Core game: biome, obstacles, scoring, result screen, character v1. First #BuildInPublic posts. Play closed testing starts with the ugly build (testers don't need the final game). **CV track starts:** Inference Engine BlazePose sample running on-device, webcam → 33 landmarks visualized (great #BuildInPublic material). |
| Aug 30–Sep 5 | Content + feel polish; RevenueCat paywall + SKUs; OneSignal + Layers SDKs in; Daily Run v0; store listing assets. Catvertising/RevenueCat-Ads Unity spike (timeboxed: 1 day). **CV track:** landmarks → LEFT/RIGHT/JUMP state detector; measure false positives; go/no-go for camera mode in Update 2. |
| Sep 6–12 | Release candidate; QA on real devices; **submit to App Store + Play (as accounts allow) by Sep 12**; Galaxy seller/app submission in flight. First OneSignal campaign scheduled for release day. |
| Sep 13–20 | **v1.0 live.** Update 1 (share/leaderboard/XP/Journey/Layers experiment). Noise content push. Collect growth metrics for the Grand Prize narrative. |
| Sep 21–27 | Update 2 (season shell, slide, rewarded revive, Fold layout, design polish). Keep posting. |
| Sep 26–29 | Submission package: ≤2-min video, icon, screenshots, category answers (only genuinely targeted ones), promo codes, OneSignal App ID, Layers evidence, #BuildInPublic links. **Submit ≥24h before the Sep 30 11:45pm PDT deadline.** |

# 13. Risks, assumptions and open technical questions

| **Question / risk** | **Why it matters** | **Proposed test / decision** |
|---|---|---|
| Google Play 12-testers/14-days rule | Can make a Play release impossible if discovered late | Day 1: audit account type; recruit testers immediately; fallback = iOS-first + Galaxy Store (both are eligible release stores) |
| Is gyro control actually fun? | Everything depends on it | 1-hour prototype, play-test, go/no-go by Aug 22 |
| Mirroring latency | Runner gameplay is latency-sensitive | Measure on real iPhone/Apple TV + Android/TV in Week-1 spike; if bad, de-emphasize TV in pitch |
| RevenueCat Ads in Unity | Gates the Catvertising opportunity | 1-day timeboxed spike ~Sep 3; drop silently if it fails |
| RevenueCat Galaxy billing not in Unity SDK | Galaxy build can't sell through RevenueCat today | Re-verify SDK status at kickoff ("other hybrid SDKs coming soon"); else ship Galaxy build without store IAP or with web-purchase link |
| Layers SDK freshness | Category requires verifiable install | Unity UPM package confirmed to exist; integrate early and verify events arrive |
| 60 FPS on mid-tier Android | Mirroring doesn't fix rendering perf | Profile on a mid-tier device from Week 2; stylized assets |
| Daily-seed stability across versions | Version changes can alter generation | Store generator version with the seed |
| Season economy pricing | HAMM credibility | 2–3 price points; write up actual conversion honestly |
| Fold camera posture | Fold cameras/orientation differ; flex-mode is the Samsung showcase | Test on a real Fold before committing the demo; fallback = regular Galaxy phone propped up |
| CV inference + rendering on one phone | Landmarker every frame competes with the game for GPU | Lite model variant; detector only on tracking loss; if needed, infer every 2nd frame + interpolate |
| Camera-mode quality bar | A janky camera mode hurts more than none | Ship it in Update 2 only if false-positive rate is low in normal living-room lighting; else keep it demo-video-only |

## 13.1 Assumptions

- Standard OS mirroring/casting is permitted for the big-screen experience (no rule requires a TV app).
- Asynchronous competition provides enough social value for this submission.
- Updates during the window to an app first released during the window are eligible (Devpost: only apps *previously* released are excluded).
- The team has (or can get) access to at least one real iPhone, one mid-tier Android, and ideally one Galaxy Fold.

# 14. Handoff checklist for the implementation agent

## 14.1 Today (parallel with all other work)

1. Audit Google Play account type; start tester recruitment if the 14-day rule applies.
2. Verify Apple Developer Program access; start Galaxy Store commercial seller registration.
3. Create RevenueCat, OneSignal, Layers, Noise accounts; register on Devpost; set up #BuildInPublic social account(s) and post #1 ("we're building a motion-controlled runner").

## 14.2 First engineering task: validate the premise (by Aug 22)

1. Unity mobile prototype: one runner, one camera, one road plane, no art.
2. Gyro movement: left/right must feel immediate and stable.
3. Jump on tap.
4. Screen-mirror the prototype to a TV; measure perceived latency.
5. Record a 20–30s proof clip (this is also #BuildInPublic post #2).

## 14.3 Second task: procedural illusion

1. 5–10 chunks (straight, curve, jump, obstacle, tunnel, scenic).
2. Chunk recycling: N ahead, recycle behind.
3. Deterministic seed: same seed = same sequence.
4. Stress test minutes-long runs: no gaps, no memory growth, no obvious repetition.

## 14.4 Parallel task: CV learning track (starts Week 1, never blocks release)

1. Clone Unity's BlazePose sample [S16], import the Hugging Face models [S17], run on a real phone.
2. Wire `WebCamTexture` → detector + landmarker (lite variant) → draw the 33 landmarks on screen.
3. Add one-euro smoothing; build the neutral-band LEFT/RIGHT/JUMP state detector; log false positives in normal room lighting.
4. Implement `CameraPoseInput : IGameInput` and play the actual game with it.
5. Go/no-go for shipping camera mode in Update 2; either way, record it for the demo video and #BuildInPublic.

## 14.5 Third task: sponsor skeleton (inside v1.0, not after)

- RevenueCat: 1–2 cosmetic SKUs, Paywall Builder paywall, promo-code path for judges.
- OneSignal: SDK in; first campaign drafted, fires on release day.
- Layers: SDK in; share/return events tracked; one experiment designed.
- Samsung: Galaxy submission after v1.0 is live; foldable layout check.
- Noise: account ready; first repeatable format tested.
- #BuildInPublic: every milestone above is a post.

## 14.6 Submission discipline

Judging filters check store publication, RevenueCat SDK on the bundle ID, required fields, video and marketing assets; ≥2 screeners per project; ~100 finalists. [S3]

| **WATCHOUT** The demo video is capped at **2 minutes of essential footage** — judges are not required to watch more. Structure: 0:00–0:20 person + phone tilting + game reacting (the hook), 0:20–1:10 gameplay + Daily Run + cosmetics, 1:10–2:00 monetization, notifications, growth evidence, targeted categories. No architecture diagrams. Claim only the categories genuinely targeted — screeners penalize box-checking. |
|---|

# 15. Sources and research notes

Contest facts re-verified on the live pages on **20 August 2026**. Technology facts from official vendor documentation.

**S1 — Shipaton 2026 Devpost overview + full prize list** — https://revenuecat-shipaton-2026.devpost.com/
Deadline, challenge text ("in-app purchase or serve ads through RevenueCat Ads"), release window (Aug 1–Sep 30, Galaxy Store new this year), full itemized prize list (quoted in §11.1), submission requirements (2-min video cap, 1024×1024 icon, 1179×2556 screenshot, free trial/promo code), judging criteria blurbs.

**S2 — Shipaton 2026 Devpost rules** — https://revenuecat-shipaton-2026.devpost.com/rules
Eligibility ("first public version… released during the Submission Period"; builds may pre-date the window), per-category requirements (Samsung 20% Galaxy-optimization weighting, OneSignal campaign + App ID, Layers SDK verifiable install, Stripe live funnel + payment volume).

**S3 — How we judge Shipaton** — https://www.shipaton.com/blog/how-we-judge-shipaton
Filtering, ≥2 screeners, 2-minute video emphasis, ~100 finalists, category-overreach warning.

**S4 — Announcing Shipaton 2026** — https://www.revenuecat.com/blog/company/announcing-shipaton-2026
New categories (Best Game, Catvertising, Next Gen), $700k+ pool, RevenueCat Ads as alternative monetization, sponsor list.

**S5 — Google Play: app testing requirements for new personal developer accounts** — https://support.google.com/googleplay/android-developer/answer/14151465
12 opted-in testers × 14 consecutive days before production access (personal accounts created after 13 Nov 2023); production-access review ~7 days.

**S6 — MediaPipe / Google AI Edge** — https://github.com/google-ai-edge/mediapipe
**S6b — MediaPipe Pose docs** — https://github.com/google-ai-edge/mediapipe/blob/master/docs/solutions/pose.md
On-device pose estimation, 33 3D landmarks, Pose Landmarker as current path.

**S7 — OpenCV platforms** — https://opencv.org/platforms/

**S8 — Android MediaProjection** — https://developer.android.com/media/grow/media-projection
**S8b — Android 14 app screen sharing** — https://developer.android.com/about/versions/14/features/app-screen-sharing

**S9 — Apple AirPlay screen mirroring** — https://support.apple.com/en-ie/102661

**S10 — Roboflow iOS SDK** — https://docs.roboflow.com/developer/ios-sdk/using-the-ios-sdk

**S11 — Unity AI tools** — https://unity.com/features/ai
Note: Sentis renamed Unity Inference Engine.

**S12 — RevenueCat Unity SDK adds Paywalls & Customer Center (8.4.0, Oct 2025)** — https://www.revenuecat.com/release/unity-sdk-adds-paywalls-and-customer-center-support-2025-10-23

**S13 — RevenueCat Galaxy Store support** — https://www.revenuecat.com/docs/ + https://central.sonatype.com/artifact/com.revenuecat.purchases/purchases-store-galaxy
Galaxy billing in Android SDK 10.7.0+ and React Native 10.3.0+; other hybrid SDKs (incl. Unity) "coming soon". Test purchases require a physical Galaxy device.

**S14 — Layers docs, SDK platforms** — https://layers.com/docs/sdk
Official SDKs incl. **Unity (C#) UPM package `com.layers.analytics`**, plus iOS/Android/RN/Expo/Flutter/Web/Node.

**S15 — RevenueCat Ad Monetization docs** — https://www.revenuecat.com/docs/ad-monetization
Ad monetization listed for Android/iOS/Flutter/Unity/RN via manual integration (AppLovin MAX, ironSource, Unity Ads…). RevenueCat-Ads-specific Unity support still needs the §12.5 spike.

**S16 — Unity official BlazePose sample (Inference Engine / Sentis)** — https://github.com/Unity-Technologies/sentis-samples/tree/main/BlazeDetectionSample/Pose (also mirrored in Unity-Technologies/inference-engine-samples)
Two-stage BlazePose pipeline (detector 224×224 + landmarker 256×256, lite/full/heavy), 33 keypoints with x/y/z + visibility, WebCamTexture-friendly texture input via compute shaders, async GPU readback.

**S17 — unity/inference-engine-blaze-pose (Hugging Face)** — https://huggingface.co/unity/inference-engine-blaze-pose
Official Unity-converted BlazePose ONNX models, Apache 2.0, for Unity 6 Inference Engine (works with Sentis).

**S18 — homuler/MediaPipeUnityPlugin** — https://github.com/homuler/MediaPipeUnityPlugin
Community MediaPipe runtime for Unity; latest release v0.16.3. iOS framework no longer bundled in the .unitypackage (tarball/self-build); recurring Android build friction in the issue tracker. Secondary path.
