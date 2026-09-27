# How-to-play guide illustrations — assets and generation brief

Written 24 Sep 2026, restructured 25 Sep into one self-contained section per image.
**28 Sep: all ten owner-supplied, AI-generated illustrations are integrated.** Originals live in
`screenshots/guide/`; byte-identical runtime copies live in `game/Assets/Resources/Art/Guide/`.
Both PNG folders use Git LFS, and `screenshots/guide/` is explicitly kept by `.gitignore`.
The first-run guide loads four tilt frames, five camera frames and one run still. Animated
pages advance every 1.1 seconds, including while paused. A caption-only fallback remains for
an unavailable sequence; normal pages no longer show "illustration coming" or asset filenames.
The prompts below are retained for future replacements and additional frames.

## Updating the images

1. Save the approved originals in `screenshots/guide/`, using the names in §1.
2. Copy those PNGs to `game/Assets/Resources/Art/Guide/`, keeping the same names. Unity loads this
   second folder; changes to the originals are not copied automatically.
3. Let Unity import them. `GuideArtImport` sets Sprite / Single / FullRect, no mipmaps, sRGB,
   max 1024, and Android ASTC 6x6. Keep the generated `.meta` files with the runtime PNGs.
4. Run Veyro → UI → Render how-to-play pages. The renderer checks that every expected sprite
   loads, saves the four pages and all ten individual frames, and fails if a page uses fallback.

The current originals are 1586×992 (approximately 16:10), 1.6–2.7 MB each; they are copied without
resizing, recolouring or palette reduction. This supersedes the original ≤400 KB source-PNG
target below: Unity applies the runtime size/compression settings, and both copies share their
content-addressed LFS objects. Preserve the approved masters when updating the game.

**25 Sep review, checked against the code:**
- Camera frames moved from a side view to a centred, symmetric view from behind and above the
  player: a sideways lean runs toward or away from the viewer in a side view, so it could not be
  seen. A second change came after the owner reviewed the first `camera_02`, in which the player
  stood off to one side, the floor line started at a table leg and the camera wedge spilled onto
  the floor. Everything now sits on one centre line, and the wedge stops at the player (§4).
- Tilt is a sideways tip around the phone's long axis (roll), not a steering-wheel turn. Owner
  correction after the first `tilt_02` came back as a steering-wheel rotation; see §3.
- Coins are gold `#FFC85C` in the game, not pink.
- The track on the phone screen now uses the park's real colours.
- There are no slide frames. No gameplay code currently reads `IsSlidePressed`; the owner
  requested keeping the slide instructions on 27 Sep because that mechanic is planned next.
- The pause/resume extras are renamed `pause_01`/`pause_02`, so they no longer ship unused
  inside the `camera` set.

## 1. What the game expects (contract with the code)

| Page | Key | Frames | Files | Shows |
|---|---|---|---|---|
| TILT & TOUCH | `tilt` | 4 | `tilt_01.png` … `tilt_04.png` | neutral hold → tilt left → tilt right → thumb tap (jump) |
| CAMERA MODE | `camera` | 5 | `camera_01.png` … `camera_05.png` | empty spot to stand on → player on the spot → step left → step right → hop |
| DURING A RUN | `run` | 1 | `run_01.png` | runner, a coin row, the pause button |

- **Size:** approximately 16:10, between 1024 and 1600 px wide (the supplied 1586 × 992 set is
  used unchanged), PNG, opaque. The slot is 804 × 502 at reference resolution and
  Unity caps the texture at 1024 wide on Android, so do not go bigger. The frame is drawn with
  `preserveAspect`, so only the ratio matters.
- **Background:** flat paper `#FBF5E9`, edge to edge. **No border, no drop shadow, no rounded
  corners.** The guide card is the same opaque paper colour, so the image blends into it.
- **No text, no numbers, no labelled arrows, no logos, no watermark.** The game renders all the
  wording (it can be localised, and the owner rejected AI-looking type). Plain arrows and motion
  lines are fine.
- **All frames of one sequence share the exact camera, framing, scale and figure.** The game
  shows each frame for 1.1 s in a loop, so they must read as one animation, not a slideshow.
  Every later frame is therefore an **edit of its sequence's anchor frame**, not a fresh prompt.
- If only one image per mode is affordable, deliver `tilt_02` and `camera_03` as `*_01.png` and
  set `FrameCount` to 1 in `GuideState.Pages`.
- Drop the files into `game/Assets/Resources/Art/Guide/`. Git LFS and the import settings are
  already set up. **Keep `pause_01`/`pause_02` (§6) out of that folder** until a page uses them:
  every file in `Resources/` ships in the APK.

## 2. How to use the sections below

1. **Order:** `tilt_01` first → the rest of `tilt` → `camera_02` (the camera anchor) → the rest
   of `camera` → `run_01`. Once `tilt_01` is approved it becomes the style reference for
   everything else, so the three sequences look like one set.
