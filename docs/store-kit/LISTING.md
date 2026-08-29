# T-030 — Google Play store listing copy

Drafted 23 Aug 2026 (no-unity-prep session). **Copy only — no binary assets.** Character limits
verified against Play Console Help on 23 Aug 2026: app name **30**, short description **80**, full
description **4,000**; limits apply to full-width and half-width characters alike [L1].

Owner must approve before anything is pasted into the Console. Nothing here is submitted by an agent.

**Positioning rule, revised 26 Aug:** ~~the store build is gyro + touch (D2) and the v1.0 listing
does not mention the camera at all~~ — **superseded by the 24 Aug owner call (OPEN_QUESTIONS 12):
camera mode ships in v1.0**, in the closed-testing build and the production release, under the real
package name. The camera copy is therefore **folded into §2 and §3 below**, not held back; §4 is
kept only as a record of what moved.

Two rules survive the change, and both still bind:

1. **BETA labelling is not optional.** Camera mode is a menu choice that needs light and space and
   gates itself out on slow devices — the copy says so in the same breath as it advertises it.
   Promising unqualified hands-free play is still the fastest way to earn 1-star reviews during the
   exact 14 days the closed test is being judged.
2. **The listing must explain the CAMERA permission.** This is now a hard requirement, not a
   nice-to-have: CAMERA is a sensitive permission, it is in the build from day one, and a reviewer
   who finds it unexplained in the description is a rejection risk
   (`PLAY_CONSOLE_SETUP.md` §A, Data safety).

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

*83 characters — 3 over, and now also incomplete: it does not mention the camera. Kept for the
record.*

**Ship this one** (camera included, 26 Aug):

```
Play hands-free with the camera, or tilt to steer. A new endless track daily.
```
77 characters. Verified by `check-lengths.sh`, which also measures both alternates below so a swap
never needs a re-count.

Alternates:

| Option | Chars | Note |
|---|---|---|
| `Tilt your phone to swerve and jump. A daily endless runner you play by moving.` | 78 | The old gyro-only line. No camera — do not ship. |
| `Move your body or tilt your phone. A new endless track every single day.` | 72 | Leads on motion rather than the camera specifically. |
| `Hands-free camera mode or tilt controls. One new endless track every day.` | 73 | Most explicit; weakest on "endless runner" as a search term. |

The short description is what shows in search results and above the fold — the shipping line carries
"hands-free", "camera", "tilt" and "endless track", which are the terms the listing is being
optimised for. It leads with the camera because that is the differentiator; tilt is the fallback and
reads as one.

---

## 3. Full description (limit 4,000) — v1.0 + purchases

