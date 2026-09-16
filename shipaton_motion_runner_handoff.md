**MOTION RUNNER**

Shipaton 2026 — Product, Game Design, Technical Architecture & Sponsor Strategy

*Handoff document for a second implementation agent*

| **DECISION** This document is intentionally written as an implementation handoff. Official contest facts are labeled as such; product/engineering choices are recommendations or working decisions and may be changed after prototype validation. |
|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|

| **Item**                      | **Current value**                                                     |
|-------------------------------|-----------------------------------------------------------------------|
| Hackathon                     | RevenueCat Shipaton 2026                                              |
| Submission deadline           | 30 September 2026 @ 11:45pm PDT                                       |
| Participants shown on Devpost | 18,574 (snapshot checked 20 Aug 2026)                                 |
| General app requirement       | Brand-new app first released during Aug 1–Sep 30, 2026                |
| Core RevenueCat requirement   | Use RevenueCat SDK for at least one in-app purchase or RevenueCat Ads |
| Primary product target        | Best Game + strong sponsor overlap                                    |

Source basis: \[S1\] official Devpost overview and \[S2\] official Devpost rules. The participant count is a live Devpost snapshot and can change after this document is handed off.

# Contents

1. Executive summary and north-star product

2. Product concept and core user experience

3. Gameplay loop and controls

4. “World that seems to move” — world/track generation

5. Characters, cosmetics, progression and season pass

6. Social, retention and growth mechanics

7. TV/display strategy and multiplatform approach

8. Computer-vision approach: MediaPipe / native CV / OpenCV / Roboflow

9. Game engine decision: Unity and alternatives

10. Recommended technical architecture

11. Sponsor strategy and implementation requirements

12. MVP scope, roadmap and cut lines

13. Risks, assumptions and open technical questions

14. Handoff checklist for the implementation agent

15. Sources and research notes

# 1. Executive summary and north-star product

Working concept: a mobile motion-controlled endless runner in which the player uses a smartphone’s motion sensors or camera-based body tracking to control a 3D character. The phone renders the game; the user can mirror/cast the phone screen to a TV so the TV acts as the large gameplay display. The core experience should feel like “a game console in your pocket,” not like a conventional mobile runner.

The product should deliberately avoid the scope trap of building a huge Minecraft-style procedural world. The preferred illusion is an endless journey created by continuously recycling a small library of hand-authored track chunks. The player stays roughly centered while the track/world moves toward and underneath them. New chunks are generated/spawned ahead; old chunks are recycled behind. This produces an apparently infinite world with a small memory and asset footprint.

The game’s strongest strategic differentiator is the physical input model: movement of the phone or the player’s body controls the character. The second differentiator is a “Daily Run” with a shared deterministic seed, making the same run available to everyone each day. That creates a natural loop for score competition, sharing, notifications, and growth.

The intended sponsor strategy is not to bolt on unrelated sponsor features. Instead, one coherent product loop should satisfy multiple categories: Best Game through gameplay/art/monetization; OneSignal through Daily Run/streak/challenge notifications; Layers through measurable referral/competition growth loops; Noise through repeatable social-video formats; RevenueCat Design through polished motion, world and progression UX; HAMM through a thoughtful cosmetic/season monetization model; Stripe as an optional later web-to-app funnel.

## North-star sentence

| **DECISION** “Turn your phone into a motion controller and your TV into a game console; run through procedurally assembled worlds, compete in a shared daily challenge, and unlock cosmetic rewards as you progress.” |
|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|

## Product principles

- **Fun before infrastructure.** Validate that moving/tilting the device actually feels responsive and fun before building stores, social systems, or large content libraries.

- **Perceived depth over actual scope.** Use modular chunks, stylized art, camera tricks and recycling to make a small technical world feel large.

- **One game, many sponsor benefits.** Prefer features that naturally satisfy multiple categories over sponsor-specific mini-features.

- **Cosmetics, not pay-to-win.** Monetization should not invalidate competition.

- **Abstract the input layer.** Gyro, camera pose tracking and touch must all map to the same internal game-input API.

- **Phone-first, TV-optional.** The game should function on a phone without a TV; TV mirroring makes it a stronger social experience rather than a hard dependency.

# 2. Product concept and core user experience

## 2.1 Intended first-session flow

1.  **Install/open the app.** Show an immediate visual demo of the runner and a clear “Play” CTA.

2.  **Choose control mode.** Default to Motion / Gyro. Offer Camera Mode only when device/platform validation says it is ready.