2. **Each section is complete.** Attach what its **Attach** list says, in that order, then paste
   its block. Every block restates the full scene, so it still works if your tool treats the
   attachment only loosely.
   - For a frame that changes one thing, prefer the tool's **select-area edit**: brush over just
     the part that changes, then paste. Whole-image edits sometimes redraw everything, and the
     loop then visibly jumps.
   - After every edit, toggle it against its anchor in an image viewer. Nothing but the
     described change should move.
3. **Aspect ratio:** ask for 16:10. If the tool only offers 3:2 (e.g. 1536×1024) or 16:9,
   generate that and pad or crop to 1600×1000 with `#FBF5E9`. The background is flat paper, so
   padding does not show.
4. **Negative prompts:** each block ends with a "Do not include" sentence. If your tool has a
   separate negative field, move that sentence there.
5. **Game screenshots:** take them from the **current park build**. Never use
   `screenshots/v1.0.1/`, which is the 25 Aug greybox (blue road, capsule runner, red blocks) that
   the owner vetoed. A model will copy what it sees. Two ways to capture:
   - In the Editor: Play `Main.unity`, pause at the moment you want, and grab the Game view at
     1080×2400.
   - On a phone: `cmd /c "adb exec-out screencap -p > C:\path\shot.png"`.

   Always crop off the score strip at the top (score, coin count, combo text): models reproduce
   any text they are shown.
6. **Style reference for the very first image:** crop `screenshots/graphic.png` to its right 30%
   (from x ≈ 1290 px): the winding path and pink diamonds, with no letters. If the result comes
   back with a dark teal background, generate again without this attachment.

## 3. TILT & TOUCH — `tilt_01` … `tilt_04`

### `tilt_01.png` — neutral hold (anchor: generate first)

**Attach**
1. The `graphic.png` crop from §2.6 (style only).
2. A gameplay screenshot showing:
   - tilt mode in portrait, mid-run;
   - the runner in the **centre lane**;
   - a straight row of gold coins ahead in the same lane;
   - no obstacle close to the runner;
   - trees on both banks if possible.

   Crop off the top score strip. The model only needs it to know what the phone screen shows.

```text
Flat risograph-style instructional illustration. Inks: deep teal #12454C as the main ink, fluoro pink #EE3D87 for accents and motion marks, mint #87BEA6 and warm gold #FFC85C, printed on warm off-white paper #FBF5E9. Slight paper grain; any ink misregistration is a hairline offset at most, never a pink outline or halo around shapes; bold clean flat shapes, no gradients, no shading, no outlines around fills. It should feel like a 1970s safety pictogram redrawn by a modern indie game studio.

Attached image 1 is a style reference only: copy its ink grain, misregistration and teal/pink/paper palette, but NOT its dark background, its lettering or its layout. Attached image 2 is a screenshot of the game: use it only to know what the game screen shows, and redraw that content tiny, flat and simplified. Do not copy its 3D rendering.

Scene: point-of-view close-up of two hands holding a modern smartphone upright in portrait orientation, the way you hold a phone to play a game, seen from the player's own eyes. The phone fills about half the image height and is centred. Hands and phone body are solid teal; the hands are simplified but real hands with a thumb and fingers wrapped round the phone's sides.

The phone's screen shows a tiny simplified view of the game from behind the runner: a straight garden path of three lanes receding to the horizon (centre lane paper-white, the two side lanes pale warm stone #E9DFCB, thin stone-coloured edges), mint grass banks and two or three round teal trees on each side. A small human runner figure (pink top, teal trousers, seen from behind) runs in the centre lane, and a short straight row of small gold coins floats ahead of it in the same lane.

In this frame: the phone is perfectly upright and level, the runner is in the centre lane, calm and neutral. No arrows, no motion marks.

Wide 16:10 landscape image. The background is plain flat paper #FBF5E9 all the way to every edge: no border, no frame, no vignette, no rounded corners, no drop shadow. Generous empty paper around the subject.

Do not include: any text, letters, numbers or labels (none on the phone screen either), logos, watermark, signature, app interface elements, photo or photorealism, 3D render, gloss, gradients, blur, depth of field, lens flare, dark background, stick figures, capsule or blob bodies, extra fingers or limbs, cars or any vehicle, motor-racing track, city skyline, neon.
```

**What "tilt" means here.** The player tips the phone sideways around its own long axis: one side
edge dips down and away, the other rises. That is phone "roll", and `GyroTiltInput` reads it as
the accelerometer's x axis. It is **not** a steering-wheel turn: the phone's top never swings left
or right. Models default to the steering wheel, so the tilt prompts spell out the difference.

### `tilt_02.png` — tilt left

**Attach**
1. Your approved `tilt_01.png`. Start from it, never from a rejected `tilt_02`.

