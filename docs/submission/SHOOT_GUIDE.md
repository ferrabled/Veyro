# Submission video — director's pack (shoot + edit, one day)

Written 30 Sep 2026, 00:30 CEST. Supersedes the *structure* in `VIDEO_SCRIPT.md` (23 Aug) — that
file's rules still apply (no fake, first frame that matters is the app, no architecture diagrams).
This one adds the motion reel, the exact shot list, the capture commands, and the edit.

**Deadline:** 30 Sep 2026, 11:45 pm PDT = **1 Oct 2026, 08:45 CEST**. Hard cap **2:00**; target
**1:53**. Public on YouTube, embeddable, checked from a logged-out browser.

---

## 0. Decisions the owner makes before anything is recorded

**Owner, 1 Oct:** no iPhone is available for the shoot. The video is shot on the **latest Android
dev build** on the Nord 2 (`1.0.0-dev.20260929-0025`, sideloaded). Consequences are folded into §1.

| # | Decision | Recommended default | Why |
|---|---|---|---|
| D-a | **Dev-build watermark** — crop it or leave it | **Crop** (`--cropbottom 36` in the edit; see §1) | The *Development Build* text sits in the bottom-right 12 px of the screen, below the tab bar. Cropping an edge is ordinary editing; it hides a build-flavour label, not a feature. The menu's `v1.0.0-dev…` version line stays visible — that is fine and honest. |
| D-b | **Fix the wrong in-game copy before shooting?** | **Yes, if you can spare ~30 min for a dev rebuild** | The run screen says "lean to steer, hop to jump, crouch to slide" and the guide says "SWIPE DOWN / CROUCH to slide". The game has **no slide** (`IsSlidePressed` has no gameplay reader) and the reel says **STEP**, not lean. Judges see that card in shot S4. Two strings (`ModePickerCard.cs:47`, `GuideState.cs` pages 2–3) + `BuildAndroidDev` + `adb install -r`. If not, keep S4 short and do not linger on the card. |
| D-c | **Voice-over or captions** (OPEN_QUESTIONS 11) | **Captions + game audio**, reel score for the open | The caption cards exist. Record VO only if you can do it in one pass; the §4 strings double as the script. |
| D-d | **Who is on camera for the hook** | You, from behind or in silhouette; a friend also works | The shot needs a body, not a face. |
| D-e | **Categories on the closing card** | `Best Game · Design · HAMM · #BuildInPublic · OneSignal` (already on `closing.png`) | Only what you will tick on the form. Add Layers only if the experiment write-up exists. |
| D-f | **Purchase in S5** — show the sandbox purchase or stop at the paywall | **Show it**: paywall → buy → RevenueCat *Test Store* sheet → owned → equip | The dev build has no Google sheet; the Test Store sheet is visibly a test store, so it is honest as shown. The HAMM/Design evidence is the paywall itself. |

Already settled: the reel runs whole with the **no-platform end line**, the closing card carries
`veyro.ferrabled.com`, not a store URL, and no caption names a store.

---

## 1. The build on the phone

**Nord 2 / DN2103, 1080×2400, `1.0.0-dev.20260929-0025.nogit`, installed via adb.** Verified 1 Oct
13:20 with a screenshot: menu renders, pass owned, streak card, mode picker with the gold BETA pill,
*Development Build* watermark bottom-right at roughly y 2385–2398 (12 px tall, under the tab bar).

What the dev build means for the shots:

- **Watermark:** `assemble.sh … --cropbottom 36` removes the bottom 36 source rows of every
  `phone` segment before scaling. Check one frame of S1 afterwards: the pause button (bottom-left)
  and the tab-bar labels (bottom ≈ y 2350) must still be whole. If the watermark survives, raise to
  48; if the tab labels are clipped, lower to 28.
