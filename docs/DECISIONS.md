# Decision log (append-only)

Settled decisions. Agents: do not relitigate; propose changes via OPEN_QUESTIONS.md. Full rationale for D1–D9 lives in `shipaton_motion_runner_handoff_v2.md`.

| # | Date | Decision |
|---|---|---|
| D1 | 2026-08-20 | Engine: Unity 6 LTS, single codebase for Android + iOS. |
| D2 | 2026-08-20 | v1.0 input: gyro/tilt + touch only. Camera mode is a parallel track and ships only as a post-release update if it meets the quality bar. |
| D3 | 2026-08-20 | CV path: BlazePose via Unity Inference Engine (primary) → homuler MediaPipeUnityPlugin (secondary) → native bridges (last resort). All on-device; no servers. |
| D4 | 2026-08-20 | World: chunk-recycling endless track, deterministic from seed; no voxel/persistent world. One biome at launch. |
| D5 | 2026-08-20 | Release strategy: v1.0 to store review by ~Sep 12, then iterate with updates inside the Shipaton window. Store/account pipeline starts immediately. |
| D6 | 2026-08-20 | Monetization: cosmetics only (no pay-to-win), via RevenueCat `purchases-unity`; a purchasable SKU and judge promo-code path ship in v1.0. |
| D7 | 2026-08-20 | Target categories: Best Game, Grand Prize (via early ship + growth), #BuildInPublic, OneSignal, Design, Layers, Noise, HAMM, Samsung (HIGH — likely low competition; Fold flex-mode camera showcase), Catvertising (only if T-023 spike passes). Skip: Stripe, Replit, JetBrains, Influencer, Peace, Next Gen. |
| D8 | 2026-08-20 | TV: OS-level mirroring only (AirPlay / casting); no dedicated TV app. Pitch emphasis depends on measured latency (T-034). |
| D9 | 2026-08-20 | Multiplayer: asynchronous only (shared daily seed + scores). No real-time networking during Shipaton. |
| D10 | 2026-08-20 | Backend: none for v1.0 (date-derived daily seed, local scores). Supabase only when the shared leaderboard ships. |
| D11 | 2026-08-20 | Release strategy (per owner's account situation): **Android-first on Google Play** — new personal developer account registered immediately, closed testing (12×14) started with the earliest APK, production release targeted ~Sep 22. **iOS is optional**, via a friend's Apple Developer account: Windows Unity exports the Xcode project, friend builds/uploads. Dev machine: this Windows PC. |
| D12 | 2026-09-16 | **Samsung / Best App for Galaxy dropped; Galaxy Store is not a distribution target.** Publishing an app with in-app purchases on Galaxy Store needs a Samsung *commercial* seller account, which needs a registered business entity the project does not have, so the category's mandatory live Galaxy Store URL is unobtainable. Supersedes the Samsung entry in D7 and the Galaxy backup-store clause in D11. Consequences: T-033 dropped; P3 and H5 not applicable; no Fold/foldable work; **Google Play is now single-threaded**, with iOS (P2) the only fallback if Play review rejects. The game itself is unchanged. Rationale and the full list of docs edited: STATUS 16 Sep. |
