# Prerequisites — human-only checklist

Agents cannot do these. Items marked **[BLOCKING]** gate the schedule. Update the Status column as you go; agents read this file.

## Accounts & money

| # | Item | Why | Lead time | Status |
|---|---|---|---|---|
| P1 | **[BLOCKING]** Register a NEW personal Google Play developer account TODAY ($25 one-time, play.google.com/console/signup). Confirmed 20 Aug: owner has an old Google account but **no Play developer account** — the Nov-2023 cutoff is about the *developer* account, so the **12 testers × 14 days closed-testing rule applies**, plus identity verification for new accounts (can take days) | Register → verify → closed test from ~Sep 1 → apply for production ~Sep 15 → release ~Sep 22. Any slip kills the Play release | Every day counts | ✅ registered + verified (24 Aug, owner-reported). **Next: `docs/PLAY_CONSOLE_SETUP.md`** |
| P2 | Apple side — friend's existing Apple Developer account (confirmed as an option 20 Aug). Workflow: Unity on Windows exports the Xcode project → friend archives/signs/uploads on their Mac (runbook: T-032). App will be listed under the friend's developer name — acceptable for Devpost (submission needs the store URL; RevenueCat verification is by bundle ID) | iOS release path without owning a Mac | Friend needs Xcode + ~1–2h per release | ☐ friend confirmed ☐ runbook tested |
| P3 | Samsung Galaxy Store commercial seller account | Samsung category requires Galaxy Store publication | Days; approval is manual | ☐ |
| P4 | RevenueCat account + project | Eligibility requirement | Minutes | ✅ COMPLETE 29 Aug — service credentials "valid", Play products created + attached, license-test purchase verified end-to-end through the real Play Store |
| P5 | OneSignal account | $25k category | Minutes | ☐ |
| P6 | Layers account + App ID | $15k category | Minutes | ☐ |
| P7 | Noise account | $15k category | Minutes | ☐ |
| P8 | Devpost registration for Shipaton 2026 (also unlocks Ship Kit freebies) | Required to submit | Minutes | ☐ |
| P9 | Social account(s) for #BuildInPublic (X and/or LinkedIn/TikTok) — first post | $30k category; judged on journey, not audience size | Minutes | ☐ |
| P10 | Store payout/tax profiles (Play, App Store, Galaxy) | IAP cannot go live without them | Can take days | ⚠️ Play merchant profile ✅ created + linked (~27 Aug, W-8BEN treaty claim filed). **Payout bank account still unwired** — non-blocking (sales accrue; only payouts wait), wire within ~2 weeks. Galaxy/Apple untouched |

## Hardware & tooling

| # | Item | Why | Status |
|---|---|---|---|
| H1 | One Android phone with USB debugging enabled (Settings → Developer options) | Primary dev/test device; perf target | ✅ available (20 Aug) — enable USB debugging |
| H2 | One iPhone | iOS validation + AirPlay latency test | ☐ |
| H3 | macOS for iOS builds — resolved via friend's Mac (see P2). Unity on Windows CAN export the Xcode project; only compile/sign/upload needs the Mac. Fallback: Unity Build Automation (cloud) | iOS builds cannot be finished on Windows | ☐ friend's Mac confirmed |
| H4 | TV with Chromecast/AirPlay | Mirroring latency spike (T-004) | ☐ |
| H5 | Galaxy Fold (or any recent Galaxy) | Samsung category testing; Fold flex-mode demo | ☐ |
| H6 | **[BLOCKING]** 12 testers recruited (friends / Shipaton Discord mutual-testing channels) — must be opted in and have installed the closed-test build by ~Sep 1 | Closed testing DOES apply (see P1) | ☐ |

## Dev machine (Windows, agents can help)

| # | Item | Status |
|---|---|---|
| D1 | Unity Hub + Unity 6 LTS (6000.x) with Android Build Support (SDK/NDK/OpenJDK bundled) | ✅ 6000.5.9f1; headless Android builds proven (21 Aug) |
| D2 | Git installed; repo initialized with Unity .gitignore (+ Git LFS for binary assets) | ⚠️ repo + .gitignore + `origin` → github.com/ferrabled/Veyro. **Git LFS not set up** — do it before T-006 lands binary art |
| D3 | adb working with H1 connected | ✅ verified 21 Aug (DN2103, install/launch/screencap/logcat loop) |
| D4 | Blender (asset work, later) | ☐ |
| D5 | **[BLOCKING]** Upload keystore for Play signing — owner-only because the password is a secret agents must never hold. Create once, keep it OUTSIDE the repo (e.g. `%USERPROFILE%\.keys\veyro-upload.keystore`), back it up (losing it means a Play support ticket to reset the upload key): `keytool -genkeypair -v -keystore veyro-upload.keystore -alias upload -keyalg RSA -keysize 2048 -validity 10000` (keytool ships in Unity's OpenJDK: `Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin`). Then set env vars `VEYRO_KEYSTORE` + `VEYRO_KEYSTORE_PASS` for `BuildScript.BuildAndroidBundle`. On the first Play release, accept the Play App Signing ToS and let Google generate the app signing key (the keystore here is only the *upload* key) | ✅ created + used for the 25 Aug internal-track upload (owner, STATUS 25 Aug) — keep the runbook here for key loss/recovery |