- **Store:** RevenueCat **Test Store**, not Google Play Billing. The Paywall-Builder paywall renders
  normally; purchases complete on RevenueCat's test sheet. Shot S5 shows exactly that (D-f).
  Nothing is real revenue, so `LICENSING_REVENUE.md` §5 is not a concern here.
- **Music + SFX, guide, share card, podium, hop-twice** are all in this build — it is the newest UI.
- **Before recording:** RESTORE PURCHASES in SHOP (the pass shows OWNED), play two full runs, send
  yourself one OneSignal test push and confirm it lands on the lock screen.
- **If D-b is yes:** fix the two strings, rebuild (`BuildAndroidDev`, scratch copy if the editor
  has `game/` open — gotcha 4), `adb install -r builds/MotionRunnerDev.apk`, re-verify the menu.

Not an option today: a signed internal-track build or an iPhone recording (none available). The iOS build is the same code; nothing in the video is
Android-specific except the recorder.

---

## 2. Final cut — 1:53

Reel v2 has no clean early cut (its only harmonic resolution is the 24.0 s slam; every section
boundary sits on a transition), so it runs **whole, as the title sequence**, and the film starts
at 0:27. That is the strongest 27 seconds of craft the project has; it also means the live hook
must land immediately after, because judges are only guaranteed to watch two minutes.

| Time | Section | Source | Caption (top-left, Anybody, ink on paper) |
|---|---|---|---|
| 0:00–0:27 | **Title sequence** — reel v2, whole | `builds/reel/out/v2/veyro-run-reel-v2-noplatform.mp4` (end line "Free to play · No ads · Cosmetics only", the D-c default; audio bit-identical to v2). `veyro-run-reel-v2.mp4` is the variant with the "iPhone & Android" end line. | — (burned in) |
| 0:27–0:45 | **The hook** — hands-free, one continuous take | Shot H1 (room) + H2 insert (screen) | `front camera only · camera images stay on the phone` |
| 0:45–0:55 | **The premise** — tilt in hand | Shot T1 (room, hands + phone) | `tilt to steer · tap to jump` |
| 0:55–1:03 | A real run: swerve, jump, coins, combo | S1 (screen) | `three lanes · one thumb-free run` |
| 1:03–1:08 | Crash → result card → tap → running again | S2 | `crash · tap · you're back` |
| 1:08–1:15 | Daily Run HUD line + (optional) two phones, same track | S3 (+ S3b room) | `DAILY · 2026-09-30 · same track for everyone` |
| 1:15–1:20 | Mode picker: TILT & TOUCH / CAMERA **BETA** | S4 | `camera mode is opt-in · the phone is speed-tested first` |
| 1:20–1:38 | **Monetization**: SHOP → Season 1 pass → RevenueCat paywall → purchase sheet (Test Store on the dev build) → owned → Prism equipped → in the run | S5 | `RevenueCat · Paywall Builder · cosmetics only` then `no lives · no boosts · no ads` |
| 1:38–1:44 | Lock-screen notification "Today's run is live" → tap → Daily Run | S6 | `OneSignal · push notifications` |
| 1:44–1:50 | Result card → SHARE → challenge link opens on the second phone | S7 | `challenge a friend · Layers-instrumented` |
| 1:50–1:53 | **Closing card** (static) | reel agent PNG | `Veyro Run · [store URL] · categories (D-e)` |

Caption facts were checked against the code (reel pane, 30 Sep): skins buy **directly on Google's
sheet**; only the **Season 1 pass** goes through the RevenueCat paywall (STATUS 29 Aug) — so S5 must
show the *pass*, not a skin, when the caption says "Paywall Builder". The pass paywall currently
shows a **fake discount** (STATUS 29 Sep) — fix it in the RevenueCat dashboard *before* recording
S5. "Camera images stay on the phone", not "nothing leaves the phone": the build talks to
RevenueCat, OneSignal, Layers and the leaderboard. "Push notifications", not "daily reminder":
`LISTING.md` forbids advertising a scheduled reminder until that increment ships, and S6 is a
manual dashboard push. **There is no slide action in the game** (`IsSlidePressed` is plumbed but
unread; `LISTING.md` "Deliberately absent") — never show or caption one.

