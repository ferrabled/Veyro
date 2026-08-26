# T-030 — screenshot shot list

For whoever has the device. **No captures exist yet** — this is the instruction sheet, written
23 Aug 2026 so the capture session is 30 minutes and not an afternoon of re-shoots.

Two audiences, two different requirements, one capture session:

| | Google Play | Devpost |
|---|---|---|
| Count | 2 min / 8 max per device type; **4–6 is the sweet spot** | ≥ 1 |
| Size | min dimension 320 px, max 3840 px, longest side ≤ 2× shortest; **1080×1920 recommended** [G1] | **exactly 1179 × 2556**, no device frame [D1] |
| Format | JPEG or 24-bit PNG, no alpha [G1] | not specified — use PNG |
| Device frames | allowed | **forbidden** |

**1179 × 2556 is the iPhone 15/16 Pro portrait resolution** (2556/1179 ≈ 2.168:1). No Android phone
produces it natively, so it is a **crop/scale step in an image editor**, not a capture setting.
See §3 — get this wrong and the submission is filtered before a judge sees it [D1].

---

## 1. Capture procedure (Android, the proven loop)

Per CLAUDE.md's known-good loop and STATUS 20 Aug. Git Bash rewrites unix-looking paths, hence the
`MSYS_NO_PATHCONV=1` prefix on device paths only (gotcha 6):

```bash
# launch (Unity 6 GameActivity — gotcha 5)
adb shell monkey -p com.ferrabled.veyro.run -c android.intent.category.LAUNCHER 1

# capture
MSYS_NO_PATHCONV=1 adb shell screencap -p /sdcard/veyro_01.png
adb pull /sdcard/veyro_01.png C:\GIT\run-game\builds\shots\veyro_01.png   # local path stays Windows
MSYS_NO_PATHCONV=1 adb shell rm /sdcard/veyro_01.png
```

Never `adb pull /sdcard/` — it pulls the whole phone (gotcha 6).

Before the first capture:

- **Screen recording on as well.** Record a continuous 3–4 minute play session and pull stills from
  it afterwards. Re-entering a specific game state to re-shoot one frame is the thing that turns 30
  minutes into an afternoon, and the same recording feeds the T-035 submission video.
- Notification shade clear, Do Not Disturb on, battery > 50%, brightness at max.
- The status bar **is** in an `adb screencap`. Either accept it (normal, and honest) or hide it with
  `adb shell settings put global policy_control immersive.full=*` — and remember to
  `adb shell settings delete global policy_control` afterwards.
- Portrait only. The game locks portrait at `GameBootstrap.cs:16`.

---

## 2. The shots

Ordered as they should appear in the Play listing — Play shows the first 2–3 above the fold, so the
hook goes first and the paywall goes last.

### Shot 1 — mid-run, obstacle immediately ahead *(the hook; also the Devpost shot)*

- **State:** running, score above ~150 so the HUD numbers look lived-in, an obstacle 1–2 chunks ahead
  and clearly readable, runner slightly off-centre mid-swerve (not dead centre — motion should be
  legible in a still).
- **Why:** this is the only shot most people will look at. It has to answer "what is this game" in
  under a second.
- **Watch for:** combo indicator visible if a combo is running; no result panel; runner not clipped
  behind an obstacle.

### Shot 2 — the tilt itself

- **State:** same as shot 1 but captured while the phone is visibly tilted, so the runner is hard
  against one side of the road.
- **Why:** the listing's entire claim is "you steer by moving the device". A screenshot cannot show
  motion, so this shot plus caption text is the substitute. Pairs with a text overlay if the owner
  wants captions (see §4).

### Shot 3 — Daily Run banner

- **State:** early in a run, HUD mode line reading `DAILY · 2026-09-XX`.
- **Why:** "everyone plays the same track today" is the retention hook and the OneSignal story. It
  is literally rendered by `RunHud.SetMode` (`RunHud.cs:72`) — capture it, do not mock it up.
- **Watch for:** capture on a date that will still look current at launch. A screenshot dated
  2026-08-24 sitting on a listing in October reads as abandoned. **Recapture on release day if the
  date is more than ~2 weeks stale.**

### Shot 4 — result screen with a good score

- **State:** crash → result panel, score high enough to be aspirational, best-score line visible.
- **Why:** shows the loop closes and that progress is tracked. Also the natural "beat my score"
  share image for #BuildInPublic and Noise.