3.  **Optional TV step.** A short prompt explains that the player can use AirPlay/casting/screen mirroring to move the game to a TV. Do not require a proprietary TV receiver in the MVP.

4.  **Choose a world.** Start with one world/biome. Later, world map unlocks additional environments.

5.  **Start run.** The player character begins running automatically. The player controls lateral movement and a small number of actions.

6.  **Survive and collect.** Avoid obstacles, collect coins/energy, hit occasional jump/slide prompts, and build a score multiplier.

7.  **Finish / crash.** Show score, distance, best score, XP gained, new rewards, and a direct “Share / Challenge” action.

8.  **Return loop.** Daily Run, streak, friend challenge, season progression, or new unlock gives a reason to play again.

## 2.2 Core game loop

> PLAY -\> MOVE / DODGE -\> COLLECT -\> COMBO -\> SURVIVE -\> SCORE -\> XP -\> UNLOCK -\> SHARE / CHALLENGE -\> PLAY AGAIN

## 2.3 Minimal gameplay vocabulary

| **Action** | **MVP input**                           | **Possible later input** | **Game result**                |
|------------|-----------------------------------------|--------------------------|--------------------------------|
| Move left  | Tilt left                               | Body lean left           | Switch lane / steer            |
| Move right | Tilt right                              | Body lean right          | Switch lane / steer            |
| Jump       | Shake / button or quick vertical motion | Detected body jump       | Clear low obstacle / gap       |
| Slide      | Button / gesture                        | Detected crouch          | Avoid high obstacle            |
| Power move | Shake / tap combination                 | Pose/gesture             | Temporary multiplier or shield |

| **WATCHOUT** MVP should probably start with only left, right and jump. Slide and special moves can be added after input quality is proven. |
|--------------------------------------------------------------------------------------------------------------------------------------------|

# 3. Gameplay loop and controls

## 3.1 Player model

The character automatically runs forward. The apparent forward motion is produced mostly by moving/recycling the environment toward the camera. Keep the player’s local position relatively stable so procedural chunk management is predictable and low-cost. The camera can subtly follow the track or character, but should avoid making the player feel like they are drifting unpredictably.

## 3.2 Difficulty model

- **Difficulty 1–2:** wide lanes, low obstacle density, generous reaction time.

- **Difficulty 3–5:** faster world speed, more lane changes, mixed obstacle combinations.

- **Difficulty 6+:** patterned sequences, fake-outs, tighter timing, branch choices and elite challenges.

Difficulty should be a property of chunks and obstacle patterns, not just a global speed multiplier. This enables the generator to construct playable sequences and prevents random combinations that are technically valid but unpleasant.

## 3.3 Deterministic runs

A run is represented by a seed. Given the same seed, game rules/version and content manifest, the same chunk sequence and obstacle layout should be reproducible. This is important for Daily Run leaderboards and social challenges.

> RunSeed + GameVersion + WorldId -\> deterministic chunk sequence -\> reproducible challenge

## 3.4 Multiplayer strategy

Do not start with real-time multiplayer networking. First implement asynchronous competition: everyone plays the same seeded run and compares scores. This gives most of the social value without requiring synchronized simulation. A later party mode can allow multiple phones/players, but that is a post-MVP feature.

# 4. “World that seems to move” — world / track generation

## 4.1 Core decision

| **DECISION** Do not build a Minecraft-scale persistent voxel world. Build a visually rich endless runner from a small library of reusable chunks. The player experiences infinite travel because chunks are generated ahead and recycled behind. |
|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|

## 4.2 Chunk-based procedural track

A TrackChunk is a hand-authored segment with a known entry/exit shape and metadata. The procedural generator assembles chunks into a run. A chunk might contain a straight road, bend, jump, bridge, tunnel, obstacle pattern or special set-piece. The environment around the road is decorative and biome-specific.

> START -\> Straight -\> Coins -\> Bend -\> Jump -\> Obstacle -\> Tunnel -\> Bridge -\> Special -\> ...

## 4.3 Proposed TrackChunk metadata

| **Field**            | **Purpose**                                                   |
|----------------------|---------------------------------------------------------------|
| chunkId              | Stable identifier for debugging and deterministic generation. |
| biome                | Forest / Alps / Desert / Neon etc.                            |
| difficultyMin/Max    | Generator constraints.                                        |
| length               | World-space length.                                           |
| entryType / exitType | Ensures chunks connect cleanly.                               |
| requiredSkills       | e.g. jump, lane-change, slide.                                |
| tags                 | Bridge, tunnel, scenic, coins, risk/reward, etc.              |
| rarity               | Controls how frequently special chunks appear.                |