```text
Edit the attached image. Keep the style, inks, paper, framing, scale, hands and phone exactly as they are; this is the next frame of a simple two-frame animation.

Change only this: the player tips the phone sideways around its own long, vertical centre line, the way you tip a tray to let something slide off its left side. The LEFT edge of the phone dips down and away from the viewer and the RIGHT edge rises toward the viewer; the left hand moves slightly down and away with it, the right hand slightly up and closer. The phone stays upright in portrait: its long edges stay vertical in the picture and its top does NOT lean to the left. This is NOT a steering-wheel turn and NOT a rotation within the picture plane. Because the screen now faces a little to the left, it looks slightly narrower (foreshortened), and the phone's right side edge shows as a thin teal strip of its thickness. The picture on the screen stays flat on the screen and turns with it.

One single bold pink curved arrow, drawn as a short piece of a flat oval hoop around the phone at the height of the screen's centre, seen slightly from above: it sweeps across the front of the phone from the right edge to the left edge, with its arrowhead at the left. On the phone screen the small pink-topped runner has moved from the centre lane to the LEFT lane; everything else on the screen stays the same.

Scene, unchanged from the attached image: flat risograph-style instructional illustration in deep teal #12454C, fluoro pink #EE3D87, mint #87BEA6 and gold #FFC85C on warm off-white paper #FBF5E9, slight grain, at most a hairline ink offset and no pink outlines around shapes, flat shapes, no gradients. Point-of-view close-up of two teal hands holding a teal smartphone upright in portrait orientation, phone about half the image height, centred. The screen shows a tiny three-lane garden path from behind the runner (centre lane paper-white, side lanes pale stone #E9DFCB, mint banks, round teal trees) with a row of small gold coins in the centre lane.

Wide 16:10 landscape, plain flat #FBF5E9 paper to every edge, no border, no rounded corners, no drop shadow.

Do not include: a phone rotated or leaning within the picture plane, a steering wheel or steering-wheel pose, any text, letters, numbers or labels (none on the phone screen either), logos, watermark, app interface elements, photorealism, 3D render, gradients, blur, dark background, stick figures, capsule or blob bodies, extra fingers or limbs, vehicles, neon.
```

If the oval arrow comes out muddled, drop that paragraph's arrow sentence and regenerate. The
narrowed screen and the visible side edge carry the motion on their own.

### `tilt_03.png` — tilt right

**Attach**
1. Your approved `tilt_01.png`.

Shortcut, no generation needed: if the two-hand grip in `tilt_02` is symmetric, flipping `tilt_02`
horizontally gives this frame exactly. Check that nothing asymmetric (a watch, a phone camera
bump) gives the flip away.

```text
Edit the attached image. Keep the style, inks, paper, framing, scale, hands and phone exactly as they are; this is the next frame of a simple two-frame animation.

Change only this: the player tips the phone sideways around its own long, vertical centre line, the way you tip a tray to let something slide off its right side. The RIGHT edge of the phone dips down and away from the viewer and the LEFT edge rises toward the viewer; the right hand moves slightly down and away with it, the left hand slightly up and closer. The phone stays upright in portrait: its long edges stay vertical in the picture and its top does NOT lean to the right. This is NOT a steering-wheel turn and NOT a rotation within the picture plane. Because the screen now faces a little to the right, it looks slightly narrower (foreshortened), and the phone's left side edge shows as a thin teal strip of its thickness. The picture on the screen stays flat on the screen and turns with it.

One single bold pink curved arrow, drawn as a short piece of a flat oval hoop around the phone at the height of the screen's centre, seen slightly from above: it sweeps across the front of the phone from the left edge to the right edge, with its arrowhead at the right. On the phone screen the small pink-topped runner has moved from the centre lane to the RIGHT lane; everything else on the screen stays the same.

Scene, unchanged from the attached image: flat risograph-style instructional illustration in deep teal #12454C, fluoro pink #EE3D87, mint #87BEA6 and gold #FFC85C on warm off-white paper #FBF5E9, slight grain, at most a hairline ink offset and no pink outlines around shapes, flat shapes, no gradients. Point-of-view close-up of two teal hands holding a teal smartphone upright in portrait orientation, phone about half the image height, centred. The screen shows a tiny three-lane garden path from behind the runner (centre lane paper-white, side lanes pale stone #E9DFCB, mint banks, round teal trees) with a row of small gold coins in the centre lane.

Wide 16:10 landscape, plain flat #FBF5E9 paper to every edge, no border, no rounded corners, no drop shadow.

Do not include: a phone rotated or leaning within the picture plane, a steering wheel or steering-wheel pose, any text, letters, numbers or labels (none on the phone screen either), logos, watermark, app interface elements, photorealism, 3D render, gradients, blur, dark background, stick figures, capsule or blob bodies, extra fingers or limbs, vehicles, neon.
```

### `tilt_04.png` — tap to jump

**Attach**
1. Your approved `tilt_01.png`.
2. *(Optional)* a gameplay screenshot with the runner **in mid-air over a gold hurdle** in the
   centre lane (the knee-high gold bar with a paper-white top). Crop off the score strip. Skip it
   if you can't catch the moment; the prompt describes the hurdle.