### Shot 5 — mode-select menu *(ship in v1.0)*

- **State:** `ModeSelectMenu` showing TILT & TOUCH vs CAMERA (BETA).
- **Why:** it is the honest way to show camera mode exists without claiming it is the default. The
  BETA label being visible in the screenshot is the point.
- **Ship this in the v1.0 listing.** Reversed 26 Aug: the old note said to hold it because "the v1.0
  build has no menu worth showing and the listing does not mention the camera" — both are now false.
  `ModeSelectMenu` is in the build, camera mode ships in v1.0 (OPEN_QUESTIONS 12), and the listing
  advertises it (LISTING.md §2–§3). This shot is also the visual half of explaining the CAMERA
  permission to a reviewer, which makes it one of the more valuable slots, not an optional extra.

### Shot 6 — paywall *(only once T-020 is in the build)*

- **State:** the RevenueCat Paywall-Builder paywall, cosmetics visible, price shown.
- **Why:** the HAMM and Design categories are judged partly on the paywall. Judges also want to see
  it before they redeem a code.
- **Watch for:** a real localized price string from the store, not `$0.00` and not a placeholder.
  This is device-only — the paywall does not render in the Editor (REVENUECAT_PLAN §5).

### Shot 7 — hands-free camera mode, in use *(v1.0; needs a second camera to capture)*

- **State:** a person standing 2 m from a propped-up phone, mid-lean, game visible on screen.
- **Why:** the single most differentiating image the project can produce.
- **Note:** this is a **photograph of a person playing**, not a screencap — so it belongs in the
  feature graphic and the video, and in the Play listing only if the owner is comfortable with a
  non-screenshot in the set (Play allows it; some reviewers consider it misleading if it is the
  first image). Recommended: video and feature graphic, not the screenshot carousel.

---

## 3. The Devpost 1179 × 2556 frameless shot — exact recipe

Devpost: *"At least one screenshot…1179px width and 2556px height WITHOUT device frames"* [D1].
Screener filtering checks marketing assets before a judge scores anything [D2], so treat the pixel
dimensions as a hard gate.

1. Start from **Shot 1** at the phone's native portrait resolution (the Nord 2 / DN2103 is
   1080 × 2400 → aspect 2.222).
2. Target aspect is 2556/1179 ≈ **2.168** — slightly *wider* than the source. So: **scale to width
   1179** (1080 → 1179 gives height 2620), then **crop 64 px off the height**, taking it off the top
   where the status bar and empty sky are, not off the bottom where the HUD is.
3. Export PNG, no device frame, no border, no added text.
4. **Verify before submitting:** `python3 -c "from PIL import Image;print(Image.open('shot.png').size)"`
   must print exactly `(1179, 2556)`.

If the phone is ever swapped, redo the arithmetic from that phone's native resolution — do not reuse
the 64 px number.

---

## 4. Captions / text overlays — recommendation

**Do not add text overlays for v1.0.** Reasons: they must be redone for every locale, they read as
marketing rather than gameplay, and the RevenueCat Design category rewards craft in the app rather
than in Photoshop. The short description already carries the words.

If the owner disagrees, the three captions worth having are "TILT TO STEER", "SAME TRACK FOR
EVERYONE, EVERY DAY", "HANDS-FREE (BETA)" — top third only, never over the HUD.

---

## 5. Delivery

- Raw captures → `builds/shots/` (gitignored — they are binaries and Git LFS is not set up yet, D2).
- The Devpost 1179 × 2556 export and the final Play set → wherever the owner keeps submission
  assets; list the paths in `docs/submission/CHECKLIST.md` when they exist.
- **Nothing binary lands in `docs/store-kit/`** — this task is copy-only by scope, and committing
  PNGs before Git LFS is configured is the exact thing D2 warns against.

---

## Sources

- **[G1]** Play Console Help — graphic assets, screenshots, icon, feature graphic — https://support.google.com/googleplay/android-developer/answer/9866151
- **[D1]** Shipaton 2026 rules — 1024×1024 icon, ≥1 screenshot at 1179×2556 without device frames — https://revenuecat-shipaton-2026.devpost.com/rules
- **[D2]** How we judge Shipaton — screener filtering on marketing assets — https://www.shipaton.com/blog/how-we-judge-shipaton