## 4.4 Biomes / global-map layer

A global map can give the impression of worldwide travel without recreating real cities. Each region selects a visual theme, chunk pool, music and cosmetic set. Example regions: Alps, Tokyo/Neon City, Desert, Tropical Jungle, Volcanic, Nordic. The visual language should be stylized rather than photorealistic.

## 4.5 Example “World Journey” system

| **World** | **Visual identity**                                 | **Potential rewards**               |
|-----------|-----------------------------------------------------|-------------------------------------|
| Alps      | Snow, pines, cliffs, bridges, cable-car silhouettes | Alpine Runner skin, snow trail      |
| Neon City | Neon signage, futuristic road, tunnels              | Cyber Runner skin, neon trail       |
| Desert    | Sand, ruins, heat haze, canyons                     | Explorer / Pharaoh-themed cosmetics |
| Tropical  | Jungle, waterfalls, bridges, ruins                  | Tropical Runner, leaf trail         |

## 4.6 “Minecraft-like” lesson

The useful part of the Minecraft analogy is not voxels; it is the sense that a huge environment can be composed from reusable pieces. Our equivalent is modular chunks + deterministic generation. This should give the team the scalability of a procedural game without the engineering burden of a general-purpose voxel engine.

# 5. Characters, cosmetics, progression and season pass

## 5.1 Character architecture

Use one high-quality stylized 3D base character for the MVP. Build the character as modular cosmetic slots so the system can grow without re-authoring the entire mesh.

> Character  
> \|- Body  
> \|- Head / Hair  
> \|- Outfit  
> \|- Shoes  
> \|- Accessories  
> \|- Trail / VFX  
> \|- Emote / Animation set

## 5.2 3D asset workflow

Recommended creation tool: Blender for custom assets and cleanup, then export to Unity. Do not make every asset from scratch if licensed or CC0 assets can accelerate the MVP. The game’s identity should come from the character, color language, materials, UI and a small set of recognizable environmental props.

## 5.3 Cosmetic philosophy

- **No pay-to-win.** Cosmetics change appearance, effects, animation or emotes—not competitive stats.

- **Theme-driven skins.** Examples: Explorer, Cyber Ninja, Alpine Runner, Robot, Alien.

- **World-linked cosmetics.** Completing a world can unlock a cosmetic related to that world.

- **Earn + buy.** Some cosmetics are earned through progression; premium cosmetics may be purchased.

## 5.4 Season pass concept

Working concept: “Season 1 — World Runner.” Keep the pass intentionally small for Shipaton: perhaps 10–15 reward nodes, with 3–4 real/high-quality cosmetics and a few trails/emotes/currency rewards. Do not build a giant 100-tier system before the core game is fun.

| **Tier example** | **Reward type**                     |
|------------------|-------------------------------------|
| Level 1          | Starter cosmetic / profile frame    |
| Level 5          | World-themed skin                   |
| Level 10         | Trail / VFX                         |
| Level 15         | Premium-looking character variation |
| Level 20         | Legendary “World Runner” cosmetic   |

## 5.5 Progression

> Run -\> Score -\> XP -\> Level -\> Unlock -\> New cosmetic / world access -\> Daily challenge

# 6. Social, retention and growth mechanics

## 6.1 Daily Run

Every day, publish one globally shared challenge seed. Everyone receives the same world/obstacle layout for that day. This gives users a reason to return and makes scores comparable.

## 6.2 Friend challenge loop

> Player A score -\> share challenge -\> Player B plays -\> Player B beats score -\> A gets notified -\> A returns -\> repeat

This is the preferred growth loop because it naturally connects the game, social sharing, OneSignal notifications and Layers measurement.

## 6.3 Streaks

- Daily Run streak: number of consecutive days played.

- Personal best streak: number of consecutive days beating a threshold.

- Friend streak: consecutive days both players compete.

## 6.4 OneSignal messaging examples

- “Today’s Run is live — can you break 12,000?”

- “Alex just beat your score by 214 points.”

- “Your 7-day run streak ends tonight.”

- “Tomorrow: Neon City Daily Run.”

## 6.5 Social content / Noise strategy

The game is inherently visual and should be easy to demonstrate in short-form video. Repeatable formats can include: “Can you beat me?”, “I built a game controlled by tilting your phone,” “One phone. One TV. Four players,” and “The same world every day—who is \#1?”

# 7. TV/display strategy and multiplatform approach

## 7.1 Core decision

