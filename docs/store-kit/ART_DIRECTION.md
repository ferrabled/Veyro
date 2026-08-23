# T-030 — icon and feature graphic: art direction brief

Written 23 Aug 2026. **Brief only — no assets produced.** For whoever draws these (owner, a
commissioned artist, or an image model with the owner's review).

This brief also feeds T-006 (first biome art pass), because the icon and the game must look like the
same product. If T-006 lands a style first, the icon follows it rather than the other way round.

---

## 0. The hard constraint, first

**Nothing automotive. Nothing racing.**

VEYRON is a live Bugatti/VW trademark one letter away from Veyro (OPEN_QUESTIONS, "Answered",
21 Aug). A confusion argument gets traction where the name sits next to car imagery — so the brand
must never be seen beside any of:

> cars · wheels · tyres · steering wheels · speedometers · rev counters · chequered flags · podiums ·
> racing stripes · pit-lane or circuit iconography · "GT"/"RS"/"Turbo"-style badging · asphalt with
> lane markings that read as a road for vehicles

That last one needs care: the game's track *is* a three-lane strip. Keep it reading as a **path,
walkway or ribbon** — no dashed centre lines, no kerbs, no road signage. If a viewer could caption
the icon "car game", it is the wrong icon regardless of how good it looks.

Positive framing: this is a game about **a body moving**, not a vehicle. Every art decision should
push toward human motion.

---

## 1. App icon

**Specs** [G1]: 512 × 512 px, **32-bit PNG with alpha**, ≤ 1024 KB, for Play.
**1024 × 1024** is separately required by Devpost [D1] — produce at 1024 and downscale, and keep the
source vector/layered file.

Play applies its own rounded-corner mask and shadow. Design on a full-bleed square, keep everything
meaningful inside a **centre circle of ~410/512 diameter**, and add no baked-in rounded corners,
border or drop shadow.

**Concept A — the lean (recommended).** A single stylized runner figure, silhouetted, caught
mid-lean: body tilted 15–20° off vertical, arms out, one foot off the ground. Flat two-tone
silhouette on a bold background. The lean *is* the product — it is what tilting the phone does and
what leaning in camera mode does — and it is legible at 48 px.

**Concept B — the tilted ribbon.** The three-lane path receding to a vanishing point, but the whole
composition rotated ~12°, with a small figure on it. Communicates "endless runner" faster than A;
communicates "motion control" slower; and the receding-lanes composition is the one most at risk of
reading as a road. Second choice.

**Concept C — the chevron.** Pure abstraction: a bold arrow/chevron made of three offset bars,
leaning. Scales beautifully, says nothing about it being a game. Only if A and B both fail.

**Rules for whichever wins:**

- One focal element. No text — the app name is rendered under the icon by the OS.
- Two to three colours plus one accent. High contrast, because it must survive both light and dark
  Play themes and a cluttered home screen.
- The accent is **`#FF8C26`** — the orange already used for the runner and the buttons in-game
  (`GameBootstrap.cs:44` and `RunHud`'s `ButtonColor`, both `(1, 0.55, 0.15)`). Using the in-game
  colour makes the icon and the first screenshot look like the same product for free.
- Suggested background: the in-game deep blue-black `#171A29` (the camera clear colour,
  `GameBootstrap.cs:30`) or a saturated step up from it. Dark background + orange figure is a strong,
  cheap, non-automotive identity.
- **Test at 48 px before committing.** Shrink it, look at it on a phone home screen, and if the
  figure turns to mush, simplify until it does not.

---

## 2. Feature graphic

**Specs** [G1]: 1024 × 500 px, **JPEG or 24-bit PNG, no alpha**.

This is the banner at the top of the store page, and Play may crop or overlay it — so:

- **Keep the middle ~60% clear of anything critical.** Play can overlay a play button and can crop
  the edges on some surfaces.
- **No text that repeats the app name.** The name is rendered next to it.

**Composition:** the runner figure from the icon, small, on the left third, on the ribbon path
receding right; wide empty space in the centre-right; the same dark background with the orange
accent. Motion streaks or a soft speed gradient behind the figure are fine — *streaks, not
speedometers*.

**Once camera mode ships**, the strongest possible feature graphic is a **photograph**: a person
standing in a living room, arms out, leaning, with the phone propped on a table in the foreground
showing the game. That image sells the whole project in one frame and no competitor can copy it
cheaply. It is also the video's opening shot (T-035). Hold it until the feature is live —
LISTING.md §4's rule applies to imagery as much as to words.

---

## 3. Style frames for T-006 (owner picks; OPEN_QUESTIONS 4)

The backlog still has "art direction" open. Three coherent directions, all cheap for a solo dev, all
clear of the automotive constraint:

| | Direction | What it means | Cost |
|---|---|---|---|
| **1** | **Flat low-poly, dusk palette** | Untextured low-poly geometry, flat vertex colours, one warm key light against a cool sky. Deep blue-black ground, orange runner — the palette already on screen. | Lowest. It is essentially what the prototype renders now, made deliberate. |
| **2** | **Neon grid at night** | Dark world, emissive edge-lit geometry, bloom, magenta/cyan accents. Reads instantly as "arcade" and photographs well in short-form video (Noise category). | Medium — needs URP bloom tuning and emissive materials, and it fights the 60 FPS budget on a mid-tier phone. |
| **3** | **Paper / cut-out** | Flat matte shapes, visible paper grain, soft ambient occlusion, warm daylight palette. Distinctive, gentle, memorable; the least common look in the endless-runner category. | Medium — needs a texture pass; hardest to keep coherent as content grows. |

**Recommended default: direction 1.** It is closest to what already runs on the device, it hits the
performance budget by construction, and the deadline outranks completeness (working protocol 6).
Direction 3 is the one most likely to win the Design category if there is time — it is a genuine
"nobody else looks like this" play. Direction 2 is the trap: it looks best in a 10-second clip and
costs the most frames.

Whichever is chosen, **the icon and the game must share a palette.** Pick the direction first, then
draw the icon.

---

## 4. Attribution note

If a credits screen is ever added, Unity's terms require:
*"Veyro Run was made with Unity®. Unity is a trademark or registered trademark of Unity
Technologies"* plus the copyright line (`docs/LICENSING_REVENUE.md` §3). No credits screen, no
obligation — but if T-006 adds one, this line ships with it.

---

## Sources

- **[G1]** Play Console Help — graphic asset specs — https://support.google.com/googleplay/android-developer/answer/9866151
- **[D1]** Shipaton 2026 rules — 1024×1024 icon — https://revenuecat-shipaton-2026.devpost.com/rules