```text
Edit the attached image. Keep the style, inks, paper, framing, scale, hands, grip and phone exactly as they are; this is the next frame of a simple animation. If a second image is attached, it is a game screenshot: use it only to know what the jump looks like, and redraw it tiny and flat.

Change only this: the phone is upright and level, exactly as in the attached image. One thumb lifts off the grip and taps the lower middle of the screen; three short pink ripple rings spread around the thumb tip. On the phone screen the small pink-topped runner is in mid-air above the centre lane, jumping over a low gold #FFC85C hurdle bar (with a paper-white top edge) that crosses the centre lane, with small pink motion lines under its feet.

Scene, unchanged from the attached image: flat risograph-style instructional illustration in deep teal #12454C, fluoro pink #EE3D87, mint #87BEA6 and gold #FFC85C on warm off-white paper #FBF5E9, slight grain, at most a hairline ink offset and no pink outlines around shapes, flat shapes, no gradients. Point-of-view close-up of two teal hands holding a teal smartphone upright in portrait orientation, phone about half the image height, centred. The screen shows a tiny three-lane garden path from behind the runner (centre lane paper-white, side lanes pale stone #E9DFCB, mint banks, round teal trees) with a row of small gold coins ahead.

Wide 16:10 landscape, plain flat #FBF5E9 paper to every edge, no border, no rounded corners, no drop shadow.

Do not include: any text, letters, numbers or labels (none on the phone screen either), logos, watermark, app interface elements, photorealism, 3D render, gradients, blur, dark background, stick figures, capsule or blob bodies, extra fingers or limbs, vehicles, neon.
```

## 4. CAMERA MODE — `camera_01` … `camera_05`

**Viewpoint:** directly behind the player and above them, looking down. Five things sit on one
vertical centre line: the phone, the middle of the table, the dashed floor line, the player and
the pink camera wedge. That fixes three things at once:
- The player stands exactly in front of the phone.
- The player's left is the picture's left. That is how the game maps it: step left, the runner
  goes into the left lane.
- The scene is symmetric, so `camera_04` is simply `camera_03` flipped.

Looking down keeps the phone visible above the player's head.

The lane frames show a **side step of the whole body, not a lean**, at the owner's call. The
lane follows where the face is, so the step moves the head over, and it must stay within the
wedge's width.

**The wedge** is the camera's view. The first attempt drew it as a beam that ran past the player
and landed on the floor. Every prompt therefore pins it down: a flat triangle from the phone's
front camera that **stops at the player**.

Generate **`camera_02` first**: it is the anchor. Then make `01`, `03`, `04` and `05` as edits of
it. `camera_01` shows the empty spot to stand on, not a "too close" pose: from this viewpoint a
player standing near the table would hide the phone.

### `camera_02.png` — standing on the spot (anchor: generate first)

**Attach**
1. Your approved `tilt_01.png` (style reference, so this set matches the tilt set).

No screenshot needed: no game screen shows this room. Don't attach a rejected attempt, even one
with a table or figure you like: the model copies its layout too.

```text
Flat risograph-style instructional illustration. Inks: deep teal #12454C as the main ink, fluoro pink #EE3D87 for accents and motion marks, mint #87BEA6 and warm gold #FFC85C, printed on warm off-white paper #FBF5E9. Slight paper grain; any ink misregistration is a hairline offset at most, never a pink outline or halo around shapes; bold clean flat shapes, no gradients, no shading, no outlines around fills. It should feel like a 1970s safety pictogram redrawn by a modern indie game studio.

Attached image 1 is a style reference from the same illustration set: match its inks, grain, line weight and way of drawing hands and figures exactly, but not its subject.

Scene: a clear instruction diagram of a simple room, drawn perfectly symmetric about one vertical centre line down the middle of the picture. We look from directly behind the player and above them, as if standing a couple of steps behind them on a stepladder, looking down at about 40 degrees. From there, the table and phone beyond the player appear above the player's head and are never hidden by it.

At the top of the picture, centred: a low coffee table drawn in flat teal, seen straight on from its long side. At the exact middle of the table, a smartphone stands upright in portrait orientation in a small stand. Its screen and front camera face straight toward the player, and toward us. The screen shows a tiny three-lane garden path with a small pink-topped runner in the centre lane and a row of small gold coins.

In the lower middle of the picture, on the same centre line: the player, full body visible, seen from directly behind. They stand squarely facing the phone, about two metres from the table, shoulders parallel to the table's edge. The player is a simplified but real human figure (head, neck, shoulders, arms, hands, legs and shoes), gender-neutral, in a pink t-shirt, teal trousers and pink sneakers, with teal head and arms. Not a stick figure, not a capsule or blob person. In this frame they stand upright, weight even, arms relaxed at their sides.

The camera's view: one flat, pale, see-through pink triangle, symmetric about the centre line. Its point is exactly at the phone's front camera, at the top of the screen. From there it opens toward the player, floating in the air between the phone's height and the player's upper body, and it ends at the player. Its wide end is a little wider than the player's shoulders and is hidden behind their upper body; only its two outer corners show, one either side of their shoulders. The triangle stops at the player: it does not continue past them toward us, it does not reach the floor, and it casts no shape on the floor.

The floor is one flat pale-mint rectangle under the table and the player, symmetric about the centre line. A dashed teal line on the floor runs straight along the centre line, from the point directly below the phone (between the table's front legs) to the point between the player's heels, where it ends; it does not continue past their feet toward us. Nothing else is in the room.

Wide 16:10 landscape image. The background around the floor shape is plain flat paper #FBF5E9 all the way to every edge: no border, no frame, no vignette, no rounded corners, no drop shadow. Generous empty paper on both sides of the scene.

Do not include: a bench, pink outlines or halos around shapes, a pink beam or pink shape on the floor, a camera view that continues past the player, the player off the centre line or to one side of the phone, the table seen at an angle, any text, letters, numbers or labels (none on the phone screen either), logos, watermark, signature, app interface elements, photo or photorealism, 3D render, gloss, gradients, blur, depth of field, lens flare, dark background, furniture other than the one table, stick figures, capsule or blob bodies, extra fingers or limbs, cars or any vehicle, city skyline, neon.
```