| **DECISION** The phone should be the game machine. The TV should be an optional large display obtained through standard OS mirroring/casting. Do not build a separate TV client for the MVP. |
|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|

## 7.2 iOS

Apple officially supports mirroring an iPhone/iPad screen to Apple TV, AirPlay-compatible smart TVs or a Mac. The user selects Screen Mirroring from Control Centre. This means the MVP can leverage the operating-system mirroring workflow rather than building a custom TV receiver. \[S9\]

## 7.3 Android

Android’s MediaProjection APIs can capture a device display and cast it to other devices, including TVs. Android 14+ also supports app-window sharing, which can share only the selected app rather than the whole screen. User consent is required for each MediaProjection session. \[S8\]

## 7.4 UX implication

The app should treat TV mirroring as a “make this bigger” action, not a hard technical dependency. The first-run prompt can say “For the best experience, mirror this game to your TV.” The game must remain playable directly on the phone.

## 7.5 Important caveat

| **WATCHOUT** Do not assume every TV, Android device or iOS device will provide identical latency or mirroring behavior. The first technical spike must test actual gameplay latency over a representative Android and iOS device/TV combination. |
|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|

## 7.6 Multiplatform target

Target iOS and Android with a shared Unity game codebase. The app does not need a custom TV app.

# 8. Computer-vision approach: MediaPipe / native CV / OpenCV / Roboflow

## 8.1 Core observation

The camera-control idea is technically viable. MediaPipe is explicitly designed for on-device ML and provides human pose estimation. The legacy Pose solution describes 33 3D body landmarks and supports Android and iOS examples; current MediaPipe Tasks are the modern path and process inputs on-device rather than sending frames to Google servers. \[S6\] \[S6b\]

## 8.2 Do not start with “AI understands running”

We do not need a sophisticated classifier for the first prototype. Pose landmarks can be converted into simple game controls with geometry and temporal thresholds. Example: torso/hip center moves left of a neutral band =\> LEFT; moves right =\> RIGHT; rapid vertical torso displacement or a validated ankle/hip trajectory =\> JUMP.

## 8.3 Recommended perception architecture

> Camera frame  
> -\> native / MediaPipe pose estimation  
> -\> body landmarks  
> -\> lightweight movement state detector  
> -\> abstract GameInput (LEFT/RIGHT/JUMP/SLIDE)  
> -\> Unity game

## 8.4 Why separate CV from gameplay

Unity can integrate MediaPipe via community/native plugins, so integration is technically possible. However, keeping the CV layer platform-native reduces coupling between the 3D engine and camera/ML dependencies. Unity should consume a small, stable input interface rather than knowing whether an input originated from MediaPipe, Apple Vision, gyroscope or touch.

## 8.5 OpenCV

OpenCV is a strong cross-platform image-processing toolbox and may be useful for preprocessing, filtering, optical-flow experiments or geometric operations. It should not be the primary human-pose estimator for this project unless a specific need emerges. \[S7\]

## 8.6 Roboflow

Roboflow becomes interesting if we later need a custom model for a bespoke gesture or object detector. Its iOS SDK supports on-device inference for trained models. That makes it a candidate for a second-stage custom gesture model, not the preferred first-stage pose solution. \[S10\]

## 8.7 Proposed input abstraction

> interface IGameInput  
> GetMoveAxis()  
> IsJumpPressed()  
> IsSlidePressed()  
> IsSpecialPressed()

Implement adapters: GyroInput, CameraPoseInput, TouchInput. The game should never call MediaPipe directly.

# 9. Game engine decision: Unity and alternatives

## 9.1 Recommended choice: Unity

Unity remains the preferred game engine because the project needs polished mobile 3D, character animation, physics, procedural chunk streaming, asset import, VFX and rapid iteration. The engine’s value is in the game layer, not in owning the perception layer.

| **Option**               | **Strengths**                                                                                   | **Weaknesses**                                                                      | **Recommendation**             |
|--------------------------|-------------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------------|--------------------------------|
| Unity                    | Mobile 3D, animation, physics, asset ecosystem, rapid prototyping, shared iOS/Android game code | Native CV plugins require careful integration                                       | PRIMARY                        |
| Unreal                   | Excellent rendering and 3D toolset                                                              | Heavier for a small mobile-first hackathon team                                     | Not necessary                  |
| Godot                    | Lightweight, open source, good iteration                                                        | Smaller ecosystem; mobile/C# integration is less attractive for this specific stack | Possible but not preferred     |
| React Native + 3D        | Strong mobile UX and TS ecosystem                                                               | Not ideal for polished 3D game pipeline/physics/asset workflow                      | Not preferred                  |
| Native Swift/Kotlin game | Maximum platform control                                                                        | High cost to build cross-platform 3D game systems                                   | Too much custom infrastructure |

