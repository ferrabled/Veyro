# T-035 — Devpost submission answers (draft)

Written 23 Aug 2026. **Draft for owner review — nothing is submitted by an agent.** Numbers marked
`[N]` are placeholders that must be real at submission time; shipping a placeholder is worse than
omitting the claim.

Judging note that shapes everything below: submissions are filtered on store publication, RevenueCat
SDK verification against the bundle ID, required fields, video and marketing assets; at least two
screeners read the description and watch **up to two minutes**; **claiming categories that do not
genuinely apply is penalised** [S3].

---

## 1. Categories to enter — and the ones to leave alone

D7 set the target list on 20 Aug. Re-checked against today's state:

| Category | Enter? | Why |
|---|---|---|
| **Best Game** | ✅ | It is a game. Core target. |
| **Grand Prize** | ✅ | Not a checkbox — it is judged on post-release traction, so it is earned by shipping early and documenting growth. |
| **#BuildInPublic** | ✅ | Genuine: posts from day one, including the failures. |
| **RevenueCat Design** | ✅ | Paywall Builder paywall + the motion-control UX. Honest claim. |
| **HAMM** | ✅ | Cosmetic-only economy with a written rationale. Honest claim. |
| **Keep Them Coming Back (OneSignal)** | ⚠️ **only if T-021 ships** | Requires the SDK **and** at least one deployed campaign **and** the App ID [S2]. T-021 is unstarted. |
| **Growth Loop (Layers)** | ⚠️ **only if T-022 ships** | Requires the SDK installed and verifiable, plus a described experiment with an outcome [S2]. T-022 is unstarted. |
| **Most Viral (Noise)** | ⚠️ **only if there is real content** | Needs a live app promoted via Noise. Entering with two posts is exactly the overreach screeners punish. |
| **Best App for Galaxy (Samsung)** | ⚠️ **only if actually published there** | Requires a live **Galaxy Store URL** [S2]. P3 (seller account) is not started; T-033 is post-v1.0. High upside, low competition — but the URL is not optional. |
| **Catvertising** | ❌ | See `docs/STATUS.md` T-023: no ads in v1.0. The category wants RevenueCat Ads as *a monetization method*; a rewarded revive bolted on in the last week is the textbook box-check. |
| Stripe / Replit / JetBrains / Influencer / Peace / Next Gen | ❌ | Per D7 — wrong stack, wrong product, or ineligible. |

**Rule for the final pass, the night before submission:** walk the ⚠️ rows one at a time and ask
*"can I paste the required artefact right now?"* — the App ID, the Layers experiment outcome, the
Galaxy Store URL. If not, untick it. Four strongly-evidenced categories beat nine thin ones.

---

## 2. Core fields

### Project name
```
Veyro Run
```

### Elevator pitch / tagline (Devpost's short field)
```
An endless runner you play by moving — tilt the phone to steer, or prop it up and use your body.
Everything runs on-device.
```

### Built with
```
Unity 6, C#, Unity Inference Engine, BlazeFace, RevenueCat, Google Play Billing, Android
```

### Description

Devpost's long field. Written to be read in about 90 seconds, because that is what a screener has.

```
## What it is

Veyro Run is a motion-controlled endless runner for Android. You tilt the phone to steer and tap to
jump — no virtual joystick, no thumbs over the screen. Or you prop the phone up, step back, and play
hands-free: the front camera tracks you, and leaning steers the runner.

Everything runs on the device. No servers, no accounts, no connection required.

## Why it's built this way

Motion control on phones is usually a gimmick because it's usually laggy. The whole project was
organised around not being laggy.

All input goes through one interface — IGameInput — with three adapters behind it: gyro, touch, and
camera. Gameplay code never touches a sensor, a camera or a model. That meant the computer-vision
work could run as a completely parallel track that was never allowed to block the release, and it
meant that when the first CV approach failed, nothing else had to change.

And it did fail. The plan was BlazePose — 33 body landmarks, the obvious choice. On the test phone it
came in at a 118 ms median for the landmarker and 217 ms for the detector: about 3 Hz, against a
30 ms budget. Four times too slow, with no tuning left to do.

So the perception problem got smaller instead. Steering doesn't actually need a skeleton — it needs
to know where you are. BlazeFace, a face detector, runs at a 3.7 ms blocking median on the same
phone: 30 times faster than what it replaced, and comfortably inside the frame budget. Face centre
drives left/right, upward face velocity is a jump. It's a worse model of a human and a much better
controller.

The game measures the phone before it offers camera mode, and a device that can't keep up never even
gets asked for camera permission.

## The daily loop

Track generation is deterministic from (seed, content version, world id). The Daily Run seeds from
the UTC date, so every player in the world runs an identical course on the same day — same
obstacles, same order — with no backend involved at all. Two cold launches on two devices produced
byte-identical runs. The generator's seed hash and PRNG stream are pinned by tests, so a content
change that would silently alter an already-played daily fails CI instead.

## Monetization

RevenueCat, cosmetics only. Three one-time non-consumable products — two skins and a Season 1 pass —
each mapped to its own entitlement (`skin_ember`, `skin_frost`, `season1`), with a paywall built in
RevenueCat's Paywall Builder so it can be iterated without an app update. Nothing for sale affects
gameplay: no lives, no boosts, no advantage. [N: conversion / revenue figures — real numbers only,
or omit this sentence.]

## Honest status

Camera mode ships labelled BETA behind an explicit menu choice, because it needs reasonable light
and a couple of metres of space and it should say so. v1.0 shipped gyro and touch, which was always
the plan: the interesting feature was never allowed to hold the release hostage.

Built solo, in public, in [N] weeks.
```