Cut on action. Every segment is trimmed in the edit; record long.

---

## 3. Shot list

Two phones. **Game phone** = Nord 2 (1080×2400) with the §1 build. **Camera phone** = OnePlus 6T,
rear camera, 1080p60 (Settings → Video → 1080p, 60 fps; 4K30 is worse for a runner). Landscape,
locked exposure and focus (long-press the subject), on a tripod, a book stack or a mug.

### Room (camera phone records; game phone also screen-records the same take)

| Shot | Setup | Action | Length | Watch for |
|---|---|---|---|---|
| **H1 hook** | Wide, static. Daylight or all room lights on, light **in front** of you, not behind. Game phone propped upright at chest height on a table, front camera facing you, screen visible to the room camera. You 1.5–2 m back, chest-up in the game's frame. Plain wall behind. | Pick CAMERA on the run screen, let the staging finish (framing → 3-2-1). Then: **step right, step left, step right, hop, hop** — slow, deliberate, one lane per step. Keep running ≥ 25 s. | 40 s | **No cuts inside step-swerve-step.** Runner must visibly react in the same frame as the body. If the run pauses (out of frame), stand still, hop twice: also usable footage. |
| **H2 insert** | Same take, from the game phone's own screen recording | — | — | Synced to H1 by the 3-2-1 countdown beep (audible on both). |
| **T1 tilt** | Medium, over the shoulder or from the front, phone in both hands, portrait, screen readable | Tilt left, hold, tilt right, hold, tap to jump ×2 | 20 s | Turn about the phone's **vertical** axis (the way the reel draws it). No slide — the game has none. |
| **S3b two phones** *(optional, strong)* | Both phones side by side on the table, both on today's DAILY run, started within a second of each other | Same obstacles in the same order | 15 s | Only works if the second phone also has a non-dev build. Skip if not. |

### Screen (game phone's built-in recorder; §3.1)

| Shot | Path in the app | Action | Length |
|---|---|---|---|
| **S1 run** | RUN tab → TILT & TOUCH → play | A good run: swerves, two jumps, a coin chain with the combo climbing, score > 150 | 60 s (take the best 8) |
| **S2 loop** | continue S1 | Crash deliberately → result card → tap → new run | 15 s |
| **S3 daily** | RUN tab → DAILY | First seconds of the run with the HUD reading `DAILY · 2026-09-30` | 15 s |
| **S4 picker** | RUN tab | Slowly scroll/hover the mode card so the gold **BETA** pill is readable; tap CAMERA, let the speed gate + permission text show, cancel | 15 s |
| **S5 shop** | SHOP tab | Season 1 pass card → RevenueCat paywall (hold 3 s so it can be read) → buy → purchase sheet → owned → equip Prism → RUN → 5 s of the run in the new look. If the pass is already owned on this profile, record a skin purchase instead (skin row → sheet → owned → equip). | 60 s |
| **S6 notification** | Lock the phone; send a OneSignal push from the dashboard ("Today's run is live") | Notification arrives on the lock screen → tap → app opens on the Daily run | 20 s |
| **S7 share** | After a run: result card → SHARE → share sheet → send to yourself | On the camera phone, open the link → landing page "Beat …" → **Open in Veyro** | 20 s (both phones) |

### 3.1 Screen-recording settings (game phone)

Use the phone's **built-in screen recorder** (Quick Settings tile), not `adb screenrecord`: it
records internal game audio and 60 fps; `adb screenrecord` drops audio and stops at 3 min.

- Resolution **native** (1080×2400), **60 fps** if offered, bitrate highest, **audio: internal/app**
  (not mic). **Show touches: on** for S1/S2/S5 (tap-to-jump reads better), off for H2.