## 9.2 Current Unity AI situation

Unity’s current AI tooling includes an editor-integrated agentic assistant, AI Gateway and official MCP Server. The assistant is project-aware and designed for Unity workflows. Unity also states that Sentis remains the runtime for running neural-network models natively inside Unity. \[S11\]

Use Unity AI as a development productivity layer—not as a reason to put perception inside Unity. Good uses include generating boilerplate, ScriptableObject definitions, editor tools, chunk-manager code, tests and repetitive scene setup. Every generated system still requires code review and runtime validation.

# 10. Recommended technical architecture

## 10.1 High-level

> MOBILE APP  
>   
> ┌─────────────────────────────────┐  
> │ UNITY │  
> │ Runner / World / UI / Audio │  
> │ Physics / Animation / Scoring │  
> │ Progression / Cosmetics │  
> └──────────────┬──────────────────┘  
> │ GameInput API  
> ┌──────────────┴───────────────┐  
> │ │  
> Sensor adapter Camera adapter  
> Gyroscope / accel MediaPipe/native CV  
> │ │  
> └──────────────┬───────────────┘  
> │  
> User controls  
> │  
> Optional OS screen mirroring  
> │  
> ▼  
> TV

## 10.2 Game modules

| **Module**           | **Responsibility**                                             |
|----------------------|----------------------------------------------------------------|
| Bootstrap / AppState | Launch, mode selection, device checks.                         |
| InputSystem          | Normalizes gyro/camera/touch inputs into GameInput.            |
| RunnerController     | Applies lane/axis movement, jump and slide actions.            |
| TrackGenerator       | Maintains chunks ahead and recycles chunks behind.             |
| ChunkLibrary         | ScriptableObjects / prefabs and metadata.                      |
| ObstacleSystem       | Collision, timing, patterns, difficulty.                       |
| WorldTheme           | Biome visuals, music, lighting, chunk pools.                   |
| ScoreSystem          | Distance, coins, combo, damage, score multiplier.              |
| Progression          | XP, levels, unlocks, streaks, season progress.                 |
| Cosmetics            | Character customization, trails, emotes.                       |
| Social               | Daily seed, leaderboards, challenges, share payload.           |
| Monetization         | RevenueCat products/entitlements/paywalls.                     |
| Notifications        | OneSignal tags, campaigns, journeys.                           |
| Analytics            | Events for run start/end, control mode, conversion, retention. |

## 10.3 Backend philosophy

Keep the backend minimal. Supabase is a reasonable optional choice for authentication, profiles, daily seeds, scores and lightweight content metadata. Do not introduce a backend dependency until the single-player game is playable. If the game can ship the MVP without login, postpone mandatory accounts.

## 10.4 Data entities

> User  
> - id  
> - displayName  
> - cosmeticLoadout  
> - XP / level  
> - seasonProgress  
>   
> DailyRun  
> - date  
> - worldId  
> - seed  
> - version  
>   
> RunResult  
> - userId  
> - dailyRunId  
> - score  
> - distance  
> - timestamp  
>   
> Cosmetic  
> - id  
> - type  
> - rarity  
> - source (earned/store/season)

# 11. Sponsor strategy and implementation requirements

This section distinguishes official category criteria from our recommended product implementation. Prize amounts and requirements below are taken from the official Devpost rules/overview checked on 20 August 2026. \[S1\] \[S2\]

## 11.1 Priority matrix