> ⚠️ **PASTE THIS WITH THE T-020 RELEASE, NOT BEFORE (prepared 29 Aug 2026).** The block below is
> the description for the submission that ships the purchases build (versionCode 5) — it names the
> cosmetics and it no longer claims the game never connects. Pasting it earlier would advertise a
> feature the live build does not have and contradict the Console answers currently on file.
> Pasting the *old* text after that build ships is the mirror-image error: it would claim
> "no connection required" about a binary that talks to Google Play billing and RevenueCat.
> The listing edit, the Console flips and `wrangler deploy` from `site/` all happen in one sitting
> (`STORE_COMPLIANCE.md` T-020).
>
> **Delta vs. the text currently live on the listing** — three spots, nothing else:
> 1. `PLAYS OFFLINE` — "No connection required." replaced; the paragraph now says every run works
>    offline and names the one exception.
> 2. `COSMETICS ONLY` paragraph added directly after `PLAYS OFFLINE` (this is the previously
>    held-back paragraph, released — with "no speed boosts" reworded to "no boosts", since
>    *speed* is on the banned-words list in §3's "Deliberately absent" note).
> 3. FEATURES — "Fully offline — no account, no connection" → "Plays fully offline — no account,
>    no sign-in", plus one new "Cosmetics only" bullet.

Play indexes this field, so the search terms appear in natural sentences rather than a keyword block.
Structure: hook → how it plays → daily loop → what is honest about the scope → feature list.

```
Your phone is the controller.

Veyro Run is a motion-controlled endless runner. Tilt left and right to weave across three lanes, tap
to jump, and see how far you get before the track catches you. No virtual joystick, no thumbs
covering the screen — you steer by moving the device itself, and it is immediate enough that you stop
thinking about it after about ten seconds.

HANDS-FREE CAMERA MODE (BETA)

Prop your phone up, step back, and play with your body. Camera mode uses the front camera to follow
you — lean left and right to swerve, hop to jump. It runs entirely on your phone: no video ever
leaves the device, nothing is uploaded, and nothing is stored.

It is labelled BETA on purpose. You choose it from a menu at launch, it needs a reasonably lit room
and a couple of metres of space, and on slower phones the game will tell you it is not fast enough
and hand you back the tilt controls. Tilt and touch are always there.

A NEW TRACK EVERY DAY

Every player in the world gets the same track on the same day. The Daily Run is generated from the
date itself, so the course you are running is the exact course your friends are running — same
obstacles, same jumps, same order. Beat your own best, then go and tell somebody what you scored.

BUILT TO BE PICKED UP

A run lasts as long as you last. Open it in a queue, run once, put it away. There is no energy
meter, no timer counting down to a thing you have to come back for, and no account to create. Your
best scores live on your phone.

PLAYS OFFLINE

Every run works with no connection at all. No servers, no sign-in, no ads interrupting a run —
everything the game needs to play is already on the device. The only thing that ever goes online is
an optional cosmetic purchase, and only if you choose to make one.

COSMETICS ONLY

If you want to support development you can unlock cosmetic looks for your runner. They change how
you look and nothing else — no boosts, no extra lives, no advantage of any kind over a player who
never spends a penny. The game you download is the whole game.

CAMERA, TILT OR TOUCH — YOUR CALL

Motion control is the point, but it is not compulsory. Pick your input from a menu when the game
opens: the camera if you have the space, tilt if you do not, and touch controls are always live
alongside the tilt input — so you can swipe and tap on a bus without looking like you are steering
one.

FEATURES

• Motion controls — tilt to swerve, tap to jump
• Hands-free camera mode (BETA) — play by moving your body, all on-device
• Daily Run — one shared, identical track per day, generated from the date
• Instant restart — crash, tap, you are running again
• Plays fully offline — no account, no sign-in, no interruptions
• Cosmetics only — optional skins that change nothing but how you look
• Built for one hand and for short sessions

Veyro Run is made by one developer, in public. It is early, it will get better, and if something
feels wrong the fastest way to change it is to tell us.
```


Length check: run `docs/store-kit/check-lengths.sh` — comfortably inside 4,000, with the camera and
cosmetics blocks now included.

**Deliberately absent, and why:**

- *Free Run.* `RunMode.Free` exists in code but **the player cannot reach it** —
  `RunSession.Mode` is hard-coded to `Daily` (`RunSession.cs:33`) and nothing sets it. The HUD would
  render "FREE RUN" if it were set, which is what makes this easy to mistake for a shipped feature.
  Either expose a toggle (a few lines, worth folding into the T-020 session) and then add the
  paragraph back, or leave it out. **Currently left out.**
- *Sliding.* Not in the runner. `IGameInput.IsSlidePressed()` is plumbed all the way through
  `CompositeInput`, `TouchTapInput`, `KeyboardInput` and `FaceSteering`, but
  `RunnerController.Step()` only reads `GetMoveAxis()` and `IsJumpPressed()` — the input arrives and
  is dropped. Backlog T-002 called it a "slide stub" and Update 2 owns the real thing. Not mentioned
  anywhere in this listing, including the camera block.
- *Leaderboards, challenges, friends* — T-024 and the Supabase leaderboard are Update 1. "Go and
  tell somebody what you scored" is true today; "compete on the leaderboard" would not be.
- *Anything automotive.* No "race", "speed", "drive", "circuit", "grand prix", "turbo". VEYRON is a
  Bugatti/VW mark one letter away from Veyro (OPEN_QUESTIONS "Answered", 21 Aug); the argument that
  gets traction is confusion in an automotive context, so the listing never puts the name next to a
  car word. Note "run", "runner" and "track" are used throughout — "track" here reads as *course*,
  and it is the genre-standard term, but if the owner wants maximum distance from the mark, swap
  "track" for "course" globally. Recommended default: leave it.

---

## 4. Camera-mode copy — MERGED INTO §2 AND §3 (26 Aug)

**Nothing here is pending.** This section used to hold a "paste only when the camera update is live"
delta, written when v1.0 was planned as gyro-only (D2). The 24 Aug owner call put camera mode in the
v1.0 build (OPEN_QUESTIONS 12), so the copy moved: the short description in §2, the
`HANDS-FREE CAMERA MODE (BETA)` block and the FEATURES bullet in §3. There is no separate camera
listing update to schedule.

What is kept here, because it is the reasoning the copy has to preserve:

**The three load-bearing sentences.** The BETA label, "you choose it from a menu at launch", and the
speed-gate sentence are the difference between a feature and a promise. The app already behaves
exactly this way: `FaceDetector.TryGate` measures the device *before* asking for camera permission
and falls back to the menu on any failure (STATUS, 23 Aug). If anyone trims §3 for length, these are
the last lines to cut, not the first.

**Data safety form — now required from build one, not later.** The app requests
`android.permission.CAMERA` in the closed-testing build. The Play Data safety declaration must say
camera data is accessed and **not collected, not shared, processed on-device only** — which is true
(BlazeFace runs locally, no network in the CV path, no frames retained). Getting this form wrong is a
policy rejection, and it is filled in by a human: `PLAY_CONSOLE_SETUP.md` §A, and it gates publishing
the closed track.

---

## 5. Other Console fields

| Field | Value |
|---|---|
| Default language | English (United States) |
| App category | Games → Arcade (Action is the alternate; Arcade fits an endless runner better) |
| Tags | Choose from Play's fixed list: *Arcade*, *Casual*, *Endless runner* if offered |
| Contact email | **ferrabled+veyro@gmail.com** (answered 25 Aug, OPEN_QUESTIONS 9). Explicitly a temporary alias the owner will replace — grep `site/` + `docs/PRIVACY_POLICY.md` when that happens. **Not** the OpenZeppelin work address. |
| Website / privacy policy | ✅ **Live** at `https://veyro.ferrabled.com/privacy/` (Cloudflare Workers, deployed + verified 25 Aug). The GitHub Pages plan was dropped. ⚠️ **versionCode 1 still has the dead GitHub Pages URL compiled in** — the closed track must serve versionCode 2, and `GameLinks.PrivacyPolicyUrl` must match the field above (OPEN_QUESTIONS 9; "URL coupling" in `STORE_COMPLIANCE.md`). **Purchases section written 29 Aug, effective date 29 August 2026, not yet deployed** — `npx wrangler deploy` from `site/` belongs to the T-020 submission. |
| Content rating | Filed 25 Aug: all-No → **PEGI 3**. Digital purchases answered **no**, which is correct while the build has none — it flips to **yes** in the T-020 release (`STORE_COMPLIANCE.md` flip table). |
| Ads | **No** (v1.0 contains no ads — see the T-023 verdict in STATUS) |
| In-app purchases | **Flips to Yes in the T-020 submission** — the flag stays off until the versionCode-5 build with `purchases-unity` in it is the one being uploaded, then goes on with the price range (€2.99–€4.99: `veyro.skin.ember`, `veyro.skin.frost` €2.99; `veyro.season1.pass` €4.99). Same submission as the §3 description with COSMETICS ONLY in it — the two must not be published apart. |
| Target audience | 13+ recommended. Under-13 pulls in Families policy, Designed-for-Families review and extra ad/data rules for no benefit here |
| Support URL | `https://veyro.ferrabled.com/support/` (live). **Rewritten 29 Aug, not yet deployed:** the purchases FAQ is now present tense, a "restore purchases" answer was added, and the camera/offline answers no longer claim the game makes no network requests. Deploy with the T-020 submission. |
| Terms | `https://veyro.ferrabled.com/terms/` (live). **Rewritten 29 Aug, not yet deployed:** §3 moved from "may offer" to "offers" purchases, §2 names the one online exception, effective date bumped to 29 August 2026. Deploy with the T-020 submission. |

**Two claims from the old `listing.md` draft that must NOT be carried forward** (it was deleted in the
26 Aug merge; its asset/brand half moved to `ART_DIRECTION.md` §0a):

1. *"lean, hop & crouch"* / *"crouch to slide"* — **sliding is not in the game.**
   `IGameInput.IsSlidePressed()` is plumbed through `CompositeInput`, `TouchTapInput`,
   `KeyboardInput` and `FaceSteering`, but `RunnerController.Step()` reads only `GetMoveAxis()` and
   `IsJumpPressed()`, so the input is dropped. Advertising it is a false feature claim; see the
   *Deliberately absent* list in §3.
2. *"No data collection — the game makes zero network requests"* — **now false: T-020's SDK is in
   the build** and purchase history is collected. The rule this taught stands permanently: never
   put an absolute no-network claim in copy that outlives a build. Any wording about connectivity
   is now the qualified form — *the game plays fully offline; the network is used only for optional
   purchases (Google Play billing + RevenueCat); no analytics, no ads, no accounts, and camera
   frames are never transmitted.* The full sweep of that wording across `site/` and this file was
   done 29 Aug 2026; the Data safety form flips in the same submission.

---

## Sources

- **[L1]** Play Console Help — store listing character limits (30 / 80 / 4,000) — https://support.google.com/googleplay/android-developer/answer/9859152
- **[L2]** Play Console Help — graphic asset specs — https://support.google.com/googleplay/android-developer/answer/9866151
