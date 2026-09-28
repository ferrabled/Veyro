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
- Guide frames change only when a visitor chooses a step; no autoplay, timers or
  cross-fades. Reduced motion disables smooth scrolling and button transitions.
- Cosmetics use honest product labels; approved skin-specific web artwork is a
  future optional addition, not replaced with fabricated renderings.

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
- `public/site.js`: UTC ticket date and manual guide-frame selection. The date
  refreshes after midnight UTC. It does not invent or claim to compute a game seed.
- `public/art/`: optimized derivatives of all ten approved guide PNGs.
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
Hero `tilt_02` and Daily Run `run_01` also have 1586×992 versions for responsive
`srcset`. Quality 84, Lanczos downsampling, WebP method 6; no crop or colour edits.
The twelve WebPs total **274,462 bytes**. Only images that are used are requested;
additional guide frames load on interaction. Explicit dimensions reserve layout
space, and a failed frame load leaves the preceding image and selection intact.
The source illustration set was supplied by the owner and documented as AI-generated;
this website pass creates no new AI art.

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
