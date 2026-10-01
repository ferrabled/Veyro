# T-032 — App Store Connect listing copy and field values

Drafted 28 Sep 2026 (feat-game-UI-improvement iOS session), companion to the Play copy in
`LISTING.md`. Every value below is typed into App Store Connect by the owner; nothing here is
submitted by an agent. Account state on 28 Sep: Apple Developer Program (individual) approved,
Free + Paid Apps agreements **Active**, bank + W-8BEN **Active**, App ID
`com.ferrabled.veyro.run` registered (In-App Purchase + Push Notifications), app record created
(SKU `veyro-run-ios`), the three IAPs created as drafts.

**Camera mode ships on iOS too** (owner, 28 Sep) — the subtitle, keywords and description
advertise it, so the iOS build must carry it working: `NSCameraUsageDescription` set, the iOS
permission path handled (today `CameraFeed.HasPermission()` returns true on every non-Android
platform), and the front-camera orientation checked on an iPhone before submission.

Rules inherited from `LISTING.md` that still bind here:

1. Camera mode is always labelled **BETA**, with the "choose it from a menu / needs light and
   space / slow phones fall back to tilt" sentences next to it.
2. No automotive words next to the name (race, speed, drive, turbo…) — the VEYRON concern.
3. No sliding claims until the slide mechanic ships. The camera move is **STEP** left/right
   (owner copy change, STATUS 28 Sep), not "lean".
4. No absolute offline/no-network claims — runs play offline; leaderboard, shop, challenges,
   notifications and optional analytics use a connection.

Character limits (App Store Connect): name 30, subtitle 30, promotional text 170, keywords 100,
description 4,000, IAP display name 35, IAP description 55.

---

## 1. App Information (General → Información de la app)

| Field | Value |
|---|---|
| Name | `Veyro Run` |
| Subtitle | **`Tilt or camera: daily runner`** (28) |
| Primary category | Games → subcategories **Arcade**, **Action** |
| Secondary category | empty |
| Content rights | Third-party art/audio is licensed → "Yes, and I have the necessary rights" (No only if every asset is original) |
| Age rating | Every question **None / No** — step-by-step answers below. Expected **4+**, matching the Play PEGI 3 filing |
| Made for Kids | No |
| App Store Server Notifications | RevenueCat's Apple server-notification URL, **Production and Sandbox**, version 2 |
| Encryption documentation | Nothing to upload — HTTPS through the OS + hashing only (exempt); the build sets `ITSAppUsesNonExemptEncryption=false` |
| DSA / Vietnam game licence / regulated medical device | DSA: after submission (payments can be delayed without it). Vietnam: excluded instead. Medical device: not applicable |
| App-specific shared secret | Not needed — RevenueCat validates with the In-App Purchase key; no subscriptions |

**Age rating questionnaire (7 steps), answers and why:**

| Step | Question | Answer | Why |
|---|---|---|---|
| 1 Capabilities | Parental controls · Age assurance | No · No | none in the app |
| 1 | Unrestricted web access | No | policy/support links open Safari outside the app; no in-app browser |
| 1 | User-generated content · Social media · Social media off for under-13 | No · No · No | handles are server-generated rerolls, no free text anywhere; challenge links go out through the system share sheet (or clipboard), not an in-app feed |
| 1 | Messaging and chat · Advertising | No · No | no player-to-player messages; no ads (T-023) |
| 2 Mature themes | Profanity · Horror · Alcohol/tobacco/drugs · Mature/suggestive | None | — |
| 3 Medical/wellness | Medical info · Health or wellness topics | None / No | motion game, no fitness or health claims |
| 4 Sexuality/nudity | all | None | cosmetics are stylised outfits |
| 5 Violence | Cartoon/fantasy · Realistic · Prolonged graphic · Weapons (if asked) | None | no enemies or fighting — a crash is the runner tripping on an obstacle; the quiver is a decorative back item. Conservative alternative: "Infrequent/Mild cartoon" → 9+, harmless for the submission |
| 6 Chance | Simulated gambling · Contests · Gambling · Loot boxes | No | leaderboards award nothing; Season rewards are fixed per level, never random |
| 7 Summary | Made for Kids · rating override | No · none | Kids category would add review rules for no benefit |

Subtitle alternates (all counted):

