# T-040 — #BuildInPublic drafts, camera-mode milestone

Drafted 23 Aug 2026. **Nothing here has been posted.** Owner reviews, edits and publishes; agents
publish nothing, ever.

Every number below is from `docs/STATUS.md` and was measured on the OnePlus Nord 2 (DN2103). If a
number cannot be pointed at a STATUS entry, it does not go in a post. The #BuildInPublic category is
judged on the journey and the honesty of it [S1] — an inflated benchmark is both a bad post and a bad
submission.

**Tags on every post:** `#Shipaton #BuildInPublic` (plus `#Unity #gamedev` where the platform suits).
**Cadence target:** ≥ 3/week (T-040). These three cover roughly one week.

---

## Post 1 — the pivot *(the strongest one; lead with it)*

**Angle:** the negative result. This is the most interesting true thing that has happened on the
project and it is the post most likely to travel.

**Media:** side-by-side screen recording — BlazePose landmark overlay stuttering at ~3 Hz, next to
camera mode driving the actual game at frame rate.

### X / Twitter (thread)

```
1/ I spent three days getting Google's BlazePose running on a phone for my hands-free game,
got all 33 body landmarks tracking live, and then measured it.

118 ms per landmarker inference. My budget was 30.

Four times too slow. #Shipaton #BuildInPublic

2/ Full numbers on a OnePlus Nord 2, Unity Inference Engine, lite model:

  detector      217 ms
  landmarker    118 ms
  full cycle    ~334 ms  →  about 3 Hz

At 3 Hz you don't have a controller. You have a suggestion.

3/ Surprise finding on the way: Unity's CPU backend was 2.5× FASTER than GPUCompute for
this model on this phone. I'd assumed GPU and would have shipped that assumption if I
hadn't measured both.

Still missed the gate.

4/ So I stopped trying to make the model faster and made the problem smaller.

Steering doesn't need a skeleton. It needs to know where you are.

BlazeFace — a face detector, not a pose model — runs at a 3.7 ms blocking median on the
same phone. 30× faster than what it replaced.

5/ Face centre → left/right.
Upward face velocity → jump.

That's the whole controller. It's a much worse model of a human and a much better input
device.

6/ It's now in the game and I played it hands-free yesterday. Phone propped on a table,
me two metres back, leaning to steer.

Runs entirely on the phone. 418 KB model shipped in the APK. No servers, no uploads,
nothing leaves the device.

7/ Lesson I'd tattoo on myself: measure the obvious approach before you commit a week to it,
and be willing to solve a smaller problem.

The 33-landmark version was more impressive. The 4-number version is the one people can play.
```

### LinkedIn (single post)

```
Three days building the obvious solution, one measurement, and a complete rethink.

I'm building a motion-controlled endless runner for RevenueCat Shipaton 2026 — you play it by
moving, not by touching the screen. The plan was Google's BlazePose: 33 body landmarks, on-device,
running in Unity's Inference Engine.

It worked. It was also four times too slow.

On a mid-range Android phone: 217 ms for the detector, 118 ms for the landmarker, ~334 ms for a
full cycle — about 3 Hz, against a 30 ms frame budget. A useful benchmark along the way: the CPU
backend beat GPUCompute by 2.5× for this model on this device, which is the opposite of what I'd
have guessed and would have shipped on assumption.

There was no tuning left. So instead of making the model faster, I made the problem smaller.

Steering a runner doesn't require a skeleton — it requires knowing roughly where the player is.
BlazeFace, a plain face detector, runs at a 3.7 ms blocking median on the same phone. Thirty times
faster. Face centre drives left and right; upward face velocity is a jump.

It's a far worse model of a human body and a far better controller. It's in the game now, it runs
entirely on the device, and nothing leaves the phone.

The takeaway I keep relearning: measure the obvious approach before you spend a week on it, and be
willing to solve a smaller problem than the one you started with.

#Shipaton #BuildInPublic #Unity #gamedev
```

---

## Post 2 — hands-free, playing it

**Angle:** the demo. Short, visual, low text.

**Media:** the money shot — person 2 m from a propped-up phone, leaning left and right, runner
following. **One continuous take, no cuts** (a cut in the middle reads as fake, and this is the same
shot the submission video opens on — see `docs/submission/VIDEO_SCRIPT.md`).

### X / Twitter

```
No controller. No wearable. Just the front camera and a phone propped on a table.

Lean to steer, hop to jump.

3.7 ms per frame to find me, everything on-device, nothing uploaded.

#Shipaton #BuildInPublic

[VIDEO — one take, ~15s]
```

### LinkedIn

```
This is what I've been building towards: playing a game by moving.

Phone propped on a table, me two metres back. Lean left, the runner goes left. Hop, it jumps.
No controller, no wearable, no calibration screen.

The tracking costs 3.7 ms per frame and runs entirely on the phone — no server, no upload, no video
leaving the device. The whole model is 418 KB and ships inside the app.

It's shipping labelled BETA behind a menu choice, on purpose. It needs a reasonably lit room and a
couple of metres of space, and on slower phones the game measures itself, decides it can't keep up,
and hands you back the tilt controls before it even asks for camera permission. A feature that
sometimes can't work should say so rather than fail in the user's hands.

#Shipaton #BuildInPublic #Unity
```