#### Touch-ups on a `camera_02` you're keeping: select-area edits only

The owner kept the first good `camera_02` (28 Sep) as it came: pink t-shirt, teal trousers,
pink sneakers, wide coffee table.
- A whole-image touch-up of it came back completely redrawn: new table and figure, the t-shirt
  lost in an all-teal silhouette, and heavier pink edges.
- A select-area edit to narrow the table worked, but the owner preferred the wide table.

So on a kept image, fix details only with a **select-area edit** (brush over just the part to
change) or by hand.

**Example: dashed line past the heels** (optional, and left as is in the kept image). Brush over the dashes below the heels and paste:

```text
Replace the selected area with the plain flat mint floor around it. Change nothing else.
```

### `camera_01.png` — the spot to stand on

**Attach**
1. Your approved `camera_02.png`.

No screenshot needed.

**This frame drifts easily.** A tool that redraws instead of editing produces a new table, floor
and wedge, and the loop visibly jumps between 01 and 02. Options, best first:
1. **Select-area edit** (ChatGPT and most image tools have one): brush over the player only, then
   paste the prompt. Nothing outside the selection changes.
2. **By hand in any image editor.** It is all flat colour:
   - fill the figure's area with the floor mint and the wedge pink, sampling both from the image;
   - continue the dashes up to where the heels were;
   - paint two pink footprints.
3. A plain whole-image edit with the prompt below. Then toggle 01 and 02 in an image viewer; if
   anything besides the player moved, use option 1 or 2.

```text
Edit the attached image. Do not redraw or recompose it: the table, phone, pink triangle, floor shape and dashed floor line must stay exactly as they are, in the same size and position. This is the first frame of a simple animation.

Change only this: remove the player entirely. On the centre line, where their heels were, two small solid pink footprints mark the spot to stand on: a pair of shoe-sole shapes side by side, toes pointing at the phone. The dashed teal floor line still runs from under the phone and now ends between the footprints. The pink triangle does not move and keeps its exact width: its two outer corners stay exactly where they were beside the player's shoulders. With nobody in front of it, its wide end now shows as a clean straight edge between those two corners. The phone screen is unchanged.

Scene, unchanged from the attached image: flat risograph-style instructional illustration in deep teal #12454C, fluoro pink #EE3D87, mint #87BEA6 and gold #FFC85C on warm off-white paper #FBF5E9, slight grain, at most a hairline ink offset and no pink outlines around shapes, flat shapes, no gradients. A simple room drawn symmetric about one vertical centre line, seen from directly behind and above the player's spot. A low teal table at the top, seen straight on, with a phone upright in a stand at its exact middle facing the spot. A flat, pale, see-through pink triangle opens from the phone's front camera toward the spot and never touches the floor. A dashed teal line runs along the centre line of a flat pale-mint floor rectangle, from under the phone to the spot. Nothing else is in the room.

Wide 16:10 landscape, plain flat #FBF5E9 paper to every edge, no border, no rounded corners, no drop shadow.

Do not include: any person or figure, a pink beam or pink shape on the floor, any text, letters, numbers or labels (none on the phone screen either), logos, watermark, app interface elements, photorealism, 3D render, gradients, blur, dark background, extra furniture, stick figures, capsule or blob bodies, extra fingers or limbs, vehicles, neon.
```

### `camera_03.png` — step left

**Attach**
1. Your approved `camera_02.png`.

No screenshot needed. This is a **side step, not a lean**. As in the game, the player moves their
whole body over to one side, and the runner follows into that lane. With a select-area edit,
brush over:
- the player;
- the floor and wedge area to their left;
- the phone screen.