| **Target**                        | **Current 1st prize**                                       | **Why it fits**                                              | **What we should implement**                                                        | **Priority**                 |
|-----------------------------------|-------------------------------------------------------------|--------------------------------------------------------------|-------------------------------------------------------------------------------------|------------------------------|
| Best Game                         | USD 15,000                                                  | This is the product itself                                   | Great gameplay, art direction, genre-fit monetization                               | MUST                         |
| Keep Them Coming Back — OneSignal | USD 25,000                                                  | Daily Run and challenge loop naturally creates re-engagement | At least one campaign; ideally Daily Run + streak + friend-beat-you Journey         | MUST                         |
| RevenueCat Design                 | USD 15,000                                                  | Game is visual; polish can differentiate                     | World transition, motion, animation, progression, cosmetics, clean HUD              | HIGH                         |
| Most Viral — Noise                | USD 15,000                                                  | The concept is demonstrable in short video                   | Repeatable “beat my score / phone controller / hands-free” content formats          | HIGH                         |
| Growth Loop — Layers              | USD 15,000                                                  | Challenge/share/beat-score is a natural loop                 | Install Layers, define hypothesis, measure one growth experiment, document learning | HIGH                         |
| HAMM                              | USD 15,000                                                  | Cosmetics/season pass fit mobile game economics              | Thoughtful paywall, cosmetic pricing, Season Pass, no pay-to-win                    | MEDIUM-HIGH                  |
| Funnel Vision — Stripe            | USD 15,000                                                  | Could sell season pass via web funnel                        | RevenueCat Funnels + Stripe checkout + live web-to-app funnel                       | OPTIONAL LATER               |
| Replit Idea to Income             | USD 15,000                                                  | Possible only if app is built via Replit/Agent               | Would conflict with Unity-first architecture                                        | SKIP unless strategy changes |
| Ship Kotlin Everywhere            | USD 15,000                                                  | Requires KMP/Compose Multiplatform for iOS+Android           | Would force a different stack                                                       | SKIP                         |
| Gaming Influencer                 | USD 15,000                                                  | Not about game development; it is a gaming backlog app       | Would require building a different product                                          | SKIP                         |

## 11.2 Official sponsor facts

OneSignal: eligibility requires integrating OneSignal and deploying at least one campaign through the OneSignal API, MCP or dashboard, then describing the campaign and providing the OneSignal App ID. A single deployed message is enough for eligibility, but more thoughtful use can score better. \[S2\]

Layers: eligibility requires the Layers SDK, a description of the growth loop, audience/message/channel/product surface/experiment/intended outcome, and a description of what was learned from the signal and what comes next. \[S2\]

Stripe: eligibility requires a live web-to-app funnel using RevenueCat Funnels with Stripe as checkout provider, including a live URL and Stripe Project ID. \[S2\]

## 11.3 Sponsor integration blueprint

> BEST GAME  
> \|  
> +-- core gameplay + art + genre-fit monetization  
>   
> ONESIGNAL  
> \|  
> +-- Daily Run  
> +-- streak  
> +-- “friend beat your score”  
>   
> LAYERS  
> \|  
> +-- share score -\> friend installs -\> challenge -\> return  
>   
> NOISE  
> \|  
> +-- repeatable short-form content  
>   
> REVENUECAT / HAMM  
> \|  
> +-- cosmetics + season pass + paywall experiments  
>   
> STRIPE (later)  
> \|  
> +-- web season-pass funnel

## 11.4 BuildInPublic

The official rules say #BuildInPublic judges sharing the journey, engagement and lessons learned; audience size does not matter. Posts should be publicly visible and linked in the Devpost submission. \[S2\] This is worth doing regardless of other sponsors because the development itself is unusually visual: sensor prototype -\> first motion -\> first track -\> first TV mirror -\> first daily challenge -\> first user score.

# 12. MVP scope, roadmap and cut lines

## 12.1 MVP definition

| **WATCHOUT** MVP success condition: one complete run feels responsive, visually coherent and replayable on a real phone. If that is not fun, do not spend time on seasons, multiplayer or complex CV. |
|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|

- Unity mobile build running on Android first; architecture kept portable to iOS.

- One stylized biome.

- One character.

- Three controls: left, right, jump.

- Gyro/accelerometer control as the first input mode.

- Endless chunked track with 10–20 reusable chunks.

- Obstacle/collision system.

- Score + distance + coins + simple combo.

- Post-run result screen.

- Optional phone-to-TV mirroring using OS capabilities.

## 12.2 Phase 2 — retention and social

- Daily seeded challenge.

- Persistent best score.

- Shareable challenge card/deep link.

- Basic leaderboard.

- XP and levels.

- 3–4 cosmetic rewards.

- OneSignal integration and one thoughtful Journey/campaign.

- Layers experiment around challenge sharing.

## 12.3 Phase 3 — sponsor differentiators

- Noise content pipeline / repeated social formats.

- RevenueCat paywall + cosmetic product catalog.

- Small Season 1 pass.

## 12.4 Phase 4 — optional

- iOS camera pose mode.

- More biomes.

- Friend-versus-friend party mode.

- Web-to-app Stripe funnel.

- More advanced gesture model via Roboflow or another custom model.

## 12.5 Cut list — do not build unless everything above is stable

- Minecraft/voxel terrain.

- Persistent 3D real-world cities.

