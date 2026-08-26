# Play Store listing drafts (T-030)

Default listing language: en-US (matches the release notes language already used).
Character limits: name 30 · short 80 · full 4000.

## App name (30)

Veyro Run

## Short description (80)

Motion-controlled endless runner. Lean, hop & crouch on camera — or tilt.

## Full description (4000)

Run by actually moving.

Veyro Run is an endless runner you control with your body. Prop your phone up,
step back, and play hands-free: lean to steer, hop to jump, crouch to slide.
The camera never records you — every frame is processed on your phone, in
memory, and instantly discarded. Nothing is stored, nothing is sent anywhere.

Prefer the classic way? Tilt & touch mode is the full game: tilt to steer,
tap to jump.

ONE TRACK. EVERYONE. EVERY DAY.
Each day the game builds one endless track from the date itself — the same
track on every phone on Earth, no server involved. Run it once or chase your
best all day; at midnight UTC it's gone forever.

WHAT VEYRO RUN IS NOT
• No ads
• No accounts or sign-in
• No data collection — the game makes zero network requests
• No internet needed — fully playable offline

Camera mode is in beta: it needs a reasonably lit room and about 1–2 meters
of space, and it hands you back to tilt & touch any time it can't see you.

Support: https://veyro.ferrabled.com/support/
Privacy: https://veyro.ferrabled.com/privacy/

<!-- Do NOT mention purchases until the RevenueCat release ships them; add a
     cosmetics paragraph + flip the IAP flag, Data safety, and content-rating
     purchases answer in that same release. -->

## Graphic assets (required before the listing saves)

| Asset | Spec | Status |
|---|---|---|
| App icon | 512×512 PNG (32-bit), ≤1 MB, no rounded corners (Play adds them) | ✅ AI-generated 25 Aug, uploaded to Console, **tagged AI** in the listing's AI-asset declaration. Not in repo — owner: drop a copy in `store-assets/` for the record |
| Feature graphic | exactly 1024×500, JPEG or 24-bit PNG, no alpha | ✅ AI-generated 25 Aug, uploaded to Console, **tagged AI** (same declaration). Copy to `store-assets/` too |
| Phone screenshots | 2–8, portrait 9:16, each side 320–3840 px (device screencaps OK) | ☐ missing |
| Devpost extra | 1179×2556 frameless screenshot (submission kit, not Play) | ☐ missing |

Brand: use the site's riso print system — teal #12454C + fluoro pink #EE3D87
on paper #FBF5E9, Anybody type. No capsule-person figures (owner veto, see
site/README.md "Placeholder figures").

Execution handoffs (25 Aug, owner-approved subjects — icon = daily ticket + V
monogram; human-figure art postponed until the real character model exists):
`PROMPT_ICON.md` and `PROMPT_FEATURE_GRAPHIC.md` in this folder, each with a
coding-agent version and an image-model version. Rendered PNGs land in
`store-assets/` (binaries stay out of docs/store-kit/), SVG sources in
`docs/store-kit/src/`.