```text
Edit the attached image. Keep the style, inks, paper, viewpoint, framing, table, phone, pink triangle, floor shape and dashed floor line exactly as they are, and keep the player's figure and clothes the same; this is the next frame of a simple animation.

Change only this: the player takes one small side step to THEIR LEFT, which is the LEFT side of the picture. Their whole body, head, shoulders, hips and both feet, now stands upright about half a shoulder-width to the left of where it stood in the attached image. This is not a lean or a bend: the body stays straight and vertical, just moved over. The dashed teal centre line is now visible on the floor just to the right of their feet. The head is still within the width of the pink triangle. A short, straight, bold pink arrow on the floor runs from the dashed centre line to their feet, pointing left. Where the player stood before, show what was behind them: the pink triangle's wide end and the mint floor with the dashed line. The pink triangle does not move. On the phone screen the small pink-topped runner has moved to the LEFT lane.

Scene, unchanged from the attached image: flat risograph-style instructional illustration in deep teal #12454C, fluoro pink #EE3D87, mint #87BEA6 and gold #FFC85C on warm off-white paper #FBF5E9, slight grain, at most a hairline ink offset and no pink outlines around shapes, flat shapes, no gradients. A simple room drawn symmetric about one vertical centre line, seen from directly behind and above the player. A low teal table at the top, seen straight on, with a phone upright in a stand at its exact middle facing the player. A flat, pale, see-through pink triangle opens from the phone's front camera toward the player and stops at them, never touching the floor. A dashed teal line runs along the centre line of a flat pale-mint floor rectangle, from under the phone to the player's spot. Nothing else is in the room. The player is a simplified but real human figure, full body, seen from directly behind, with exactly the clothes and colours of the attached image.

Wide 16:10 landscape, plain flat #FBF5E9 paper to every edge, no border, no rounded corners, no drop shadow.

Do not include: a pink beam or pink shape on the floor, a camera view that continues past the player, a lean or bend of the upper body, any text, letters, numbers or labels (none on the phone screen either), logos, watermark, app interface elements, photorealism, 3D render, gradients, blur, dark background, extra furniture, stick figures, capsule or blob bodies, extra fingers or limbs, vehicles, neon.
```

### `camera_04.png` — step right

**Attach**
1. Your approved `camera_02.png`.

No screenshot needed. Shortcut, no generation needed: the scene is symmetric, so flipping
`camera_03` horizontally gives this frame. Check that nothing asymmetric (a hair parting, a logo
on the shirt) gives the flip away.

```text
Edit the attached image. Keep the style, inks, paper, viewpoint, framing, table, phone, pink triangle, floor shape and dashed floor line exactly as they are, and keep the player's figure and clothes the same; this is the next frame of a simple animation.

Change only this: the player takes one small side step to THEIR RIGHT, which is the RIGHT side of the picture. Their whole body, head, shoulders, hips and both feet, now stands upright about half a shoulder-width to the right of where it stood in the attached image. This is not a lean or a bend: the body stays straight and vertical, just moved over. The dashed teal centre line is now visible on the floor just to the left of their feet. The head is still within the width of the pink triangle. A short, straight, bold pink arrow on the floor runs from the dashed centre line to their feet, pointing right. Where the player stood before, show what was behind them: the pink triangle's wide end and the mint floor with the dashed line. The pink triangle does not move. On the phone screen the small pink-topped runner has moved to the RIGHT lane.

Scene, unchanged from the attached image: flat risograph-style instructional illustration in deep teal #12454C, fluoro pink #EE3D87, mint #87BEA6 and gold #FFC85C on warm off-white paper #FBF5E9, slight grain, at most a hairline ink offset and no pink outlines around shapes, flat shapes, no gradients. A simple room drawn symmetric about one vertical centre line, seen from directly behind and above the player. A low teal table at the top, seen straight on, with a phone upright in a stand at its exact middle facing the player. A flat, pale, see-through pink triangle opens from the phone's front camera toward the player and stops at them, never touching the floor. A dashed teal line runs along the centre line of a flat pale-mint floor rectangle, from under the phone to the player's spot. Nothing else is in the room. The player is a simplified but real human figure, full body, seen from directly behind, with exactly the clothes and colours of the attached image.

Wide 16:10 landscape, plain flat #FBF5E9 paper to every edge, no border, no rounded corners, no drop shadow.

Do not include: a pink beam or pink shape on the floor, a camera view that continues past the player, a lean or bend of the upper body, any text, letters, numbers or labels (none on the phone screen either), logos, watermark, app interface elements, photorealism, 3D render, gradients, blur, dark background, extra furniture, stick figures, capsule or blob bodies, extra fingers or limbs, vehicles, neon.
```

### `camera_05.png` — hop to jump

**Attach**
1. Your approved `camera_02.png`.

No screenshot needed. With a select-area edit, brush over the player plus the space just above
their head and just under their feet, and over the phone screen. The game detects a jump by the
**head moving up**, so the whole figure must rise, head included. Toggle against `camera_02`:
the head should sit visibly higher.

