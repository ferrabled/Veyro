# T-035 — demo video script (≤ 2 minutes)

Written 23 Aug 2026. **Script only — nothing shot, nothing recorded.**

> **30 Sep 2026:** the shoot-day structure now lives in `SHOOT_GUIDE.md` — the 26.7 s motion reel
> (`reel/`) opens the video as a title sequence and the live footage starts at 0:27. The rules in
> this file (no fake hook, no architecture diagrams, no unlicensed music) still apply; the
> timings below are superseded.

## The constraint

Devpost requires a video **under two minutes**, publicly visible on YouTube or Vimeo, showing the app
functioning on a device, with no unauthorised trademarks or copyrighted material [S1][S2]. RevenueCat's
own judging post is blunter: **judges are not required to watch past two minutes**, and every
submission gets at least two screeners before scoring [S3].

So: **1:50 target, hard cap 2:00.** No title card longer than 2 seconds, no architecture diagrams
(handoff §14.6 says this explicitly), no logo animation, no "hi everyone, so today I want to show
you". The first frame is gameplay.

**Music:** either silence + game audio, or a track with an explicit commercial-use licence the owner
can point to. "No third-party trademarks or copyrighted music" is a filtering criterion, not a
guideline — a copyright claim on the YouTube upload can make the video private mid-judging, which is
the same as not submitting one.

---

## Structure

| Time | Section | Purpose |
|---|---|---|
| 0:00–0:18 | The hook — hands-free | Show the thing nobody else has, before anything else |
| 0:18–0:32 | The premise, stated | One sentence of what this is |
| 0:32–1:05 | The game | Gameplay, Daily Run, the loop |
| 1:05–1:30 | Monetization | RevenueCat paywall, cosmetics, honest positioning |
| 1:30–1:50 | Evidence | Notifications, growth loop, built-in-public, the categories claimed |

---

## 0:00–0:18 — The hook

**Shot:** wide, static, one continuous take. A person standing in a room, phone propped on a table
2 m away, screen facing them. They lean left — the runner swerves left. They lean right — swerves
right. They hop — the runner jumps. Cut in a tight insert of the phone screen at ~0:10 so the
reaction is unmistakable, then back to wide.

**No cuts inside the lean-swerve-lean beat.** The whole claim is that the phone is watching a real
body in real time; a cut there reads as a fake, and every viewer who suspects a fake stops watching.

**On-screen text (0:03, 2 s):** `no controller · no wearable · front camera only`

**VO:** *"That's the front camera. Nothing is uploaded — the model runs on the phone."*

**Why this opens the video:** handoff §14.6 puts the hook first, and this is the strongest 18 seconds
the project can produce. It is also the only part a screener is guaranteed to see.

> **Contingency — largely retired 26 Aug.** Camera mode ships **in v1.0**, in the closed-testing
> build and the production release (OPEN_QUESTIONS 12), so the hook is footage of a shipped feature
> and needs no hedge. The rule behind the contingency still stands, though, and it is the one that
> matters: **do not fake it, and do not shoot the hook before the build is live in production.** If
> production review somehow slips past the shoot date, open instead on a tight shot of hands
> visibly tilting the phone with the game reacting (the §14.2 "proof clip" shot) and move the
> camera footage to 1:30–1:50 with the mode-select menu visible. Showing a beta as a beta is fine.
> Showing an unreleased feature as the headline is what gets a submission disqualified.

## 0:18–0:32 — The premise

**Shot:** phone in hand, portrait, tilting. Runner weaving. Clean gameplay, no UI overlays.

**VO:** *"Veyro Run is an endless runner you play by moving. Tilt the phone to steer, tap to jump —
or prop it up and use your body. It's on Google Play, it works offline, and there's no server
anywhere in it."*

**On-screen text:** `Veyro Run · Google Play`

## 0:32–1:05 — The game

Four beats, roughly 8 seconds each. Cut on action, not on pauses.

1. **A real run.** Tilting, swerving, jumping, coins, combo counter climbing. Let one run breathe —
   this is the Best Game category's evidence and it should look like a game somebody enjoys playing.
2. **The crash and instant restart.** Crash → result screen → tap → running again. The loop, in 4
   seconds. **VO:** *"Crash, tap, you're running again."*