---

## Post 3 — the engineering discipline behind it

**Angle:** for the developers. Explains *why* the pivot in Post 1 was cheap — which is the actually
transferable idea, and the one that makes the project look competently built rather than lucky.

**Media:** a code screenshot of `IGameInput` next to the three adapter class declarations.

### X / Twitter (thread)

```
1/ Why throwing away my entire computer-vision approach cost me about a day instead of a week.

Every input in the game goes through one interface: #Shipaton #BuildInPublic

  float GetMoveAxis();
  bool  IsJumpPressed();
  bool  IsSlidePressed();
  bool  IsSpecialPressed();
  void  Tick();

2/ Three things implement it: gyro, touch, camera. Gameplay code has never seen a sensor,
a camera, or a model — it asks for an axis and gets a float.

So swapping BlazePose for BlazeFace changed one adapter. The game didn't know it happened.

3/ The other half: the input logic is engine-free. No UnityEngine, no Time.deltaTime,
no camera. The caller passes in the timestep.

Which means it's testable headlessly — 13 tests drive synthetic 30 Hz trajectories through
the gesture detector. 140 EditMode tests in the project, no device required.

4/ Those tests caught a design bug I'd never have found by playing:

standing up from a crouch has the exact velocity signature of a jump.

Every slide ended with an accidental jump. Ending a slide now arms the jump refractory.

5/ Same discipline elsewhere: track generation is deterministic from (seed, version, worldId),
and the seed hash + PRNG stream are pinned by tests. A content change that would silently
alter an already-played Daily Run fails CI instead of shipping.

6/ None of this is clever. It's just the boring rule — put a seam between the thing that
might not work and the thing that has to ship — applied before I knew which one was which.

The camera track was never allowed to block the release. That's the only reason I could
afford to be wrong about it.
```

### LinkedIn

```
Throwing away my entire computer-vision approach cost about a day. Here's the boring reason.

Every input in my game goes through one small interface — get axis, jump, slide, special, plus a
per-frame tick.
Three adapters implement it: gyro, touch, camera. Gameplay code has never touched a sensor, a
camera or a model; it asks for a number and gets a number.

So when the pose-estimation model turned out to be four times too slow and had to be replaced with
something completely different, exactly one file changed. The game never found out.

The second half is that the input logic is engine-free — no engine types, no clock, the caller
passes in the timestep. That makes it testable without a device: 13 tests drive synthetic 30 Hz
motion through the gesture detector, inside 140 tests that run headlessly in CI.

Those tests found something playtesting wouldn't have. Standing up from a crouch has the identical
velocity signature to a jump — so every slide was ending in an accidental jump. Caught in a unit
test, fixed before it ever reached a phone.

None of it is clever. It's the ordinary rule: put a seam between the thing that might not work and
the thing that has to ship — and do it before you know which is which. The camera track was never
allowed to block the release, which is the only reason I could afford for it to fail.

#Shipaton #BuildInPublic #Unity #gamedev
```

---

## Numbers used, and where they come from

| Claim | Source |
|---|---|
| BlazePose lite: detector 217 ms, landmarker 118 ms, cycle ~334 ms ≈ 3 Hz | STATUS 22 Aug (T-010) |
| 30 ms gate; missed by 4× | STATUS 22 Aug (T-010) |
| CPU backend 2.5× faster than GPUCompute | STATUS 22 Aug (T-010) |
| BlazeFace **3.7 ms** blocking median, gate PASS on device | STATUS 23 Aug (T-013). *The 22 Aug feasibility sweep measured 4.2 ms; 3.7 ms is the in-game gate reading. Use 3.7 and don't mix them in one post.* |
| 418 KB ONNX committed and shipped in the APK | STATUS 23 Aug |
| 140 EditMode tests (127 + 13 new) | STATUS 23 Aug |
| Stand-up-from-crouch === jump signature bug | STATUS 23 Aug |
| Deterministic Daily Run; two cold launches byte-identical | STATUS 21 Aug (T-008) |

**Do not post yet, because they are not true yet:** any RevenueCat/monetization claim (T-020
unstarted), any install or player numbers (not released), any leaderboard or challenge feature
(Update 1), sliding (`RunnerController` ignores `IsSlidePressed`), and Free Run as a player-facing
mode (`RunSession.Mode` is hard-coded to Daily).

**Also missing before any of this can go out: P9** — the social account itself does not exist yet.
That is the actual blocker on T-040, and it takes minutes.

---

## Sources

- **[S1]** Shipaton 2026 rules — #BuildInPublic category — https://revenuecat-shipaton-2026.devpost.com/rules