| Option | Chars | Note |
|---|---|---|
| `Tilt or camera: daily runner` | 28 | Recommended — tilt, camera, daily, runner all indexed |
| `Tilt or camera. Daily runs.` | 27 | Same terms, "runs" instead of "runner" |
| `Steer with tilt or camera` | 25 | Loses "daily"/"runner" |
| `Tilt to run. New track daily.` | 29 | Earlier draft, no camera — superseded |

## 2. Pricing and Availability

| Field | Value |
|---|---|
| Price | Free |
| Countries | All **except the 27 EU storefronts** until the DSA trader declaration is verified; then add them (no new review needed). Also exclude **Vietnam** (games need a local game licence) and **China mainland** (games need an approval/ISBN licence) |
| Apple Silicon Mac availability | **Off** (no tilt, no front camera) |
| Apple Vision Pro availability | **Off** |
| Distribution | Public |
| Pre-order | No |

## 3. App Privacy

Privacy policy URL: `https://veyro.ferrabled.com/privacy/`. Data is collected. Every row: **linked
to the user: Yes**, **used for tracking: No**. Mirrors the Play Data safety rows in
`STORE_COMPLIANCE.md`; drop the OneSignal/Layers rows if either SDK is switched off in the iOS
build before submission.

| Apple data type | Source | Purposes |
|---|---|---|
| Purchases → Purchase History | RevenueCat | App Functionality, Analytics |
| Identifiers → User ID | Supabase / RevenueCat app user ID | App Functionality, Analytics |
| Identifiers → Device ID | OneSignal, RevenueCat, Layers installation IDs | App Functionality, Analytics |
| User Content → Gameplay Content | Runs/scores submitted to the leaderboard | App Functionality |
| Usage Data → Product Interaction | Layers, OneSignal | Analytics |
| Location → Coarse Location | Layers (region derived from IP) | Analytics |
| Diagnostics → Other Diagnostic Data | Layers SDK delivery health | Analytics |

Camera frames are **not** declared: processed on-device, never transmitted.

**Tracking: No** ("Do you or your third-party partners use data for tracking?" → No), and there is
no App Tracking Transparency prompt. The export enforces this rather than trusting it:
`IosPrivacyPostProcess` removes the Layers SDK's ATT/IDFA bridge from every iOS export and
compiles a no-op stub in its place, then fails the export if `NSUserTrackingUsageDescription`,
`SKAdNetworkItems`, `NSAdvertisingAttributionReportEndpoint` or an AppTrackingTransparency /
AdSupport link survives (§7, `STORE_COMPLIANCE.md` "iOS / App Store").

**Rows re-checked against the iOS build (28 Sep, Phase 2b); no answer changes:**

- *Device ID* stays true without any Apple identifier. On iOS, Layers now sends **no IDFV** (and
  never an IDFA): its bridge returns none. The row is carried by the random installation IDs each
  SDK generates and stores itself: RevenueCat's anonymous app user ID (every install, at launch),
  OneSignal's subscription ID (every install, at launch), and Layers' SDK device ID + the game's
  `veyro-…` support ID (only after the analytics opt-in).
- *Coarse Location* stays true for Layers, derived by the provider from the connection's IP
  address. The app uses no location API, carries no `NSLocation*` key, and OneSignal's location
  module is not in the build.
- *Product Interaction* stays true through OneSignal alone if Layers is switched off; it drops
  only if both are off.
- The build's privacy manifest (`UnityFramework/PrivacyInfo.xcprivacy`) declares the same seven
  types, linked, not tracking, with the same purposes (`IosPrivacyPostProcess.CollectedData`,
  kept row-for-row with this table: change both together). With a kill switch on, the export
  drops that SDK's rows from the manifest by itself; drop them here too.

## 4. Version 1.0

**Promotional text** (editable without review):

```
A new Daily Run every day: the same track for every player. Tilt to steer, tap to jump, and see where you land on the leaderboard.
```

**Keywords** (94; no words repeated from name/subtitle, which Apple already indexes):

```
motion,endless,arcade,gyro,hands-free,body,lane,jump,casual,dodge,obstacle,accelerometer,track
```

**Description:**