3. **Daily Run.** Cut to the HUD's `DAILY · 2026-09-XX` line. **VO:** *"Every player in the world
   gets the same track on the same day — generated from the date itself, so two phones with no
   connection between them run an identical course."* Optional and strong if the shoot allows:
   **two phones side by side, same date, visibly identical track.** That single shot proves
   determinism better than any words, and it is already verified behaviour (STATUS, 21 Aug).
4. **Mode select.** The menu: TILT & TOUCH / CAMERA (BETA). **VO:** *"Camera mode is opt-in, and the
   game measures your phone before it offers it — too slow, and it hands you back the tilt
   controls."*

## 1:05–1:30 — Monetization

**Shot:** result screen → tap the cosmetics entry → the **RevenueCat Paywall-Builder paywall** opens
→ purchase → the runner's new look, in game.

**VO:** *"Purchases run through RevenueCat. The paywall is built in RevenueCat's Paywall Builder, so
I can redesign it without shipping an app update. Everything for sale is cosmetic — no lives, no
boosts, no advantage. The whole game is in the free download."*

**On-screen text:** `RevenueCat · Paywall Builder · cosmetics only`

**Requirements this section satisfies:** the RevenueCat SDK powering a real purchase is the
eligibility gate [S2]; HAMM is judged on the monetization strategy; the paywall itself is Design
evidence. Show the actual paywall, not a mock — screeners verify the SDK against the bundle ID [S3].

## 1:30–1:50 — Evidence and close

Fast montage, ~4 seconds each, VO continuous over the top:

- OneSignal notification arriving on the lock screen ("Today's run is live") → tapping it opens the
  Daily Run.
- A share/challenge card going out (Layers-instrumented).
- A quick scroll of the #BuildInPublic post history.

**VO:** *"Daily Run notifications go out through OneSignal. The share loop is instrumented with
Layers. And the whole build has been posted in public since day one — including the week the pose
model missed its latency target by four times and the approach had to change."*

**Final card (3 s, static):**
```
Veyro Run
[Play Store URL]
Best Game · Design · HAMM · OneSignal · Layers · #BuildInPublic
```

**Only list categories actually entered.** Screeners penalise category overreach [S3], and a card
claiming nine categories does measurable harm. See `docs/submission/DEVPOST_ANSWERS.md` §1 for the
list that survives that filter.

---

## Production notes

- **Capture during the T-030 screenshot session** (`docs/store-kit/SCREENSHOTS.md`) — same device,
  same lighting, one setup. Record continuously and cut later.
- **Screen capture:** `adb shell screenrecord` maxes at 3 minutes per file and drops the audio; for a
  1:50 cut that is fine, but record the phone screen separately from the room camera and sync in the
  edit.
- **The room shots need a second camera** — another phone on a tripod, or a friend. Shot 7 in the
  screenshot list is the same setup.
- Shoot the hook **last**, once everything else is in the can and the game is at its most stable.
  It is the shot most likely to need ten takes.
- Export 1080p60 if the source allows. Endless-runner footage at 30 fps looks worse than the game is.

## Owner decisions (→ OPEN_QUESTIONS 11)

1. **Voiceover or text-only?** Recommended default: **voiceover.** The #BuildInPublic and Grand Prize
   categories reward a person with a story; a silent captioned video reads as an ad. If the owner
   would rather not be recorded, on-screen captions with game audio is an acceptable fallback — but
   then the final card should carry a link to the build-in-public thread so the human is still
   present somewhere.
2. **On camera or not?** The hook needs *a* body. It does not have to be the owner's face — the shot
   works from behind, or in silhouette, or with a friend playing.
3. **Music.** Owner picks and confirms the licence. Silence plus game audio is a legitimate choice
   and carries zero risk.

---

## Sources

- **[S1]** Shipaton 2026 overview — https://revenuecat-shipaton-2026.devpost.com/
- **[S2]** Shipaton 2026 rules — video under 2 minutes, public on YouTube/Vimeo, app shown functioning, no unauthorised trademarks/copyrighted material — https://revenuecat-shipaton-2026.devpost.com/rules
- **[S3]** How we judge Shipaton — 2-minute cap, ≥2 screeners, SDK verified by bundle ID, category-overreach penalty — https://www.shipaton.com/blog/how-we-judge-shipaton
