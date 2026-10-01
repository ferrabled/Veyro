# Motion reel (v2, 26.7 s)

A motion-graphics teaser for the Devpost description and social posts, 1920×1080 at 60 fps with 4-sample motion blur. It is not the <2 min demo video (`../VIDEO_SCRIPT.md`). That one still has to show the app running on a device.

Everything is code:
- `reel.html` draws each frame as a pure function of time (`seek(t)`).
- `render.mjs` captures the frames in headless Chrome.
- `music.py` synthesizes the score against the cue list the page exports.

`./build.sh` rebuilds the reel from the repo. It needs Chrome, ffmpeg, Node and Python. Output goes to `out/veyro-run-reel.mp4`. `./build.sh draft` skips motion blur.

## Timing

The scenes are authored on a 120-BPM design timeline of 20 s (40 beats). `TS` in `reel.html` sets the playback tempo: `90/120` plays those 40 beats at 90 BPM, which gives 26.7 s. The page exports the timescale with its cue list, and `music.py` stretches every note and cue by the same factor, so picture and score stay locked.

v1 ran at 120 BPM (20 s). The owner found that too fast to read, so v2 slows it down.

## Beat map (output seconds, 90 BPM, F major)

| Time | Section | What happens |
|---|---|---|
| 0–2.7 s | Intro | The daily-ticket icon prints: the V is written stroke by stroke, the card is revealed as ink floods the frame, and the barcode scans in. The camera then flips the V and falls into its notch. |
| 2.7 s | Drop | Cut to the park: the V's inner edges become the path's vanishing edges. |
| 2.7–6.7 s | Tilt | A full-bleed run with a level horizon; the TILT word turns with each lane change. The view then shrinks into a 3D phone that turns on its vertical centre axis, the gesture `GyroTiltInput` reads (acceleration.x, about 20° of roll; guide art `tilt_02/03`). The turn arrow orbits at mid-height, behind the phone and then in front. The tilt/lane readout is live. |
| 6.7–9.3 s | Tap | A tap ripple irises into an engineering drawing of the real jump arc. |
| 9.3–14.7 s | Hands-free | The guide illustrations: STEP right, STEP left (one lane per step), then HOP. Beside them, a phone's-eye viewfinder shows the six face keypoints and telemetry. Half-time groove. |
| 14.7–20 s | Daily Run | The date rolls in and becomes the seed hash. A print head prints the track, which tips into perspective and multiplies into seven players' identical tracks. Band stops land on "ONE TRACK. / EVERYONE. / EVERY DAY." |
| 20–23.3 s | Looks | Ember, Frost and Prism are dealt as tickets, then flip to "0 BOOSTS · 0 EXTRA LIVES · 0 ADS". |
| 23.3–26.7 s | End | The cards collapse into the ticket. The wordmark lands on the downbeat, followed by the tagline and platforms. |

## Variants and cards for the submission cut (`../SHOOT_GUIDE.md` §4)

- **End line:** `render.mjs --query end=<variant>` swaps the end card's line. The variants are `noplatform` ("Free to play · No ads · Cosmetics only") and the static `closing` / `closing-play` cards (the category line plus the site or Play URL). Edit the text in `END_LINES` in `reel.html`. `veyro-run-reel-v2-noplatform*.mp4` is v2 with only its tail (23.917 s keyframe onward) re-rendered; the audio is v2's stream, copied.
- **Captions:** `node cards.mjs` renders the caption set (the `CAPTIONS` array) as 1920×1080 alpha PNGs. `N.png` is an ink plate for the paper side panels of the phone footage; `Nw.png` is a paper plate for full-frame footage. Outputs go to `out/cards/`.

## Every number on screen is the game's

| On screen | Source |
|---|---|
| v₀ 7.5 m/s, g 22 m/s², apex 1.78 m, air 0.68 s | `RunnerController.JumpVelocity/Gravity`, `TrackMetrics.RunnerRestY` |
| barrier 0.5 m, 3 lanes × 1.6 m | `TrackMetrics.LowBarrier*`, `LaneCount`, `LaneWidth` |
| seed 20260930 · greybox-1 · greybox → FNV-1a → 0xC56BC9AF | `DailySeed.ForDate` + `RunSeed.RngState`, ported to JS. The port reproduces the pinned `RunSeedTests` values. |
| INFER 3.7 ms | The shipping in-game face-detector gate reading. STATUS, 23 Aug (T-040), records it as the figure public posts use. |
| "never recorded, saved or sent", "cosmetics only", "no ads" | Site copy and `docs/store-kit/LISTING.md` |

Nothing automotive and no sliding (`LISTING.md` "Deliberately absent").

The v2 slowdown affects the jump drawing: it plays at 90/120 speed, so the on-screen arc takes 0.91 s while the label keeps the game's real 0.68 s.

The track rows in the Daily Run section are drawn from an XorShift stream seeded with the real hash. They illustrate determinism; they are not the chunk generator's output.

## Licences

- **Score:** original, synthesized by `music.py`. There is no third-party music, so there is no copyright-claim risk on YouTube.
- **Foley:** the game's own CC0 SFX (`game/Assets/Resources/Audio/SOURCES.md`).
- **Fonts:** Anybody, Atkinson Hyperlegible and Fragment Mono, all SIL OFL. They are fetched at build time and not committed.
- **Art:** the approved icon geometry (redrawn in code), the three camera guide illustrations and the skin renders. The guide illustrations are the owner-supplied AI-generated set (`docs/GUIDE_ILLUSTRATIONS.md`). Keep that in mind wherever a platform asks about AI-generated media.
