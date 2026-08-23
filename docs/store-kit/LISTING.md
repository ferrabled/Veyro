# T-030 — Google Play store listing copy

Drafted 23 Aug 2026 (no-unity-prep session). **Copy only — no binary assets.** Character limits
verified against Play Console Help on 23 Aug 2026: app name **30**, short description **80**, full
description **4,000**; limits apply to full-width and half-width characters alike [L1].

Owner must approve before anything is pasted into the Console. Nothing here is submitted by an agent.

**Positioning rule for v1.0:** the store build is gyro + touch (D2). Camera mode is on the
`camera-feature` branch and ships as an update. **The v1.0 listing therefore does not mention the
camera at all.** §4 holds the drop-in delta for the day the camera update goes live, marked BETA.
Promising hands-free play in a build that does not have it is the fastest way to earn 1-star reviews
during the exact 14 days the closed test is being judged.

---

## 1. App name (limit 30)

**Recommended:**

```
Veyro Run: Motion Runner
```
24 characters.

Rationale: "Veyro Run" alone (9 chars) wastes 21 characters of the highest-weighted indexable field
in the listing. "Motion Runner" is the two-word phrase the whole positioning rests on and reads as a
descriptor, not keyword stuffing. Play's metadata policy prohibits ranking claims, promo text and
emoji in the title — this has none.

Alternates, if the owner prefers:

| Option | Chars | Note |
|---|---|---|
| `Veyro Run` | 9 | Cleanest brand-only. Weakest for search. |
| `Veyro Run: Tilt Endless Run` | 27 | "Tilt" is the more-searched term; "Endless Run" is awkward English. |
| `Veyro Run — Motion Runner` | 25 | Em dash renders inconsistently in some Play surfaces. Avoid. |

**Also fix in Unity:** `ProjectSettings.asset` currently has `productName: Motion Runner` and
`companyName: DefaultCompany`. `productName` is the launcher label on the phone — it must read
**Veyro Run**. Tracked as step 13 of the T-020 checklist in `docs/REVENUECAT_PLAN.md`.

---

## 2. Short description (limit 80)

**Recommended:**

```
Tilt your phone to run, swerve and jump. A daily endless runner you play by moving.
```

*83 characters — 3 over. Use one of the trimmed versions below; kept here because the owner may
prefer to re-cut it themselves.*

**Ship this one:**

```
Tilt your phone to swerve and jump. A daily endless runner you play by moving.
```
78 characters.

Alternates:

| Option | Chars |
|---|---|
| `Your phone is the controller. Tilt to swerve, jump and chase a daily best.` | 74 |
| `An endless runner you steer by tilting. New track every day. Plays offline.` | 75 |
| `Tilt to swerve. Tap to jump. One new endless track every single day.` | 68 |

The short description is what shows in search results and above the fold — it carries "tilt",
"endless runner" and "daily", which are the three terms the listing is being optimised for.

---

## 3. Full description (limit 4,000) — v1.0

Play indexes this field, so the search terms appear in natural sentences rather than a keyword block.
Structure: hook → how it plays → daily loop → what is honest about the scope → feature list.

```
Your phone is the controller.

Veyro Run is a motion-controlled endless runner. Tilt left and right to weave across three lanes, tap
to jump, and see how far you get before the track catches you. No virtual joystick, no thumbs
covering the screen — you steer by moving the device itself, and it is immediate enough that you stop
thinking about it after about ten seconds.

A NEW TRACK EVERY DAY

Every player in the world gets the same track on the same day. The Daily Run is generated from the
date itself, so the course you are running is the exact course your friends are running — same
obstacles, same jumps, same order. Beat your own best, then go and tell somebody what you scored.

BUILT TO BE PICKED UP

A run lasts as long as you last. Open it in a queue, run once, put it away. There is no energy
meter, no timer counting down to a thing you have to come back for, and no account to create. Your
best scores live on your phone.

PLAYS OFFLINE

No connection required. No servers, no sign-in, no ads interrupting a run. Everything the game needs
is on the device.

TILT OR TOUCH — YOUR CALL

Motion control is the point, but it is not compulsory. Touch controls are always live alongside the
tilt input, so you can swipe and tap on a bus without looking like you are steering one.

COSMETICS ONLY

If you want to support development you can unlock cosmetic looks for your runner. They change how
you look and nothing else — no speed boosts, no extra lives, no advantage of any kind over a player
who never spends a penny. The game you download is the whole game.

FEATURES

• Motion controls — tilt to swerve, tap to jump
• Daily Run — one shared, identical track per day, generated from the date
• Instant restart — crash, tap, you are running again
• Fully offline — no account, no connection, no interruptions
• Cosmetic-only purchases — nothing that affects gameplay
• Built for one hand and for short sessions

Veyro Run is made by one developer, in public. It is early, it will get better, and if something
feels wrong the fastest way to change it is to tell us.
```

