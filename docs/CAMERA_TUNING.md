# Camera mode — tuning reference

The durable half of the 30–31 Aug camera rework (STATUS has the short entry). Everything a session
needs to retune, calibrate, or debug camera input. Code comments on each constant carry the local
reasoning; this file is the map.

## Units — the one identity

The detector reports positions and its box width as fractions of the upright frame
(480×640 portrait; x and Size normalize by width, y by height — `FaceObservation.FrameAspect`
carries the measured aspect). All gesture thresholds are in **face widths** (fw):

```
displacement_fw = Δ(frame fraction) / (Size / BoxWidthsPerFace)      face ≈ 15 cm
⇒ a threshold of T fw costs the same T × 15 cm of head travel at ANY distance
```

**`FaceSizeFilter.BoxWidthsPerFace` = 1.35 IS AN ESTIMATE** — BlazeFace regresses its training box
(chin→hairline, past the ears), not an anatomical face. Every centimetre below scales with it.
Calibrate it first (protocol below); it is the single conversion point.

## Dials

| constant | value | means | move it when |
|---|---|---|---|
| `FaceSteering.HalfRangeX` | 1.1 fw | full deflection ≈ 16.5 cm; lane enter ≈ 9.5 cm, hold ≈ 6.7 cm, direct crossing ≈ 19 cm | lane commitment too twitchy (raise) / too tiring (lower); scales all three |
| `FaceSteering.DeadZone` | 0.15 | ≈ 2.5 cm ignored around neutral | runner twitches while standing still at range |
| `FaceSteering.JumpRise` | 0.40 fw | hop = ≥ 6 cm net rise within `JumpLookbackSeconds` 0.25 s, ending `JumpApexAboveBaseline` 0.15 fw above baseline | hops missed (raise lookback before lowering rise) |
| `FaceSteering.SlideDrop` | 0.85 fw | crouch ≈ 12.8 cm below baseline; doubles as the y-drift band | |
| `FaceSteering.JumpRefractorySeconds` | 0.5 s | under the runner's 0.68 s air time — **retuning `RunnerController.JumpVelocity`/`Gravity` retunes this gesture** | |
| score tiers | < 0.45 loss / 0.45–0.65 position / ≥ 0.65 confident | position tier honoured only within `PositionCarrySeconds` 0.30 s of a confident frame (an empty room scores up to 0.47 — never trust a floor alone). Jumps **arm** on any tier, **fire** only on confident | if a blurred real face scores < 0.45 the tier never engages (unmeasured) |
| `FaceSizeFilter.MinPlayableSize` | 0.06 | "step a bit closer…" staging state; ≈ 3.4 m in box units — deliberately long (false "too far" is the expensive direction) | telemetry shows real 2.5 m sizes |
| `LaneSelector` enter/hold | 0.5 / 0.3 | hysteresis ≈ 2.8 cm, ~20× above jitter at 2.5 m | |
| loss behaviour | hold | axis freezes on loss (owner call); reacquisition resumes absolute mapping; `Lift` decays after 0.28 s grace | |

Overlay (`FaceOverlay`): zones drawn to scale (57.5 / 21.25 / 21.25 of deflection); draws
deflection with **no flip of its own** — `CameraFeed.MirrorForSelfie` already mirrors once
upstream; placement params live at the two creation sites (RunFlow / menus).

## Resume gesture (hop twice) — 2026-09-28

Pause-resume in camera mode: camera back → face held 0.5 s (`CameraStaging`) → **stand still**
(recalibration) → **hop twice** → 3-2-1 → run. It replaced the 1 Sep raise-your-right-hand
confirm (owner call, `docs/IOS_HANDOFF.md` decision 7): that needed BlazePose (~12 MiB of APK, a
second model to verify on iOS, and it errored on the OnePlus 6T); a hop is read by the same
`FaceSteering` jump rule the run steers with, so there is no model to load and nothing to fail.
A camera-outage auto-pause begins this staging BY ITSELF (reason line on top) so the loop is
touch-free end to end; manual and app-background pauses wait on the idle card. Touch RESUME and
back work at every step (rule 3 — the gesture augments). The card HIDES during the count (owner
call, 2 Sep device session): what's on screen is the frozen run, the framing overlay and the
numeral; back and the HUD pause button cancel back to the card.

