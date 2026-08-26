# Veyro Run — marketing site

Static site for **veyro.ferrabled.com**. No framework, no build step: plain HTML/CSS/JS served as
Cloudflare Workers static assets (free tier, unmetered for static requests).

Design: "Riso Print" — two-ink risograph poster (teal `#12454C` + fluoro pink `#EE3D87` on paper
`#FBF5E9`). Direction pitch and rationale: https://claude.ai/code/artifact/c0ca2d3c-4582-4968-8592-8b2a9cfb46be

## Layout

```
site/
  wrangler.jsonc          deploy config (assets-only Worker + custom domain)
  public/                 everything in here ships verbatim
    index.html            homepage — built from independent <section> blocks
    styles.css            design tokens + one CSS block per section
    site.js               daily-ticket generator (page works without JS)
    privacy/index.html    REQUIRED by Google Play; path /privacy/ is load-bearing
    terms/index.html      terms of use (cosmetic IAPs, safety notice)
    support/index.html    contact, FAQ, data-deletion statement
    404.html              served for unknown paths (see wrangler not_found_handling)
    favicon.svg  robots.txt  _headers
```

## Deploy

```
cd site
npx wrangler login      # once, opens browser
npx wrangler deploy
```

First deploy registers `veyro.ferrabled.com` automatically (the zone must be in the same
Cloudflare account). A `*.workers.dev` URL also works for previewing before DNS.

Local preview (no Cloudflare needed): `npx wrangler dev` from `site/`, or any static file server
pointed at `site/public/`.

## How to modify things (the modular contract)

- **Homepage sections are independent.** Each `<section>` in `index.html` has a banner comment;
  each has a matching CSS block in `styles.css` (same name). To remove a section, delete both.
  To reorder, move the HTML — no section depends on another.
- **Design tokens** live at the top of `styles.css`. The two-ink rule: anything that must be
  *read* is teal ink; pink is display/accent only (contrast: pink passes only as large text).
- **Header/footer are duplicated** into every page (marked `@partial` in comments). Changing
  them means changing all five HTML files — grep for `@partial`.
- **Fonts**: Anybody (titles/headings via variable width axis), Atkinson Hyperlegible (body),
  Fragment Mono (data/labels). Loaded from Google Fonts; self-host later if wanted.

## Placeholder figures (owner-flagged — replace when the real model exists)

The capsule-person figures are **stopgaps the owner explicitly dislikes** (25 Aug). Once the
game has its main character model and skins, replicate *that* character here instead —
keeping the riso treatment (teal ink figure + offset pink misregistration ghost). Tracked as
**T-037** in `docs/BACKLOG.md`. Every spot to touch:

| Where | What |
| --- | --- |
| `index.html` — SECTION: hero, `.hero-stage .fig` | big leaning figure |
| `index.html` — SECTION: how, `.panel .fig` × 3 | lean / hop / crouch poses |
| `favicon.svg` | mini leaning figure |

Any technique works (SVG traced from renders, or actual renders duotoned to the two inks), as
long as the two-ink rule holds and each pose still reads at a glance.

## Launch-day switches

1. **Hero CTA** (`index.html`, SECTION: hero): swap the pre-launch "join the closed test" block
   for the commented "Get it on Google Play" block once the listing is live.
2. **Spec strip numbers** are real values from the game — update only when the game's change.
3. **Privacy policy**: source of truth is `docs/PRIVACY_POLICY.md`; `/privacy/` here must match
   it. Both MUST be updated before RevenueCat purchases / OneSignal / Layers ship (they start
   real data collection). The in-game link is `GameLinks.PrivacyPolicyUrl`.
4. **Contact email** `ferrabled+veyro@gmail.com` is a temporary alias — when the real one
   exists, grep the whole `site/` + `docs/PRIVACY_POLICY.md` for it.

## Copy rules (binding — docs/store-kit/ART_DIRECTION.md)

Nothing automotive, nothing racing: no cars, wheels, flags, road imagery. Banned words:
race, speed, drive, circuit, grand prix, turbo. Camera mode always carries the BETA label and
the on-device privacy line. Monetization copy is always "cosmetics only".
