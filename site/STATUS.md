# Website work log

## 2026-09-28 — Deletion path verification

Owner asked whether user deletion works. Verified the public support page still
contains the deletion instructions and separate notification/analytics request
paths. The website is an instructions/email entry point; account deletion runs
through the app's authenticated Supabase endpoint.

Fresh verification: all 9 existing database/provider-cleanup tests passed, using
disposable PGlite and mocked provider requests. These cover cascading profile/run/
board deletion, deletion/recovery ordering, service-only RPC access and retryable
provider failures. Unity source was inspected read-only; Unity tests were not rerun.

Live backend test used one newly created anonymous profile and one zero-score Free
history record (no public leaderboard entry or purchase). Confirmed its profile and
run existed, then called the same `delete-account` endpoint used by the game. HTTP
200 returned `deleted:true` and `provider_deleted:true`. Subsequent authenticated
reads returned zero profile/run rows; Auth rejected the deleted user (403), and its
old refresh token could not restore a session (400). Unauthenticated deletion was
also rejected (401). The disposable profile was removed successfully; credentials
were held only in process memory. Sanitized evidence and temporary verification
script stay in ignored `.preview/`.

Limits: the provider result confirms the endpoint's cleanup outcome for this fresh
identity; it does not prove deletion of an existing purchase-linked RevenueCat
customer. Live leaderboard-row cascading was covered by the database tests rather
than publishing a test score. OneSignal/analytics manual requests and actual email
delivery were not exercised. The actual in-app two-tap flow, local recovery/session
cleanup and continued play need a disposable device profile: asked the owner to
identify one on the connected Nord 2 or OnePlus 6T before permanent deletion.
Neither phone's game data was accessed or changed. No code/deployment changes to the
backend, Unity or iOS; only this website verification record was updated.

## 2026-09-28 — Rotating hero illustrations within each mode

Owner requested rotation through several approved images inside the selected hero
mode. Camera now loops standing → left → right → hop; Tilt & Touch loops hold →
left → right → tap. Each frame stays for three seconds before the next image loads
and decodes. Image, descriptive alt text, caption and step counter update together.
The mode pills remain manual and each newly selected mode starts at its first step.

Added Pause/Play and Next controls. Next pauses automatic playback; explicit pause
persists across mode changes. Hovering, scrolling the card out of view and hiding
the tab suspend rotation. Keyboard focus on mode/Next controls stops it until Play.
Reduced motion starts with a still image and permits manual steps or explicit Play;
changing the preference stops playback. Without JS the camera still/caption remain.
No cross-fades, layout shifts, automatic live announcements or new dependencies.
Failed images keep the prior complete scene and pause; stale loads cannot override
a newer choice or resume after Pause.

Added six full-size responsive WebP derivatives for the remaining hero frames.
Generator, manifest and README updated. All 22 WebPs total 686,206 bytes; original
PNG hashes and derivative sizes verified. Only website files changed; no deployment
or edits to Unity or the active iOS worktree.

Verified both complete loops, mode persistence, Pause/Play/Next, keyboard and touch,
hover/offscreen/background suspension, no-JS and reduced-motion behavior, live
preference changes, failed-image recovery, pause during loading and rapid selection.
Card dimensions remain identical across all eight frames at 1440, 390 and 320 pixels
and 200% text at 320/800. No horizontal overflow; desktop/phone screenshots inspected.
High-density mobile selects full-size art. All nine how-to frames remain independent.
Existing home/privacy/terms/support/challenge routes and custom 404 return their
expected statuses with the existing CSP. JS syntax and whitespace checks pass.
Visual QA reused the ignored official-font response cache noted below; production
font loading is unchanged. Preview: `http://127.0.0.1:8765/` (refresh to update).

## 2026-09-28 — Camera-first hero with manual mode selection

Owner approved the two-mode hero proposal. Camera Mode (BETA) is now the default,
using the approved sideways-step illustration; the two native pill buttons select
Camera Mode or Tilt & Touch and swap the corresponding illustration, caption and
instruction together. The main introduction now mentions both body movement and
phone tilt. There is no autoplay. Camera copy retains the on-device privacy note.

Both scenes share a grid slot, so the card reserves the taller caption at every
width without moving during selection. The inactive scene is invisible, inert and
hidden from assistive technology. Selection changes only after its image decodes;
failed loads retain the previous complete scene, and rapid selections respect the
latest choice. Without JavaScript the camera image/instructions remain visible.

Added the full-size `camera_03` WebP for responsive hero art; existing guide/skin
derivatives are unchanged. Asset manifest and generator updated. All sixteen WebPs
total 401,626 bytes. Only website files changed; no deployment or Unity/iOS changes.

Verified mouse and keyboard switching at 1440, 390 and 320 pixels; 200% text at 320
and 800 pixels; identical card dimensions before/after selection; correct pressed,
visibility and inert states; no horizontal overflow; no-JS and reduced-motion
static defaults; failed-image fallback and rapid-click ordering. Desktop and phone
screenshots inspected. JS syntax and whitespace checks pass. Google Fonts timed
out in the QA browser, so the final visual checks used downloaded official font
responses cached only in ignored `.preview/`; production font loading is unchanged.

Preview remains `http://127.0.0.1:8765/`; refresh to see the new mode selector.

## 2026-09-28 — Heading wrap fix and actual skin previews

Owner reported "controller" splitting before its final letter and requested skin
images in Fresh look. The hero now uses container-relative type sizing and normal
word wrapping, with the narrow-layout width cap removed. This replaces the hero's
emergency mid-word wrapping rather than hiding overflow.

Added Ember, Frost and Prism previews using the game's existing 512px transparent
thumbnails, converted losslessly to website-only WebPs (93,230 bytes combined).
Ember/Frost copy reflects their distinct character models; the Season 1 card labels
Prism as a level-10 pass reward. Asset generator, provenance and README updated.
This resolves the optional skin-preview asset gap recorded in the original pass.

Verified 18 viewport widths from 320 to 1920 pixels at normal and 200% text size:
no hero word splits, clipped headline or horizontal page overflow. Inspected new
desktop and phone screenshots. All three previews load without script/CSP errors,
reserve their square aspect ratio and preserve the source RGBA pixels. Existing
guide derivatives are unchanged. Only website files changed; no Unity/iOS writes,
no production deployment. Refresh `http://127.0.0.1:8765/` to see the updates.

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
