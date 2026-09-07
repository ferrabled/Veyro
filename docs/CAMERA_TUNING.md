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

## Resume gesture (raise your right hand) — 2026-09-01

Pause-resume in camera mode: camera back → face held 0.5 s (`CameraStaging`) → raise your RIGHT
hand → 3-2-1 → run. A camera-outage auto-pause begins this staging BY ITSELF (reason line on
top) so the loop is touch-free end to end; manual and app-background pauses wait on the idle
card. Touch RESUME and back work at every step (rule 3 — the gesture augments). The card HIDES
during the count (owner call, 2 Sep device session): what's on screen is the frozen run, the
framing overlay and the numeral; back and the HUD pause button cancel back to the card.
BlazePose (detector + lite landmarker, CPU) runs ONLY while `timeScale == 0`: `PoseGestureProbe`
logs `[CAM] pose probe up/down`, self-stops with an error if it ever finds the world unfrozen,
and is disposed the moment the countdown starts. A full cycle is ~334 ms on the Nord 2 (T-010),
so the probe answers at ~3 Hz; a floor of 0.25 s/sample keeps Editor rates honest.

| constant | value | means | move it when |
|---|---|---|---|
| `RaisedHand.HeadUnitsAboveNose` | 0.5 | wrist must clear the crown (~10–12 cm above the nose); head unit = nose→shoulder-line span | hair-pushback/wave false-confirms (raise) / real raises missed (lower) |
| `RaisedHand` wrist gate | visibility ≥ 0.5 only | an overhead wrist rides the crop edge: presence collapses while visibility holds (2 Sep device), so presence is NOT required for the wrist | overhead false-confirms appear (re-add presence + rely on the elbow) |
| `RaisedHand` elbow fallback | elbow strictly above nose, full `IsTracked` | vouches when the hand left the frame entirely; no near-face fidget puts an elbow overhead | |
| `RaisedHandConfirm.RequiredSamples` | 3 | ~1 s hold at 3 Hz; NET evidence — a miss winds back one sample, alternating noise never confirms (a hard reset flip-flopped the prompt against a held arm, 2 Sep) | hold feels laggy / twitchy |
| `PoseGestureProbe.MinSampleIntervalSeconds` | 0.25 s | sample cadence floor, device-independent hold | |
| `PauseMenu.CountdownFaceGraceSeconds` | 0.75 s | staging may lose the face this long mid-count before cancelling to the paused card | countdown cancels on blur (raise) |
| `ResumeCountdown.DefaultDurationSeconds` | 3 s | numerals + still-player window for `FaceSteering` recalibration | owner call (OPEN_QUESTIONS 13) |

**Mirror rule (pinned in `RaisedHandTests`):** the upright frame is selfie-mirrored once, and
BlazePose labels anatomy as if it never was — so the player's right hand is `PoseJoint.LeftWrist`
on the mirrored frame (`RaisedHand.WristFor`). The overlay's raised-arm hint draws on the glyph's
screen-right, which is the same side; never flip either independently.

**Mid-run pause gesture:** deliberately none — leaving the frame *is* the gesture (auto-pause,
`CameraOutage`, 1.75 s). Pose during gameplay stays abandoned (118 ms vs 30 ms budget, T-010).

## Telemetry

~1 Hz + on lane change, tag Unity: `[CAM] telemetry why= ctx= size= x= nx= defl= lift= axis= lane=
score= raw= track= pos= toofar=`. `raw` = pre-threshold detector score; `size` = smoothed **box**
width in frame widths; `defl` in `HalfRangeX` units. `adb logcat -d -s Unity | grep telemetry`.

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

## Open owner decisions

- **Absolute vs latched lanes**: step-and-stay holds a lane, step-and-return recenters. The
  gesture-resume spec landed 2026-09-01 (countdown + recalibration on every resume) — re-ask
  with that in hand.
- SLIDE is a no-op: nothing consumes `IsSlidePressed`, no obstacle needs it; guide says so.
- Back at the picker does nothing (exit the app?).
- Result hint "tap anywhere or press space" is half-true with three buttons on the card.
- Overlay placement (provisional; modular).
- Raise-hand confirm at the picker's **initial** staging too, replacing the 0.5 s face hold — one
  mechanic taught once, pairs with the Wii-style setup card (BACKLOG T-015).
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