Prompts (card / overlay caption): `stand still…` / `stand still` → `hop twice when ready` /
`hop twice` (+ an up-chevron either side of the glyph's head) → after one hop `one more hop…` /
`one more hop`, each card line followed by `— or tap RESUME`.

How it is built: `PauseMenu` gives the confirm its **own** `CameraFaceInput` (so its own
`FaceSteering`, which it may reset — the run's and the overlay's are never touched) and ticks
`Pose/DoubleHopConfirm` after it each frame. The confirm owns that steering's calibration: it
`Reset()`s it whenever a settle starts, so the baseline is where the player stands NOW, not where
they walked in. Phases: **Settling → Armed → Confirmed**; every stray throws it back to Settling.

**Why the stand-still is the whole design:** the bare jump rule fires on a player WALKING BACK
IN. Walking towards a lens below face height moves the face *up* the frame, and the head bob
rides on top — in the EditMode walk-in trajectories the run's own steering fires 2+ jumps. So:
(1) nothing counts until the player has stood still on a fresh calibration for 1 s; (2) a hop
counts only when it *lands* back on the settled spot; (3) two of them, take-offs ≤ 2 s apart,
feet back on the ground in between. A scripted sweep (outside the repo, 28 Sep) confirmed 0 of
5,760 walk-in variants (speed 0.4–1.4 m/s, lens −0.3…+0.9 m vs face, bob ±1–4 cm, 30/60/90 Hz,
observations held 1–3 frames), 0 of 576 armed-then-walk variants and 0 of 2,250 single hops,
while 99.1 % of 13,500 real double hops (7–22 cm, 0.15–0.45 s ascent, blur, 3 cm drift, 0.9–3 m)
confirmed. The misses are 16–22 cm hops done back-to-back: FaceSteering's own echo refractory
eats the second jump edge (the run would miss it too) — a third hop confirms.

| constant | value | means | move it when |
|---|---|---|---|
| `DoubleHopConfirm.SettleSeconds` | 1 s | stand still this long (confident frames) on a fresh calibration before hops count — the walk-in defence | prompt feels slow (lower, carefully) / walk-ins confirm (raise) |
| `StillDeflection` / `StillLift` / `StillSizeRatio` | 0.30 / 0.50 / 12 % | the settle band: ~5 cm sideways, ~6.4 cm up/down (sway is mostly vertical; tests stress ±3 cm), ±12 % box (~25 cm at 2 m) | honest standers never arm (widen) |
| `RequiredHops` / `HopWindowSeconds` | 2 / 2 s | two landed hops, take-offs ≤ 2 s apart (the 0.5 s jump refractory is the floor) | |
| `StrayDeflection` / `StraySizeRatio` | 0.60 / 20 % | while armed: ~10 cm sideways or ±20 % box → back to Settling (vertical is left to the hop rule) | |
| `LandLift` / `LandDrop` / `MinPeakLift` | 0.35 / 0.15 / 0.25 | landed = back within ~4.5 cm of the baseline, ≥ ~2 cm down from a peak that reached ~3.2 cm | small hops not counted (lower `MinPeakLift`) |
| `LandDeflection` / `LandSizeRatio` | 0.45 / 8 % | a landing must be on the settled spot (~7.5 cm) at the same distance (vs the box width at arming, not `CalibratedSize` — the size low-pass lags a player who just stopped) | forward-drifting hops rejected (raise) |
| `GroundLift` | 0.15 | a take-off counts only if the face is up (> ~2 cm) and, after a landing, has been back down — FaceSteering can fire twice on one big hop | |
| `LandTimeoutSeconds` / `LossGraceSeconds` | 1 s / 0.5 s | a take-off that never lands, or a face gone > 0.5 s, starts over | |
| `PauseMenu.FaceGraceSeconds` | 0.75 s | the confirm wait AND the countdown may lose the face this long (confirm: `HasPosition`, not staging `Ready` — a hop blurs frames under the staging's strict bar) | waits cancel on blur (raise) |
| `ResumeCountdown.DefaultDurationSeconds` | 3 s | numerals + still-player window: `RunFlow.RecalibrateForResume` resets the run's `FaceSteering` when the count starts (`PauseMenu.CountdownStarted`, logs `[CAM] countdown started: run steering reset…`), so it calibrates on the player standing after the hops — the reset at `BeginResume` alone calibrated on the walk-in and a resumed run held a side lane (device, 28 Sep) | owner call (OPEN_QUESTIONS 13) |

Units are `FaceSteering`'s: `Deflection` in `HalfRangeX` (1.1 fw), `Lift` in `SlideDrop`
(0.85 fw), sizes as the smoothed box — so every centimetre above holds at every distance.

**Mid-run pause gesture:** deliberately none — leaving the frame *is* the gesture (auto-pause,
`CameraOutage`, 1.75 s). BlazePose is out of the shipping build entirely (steering: 118 ms vs
30 ms budget, T-010; resume: replaced by hop-twice, 28 Sep).

## Telemetry

~1 Hz + on lane change, tag Unity: `[CAM] telemetry why= ctx= size= x= nx= defl= lift= axis= lane=
score= raw= track= pos= toofar=`. `raw` = pre-threshold detector score; `size` = smoothed **box**
width in frame widths; `defl` in `HalfRangeX` units. `adb logcat -d -s Unity | grep telemetry`.
The resume confirm logs one line per phase/hop change: `[CAM] hop confirm: Settling|Armed|Confirmed
hops=N restarts=N` (`restarts` = times a stray sent it back to Settling).

## Calibration protocol (owner, in order)

1. Framing panel up, **step LEFT** — glyph must go left (sign never verified with a face).
2. Find the movement that reaches the bright enter tick (~9.5 cm; `HalfRangeX` is the dial).
3. **Measure `BoxWidthsPerFace`**: stand 20 s, step exactly 25 cm sideways, stand 20 s →
   `box_cm = 25 × size / Δx`, factor = `box_cm / 15`.
4. Cross-check at 1.00 m: `box_cm ≈ size × 100` (within ~10% of item 3, else re-measure).
5. Re-run any failing gesture and read `defl` / `raw` / `why=lane`: separates "too small",
   "detector dropped you", "game ignored it".
6. Same lane change at 1–2.5 m should cost the same centimetres (distance invariance).
7. 60 s standing still fires nothing; side-lane hop fires (2–4 frames late is by design);
   step out of frame in a side lane → runner holds the lane.
8. Pause/resume framing panel (never yet on screen) + handheld regression (~2–2.5× more movement
   than the pre-rework tuning, by design).
9. Hop-twice resume: step out → auto-pause; walk back in and just stand — it must NEVER resume by
   itself; `stand still…` → `hop twice when ready` → two hops → 3-2-1 → the run resumes aimed
   straight (first `ctx=run` telemetry after the count: `nx` ≈ `x`). Read the
   `[CAM] hop confirm:` lines: many `restarts` while standing = the settle band is too tight.

## Open owner decisions

- **Absolute vs latched lanes**: step-and-stay holds a lane, step-and-return recenters. The
  gesture-resume spec landed 2026-09-01 (countdown + recalibration on every resume) — re-ask
  with that in hand.
- SLIDE is a no-op: nothing consumes `IsSlidePressed`, no obstacle needs it; guide says so.
- Back at the picker does nothing (exit the app?).
- ~~Result hint "tap anywhere or press space" is half-true with three buttons on the card.~~
  Resolved 25 Sep: tap-anywhere restart removed for every scheme; RUN AGAIN button only, plus a
  HOP in camera mode (`RunSession.RestartGesture`), with a camera-only hint under the card.
- Overlay placement (provisional; modular). On the camera result card it now sits above the
  card (`FaceOverlay.ResultPosition`) rather than being hidden — STATUS 25 Sep.
- Hop-twice confirm at the picker's **initial** staging too, replacing the 0.5 s face hold — one
  mechanic taught once, pairs with the Wii-style setup card (BACKLOG T-015). (Was asked about the
  raise-hand confirm; the question carries over to its replacement.)
- Picker: show both schemes' boards, or only the selected one's?

## Live risks

- `BoxWidthsPerFace` unmeasured; blurred-real-face score floor unmeasured (< 0.45 ⇒ position tier
  inert); noise followable ≤ 0.3 s after leaving frame (next dial: per-frame continuity limit);
  `CompositeInput` takes the largest-magnitude axis camera-first, so a frozen full-deflection
  camera axis beats keyboard (Editor-only in practice); 2.5 cm dead zone vs box jitter at 2 m+.

## Device-loop notes (Nord 2)

`pm clear` blocked by OxygenOS · `svc power stayon` / `settings put system screen_off_timeout`
also blocked (`WRITE_SETTINGS`), and the failing `svc` call plants a `com.android.shell` FATAL in
the crash buffer that is easy to misread as a game crash — unattended tests must fit inside the
10-minute screen timeout or be kept alive with input · stalled `adb install -r` → push to `/data/local/tmp` +
`pm install -r` · `screencap` lags a frame — use device-side sleeps in one `adb shell` · exactly
one `E/Unity` line per cold launch is normal (AssetPackManager boot probe, pre-existing) · a
"footer drawn twice at screen top" in downscaled screenshots is a preview artifact — raw pixels
are background (verified 31 Aug).