- Dedicated TV application.

- Real-time networked 4-player simulation.

- Large 100-tier battle pass.

- Large custom-trained vision model.

- Complex backend infrastructure.

- Dozens of characters.

## 12.6 Suggested schedule to submission

| **Window** | **Outcome**                                                                        |
|------------|------------------------------------------------------------------------------------|
| Days 1–3   | Technical spike: Unity mobile build + gyro input + basic runner.                   |
| Days 4–7   | Chunk recycling + one biome + obstacles + scoring.                                 |
| Week 2     | Polish, character, audio, result screen, TV-mirroring validation.                  |
| Week 3     | Daily Run + seed + leaderboard/share proof-of-concept.                             |
| Week 4     | RevenueCat + basic cosmetics + Season 1 shell.                                     |
| Week 5     | OneSignal + Layers experiments.                                                    |
| Week 6     | Noise content + App Store/Play release work + QA.                                  |
| Final days | Submission video, screenshots, category answers, sponsor proof, analytics/results. |

# 13. Risks, assumptions and open technical questions

| **Question / risk**                         | **Why it matters**                                      | **Proposed test / decision**                                            |
|---------------------------------------------|---------------------------------------------------------|-------------------------------------------------------------------------|
| Is gyro control actually fun?               | Everything depends on responsiveness and intuitiveness. | Build 1-hour prototype and play-test before adding systems.             |
| Is camera mode reliable enough?             | Lighting/pose visibility may vary.                      | MediaPipe/native CV prototype with 3 gestures; measure false positives. |
| Can the phone mirror at acceptable latency? | Runner gameplay is latency-sensitive.                   | Test iPhone AirPlay + Android casting/MediaProjection on real TVs.      |
| Will the game stay 60 FPS?                  | TV mirroring does not fix rendering performance.        | Profile on mid-tier Android + recent iPhone; keep assets stylized.      |
| How much UI can remain on the phone?        | Mirroring duplicates the phone UI.                      | Design a mirrored-safe “TV layout” and keep HUD minimal.                |
| How do daily seeds remain stable?           | Version changes can alter generation.                   | Store generator version with the seed.                                  |
| What is the correct season economy?         | Too cheap/expensive can hurt HAMM credibility.          | Prototype paywall and 2–3 price points using analytics.                 |

## 13.1 Important assumptions

- The core game can ship without a separate TV application.

- The user is allowed to use standard device mirroring/casting for the big-screen experience.

- The camera mode is an enhancement, not a required control mode for the first submission.

- The first world can be composed from a limited chunk library.

- Asynchronous competition provides enough social value for the first submission.

# 14. Handoff checklist for the implementation agent

## 14.1 First task: validate the premise

1. Create a Unity mobile prototype. One runner, one camera, one road plane, no art polish.

2. Implement gyro movement. Left/right must feel immediate and stable.

3. Implement jump. Prefer a simple input first; camera-detected jump is optional.

4. Test phone screen mirroring. Android and iOS representative devices.

5. Record a 20–30 second proof clip. A person controls the runner while the phone screen is mirrored to a TV.

## 14.2 Second task: prove procedural illusion

1. Create 5–10 chunks. Straight, curve, jump, obstacle, tunnel, scenic, etc.

2. Implement chunk recycling. Maintain N chunks ahead; recycle behind.

3. Add deterministic seed. Same seed = same chunk sequence.

4. Stress test for several minutes. Confirm no gaps, memory growth or obvious repetition.

## 14.3 Third task: sponsor skeleton

- RevenueCat: one real purchase and a minimal cosmetic SKU.

- OneSignal: one deployed campaign; ideally a Daily Run Journey.

- Layers: instrument challenge share loop and run a focused experiment.


- Noise: prepare repeatable social formats and publish consistently.

- #BuildInPublic: log public milestones and link the posts for submission.

## 14.4 Submission discipline

RevenueCat’s judging article says submissions are filtered for store publication, RevenueCat SDK verification, required submission fields, video and marketing assets. Each project is assigned to at least two screeners. Judges must read the description, watch at least two minutes of the video, review screenshots and score each applicable category. Close to 100 apps make it to the final judging round. \[S3\]

| **WATCHOUT** The first two minutes of the submission video must make the product instantly understandable: person + phone/controller + TV + game + core differentiator + targeted categories. Do not spend the opening minutes on architecture diagrams. |
|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|

# 15. Sources and research notes

All sources below were consulted for this handoff. Contest facts were checked against official RevenueCat/Devpost materials on 20 August 2026. Technology sources are official vendor/developer documentation where possible.