```text
Edit the attached image. Keep the style, inks, paper, viewpoint, framing, table, phone, pink triangle, floor shape and dashed floor line exactly as they are, and keep the player's figure and clothes the same; this is the next frame of a simple animation.

Change only this: the player hops straight up from the same spot two metres from the table. Their whole body, head included, rises by about half a head's height compared with the attached image, so the head sits visibly higher than before; it stays on the centre line and within the width of the pink triangle. Both feet are a little off the floor, knees slightly bent, arms lifted a little. A small flat shadow stays on the floor exactly where the feet stood, so the gap between the sneakers and the shadow shows the hop. Three short pink motion lines under the feet. The pink triangle does not move. On the phone screen the small pink-topped runner is in mid-air above the centre lane.

Scene, unchanged from the attached image: flat risograph-style instructional illustration in deep teal #12454C, fluoro pink #EE3D87, mint #87BEA6 and gold #FFC85C on warm off-white paper #FBF5E9, slight grain, at most a hairline ink offset and no pink outlines around shapes, flat shapes, no gradients. A simple room drawn symmetric about one vertical centre line, seen from directly behind and above the player. A low teal table at the top, seen straight on, with a phone upright in a stand at its exact middle facing the player. A flat, pale, see-through pink triangle opens from the phone's front camera toward the player and stops at them, never touching the floor. A dashed teal line runs along the centre line of a flat pale-mint floor rectangle, from under the phone to the player's spot. Nothing else is in the room. The player is a simplified but real human figure, full body, seen from directly behind, with exactly the clothes and colours of the attached image.

Wide 16:10 landscape, plain flat #FBF5E9 paper to every edge, no border, no rounded corners, no drop shadow.

Do not include: a pink beam or pink shape on the floor, a camera view that continues past the player, a lean or bend of the upper body, any text, letters, numbers or labels (none on the phone screen either), logos, watermark, app interface elements, photorealism, 3D render, gradients, blur, dark background, extra furniture, stick figures, capsule or blob bodies, extra fingers or limbs, vehicles, neon.
```

## 5. DURING A RUN — `run_01`

### `run_01.png` — a run in the park

**Attach**
1. Your approved `tilt_01.png` (style reference).
2. A gameplay screenshot. This is the image where it matters most. It should show:
   - a run in the current park build, portrait;
   - the runner in the **centre lane**;
   - a straight row of gold coins ahead in that lane;
   - a **coral planter** block in one side lane further ahead;
   - ideally a teal **pergola** arch in the distance;
   - the **pause button** (the small "II" square, bottom-left) on screen.

   Crop off the top score strip, and the phone status bar if it's there. The model redraws this as
   a wide 16:10 picture, so it doesn't matter that the screenshot is portrait.

```text
Flat risograph-style instructional illustration. Inks: deep teal #12454C as the main ink, fluoro pink #EE3D87 for accents, mint #87BEA6, warm gold #FFC85C and a little coral #D86755, printed on warm off-white paper #FBF5E9. Slight paper grain; any ink misregistration is a hairline offset at most, never a pink outline or halo around shapes; bold clean flat shapes, no gradients, no shading, no outlines around fills. It should feel like a 1970s safety pictogram redrawn by a modern indie game studio.

Attached image 1 is a style reference from the same illustration set: match its inks, grain, line weight and figure style exactly, but not its subject. Attached image 2 is a screenshot of the game: use it only for what is on the path and where. Recompose it as a wide picture and redraw it flat; do not copy its 3D rendering and do not copy any text.

Scene: a wide view of the game's garden path from behind and a little above the runner, like the game's own camera. A straight path of three lanes recedes to the horizon: centre lane paper-white, the two side lanes pale warm stone #E9DFCB, thin stone-coloured #C6B69B edges. It runs through a flat sculpture garden: mint grass banks, a few round teal trees, and one teal pergola arch spanning the path in the distance. The runner, a simplified but real human figure seen from behind (pink top, teal trousers, sneakers, mid-stride), is in the centre lane in the lower middle of the picture. Ahead of it in the same lane, a straight row of five small gold #FFC85C coins, evenly spaced; the nearest coin is being collected, with a small pink sparkle burst. Further ahead in the right lane, one coral #D86755 planter block with a teal base.

The whole scene sits as a vignette on the paper: the path and banks stop in clean straight edges at the bottom and sides, with plain paper around them. At the vignette's bottom-left corner, a small paper-white square containing two short vertical teal bars (a pause symbol). This is the only interface element.

Wide 16:10 landscape image. Plain flat paper #FBF5E9 at every edge of the image: no border, no frame, no rounded corners, no drop shadow.

Do not include: any text, letters, numbers or labels, score or coin counters, any interface element other than the one pause symbol, logos, watermark, signature, photo or photorealism, 3D render, gloss, gradients, blur, depth of field, lens flare, dark background, stick figures, capsule or blob bodies, extra fingers or limbs, cars or any vehicle, motor-racing track, city skyline, neon.
```

## 6. Extras — hands-free pause (not loaded by the game yet)

These are for a later pause-screen or guide page. Generate them now while the camera set is
fresh. Keep them **outside** `Resources/Art/Guide/` until a page loads the `pause` key.

### `pause_01.png` — step out of frame to pause

**Attach**
1. Your approved `camera_02.png`.

