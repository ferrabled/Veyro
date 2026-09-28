# Website work log

## 2026-09-28 — T-037 website refresh (feat/update-website-ui)

Claimed by the website session. Owner's current brief supersedes T-037's original narrow
two-ink/three-figure acceptance criteria: use approved guide illustrations, two accurate
control modes, game palette, refreshed Daily Run/cosmetics/chrome, approved icon derivatives,
responsive QA and local preview. All changes stay under `site/`; the shared backlog,
game assets and active iOS worktree are read-only for this task.

**Implemented; local preview ready for owner visual review.** Production deployment
is out of scope and was not performed.

### What changed

- Approved guide artwork replaces every capsule illustration. All ten source frames
  have optimized, uncropped web derivatives; the hero and garden still also have
  high-resolution responsive sources. Twelve WebPs total 274,462 bytes. No new AI art.
- Named park/menu colour tokens, sage page, rounded paper cards, clear teal type,
  pill controls, responsive navigation/footer and accessible skip links. Retained
  Anybody, Atkinson Hyperlegible and Fragment Mono; removed heavy text shadows and
  texture over text. Favicon/touch icons derive from the approved ticket/V icon.
- Separate Tilt & Touch and Camera Mode (BETA) cards with manual step selection.
  Nine frames work with native buttons and keyboard. No autoplay or animation;
  reduced motion disables smooth scrolling/transitions. Static imagery/copy works
  without JavaScript. Reserved image dimensions prevent frame-induced shifts.
- Daily Run becomes a garden ticket with the current UTC date and midnight refresh.
  Removed fabricated seed/chunk details and the incorrect forever-expired-track claim.
- Cosmetics have catalog-backed Ember/Frost/Season 1 cards and appearance-only copy.
  Skin-specific illustrations are an optional remaining asset opportunity; none were
  invented. Camera privacy is explicitly on-device without claiming a network-free app.
- Removed crouch/slide claims from home, challenge and terms. Privacy policy body is
  unchanged. Existing support, terms, privacy, challenge and 404 routes remain.
- Homepage keeps the existing closed-test email CTA: public Play availability was
  not confirmed. Challenge keeps its existing store destination/fallback with
  availability wording. No App Store link or claim of released iOS support.
- Website README, provenance manifest and repeatable asset/preview scripts added.
  Shared game/process docs, Unity files, iOS worktree, Cloudflare configuration,
  production headers, robots and challenge JavaScript were left untouched.

### Verification

- Headless installed Chrome/Playwright against the local server **with production CSP**:
  home, privacy, terms, support, challenge and custom 404 at 320, 390, 768, 1024 and
  1440 pixels. No horizontal overflow; all local links/anchors resolve; correct
  200/404 statuses. Screenshots inspected at desktop and phone sizes.
- Every guide control exercised; all nine frames load with stable image dimensions,
  correct selection state and keyboard activation. Header/footer navigation and
  skip link exercised. No page errors, missing artwork or CSP violations.
- All five content routes reflow at **320px with 200% text**. No-JS and reduced-motion
  variants checked. Guide controls/help are hidden without JS and useful stills remain.
- Android intent and generic scheme generation checked with score, negative/boundary
  seed, version/world/day, unknown parameters and duplicate keys; malformed/missing
  seeds retain the existing safe fallback. Existing challenge JS is byte-unchanged.
  Browser checks verify generated links, not the installed app's OS handoff.
- UTC date rollover tested across midnight. Reading contrast: teal/paper 9.76:1,
  secondary/sage 4.99:1, secondary/paper 5.50:1, teal/gold 6.90:1, teal/slot 8.54:1,
  mint/teal 5.02:1. Pink is decorative, never small reading text.
- JS syntax checks and `hob git diff --check` pass. Derivative dimensions verified;
  approved source PNG hashes still match their provenance manifest. All modifications
  are under `site/` on `feat/update-website-ui`.

Preview: `http://127.0.0.1:8765/`, running `python site/tools/preview.py`.
Hob preview pane: `web_1790612943877_067fe556`.
Temporary browser tooling/screenshots/results are in ignored `site/.preview/`.

**Next:** owner visual review; optional approved skin close-ups; recheck real store
availability before changing download links. Deploy only as a separate authorized task.