**S1 — RevenueCat Shipaton 2026 — Devpost overview** — [https://revenuecat-shipaton-2026.devpost.com/](https://revenuecat-shipaton-2026.devpost.com/)  
Official contest overview; deadline; participant snapshot; app requirements; categories; sponsor list.

**S2 — RevenueCat Shipaton 2026 — Devpost rules** — [https://revenuecat-shipaton-2026.devpost.com/rules](https://revenuecat-shipaton-2026.devpost.com/rules)  
Official eligibility, sponsor criteria, implementation requirements, judging and prize details.

**S3 — How we judge Shipaton — Shipaton / RevenueCat** — [https://www.shipaton.com/blog/how-we-judge-shipaton](https://www.shipaton.com/blog/how-we-judge-shipaton)  
Official judging process, filtering, two-minute video emphasis, historical scale of reviews.

**S4 — Shipaton 2025 Winners — Shipaton / RevenueCat** — [https://www.shipaton.com/blog/shipaton-2025-winners](https://www.shipaton.com/blog/shipaton-2025-winners)  
Historical examples and 2025 submission/winner context; 812 projects in 2025 and winner patterns.

**S5 — Announcing Shipaton 2026 — Shipaton / RevenueCat** — [https://www.shipaton.com/blog/announcing-shipaton-2026](https://www.shipaton.com/blog/announcing-shipaton-2026)  
Official announcement of 2026 categories and sponsor set.

**S6 — MediaPipe / Google AI Edge** — [https://github.com/google-ai-edge/mediapipe](https://github.com/google-ai-edge/mediapipe)  
On-device ML, cross-platform media pipelines, pose estimation, mobile support and privacy notes.

**S6b — MediaPipe Pose documentation** — [https://github.com/google-ai-edge/mediapipe/blob/master/docs/solutions/pose.md](https://github.com/google-ai-edge/mediapipe/blob/master/docs/solutions/pose.md)  
Pose landmarks and Android/iOS example references; legacy page points to current Pose Landmarker path.

**S7 — OpenCV — Platforms** — [https://opencv.org/platforms/](https://opencv.org/platforms/)  
Cross-platform computer-vision capabilities and platform support.

**S8 — Android Developers — MediaProjection** — [https://developer.android.com/media/grow/media-projection](https://developer.android.com/media/grow/media-projection)  
Official Android screen capture/casting capability; user consent; Android 14+ app-window sharing.

**S8b — Android Developers — App screen sharing** — [https://developer.android.com/about/versions/14/features/app-screen-sharing](https://developer.android.com/about/versions/14/features/app-screen-sharing)  
Official Android 14+ app-window screen-sharing behavior.

**S9 — Apple Support — AirPlay screen mirroring** — [https://support.apple.com/en-ie/102661](https://support.apple.com/en-ie/102661)  
Official iPhone/iPad screen mirroring workflow to Apple TV/AirPlay-compatible TV/Mac.

**S10 — Roboflow Docs — iOS SDK** — [https://docs.roboflow.com/developer/ios-sdk/using-the-ios-sdk](https://docs.roboflow.com/developer/ios-sdk/using-the-ios-sdk)  
On-device model inference and custom CV deployment on iOS.

**S11 — Unity — AI tools** — [https://unity.com/features/ai](https://unity.com/features/ai)  
Current Unity AI assistant/agentic tooling, MCP Server, AI Gateway and Sentis status.

## Research notes / corrections from earlier discussion

The current official rules show that several prize amounts are lower than earlier rough estimates discussed in chat. For example, Best Game, RevenueCat Design and HAMM are currently listed at USD 15,000 for first place, while OneSignal is USD 25,000. This handoff uses the current official rules as the source of truth. \[S2\]

The Devpost overview currently shows 18,574 participants. This is a live registration count, not the number of submitted projects. Historical data from Shipaton/Shipyard indicates 60,630 participants, 8,904 apps started, 2,125 apps in stores and USD 7.8M revenue across the prior two years; Shipaton 2025 alone had 812 submitted projects. These figures should be treated as historical context, not a forecast of 2026 submissions. \[S1\] \[S3\] \[S4\]

## Final handoff directive

| **DECISION** When implementing, optimize for one polished, demonstrably fun core interaction first: camera/gyro movement -\> responsive runner -\> beautiful moving world -\> satisfying score result. Everything else should be justified by one of four purposes: make the game better, make users return, make users share/invite, or strengthen a target Shipaton category. |
|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