**Why the description leads with the failure:** the CV pivot is the most interesting true thing about
this project, it is the strongest #BuildInPublic and engineering-credibility evidence available, and
"we tried the obvious thing, measured it, and it was 4× too slow" is more persuasive than any
adjective. It is also verifiable — every number in it is in `docs/STATUS.md`.

---

## 3. Per-category answers

### Best Game
> *"Description of the game covering gameplay, art direction, and how the monetization model fits
> the genre."* [S2]

```
Gameplay: a three-lane endless runner steered by tilting the phone, with a shared Daily Run — the
same generated track for every player on a given date. Runs are short, restart is instant, and the
whole game is playable one-handed and offline.

Art direction: [N — fill from T-006. Do not answer this until the art pass exists; "flat low-poly"
is currently a plan, not a screenshot.]

Monetization fit: endless runners live or die on the cosmetic economy, and pay-to-win kills them.
Veyro Run sells looks and nothing else. There are no revives for sale, no coin doublers and no
advantage of any kind — a player who never spends gets the identical game. That's a genre-native
model, and it's also the only one compatible with a shared Daily Run where everybody must be running
the same course under the same rules.
```

### RevenueCat Design
```
Two things carry the design claim. First, the control scheme: the game's whole interface premise is
that the device is the input, so the UI is built to disappear — no virtual pad, no thumb furniture,
a HUD that stays out of the play space. Second, the paywall is built in RevenueCat's Paywall Builder,
which means it can be iterated against real behaviour without shipping an app update — the paywall
that judges see is not the one that shipped on day one. [N: what actually changed between them.]
```

### HAMM
```
Cosmetics only, one-time non-consumable purchases: two skins at €2.99 and a Season 1 pass at €4.99.
Deliberately three entitlements rather than one shared "cosmetics" flag — a buyer who wants one skin
is not forced into a bundle, and because products attach to entitlements dashboard-side, a Supporter
Bundle granting all three can ship later with zero app-code change. Packaging stays a pricing
decision instead of becoming a release.

The decision that shaped everything: no revives, no continues, no currency. An endless runner that
sells second chances is selling relief from its own difficulty curve, and it corrupts a shared Daily
Run — everyone has to be running the same course under the same rules for the daily score to mean
anything.

The paywall is server-configured through RevenueCat's Paywall Builder, so pricing and presentation
were iterated after launch without an app update. [N: conversion numbers — real ones or none.]
```

### #BuildInPublic
```
Links: [N: social profile + the specific posts worth reading].

Posting started before there was a game. The most useful thing that came out of it was negative
results: the week the pose model was measured at 4× over budget and the approach had to be thrown
out was the post that got the most response, and the "measure it before you commit" replies are
directly why the shipped build gates camera mode on a speed test before it asks for permission.
```

### OneSignal — *only if T-021 shipped*
```
App ID: [N]
Campaign: [N — describe the deployed campaign and how it was created: API, MCP, or dashboard.]
```

### Layers — *only if T-022 shipped*
```
SDK: com.layers.analytics, installed in the submitted build.
Audience: [N]  Message: [N]  Channel/surface: [N]  Experiment: [N]
Outcome and what the signals showed: [N]
```

### Samsung — *only if published on Galaxy Store*
```
Galaxy Store URL: [N]
Optimization: [N — foldable/flex-mode behaviour, multi-window, Samsung-device performance notes.]
Note: RevenueCat's Unity SDK does not yet support Galaxy billing, so the Galaxy build ships with
store purchases disabled or via a web purchase link; the RevenueCat purchase lives in the Play build.
```

---

## 4. Judge access to the purchase

Devpost requires a free trial **or** a promo code so judges can unlock the IAP [S1][S2]. Our product
is a one-time cosmetic unlock, so free trials do not apply — promo codes are the path.

Paste into the submission's judge-access field:

```
To test the purchase at no cost:

1. Install Veyro Run from Google Play: [STORE URL]
2. Play one run, then on the result screen tap the cosmetics button to open the paywall.
3. Choose a cosmetic. When Google Play's purchase sheet appears, tap the small down-arrow next to
   the payment method, then tap "Redeem code".
4. Enter one of the codes below and confirm. The unlock applies immediately and the runner's look
   changes in the next run.

   [CODE 1]
   [CODE 2]
   [CODE 3]

Alternatively the codes can be redeemed in the Play Store app (menu → Redeem code) before opening
the game; the entitlement is picked up when the app next resumes.

Restore is available from the same screen if you reinstall.
```

Step 3 is the one people miss — the redeem field belongs to Google's billing sheet, not to the app,
and the down-arrow is not obvious. Full mechanics and the fallback if a code fails:
`docs/REVENUECAT_PLAN.md` §6.

---

## 5. Sources

- **[S1]** Shipaton 2026 overview — https://revenuecat-shipaton-2026.devpost.com/
- **[S2]** Shipaton 2026 rules — per-category requirements, submission requirements — https://revenuecat-shipaton-2026.devpost.com/rules
- **[S3]** How we judge Shipaton — filtering, ≥2 screeners, category-overreach penalty — https://www.shipaton.com/blog/how-we-judge-shipaton