- Before recording: notification shade clear, **Do Not Disturb on** (except during S6),
  brightness max, battery > 50 %, auto-rotate off, screen timeout 10 min, no other apps in recents.
  Wi-Fi on (Play billing and the share link need it).
- Fallback if the tile is missing (no audio, ≤ 3 min per file; Git Bash needs the path guard):

```bash
MSYS_NO_PATHCONV=1 adb shell screenrecord --bit-rate 20000000 --time-limit 180 /sdcard/s1.mp4
adb pull /sdcard/s1.mp4 C:\Users\fraba\Videos\veyro\s1.mp4 && MSYS_NO_PATHCONV=1 adb shell rm /sdcard/s1.mp4
```

- **Do not run a Unity build while recording** — it restarts the adb server (gotcha 6).

### 3.2 Order of the shoot (≈ 90 min once the build is on the phone)

1. Fix the pass paywall discount in the RevenueCat dashboard. Send yourself a test push to confirm
   OneSignal works *before* the shoot. If D-b is yes, the dev rebuild runs while you set up the room.
2. Screen shots first, in order S1 → S2 → S3 → S4 → S5 → S7 → S6. Each in one file; re-take a shot
   immediately if it fails, do not "fix it later".
3. Room shots: T1, then S3b if both phones qualify.
4. **H1 last**, when the game is warmest and you have ten takes in you. Stop after the first take
   with a full uncut step-swerve-step-hop beat.
5. Copy everything to `C:\Users\fraba\Videos\veyro\raw\` with the shot name in the filename.

---

## 4. Captions, cards and what the reel pane produces

**Delivered by the reel pane, 30 Sep, in `builds/reel/out/cards/`** (source `builds/reel/cards.html`
+ `cards.mjs`; `node cards.mjs` re-renders all of them in seconds; strings are its `CAPTIONS`
array). 1920×1080 PNGs with alpha, Anybody, riso pink plate:

- `N.png` (1–10) — for **phone** shots: ink plate, paper type, top-left, inside the left paper panel.
- `Nw.png` — for **wide** shots: paper plate, ink type, lower-third-left.

```
1  front camera only · camera images stay on the phone
2  tilt to steer · tap to jump
3  three lanes · one thumb-free run
4  crash · tap · you're back
5  DAILY · 2026-09-30 · same track for everyone
6  camera mode is opt-in · the phone is speed-tested first
7  RevenueCat · Paywall Builder · cosmetics only
8  no lives · no boosts · no ads
9  OneSignal · push notifications
10 challenge a friend · Layers-instrumented
```

Also there: `closing.png` (reel end frame + category line + `veyro.ferrabled.com`) and
`closing-play.png` (same with the Play URL — **only if Play is confirmed live**). To change the
category line: edit `END_LINES` / `CATEGORIES` in `builds/reel/reel.html`, then
`node render.mjs stills --query "end=closing" 26.65`. The **end-card platform-line variant** of the
reel (D-c) is `builds/reel/out/v2/veyro-run-reel-v2-noplatform.mp4` (+ `-master`): frames up to
23.9 s stream-copied from v2, the end section re-rendered, audio bit-identical. Glyph note: the
fonts have `·`, `€`, `™` but **not `→`/`←`**.

If a PNG is missing, `assemble.sh` falls back to `drawtext` in Bahnschrift. Uglier, still honest.

**VO script, if D-b is "voice":** read the captions as sentences, one per segment, plus the two
lines from `VIDEO_SCRIPT.md` that matter most: *"That's the front camera. Nothing is uploaded — the
model runs on the phone."* over the hook, and *"Everything for sale is cosmetic — no lives, no
boosts, no advantage."* over S5. Record on the camera phone's voice recorder in a quiet room, one
pass per line, 10 cm from the mouth; the edit script takes a single `vo.wav` with `--vo`.

---

## 5. The edit — `docs/submission/assemble/assemble.sh`

One ffmpeg script; no editor needed. It takes an **EDL** (tab-separated; template in
`assemble/edl.tsv`), normalises every clip to 1920×1080 60 fps H.264 + 48 kHz stereo AAC, puts
portrait phone clips on the reel's paper colour with the caption to the left, concatenates,
loudness-normalises to −14 LUFS, and **fails if the result is ≥ 120 s**.

```
# edl.tsv — one line per segment, in order. Columns: kind  file  in  out  caption  card#
reel   builds/reel/out/v2/veyro-run-reel-v2.mp4
wide   raw/H1.mp4   12.0   30.0   front camera only · camera images stay on the phone   1
phone  raw/S1.mp4   22.5   30.5   three lanes · one thumb-free run                      3
card   ../../../builds/reel/out/cards/closing.png   3
```

- `reel` — used as is (already 1080p60, −13.5 LUFS, no black, fades to silence).
- `wide` — landscape room footage, scaled/padded to 1080p; with `--captions DIR` it overlays
  `DIR/<card#>w.png`.
