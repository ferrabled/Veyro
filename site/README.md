# Veyro Run — marketing site

Static HTML/CSS/JS for **veyro.ferrabled.com**, served as Cloudflare Workers static
assets. No framework, build step, analytics or new runtime dependencies.

## Visual direction

The 28 September refresh follows the approved how-to-play illustrations and current
park/menu palettes. Warm paper artwork sits inside rounded cards on sage; teal is
the reading colour, with pink motion marks, mint vegetation and gold rewards.
The older two-ink-only rule and capsule figures are retired. No text shadows,
page-wide grain, glossy effects or invented runner drawings.

- **Anybody** for expressive headings; **Atkinson Hyperlegible** for body copy;
  **Fragment Mono** sparingly for labels and the Daily Run. Existing Google Fonts
  loading is preserved.
- Named tokens in `public/styles.css` cover the owner's core, supporting and UI
  colours. Reading text uses `--ink` or `--secondary`; pink/gold are accents.
- Artwork is never cropped, stretched, recoloured or used behind reading text.
- The how-to-play guide cards change only when a visitor chooses a step.
- The hero starts on Camera Mode (BETA). Two native pill buttons select the mode;
  only the selected mode rotates through four approved frames every three seconds:
  camera standing/left/right/hop, or phone hold/left/right/tap. Captions and image
  descriptions change with the artwork. Frames switch after decoding, without
  cross-fades, duplicate silhouettes or layout shifts.
- The hero has Pause/Play and Next controls. Next pauses the sequence; changing
  modes starts that mode at its first frame and preserves the paused state.
  Hovering, an offscreen card or a hidden tab suspend rotation. Keyboard focus on
  mode/Next controls stops it until Play is activated. Automatic changes are not
  live announcements. Both scenes and all captions reserve their layout space;
  inactive content is hidden from assistive technology.
- Reduced motion starts the hero paused; visitors can step manually or explicitly
  press Play. Changing the motion preference stops playback. Reduced motion also
  disables smooth scrolling and button transitions. Without JavaScript, the camera
  still and caption remain visible and enhancement controls stay hidden.
- Cosmetics show the existing in-game Ember, Frost and Prism character thumbnails,
  with appearance-only labels. Prism is identified as a level-10 pass reward.
- The hero headline scales with its container and wraps only between words;
  "controller" stays intact at narrow widths and enlarged text settings.

Primary references (read-only): `docs/GUIDE_ILLUSTRATIONS.md`, `screenshots/guide/`,
`ParkTheme.cs`, `MenuTheme.cs`, `docs/GAME_ART.md`, and the approved ticket/V icon in
`screenshots/icon.png`. For this refresh they were read from the active
`feat-iOS-implementation` reference worktree; every edit stays under `site/` in
`feat/update-website-ui`.

## Layout

- `public/index.html`: hero, two control modes, Daily Run, cosmetics, camera privacy,
  developer contact.
- `public/styles.css`: named tokens, shared components, section-specific rules,
  supporting-page styles, responsive and reduced-motion rules.
- `public/site.js`: hero rotation/playback controls, manual mode/guide selection and
  UTC ticket date. The date refreshes after midnight UTC. It does not invent or
  claim to compute a game seed.
- `public/art/`: optimized derivatives of all ten approved guide PNGs plus three
  existing in-game character thumbnails.
- `public/favicon.svg`, `favicon-32.png`, `favicon-64.png`, `apple-touch-icon.png`:
  resized approved V/ticket identity (the SVG embeds the 64px PNG).
- `public/challenge/`: existing shared-run landing page and unmodified deep-link JS.
- `public/privacy/`, `terms/`, `support/`, `404.html`: existing routes with shared
  visual chrome. The support deletion anchor `#delete` is preserved.
- `public/_headers`, `robots.txt`, `wrangler.jsonc`: production configuration,
  unchanged by this refresh.
- `tools/prepare_assets.py`: repeatable image conversion (authoring only; Pillow).
- `tools/preview.py`: standard-library local server that applies `_headers` and
  serves the custom 404 with a 404 status.
- `art-manifest.json`: derivative dimensions, byte sizes, source paths and hashes.
- `STATUS.md`: website-only task claim, acceptance and verification record.

Header/footer markup is duplicated across all **six** HTML pages. Keep them in
sync. Each main content region has a skip-link target. Navigation stays available
on phones without requiring JavaScript.

## Local preview

From the repository root:

```sh
python site/tools/preview.py
```

Open **http://127.0.0.1:8765**. Use `--port` to choose a different explicit port.
This server is loopback-only, reads only `public/`, applies the current CSP and
other production headers, and disables cache for local iteration. Cloudflare's
own preview remains available with `npx wrangler dev` from `site/`.
Temporary browser QA tooling and screenshots belong in ignored `site/.preview/`.