```text
Edit the attached image. Keep the style, inks, paper, viewpoint, framing, table, phone, pink triangle, floor shape, dashed floor line and the player's figure exactly as they are.

Change only this: the player has stepped sideways to the right, clearly outside the width of the pink triangle: at least half their body is past its right corner, still standing and facing the phone. The pink triangle does not move; with nobody in front of it, its wide end now shows as a clean straight edge in the air above the empty spot. The phone's screen no longer shows the path: it shows two short vertical pink bars on paper-white (a pause symbol).

Scene, unchanged from the attached image: flat risograph-style instructional illustration in deep teal #12454C, fluoro pink #EE3D87, mint #87BEA6 and gold #FFC85C on warm off-white paper #FBF5E9, slight grain, at most a hairline ink offset and no pink outlines around shapes, flat shapes, no gradients. A simple room drawn symmetric about one vertical centre line, seen from directly behind and above the player. A low teal table at the top, seen straight on, with a phone upright in a stand at its exact middle facing the player's spot. A flat, pale, see-through pink triangle opens from the phone's front camera toward the spot and stops there, never touching the floor. A dashed teal line runs along the centre line of a flat pale-mint floor rectangle, from under the phone to the spot. Nothing else is in the room. The player is a simplified but real human figure, full body, seen from directly behind, with exactly the clothes and colours of the attached image.

Wide 16:10 landscape, plain flat #FBF5E9 paper to every edge, no border, no rounded corners, no drop shadow.

Do not include: a pink beam or pink shape on the floor, any text, letters, numbers or labels, logos, watermark, app interface elements, photorealism, 3D render, gradients, blur, dark background, extra furniture, stick figures, capsule or blob bodies, extra fingers or limbs, vehicles, neon.
```

### `pause_02.png` — raise your right hand to resume

**Attach**
1. Your approved `camera_02.png`.

```text
Edit the attached image. Keep the style, inks, paper, viewpoint, framing, table, phone, pink triangle, floor shape, dashed floor line and the player's figure and position exactly as they are.

Change only this: the player stands on the spot, on the centre line two metres from the table, and raises their RIGHT arm straight up (the arm on the RIGHT side of the picture), open palm facing the phone; a small pink ring circles the raised hand. The left arm stays relaxed. The pink triangle does not move. The phone's screen shows two short vertical pink bars on paper-white (a pause symbol).

Scene, unchanged from the attached image: flat risograph-style instructional illustration in deep teal #12454C, fluoro pink #EE3D87, mint #87BEA6 and gold #FFC85C on warm off-white paper #FBF5E9, slight grain, at most a hairline ink offset and no pink outlines around shapes, flat shapes, no gradients. A simple room drawn symmetric about one vertical centre line, seen from directly behind and above the player. A low teal table at the top, seen straight on, with a phone upright in a stand at its exact middle facing the player's spot. A flat, pale, see-through pink triangle opens from the phone's front camera toward the spot and stops there, never touching the floor. A dashed teal line runs along the centre line of a flat pale-mint floor rectangle, from under the phone to the spot. Nothing else is in the room. The player is a simplified but real human figure, full body, seen from directly behind, with exactly the clothes and colours of the attached image.

Wide 16:10 landscape, plain flat #FBF5E9 paper to every edge, no border, no rounded corners, no drop shadow.

Do not include: a pink beam or pink shape on the floor, any text, letters, numbers or labels, logos, watermark, app interface elements, photorealism, 3D render, gradients, blur, dark background, extra furniture, stick figures, capsule or blob bodies, extra fingers or limbs, vehicles, neon.
```

## 7. Video / GIF alternative

Stills are recommended:
- Unity cannot play GIFs.
- A video adds APK size and a store-review surface.
- Frame sequences localise for free.

If you still want a clip for the website or the Devpost video, reuse a sequence's anchor prompt
with a video model (Veo, Runway, Kling), describing the sequence as one continuous shot: "static
camera, 4 seconds, loop, the figure returns to the neutral pose at the end". Then export stills at
the poses above and name them as in §1.

## 8. Checks for replacement art

- [ ] All frames of one key share camera, scale, palette and background. Cycle them in an image
  viewer: nothing but the described change should move.
- [ ] Paper is exactly `#FBF5E9` at the edges. Sample it: models drift warm or grey.
- [ ] No text or letters anywhere, including on the phone screen.
- [ ] Coins are gold, not pink. In the camera frames:
  - the phone, the floor line and the player's spot sit on one centre line;
  - the pink wedge stops at the player and never touches the floor;
  - after a side step, the whole body is upright and moved over, not leaning, and the head
    stays within the wedge's width.
- [ ] Figures have hands and feet; not a capsule (owner veto, `site/README.md` "Placeholder
  figures").
- [x] Files named exactly `<key>_NN.png`, approximately 16:10 and 1024–1600 px wide.
  All supplied frames are 1586×992; the original source-PNG size target is superseded above.
- [x] `FrameCount` in `GuideState.Pages` matches the number of files present (tilt 4, camera 5,
  run 1).
- [x] Re-run Veyro → UI → Render how-to-play pages (`GuideUiReview`) and look at the result.
  28 Sep: all ten sprites loaded at 1024×640; four pages plus ten frame previews rendered.
- [x] If AI-generated: note it in `docs/STATUS.md`. The Play Console AI-asset declaration covers
  listing assets only, but keep the record accurate in case in-app art is asked about.