- `phone` — portrait screen recording; scaled to 1080 high, centred on paper `#FBF5E9`; with
  `--captions DIR` it overlays `DIR/<card#>.png`. Without the flag (or a missing file) the caption
  column is drawn with `drawtext`.
- `card` — a PNG held for N seconds (silence).
- `--vo vo.wav` mixes a voice track from 0:27 at −3 dB with the segment audio ducked −9 dB.
- `--music bed.wav` mixes a music bed under everything after the reel at −18 dB.

```bash
cd docs/submission/assemble && ./assemble.sh edl.tsv out/veyro-run-demo.mp4 --captions ../../../builds/reel/out/cards --cropbottom 36
KEEP=1 ./assemble.sh edl.tsv out/veyro-run-demo.mp4 --vo raw/vo.wav   # re-mix only, segments reused
```

Every segment is re-encoded (~1 min for the reel, seconds per clip); `KEEP=1` skips segments
already in `out/work/` when only the mix changed. Tested 30 Sep with synthetic clips (portrait
1080×2340 30 fps with audio, landscape 1080p without audio, a still) plus the real reel: output
1920×1080 60 fps H.264 + AAC 48 kHz, captions placed correctly on both clip kinds, duration
check working.

### Upload

- YouTube, **Unlisted or Public**, embedding allowed, title `Veyro Run — Shipaton 2026 demo`,
  description: one line + store link. Check the link **logged out**. Paste into Devpost B3.
- Keep `out/veyro-run-demo.mp4` and `raw/` — the same footage cuts into the store listing video
  and the #BuildInPublic post.

---

## 6. Timeline — one afternoon (≈ 5 h from "go")

| Elapsed | Milestone |
|---|---|
| 0:00 | §0 answered. If D-b: strings fixed, dev rebuild started. Paywall discount fixed. Test push received. |
| 0:45 | Build on the phone (if rebuilt), RESTORE PURCHASES, two runs played. Recorder settings set (§3.1). |
| 2:00 | All screen shots (S1–S7) in `raw/`. |
| 3:00 | Room shots (T1, S3b, H1) done. |
| 4:00 | Trim times in `edl.tsv`, first assembled cut watched end to end, under 1:55, watermark gone. |
| 5:00 | Final render, YouTube upload (unlisted, embeddable), logged-out check, link pasted into the form. |

---

## 7. Things that get a submission filtered — re-check before upload

- Video **< 2:00** (`ffprobe` the file, not the YouTube page).
- Public/unlisted **and embeddable**; watched once logged out.
- No third-party music: reel score is original, game SFX are CC0, nothing else added.
- No store names in captions ("Now on Google Play", "on the App Store").
- No *Development Build* watermark, no Test Store sheet, no fake discount on the paywall.
- Category line on the closing card equals the boxes ticked on the form.
- The hook is a real, uncut take.