## Artwork derivatives

Original PNGs and game runtime copies are never edited. To regenerate, with
Pillow installed:

```sh
python site/tools/prepare_assets.py PATH_TO_REFERENCE_REPO
```

All ten frames have a 793×496 WebP derivative (exactly half the original dimensions).
All eight hero frames (`camera_02`–`camera_05`, `tilt_01`–`tilt_04`) and Daily Run
`run_01` also have 1586×992 versions for responsive `srcset`. Quality 84, Lanczos
downsampling, WebP method 6; no crop or colour edits. The nineteen guide WebPs total
**592,976 bytes**. Additional frames load as the selected sequence advances or on
manual interaction; the entire sequence is not preloaded. Explicit dimensions
reserve layout space. A failed hero frame leaves the preceding image, caption and
selection intact and pauses rotation; Play or Next retries.
The source illustration set was supplied by the owner and documented as AI-generated;
this website pass creates no new AI art.

The three character previews are lossless 512×512 WebPs with their original alpha,
converted from `game/Assets/Resources/Art/CosmeticThumbnails/{ember,frost,prism}.png`.
Their combined size is **93,230 bytes**; all twenty-two WebPs total **686,206 bytes**.
No Unity rendering or asset changes are needed. Ember/Frost originate from the
project's CC0 KayKit Adventurers models; Prism uses the existing runner/reward render.
The game retains model/license records; `art-manifest.json` records the exact PNG
source hashes. Web previews preserve the existing poses, colours and proportions.

## Content and route contracts

- **Tilt & Touch**: tilt to steer, tap to jump.
- **Camera Mode (BETA)**: move sideways to steer, hop to jump. On supported phones,
  optional and processed on-device. Do not advertise sliding: current guide
  documentation confirms it isn't implemented.
- Camera frames stay on-device. The app itself uses network services for cosmetics,
  shared scores, enabled notifications and opt-in analytics. Never broaden camera
  privacy into a whole-app no-network claim.
- Optional purchases are appearance-only; there is no gameplay advantage. The
  Ember/Frost/Season 1 names come from the existing cosmetics catalog. No web
  purchase controls or speculative pricing are added.
- Daily tracks match within a content version/world. Shared seeds allow replay;
  don't claim yesterday's track is gone forever.
- `/challenge/?s=&v=&w=&p=&m=&d=` is a published contract. Keep existing parameter
  names, unknown parameters and repeated-key semantics. The unchanged JS uses
  `URLSearchParams`, passes its serialization to `veyro://challenge`, and wraps it
  in an Android intent with the existing Google Play fallback. No automatic
  launch occurs on page load. Test actual OS handoff on a phone separately.
- `/privacy/` is load-bearing for Google Play and the game. Its body remains in
  sync with `docs/PRIVACY_POLICY.md`; this pass changes its presentation only.
- Keep support contact `ferrabled+veyro@gmail.com` until the owner replaces it.
- No automotive imagery or racing language. Paths are garden walkways.

## Availability and deployment

Public release was **not confirmed on 28 September 2026**: repo decision D19 says
Play production review is pending; a direct public Play URL check was unavailable
and public search returned no listing. These results are not proof of absence.
The homepage keeps the existing **join the closed test** email destination, with
no stale September release promise. The challenge page keeps its existing Play
URL and fallback, using availability wording. No App Store URL or released-iOS
claim was added.

**29 September 2026 (store-review pass):** the iPhone build 1.0.0 (1) was uploaded and
processed in App Store Connect and is being submitted to App Review; it is **not publicly
available**. The privacy policy, support page and terms now cover the iPhone version in
version-scoped "On iPhone…" clauses, because App Review reads the policy linked from App
Store Connect before approval. The footer reads "Veyro Run" with the Apple and Google
trademark lines, and the terms say "for iPhone from its first App Store release". The home
page's CTA note reads **"For iPhone and Android"** (owner decision, 29 Sep): App Store Connect
lists this site as the app's marketing URL, and App Review flags metadata that names only
another mobile platform (guideline 2.3.10). It names the platforms the game is built for; it
links no store. Add an App Store URL or badge only after the listing is live (same rule as
the Play CTA below).

Once the owner verifies a public listing, update the homepage CTA using the real
store URL. Do not infer availability from a registered bundle ID or an internal
build. Coordinate policy publication with release owners as already required;
shop-enabled builds contact RevenueCat at launch and resume, not only at purchase.

Production deployment is outside the refresh task. Existing owner deployment:

```sh
cd site
npx wrangler login
npx wrangler deploy
```

The Workers configuration, custom domain, CSP and challenge fallback are preserved.