Length check: run `docs/store-kit/check-lengths.sh` — comfortably inside 4,000 (measured 2,077 characters),
which leaves room for the camera-mode block in §4 without a rewrite.

**Deliberately absent, and why:**

- *Camera / hands-free / body tracking* — not in the v1.0 binary. See §4.
- *Free Run.* `RunMode.Free` exists in code but **the player cannot reach it** —
  `RunSession.Mode` is hard-coded to `Daily` (`RunSession.cs:33`) and nothing sets it. The HUD would
  render "FREE RUN" if it were set, which is what makes this easy to mistake for a shipped feature.
  Either expose a toggle (a few lines, worth folding into the T-020 session) and then add the
  paragraph back, or leave it out. **Currently left out.**
- *Sliding.* Not in the runner. `IGameInput.IsSlidePressed()` is plumbed all the way through
  `CompositeInput`, `TouchTapInput`, `KeyboardInput` and `FaceSteering`, but
  `RunnerController.Step()` only reads `GetMoveAxis()` and `IsJumpPressed()` — the input arrives and
  is dropped. Backlog T-002 called it a "slide stub" and Update 2 owns the real thing. Not mentioned
  anywhere in this listing, including §4.
- *Leaderboards, challenges, friends* — T-024 and the Supabase leaderboard are Update 1. "Go and
  tell somebody what you scored" is true today; "compete on the leaderboard" would not be.
- *Anything automotive.* No "race", "speed", "drive", "circuit", "grand prix", "turbo". VEYRON is a
  Bugatti/VW mark one letter away from Veyro (OPEN_QUESTIONS "Answered", 21 Aug); the argument that
  gets traction is confusion in an automotive context, so the listing never puts the name next to a
  car word. Note "run", "runner" and "track" are used throughout — "track" here reads as *course*,
  and it is the genre-standard term, but if the owner wants maximum distance from the mark, swap
  "track" for "course" globally. Recommended default: leave it.

---

## 4. Camera-mode delta — paste only when the camera update is LIVE

Do not paste any of this before the update carrying `CameraFaceInput` is rolled out to production.

**Short description swap (77 characters):**

```
Play hands-free with the camera, or tilt to steer. A new endless track daily.
```

**Insert into the full description, immediately after the opening section:**

```
HANDS-FREE CAMERA MODE (BETA)

Prop your phone up, step back, and play with your body. Camera mode uses the front camera to follow
you — lean left and right to swerve, hop to jump. It runs entirely on your phone: no video ever
leaves the device, nothing is uploaded, and nothing is stored.

It is labelled BETA on purpose. You choose it from a menu at launch, it needs a reasonably lit room
and a couple of metres of space, and on slower phones the game will tell you it is not fast enough
and hand you back the tilt controls. Tilt and touch are always there.
```

**And add to the FEATURES list:**

```
• Hands-free camera mode (BETA) — play by moving your body, all on-device
```

The BETA label, the "you choose it from a menu" sentence and the speed-gate sentence are load-bearing
— they are the difference between a feature and a promise. The app already behaves exactly this way:
`FaceDetector.TryGate` measures the device before asking for camera permission and falls back to the
menu on any failure (STATUS, 23 Aug).

**Data safety form, when this ships:** the app will request `android.permission.CAMERA`. The Play
Data safety declaration must say camera data is accessed and **not collected, not shared, processed
on-device only** — which is true (BlazeFace runs locally, no network in the CV path, no frames
retained). Getting this form wrong is a policy rejection, and it is filled in by a human.

---

## 5. Other Console fields

| Field | Value |
|---|---|
| Default language | English (United States) |
| App category | Games → Arcade (Action is the alternate; Arcade fits an endless runner better) |
| Tags | Choose from Play's fixed list: *Arcade*, *Casual*, *Endless runner* if offered |
| Contact email | Owner's — **not** the OpenZeppelin work address |
| Website / privacy policy | **BLOCKED — required before any release.** Even with no data collected, Play requires a hosted privacy-policy URL. A GitHub Pages page under the existing `ferrabled/Veyro` repo is the cheapest option. Owner action; see OPEN_QUESTIONS 12. |
| Content rating | Complete the IARC questionnaire in-console: no violence, no user interaction, no data sharing, digital purchases **yes** |
| Ads | **No** (v1.0 contains no ads — see the T-023 verdict in STATUS) |
| In-app purchases | **Yes** — a price range shows on the listing once products exist |
| Target audience | 13+ recommended. Under-13 pulls in Families policy, Designed-for-Families review and extra ad/data rules for no benefit here |

---

## Sources

- **[L1]** Play Console Help — store listing character limits (30 / 80 / 4,000) — https://support.google.com/googleplay/android-developer/answer/9859152
- **[L2]** Play Console Help — graphic asset specs — https://support.google.com/googleplay/android-developer/answer/9866151