```
Your phone is the controller.

Veyro Run is a motion-controlled endless runner. Tilt left and right to weave across three lanes, tap to jump, and see how far you get. No virtual joystick, no thumbs covering the screen — you steer by moving the device itself.

HANDS-FREE CAMERA MODE (BETA)
Prop your phone up, step back, and play with your body. Camera mode uses the front camera to follow you — step left and right to switch lanes, hop to jump. It runs entirely on your iPhone: no video ever leaves the device, nothing is uploaded, and nothing is stored.
It is labelled BETA on purpose. You choose it from a menu, it needs a reasonably lit room and a couple of metres of space, and on slower phones the game will tell you it is not fast enough and hand you back the tilt controls. Tilt and touch are always there.

A NEW TRACK EVERY DAY
Every player gets the same track on the same day. The Daily Run is generated from the date, so the course you run is exactly the course your friends run. Beat your best, see where you land on the leaderboard, and share a challenge link so a friend can run the same track.

BUILT TO BE PICKED UP
A run lasts as long as you last. No energy meter, no timers, no account to create — your player name is generated for you.

SEASON 1 AND COSMETICS
Earn XP by running and collect daily stamps. The optional Season 1 Pass and runner skins change how you look and nothing else — no boosts, no advantage over players who never spend anything. Purchases can be restored at any time from the SHOP.

YOUR CHOICE
Gameplay analytics stays off until you turn it on, and Daily Run notifications are optional. Both live in PROFILE. Every run works offline; the leaderboard, shop and challenges use a connection.

FEATURES
• Tilt to steer, tap to jump — touch controls always available
• Hands-free camera mode (BETA) — play by moving your body, all on-device
• Daily Run — one shared, identical track per day
• Leaderboard and challenge links
• Season 1 progression and daily stamps
• Cosmetic-only purchases, restorable any time
• Built for one hand and short sessions

Veyro Run is made by one developer, in public. If something feels wrong, tell us — it is the fastest way to change it.
```

| Field | Value |
|---|---|
| Support URL | `https://veyro.ferrabled.com/support/` |
| Marketing URL | `https://veyro.ferrabled.com/` |
| Version | `1.0.0` (must equal `PlayerSettings.bundleVersion`) |
| Copyright | `2026 Fernando Rabasco Ledesma` |
| Sign-in required | No |
| Review contact | owner's name, phone, email (not public) |
| Version release | Automatically after approval |
| Screenshots | iPhone 6.9" **1290×2796** (or 1320×2868), 3–10, RGB, no alpha. iPhone-only build ⇒ no iPad set |
| Build + IAPs | Chosen on the version page at submission; the three IAPs are attached there (first IAPs must ship with a version) |

**App Review notes** (corrected 30 Sep: the first version claimed touch alone could play the game,
but touch only jumps (`TouchTapInput`); steering is tilt or camera):

```
Veyro Run is a motion-controlled endless runner. No account or login is required.

HOW TO PLAY
Hold the phone upright (portrait). Tilt left/right to change lanes and tap the screen to jump. Steering needs tilt (or camera mode); touch jumps.

CAMERA MODE (BETA, optional)
Choose CAMERA (the button tagged BETA) in the mode picker. The front camera is used on-device only to follow the player; no images are stored or transmitted. The camera permission is requested only when the player picks this mode. Tilt remains available if permission is declined.

IN-APP PURCHASES (non-consumable, cosmetic only)
Open the SHOP tab in the bottom bar: Ember Skin, Frost Skin and Season 1 Pass. RESTORE PURCHASES is at the bottom of the SHOP screen.

OPTIONAL FEATURES
PROFILE → NOTIFICATIONS: optional Daily Run notifications.
PROFILE → GAMEPLAY ANALYTICS: optional, off by default.
The app does not track users and does not request App Tracking Transparency permission.
```

## 5. In-app purchases (Monetización → Compras dentro de la app)

Shared: type Non-Consumable; **Family Sharing off** (irreversible once on); availability all
countries (the app's availability is the real limit); price schedule base country Spain; tax
category "Match to parent app"; promotional image empty; review screenshot 1290×2796 RGB — the
current ones are resized Android captures, **replace with iOS captures before submission**.
Do not press "Add to review" per IAP — they go in with the 1.0 version.

| Product id | Reference / display name | Price | Description (≤55) |
|---|---|---|---|
| `skin.ember` | Ember Skin | €2.99 | `Unlock the Ember look for your runner. Cosmetic only.` (54) |
| `skin.frost` | Frost Skin | €2.99 | `Unlock the Frost look for your runner. Cosmetic only.` (54) |
| `season1.pass` | Season 1 Pass | €4.99 | `Unlock the Season 1 pass reward track. Cosmetic only.` (54) |

Review notes, skins (swap the name for Frost):

```
Cosmetic skin for the player character, non-consumable, no gameplay effect.
To find it: open the SHOP tab in the bottom bar and select Ember Skin. After purchase it shows as owned and can be equipped. RESTORE PURCHASES is at the bottom of the SHOP screen.
```

Review notes, pass:

```
Non-consumable. Unlocks the paid track of the Season 1 reward ladder (10 levels, cosmetic rewards only).
To find it: open the SHOP tab in the bottom bar (or the Season Pass card on the RUN tab) and select Season 1 Pass. The level-1 pass reward unlocks immediately after purchase; later rewards unlock as XP is earned from finished runs. Owners keep earning after the Season 1 event ends. RESTORE PURCHASES is at the bottom of the SHOP screen.
```

**Judge access on iOS:** Apple retired IAP promo codes on 26 Mar 2026; **offer codes** replace
them and support non-consumables. Create after approval: one-time-use, free — 10 for
`season1.pass`, 5 each for the skins, mirroring the Play split in `COSMETICS_CATALOG.md`.

## 6. TestFlight

| Field | Value |
|---|---|
| Beta App Description | `Motion-controlled endless runner. Tilt to steer, tap to jump; one shared Daily Run track per day.` |
| Feedback email | `ferrabled+veyro@gmail.com` |
| Marketing / privacy URL | as in §4 / §3 |
| Internal group | `Friends`, automatic distribution on; testers added first as App Store Connect users (role Developer, this app only) |

TestFlight purchases run in the sandbox and cost nothing; no sandbox tester account is needed.

## 7. Strings the iOS build must carry

| Info.plist key | Value |
|---|---|
| `NSCameraUsageDescription` | `Camera mode uses the front camera to follow your movements. Images are processed on your iPhone and never leave it.` |
| `ITSAppUsesNonExemptEncryption` | `false` (HTTPS only) |
| URL scheme | `veyro` (challenge links) |

`NSCameraUsageDescription` is the **only** purpose string in the build. The other system dialogs a
player can meet need none: the notification prompt (PROFILE → NOTIFICATIONS) and iOS's own "Allow
Paste" confirmation when IMPORT PROFILE reads a pasted recovery code (iOS 16+). Both
`BuildScript.BuildIOS` (sets it) and `IosBuildPostProcess` (fails the export unless it is exactly the
text above) pin it.

**Not in Info.plist, ever** (`IosPrivacyPostProcess` removes each one from the app's, UnityFramework's
and the OneSignal extension's Info.plist and fails the export if one survives — each would either
show the player a prompt for something the game does not do, or make App Store Connect expect one,
ITMS-90683):

| Key | Why it is absent |
|---|---|
| `NSUserTrackingUsageDescription` | no App Tracking Transparency: Layers' ATT/IDFA bridge is replaced by a stub at export, and the App Privacy answer is "no tracking" |
| `SKAdNetworkItems`, `NSAdvertisingAttributionReportEndpoint` | no ad-network attribution; Layers' own post-build step stays inert (no `LayersSettings` asset, and the export fails if one appears) |
| `NSLocationWhenInUseUsageDescription` and every other `NSLocation*` key; `location` in `UIBackgroundModes` | no location API: OneSignal's location module is off (`OneSignalSettings.json`), Unity's `UNITY_USES_LOCATION` must stay 0 |
| `NSMotionUsageDescription` | no pedometer: the Input System's `iOSStepCounter.mm` is replaced by a stub at export. Tilt reads the accelerometer, which needs no purpose string |

Likewise no target links `AppTrackingTransparency.framework`, `AdSupport.framework` or
`CoreLocation.framework`, and no native plugin source may reference `ATTrackingManager`,
`ASIdentifierManager`, `CLLocationManager`, `CMPedometer` or `CMMotionActivityManager`.

**Privacy manifest** (`UnityFramework/PrivacyInfo.xcprivacy`, rewritten on every export):
`NSPrivacyTracking` false, `NSPrivacyTrackingDomains` empty, `NSPrivacyCollectedDataTypes` = the §3
rows, required-reason APIs UserDefaults `CA92.1` (PlayerPrefs), FileTimestamp `C617.1` (+ Unity's
own `0A2A.1`), SystemBootTime `35F9.1` (Stopwatch, engine timing; the free-run challenge salt is
a GUID on iOS, not `Environment.TickCount`, because 35F9.1 forbids sending boot time off-device) and
DiskSpace `E174.1` (engine free-space checks). RevenueCat's and OneSignal's manifests arrive with
their pods on the Mac.
