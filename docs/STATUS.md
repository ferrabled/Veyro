# Status journal (newest at top)

## 2026-09-28 — Main merged into the game UI branch (feat-game-UI-improvement / main-merge)

Owner committed the audio, illustrated guide, icon and documentation through `d52a8dc`,
then requested merging main. Fetched `origin/main` at `1da494f` (share-run PR #13 included)
and merged it into `feat/game-UI-improvement`. The only text conflict was this journal:
all 13 entries in the conflicting additions were retained, ordered newest first.

The automatic code merge brought in the challenge modal guard but knew only notifications
and analytics. Switched it to the existing `AnyPanelOpen` check so SOUND also holds an
incoming challenge. Added three EditMode regressions exercising the actual RunFlow method:
each panel retains the pending link across repeated checks, and closing it allows processing.
The tests use a foreign-version link to exercise refusal without starting sensors/services.
Audio, illustrated guide, icon and the owner's retained slide instructions are preserved.

**Verified:** Unity 6000.5.9f1, **605/605 EditMode tests passed**, including all three new
panel checks. Both tests and the Android build used `C:\scratch\veyro-audio\game`, with all
981 Assets/Packages/ProjectSettings files SHA-256 matched to the merge snapshot and no extras
before execution. Gradle staging was cleared at its checked scratch path. Challenge landing
JavaScript passes `node --check`. `BuildAndroidDev` completed with exit 0; the ARM64 APK is
**99,342,700 bytes**, version `1.0.0-dev.20260928-0202.nogit`, versionCode 5, with the same
permissions as the pre-merge development APK and the UnityPlayerGameActivity launcher.
SHA-256: `bfcb44632d4ddcc8c6974a16a8da5d0b7db1d0172cc2b6efc3fef894949b08ae`.
Reports, logs, permission snapshots and APK are under ignored `builds/main-merge-review/`;
`builds/MotionRunnerDev.apk` was also updated.

**Scope:** verification covers the merge snapshot, before the other pane's concurrent
profile/guide UI polish. Those edits remain outside the staged merge; the scratch was not
resynced after that work began. No phone installation, push or deployment in this merge task.

**Needs human device test:** after installing this merged build, open SOUND, deliver a valid
challenge link and verify the menu/panel remain until SOUND closes, then the challenge starts
once. Repeat for notifications/analytics. The previous guide animation/legibility, audio mix,
camera-hop restart, fallback, SHARE-return and two-device challenge checks remain pending.

## 2026-09-28 — Latest UI worktree built and installed on the Nord 2 (feat-game-UI-improvement / phone-install)

Owner requested the latest app on the phone and a check of the remaining pre-merge work.
Built the current worktree (including uncommitted guide illustrations, fallback cleanup and
icon) using `BuildScript.BuildAndroidDev` in `C:\scratch\veyro-audio`. Assets, Packages and
ProjectSettings were synced with `robocopy /E`, checked byte-for-byte by SHA-256, and audited
for scratch-only files before building; the Gradle staging path was checked and cleared if
present. The sole trailing-whitespace warning in the worktree's icon settings was removed.

**Build:** Unity 6000.5.9f1 exited 0. `builds/MotionRunnerDev.apk` is **99,338,664 bytes**
(+2,445,920 bytes versus the 25 Sep local APK). Version `1.0.0-dev.20260928-0134.nogit`,
versionCode 5, package `com.ferrabled.veyro.run`, launcher `UnityPlayerGameActivity`, adaptive
ticket icon. `aapt2 dump permissions` is identical to the previous local development APK.
SHA-256: `9b3371309566e588b27ea9935ea52b7cd6ebe48d893620423c75a85246ba4cce`.
Build log: `C:\scratch\veyro-audio\guide-phone-build-20260928.log`.

**Device:** Nord 2 / DN2103, 1080×2400. `adb install -r` succeeded, without uninstalling or
clearing app data. Package first-install time remains **18 Sep 13:16:09**; update time is
**28 Sep 01:40:01**. Launch succeeded, the menu displays the new build stamp, and the existing
owned pass/equipped outfit remain visible. A subsequent capture showed an active camera run;
further input was left to the owner. Screenshots are ignored under `builds/guide-review/`.
The only startup Unity error sampled was the previously documented AssetPackManager class
probe; no AndroidRuntime crash was observed. This was an install/launch smoke check, not an
audio listen or a physical camera-gesture acceptance test. The earlier **590/590 EditMode**
result still applies; the only source change since that run was YAML whitespace.

**Remaining:** human guide animation/legibility, audio mix, camera-hop restart (no first-frame
jump), camera fallback and SHARE-return checks. Re-stage and commit the guide/icon/docs changes,
then merge `origin/main` (now includes share-run PR #13), include SOUND in the pending-challenge
modal guard, and verify the combined result. This installed APK predates that main merge.
Slide instructions remain as requested. No commit or merge performed.

## 2026-09-28 — App Store Connect set up under the owner's own Apple account; listing copy written (T-032, feat-game-UI-improvement iOS session)

Play v5 has been in production review since 16 Sep, so the owner opened the iOS route as a
second store — under **their own** Apple Developer account (individual, approved 28 Sep), not the
friend's account D11/P2 assumed; iOS revenue and tax now land on the owner. Done by the owner:
Free + Paid Apps agreements Active, bank account + W-8BEN (line 10 left blank per Apple's tips
sheet) Active, App ID `com.ferrabled.veyro.run` (In-App Purchase + Push Notifications), app record
(SKU `veyro-run-ios`), the three non-consumable IAPs as drafts with 1290×2796 placeholder review
screenshots. EU storefronts stay off until the DSA trader declaration is verified.

Added `docs/store-kit/APP_STORE_LISTING.md`: every App Store Connect field value, including the
owner-requested camera subtitle `Tilt or camera: daily runner`, keywords, description (camera
BETA block, STEP wording), App Privacy rows mirroring the Play Data safety state, IAP fields,
review notes, TestFlight info and the Info.plist strings the iOS build needs. Camera mode ships on
iOS (owner, 28 Sep).

**Next:** iOS code changes (App Store RevenueCat key + iOS build method, iOS player settings,
iOS camera permission path, OneSignal iOS package, Layers on iOS without ATT, Android-only copy),
Xcode export from Windows, then a Mac (rented or the friend's) to archive and upload to TestFlight.
Not yet verified on any Apple hardware. D11 in DECISIONS.md still describes the friend's-account
path — owner to update it.

## 2026-09-28 — Guide illustrations integrated and rendered (feat-game-UI-improvement / guide-art-integration)

The owner supplied all ten AI-generated PNGs in `screenshots/guide/`: four tilt frames, five
camera frames (setup → centred player → step left → step right → hop), and one run still.
Copied them byte-for-byte to `game/Assets/Resources/Art/Guide/` and retained the masters. Every
source is 1586×992; all ten SHA-256 pairs match. No resizing, recolouring or palette reduction.
Added explicit keep rules for `screenshots/guide/` (it was already outside the dated-screenshot
ignore rule) and an LFS rule for its PNGs. Runtime PNGs already had LFS coverage. Unity-generated
folder/texture `.meta` files are included with the runtime assets.

The guide now displays the supplied art through its existing 1.1-second unscaled frame loop.
Removed the "illustration coming"/asset-filename line from the missing-sequence fallback; the
instruction caption remains. Updated the brief, importer/content comments, BACKLOG and GAME_ART
to describe the delivered assets and the two-folder update workflow. Preserved the owner's
camera copy change to STEP and the slide instructions, which the owner wants to keep ahead of
the upcoming slide mechanic. T-015's separate procedural in-mode showcase remains open.

**Verified:** reused `C:\scratch\veyro-guide`, synced Assets/Packages/ProjectSettings with
`robocopy /E`, checked every source file by SHA-256 and checked for extra scratch files before
Unity ran. Unity 6000.5.9f1 `GuideUiReview.Render` succeeded; its expanded review checks every
expected sprite and rejects a page showing fallback. All ten sprites loaded at 1024×640.
All ten importers have Sprite/Single, no mipmaps, sRGB and the Android 1024/ASTC 6x6 override.
Four page renders plus ten individual-frame previews are in ignored `builds/guide-review/`;
the four pages were visually inspected at 1080×2400: artwork visible, copy clear of buttons.
EditMode: **590/590 passed**, including **20/20 GuideStateTests**; report copied to
`builds/guide-review/guide-art-tests-20260928.xml`. No Android APK built or phone touched.

**Needs human device test:** open HOW TO PLAY; watch a complete tilt loop (four frames) and
camera loop (five), verify the run image stays still, then check NEXT/back/skip and legibility
on the phone. Historical 24/25 Sep notes below describe the earlier placeholder-only state.

**Git:** audio is already committed as `b978dcf`. The owner's existing guide staging was left
intact; re-stage the edited guide files, both PNG folders, runtime metadata and ignore/LFS rules
before committing. Main now contains all of share-run via PR #13 (`1da494f`); finish the local
guide/icon/docs commits before merging `origin/main`. No commit or merge was performed here.

## 2026-09-28 — PR #13 (feat-share-run) Copilot review: 8 of 9 findings applied

- **Landing page, Android:** `challenge.js` now opens `intent://challenge?<query>#Intent;scheme=veyro;package=com.ferrabled.veyro.run;S.browser_fallback_url=<Play listing>;end`
  when the user agent says Android — the app still receives exactly `veyro://challenge?<query>`.
  The bare scheme made Chrome replace the page with `ERR_UNKNOWN_URL_SCHEME` when the game was
  missing (SHARE_COMPLIANCE §3.5), which also killed the 1.5 s Play fallback timer. The Android
  path has no timer: while Chrome shows an "open in app?" prompt the page is still visible, and a
  timer would send someone who has the game to the Play Store. Non-Android visitors keep the bare
  scheme + timer.
- **Landing page disclosure** (SHARE_COMPLIANCE §8.5): the no-cookies / no-analytics line and a
  privacy-policy link are on the page itself.
- **Privacy policy:** SHARE_COMPLIANCE §8.1–§8.4 applied to `docs/PRIVACY_POLICY.md` and
  `site/public/privacy/index.html`, effective date 28 September 2026 in both. Corrected to the
  shipped build on the way: real `v=greybox-1` example, the personal-best line and the `d` date
  key are listed, the clipboard fallback is mentioned, and the link paragraph describes today's
  unverified path (page → button) rather than assuming App Links already verify.
- Doc fixes: App Links item is OPEN_QUESTIONS **24** (BACKLOG T-024, STATUS 21 Sep said 20);
  SHARE_COMPLIANCE inventory no longer says "to be written"; the T-024 entry's "known, deliberate"
  paragraph no longer claims the banner survives RUN AGAIN (the leak fixed in the review-fixes
  entry); `Quaternius/SOURCES.md` now says `crashWall` = `Death01` ×1.7, matching
  `RunnerCrashBuild.Final`.
- **Not applied:** the "high" finding that `filter.Add(new XAttribute(...))` after child elements
  throws in `AndroidChallengeLinks.cs`. It does not — LINQ to XML keeps attributes and child nodes
  in separate lists (only XmlWriter has that ordering rule). Checked with a .NET snippet, and the
  21 Sep device build's manifest already gained both filters, so the postprocessor ran cleanly.

**Verified:** `node --check` on `challenge.js`; sample intent URL inspected (a `#` in a value is
encoded to `%23`, so it cannot end the data URI early). No game code changed, so no Unity run.

**Second Copilot pass, same day — two "previously missed" findings, both real, both fixed:**
- **A seed-only (truncated) link was parsed and then thrown away.** `TryParse` left a missing `v`
  as `""`, and `RunFlow.TryStartPendingChallenge` refuses any version but `ChunkLibrary.ContentVersion`
  — so `AHalfBrokenLinkStillPlaysIfItHasASeed` promised a run that never started. A missing or empty
  `v` now means this build's version, the same rule a missing `w` already had. An *oversized* `v`
  stays `""` and is still refused (present but no version any build ships). Tests: the half-broken
  link now asserts the version; new `AnEmptyVersionValueCountsAsAbsent`.
- **The landing page read numbers more loosely than the app.** `parseInt("4210oops")` showed 4210
  where the app reads 0, and an out-of-range seed showed a challenge the app refuses. `challenge.js`
  now requires the whole value to be a signed 32-bit integer, takes the last of a repeated key and
  compares the mode case-insensitively — all three exactly as `ChallengeMessage.TryParse` does.
  Edge cases checked in node (`4210oops`, `2147483648`, `%2B7`, `+3`, `s=1&s=x`, `007`).

The XAttribute finding was listed again only because its thread was still unresolved — the code is
unchanged and the reasoning above stands.

**Needs a human device test** (after `npx wrangler deploy` from `site/`, owner-only): on an Android
phone **without** the game, open a challenge link in Chrome and tap **Open in Veyro** → the Play
listing must open, not an error page. With the game installed → the game opens on the challenge.
Repeat once in Samsung Internet if one is to hand.

## 2026-09-25 — App icon: the approved ticket + V monogram is now the Android launcher icon, applied from code (T-030 icon slice, feat-game-UI-improvement icon session)

Until this pass every build shipped Unity's default icon. The owner-approved "daily ticket + V
monogram" (`screenshots/icon.png`, `docs/store-kit/ART_DIRECTION.md` §0a) is now the launcher icon,
and nothing about it was clicked in the inspector: import settings, layer generation and the
PlayerSettings wiring are all code, and every Android build re-applies it.

**What changed**
- `Assets/Art/Icon/AppIcon.png` — 1024×1024 from the 1254 master (ffmpeg lanczos), then a
  256-colour palette: 234 KB against 1.31 MB for the 24-bit downscale (riso grain does not
  compress). Difference from 24-bit at launcher size (192 px): mean 0.7/255, max 7 — invisible.
  Picked up by the existing LFS rule `game/Assets/Art/**/*.png`; `.gitattributes` unchanged.
- `Assets/Art/Icon/AppIconForeground.png` + `AppIconBackground.png` — generated by the new
  `Editor/AppIcon.cs` (`AppIcon.Generate`, menu Veyro/Art/Regenerate app icon layers): the flat art
  at 634/1024 px (62 %) centred on a transparent canvas, and a solid fill of the art's ground colour
  sampled from the source's corners. Integer supersampling, no time or randomness — two runs gave
  byte-identical SHA-256s. Committed as assets, regenerated only when the source changes.
- **Deviation from the brief, on purpose:** the background layer is the art's own teal ground
  (`#053740`), not paper `#FBF5E9`. The art is a paper ticket on a flat teal field (σ < 1 across
  all four corners), so a matching field makes the scaled square melt into the background and the
  masked icon reads exactly like the flat one; a paper background would have shown the teal
  square's edges as four paper slivers through Android's round mask. At 62 % the ticket's far
  corner sits at 0.55 of the half-size, inside the 66 dp safe circle (0.61). Flip it in one place
  (`AppIcon.SampleGround`) if the art ever gets a non-uniform ground.
- `Editor/AppIconImport.cs` — `AssetPostprocessor` for `Assets/Art/Icon/`: Default type, sRGB,
  no mips, readable (Unity reads the pixels to make the mipmap-* sizes), **uncompressed** (no ASTC
  artefacts in a 48 px icon), max 1024, Android/iPhone overrides cleared.
- `Editor/ProjectSetup.cs` — `EnsureAppIcon()`: default icon via `SetIcons(NamedBuildTarget.Unknown)`,
  Android Adaptive = (background, foreground), Round and Legacy = the flat art (minSdk is 26, so
  every supported device takes the adaptive one). Called from `SetupUrp` and from the new batchmode
  entry `ProjectSetup.ApplyAppIcon` (exit 4 when the art is missing). A missing texture warns and
  leaves that kind alone — the icon can never fail a build.
- `Editor/BuildScript.cs` — `Build()` calls `ProjectSetup.EnsureAppIcon()` first, inside the try,
  so a headless build from a fresh checkout or a scratch copy always carries it. It is a persistent
  setting, deliberately not restored in the finally.
- `ProjectSettings/ProjectSettings.asset` — only icon lines: `m_BuildTargetIcons` gains the 128 px
  default slot, and the 18 Android slots (6 adaptive × 2 layers, 6 round, 6 legacy) now reference
  the three textures by GUID. No other field moved.

**Verified.** Scratch `C:\scratch\veyro-icon` (Library from veyro-guide, `Bee/Android/Prj`
deleted, `Assets/` and `ProjectSettings/` byte-equal to the worktree before the run). Batchmode
`AppIcon.Generate` twice → identical hashes; `ProjectSetup.ApplyAppIcon` → the diff above;
`BuildScript.BuildAndroidDev` → `C:\scratch\veyro-icon\builds\MotionRunnerDev.apk`, 96.1 MB
(95.8 MB on 24 Sep; the delta is the launcher PNGs). Inside the APK: `res/mipmap-anydpi-v26/app_icon.xml`
+ `app_icon_round.xml` (adaptive), `ic_launcher_foreground.png` / `ic_launcher_background.png` at
ldpi…xxxhdpi (81…432 px) and `app_icon.png` / `app_icon_round.png` (36…192 px) — extracted to
`C:\scratch\apk-icon\out\res\` and viewed: the ticket, centred with safe-zone padding, on the teal
field. `aapt2 dump badging`: `application: label='Veyro Run' icon='res/mipmap-anydpi-v26/app_icon.xml'`,
`application-icon-120…640` all resolve to it, and the 28 `uses-permission` lines are identical to
the 24 Sep dev APK. EditMode suite in the same scratch: **590/590** (a first run showed 589/590 — `AudioCatalogTests.ResultMusic_ResolvesToClips`, because the audio session's `result_best/lost.ogg` landed in the worktree after the scratch was synced; copied in, re-run, clean). Not committed. Phone
not touched — **needs human device test:** install the scratch APK and check the launcher (circle
and squircle masks) plus the recents/settings tiles.

**Next.** The same `AppIcon.png` is the Devpost 1024×1024 icon and, downscaled to 512, the Play
listing icon (ART_DIRECTION §0a table) — upload both when the listing is next edited.

## 2026-09-25 — Guide illustration brief checked against the game, one copy-paste section per image (feat-game-UI-improvement guide session)

Owner asked whether `docs/GUIDE_ILLUSTRATIONS.md` matched the game and whether to attach
screenshots. The file is now one self-contained section per PNG: what to attach (and what a
screenshot must show), then a full prompt block to paste. Fixes from checking it against the code:
- **Camera frames:** switched to a view from behind the player. The old side view hid the
  sideways lean, and its left/right note was backwards.
- **Colours:** coins are gold `#FFC85C` (`ChunkView`), not pink. The phone-screen track now uses
  the real park colours (`ParkChunkArt`).
- **Tilt frames:** the tilt is now a sideways tip around the phone's long axis (roll, which is
  what `GyroTiltInput` reads), not a steering-wheel turn. Owner correction: the first `tilt_02`
  came back rotated in the picture plane. The tilt set is generated and approved by the owner.
- **Camera set:**
  - The crouch frame is replaced by lean right.
  - After the owner reviewed the first `camera_02`, the view became a symmetric one from behind
    and above the player. Phone, floor line, player and wedge now share one centre line, and
    the wedge stops at the player instead of spilling onto the floor.
  - `camera_01` is now the empty spot marked with footprints. A "too close" player would hide
    the phone from this viewpoint.
  - `camera_04` can be `camera_03` flipped.
  - The owner is not using pose photos, so every prompt stands on text alone.
  - 28 Sep: the owner kept the first centred `camera_02`, pink t-shirt included. A whole-image
    touch-up redrew everything: new table and figure, an all-teal silhouette, heavy pink edges.
    Fixes to a kept image are now select-area edits only. The brief asks for at most a hairline
    ink offset.
  - A select-area narrowing of the table worked, but the owner kept the wide coffee table.
    `camera_01` (empty spot with footprints) was made from the kept `camera_02` and matches its
    framing.
  - `camera_03`/`camera_04` are now a side step of the whole body into the lane, not a lean
    (owner call, matching how the game is played). `GuideState`'s camera page copy now says
    "STEP left or right to change lane." (was "LEAN or step left/right…"), and its placeholder
    caption says "step to steer". Line count and length are unchanged, so the
    `BodyCopy_FitsUnderTheIllustration` budget still holds (not re-run). `camera_05`'s hop now raises the whole
    figure, head included, because the jump is detected from the head rising.
  - The size rule is relaxed to 16:10 at 1024–1600 px wide: the tool returns 1568×980, and the
    slot uses `preserveAspect`.
- **Extras:** the step-out/raise-hand frames are renamed `pause_01`/`pause_02` and kept out of
  `Resources/`, since everything in `Resources/` ships.
- **Card:** `FirstRunGuide`'s card is now opaque `ParkTheme.Paper`. `MenuTheme.Card`'s 98% alpha
  over the dim showed a few levels darker than the opaque art, outlining it as a lighter
  rectangle. Frame counts are unchanged (tilt 4, camera 5, run 1), so `GuideState` is untouched.

**Found, not fixed — owner call.** Slide is not in v1.0: `TRACK_GENERATION.md` says "v1.0 ships
left/right/jump only", no obstacle needs it, and no gameplay code reads `IsSlidePressed`. Three
places still teach it:
- the guide's `TILT & TOUCH` page ("SWIPE DOWN to slide");
- the guide's `CAMERA MODE` page ("CROUCH to slide");
- the mode picker ("crouch to slide").

The brief no longer illustrates slide.

**Verified:** by reading only. The one code change is a colour constant, using the same
`Art.ParkTheme` reference `PauseMenu` already compiles with. No batchmode run.

## 2026-09-24 — First-run guide: four pages, an illustration slot per mode, placeholders until the art lands (feat-game-UI-improvement guide session)

Owner ask: the how-to-play guide should *show* each mode, not just say it. This pass builds the
slot and the loading path so the pictures are a drop-in; the art itself is not generated yet —
`docs/GUIDE_ILLUSTRATIONS.md` (written alongside) is the contract for keys, frame counts,
filenames, size and style. Related to **T-015** (annotated in the backlog, not marked done — the
procedural in-mode showcase remains open).

**What changed**
- `Track/GuideState.cs`: 3 → 4 pages — `TWO WAYS TO PLAY` (no picture; the copy takes the
  height), `TILT & TOUCH` (`tilt`, 4 frames), `CAMERA MODE` (`camera`, 5 frames), `DURING A RUN`
  (`run`, 1 frame). `GuidePage` gained `Illustration`, `FrameCount`, `Caption`, `HasIllustration`.
  Copy rewritten to ≤ 9 hand-wrapped lines of ≤ 40 chars on illustrated pages; the new
  `BodyCopy_FitsUnderTheIllustration` test pins that budget and caught two overruns during this
  pass (the desk render showed the 10th line touching the button).
- New engine-free `Track/GuideIllustration.cs`: `FramePath("tilt", 1)` → `Art/Guide/tilt_01`,
  `FileRange` ("tilt_01.png … tilt_04.png"), `IsValidKey` (lowercase ASCII), `NextFrame`,
  `SecondsPerFrame = 1.1`.
- `Gameplay/FirstRunGuide.cs`: 804×502 slot under the counter (16:10, the art's own aspect, so an
  opaque paper frame melts into the card), body 40 → 34 px re-anchored to a 370 px band above
  the button; a page without a picture gets the slot's height back. Frames load with
  `Resources.Load<Sprite>`; ≥ 2 present → cycled every 1.1 s in **unscaled** time (the GIF
  stand-in), a missing middle frame is skipped, none present → the mock placeholder: rounded
  `MenuTheme.Slot` panel (CosmeticPanel geometry, no sprite to ship) with an inset
  `MenuTheme.Empty` outline, the page's caption in `Dim` and
  "illustration coming · Art/Guide/tilt_01.png … tilt_04.png" in `Faint`.
- `Editor/GuideArtImport.cs`: `AssetPostprocessor` for anything under
  `Assets/Resources/Art/Guide/` — Sprite / Single / FullRect mesh, no mips, sRGB, opaque, max
  1024, Android override ASTC 6x6. The folder does not exist yet; it fires on the first import,
  so dropping the PNGs in is the whole job.
- `Editor/GuideUiReview.cs`: renders the four pages to `builds/guide-review/guide-page-N.png` at
  1080×2400 using the CanvasScaler's real 0.5-match scale (menu Veyro/UI/Render how-to-play
  pages; batchmode `-executeMethod MotionRunner.EditorTools.GuideUiReview.Render`). Re-run it the
  day the art lands.
- `.gitattributes`: LFS pattern for `game/Assets/Resources/Art/Guide/*.png`. `GuideStateTests`
  updated for four pages, +9 tests.

**Verified.** Scratch copy `C:\scratch\veyro-guide` (Library from veyro-share minus `Bee/`;
`diff -rq` of `Assets/` against the worktree showed zero non-`.meta` differences before each
run, per gotcha 4). `Unity 6000.5.9f1 -batchmode -runTests -testPlatform EditMode`: **577/577
passed**, `GuideStateTests` 20/20 — with the audio session's in-flight code compiled in (one
early run failed on its then-missing `SoundPanel`, which landed minutes later; not this pass's).
Trap re-hit: `-runTests` with `-quit` exits before the runner starts and writes no XML (STATUS
487) — drop `-quit`. Desk renders viewed at 1080×2400 for all four pages: counters read "N / 4",
placeholders draw with caption + filenames, the body clears the button on every page, page 1
fills the height with no slot.

**Not done — needs human device test** (`BuildAndroidDev` → install → launch)
1. On the mode picker tap "how to play". Page 1/4 → 4/4 with NEXT, back with "< back": page 1
   has no slot and its copy fills the card; pages 2–4 show the slot-coloured placeholder with
   caption and expected filenames; nothing touches NEXT / LET'S RUN; 34 px body is legible.
2. Skip once, relaunch: the guide must not come back by itself; it must still open from the link.
3. When the PNGs exist: drop them in `game/Assets/Resources/Art/Guide/`, confirm the Inspector
   shows Sprite (2D and UI) / no mips / max 1024 / Android ASTC 6x6 without touching anything,
   then run the guide: `tilt` and `camera` cycle every ~1.1 s (also with the game paused —
   unscaled), `run` is a still. Re-run Veyro/UI/Render how-to-play pages for the desk check.

## 2026-09-24 — T-045 Audio: music, SFX and sound settings (feat-game-UI-improvement audio session)

The game had no audio code at all — `GameBootstrap` added an `AudioListener` and nothing ever
reached it. This pass adds the whole layer behind a fail-open seam, hooks it into every screen
and gameplay beat, and gives the player two sliders. Assets (CC0, placed by the asset session
under `game/Assets/Resources/Audio/` with licences alongside) are 14 SFX + `menu_loop` + four
run tracks `run_01..run_04`; the code discovers the run tracks by probing, so a fifth is a file
drop. Not committed — owner review pending.

**Architecture.** New asmdef `MotionRunner.Audio` (`Assets/Scripts/Audio/`, engine references
on, auto-referenced, referenced from the EditMode tests asmdef).
- `SoundSettings` (engine-free): `MusicVolume`/`SfxVolume` 0–1 linear as set, defaults 0.8/1.0,
  keys `veyro.audio.music` / `veyro.audio.sfx` (floats) plus `veyro.audio.music.muted` /
  `veyro.audio.sfx.muted` (ints) for the pause toggles; NaN/out-of-range clamped on load and set;
  `Perceptual(x) = x²` is the slider → AudioSource map (0→0, 1→1, monotonic, midpoint 0.25 ≈
  −12 dB). Setters are in-memory, `Save()` writes through `ISoundSettingsStore`
  (`PlayerPrefsSoundStore` on device, a dictionary fake in tests).
- `MusicPicker.TrackIndex(RunSeed.RngState(), count)`: lowbias32 mix then modulo, so consecutive
  daily seeds spread across the tracks; `NextCycled` is the seedless fallback (unused today —
  every run has a `RunSeed`).
- `CoinPitch.For(combo)`: 1.0 + 0.035·(combo−1), capped 1.5; the "reset after a gap" is
  `ScoreState` dropping `Combo` after `ComboWindowSeconds`, which RunSession hands straight in.
- `AudioCatalog`: `Sfx` enum → `Audio/Sfx/<name>` path table; `MenuMusic`; `RunTrackPath(n)`;
  `MaxRunTracks = 8` bounds the probe.
- `GameAudio` (MonoBehaviour, one instance, static surface that no-ops without one): two looping
  music sources crossfading over 0.8 s (`ignoreListenerPause`, unscaled dt clamped at 0.1 s like
  `ResumeCountdown`), duck to 35 % on pause over 0.4 s; **asking for the track that is already
  active RESUMES it** (the source is `Pause()`d at silence, never `Stop()`ped), which is what keeps
  RUN AGAIN / Daily / RESTART RUN on the same song while a new seed crossfades to a new one. Eight
  one-shot voices (idle voice first, else round-robin); one identical `Sfx` per frame; a plain
  `UiTap` yields to a `UiConfirm`/`UiDeny`/`UiBack` already fired that frame (listener order:
  action first, then the tap — `RuntimeUi.TapSound`); clips lazy-loaded via `Resources.Load`, a
  missing clip warns once (`[Audio] missing clip …`) and is skipped. `[Audio] play <id>` /
  `music start|resume|stop <path>` lines are logged only in development builds.
- `SoundPanel` (Menu, pattern `NotificationPanel`, canvas 140): MUSIC and SOUNDS sliders built
  from `Image`s in `MenuTheme` colours (track/fill/handle in the stock three-rect layout), live
  while dragging, `Save()` on pointer-up via a small `IPointerUpHandler` relay; the SOUNDS release
  plays `ui_tap` at the new level; CLOSE (`ui_back`) and Android back close it (`RunFlow` holds
  `_soundPanel` exactly like `_notificationPanel`; the three panels share an `AnyPanelOpen` guard).
  Opened from a new **SOUND** link on the Profile links card — row 0 is now three columns HOW TO
  PLAY / NOTIFICATIONS / SOUND (each best-fits down from 32 pt; the rows below are untouched).
- Pause menu: two text-only toggles **MUSIC ON/OFF · SOUNDS ON/OFF** in the 75 px strip under QUIT
  (y = 44, 340×52 each, 28 pt dim). They fit without moving RESUME/RESTART/QUIT; skipped when no
  audio instance exists (tests).
- `Editor/AudioImportRules.cs` (`AssetPostprocessor.OnPreprocessAudio`, `GetVersion() = 1`):
  `Assets/Resources/Audio/Music/` → Streaming, Vorbis 0.7, `preloadAudioData = false`;
  `Assets/Resources/Audio/Sfx/` → DecompressOnLoad, Vorbis 0.8, `forceToMono`; Android override =
  default. The `.ogg`s arrived with no `.meta`, so first import in the scratch copy applied the
  rule; the generated `.meta` files are copied back with this change.

**Hooks (where each cue fires).** UI tap: `RuntimeUi.TextButton` (new trailing `Sfx sound`
parameter) and `RuntimeUi.TapSound(button, sound)` on every hand-rolled `AddComponent<Button>`
site — `RunHud.CardButton/CardLink`, `SeasonPassCard`, `FirstRunGuide.BuildLink`, `ProfilePage`
(links, scope tabs, reroll), `CosmeticUi.Button/Pill` (new `sound` parameter), `CosmeticItemCard`,
`ModePickerCard`, `HomePage` locker, `MenuTabBar`. `ui_back`: pause QUIT TO MENU, result card QUIT
TO MENU, NotificationPanel/AnalyticsPanel/SoundPanel CLOSE, season/locker `< BACK`, guide
`< back`/`skip`. Confirm: `StoreCatalogView` purchase/paywall success, restore that grew the
entitlement set, equip (`SkinService.Equip` true), `SeasonPassPage` collect, `CosmeticsPage` equip,
`FirstRunGuide` completed, profile import. Deny: purchase/restore failure (not Cancelled/Pending),
"not for sale yet", delete with no profile / delete failure, import failure. Gameplay: `coin`
(`RunSession.ResolveCollisions`, pitch from `Score.Combo`), `jump` and `lane_swoosh`
(`RunnerController.Step`; one swoosh per committed lane change), `crash` + `StopMusic()` in
`RunSession.Crash`. Result: `result_jingle` on the frame the card starts fading in
(`RunHud.Update`, after the crash pose) — **`new_best` replaces it on a new all-time best** (one
stinger per card; a daily best keeps the jingle). Countdown: `countdown_tick` on each `DisplayDigit`
edge (3, 2, 1) and `countdown_go` on completion in `PauseMenu` — camera resumes only, tilt has
no countdown. `streak`: `DailyStampsCard.Refresh` when today's stamp appears during the process
(first refresh of the process only baselines, so an already-stamped launch is silent). Music:
`RunFlow.ShowMenu` → menu loop; `RunSession.StartRun` → `PlayRunMusic(CurrentSeed.RngState())`;
`RunFlow.ApplyPhase` → `SetDucked(IsFrozen)`. **Not hooked: `slide`** — `RunnerController`
never reads `IsSlidePressed` (slide is still the T-002 stub), so the clip ships in the catalog
and the test, with no mechanic to sound.

**Verified.** Scratch copy `C:\scratch\veyro-audio` (robocopy `/E` of this worktree without
`.git`/`builds`, `Library/` seeded from `veyro-layers-stock`, `Library/Bee/Android/Prj` deleted
first — gotcha 4/11; the copy was checked for the new files and for `GameAudio.Create` in
`GameBootstrap` before trusting it). `Unity 6000.5.9f1 -batchmode -nographics -runTests
-testPlatform EditMode` → **577 tests, 576 passed, 1 failed**; the 33 new audio tests
(`SoundSettingsTests` 12, `MusicPickerTests` 7, `AudioCatalogTests` 7 incl. the `Resources.Load`
resolution of all 14 SFX, the menu loop and the gap-free `run_01..run_04` probe, `CoinPitchTests`
4 — plus 3 pure catalog checks) all pass. The one failure is
`GuideStateTests.BodyCopy_FitsUnderTheIllustration` ("CAMERA MODE has 10 lines; the body band
under the picture holds 9") — the guide-illustration work another session has uncommitted in
this same worktree (`GuideState.cs`, `GuideStateTests.cs`, `GuideIllustration.cs`), not audio;
that session trimmed the copy the same afternoon and its later run (entry above) was
**577/577** with both changes compiled in.
Baseline before this change was 515/515 (21 Sep) plus that session's additions. The first import
of the `.ogg`s generated 19 `.meta` files with the rule's settings (checked `coin.ogg.meta`:
loadType 0, Vorbis, 0.8, preload, mono; `run_01.ogg.meta`: loadType 2, 0.7, no preload; Android
block identical) — copied back to the worktree along with the `.meta`s for the new scripts.
Note for the next person: in this batchmode test context `GameBootstrap.Boot()` runs on every
domain reload (it always has — it builds the scene), so the test log now also shows
`[Audio] music start Audio/Music/menu_loop` four times; no exception follows it.

**Device (Nord 2, 24 Sep 13:00–13:08; finished by the coordinating session after the audio
session hit its API limit).** The first `BuildAndroidDev` died with `No space left on device`
in the IL2CPP backend; ~10 GB of stale scratch `Library/` caches (`veyro-hud`,
`veyro-layers-stock`) were deleted, the worktree re-synced into `C:\scratch\veyro-audio`
(hashes of the changed files checked) and `Library/Bee/Android/Prj` cleared before the rebuild
(gotcha 4). Result: **`builds/MotionRunnerDev.apk` 95.8 MB vs 91.9 MB** for the 24 Sep 00:18 dev
build — +3.9 MB for 6.4 MB of source Ogg re-encoded by the import rule. Installed and driven
with `adb shell input`; `adb logcat -d -s Unity` shows, in order:
- cold launch → `[Audio] music start Audio/Music/menu_loop`; the only `E/Unity` line is the
  pre-existing AssetPackManager probe (CAMERA_TUNING 119);
- PROFILE → SOUND: panel renders (title, MUSIC 80 % / SOUNDS 100 %, hint, CLOSE); every tap logs
  `play UiTap`; dragging MUSIC to 29 %, CLOSE, `am force-stop`, relaunch, reopen → **29 %**
  (persistence), then restored to 80 %;
- TILT & TOUCH → `run tracks: 4`, `music start Audio/Music/run_04` (seed-picked), `play Coin`
  then `Coin pitch=1.04 / 1.07 / 1.11 / 1.14` as the combo climbed, `play Jump` on a tap,
  `play Crash` + `music stop Audio/Music/run_04` from `RunSession.Crash`, `play ResultJingle`
  1.0 s later as the card faded in;
- tap-to-restart → **`music resume Audio/Music/run_04`** (same seed, same song, no restart);
- II → pause card shows `MUSIC ON · SOUNDS ON` under QUIT with RESUME/RESTART/QUIT unmoved;
  tapping MUSIC ON flips it to `MUSIC OFF` (screenshot) and back; QUIT TO MENU →
  `play Streak` (the day's stamp appearing on the rebuilt card), `music start
  Audio/Music/menu_loop`, `play UiBack`.
- Not exercised on the desk: `lane_swoosh` (the phone lay flat, no tilt), countdown ticks
  (camera resume only), confirm/deny (no Test Store purchase), `new_best`.
Screenshots: `C:\scratch\veyro-audio\shot-{menu,profile,sound,sound-30,sound-persist,run,pause,pause-muted}.png`
(not committed). The APK is copied to this worktree's `builds/MotionRunnerDev.apk` for the listen.

### Owner feedback pass — 2026-09-25 (result-screen music, no tap-anywhere, camera hop)

Four owner asks on the result screen, all in this uncommitted change.

**Result music.** After the stinger the card now has a loop under it: `Audio/Music/result_best`
when the run set a best of either kind (`summary.IsNewRecord` = all-time OR daily — the badge's
own rule), else `Audio/Music/result_lost`. `RunHud.Update` cues it `ResultMusicDelaySeconds`
(1.0 s) after the stinger fires, unscaled, through `GameAudio.PlayResultMusic(bool)` (crossfade
0.8 s as usual); `HideResult` clears the pending cue so a RUN AGAIN or quit inside that second
cannot drop the loop onto the next run. Both paths are in `AudioCatalog`
(`ResultBestMusic`/`ResultLostMusic`) and in `AudioCatalogTests` (paths distinct and in the
music folder; both `Resources.Load`). The two `.ogg`s and the replacement `crash.ogg` came from
the asset session mid-pass; their `.meta`s were generated by `AudioImportRules` on first import in
the scratch and copied back.
- *Resume across the result loop.* `GameAudio.PlayMusic` used to resume only when the request
  matched `_activePath`; with `result_lost` active, RUN AGAIN's `run_04` would have restarted
  from the top. The deck logic is now an engine-free `Audio/MusicDeck.cs`: a path per slot,
  `Plan(path)` → `Resume` of WHICHEVER slot still holds it (active or parked), else `Start` on
  the non-active slot (replacing whatever was parked there — the menu loop under a run, or the
  previous result loop), `FadeOut` parks the active slot, `Tick` moves the two levels in the
  caller's clock. `GameAudio` keeps the AudioSources, the unscaled/clamped dt, the duck and the
  `Pause()`-at-silence; it just does what the deck decides, and the `[Audio] music
  start|resume|stop` lines are unchanged. `MusicDeckTests` (9) pin it, including the scenario:
  run parked → result loop on the other slot → the run track resumes on its own slot with the
  loop fading out, and a second crash's `result_best` replacing `result_lost` without touching
  the parked run.

**Tap-anywhere restart removed, every run type and scheme.** `RunSession.Update` no longer
feeds `RestartGate.Tick` from the composite `Input` (`IsJumpPressed || IsSpecialPressed`);
the gate is fed only from a new optional `RunSession.RestartGesture` (`IGameInput`, null =
button-only). The `"tap anywhere or press space"` hint is gone (Space in the Editor no longer
restarts either — click RUN AGAIN). Comments rewritten in `RunHud` (events, `RevealSeconds`,
`HoldRun`, the Quit link), `RunSession` (gate field, the result-card branch, `Crash`,
`RequestRestart`), `RestartGate` (class note: who may ask, why never immediate) and
`GameBootstrap`. `RestartGateTests` rewritten to the new contract, +3: a hop held across 11
frames starts the run only once it has ended; with no gesture nothing but the button starts a
run (the tap-anywhere test's replacement); a hop landing with the click buys one run after the
hop. **`HoldRun` (SHARE) is kept**, not simplified: on a tilt card it now guards nothing, but on
a camera card the face keeps being read across the share sheet's round trip, and holding the
loop for the tap's frame and the post-focus window is the cheap belt against a re-acquired face
being read as a hop — not clearly dead, so it stays with its comment rewritten to say exactly
that.

**Camera mode: hop to run again, panel visible.** `RunFlow.StartRun` sets
`_session.RestartGesture = _faceInput` (the same `CameraFaceInput` instance inside the
composite — read by the session, never ticked twice) and null in tilt mode; `DropCameraMode`
clears it, so a run that fell back to tilt gets a button-only card. Found while wiring it:
`FaceSteering.IsJumpActive` holds for `JumpWindowSeconds` = 0.18 s (~11 frames) and
`RunnerController` reads jump as a level, so the gate's one-frame deferral would have let the
restarting hop jump the new run on its first frame. `RestartGate.Tick(dt, gestureActive,
overlayOpen)` therefore HOLDS a queued run (however it was asked for) while a gesture is in
flight and starts it on the first gesture-free frame; the post-crash lockout (`RevealSeconds`)
and the open-menu drop are unchanged. On the card: `RunFlow.SyncOverlay` keeps the `FaceOverlay`
up when the session is not running as long as `_faceInput != null` (hidden when frozen, and on
any non-camera card), and calls the new `FaceOverlay.Place(onResultCard)` which moves the HUD
panel from its run slot (top-centre, −320) to `ResultPosition` (centre-anchored, +820: 20 px
above the card's top edge at +710, so +730…+910 — clear of the masthead and score it covered in
the PR #5 review, 50 px under the top of a 16:9 screen, well under the 6T's cutout); on the card
the lane bands follow the live axis instead of the crash lane (`_hasReportedLane` reset;
`LateUpdate` only reports while running). The card's hint slot under the card (the old
tap-anywhere line, 34 px paper, 72–132 from the bottom) now reads `stand where the phone can
see you · HOP to run again`, shown only when `RunHud.HopRestartAvailable` (set by `RunSession`
before `ShowResult` from `RestartGesture != null`) and only once the card is fully up. SHARE /
QUIT TO MENU / SKINS & SHOP are untouched (they never went through the gate).

**Desk render.** New `Editor/ResultCardReview.cs` (menu Veyro/UI/Render result card; batchmode
`-executeMethod MotionRunner.EditorTools.ResultCardReview.Render`) renders the card at 1080×2340
(the 6T) to `builds/result-review/result-{camera,camera-newbest,tilt}.png` with a rig-less
`FaceOverlay` placed on the card for the camera ones — the state a desk cannot reach on the
device without a person in front of it.

**Verified (desk).** Scratch `C:\scratch\veyro-audio` re-synced with `robocopy /E` (no
`Library`/`Temp`/`builds`), sha256 of all 14 changed files checked against the worktree,
`diff -rq Assets/` clean apart from the three `.ogg`s that landed mid-sync (re-synced), and
`ProjectSettings.asset` identical (gotchas 4/11). `Unity 6000.5.9f1 -batchmode -runTests
-testPlatform EditMode` (no `-quit`): **590/590 passed** — baseline 577 + `MusicDeckTests` 9 +
`AudioCatalogTests` 2 (now 9, both result loops resolve) + `RestartGateTests` net +2 (now 12);
`SeasonIntegrationTests` 16/16 still passes through its reflection call of `ShowResult(summary,
bool)`, which is why `HopRestartAvailable` is a property and not a third parameter. The two
generated `result_*.ogg.meta` (Streaming / Vorbis 0.7 / no preload, Android block identical) were
copied back. `ResultCardReview.Render` at 1080×2340 → `builds/result-review/result-camera.png`
(panel above the card, "no face" red glyph, hint under the card), `result-camera-newbest.png`
(NEW BEST pill, same clearances), `result-tilt.png` (no panel, no hint) — copied into this
worktree's gitignored `builds/result-review/`.

**Device (OnePlus 6T `ONEPLUS A6013`, 1080×2340, Android 13, adb `a085659b`; 25 Sep 00:36–00:50).**
Scratch re-synced with the icon files and `ProjectSettings.asset` (byte-identical to the
worktree, 17 hashes checked, `diff -rq Assets/` clean), `Library\Bee\Android\Prj` deleted,
27 GB free. `BuildAndroidDev` → **`builds/MotionRunnerDev.apk` 96,892,744 B (96.9 MB) vs 95.8 MB**
on 24 Sep: +1.07 MB for the two result loops (683 + 647 KB of Ogg, re-encoded at 0.7) and the
launcher icon. The phone carried a `com.ferrabled.veyro.run` signed with a different key, so it
was **uninstalled** first (`INSTALL_FAILED_UPDATE_INCOMPATIBLE`; the 6T's local prefs — bests,
streak, guide-seen — reset), which is why the first launch showed the guide and OneSignal's
"integration complete" in-app message. Driven with `adb shell input`; `adb logcat -d -s Unity`,
timestamps as logged:
- 00:48:15 `music start Audio/Music/run_04` (seed-picked; 4 tracks probed) → 00:48:19.022
  `play Crash` + `music stop Audio/Music/run_04` → 00:48:20.026 `play NewBest` (fresh install:
  first run is a best) → **00:48:21.048 `music start Audio/Music/result_best`** (+1.02 s after
  the stinger, as written);
- tap on the card's stage, well outside every button: **no log line at all**, and the
  screenshot 2 s later (`p2-result-tap.png`) still shows the card;
- RUN AGAIN 00:49:05.642 `play UiTap` → **00:49:05.646 `music resume Audio/Music/run_04`** —
  the run track came back from the parked slot under `result_best`, same frame, no restart;
- same seed, same crash, no record this time → 00:49:09.949 `play ResultJingle` →
  **00:49:10.964 `music start Audio/Music/result_lost`**; the card shows `best today 89` and
  the defeat pose (`p2-result-lost.png`);
- RUN AGAIN again → 00:49:37.536 **`music resume Audio/Music/run_04`** from under
  `result_lost` too.
- No `[Audio] missing clip` warning in the whole session; the only `E/Unity` line is the
  pre-existing AssetPackManager probe.
- Launcher (for the icon session, which verified the APK resources but not a device): HOME →
  app drawer → search "Veyro" → `p2-launcher.png` shows **Veyro Run with the new adaptive icon**
  (dark teal disc, cream ticket "V" mark) rendered by the 6T's Pixel-style launcher, plus the
  phone's own work-profile badged copies of the same icon.
Not exercised on the desk: the camera card and the hop restart — the 6T on a desk sees no face,
and a camera run auto-pauses after 1.75 s without one (`CameraOutage`), so it never reaches a
crash; the layout is covered by the `ResultCardReview` render and the behaviour by the human
steps below. The new `crash.ogg` was not judged for loudness here (no listen); no code-side
`volumeScale` boost was added.
Screenshots: `C:\scratch\veyro-audio\p2-{launch,result,result-tap,result-lost,launcher}.png`
(not committed).

**Needs a human device test** (the parts a screenshot cannot judge — install
`builds/MotionRunnerDev.apk`, volume up):
1. Cold launch: the menu loop is audible within a second; PROFILE → SOUND opens the panel; drag
   MUSIC to ~30 % — the loop drops WHILE dragging; let go, force-stop the app, relaunch: the panel
   reopens at ~30 % and the loop is quieter (persistence).
2. Drag SOUNDS to ~50 % and release: a tap sound plays at that level. Set it back to 100 %.
3. Tap through the menu — every button, tab, link and pill clicks; CLOSE / `< BACK` / QUIT play the
   lower "back" cue; nothing double-clicks (confirm/deny replace the tap on the same press).
4. TILT run: the menu loop crossfades into a run track (~0.8 s, no gap); coins chirp and climb in
   pitch while the combo counter climbs, dropping back after a 2.5 s gap; lane changes swoosh once;
   a tap jumps with a sound; the crash thuds and the music fades under the card; the jingle lands
   as the card fades in, and ~1 s later a quiet loop (`result_lost`) fades in under the card and
   keeps looping. There is no "tap anywhere" line: tap the card anywhere outside its buttons —
   nothing happens, the card stays.
5. RUN AGAIN twice on the same day: the SAME run track resumes mid-bar from under the result loop
   (no restart from the top; the loop fades out under it). QUIT TO MENU: crossfade back to the
   menu loop.
6. Pause (II): the music ducks to ~35 %; MUSIC OFF / SOUNDS OFF at the bottom of the card mute
   instantly and survive RESUME and a relaunch; the panel's sliders read MUTED for a muted channel.
7. Beat your all-time best: a fanfare INSTEAD of the jingle (the NEW BEST pill pops with it), then
   the brighter `result_best` loop instead of `result_lost`. Beat only today's best: jingle, NEW
   DAILY BEST, and still `result_best`.
8. Finish the day's first Daily Run and return to the menu: the streak cue plays once as the
   stamp appears; relaunching does not replay it.
9. CAMERA run, pause, RESUME, raise a hand: three ticks on 3-2-1 and a "go" as the run unfreezes;
   backing out mid-count plays no "go".
10. Purchases (Test Store dev build): a completed purchase confirms, a cancelled sheet is silent, a
    failed one denies; RESTORE PURCHASES with nothing to restore is silent.
11. CAMERA run to a crash, standing 2 m away: the YOU · CAMERA panel stays up ABOVE the card (not
    over the score or the pill) and `stand where the phone can see you · HOP to run again` sits
    under the card. Step out of frame: the glyph goes red / "no face"; step back in: it follows
    you. HOP: the next run starts as you land, and the runner must NOT jump on its first frame.
    Hop during the crash pose (before the card is up): nothing. Tap the card outside its buttons:
    nothing. Then let the camera give up mid-run (cover it until "camera stopped working" /
    "couldn't see you" and the pause menu drops to tilt): the card after that crash has neither
    panel nor hint, and only RUN AGAIN restarts.
12. SHARE on a camera card, come back from the chooser while standing in frame: the card is still
    there (the hold across the round trip).

## 2026-09-24 — Consolidated SDK device notes

Removed the redundant standalone device report and its references at the owner's request.
The 23 Sep entry below retains the results, restoration incident and evidence location;
`SDK_PRIVACY_RELEASE.md` retains the remaining checks. Verified reference removal and
`hob git diff --check`; documentation only.

## 2026-09-24 — Independent validation of PR #12 Copilot corrections

`copilot-pr12-validation` / T-021, T-022. Read the five original GitHub review comments,
the correcting agent's conversation and the actual working-tree diff. All five findings
are valid against PR head `52e5201`; no additional implementation defect was found in
the local corrections. Consent failure now attempts shutdown in a finally path, reports
and retries incomplete shutdown, prevents re-enable from bypassing the purge, and rolls
back an exception during partial enable. Confirmed against the pinned SDK that Android
shutdown stops its uploader before persisting and releasing the core.

The campaign change consumes attribution on the first Daily start while preserving its
completion context; Free Runs do not consume it. Ready subscribers are excluded from the
production offer without consuming eligibility, and development verification remains
once-only. Both stale setup statements were corrected. The new visit counter confines
Android-thread work to an atomic increment and is read at click/run time, avoiding a
delayed background-clear callback erasing newer attribution. Checked its proxy override
against [Unity 6000.5's implementation](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.5/Modules/AndroidJNI/AndroidJava.cs)
and the pause/stop distinction against [Android's activity lifecycle](https://developer.android.com/guide/components/activities/activity-lifecycle).

Evidence audit, not a new test run: all 44 recorded runtime source hashes match the current
files, and the development APK hash matches the verification record. Parsed both NUnit
XMLs independently: the full run had 524 passes and two EditMode harness assertion failures;
the four-case service rerun passed, leaving all 526 unique cases with passing final results.
The test harness change uses reflection to invoke the lifecycle method without Unity's
EditMode SendMessage restriction; it does not suppress the asserted behavior.

Limits remain explicit: activity-stop/permission-dialog callbacks are Android-only and
are simulated by a counter in the Editor tests, so native device verification remains
necessary. The 23 Sep phone APK predates these corrections. The PR is still open at
`52e5201`; the fixes remain local/uncommitted. This validation changed only the task/status
record, with no runtime changes, rebuild, phone calls, provider actions, commit or push.

## 2026-09-24 — PR #12 Copilot findings validated and corrected

`copilot-pr12` / T-021, T-022. All five findings are valid against the reviewed
snapshot; each has a corresponding correction:

- [Consent failure](https://github.com/ferrabled/Veyro/pull/12#discussion_r4087512235):
  shutdown is attempted even when consent revocation throws. The pinned Android SDK's
  Shutdown aborts its HTTP uploader and writes a local disk snapshot; it does not call
  the HTTP Flush operation. If shutdown itself fails, the saved choice remains OFF,
  app events stop, the UI shows pending/error instead of OFF, and shutdown is retried
  on an unscaled timer and focus return. Re-enabling cannot bypass pending shutdown or
  the next-enable purge. A partially failed enable is rolled back through the same path.
- [Permission-dialog pause](https://github.com/ferrabled/Veyro/pull/12#discussion_r4087512276):
  Android activity-stop callbacks advance a visit counter. A pause-only dialog preserves
  attribution; leaving the app (including while a dialog is open) invalidates it. The
  counter is read when a click/run occurs, so a delayed Unity callback cannot clear a
  newer notification. This follows Android's [pause versus stop lifecycle](https://developer.android.com/guide/components/activities/activity-lifecycle).
- [Repeated campaign attribution](https://github.com/ferrabled/Veyro/pull/12#discussion_r4087512318):
  only the next Daily Run consumes the campaign. Its completion keeps the copied fields;
  restarting or abandoning that run cannot attribute another one. A Free Run does not
  consume the Daily campaign.
- [Already-enabled notification offer](https://github.com/ferrabled/Veyro/pull/12#discussion_r4087512340):
  ready subscribers no longer receive the player offer; development verification stays
  once-only and available.
- [Stale setup status](https://github.com/ferrabled/Veyro/pull/12#discussion_r4087512369):
  both setup statements now distinguish implemented Daily routing from outstanding
  campaign deployment and device/public-build verification.

Verification: **526 unique EditMode tests verified** across the full suite and a focused
rerun. The first run passed 524/526; two new retry tests hit Unity's EditMode SendMessage
assertion. After correcting only that test harness, all four service tests passed (including
both previously failing cases); runtime sources were unchanged between runs. ARM64 Android
development build **passed**, version `1.0.0-dev.20260924-0013.nogit`, APK **91,915,116 bytes**,
with permissions identical to the 23 Sep development baseline. APK:
`builds/MotionRunner-CopilotReviewDev.apk`; test XMLs, build log and source/artifact hashes:
`builds/copilot-pr12-*`. Build/test inputs were checked for extra source files and all
819 source/configuration files were hash-matched in the isolated scratch before the build;
only its Android staging folder was cleared. `hob git diff --check` passes. Existing local
SDK/device-review edits were retained. The corrections are local and have not been committed,
pushed or posted to the review threads.

Needs human device test on the resulting build: with analytics enabled, open a labelled
Daily notification and enter camera mode through the OS permission dialog; verify its
next start/completion has the campaign and the following Daily Run does not. Repeat with
Home/app switching during setup: attribution must end. Also check normal analytics OFF,
relaunch and purge on re-enable. Native persistent shutdown failures are covered by fault
injection in Editor tests; if a real device cannot finish shutdown, the UI must retain its
retry warning until success or restart. Existing push-send/permission and dashboard gates
remain in `SDK_PRIVACY_RELEASE.md`; no phone, provider, campaign or release action is part
of this review correction.

## 2026-09-23 — Current combined SDK build installed; bounded phone checks complete

`phone-sdk-verification` / T-021, T-022. Built the current `52e5201` runtime snapshot in
an isolated Unity copy after matching 817 source/configuration files. Fresh focused tests:
4 Layers consent + 26 growth/push tests passed. ARM64 development APK built and installed
with `adb install -r`; original installation date/data retained. Version
`1.0.0-dev.20260923-2324.nogit`, 91,912,748 bytes. Permissions unchanged; no AD_ID/location
permission, advertising-ID or install-referrer implementation; stock Layers native hash
unchanged. The concurrent source edit was comment-only. No release guard was bypassed.

On Nord 2: explicit analytics enable, disable, restart while off and purge on re-enable
worked. Held files stayed byte-identical across restart; Layers native library was absent
from the restarted process maps. Purge removed the held queue, rotated both SDK identities
and increased discard count without increasing delivery count. Two real Daily Runs
completed (183 points, 7 coins, 63 m each); an attempted abort was correctly counted as the
second completion. Parent then paused/quit a third run with no completion marker.
Native transport counters advanced while enabled; Layers Events receipt and first-uploaded
batch fields are still unverified.

The cheaper tapping agent accidentally hit notification TURN OFF instead of Close.
Parent stopped it, detected both app preference and SDK opt-in changes, restored only
those two app-owned fields and verified the original values/subscription/token after
relaunch. Android permission remained false throughout; no permission request, Settings,
other app, purchase or equipment changes. Completed runs naturally changed history and
progress. Final game state: menu, analytics off, original reminder choices/equipment.
Restoration used a game-only force-stop and exact `run-as` edits to the two app-owned fields;
game reminder preference=1 and SDK opt-in=true were verified after relaunch, with OS
permission still false. Raw test/build and restoration evidence remains under ignored
`builds/device/sdk-20260923/`, including `push-choice-restoration.json` and `root-final-off.json`.

Remaining: owner-assisted notification permission/retry and real foreground/warm/cold
push tests, Layers dashboard/startup-payload confirmation, then final signed Play build
checks after the production XP-curve and release inputs are resolved. No push, campaign,
website deployment or Play upload was performed. BACKLOG/OQ21 retain these gates; this
does not mark sponsor-track/public-release acceptance complete.

## 2026-09-23 — SDK review reconciled with existing device evidence

`sdk-review-validation` / T-021, T-022 documentation follow-up to the forwarded SDK review.
Reconciled the release checklist and stock-SDK assessment with the 22 Sep stock-build
device evidence: baseline off, disable/relaunch and queue purge on re-enable were already
observed; first-uploaded-batch fields, Layers Events receipt and the combined OneSignal
flow remain open. Added explicit failure/migration checks and retained the production
XP-curve release guard (OPEN_QUESTIONS 23).

Corrected one overbroad adapter comment after checking the pinned upstream SDK: Initialize
starts an immediate remote-configuration GET, while the managed periodic event uploader
waits for its interval. The app gates initialization on the player's accepted analytics
choice; mocked SDK consent-order tests cannot establish real first-batch payload fields.
No runtime behavior changed. Verification: pinned upstream/cache source inspection,
existing build/device evidence cross-check and `hob git diff --check`; no fresh Unity
test/build run was needed for comments and documentation. The 515-test result belongs
to the earlier stock-build verification, not this review.

No phone, dashboard, website deployment or Play actions were performed. The existing
`phone-sdk-verification` BACKLOG claim was preserved. Its 23 Sep handoff reports a
prepared, syntax-checked harness and action sequence, with no device calls; that is not
a current-build test result. Next: coordinate and complete the device pass and dashboard checks, then verify
the final release artifact after the owner resolves the release inputs.

## 2026-09-23 — Judge access standardized on Google Play promo codes

`judge-premium-access` / T-035 documentation follow-up. Owner requested retiring the old
judge-login proposal and making Google Play promo codes the primary route. OPEN_QUESTIONS 10
is resolved in place; its number remains stable and DECISIONS.md remains owner-maintained.

Updated the RevenueCat runbook, catalog, Play setup guide and submission instructions/checklist
to use the three real product IDs (`season1.pass`, `skin.ember`, `skin.frost`), grouped unused
codes and the current Shop/Google payment-sheet redemption flow. The pass does not grant the
separate characters or higher-level XP. Access must cover judging, 1–13 October; the planned
promotion end date is 31 October, with issued-code availability checked at the quarter change.
The profile-enabled build can use its existing Player ID for manual RevenueCat support if
needed; no identity-switching feature is planned. Historical proposals below are marked retired.

Verification: documentation-wide search for old judge-login instructions and example identifiers,
cross-check against existing restore/resume/profile behavior, and whitespace/diff checks. No game
code or store configuration changed. Next: owner generates the product-specific codes; verify
both redemption routes, resume/relaunch and restore on a second Google account using the signed,
Play-installed build. Codes have not been generated or tested by this documentation task.

## 2026-09-21 — Stock Layers SDK migration (supersedes the patched integration below)

**Device test — 22 Sep (OnePlus Nord 2, Android 13, 00:09–00:18 WEST).** The pending
physical test of the consent lifecycle was run on `builds/MotionRunner-StockLayersDev.apk`
(installed with `adb install -r` over the existing profile — no uninstall, no data clear;
prior build `1.0.0-dev.20260922-0003.nogit` → `1.0.0-dev.20260921-2227.nogit`, firstInstallTime
unchanged, streak/season/hat intact). **All seven steps passed**; full evidence in the
`device_test` object of `builds/layers-stock-verification.json`. Baseline OFF: launched and
idled 25 s — no `[Analytics]`/`[LayersSDK]` line and `liblayers_core.so` never loaded, so the
SDK really is inert until the player opts in. Enable: `[Analytics] Optional analytics
initialized; advertising disabled.` preceded by **both** `[LayersSDK] Install referrer fetch
failed: java.lang.ClassNotFoundException: com.android.installreferrer.api.InstallReferrerClient`
and `[LayersSDK] GAID fetch failed: Object reference not set to an instance of an object.` —
the Gradle exclusion is confirmed to work at runtime, not just in the dex. Two real Daily tilt
runs were played to a crash; turning analytics off 12 s after the second one left
`events_shutdown_1790028883260` on disk holding `daily_run_completed` + `$sdk_health`, both
unsent, with `$health_consent_analytics: "denied"`, `$health_flush_allowed: false`, and
`$ad_personalization`/`$ad_user_data` = false on every event; `grep -ril 'idfa|advertis|gaid'`
over the whole SDK directory matched **nothing** at any point. Force-stop + relaunch while off
left both files byte-identical (md5 unchanged) and produced no SDK activity. Re-enabling
deleted the queue file, cleared `veyro.analytics.purge_pending`, and **rotated the device id
891407e2-5cca-4bb4-acc7-ba435ce0852b → 664aeacd-43f2-434f-8413-c6f9af6ce349** (anonymous id
rotated too); the SDK's own health record proves discard rather than delivery —
`hydrated_events: 2`, `dropped: {"reset_discarded": 3}` at 22:16:20Z while `delivered` stayed
at 6 and `batches_attempted` at 3. Analytics was left OFF and the phone on the Home screen with
the teal top hat and 5-day streak showing.

Two corrections to the entry below. (1) Storage: stock persists to
`/storage/emulated/0/Android/data/com.ferrabled.veyro.run/files/layers_sdk` — this project's
`Application.persistentDataPath` is **external app-scoped storage, not `/data/data/<pkg>/files`**,
so "the app's private storage" is imprecise (the folder is visible to the device owner via a
file manager or MTP); nothing Layers-related was written to the internal `files/` dir. Stock
also does create its own `layers_sdk` subfolder, so files are not literally mixed in with game
saves. Observed filenames were `identity_state`, `events_shutdown_<epoch_ms>` and
`super_properties` — no `events_snapshot` or `remote_config` appeared. (2) The one-shot
`veyro-layers` cleanup was **not exercised**: the previously installed build came from a
non-Layers branch, so no legacy directory existed.

Could not be verified here, and remains owner work: **Layers Events-screen receipt** (needs the
owner's dashboard) — what was observed is transport-level only, `batches_by_status {"2xx": 3}`
with `delivered: 6` while consent was granted, i.e. six events were accepted by the endpoint;
**no on-wire payload was captured** (no TLS interception), so the first-batch `idfa`/ad-consent
check was done against the persisted queue file instead; `layers_init_timing` was never seen in
a persisted queue (if emitted, it was inside those six delivered events); the OneSignal→Daily
measurement loop was not touched (no pushes sent); and the release flavour is still blocked by
OPEN_QUESTIONS 23. Screenshots and the pulled queue/identity files are under `builds/device/`;
device-side screenshots were deleted. No commit was made by this pass.

`feat-implement-tracks` / T-022. The owner approved replacing the locally patched
Layers SDK after reading the assessment, and the migration was implemented the same
day across three parallel tracks: the package swap plus Gradle changes, the adapter
and test rewrite, and this documentation/copy pass.

What changed. The embedded `com.layers.analytics` 3.3.2 package with `VEYRO_PATCH.md`
is gone; the project now references the official unmodified SDK by Git URL at upstream
commit `7d28dcda555ab3ab3f0901c6c6e77f8710613f4b` (tag v3.3.2, still the latest
upstream release — upstream has no LICENSE file and no releases/changelog). No modified
SDK source is redistributed any more. Advertising-ID and install-referrer collection is
now prevented at the project level: a `configurations.all { exclude ... }` block in
`mainTemplate.gradle` and `launcherTemplate.gradle` drops
`com.google.android.gms:play-services-ads-identifier` and
`com.android.installreferrer:installreferrer` from every configuration.

Key findings behind that choice. EDM4U is installed with auto-resolve on build, so the
stock SDK's `Editor/LayersDependencies.xml` would otherwise pull ads-identifier 18.1.0
and its AD_ID manifest entry. More importantly, RevenueCat's Android SDK already depends
on ads-identifier 17.0.1, so `AdvertisingIdClient` is present in today's release APK —
without the exclusion the stock SDK's automatic lookup would read a **real GAID on
Android 8–12** (minSdk 26); Android 13+ returns zeros without the permission. With the
exclusion the JNI lookups fail and return null, which is the behaviour upstream's own
README describes for the no-EDM4U case. Also found: the Rust core exposes
`consent_required`, `respect_dnt`, `cookieless_mode` and `requires_explicit_consent`,
none of which the Unity wrapper surfaces — recorded as an upstream feature request
(it would give "no events before consent" natively), not as something we use.

Behaviour changes that had to reach the copy. Analytics stays off until PROFILE →
GAMEPLAY ANALYTICS; on enable the adapter initializes the SDK and sets consent
analytics=true / advertising=false before any event can be transmitted, then identifies
with the separate random support ID. On turn-off it revokes consent and shuts the SDK
down, so unsent events can never be sent while analytics is off — but, unlike the patch,
they are not discarded at that moment: they are deleted on the next enable (deny consent
→ the SDK's `Reset`, which discards the queue and rotates the SDK device ID → grant
analytics consent) or when app data is cleared. The stock SDK always emits an
initialization-timing event (`layers_init_timing`) alongside `$sdk_health`; both are
covered by the existing delivery-diagnostics disclosure. Isolated SDK storage is no
longer claimed anywhere: stock persists in the app's private storage next to game data,
and the old `veyro-layers` folder is deleted once on first launch.

Documentation/copy updated in this pass: `PRIVACY_POLICY.md` and the byte-synced
`site/public/privacy/index.html` (Turning-it-off bullet, diagnostics sentence, header
note, Changes paragraph), `site/public/support/index.html`, the two
`AnalyticsPanel.cs` string literals, `STORE_COMPLIANCE.md`, `SDK_PRIVACY_RELEASE.md`,
`LAYERS_STOCK_SDK_REVIEW.md` (dated Outcome section), `OPEN_QUESTIONS.md` 22 and the
T-022 backlog line.

Verification (same evening, fresh scratch copy `C:\scratch\veyro-layers-stock`, evidence in
`builds/layers-stock-verification.json`). Unity resolved `com.layers.analytics` from the Git
URL at hash `7d28dcda…` (packages-lock source `git`, copied back to the worktree); the
resolved source has none of the three patch APIs. **515/515 EditMode tests pass**, including
the four rewritten `LayersConsentTests`. `BuildAndroidDev` succeeded (the release flavour is
blocked by the Season 1 XP-curve guard, OPEN_QUESTIONS 23, so the dev APK is the artifact;
manifest merge and dependency resolution are identical). EDM4U did add the stock SDK's
ads-identifier 18.1.0 + installreferrer 2.2 lines to the generated `unityLibrary/build.gradle`,
and the `configurations.all` exclusion (present in both generated Gradle files) removed them:
permission set **identical** to the release baseline, **no AD_ID**, **no ads-identifier
implementation and no installreferrer classes in any dex** (only RevenueCat's own type
reference remains, as before), `liblayers_core.so` sha256 unchanged. Artifacts:
`builds/MotionRunner-StockLayersDev.apk`, `builds/layers-stock-permissions.txt`,
`builds/layers-stock-editmode.xml`. Scratch drift after the build was Unity/EDM4U write-back
only (resolver lines in the template, an `APP_UI_EDITOR_ONLY` define, URP assets) and was not
copied back. The site was **not** deployed and no commit was made by this pass.

Next. Owner: confirm no advertising/CAPI destinations are connected in the Layers
dashboard; send the courtesy LICENSE/terms request to Layers (no longer a release gate,
since no modified source ships); run the combined-build device test of enable / off /
relaunch / re-enable with Events-screen receipt; then the Play Console flips per
`STORE_COMPLIANCE.md`. Nothing is released.

## 2026-09-21 - Main merge and stock Layers SDK assessment

Owner requested preserving both SDK commits, bringing this branch up to main, and
assessing the original SDK patch before replacing it. Merged `origin/main` at
`d888a44` into `feat/implement-tracks`, preserving OneSignal `acc646e` and Layers
`2edbd92`; backup branch `backup/sdk-before-main-20260921` retains the pre-merge tip.
Resolved five conflict files by retaining both features: SDK initialization and
main's camera state handling, all LFS rules, and both sets of documentation updates.
Renumbered main's Season XP question to Q23 and updated its references because the
SDK branch already used Q19-Q22.

Verification: no unresolved paths; staged C#, JSON, asmdef and Markdown whitespace
checks passed. Isolated Unity 6000.5.9f1 EditMode verification could not reach tests:
package resolution repeatedly timed out. Exact-version cached-package fallbacks
also stalled; only our isolated Unity process was stopped. No test pass or Android
build is claimed. Logs: `builds/sdk-merge*-tests.log`. Source files were hash-checked
against the isolated copy; repository package configuration was not altered.

SDK assessment: compared the three patched source files with upstream Layers
v3.3.2 at `7d28dcda555ab3ab3f0901c6c6e77f8710613f4b`. The patch was chosen for
analytics-only consent ordering, disabled advertising attribution, isolated
persistence and discarding unsent data on withdrawal; it was not needed for our
gameplay events. Stock SDK migration steps and privacy gaps are recorded in
`LAYERS_STOCK_SDK_REVIEW.md`. SDK code remains unchanged, as requested for this
assessment stage. Next: resolve the consent/attribution behavior with the owner
and vendor as needed, migrate the adapter and dependency together, then rerun
EditMode tests and verify Android permissions, offline withdrawal and real Events
receipt on the device. No privacy scope change or release approval was assumed.

## 2026-09-21 — Layers SDK, consent controls and privacy website; release gates remain

`feat-implement-tracks` / T-021 + T-022. Owner supplied Layers App ID
`app_4cf32e54fc359326`. The Layers project URL is the browser address of the open
project; useful for dashboard verification, unnecessary for SDK initialization.

Implemented a pinned Layers 3.3.2 adapter with a separate PROFILE → GAMEPLAY ANALYTICS
choice, off until explicit acceptance. SDK initialization waits for acceptance; off
revokes consent, cancels transport and clears only the dedicated SDK queue directory.
The separate random analytics support ID remains copyable for provider deletion.
Advertising-ID/install-referrer collection, advertising consent, automatic application
error reports and gameplay performance traces are disabled. Native SDK delivery-health
reporting is distinct: the public configuration returns health enabled, and the core
contains queue/delivery/drop/retry/consent counters. The policy and Diagnostics declaration
include these counters. No synthetic health probe or event was sent.

Real run starts/results and consented notification opens feed Layers. Versioned,
allowlisted campaign values follow a clicked visit into Daily runs; duplicate clicks,
abandoned runs, mid-run consent and expiry are covered by tests. Notifications route to
the Daily menu only when safe, never auto-start gameplay/camera. No Supabase identity
alias, custom purchase event, sender function or scheduled campaign was enabled.

Replaced OneSignal's default small/large sample graphics with the Veyro V vector.
The observed deny/retry continuation path now uses bounded Unity Android permission
callbacks. This addresses the suspected path; its physical deny/retry verification is
still pending, as are separate foreground/warm/cold notification checks.

**Verified:** 468/468 tests pass. Three SDK consent tests enter Play Mode with the
official native/network-free mock; initial test-only failures from using a persistent
MonoBehaviour outside Play Mode were corrected. Android ARM64 release-format APK built:
`builds/MotionRunner-SponsorSDK-release.apk`, 73,469,525 bytes (+972,600 versus the prior
OneSignal release check), SHA-256
`7f4b86a25e4e4664e7e384c533962ce04bf0560d8cea9d76346d7ff41b12a522`.
Permissions are unchanged from that OneSignal APK: no AD_ID or location permissions.
UnityPlayerGameActivity remains the launcher. Packaged Layers code matches the pinned
native library after Unity's debug-symbol stripping; ELF and ZIP 16 KiB alignment pass.
The compiled drawable contains the V path, and the vendor default large icon is absent.
246 implementation/resolved-build inputs match the scratch build. EDM4U's generated
Maven Central entry was synchronized to the source Gradle settings template.

This is an **uncombined verification APK**, existing versionName 1.0.0 / code 5 and the
keyless-shop local release flavour, not a Play upload. The subsequent development build
was intentionally cancelled in our isolated scratch project and deferred until combined
scope is confirmed; it is not recorded as a passing build. Scratch PlayerSettings were
restored from source. No phone operation occurred, and the owner's latest design build
and saved loadout remain untouched. No other agent/editor process was stopped.

**Website deployed and verified:** home, privacy, support and terms at
`https://veyro.ferrabled.com/` match the prepared files byte-for-byte. Current Cloudflare
version `0242994e-c899-4634-a3cf-c188218cf2f1`; privacy/terms effective 21 September 2026.
Copy describes separate push/analytics choices, IP-derived country/region, SDK delivery
diagnostics, US processing, queued data and separate provider deletion. The website
itself gained no tracking. `STORE_COMPLIANCE.md` contains the revised Play declaration
table; `SDK_PRIVACY_RELEASE.md` is the ordered owner/integration/release guide.

**Blocked for final release acceptance / needs human device test:** Q19 feature baseline
(latest design has uncommitted fitting fixes), highest uploaded Play versionCode and
production status; Q22 advertising-forwarding/account terms and missing upstream SDK
license confirmation for the documented local privacy patch. Exact next device steps
are in SDK_PRIVACY_RELEASE: default-off/no traffic, enable + Events receipt, real
start/result/abandon, targeted push with campaign data, foreground/warm/cold, opt-out
and no replay. No real Layers event receipt, combined build, signed AAB, Console change,
Play upload or hackathon experiment result is claimed.

Evidence: `builds/layers-verification.json`, final test XML/build log, compiled icon and
permission dumps, public SDK-config response and `builds/site-layers-deployment.json`.
Private API keys/keystore secrets were not read or requested. Public SDK config HTTP
200 / success is configuration evidence only, not proof of SDK ingestion.

## 2026-09-21 — review fixes + first device pass for the card / crash poses / share (feat-share-run)

A review pass over the three tracks above found two regressions and three deep-link gaps; all fixed
before the device pass:

- **A tap during the crash pose skipped the card.** The card now waits 1.0 s + 0.25 s fade, but
  `RestartGate.LockoutSeconds` was still 0.5 s, so the tap-anywhere restart fired before the card
  had been drawn. `RestartGate.LockOut(float)` takes a duration (never below the constant);
  `RunSession.Crash` passes `RunHud.RevealSeconds`. Test: `ALongerLockoutCoversTheWholeCardReveal`.
- **The CHALLENGE banner leaked into every later run**, and the pause menu's RESTART swapped the
  friend's track for a random one under the same banner. The banner is now set by
  `RunSession.StartRun` from the seed the run actually uses (`BeginChallenge(seed, target)`), and
  `RunSession.RestartSameRun()` re-arms the challenge for the pause-menu restart.
- **Deep link hardening** — `RunFlow.TryStartPendingChallenge` refuses a link whose content
  version / world id this build cannot generate (a lookalike track is not the sender's), never
  starts on the frame the menu appeared (QuitToMenu + auto-start used to land in one frame), and
  stands aside while a mode pick is pending (`MainMenu.HasPendingPick`). `ChallengeMessage.TryParse`
  accepts only `veyro://challenge` and `https://veyro.ferrabled.com/challenge[/]` and caps `v`/`w`
  at 32 chars. Tests added in `ChallengeMessageTests`.
- The card's character block no longer disables itself forever after one failure, and the defeat
  pose gets a side-on, floor-level framing (`RunnerPreview.FrameFallen`) — the first device shot
  showed a head looming at the bottom of a standing-height frame.

**Device pass (Nord 2, `adb install -r`, profile kept; permissions byte-identical to the previous
build, manifest gained the two challenge intent-filters):** wall crash → runner stopped dead and
down in front of the planter; hurdle crash → mid-air tumble over the hurdle with the confetti
cosmetic; card fades in after the pose with score, `best today …`, three tiles, XP caption;
SHARE opens the system chooser with `I scored 69 in Veyro Run on today's Daily Run (39 m) — can you
beat me? https://veyro.ferrabled.com/challenge/?s=20260921&v=greybox-1&w=greybox&p=69&m=daily&d=2026-09-21`;
Back from the chooser leaves the card up; RUN AGAIN afterwards starts a clean run; a
`veyro://challenge?...` intent fired mid-run parks until the menu and then starts a
`FREE RUN · TILT` with `CHALLENGE · beat 4 210` on the HUD. Not reachable by automation: a
**new-record** run (all-time best on this profile is 7 787) — the NEW BEST pill and the dance are
covered by the EditMode badge test and the `builds/crash-review/` render sheets only; a human
should beat a best once and look.

**In-depth QA pass (second agent, same device, build `…-2322`), 9/9 PASS:** planter and hurdle
poses; every card element; tap 0.3 s into the pose does NOT restart, tap on the visible card does
(no first-frame jump); SHARE chooser text, BACK keeps the card, double-tap SHARE safe, RUN AGAIN
after; deep link parked on the card and taken after QUIT TO MENU, taken within 34 ms at Home,
cold start into the challenge, RUN AGAIN after a challenge has no banner, `v=nope` refused with the
menu intact, a link fired during a live Daily run parks across two further runs and never touches
the Daily; pause → RESTART RUN replays the same challenge seed (`987654` twice in the log) with the
banner; SKINS & SHOP / QUIT / back exits; no new logcat errors; card animation at 60 fps
(`p50=p95=16.8 ms`). The game auto-paused correctly when another app took the foreground mid-test
and the parked link survived it. Evidence: `C:\scratch\qa\*.png` (not committed).

Found and fixed after that pass: **the fallen character was clipped at the stage's left edge** with
the hat mostly out of frame — `Death01` carries the hips a body length from the root, and skinned
renderer bounds stay at the bind pose, so a root-targeted camera missed it. `RunnerPreview.FrameFallen`
now fits the humanoid bones (+ cosmetic meshes) every frame while active, so the camera follows the
fall and settles on the body — verified on device: `1.0.0-dev.20260922-0003.nogit` (509/509 EditMode,
permissions unchanged, `adb install -r`, profile kept) shows the whole lying body with the hat in
frame. Phone left on Home and handed to the Layers consent-test agent. Follow-ups (T-044), not done: the chase camera hides much of the wall
slam behind the runner's own back (a short camera lift on `WallSlam` would show it); a challenge
run's card does not say whether the target was beaten and re-shares as a plain free run (UX, T-024
polish); the celebrate dance is a subtle groove — a livelier CC0 clip would sell the record better.

## 2026-09-21 — crash poses and result-card poses (feat-share-run, animation track)

**Why.** On a crash the runner used to freeze mid-stride with the `run` loop still cycling in
place; only the confetti cosmetic marked the moment. The owner asked for a death animation that
depends on what was hit (a wall → stamped against it, a hurdle → trips), and for the card's
character to dance on a new record or fall in defeat otherwise.

**What changed.**
- `RunSession.ResolveCollisions` now reports the `ObstacleKind` it hit; `Crash(CrashKind)` maps
  it through the engine-free `CrashKinds.For` (`RunSummary.cs`): `FullBlock` → `WallSlam`,
  `LowBarrier` → `Trip`. The two `BestBoard.Record*` booleans are captured instead of dropped and
  travel in `RunSummary` (`NewAllTimeBest`, `NewDailyBest`, `IsNewRecord`, `Crash`, `Seed`) — the
  card's badge and the share payload read them from there.
- `RunnerVisual.Crash(kind)` fires the cosmetic and plays `crashWall` / `crashTrip`; a `_crashed`
  latch ignores `Step` until `ResetPose`, and `Play` checks `Animator.HasState` so a controller
  without the new states keeps its pose instead of logging every frame. `PlayResultPose(bool)`
  plays `celebrate` / `defeat` for the card's preview; `PlayCrashEffect()` is the locker's
  FX-only replay (was `Crash()`).
- Clips: **Quaternius Universal Animation Library (Standard, CC0)**, one FBX under
  `Assets/Art/Quaternius/` (LFS), provenance + SHA-256 in its `SOURCES.md` and `docs/GAME_ART.md`.
  `Death01` ×1.7 = wall slam (stagger, fall onto the back, on the floor by 0.7 s), `Roll` ×1.15 =
  trip (forward tumble), `Dance_Loop` = celebrate, `Death01` ×1 = defeat. Imported and wired by the
  new `Assets/Editor/RunnerCrashBuild.cs` (menu `Veyro → Art → Update crash and result
  animations`), which `ParkArtBuild.BuildRunner` also calls so a full rebuild keeps the states.
  New `ParkAssets` fields `CrashWall/CrashTrip/Celebrate/Defeat`; `Runner.controller` gained the
  four states (Ember/Frost share the controller, so all three skins have them).
- Two things the review renders caught before any device saw them: with `keepOriginalOrientation`
  the Quaternius rest pose faces the other way, so the runner spun 180° at impact — root rotation
  is now baked to the body orientation; and the 0.33 s `Hit_Chest` flinch was far too subtle for a
  wall, hence `Death01` at 1.7×. The KayKit legacy 1.2 emotes (Cheer/Dance/Defeat) were tried and
  dropped: that rig has no foot bones, so no Humanoid avatar. `RunnerCrashBuild.RenderReview`
  writes the contact sheets to `builds/crash-review/`.

**Verified.** `RunnerCrashBuild.BuildAndReview` and the EditMode suite in the scratch copy
(`C:\scratch\veyro-share`, 6000.5.9f1): **506/506 passed** (3 new `CrashKindTests`). Copied back
only `Runner.controller`, `Park.asset` and the generated `.meta` files; `diff -rq` clean.

**Needs a human device test.** Crash into a planter (full block): the runner should stop dead,
stagger and fall backwards, no sliding or spin, visible ~1 s before the card fades in. Clip a
hurdle: forward tumble. On the card, a record run dances (subtle groove — consider a livelier CC0
clip later), any other run falls to the ground. Check all three skins.

## 2026-09-21 — run-over screen redesigned as a shareable result card (feat-share-run, hud track)

**Why.** Owner feedback on the old screen: too much information, badly presented. It was "RUN
OVER", a 140px score, one wrapped string carrying both bests + distance + coins + combo, an XP
line and three stacked slabs. Nothing about it was worth screenshotting.

**What the card is now** (880 × 1420 in the 1080×1920 design space, centred; rounded
`CosmeticPanel` surface with a warm paper gradient, on an ink scrim):

| band | contents |
| --- | --- |
| 26–68 | `DAILY · 2026-09-21` left, `TILT` right — 26px, dim. Metadata, not a headline |
| 78–112 | `SCORE`, 26px |
| 112–268 | the score, 132px bold, grouped `4 210` by `ChallengeMessage.FormatScore` — the same format the share text uses |
| 284–356 | **NEW BEST** / **NEW DAILY BEST** gold pill (460×72, ease-out-back pop on reveal, unscaled) *or*, when it was not a record, a quiet `best today 4 210` line. Never both |
| 372–908 | the player's own runner, live, on the locker's teal→cream wash. `RunnerPreview` (RenderTexture → RawImage, layer 30) with the equipped loadout; draggable to spin before sharing; `veyro` wordmark signed bottom-right of the stage |
| 926–1076 | three tiles: DISTANCE / COINS / BEST COMBO |
| 1092–1134 | the season-XP caption (`SetXpStatus`, still filled asynchronously by RunSession) |
| bottom 140–260 | `RUN AGAIN` (pink, 516×120) + `SHARE` (slot, 268×120) |
| bottom 42–114 | `SKINS & SHOP` and `QUIT TO MENU` as text links |

All four existing events (`RestartRequested`, `StoreRequested`, `QuitRequested`,
`PauseRequested`) and their RunFlow subscribers are unchanged. The multiline bests string is gone.

**Reveal.** The card now always waits `RunnerVisual.CrashPoseSeconds` (1.0s) before fading in over
0.25s, where it used to wait 0.35s only when a crash cosmetic was firing — the crash pose always
exists now. `CanvasGroup.interactable` and the "tap anywhere" hint both wait for the fade.

**The one new risk, and its guard.** SHARE is the first button that stays *on* the result screen.
The release that opens the Android chooser is the same `TouchPhase.Ended` `TouchTapInput` reports
as a jump, so `RestartGate` would have queued a restart and started it the moment the player came
back from the share sheet. RunHud now holds the run for 0.5s on a share tap (re-armed on regaining
focus) through a new `HoldRun` seam wired in `GameBootstrap` to `RunSession.Frozen` — the same
mechanism the pause menu uses: the session ticks input while frozen and returns before the restart
check, so the tap is consumed and dropped. Every exit (`HideResult`, `Clear`, the timer) releases
it, because a leaked hold would soft-lock the card.

**Seams added.** `RunHud.Loadout` (`Func<CosmeticLoadout>`, wired to `SkinService.Effective`) and
`RunHud.HoldRun` (`Action<bool>`), both set in `GameBootstrap`; `RunnerPreview.Visual`. The card's
preview is built on the first result, parked by `HideResult` (the panel deactivates, so
`RunnerPreview.OnDisable` stops its stage and camera — no GPU cost during a run) and destroyed by
`Clear()`, which releases the RenderTexture rather than leaking one per run.

**Verified.** `Unity -batchmode -projectPath C:\scratch\veyro-hud -runTests -testPlatform EditMode`
(6000.5.9f1, scratch copy per CLAUDE.md gotcha 4, hashes diffed against the worktree before and
after): **503/503 passed, 0 failed**. `SeasonIntegrationTests.CrashReveal…` was extended: both
reveal paths now start hidden, and a `NewAllTimeBest` summary must show the badge reading
`NEW BEST` where an ordinary one hides it. Note `-runTests` must NOT be combined with `-quit` —
Unity exits before the tests run and writes no results file.

**Needs a human device test** (OnePlus Nord 2 or any 20:9 phone) — exact steps:
1. Crash a Free run. The world crash pose must hold for a beat before the card fades in; the "tap
   anywhere" hint must appear only once the card is fully up.
2. Check the card's margins top and bottom on the phone's aspect — the card is 880×1420 and should
   clear the notch and the gesture bar with room to spare.
3. Confirm the character on the card is wearing **exactly** what the run wore: change skin, hat and
   back item in the locker, run, crash, compare. Drag the character — it should spin.
4. Beat the all-time best: the gold **NEW BEST** pill must pop in after the fade, and the
   `best today …` line must be absent. Beat only the daily best: **NEW DAILY BEST**.
5. Tap **SHARE**. The chooser opens; cancel it with back. **The result card must still be on
   screen** — if a new run has started, the hold regressed. Repeat while tapping SHARE twice
   quickly.
6. Tap **RUN AGAIN** immediately after returning from the share sheet: it must start a run and the
   runner must not jump on its first frame.
7. `SKINS & SHOP` and `QUIT TO MENU` must land on the menu without a run starting behind them.
8. Own a crash-FX cosmetic (e.g. `confetti`) and crash: the burst should replay on the card's
   character, not only in the world.

## 2026-09-21 — T-024: challenge a friend — share sheet, deep links, landing page (feat-share-run)

**What it does.** The result card's SHARE button now produces one line of text plus a link, hands
it to the Android share sheet, and a phone that opens that link starts the *same track* as a Free
run with the sender's score on the HUD.

Exact text (free run, new personal best):

```
🏆 new personal best! I scored 4 210 in Veyro Run (1 240 m) — can you beat me? https://veyro.ferrabled.com/challenge/?s=987654&v=1&w=greybox&p=4210&m=free
```

Daily run, no record: `I scored 4 210 in Veyro Run on today's Daily Run (1 240 m) — can you beat
me? …&m=daily&d=2026-09-21`. URL shape:
`https://veyro.ferrabled.com/challenge/?s=<seed>&v=<contentVersion>&w=<worldId>&p=<score>&m=<daily|free>&d=<dayLabel>`
— single-letter keys because chat apps truncate, values percent-encoded, `d` omitted for free runs.

**The round trip, end to end.**

1. `RunHud` (other session) calls `RunShare.Share(summary)` → `ChallengeMessage.Build` (engine-free,
   Track assembly) → `ShareSheet.Send` → `Intent.ACTION_SEND` + `EXTRA_TEXT`/`EXTRA_SUBJECT` wrapped
   in `Intent.createChooser`, started from `UnityPlayer.currentActivity` (still the accessor under
   Unity 6 GameActivity). Plain `AndroidJavaObject` JNI — no package added. Editor/desktop: the text
   goes to `GUIUtility.systemCopyBuffer` and the log. Nothing throws into the UI: every path is
   caught, and a failed intent falls back to the clipboard.
2. Someone taps the link. `Assets/Editor/AndroidChallengeLinks.cs` (an
   `IPostGenerateGradleAndroidProject`, modelled on `AndroidLaunchModeFix`) has added two
   intent-filters to `com.unity3d.player.UnityPlayerGameActivity`: `veyro://challenge` and
   `https://veyro.ferrabled.com/challenge…` with `autoVerify="true"`. No committed manifest, no new
   permission.
3. `DeepLinks.Begin()` (from `GameBootstrap`) reads `Application.absoluteURL` for the cold start and
   subscribes to `Application.deepLinkActivated` for the warm one, parses with
   `ChallengeMessage.TryParse` and parks the result in `PendingChallenge`.
4. `RunFlow.TryStartPendingChallenge` takes it **only at the menu** (never mid-run, never while the
   camera picker is staging) and starts a tilt run: `_session.Mode = RunMode.Free` +
   `RunSession.BeginChallenge(seed)` — the only RunSession change, a one-run seed override that
   `StartRun` consumes and that is ignored outright in `RunMode.Daily`, so **a link can never
   rewrite today's Daily Run**. `RunHud.SetChallenge(target)` (one new additive method, building its
   own label) shows `CHALLENGE · beat 4 210`; `QuitToMenu` clears it.
5. Everyone without the app lands on the new `site/public/challenge/` page: it reads the query
   client-side, says "Beat 4 210", offers **Open in Veyro** (`veyro://challenge?` + the same query
   verbatim, falling back to the Play listing after 1.5 s if the page is still visible) and a Play
   button. Riso styling reused from the existing pages; script is external because the site's CSP is
   `script-src 'self'`; `noindex`, no cookies, no analytics.

**Verified:** EditMode, headless, in a scratch copy (`C:\scratch\veyro-share`, robocopy `/E`, Unity
6000.5.9f1) — **503/503 pass, 0 failures**, of which **18/18** are the new
`ChallengeMessageTests`: score/link in the text, daily vs free wording, the record flag, URL escaping
of an awkward worldId (`park & ride/v2?x=1` → `w=park%20%26%20ride%2Fv2%3Fx%3D1` and back),
seed→URL→`RngState()` round trips for both modes, the scheme and https forms parsing identically,
refusal of non-challenge links (`veyro://shop?s=1`, no-seed, non-numeric seed), and the
PendingChallenge take-once rule. One compile error was found and fixed on the way
(`Application.deepLinkActivated` needs a void handler; `DeepLinks.Handle` returns bool). No APK was
built — the owner builds.

**Needs a human device test (two phones, or one phone twice).** Both phones need a build with this
change; the website page must be deployed first (see below) for step 3.

1. Play a run, crash, tap **SHARE** → the system chooser opens and the result card is still there
   underneath (the `HoldRun` freeze). Check the text: score, `(… m)`, "Daily Run" only for daily
   runs, the 🏆 line only on a new best.
2. Send it to yourself. The link must be the whole `https://veyro.ferrabled.com/challenge/?s=…`.
3. Open the link on the second phone → the landing page shows **Beat &lt;score&gt;** → tap **Open in
   Veyro** → the game launches, the menu appears for a frame and a **FREE RUN** starts with
   `CHALLENGE · beat <score>` under the mode line. Compare the first few obstacles with the sender's
   run — same track, or the round trip is broken.
4. Without the site (tests the app half alone), from Git Bash — note the inner single quotes, which
   keep the device shell from eating `&`:
   ```
   MSYS_NO_PATHCONV=1 adb shell "am start -a android.intent.action.VIEW -d 'veyro://challenge?s=987654&v=1&w=greybox&p=4210&m=free'"
   ```
   Run it three ways: **cold** (`adb shell am force-stop com.ferrabled.veyro.run` first →
   `absoluteURL` path), **warm at the menu** (`deepLinkActivated` → the run should start within a
   frame), and **warm during a run** (nothing should happen until you quit to the menu, then the
   challenge starts).
5. `MSYS_NO_PATHCONV=1 adb logcat -d -s Unity` should show `[DeepLink] challenge <seed>/… score=…`
   then `Run over. mode=Free … seed=<the same seed>`.
6. **The Daily Run must be untouched:** after the challenge run, QUIT TO MENU and start a normal run
   — the mode line must read `DAILY · <today>` and the seed in logcat must be the daily one.
7. Per standing compliance rule 3 (the manifest changed): build the release APK and diff
   `aapt2 dump permissions` + file size against the previous release. Expected: **identical
   permissions** (INTERNET + CAMERA), size unchanged but for code.

**Owner actions, neither blocking the code review:**

- **Deploy the site** — `npx wrangler deploy` from `site/` — *before* any build with a SHARE button
  reaches a tester, or shared links 404. Not deployed by this session, deliberately.
- **`assetlinks.json`** with the Play *app signing* certificate SHA-256, so the https link opens the
  app directly instead of the browser: **OPEN_QUESTIONS 24**, steps in `PLAY_CONSOLE_SETUP.md` §F.
  Shipping without it is safe — the link goes through the web page, which then opens the app via the
  custom scheme.
- Privacy policy: add the sharing section (`SHARE_COMPLIANCE.md` §8), bump both copies, redeploy.
  Listing may mention "challenge a friend" only in/after the release that ships it.

**Known, deliberate:** RUN AGAIN after a challenge is an ordinary free run with a fresh seed (the
override is one-shot) and shows **no** `CHALLENGE · beat …` line — `RunSession.StartRun` sets the
banner from the run's own seed on every start (*corrected 28 Sep: this entry first said the banner
stayed up, which was the leak fixed in the "review fixes" entry above*); the challenge run is
always tilt+touch (a link must not open with a camera-permission dialog); a challenge scores on the
free/all-time board and stamps no day, exactly like any other free run.

## 2026-09-21 — PR #11 review fix: a reroll can no longer be undone by an older profile read

`_profileRequest` ordered `LoadProfile` GETs against each other, but nothing else. A reroll is a
write that changes the same row those reads return, and it did not touch the counter: the
post-run XP refresh could have a GET on the wire that read the row before the new handle existed,
the RPC could return `BRAVO` and repaint the UI, and then the older GET would land, pass the
generation guard because it was still the newest read, and put `ALPHA` back. The same callback
clears `_xpRefreshPending`, so nothing re-fetched and the stale name survived until relaunch.
Deletion and import were already safe (`_userId` / `_dormantAfterDeletion` guards; import
re-issues `LoadProfile` itself) — reroll was the only unguarded mutation.

`RerollRoutine` now retires the read generation before writing `Current`. The dropped read is not
lost work: `_xpRefreshPending` stays set, so `RetryXpRefresh` re-reads the row and picks up the XP
*and* the new handle.

**Verified:** `AProfileReadInFlightDuringARerollCannotRestoreTheOldHandle` interleaves the two
against the existing fake-HTTP harness — 10/10 `SupabaseAdapterReviewTests` green with the fix,
and with the one added line removed the test fails `Expected: "BRAVO" But was: "RUNNER"`, i.e. it
reproduces the rollback rather than passing vacuously. On device (dev APK on the Nord 2, owner
authorized the live reroll) the reroll path itself works: DAPPER-JAGUAR-62 → BRAVE-EGRET-74.
Confirming the new handle *survives a later read* could not be done in that session — from
14:03 on, both the phone and the build machine stopped reaching supabase.co entirely (curl 28,
15 s timeouts; `supabase.com` itself times out from the PC, general internet fine), so every
profile GET failed and the header fell back to its offline placeholder. Nothing to do with this
change, but worth re-checking when the backend answers again. The race itself cannot be hand-
triggered: it needs a GET already in flight at the instant of the reroll.

## 2026-09-21 — PR #11 review fixes: honest season XP state and resolved equipped slots

Three automated review findings on PR #11, all real:

1. `SeasonService.Read` passed a literal `true` for `SeasonSnapshot.IsLive`, so on a fresh or
   offline install (no confirmed profile, no recorded XP) the home card drew `0 / n XP` as
   earned progress. It now reports `HasRecordedXp`, the same gate the season page already used.
   `SeasonProgress.NotLiveNote` lost its pre-T-025 wording ("arrives with the Season 1 update")
   and is now the single shared line both surfaces show: "Finish a run to start recording XP."
2. `SkinService.IsEquipped` compared against `CosmeticLoadout.Item(slot)`, the raw stored value,
   so a character's built-in accessories (Prism's Crown/Aurora/Comet) rendered on the runner but
   read as merely OWNED. New `CosmeticLoadout.Resolved(slot)` returns what is actually worn and
   drops ids that do not belong to the slot — Ember/Frost reuse the *character* id to stand in
   for their built-in FX, which is not a wearable trail or aura.
3. `CosmeticsPage.ChooseSlot` had the same raw lookup, so opening Headwear while wearing Prism
   auto-selected an unrelated first card. It now shares `Resolved`.

Knock-on handled in the same change: an implicit accessory is worn without being *claimed*, so
"wearing ⇒ button disabled" would have stranded the reward-claim route. The detail button stays
live as VIEW REWARD / VIEW SHOP whenever the worn item is not unlocked.

**Verified:** `Unity 6000.5.9f1 -batchmode -runTests -testPlatform EditMode` on this tree —
**484/484 passed, 0 failed**, including three new tests:
`ResolvedSlotsReportWhatTheCharacterActuallyWears` in `SeasonRewardsTests`, plus
`EquippedStateFollowsTheCharactersBuiltInAccessories` and
`SeasonCardStaysNotLiveUntilAProfileRecordsXp` through the real services in
`SeasonIntegrationTests`.

**Device-verified** on the Nord 2 through `BuildAndroidCosmeticQa` (isolated package — the real
game's install and save were never touched): with Prism equipped and the Aura slot never set, the
AURAS row opens on Comet and marks it EQUIPPED, matching the comet on the runner; an explicit Top
Hat still beats Prism's implicit Crown (Crown reads OWNED) and CLEAR SLOT leaves a bare head with
nothing marked equipped. For the never-recorded case a throwaway fixture variant (no profile, hook
reverted afterwards — `GameBootstrap` is unchanged in this commit) showed LEVEL 1 / 10, an empty
bar and "Finish a run to start recording XP.", the same line the season page renders. The
Ember/Frost case where the character id stands in for built-in FX could not be reached on that
phone — both slots were already set explicitly — and is covered by the unit test instead.

## 2026-09-20 — Owner confirmed OneSignal delivery/open; automation and Layers next-step plan

Owner received the targeted test notification and tapping opened the game. Recorded as
owner-observed success; no exact message ID, installed build or warm/cold state was supplied.
This closes the basic delivery/open question, not foreground suppression or every lifecycle
case. Owner reported OneSignal branding: Veyro small/large default icon replacement is pending.
The earlier denial/retry hang is still unresolved; no source change or new device call this turn.

Added `docs/REMINDER_AUTOMATION_PLAN.md`: complete the notification UX, integrate consent-gated
Layers events, carry campaign context from push to Daily start/completion, then use a protected
Supabase Edge Function and Cron for automatic OneSignal sends. Includes private server secrets,
dry-run/one-device test, send deduplication/cap, QA exclusion, attribution window and experiment
evidence. This is proposed implementation work; no function, secret, campaign or schedule was
deployed. Existing release-baseline question Q19 still applies to identity/release scope.

Verified current official OneSignal API/icons, Layers SDK/events, Supabase scheduling/secrets
and Layers category documentation. Requested the public Layers SDK App ID/project URL; private
keys stay out of chat. Updated setup/backlog/prerequisites and Q21 to reflect delivery success.
Next milestone: one branded push opens the Daily entry and real run events appear once in Layers.
Documentation-only verification: diff whitespace check; prior binary/test results unchanged.

## 2026-09-20 — T-021 Nord 2 device pass; permission retry defect and delivery gate

Owner authorized game-only device testing with no uninstall or changes elsewhere on the phone.
Installed the verified release APK and a newly built development APK over the existing game;
firstInstallTime remains 18 Sep 13:16:09 and existing profile, scores, streak and history remain.
Sent a device reservation to the other active game agent before testing. Each tap checked the
foreground package and install timestamp. Screenshots streamed to local builds; no phone files,
other apps, global settings, system Settings screen or notification shade were manipulated.

**Passed:** server-assigned subscription registration, FCM token readiness, contextual prompt,
Android permission ask after a tap, denial without blocking play, allow after game restart,
in-game off/on, once-only offer, subscription/opt-in persistence after restart, Daily tilt run,
profile/online-score loading, camera staging/cancel. Development/Test Store checks passed for
restore, simulated failed purchase, valid Ember purchase/unlock and restart persistence. No
real payment; sandbox Ember remains owned and original Frost selection restored.

**Failed / unresolved:** after denying the first Android permission request, a second Enable
in the same process stayed on PLEASE WAIT with no second native permission activity. Close
still worked; restarting only the game restored the request path. Root cause is unconfirmed.
Further negative diagnosis is **blocked on owner-controlled game permission state**: ADB revoke
of this game's POST_NOTIFICATIONS returned SecurityException, leaving permission granted.
Did not attempt a security workaround or navigate Settings outside the owner's scope. Q21
records the needed handoff. The existing shop also shows stale "store unavailable" text despite
working Test Store actions; noted separately for the ongoing shop redesign.

**Build:** BuildAndroidDev succeeded in the verified scratch copy with two native build workers.
`builds/MotionRunner-OneSignalDev.apk`, 88,380,680 bytes, versionCode 5,
`1.0.0-dev.20260920-2051.nogit`, SHA-256
`ac79659175f38d8a6f2502d242f3b4590d6cbbd0b9f8f34b826c8d07b8de2ac1`.
Verified Development flag and Test Store key presence; log saved in builds. The nogit stamp is
from the isolated scratch checkout; 183 relevant source/configuration inputs matched before
building. No source code changed; prior 451/451 EditMode results still apply. No game crash;
optional Play AssetPackManager lookup and OEM camera-provider warnings are recorded in report.

**Needs owner/dashboard test:** no signed-in OneSignal browser is available to the agent.
Supplied the exact subscription for a one-device Send test. Actual FCM delivery, foreground
suppression and background/cold/warm notification taps are not verified. Final phone state is
the development APK, notifications on, same one-PUSH subscription (SUBSCRIBED with token),
Frost selected and notification panel open. Test record: `ONESIGNAL_DEVICE_TEST.md`.
T-021 is not release-ready until the permission retry is fixed/retested and message delivery
passes; campaign/tags/Daily routing, public-build evidence and privacy/Console release work
are still outstanding. No campaign, policy deployment or Play upload was performed.

## 2026-09-20 — T-021 Firebase configured; Android onboarding prompt checked

Owner uploaded the Firebase service-account JSON directly to OneSignal and reached the SDK
integration step. Read both official Android and Unity prompts. Android is the push delivery
platform; keep the existing Unity SDK and its native dependency, with no second Kotlin/Java
initializer or package version. The public configuration for the supplied App ID now returns
a numeric `android_sender_id`, absent at the earlier check. P5 and OPEN_QUESTIONS 20 updated.
This establishes configured sender settings, not successful delivery.

Rechecked the existing APK against the new prompt: INTERNET, POST_NOTIFICATIONS and
`com.onesignal.core.activities.PermissionsActivity` are in its merged manifest. SHA-256 still
matches the previously verified build. Connected Nord 2 runs Android 13 with a development
versionCode 5 game installed; its signing certificate matches our versionCode 5 test APK.
Pulled the existing APK to ignored `builds/onesignal-device-before.apk` for signature comparison.
No device installation, app data reset, notification opt-in or message send occurred.

**Needs human device test:** phone availability was requested because another game-design
Android build is running on this computer. Leave that test installation intact until available.
Then install the existing compatible APK preserving data, open PROFILE → NOTIFICATIONS, enable
notifications, identify the exact subscription, and send a dashboard test to that device. The
existing release test APK's shop is intentionally keyless; use BuildAndroidDev for the automatic
vendor dialog and Test Store checks. No second Unity build was launched with only ~469 MiB free
RAM. Guide updated with this onboarding checkpoint and optional Google Analytics guidance.
No source code changed; prior 451/451 tests and successful release build remain applicable.

## 2026-09-20 — T-021 initial OneSignal Android integration (feat-implement-tracks)

Owner supplied the OneSignal App ID and asked to follow the vendor's minimal integration
prompt. Added Core + Android **5.1.15 Stable** (native **5.1.37**), selected from the official
release index; the earlier T-042 audit's 5.3.5 is the Current channel. Preserved RevenueCat
9.8.1 and EDM4U 1.2.188, enabled custom Gradle templates, added the native dependency,
notification resources and IL2CPP preservation. Supabase configuration/backend is unchanged.

The bootstrap initializes one notification service behind `IPushService`; Editor uses a fake.
Registration observes changes and reads cached state immediately, rejects local placeholder IDs,
and is distinct from OS permission, opt-in and token readiness. No OS permission request at
startup. Development builds have the vendor verification dialog after the guide; player builds
offer notifications after a completed Daily Run when back on the home menu. PROFILE →
NOTIFICATIONS allows enable/disable and copying a notification support ID. Foreground banners
are suppressed, remote in-app messages are paused, location sharing is disabled. SDK errors
leave gameplay available. This initial increment is anonymous per installation: Supabase UUID
linking, campaign tags and notification payload routing are still the next T-021 increment.

**Verified:** all **451/451 EditMode tests passed**, including 12 new prompt/registration-state
cases. OneSignal package import and C# compilation succeeded; lock changes are limited to the
two pinned OneSignal packages. Every scratch Assets/Packages/ProjectSettings input was hash-
checked against this workspace before testing; asset GUIDs are unique. Reports copied to
`builds/onesignal-tests.xml` and `builds/onesignal-tests.log`.

**Android verification: PASSED.** Unity 6000.5.9f1 built the ARM64 release APK targeting API 36
(minimum 26) in `C:\tmp\veyro-onesignal-check-20260920`, with `BEE_BUILD_THREADS=2` and
`-job-worker-count 2`. Output copied to `builds/MotionRunner-OneSignal.apk`: **72,496,925 bytes
(69.14 MiB)**; SHA-256 `5c9e8a3ab6ec21858bae79eb80f750438e6d6cf9d52f9adc7a5f9e14c9b60da9`.
`com.onesignal.OneSignal` exists in classes3.dex; the OneSignalAndroid IL2CPP bridge and the
owner's exact App ID are present in metadata; both notification icons exist in Android's
resource table (AAPT shortens their packaged filenames, so ZIP-name matching is not a valid
icon check). The launcher remains UnityPlayerGameActivity, launchMode **singleTop**. No AD_ID,
fine-location or coarse-location permission was added. Permissions/manifest/verification JSON
and the successful build log are under `builds/onesignal-*`.

The preceding baseline-only native build exited with code -1 while Windows had about 400 MB
free RAM, without a compiler error at the end of its log. A fresh same-tree size delta is not
available; compared with the last existing release APK (1 Sep, 70,752,715 bytes), this APK is
1,744,210 bytes larger. That older content baseline includes other differences and is **not a
pure SDK-overhead measurement**. The permission diff is recorded separately.

**Needs human device test:** validate FCM v1 in the OneSignal dashboard, install a compatible
APK over the existing app without clearing data, verify the once-only dialog and opt-in, target
one test subscription, then check background delivery, normal cold/warm tap, permission denial,
Android-settings revocation and in-app TURN OFF. Full ordered steps: `ONESIGNAL_SETUP.md`.
The public Android app configuration had no FCM sender/project fields when checked; no live
delivery is claimed. No SDK-enabled build has been installed on the connected phone by this task.

**Release work:** privacy Markdown + site privacy/support/home text and the store flip table
are staged, not deployed. They disclose boot-time registration/session processing separately
from optional push delivery and explain the separate notification record's deletion path.
Owner must deploy these before distribution. T-021 remains incomplete until campaign/tag/routing
work, release-device checks and deployed campaign/public-build evidence are done. P5 is partially
met (App ID received; FCM confirmation pending); Layers account setup can follow independently.

## 2026-09-20 — T-042 deep SDK feasibility and owner setup guide (feat-implement-tracks)

Owner accepted the shared Daily Run reminder/measurement idea and requested a deeper integration
review. Added `SPONSOR_SDK_INTEGRATION_GUIDE.md`, updated P5/P6 with exact prerequisites and recorded
the unresolved release baseline in OPEN_QUESTIONS 19. The guide covers package installation,
bootstrap/asmdef seams, consent/UI, safe notification routing, a six-tag campaign, event semantics,
identity/deletion, build/device checks and the short handoff the owner should provide.

**Evidence:** inspected downloaded OneSignal 5.3.5 and Layers v3.3.2 tagged archives outside the
repo; verified tag commit IDs and OneSignal Android 5.3.5 npm availability. Our Unity 6000.5.9f1,
API 26/36, ARM64 IL2CPP and existing EDM4U meet published requirements. NDK llvm-readelf verified
Layers' supplied ARM64 library: 1,857,944 bytes, four LOAD segments aligned to 16 KB, all 62 C#
native binding names present in its dynamic symbols. This checks that library only, not a final
APK/AAB. Source review found Layers automatically enables diagnostics/performance and invokes
GAID lookup at init; consent has no initial config field, the README's thirdPartySharing example
does not match the two-argument API, and reset/shutdown are not deletion/opt-out shortcuts.

**Outcome:** feasible, with Layers collection/withdrawal behavior an explicit implementation gate.
No SDK/package/game changes, Unity import/build, campaign send, account mutation, or device test
were performed. The guide states these limits. Needs human device test **after implementation**:
follow §6 for consent, cold/warm/foreground notification taps, offline queue, identity where
applicable, camera pause/resume and real Play purchase/restore. Next: owner provides public App
IDs, validated FCM setup, release state/baseline and phone availability; implement T-021/T-022.

## 2026-09-20 — T-042 OneSignal/Layers readiness review (feat-implement-tracks)

Reviewed current official Shipaton requirements, vendor Unity SDK documentation and the repo's
packages, bootstrap, run completion, streak storage and release notes. Neither SDK is installed;
P5/P6 remain unconfirmed. Added `TRACKS_INTEGRATION_REVIEW.md` with a proposed Daily Run reminder
loop, Layers-assisted experiment, owner setup, adapter/event plan, device checks, schedule and
submission evidence. Corrected T-021/T-022 acceptance criteria: a deployed campaign and a
documented experiment are required beyond the old draft/dashboard checks. T-024 remains separate.

Material findings: the 16 Sep release plan keeps profiles out of the sponsor update although this
branch contains them; Layers can inject AD_ID and needs release-build attribution checks;
OneSignal collection disclosures extend beyond the existing push-ID note. SDK versions are
candidates, not tested pins. No package/code, campaign, account, policy deployment or store state
changed. Verified by source inspection and official web documentation; Unity tests/builds were
not run for this documentation-only review. Next: owner confirms OneSignal + FCM/Layers setup
and production state, then implement T-021/T-022. Future human-device verification steps are in
the review; no device integration is claimed complete.

## 2026-09-20 — T-025 softer Ember/Frost proportions

Owner feedback identified that the first fitting pass over-slimmed both premium models,
creating an elongated appearance. Softened model X/Z compression from 64%/70% to 86%/90%
and restored the uniform head-bone scale from 68% to 80%. The final height normalization
still matches the original Runner's bare-head height and foot level. This restores fuller
faces and bodies without changing gameplay size, movement, animation or colliders.
Regenerated both prefabs, their measured hat/back fitting data and all 27 thumbnails.
The original Runner's proportions and accepted cap fit are unchanged.

Updated the existing animation regression to compare head width against the fitted idle
head through run/jump/dodge, instead of requiring the rejected narrow head shape. Kept the
existing 1.30x Runner body-width limit; the fuller characters still satisfy it. All 481/481
EditMode cases pass (`builds/proportion-final-tests.xml`, 21:33 UTC). Four-angle geometry
reviews include all three characters and all hats/back items. Android development build
succeeded; APK permissions are identical to the previous fitting build. Whitespace check passes.

**Nord 2:** installed `1.0.0-dev.20260920-2334.47e4e3f` over the main game with data intact
(`builds/MotionRunnerDev.apk`, 89,619,030 bytes). Checked Ember and Frost with the cap in the
locker and real runs, including Frost's side/cape view. Three captured gameplay windows had
p50/p95 16.8 ms, CPU 5.82–6.34 ms and GPU 7.87–8.15 ms; one isolated >33 ms frame. No logged
runtime exception. This does not close the intermittent FPS investigation or long Tilt/Camera soak.

Before/after saved data confirms identity, claims, streak and original Ember + Charcoal +
Shadow + Cap + Confetti + cleared Back loadout are unchanged. Two runs raised XP 161 → 171.
Restored that outfit and verified cold relaunch. No uninstall, data clearing, purchases or
phone-settings changes. Left the game on Home, sent the coordinating agent a release notice,
and stopped device input. QA app was not changed. Evidence: `builds/cosmetic-fit/proportion-*`;
private preference backups remain ignored. Owner visual acceptance remains pending.

## 2026-09-20 — T-025 hat seating, proportions and back mounts (fitting follow-up)

Owner screenshots exposed contact/proportion defects that the earlier preview-framing tests
did not cover. Each character now has separate cap, top-hat and crown fitting data, measured
from posed upper-head sections and each hat's opening. The original Runner's accepted cap
fit is preserved. Top-hat fitting excludes its curled brim; the crown clears hair, including
Frost's squared hair corners. Ember/Frost caps sit lower and fit their reduced head size.

Adapted the two CC0 KayKit characters to slimmer proportions (head bone 68%, model X/Z
64%/70% before normalization), then matched the original Runner's measured bare-head height
and foot level. Existing Humanoid animation, the 1.45 presentation scale and gameplay collider
remain compatible. Frost's cape has reverse-facing geometry/normals (84 additional triangles,
same shared outfit material), so it remains visible from either side.

Back sockets now use the central torso surface and its slope, excluding projecting sleeves,
pouches and the cape. Quiver/shield face outward; the book presents its cover. Wings mount
at their roots. The quiver sits diagonally beside the neck, keeping arrow tips outside the head.
The locker uses a single-line RUNNER label with bounded font sizing. All 27 thumbnails were
rebaked. CosmeticFitReview renders 24 character/item combinations from four fixed-scale angles.

**Verification:** 481/481 EditMode tests passed at 20:48 UTC (`builds/fit-final-tests.xml`).
Six new cases cover actual deformed character height/foot alignment, proportions through
idle/run/jump/dodge, contact for all nine character/hat combinations, and the cape's inside
surface/restoration. Existing rotated-framing, inventory, commerce and determinism checks pass.
Android development and isolated QA builds succeeded. Final whitespace check passes.

**Nord 2:** installed `1.0.0-dev.20260920-2248.47e4e3f`, versionCode 5,
`builds/MotionRunnerDev.apk` (89,584,726 bytes). Permissions and launcher match the previous
design build. Checked the Runner's top hat/crown, Ember/Frost cap fit front/side, Frost's cape
from both sides, and all four back accessories. Ran Runner, Ember and Frost in the real app;
their lane silhouettes are now comparable. The separate QA build (`2255.47e4e3f`) also checked
Frost with cap/wings while running and jumping, and Ember with top hat/quiver during a run.
Premium collection happened only in the QA fixture; the main app's claims are unchanged.

The real profile, claims, streak and original loadout were compared before/after and preserved.
Three accepted real runs raised XP 158 → 161. Restored Runner + Charcoal + Shadow + Cap +
Confetti, Back cleared, and verified a cold relaunch. No uninstall, data clear or phone-settings
change. Main game left on Home; QA stopped. Phone released to the coordinating agent;
no background device monitor/input remains. OneSignal still belongs to the other branch.

**Performance:** four captured gameplay windows across main/QA had p50 16.8 ms, p95
16.8–16.9 ms, CPU 5.45–6.00 ms and GPU 7.78–7.92 ms. Two windows had two isolated
frames over 33 ms; two had none. No sustained 30 FPS state or runtime exception/shader error
was observed. The intermittent FPS investigation remains open; these are short runs, not a
long physical Tilt/Camera soak. Owner visual/feel acceptance and production XP-curve approval
remain the release checks already listed for T-025.

Evidence: `builds/cosmetic-fit/` contains four-angle sheets, phone screenshots, filtered
`main-performance.txt` / `qa-performance.txt`, permission comparison and private save backups.
Useful screenshots: `runner-tophat-device.png`, `runner-crown-device.png`,
`ember-locker-side.png`, `frost-locker-other-side.png`, `frost-quiver-side.png`,
`runner-run.png`, `ember-run.png`, `frost-run.png`, `qa-frost-wings-jump.png`,
`qa-ember-quiver-tophat-run.png`, and `home-final.png`.

## 2026-09-20 — T-025 fitted cosmetics, distinct characters and pass-first shop (refinement session)

Replaced Ember/Frost recolours with two different CC0 KayKit Adventurers 2.0 models:
Barbarian as Ember and Mage as Frost, retargeted to the existing idle/run/jump/lane-dodge
animations. Character identity now persists independently of dyes and accessories. Pass hats
hide native headwear; back items hide Frost's cape; Clear Slot restores those native parts.
Six-field saves migrate to seven fields without losing claims or equipped accessories.

Added a Back category with Wayfinder Quiver (Free Lv3), Moonbound Tome (Free Lv4), Sunshield
(Free Lv6), and Sky Wings (Pass Lv5, DevMops CC0 Angel Wing). These replace four dye rewards;
existing dye claims remain usable and recognize the replacement at the same XP/entitlement
milestone. Source URLs, original license and archive hashes are recorded in the asset folders
and SEASON_PASS_ASSETS. No paid asset tier, SDK, product ID or entitlement was added.

Cap meshes are centered on their opening and fitted per head. Hat previews frame the bill's
full rotation, tested at eight angles on all three characters. Back sockets are fitted to posed
torso geometry, including spine-weighted vertices in the original Runner's combined mesh.
The old ribbon and opaque ground disc are replaced by a 36-particle foot wake and a feathered
contact shadow. Both detail pages preview rewards on the currently equipped character.
The shop presents the season-pass purchase/rewards card first, then two large character cards,
locker previews and restore. SDK readiness now clears stale unavailable text and fetches prices
without discarding actionable purchase errors. Generated thumbnails cover all 27 catalog items.

**Verified:** 475/475 EditMode tests pass (`builds/cosmetic-final-tests.xml`, 20:08 UTC).
Tests cover save migration, legacy claims, entitlement revocation/restoration, character/accessory
composition, real meshes and Humanoid animation, rotated hat framing, bounded effects, shop
ordering, purchase cancellation and late store readiness. Nord 2 checks covered cap rotation on
Runner/Ember/Frost, wing and quiver previews/equipping, native cape/hat restoration, collection,
clear-slot persistence and cold relaunch. Both new characters ran with wings/cap in the isolated
QA app; the real app ran Frost with existing accessories and Ember with the earned quiver.
Real collection remained XP-gated; no purchase or fake entitlement/XP was added to the real app.
The real profile ends at 158 recorded XP. Profile identity and all previous claims are preserved;
quiver is additionally collected. Final selection: Frost + Charcoal + Shadow + Cap + Confetti,
Back cleared. Phone left on Home after cold relaunch. No uninstall, data clear or phone-settings change.

**Installed:** `1.0.0-dev.20260920-2208.47e4e3f`, versionCode 5,
`builds/MotionRunnerDev.apk`, 89,643,760 bytes (+907,984 versus the prior design APK).
`adb install -r` succeeded. Permissions and GameActivity match this branch's baseline.
This is the design branch's RevenueCat Test Store build; OneSignal work remains in the other
agent's separate branch and is not integrated into this APK. The phone was used only after
that agent's release and is released again. The separate QA package retains its labelled fixture.

**Performance:** actual running samples were p50/p95 16.8 ms, CPU 5.65–5.99 ms and GPU
7.70–7.85 ms; menus/locker were also around 16.8 ms. Two isolated >33 ms frames occurred in
one 300-frame running window; the other had none. No sustained 30 FPS state or new
NullReference/MissingReference/shader errors was observed. The development probe now uses
300-frame windows and separates active runs from results/transition screens, preventing static
results from being reported as gameplay evidence. Evidence: `builds/cosmetic-review/main-performance.txt`
and `qa-performance.txt`. The intermittent FPS issue remains open. Long physical Tilt/Camera
feel, real store commerce regression and production XP-curve approval remain human release checks.

Screenshots under `builds/cosmetic-review/`: `shop-final-device.png`, `wings-final-device.png`,
`runner-cap-device.png`, `cap-final-side.png`, `runner-back-fit-final.png`, `ember-run-final.png`,
`frost-run-final.png`, `home-final-device.png`. Owner visual acceptance is the next step.

## 2026-09-20 — T-025 horizontal pass and focused wardrobe (horizontal-season session)

Owner requested Rocket Pass-style horizontal rewards and a Pokemon Go-style locker with
larger, category-focused previews. Implemented a shared tier scroller (premium above free),
actual item thumbnails, current-XP rail, My Level recentering, explicit selected-reward
collection and direct navigation to that item in the locker. The locker has a large model
above a wardrobe sheet, six visual categories, an All/Owned filter and a three-column grid.
Detail pages use the full menu area; Back restores SHOP/RUN/PROFILE.

A shared preview eases between head, torso, full-body/effects and an angled trail view.
Dragging rotates the model; Confetti replays in preview. Static 256-pixel thumbnails are
baked from the existing assets, with no reference-game artwork or additional asset license.
Only the visible page renders its preview camera. Earned XP, entitlement/collection rules,
profile-scoped saves and the development/production curve boundary are unchanged.

**Verified:** 467/467 EditMode tests pass (`builds/horizontal-tests-polished.xml`),
including earned collection through the new action, exact-item locker navigation, locked
preview isolation, headwear framing and all 23 thumbnail assets. On the Nord 2, checked
horizontal swipes, My Level, free/premium selection, locked hat preview, smooth category
framing, drag rotation, vertical grid scrolling, All/Owned and empty-filter states, and Back
to the unchanged three-tab shell. Phone review led to a higher head/torso crop and a separate
rotation-hint label. Equipped the free cap through the new locker and verified it in a run;
that accepted Daily run moved real XP 104→105. Collected earned Matte Gold using the final
pass action and followed In Locker to its exact category. Restored the current Runner
selection; the game is left on Home. No purchase or fake XP/ownership was used.

**Installed:** `1.0.0-dev.20260920-1906.71e7c7f`, versionCode 5,
`builds/MotionRunnerDev.apk`, 88,735,776 bytes (+675,742 versus the previous season APK).
Installed with `adb install -r`; existing profile/claims preserved. Android permissions and
GameActivity match the baseline. Final screenshots: `builds/horizontal-review/pass-final.png`,
`pass-final-hat.png`, `locker-final.png`, `locker-final-body.png`, `home-final.png`.

**Performance:** pass/locker/Home and return-from-background samples remained around
16.8 ms p50/p95 (occasional 16.9 ms p95); no reproduced sustained 30 FPS state. No new
NullReference/MissingReference/index/shader errors found in the Unity device log. Filtered
frame evidence is `builds/horizontal-review/performance.txt`. The earlier intermittent FPS
issue remains open, and the existing human Tilt/Camera and real commerce release checks
still apply. Owner visual acceptance of the new portrait layout/camera motion is next.


## 2026-09-20 — T-025 season timeline, collection and cosmetic locker (season-pass session)

**Implemented:** server-recorded XP now drives a separate SeasonPassPage with a ten-level
free/paid timeline, current XP, next-level progress and explicit Collect actions after level 1.
Collection checks earned XP and the current entitlement; buying a pass never grants XP.
The existing level-1 starter rule is retained. Development thresholds are cumulative 0–9 XP;
production has a separate claims/loadout namespace and remains gated pending curve approval
(OPEN_QUESTIONS 23). No server migration or store product was changed in this session.

The Home character replaces the attract run and opens CosmeticsPage. SHOP/RUN/PROFILE stay
the three tabs. The locker supports Skin, Body, Trail, Headwear, Aura and Crash FX, with
preview/equip/clear and ownership revalidation. All 20 ladder items plus Runner/Ember/Frost
use the same RunnerVisual in preview and gameplay. Imported CC0 cap/top hat/crown meshes
are fitted to the real head bone; shirt finishes, bounded ribbons, 24-particle auras and
30-particle confetti are visual only. Physics, steering, scoring and track determinism are
unchanged. With Confetti, results reveal after a brief burst; score/XP recording is immediate.

**Review fixes:** accepted/duplicate submissions refresh the profile, including queued runs;
failed XP refreshes retry only the read, with backoff. Stale responses cannot roll XP back.
Claims and loadout are profile-scoped, survive relaunch and revalidate on entitlement changes.
Recovered server XP/store ownership permits re-collection; claims/loadout do not cloud-sync.
Accessories account for FBX bone scale; auras resume after hidden-menu activation. On-device
UI testing caught incorrect first-display geometry: each page now owns its Canvas batches
and raycaster, retaining the common shell scale/sorting. Cold locker, category switches,
timeline and repeated navigation were checked after the fix. Hidden previews stop rendering.

**Verified:** 464/464 EditMode tests pass (`builds/season-tests-final.xml`). The isolated
`Veyro Cosmetic QA` app uses an explicitly labelled 9-XP/FakeStore fixture, separate package
and preferences, with no RevenueCat/profile traffic. Collected rewards, equip, composed
Chrome/Crown/Aurora/Comet/Confetti, cold relaunch persistence and real runner rendering were
checked there. Crash-window captures show the trail and Confetti before the results card.
Three real-account Daily Tilt runs scored 153 each, synced, and moved the timeline from
101 to 104 XP. Cap, Shadow and Confetti were collected from earned free rewards; Mint +
Cap + Shadow + Confetti equipped and rendered in the final regular build. Paid rewards
stayed locked without the pass. No purchase, refund or restore was performed.
Evidence and APKs are ignored under `builds/season-review/` and `builds/`.

**Performance — still open:** added local development CPU/GPU/presentation timings, target,
refresh, render interval and focus/pause/surface logs. Samples during Home, timeline, locker,
runs/results and repeated background/resume+screenshot checks held p50/p95 about 16.8 ms.
The phone briefly reported 90 Hz at resume, then 60 Hz, while the game target stayed 60.
The earlier intermittent 30 FPS behavior was not reproduced; this is not a proven FPS fix.
No speculative frame-rate policy or phone setting was changed. Filtered evidence:
`builds/season-review/performance.txt`.

**Needs human device test / release follow-up:** collect an earned reward, tap the Home
character, equip it, and judge a ten-minute Tilt & Touch run including rapid lane reversals
and jumps. Repeat Camera mode with a person in frame, lost tracking, pause/raise-hand resume,
countdown and return Home. Exercise the existing real Test Store pass purchase/restore/refund
acceptance. Tune/approve production XP thresholds; confirm the existing backend rollout,
including migration 0008 for Free-run XP, plus T-009's declaration/privacy flip. Detailed
review and steps: `docs/SEASON_PASS_IMPLEMENTATION.md`. Website and T-018 icons/avatar are
unchanged.

**Final installed build:** `1.0.0-dev.20260920-1748.71e7c7f`, versionCode 5,
`builds/MotionRunnerDev.apk` (88,060,034 bytes; +919,850 bytes versus the previous art APK).
`aapt2` permissions match the baseline exactly; the Unity GameActivity launcher is retained.
Installed with `adb install -r`, preserving the real profile. Restored the owner's Frost
selection and confirmed it on Home after a cold relaunch. The separate cosmetic QA app
remains installed for paid-item preview/testing. Final screenshots, including 104 XP and
the free outfit in a run, are under `builds/season-review/main-final-*.png`.

## 2026-09-20 — T-006 runner/camera polish and T-025 asset research (feat-game-design session)

Owner likes the park direction and requested a larger character, lateral animation,
matching camera guide and free/commercial Season Pass sources.

**Changed:** runner presentation is 1.45× its previous size, scaled about the feet with a
matching ground shadow. Physics bounds, lane movement, jump timing, seed and scoring are
unchanged. Two Humanoid dodge takes from KayKit Character Animations 1.1's free CC0 download
now play on left/right lane changes, with a 0.22-second pose and jump priority. Horizontal
root travel is extracted and discarded so the animation cannot add a second lane move.
The source FBX is covered by the existing scoped LFS rule; its license is beside it.
The explicit editor command updates animation assets without rebuilding authored chunks.
The camera-position guide uses the shared paper/mint/teal/pink palette and a YOU · CAMERA
label; it still represents the person in the room. Tracking/thresholds/calibration are unchanged.

**Pass research:** `docs/SEASON_PASS_ASSETS.md` maps the existing ten free + ten paid reward
slots to downloaded CC0 hats, a crown, Kenney particles and the existing runner's outfit
materials. Includes creator links, download hashes, licensing evidence and authoring work.
Only the dodge source ships in this change. Pass hats/particles remain ignored research
inputs, not live rewards or APK content. No store products, entitlements or pass promises
changed; implementing XP/unlocks/locker/effects remains T-025.

**Verified:** 442/442 EditMode tests pass (`builds/runner-polish-tests.xml`), including feet/
scale/collider checks and both retargeted dodge clips moving bones without root travel.
`BuildAndroidDev` succeeds. Installed with `adb install -r` on the Nord 2, retaining app data.
Final installed version is `1.0.0-dev.20260920-1350.dc3a24d`; APK is 87,140,184 bytes,
+19,982 bytes over the 19 Sep art build. Android permissions match that build exactly.
No SDK/package, scene edit, purchase, restore or production deployment. `hob git diff --check`
passes. Real-device captures under ignored `builds/art-review/` show the larger runner,
jump, home and themed camera staging guide. Camera gate passed at median 3.7 ms and
briefly entered a tracked camera run; this is not a physical gesture/feel acceptance test.

**Performance finding — still open:** short fresh-launch samples of the updated build show
p50/p95 16.8 ms (~60 FPS), allocated Unity memory about 64.6–64.9 MB before camera use.
An earlier launch and one background/resume + screenshot sequence instead settled at
p50 33.6 ms (~30 FPS). Reinstalling/relaunching the same APK restored 60 FPS. A later identical
resume/capture check stayed at 60 FPS, as did the 19 Sep APK comparison. The cause is not
isolated; do not describe this as a fixed or proven pre-existing issue, or claim an
unconditional 60 FPS pass. The final updated APK was left installed and foregrounded at
60 FPS. The bounded investigation made no speculative frame-rate/system-setting changes.
Filtered evidence is `builds/art-review/runner-polish-performance.txt`. Next profiling step:
log target frame rate, frame timings and focus/pause transitions while repeating this
sequence; isolate the presentation changes only if the drop can be reproduced reliably.

**Needs human device test / acceptance:** rapid left/right reversals and lane changes during
jumps, then a ten-minute Tilt & Touch run; judge the new size/obstacle clearance and dodge
feel. Repeat Camera mode, verifying guide readability in tracking/lost/too-far states,
pause, raise-hand resume and countdown. Check resume from Home for smoothness. The
performance finding and the ten-second stranger-recognition check remain open for T-006.
T-018 menu icons/avatar and T-037 website figures remain their existing separate tasks.

## 2026-09-19 — T-006 park art pass built and installed (feat-game-design session)

**Working direction:** a sunlit sculpture garden, using the website's teal, pink and paper
anchors. Seven curated CC0 Kenney Nature Kit props plus one rigged Protagonists character
with run/jump/idle replace the capsule and greybox scenery. The 13 chunks now have paving,
foliage, distant rock silhouettes, water/bridges and pergolas, with coral dodge obstacles,
gold jump hurdles and coins. UI colors match the game; pause/countdown/results contrast was
checked. Tall obstacles use outward dodge arrows; low hurdles use upward jump arrows.
The website itself is unchanged; recommendation and source/license provenance are in
`docs/GAME_ART.md`. T-018's actual menu icons/avatar and T-037's website figures remain separate
unfinished tasks. Optional splash is deferred.

**Implementation:** ordered chunk ScriptableObjects preserve every original gameplay field.
Seed/content/world IDs, collision extents, scoring and input tuning remain unchanged. Scenery
uses an independent seeded PRNG; meshes are combined once per definition, prewarmed and shared
by pooled views. The humanoid's animation never controls physics/root movement. Owned cosmetics
color its shirt via the existing SkinService entitlement path. Curated FBX/PNG sources use
scoped Git LFS rules, configured before import; licenses stay beside them. No new package/SDK,
permission, scene content or production deployment. The explicit editor rebuild command can
overwrite authored art/chunk data, so GAME_ART documents that boundary.

**Verified:** 439/439 Unity EditMode tests pass (`builds/art-tests.xml`), including four new
checks for equivalent chunk fields/order, 10,000 generated selections across all difficulties,
valid moving Humanoid animation/size/loop settings, and URP-ready readable prop meshes. Android
development build succeeds and installs with `adb install -r` on the connected Nord 2, retaining
app data. Real-device captures show the animated runner, coins, garden, bridge, start/run/jump,
pause and crash/results. The last small contrast/arrow edits were compiled in the final build;
the full test suite preceded those presentation-only edits. `hob git diff --check` passes.

**Measured on device:** 600-frame attract-loop samples show p50 16.8 ms, p95 16.8–16.9 ms
(roughly 60 FPS). The first six consecutive samples held allocated Unity memory at 64.7–64.8 MB.
The final build's two saved samples show p95 16.8 ms, zero/one >33 ms frames and allocated
memory 64.7/65.1 MB across the start/play/pause/menu smoke check.
This is a short automatic attract-loop check, not a ten-minute played run or camera-mode
performance sign-off. Evidence lives under ignored `builds/art-review/`; performance logging
is local and development/editor-only. Android permission output matches the prior installed
APK exactly. No store purchase, profile deletion or account migration was performed.

Final installed build: `1.0.0-dev.20260919-2107.94f60e9`, generated by `BuildAndroidDev` at
21:10 local. APK is 87,120,202 bytes versus the prior installed development APK's 86,762,292
(+357,910 bytes, about 0.34 MiB). `phone-final-run.png` and `phone-final-jump.png` show the
final outward-arrow version on the phone; the visible art preview pane displays that run
capture. The device shell has no `screenrecord` binary, so evidence here is still captures;
the ten-second footage/recognition check remains a human step.

**Needs human device test / acceptance:** play Tilt & Touch for ten minutes, steering across all
lanes and jumping hurdles; check runner/obstacle readability, input feel and visible seams or
hitches as difficulty increases. Repeat in Camera mode, including pause/raise-hand resume and
the countdown. If Ember/Frost are already owned, equip each and verify only the shirt changes;
verify restore still updates it in the licensed store build. Show a stranger ten seconds of
gameplay and ask what game/action they recognize. Owner approval of the park/runner and this
recognition test are still required for T-006 to become done. No final art-direction decision
was added to DECISIONS on the owner's behalf. Next: accept/tune this first biome, then use its
actual character in T-018/T-037 and capture submission footage.

## 2026-09-19 — Latest PR #10 Copilot review verified and fixed (copilot-latest session)

Checked the nine comments in Copilot review 5256642035 (19 Sep, 17:11 UTC, commit
51d2af5). All are valid; four describe the same bounded-offline-queue wording issue.

- Migration `0008_latest_review.sql` adds service-only transactional `delete_profile`,
  taking the recovery/submission lock and cascading profile/run/board deletion before
  auth removal. Auth/provider cleanup remains durable and retryable. Recovery-first
  returns a conflict to the old session instead of false deletion success; deletion-first
  prevents recovery. Recovery also refuses a deleted destination awaiting auth cleanup.
- Every accepted Free run now earns the existing XP grant exactly once. It still creates
  no board rows or ranks; flagged submissions earn no XP. No historical XP backfill.
- Purchases, restores, paywalls and customer-info refreshes wait for identity settlement
  and hold that identity through entitlement application and the completion callback.
  A 15-second wait failure reports an error without starting a store operation. Refresh
  failure cannot be reported as successful purchase/restore entitlement synchronization.
- Blocked automatic recovery archives the foreign code separately, then issues the current
  profile's code. Restart and failed-issuance retry paths preserve both; account deletion
  clears both local code files. Disk errors preserve the original instead of overwriting it.
- iOS builds emit `ios` run metadata; Android retains `android_gp`.
- Privacy source/page, support and terms disclose the eight most recent pending runs,
  older-entry eviction, and server validation/limits. Privacy also explains archived codes.

**Verified:** reproduced missing Free XP and both blocked-recovery test cases before fixing.
435/435 Unity EditMode tests pass (seven new cases), including actual RevenueCat adapter
coroutines and SDK callbacks over its inert native wrapper, entitlement visibility inside
restore callbacks, deferred identity changes, timeout, and disk-backed recovery/relaunch.
12/12 Deno tests pass, including real migration/RPC execution in disposable PGlite PostgreSQL,
Free XP/replay/flagging and both serialized deletion/recovery outcomes plus deleted-destination
rejection. Edge Functions and cleanup script pass `deno check`. PGlite uses the existing
pgcrypto test shim; these tests do not simulate simultaneous hosted transactions.
The iOS tag is selected by compile-time guard; no iOS player build was run.

**Next / needs human device test:** apply migration 0008 after 0007, then redeploy
`delete-account` and `recover-session` together with the policy pages. Nothing deployed here.
On the licensed Android build: delete the profile, immediately tap RESTORE, and verify the
cosmetic appears before successful feedback; repeat with delayed/failed connectivity and
confirm retry feedback. Verify recovery-first deletion reports failure on the old install,
and deletion-first makes the old code unusable. Test a blocked foreign code on a played
profile: its own code must become copyable and remain so after relaunch. Finish a valid Free
run and verify XP increases once while both boards remain unchanged. These device/release
steps are still pending; existing T-009 rollout gates continue to apply.

## 2026-09-19 — T-009 Copilot findings verified and corrected (copilot-review session; uncommitted)

Checked all 17 Copilot comments on PR #10 against the implementation. All were valid;
the two flagged-retention comments described the same issue in the two policy copies.

- Profile deletion confirmation resets whenever the tab is shown. Board requests expose
  loading/error completion, and fallback rows stay explicitly labelled as samples.
- Restore feedback compares entitlement IDs, including equal-count replacements.
- RevenueCat identity transitions serialize login/logout, discard stale customer info,
  remember a stale successful login for the required logout, and retry failures without
  forgetting the applied ID. Anonymous SDK state is handled without a failing logout loop.
- Runs queued without a session now schedule draining with the session backoff. A pending
  recovery no longer prevents the drain from retrying that recovery. Duplicate responses
  preserve both ranks. Imports missing a rotated recovery key request and persist one before
  reporting success; issuance failure remains unsettled and retries during the same boot.
- Forward migration `0007_review_corrections.sql` checks app-version/platform replay drift
  and computes new-run/replay ranks from the board score actually retained. Score economy,
  XP rules, seed generation and board keys are unchanged.
- Recovery queues the old provider UUID transactionally before transferring the profile.
  Recovery/deletion functions retry provider cleanup before removing old auth users, retain
  failed jobs across account deletion, and preserve a successful recovery response even if
  cleanup throws. Owner retry script and deployment order documented in the profile plan.
- Corrected reroll/day-label contracts, current backlog count and the contradictory question
  headline. Both privacy copies now describe opportunistic flagged-run retention accurately
  and disclose pending cleanup identifiers; effective date is 19 September.

**Verified:** 428/428 Unity EditMode tests (17 new), including actual adapter coroutines with
only their HTTP leaf scripted: offline scheduling/drain, pending recovery, duplicate ranks,
import key issuance and retry. Menu regressions exercise the actual compiled menu classes.
9/9 Deno tests cover validator limits, provider success/failure/retry, and the actual SQL
migration chain/RPCs in disposable PGlite PostgreSQL. PGlite's unavailable pgcrypto extension
setup/random-byte issuance is shimmed only in tests; PostgreSQL SHA-256 remains real. This is
not a hosted Supabase/concurrency/device verification. Edge Functions and retry script pass
`deno check`; `hob git diff --check` passes. Nothing committed or deployed.

**Next / needs human device test:** owner applies migration 0007, deploys both changed Edge
Functions and the policy copy, and checks RC_API_KEY. On the phone: arm delete, switch tabs,
return and confirm two fresh taps are needed; go offline, finish a run, restore connectivity
without backgrounding and confirm it drains; import a profile and confirm a recovery code is
immediately copyable; buy/restore a licensed cosmetic across recovery/deletion and verify the
old RevenueCat identity is deleted and pending cleanup clears. Previously orphaned IDs are
not backfilled by 0007; include them in the already-planned pre-tester test-data cleanup.

## 2026-09-19 — T-009 owner decisions recorded (D13–D18), overflow + restore-honesty fixes, validator tests run (profile-integration session)

**Headline: the four owner calls this PR encodes are no longer proposals** — D13 (Supabase
backend, anonymous-first identity, recovery code + manual import), D14 (Supabase UUID =
RevenueCat app user ID, reset on delete), D15 (server-generated handles only; unlimited
rerolls per the 17 Sep amendment) and D16 (Daily + All-time, keyed and split standard vs
camera) are in DECISIONS.md, together with **D17 score formula FROZEN** and **D18 CAPTCHA
deliberately off**. OPEN_QUESTIONS 6, 15, 16 and 17 struck; **18 (attestation / replay
validation) is tracked as T-041 at the owner's request** — T-009 has no open questions left.

**D17 is binding on three files at once:** the formula (`ScoreState`), `game_config`, and
`supabase/functions/_shared/validate.ts` now move together or not at all. Changing one alone
flags every honest run, because the server reproduces the coin economy.

**Code fixes:**
- **Links-card text no longer runs off the screen.** The recovery-code row and the post-delete
  message were clipped at the right edge on a 1080-wide device. First attempt (best-fit alone)
  was a no-op: `RuntimeUi.Label` ships `Overflow`/`Overflow`, and an unbounded line always
  "fits", so Unity never shrinks. The fix (`ProfilePage.ShrinkToFit`) sets `Wrap`/`Truncate`
  first, then best-fit with the DESIGNED size as the ceiling — short labels render exactly as
  before, long feedback shrinks or wraps inside its row. Verified on device.
- **RESTORE PURCHASES stops claiming success when nothing came back** (owner call, 19 Sep).
  A restore that reaches the store and finds no receipt still *succeeds*, so the old code said
  "purchases restored" over an unchanged, empty entitlement set — exactly the path a player
  takes after DELETE ONLINE PROFILE resets the store identity. `StoreCatalogView` now snapshots
  `ActiveEntitlements.Count` across the call (the adapter applies customer info before invoking
  the callback) and reports what actually happened: restored / already unlocked / nothing to
  restore.

**Privacy copy:** the "Data stored on your device" section now discloses that the recovery code
is written to the app's storage folder and, on some Android versions and devices, **survives an
uninstall** — observed on the Nord 2, and the reason reinstall recovery works with backup off.
Uninstalling is therefore not a reliable erase; the two deliberate removals (DELETE ONLINE
PROFILE, or clearing app data) are named. Applied to `docs/PRIVACY_POLICY.md` and
`site/public/privacy/`; the support page's deletion section now also says deletion removes the
device's recovery code. OPEN_QUESTIONS 16 answered: ship as drafted.

**Verified:** 411/411 EditMode; **3/3 Deno tests** for the shared validator (`deno test
supabase/functions/_shared/validate.test.ts` — Deno installed on the dev machine 19 Sep, so the
"not installed here" note in that file is gone); device pass on the Nord 2 for both layout
fixes. Batchmode left no settings-asset churn.

**Needs a human:** (1) **purchase restore after deletion** — the owner will verify it in the
internal release: buy as a license tester, delete the profile, tap RESTORE, confirm the skin
returns. The Test Store cannot answer this (no real receipt). (2) Redeploy the keep-alive
worker. (3) Wipe test debris before testers — device testing left several throwaway profiles
and auth users, including one minted by a direct `recover-session` probe.
(4) Nothing further on decisions — see the addendum below.

**Addendum (owner call, same day): attestation / replay validation is NOT a decision.** The
owner declined to record it as D19 — "not yet" is not "never" — so it is filed as **T-041**
instead: post-hackathon by default, pulled forward only if the schedule frees up.
OPEN_QUESTIONS 18 struck and now points at the task, which means **T-009 has no open
questions left**. Two things carried into the task rather than lost: the standing constraint
that no document, listing or pitch may call the board cheat-proof, and the observation that
"revisit if abuse appears" cannot fire on its own today — nothing alerts on an implausible
score — so the cheapest first step is a top-N sanity query before submission day.

## 2026-09-18 (later) — T-009 review findings FIXED: recovery state machine, complete deletion, retry/queue rework, privilege + validator corrections, website copy landed (profile-integration session)

**Headline: every R-finding in `docs/T009_SECURITY_PRIVACY_REVIEW.md` was validated and fixed;
migration `0006_hardening2.sql` + rewritten functions are DEPLOYED and live-verified.**

- **R1 (worst):** recovery is now a persisted state machine. The rules live engine-free in
  `RecoveryGate` (unit-tested): a foreign key on file = PENDING → it is never overwritten,
  submissions queue instead of sending (a submission would make the fresh user a non-empty
  destination and permanently block the claim), and retries continue across restarts because
  the key file itself is the state. Only definitive outcomes resolve: claimed (store rotated
  key), key-invalid (discard), destination-not-empty (keep file, stop blocking).
- **R2:** `delete-account` now also requests **RevenueCat customer deletion** (needs the
  owner-set `RC_API_KEY` function secret; response reports `provider_deleted`), the client
  resets the commerce identity (`IStore.ResetIdentity` → `Purchases.LogOut`), the service
  goes DORMANT after deletion (no call can mint a replacement account mid-flow — ProfilePage
  no longer refreshes the board there), and the player-facing scope says local device stats
  remain.
- **R4/R5:** submissions have their own throttle gate + scheduled drain loop (retries no
  longer wait for a profile load or restart; foregrounding kicks both); Retry-After respected
  up to 1 h (`RetrySchedule`, unit-tested — the 120 s repro is a test); profile-load failure
  now retries on later calls while the token stays valid.
- **R6:** global `ALTER DEFAULT PRIVILEGES` (the per-schema 0004 revokes could not subtract
  PostgreSQL's global PUBLIC EXECUTE) + a DO-block SELF-TEST inside 0006 that creates a
  disposable table+function and fails the migration if either is born reachable — it passed
  on the production role during `db push`.
- **R7:** rerolls stay UNLIMITED; a 2 s per-user burst throttle (and the same on key
  rotation) stops scripts without touching humans. Verified live: immediate 2nd reroll
  throttled, 3 s later fresh name.
- **R8/R9 + idempotency:** Free seeds use the signed-int32 contract (verified live with a
  negative seed); `readJsonBody` is a bounded BYTE reader (6 KB unicode body → 400, verified
  live); duplicate detection compares the FULL payload (drift → 409, exact replay →
  duplicate, verified live) and returns the stored flag reason (new `runs.flag_reason`).
  Committed `validate.test.ts` (deno — runs in CI/anywhere with deno; not runnable on this
  machine). Recovery now takes the same per-user submission locks as `process_run` for both
  sides of a claim (deadlock-ordered), and the fresh-destination check dropped the
  rerolls_left signal 0005 had frozen.
- **R3 (website):** the full copy is IN the site files now, all version-scoped ("the
  leaderboard update onward") so every page stays truthful whenever deployed: privacy gains
  the "Player profile and leaderboards" section + recovery-code paragraph; support gains the
  leaderboard FAQ and the real **`#delete` anchor** (in-app path, Player-ID email fallback,
  ownership verification, 30-day window); terms §2/§6 and the homepage claims updated ("no
  accounts/none" absolutes gone); `docs/PRIVACY_POLICY.md` in sync; effective dates 18 Sep.
  The app now shows a **copyable Player ID** on the PROFILE tab (the email fallback's
  identifier). "Anonymous = no personal data" wording removed everywhere: profiles are
  pseudonymous, deletable records. STORE_COMPLIANCE Data safety reconciled: **User IDs
  (Personal info) collected/required; App activity → Other actions collected/required/SHARED**.

**Live evidence (self-cleaned):** free negative seed accepted · payload-drift 409 · exact
replay duplicate · oversized unicode body 400 · reroll throttle · privilege self-test in-push.
NOT verified here: RC customer deletion (needs RC_API_KEY), deno tests (no deno locally), the
client state machine on-device (fresh build below), Auto-Backup reinstall (human-only).

**Needs the owner:** (1) create a RevenueCat **secret** API key and set it as a function
secret: `npx supabase secrets set RC_API_KEY=sk_...` (never in the repo) — until then
deletions report `provider_deleted:false` and the support-email fallback covers provider
deletion; (2) deploy the site (`npx wrangler deploy` from `site/`) no later than the T-009
release window — the pages are version-scoped and safe to deploy early; (3) at release: the
Data safety rows above + deletion URL `https://veyro.ferrabled.com/support/#delete` in the
Console; (4) commit; (5) device pass incl. delete flow + reinstall-recovery.

**Addendum (owner call, same day): the recovery code is PLAYER-VISIBLE and replaces
rename-verification.** PROFILE tab gains RECOVERY CODE (tap to copy, with the takeover
warning) and IMPORT PROFILE (paste a code → the existing recover-session claim; fresh
destination required, code rotates on success) — the manual new-device/backup-off rescue and
the support ownership proof in one credential. `IProfileService` gains `RecoveryCode` +
`ImportProfile`; site/policy verification copy updated (code primary, rename gone). Post-
deletion behaviour confirmed as designed: boards drop the rows instantly; the next launch
auto-creates a fresh profile (disclosed in policy — Play-compliant). RC-deletion-less restores
confirmed correct: receipts always restore entitlements and simply recreate a provider record.

## 2026-09-18 — T-009 independent security/privacy review: changes requested (profile-security-review session)

Actual adapter source compiled in a temporary .NET harness with scripted HTTP reproduced:
recovery-key overwrite after recovery 503; submission backoff bypass with a live token;
profile loading stranded after 503; queued retries not scheduled; a new profile created by
the board refresh immediately after deletion. Actual TypeScript probes also reproduced
valid Free seeds rejected and a 6,300-byte body passing the advertised 4 KB limit.

Additional findings: no RevenueCat deletion/identity-reset flow; future function default
privileges not fully revoked; unlimited rerolls lack throttling. CAPTCHA/secure storage/
strong anti-cheat remain deferred. Live privacy/support/terms still omit Supabase and retain
no-account claims; support lacks the advertised deletion anchor, and the proposed Player ID
fallback has no corresponding ID display in the game. Full website and Play Data safety
updates remain a T-009 release gate. Detailed fixes, limitations and owner checklist are in
the review. No application changes, deployments, Console mutations or live abuse tests.
Not rerun: Unity/device suite and database concurrency tests (local Docker engine unavailable).

## 2026-09-17 (evening) — T-009 DEVICE PASS on the Nord 2: full loop green against the live backend (profile-integration session)

**Headline: fresh `BuildAndroidDev` (82.7 MB) installed and driven via adb — the whole
profile/leaderboard loop works on the phone against production:** anonymous sign-in + trigger
minted **ARCTIC-KIWI-59** on the profile tab; **TAP TO JOIN** consent gate flipped to "finish a
run to land on this board"; a tilt Daily run (89, 39 m, 4c) crashed and **submitted through the
hardened `process_run` path — verified server-side** (`1. ARCTIC-KIWI-59 = 89` on today's
standard board) **and in-app** ("you are #1 on this board", own row accent-highlighted, streak
stamped, history row added); force-stop + relaunch restored the same profile (refresh-token
path); **reroll live on device** (→ GOLD-BEAVER-66, budget 3→2, board row renamed immediately).
Bonus evidence: logcat shows `_logIn` — **RevenueCat aliased to the Supabase UUID on-device and
the Test Store entitlements (season1, skin_ember) stayed active through it** (the D14 wiring,
seen working with real purchase data). No game-side exceptions (the boot-time
`AssetPackManager` ClassNotFound is pre-existing dev-build noise, logged before any profile
code runs). Batchmode/Gradle side-effects on settings assets reverted again (gotcha 11 class).

DB wiped by the owner beforehand (`delete from auth.users`) — all four boards verified empty
before the pass; cascade confirmed (the "leftover runs" in the Table Editor were a stale view).

**Still human-only:** camera run + forced fallback (needs a person in frame; fallback runs must
land on the standard board — force the give-up with `adb shell appops set
com.ferrabled.veyro.run CAMERA deny` mid-pause, restore with `… allow`), Auto-Backup reinstall
restore (recovery key), offline-queue drain, delete-profile tap-through (left alive so the
owner can poke at the live board; wipe test users again before testers). Worker note: cron is
`0 3 */3 * *` → first metrics appear after the next 03:00 UTC tick on a matching day — an
empty Metrics tab an hour after deploy is expected.

**Addendum (owner call, same evening): rerolls are UNLIMITED** — migration
`0005_unlimited_rerolls.sql` pushed live and probe-verified (5 straight rerolls, fresh names);
client link is now plain "new name" (no budget); D15 proposal amended in OPEN_QUESTIONS 15;
UGC posture unchanged (still generated-only). 404/404 EditMode after the change; fresh dev APK
rebuilt + installed on the Nord 2.

## 2026-09-17 (later) — T-009 security/reliability hardening: transactional backend, least privilege, resilient client — deployed and live-tested (profile-integration session)

**Headline: migration `0004_hardening.sql` + rewritten Edge Functions are DEPLOYED to the live
project (CLI was linked, owner had authorized testing) and verified with a live battery; the
Unity client gained real failure discipline; 404/404 EditMode tests pass.**

- **Transactional submit (`process_run` RPC):** per-user advisory lock; idempotent replay
  checked BEFORE quotas (a retry of a completed request succeeds even after the quota fills;
  same client_run_id + different payload → 409); quotas at SUBMISSION time counting EVERY
  stored attempt — flagged and yesterday-seed runs included (≤30/24 h, ≤10 flagged/24 h,
  min-interval ≥ duration×0.5); boards + XP in the same transaction as the insert; DB errors
  fail CLOSED. Old `upsert_board_score`/`grant_xp` dropped.
- **Transactional recovery (`recover_profile` RPC):** hash validated with the source row
  locked; attempts throttled atomically (5/h/caller, FK-cascaded on deletion); **destination
  must be a fresh profile** — a recovery key can never destroy a played profile; key rotates in
  the same transaction. `issue_recovery_key` → **`rotate_recovery_key`** (retry-safe; lost
  responses self-heal; superseded keys stop working). Key file now `<userId>:<key>` so stale
  keys are detected and re-rotated. Plaintext-by-design rationale documented in the plan
  (Keystore keys don't survive reinstall — encrypting would break the feature).
- **Least privilege:** column-level SELECT on profiles (hash unreadable, `select=*` → 403);
  `ALTER DEFAULT PRIVILEGES` so future tables/functions are deny-by-default; 4 KB body caps +
  hard numeric bounds in the functions. config.toml: `enable_signup` stays true (anonymous IS
  a signup) with email/SMS signup off individually.
- **Client (`SupabaseProfileService`):** refresh token dropped ONLY on 400/401/403 — never on
  429/5xx/timeout; transient submit failures (0/429/5xx) queue instead of dropping;
  Retry-After honored; bounded exponential backoff with jitter; session work serialized; a
  transient refresh failure no longer mints a new anonymous user over a live identity; expired
  unrefreshable session = "no session" (nothing sent with a stale token).
- **Keep-alive worker:** bearer header removed (apikey suffices — evidence: both forms 200),
  10 s timeout, failures now THROW (visible in CF metrics/cron events); "never pauses" claims
  corrected everywhere to "very unlikely; only the paid plan guarantees" (owner decision).
- **Live evidence (all against the real project, then self-cleaned):** privileged fns 404 to
  clients; direct writes 403; two-user isolation; rotation returns distinct keys and the old
  key stops recovering; claim migrates handle+XP+runs+boards and deletes the old auth user;
  nonempty destination → 409 with both profiles intact; **concurrent double-claim → exactly
  one winner**; duplicate submit → no double XP/boards; mismatch → 409; too_fast 429; flagged
  flood capped at 10. NOT live-tested (honest gaps): retry-after-quota-full replay (by design
  only), recovery throttle at 5+ attempts, CAPTCHA (deliberately off — OPEN_QUESTIONS 17),
  device behaviour (Auto Backup reinstall remains human-only). Cheating stays possible by
  design limits — OPEN_QUESTIONS 18; never call the board cheat-proof.

**Needs a human:** (1) **redeploy the keep-alive worker** (`npx wrangler deploy` in
`infra/keepalive-worker/`) — the deployed copy predates the hardening; check: CF dashboard →
Worker → Metrics/Cron events shows successful runs, failures appear as errored invocations;
(2) wipe test debris before testers: SQL editor →
`delete from auth.users; truncate public.recovery_attempts;` (DB holds only test data);
(3) commit (GPG); (4) device pass per plan §10 unchanged.

## 2026-09-17 — T-009 Supabase security research (supabase-security-review session)

Reviewed migrations, all three Edge Functions, Unity session/recovery/queue handling and
the keep-alive Worker against current official Supabase documentation. The shipped key is
a public `sb_publishable_` key; its presence in the APK/Worker is expected. Existing RLS and
function allowlisting are a useful baseline, not proof of the hosted configuration.

Findings to address before the profile release: flagged submissions have no effective
storage cap; yesterday's accepted runs escape the today-only daily count; rate checks race;
run/board/XP writes are not transactional; recovery lookup/claim is not atomic and permits
destructive claims onto nonempty destination profiles; recovery issuance/rotation can lose
the usable key on a lost response; transient HTTP errors discard queued runs/session state.
Client-provided scores and camera mode remain forgeable within plausibility bounds.
Default privileges for future objects and secure device credential storage need hardening.

Verification: source review plus two non-mutating live `ping()` calls, both HTTP 200/body 1
(apikey only, and the current apikey-plus-bearer form). No abuse/concurrency tests, dashboard
inspection, deployment, game changes or settings changes. Official guidance recommends
apikey-only for publishable keys; the current bearer form was not observed failing.
Free-plan keep-alive is not an official no-pause guarantee. Next: backend hardening and
staging negative/concurrency tests; verify hosted grants/RLS and anonymous signup limits.
CAPTCHA requires client integration before enabling it. Research delivered in conversation.

## 2026-09-17 — T-009 backend LIVE and smoke-tested end-to-end; one pgcrypto bug found + fixed; branch on f40816d (profile-integration session)

**Headline: the owner deployed the Supabase backend (P11 mostly ✅) and a live REST smoke test
from this machine verified almost the whole server: anonymous sign-in ✅ (handle trigger minted
`HAZEL-HARE-30`/`GRAND-CIVET-39`), `submit-run` ✅ (plausible daily run accepted, daily+alltime
rank 1), `get_leaderboard`/`get_my_rank` ✅, `delete-account` ✅ (true deletion). One real bug:
`issue_recovery_key()` failed with 42883 — hosted Supabase puts pgcrypto in the `extensions`
schema and the function pins `search_path = public`. Fixed in
`supabase/migrations/0003_fix_recovery_key_pgcrypto.sql` (functions now call
`extensions.gen_random_bytes/digest`; 0001 aligned for fresh envs). The recovery→claim path is
therefore still UNVERIFIED — retest after the owner runs `supabase db push` again.**

- Branch fast-forwarded 8894ec9 → **f40816d** (PRs #8/#9: v5 in production review, **Samsung/
  Galaxy dropped as D12** — plan §0 annotated; the schema's `android_galaxy` platform value
  stays as a harmless reserved tag). Conflicts resolved in OPEN_QUESTIONS (upstream took 14 →
  **T-009 decision proposals now live at 15/16, D-numbers shifted to D13–D16, score freeze
  D17**; all cross-references updated) and PREREQUISITES (P11 row updated to deployed state).
- `SupabaseKeys.cs` carries the real URL + publishable key (owner, 17 Sep — public by design).
- Keep-alive Worker simplified: URL + anon key are plain `[vars]` in wrangler.toml (both are
  public and committed anyway), so deployment is just `npx wrangler deploy` — no secret step.
- **Test debris to clean (owner, SQL editor, service role):** the smoke test could not delete
  its first two anonymous users (sessions are not retained here). Today's daily board carries
  one leftover row (`GRAND-CIVET-39`, 600). One statement removes both users and cascades:
  `delete from auth.users where id in
  ('a34e10fd-bf38-4a6b-97eb-d5b8763bdfe3','e1c1a181-8c7d-4cb9-adae-9eeebb55861e');`

**Needs a human:** (1) `supabase db push` (applies 0003), then say the word — the recovery
claim gets smoke-tested the same way; (2) `npx wrangler deploy` in `infra/keepalive-worker/`;
(3) the anonymous **rate limit** field only appears once anonymous sign-ins are on:
Authentication → Rate Limits → "Rate limit for anonymous users" (set ~20/hr); (4) run the
debris-cleanup SQL above; (5) commit the branch (GPG); (6) then the on-device pass
(`BuildAndroidDev`) per PROFILE_LEADERBOARD_PLAN §10 — the Editor uses FakeProfileService, so
the real backend is only exercised on a device build.

## 2026-09-15 — Release handoff prepared (play-release-handoff session)

- Created `docs/HANDOFF_PLAY_RELEASE_NO_PROFILES.md` in English; Play Console guidance and paste-ready answers must be in Spanish.
- Fetched remote refs: clean `feat/main-menu` at `16fd170` has identical tracked content to `origin/main` merge `8894ec9`; local `main` is stale. Profile work remains separate and untouched.
- Audited the main-menu website drafts against the public policy and commerce/menu code. Recorded transition wording, purchase analytics, restoration/deletion/persistence copy, and the unfinished purchasable season pass for the release agent to resolve.
- Verification: repository/source inspection and official documentation; no new Unity tests/build, upload, website edits or deployment performed. Owner still needs to confirm the highest Play versionCode, configure signing locally, decide pass availability, and perform the Play/device checks.

## 2026-09-09 — T-009 Unity half built: Social seam, Supabase adapter, live board in ProfilePage; branch rebased onto main (profile-integration session)

**Headline: the whole client half of T-009 exists and compiles — 404/404 EditMode tests pass
(391 + 13 new `SocialTests`), `RunSeedTests` untouched. The branch was fast-forwarded onto
`origin/main` (8894ec9, the menu merge) with the two predicted docs conflicts hand-resolved
(BACKLOG T-009, OPEN_QUESTIONS renumbering — after the 17 Sep ff onto f40816d the T-009
decision proposals live at 15/16). Owner created the Supabase project — connection steps are
PROFILE_LEADERBOARD_PLAN §9.**

- **`MotionRunner.Social`** (engine-free, mirrors Commerce): `IProfileService` (+`Profile`,
  `SocialError`), `RunSubmission` (snake_case fields ARE the wire format), `InputModes`
  (tilt/camera/camera_fallback + board grouping — the one place the 3 Sep fallback rule lives
  client-side), `PendingRuns` (offline queue, RunHistory-style single-string encoding, cap 8,
  corruption-tolerant), `FakeProfileService` (Editor + tests; consent gates, not-ready queues,
  becoming ready drains).
- **`MotionRunner.Social.Supabase`**: `SupabaseProfileService` on UnityWebRequest+JsonUtility
  (no new UPM package). Boot: refresh token → else anonymous sign-in + recovery-key claim →
  profile load → drain queue → one-time `issue_recovery_key`. Recovery key lives in a
  persistentDataPath file (Auto Backup carries it across reinstall; Keychain is T-032's job).
  `SupabaseKeys.cs` holds URL + anon key (committed-public pattern; **currently empty — owner
  pastes, §9**). PostgREST reads use the object accept header; every response shape is a flat
  JSON object (JsonUtility can't parse top-level arrays) — server functions were aligned to
  return 0/"" instead of nulls.
- **Plumbing:** `IStore.Identify(userId)` → `RevenueCatStore` holds it until configured, then
  `Purchases.LogIn` (fail-open); GameBootstrap wires profile→store on ProfileChanged.
  `RunSession.Crash` submits fire-and-forget (`SubmitToBoards`); quits/restarts still write
  nothing. `RunFlow.DropCameraMode` now tells the session (`NoteCameraDropped`) — the shared
  board files such a run as `camera_fallback`/standard while the LOCAL BestBoard still scores
  it as camera (Feature D). That asymmetry is deliberate and documented at both sites.
- **UI:** `LiveLeaderboard : ILeaderboardSource` (cached async source; keeps the mock's
  non-contiguous my-rank contract; stale-response guard). `ProfilePage`: handle + reroll on
  the player card, TODAY/ALL-TIME scope tabs (group follows last-used scheme), one honest
  bottom line (sample-data note / TAP TO JOIN consent ask — nothing submits before it / live
  "you are #N"), and DELETE ONLINE PROFILE (two-tap, immediate) in the links card. Mock stays
  the fallback whenever the live board has no rows yet.
- Batchmode side-effects on ProjectSettings/URP assets were reverted (gotcha 11 class — the
  URP global-settings runtime list got emptied by the import; not shipped).

**Verified:** EditMode 404/404 in this worktree (log clean). NOT verified — needs the owner:
paste SupabaseKeys + run the §9 CLI steps, then the §10 device pass (submits incl. forced
fallback, board splits, reroll, offline queue, delete, reinstall-restore both ways) and the
gotcha-10 release-APK diff (expect no new permissions).

**Needs a human:** (1) PROFILE_LEADERBOARD_PLAN §9 steps 1–4 (keys, `db push` + functions
deploy, anonymous sign-ins toggle, keep-alive Worker); (2) commit this branch (GPG); (3)
OPEN_QUESTIONS 15/16 → DECISIONS + score-formula freeze (6→D17, gates live boards); (4) §10
device test. The versionCode-5 T-020 flip release remains untouched and ahead of this.

## 2026-09-03 — T-009 redefined: Supabase profile + leaderboard; backend + docs landed (profile-integration session)

**Headline: T-009 is now "profile + shared leaderboard + run history on Supabase" — spec written,
owner decisions taken, and the whole backend exists as config-as-code on this branch
(`feat/profille-integration`). Unity work deliberately not started: it waits on the main-menu
rework being committed (see Needs a human).**

- **`docs/PROFILE_LEADERBOARD_PLAN.md`** (new, canonical): anonymous-first Supabase identity (no
  login UI), **recovery-key reinstall restore** (hash server-side, key in Auto-Backup/Keychain —
  the STATUS 29 Aug Auto-Backup resurrection is the existence proof), `Purchases.LogIn(uuid)`,
  generated handles only (no UGC), Daily + All-time boards split standard vs camera with
  `camera_fallback` demoted to standard (owner calls, 3 Sep — OPEN_QUESTIONS 15). Free on
  Supabase Free + Cloudflare free; 7-day-pause trap mitigated by a cron Worker.
- **`supabase/`**: migrations (profiles/runs/board_scores/game_config; `input_group` as a
  *generated column* so board grouping is enforced in the DB; one-row-per-user-per-board
  conditional upsert; RLS with **zero client writes**; leaderboard reads only via
  security-definer RPCs exposing handle+score+rank, LIMIT capped, JSON-object shapes for
  JsonUtility), Edge Functions `submit-run` (schema/allowlist/seed-date/plausibility/rate
  limits; soft-fail = flagged row, never a punished honest client; server-side XP),
  `recover-session` (claims the old profile onto the new anonymous user — no session minting,
  no custom JWT; key rotates on claim), `delete-account` (true deletion, cascades).
- **`infra/keepalive-worker/`**: Cloudflare Worker cron → `rpc/ping` every 3 days.
- Docs synced: BACKLOG T-009 rewritten (PGS/iCloud approach superseded, Sidekick unbundled),
  STORE_COMPLIANCE T-009 flip table rewritten for anonymous-first (deletion URL + Data safety
  scores; content rating unchanged — handles are not UGC; policy copy HELD BACK in plan §8, must
  not ride the versionCode-5 flip), PREREQUISITES **P11** (owner creates the Supabase project),
  OPEN_QUESTIONS 6 urgency bump (score freeze now gates boards) + 14/15, REVENUECAT_PLAN §3.3
  ID note superseded (Supabase UUID, proposal D14 after the 17 Sep renumber).

**Verified:** desk-checked only — no supabase CLI/deno/postgres on this machine, and nothing
was deployed (by design: owner-only). SQL/functions follow current Supabase docs; the coin-
economy score bound in `validate.ts` mirrors `ScoreState` exactly. EditMode suite untouched.

**Needs a human:** (1) **commit the main-menu rework on `feat/main-menu`** — this branch then
fast-forwards onto it and the Unity half (Social seam, SupabaseLeaderboardSource behind the
rework's `ILeaderboardSource`, ProfilePage extensions, run-end plumbing) starts; (2) P11
(Supabase project + anon key + keep-alive Worker); (3) move OPEN_QUESTIONS 15 to DECISIONS,
freeze the score formula (6→D17); (4) approve plan §8 copy.

Keep entries short: what changed, how it was verified, what needs a human. **Hard cap ~40 lines
per entry** — this file is read at the start of every session, so length here is context another
session doesn't get. Durable operational knowledge does **not** belong here — invariants go in
code comments, recurring traps go in the "Known gotchas" list in CLAUDE.md. Old entries may be
pruned once their content lives elsewhere (the 30–31 Aug five-entry arc was consolidated this way).

## 2026-09-16 — PRODUCTION ACCESS GRANTED · v5 submitted to production review

**Google granted production access** (Console: "Tu juego tiene acceso a producción"). The
application had been filed against v3, but access is account/app-level permission — it does **not**
bind which bundle you publish. Nothing auto-published.

Owner then created the production release directly, choosing the right bundle:

- **versionCode 5 (1.0.0)** — the RevenueCat build, promoted **from the library**, not rebuilt, so
  it is the exact artifact already reviewed on the closed track (STATUS 16 Sep, v5 entry).
- **Full rollout** ("Iniciar lanzamiento completo"), not staged.
- **Countries: 176 + rest of world.** US inclusion is the eligibility-critical one (CHECKLIST A3).
- **Managed publishing OFF** — it auto-publishes the moment review passes, so there is no forgotten
  Publicar button between approval and a live store URL.

**The 12×14 closed-testing gate is permanently cleared.** It was a one-time requirement to obtain
production access for a new personal developer account. It never applies again — to this app or any
future release of it.

**Consequence for every future update:** no new closed release, no re-application. The ladder is
now **internal testing (instant, no review) → verify on device → production release with that same
bundle → review (fast now the app is known) → live.** The closed track is optional from here and is
only worth using when tester feedback is actually wanted. This supersedes T-031's AC.

**Where this leaves the schedule.** First production reviews can take up to ~7 days, so live ~23 Sep
if it runs normally, against a 30 Sep deadline. That leaves roughly a week for ONE update — the
window for OneSignal (T-021) + Layers (T-022). Profiles/Supabase deliberately stays out of it
(account-deletion policy is a documented rejection trigger; see the accounts brief).

**Needs the owner:** P5/P6/P7/P9 account signups — every one is "minutes" and each gates a category.
P9 in particular blocks T-040, whose posts are already drafted, for the $30k #BuildInPublic category.

## 2026-09-16 — Samsung / Galaxy Store track dropped (docs only)

**Owner call: we are not entering the Samsung "Best App for Galaxy" category, and Galaxy Store is no
longer a distribution target.** Publishing an app *with in-app purchases* on Galaxy Store requires a
Samsung **commercial** seller account, which requires a registered business entity. We do not have
one, so the category's mandatory live Galaxy Store URL is unobtainable — not a scheduling problem
that more time would fix. This is the record of the decision; the planning docs no longer pursue
Samsung anywhere.

Stripped every Samsung/Galaxy/Fold reference out of the plan: both handoff specs (prize-matrix row,
sponsor-strategy paragraph, §11.2 category requirement, §11.3 blueprint line, multiplatform target,
seller-registration step, the Update-1 Galaxy submission and Update-2 Fold layout, the Fold-camera
and Galaxy-billing risk rows, the Fold hardware assumption, the §14 checklists, source S13),
PREREQUISITES (P3, H5, P10's Galaxy payout profile), OPEN_QUESTIONS (Fold-access question),
DEVPOST_ANSWERS (category row + answer block), CHECKLIST (C8, A2 wording, the slack note) and
LICENSING_REVENUE. BACKLOG keeps **T-033 marked dropped** so nobody re-adds it, pointing here.

**Schedule consequence worth flagging: Google Play is now single-threaded.** D11 had treated Galaxy
Store as the backup eligibility store if Play slipped; that backup is gone, so iOS via P2 (the
friend's Apple Developer account) is the only remaining fallback if Play review rejects. The
CHECKLIST slack note now says so.

No code, build or store-listing change — docs only. Nothing to verify in Unity.

**Next / human:** nothing blocking. If Play rejection risk starts looking real, P2's iOS path is now
worth testing earlier rather than later.

## 2026-09-16 — v5 (RevenueCat) SUBMITTED to closed-track review (release-revenue-cat session)

Owner sent the atomic package from Resumen de publicación: closed track "Alpha" release **5
(1.0.0)** (the verified AAB below) + Seguridad de los datos (purchase history + device/other IDs,
deletion URL /support/) + Datos de inicio de sesión ("Sí" — one-time products; English
no-credentials instructions) + content-rating redo (digital purchases = Yes) + new short/full
listing descriptions (the live listing's "crouch to slide" and "zero network requests" claims are
gone). Ordering held: the 15 Sep policy was live on the site before submission (fetch-verified),
and the internal-track install + purchase checks passed on device (owner, 16 Sep).

**State: sent for review — not published.** Production-access application separately "en
revisión" (submitted 14 Sep, ≤7 days per Console). No changes while review runs; testers update
only after it publishes. **Deliberately deferred (owner):** the 6 listing screenshots were not
refreshed — some may show the old mode picker; a listing-only edit later reviews without a binary.

## 2026-09-15 — RevenueCat closed-test release prepared (release-revenue-cat session)

`release/revenue-cat` = the main-menu merge `8894ec9` (= origin/main; no profile/Supabase work,
grep-verified). **391/391 EditMode green** (6000.5.9f1 batchmode on this tree). No game code changed.

- **Site + policy revised to effective date 15 Sep, undeployed** — the 29 Aug text described a
  purchases launch that never reached testers. Now: beta-vs-shop version transition; deletion no
  longer claims to prevent restoration + honest anonymous-non-buyer answer; Android Auto Backup
  hedges; offline = owned cosmetics stay cached; restore route SHOP → RESTORE PURCHASES; "no
  analytics" → "no gameplay analytics" with purchase stats disclosed; "trails" removed from sales
  copy. Full delta: STORE_COMPLIANCE T-020 (15 Sep block). **Deploy BEFORE testers get the build.**
- **Spanish Console guidance for this release** (products check, internal-test purchase checklist,
  Data safety / app access / rating answers, closed-track promotion) was prepared and given to the
  owner directly; it is deliberately not committed. The binding declaration record stays
  STORE_COMPLIANCE.md.
- **Owner answers, same day (OPEN_QUESTIONS 14):** versionCode **5** (Console max is 4);
  production access **en revisión** (submitted Mon 23:05, ≤7 days — wait for the email); **pass
  stays on sale as-is** (ladder is upcoming work; the row's honesty note stays).

**Artifact (owner-built 15 Sep 21:29, agent-verified):** `builds/MotionRunner.aab` — game content
byte-identical to `8894ec9` (the tested merge; later commits touch only site/docs), versionCode
**5**, versionName 1.0.0 (no dev stamp, not debuggable), 67.1 MiB, SHA-256
`DF703882D708585DAD9E08E2339E37B093A14453A973CD533B83D4B3BC8311AE`. Manifest: target SDK 36,
ARM64-only, `launchMode` singleTop, permissions INTERNET + CAMERA (features `required=false`) +
BILLING + ACCESS_NETWORK_STATE, **no AD_ID** (Advertising-ID declaration stays No), no debuggable
flag. jarsigner: verified, self-signed upload cert exp. 2054 (D5 keystore). IL2CPP metadata carries
the live privacy URL and the `goog_` Play key; no Supabase strings. **Cleared for the
internal-track upload** once the site deploy is live.

**Needs the owner:** review the site diff → `npx wrangler deploy` from `site/` (BEFORE testers get
the build); upload the verified .aab to internal testing, run the device/purchase checks, then the
atomic closed-track promotion with the Console flips (STORE_COMPLIANCE T-020).

## 2026-09-02 — feat/main-menu: three-tab menu, attract run, dev version line (T-016, T-017)

**EditMode 272 → 327 green** (Unity 6000.5.9f1 batchmode); `RunSeedTests` untouched.
**Device-verified on the Nord 2** — dev APK 69.9 MB, `versionName=1.0.0-dev.20260902-1634.5aeab74`,
full loop walked: menu → tilt run → crash → SKINS & SHOP → shop tab, and the finished run stamped
the day (streak 1, W filled) and wrote its history row. Owner-requested rework of the front screen.

- **`ModeSelectMenu` → `MainMenu`** (`Assets/Scripts/Menu/`, Assembly-CSharp, canvas 100 as
  before). Three tabs, Clash-Royale shaped: SHOP · **RUN** · PROFILE. The shell owns only the
  frame; every card is its own class placed by `MenuStack`, themed by `MenuTheme`, so reordering
  or restyling the screen is one call each. Adding a tab is a subclass + one table row.
- **Home tab:** season-pass banner (level + XP bar; XP is honestly labelled *not live* until
  T-025, the OWNED/LOCKED half is real today) · 7-day stamp card wired to a real local streak ·
  a transparent window · the two mode buttons.
- **Attract run** shows through that window: the real `TrackDirector` + `RunnerController`, a fixed
  showcase seed, difficulty pinned at ~2, steered by engine-free `AutoPilot` (Track asmdef, 14
  tests incl. "every library chunk is drivable"). Scores nothing, resolves no collisions, writes
  nothing. `RunFlow` owns its lifetime alongside the camera's — one owner of "is the track being
  driven". Camera pulls back to (0, 5.2, −8.4)/27° while the menu is up and is restored on handover.
- **`StorePanel` → `StoreCatalogView`** on the shop tab, logic unchanged (direct Play sheet for
  skins, paywall for the pass, fail-open). The result screen's button now *leaves* the run for that
  tab via the proven `QuitToMenu` path, so no overlay sits over a finished run and RESTORE is
  reachable cold in two taps. `StorePanel.IsOpen` → `MainMenu.IsOpen` at both call sites.
- **Profile tab:** real numbers (all-time best, today, streak, last 8 runs) via new
  `ProgressStore` + engine-free `MotionRunner.Progression` (`DailyStreak`, `RunHistory`,
  `SeasonProgress`, `MockLeaderboard`); the board is mocked behind `ILeaderboardSource` and says
  so on screen. `RunSession.Crash` now also stamps the day and appends a run record — only a
  *finished* run, so a streak cannot be farmed by quitting.
- **Back** gained `BackAction.MenuHome`: on a non-home tab back comes home instead of falling
  through to Android. Ranked below `CancelStaging`, inert inside a session (both tested).
- **Dev version line (T-017):** `bundleVersion` `1.0` → `1.0.0`; `BuildAndroidDev` stamps
  `1.0.0-dev.<yyyyMMdd-HHmm>.<sha>` for the build only and restores it in the existing `finally`.
  Shown under the title when `Debug.isDebugBuild`, so the `.aab` and the release APK cannot carry
  it. Also lands in `android:versionName` — `adb shell dumpsys package` answers "which build?".

**Four things only the device showed** (all fixed, second build re-verified): cards at alpha 0.92
let the attract run's lane lines and a red block read *through* the season-pass and streak text →
0.98 (scrim 0.90 → 0.97); the menu camera at 8.5 m made the road wider than the view and put a
side-lane runner two thirds of the way to the screen edge → (0, 5.2, −8.4)/27°; the demo runner
swept through coins and left them hanging → `AttractRun.CollectCoins` (obstacles still pass
through, deliberately); unstamped days were near-identical to the card → `MenuTheme.Empty` lifted.
Also dropped the profile's duplicate version line, and the mock board now ranks on the ALL-TIME
best — ranking on today's put the player last on their own profile every morning.

Still owed by a human: **camera mode from the new home tab** (needs a face in frame — the
staging/cancel path is unchanged code but was not walked), and a look at the attract run's pace
over a long idle.
## 2026-09-01 — hands-free flow: raise-hand resume, 3-2-1 countdown, per-scheme boards (pause-game-feature)

Implements the hands-free flow handoff (auto-pause fold-in, raise-hand resume, per-scheme boards;
the "pause gesture" stays "leaving the frame IS the pause", now taught in guide page 3). **EditMode 292 → 340 green** (`RaisedHandTests` 22, `ResumeCountdownTests`
13, `BestBoardTests` 13); pinned `RunSeedTests` untouched. **Release APK 55.5 → 67.5 MiB
(+11.9 MiB = the two pose models); permissions byte-identical** (aapt2, gotcha #10).

- **A (verify + fold-in)**: auto-pause confirmed landed. New: `RunFlow.OnApplicationPause(true)` →
  same `RequestPause` path (also cancels an in-flight resume wait); an outage auto-pause now goes
  **straight back into resume staging** — framing overlay up, camera looking, reason line on top
  ("couldn't see you — paused" / "camera stopped working — paused") — so the whole recovery loop
  is touch-free (criterion 1). Manual/background pauses still land on the idle card.
- **B — raise-hand resume**: camera resume = camera back → face held → "raise your right hand when
  ready — or tap RESUME" → 3-2-1 → run. `CameraInput/PoseGestureProbe` (BlazePose detector + LITE
  landmarker, CPU, ported from the T-010 spike): owned by `FaceTrackingRig`, reads the rig's own
  upright texture (never a second camera client), **alive only at `timeScale == 0`** — it logs
  `[CAM] pose probe up/down`, self-stops with an error if unfrozen, and is disposed the moment the
  countdown starts. Rule engine-free in `Pose/RaisedHand`, retuned on device 2 Sep: wrist
  (visibility-only — presence collapses at the crop edge mid-hold) above nose by 0.5 head units,
  OR the same arm's elbow strictly above the nose (hand out of frame); 3 NET ~3 Hz samples — a
  miss winds back one, a hard reset flip-flopped the prompt against a held arm. **The
  selfie-mirror mapping (player's right = model's `LeftWrist`) is pinned in tests**, and the
  overlay's raised-arm hint draws on the matching side. Touch + back work at every step (rule 3);
  a probe that can't load degrades to touch + countdown. Countdown engine-free in
  `Track/ResumeCountdown` (unscaled, hitch-clamped); the CARD HIDES for the count (2 Sep owner
  call) — frozen run + overlay + numeral on screen; cancels — back, face lost > 0.75 s, the HUD
  pause button — land back on the paused card; `ApplyPhase` stays the only `timeScale` writer.
  Dials + mirror rule: `docs/CAMERA_TUNING.md` §Resume gesture.
- **D — per-scheme boards** (owner call: camera and tilt are separate games): `Track/ControlScheme`
  + `Track/BestBoard` behind an `IScoreStore` seam. Keys
  `veyro.best.{alltime,daily,daily.date}.{tilt,camera}`; legacy keys → tilt once, idempotent,
  never deleted/rewritten — keys and migration pinned in `BestBoardTests`. `RunSession.Begin`
  learns the scheme; a camera run that drops to tilt still scores camera; bests reload per run so
  a mid-session scheme switch shows the right board. HUD mode line + result best line name the
  board. Daily seed untouched — only the boards split. T-009 note added (two leaderboard IDs).
- **Models**: 20.6 MB on disk under `CameraInput/Resources` — the brief estimated 5–10 MB.
  fp16 quantization is the fallback if the budget minds (OPEN_QUESTIONS 13).

Needs a human (Nord 2): the full acceptance loop — walk out mid-run → lane holds ≤ 2 s →
auto-pause + reason line → re-enter → raise RIGHT hand ~1 s → 3-2-1 → resumes at same score,
aimed straight; touch RESUME at every step; wrong hand must NOT confirm (mirror check); logcat
`pose probe up/down` exactly bracketing the wait; tilt + camera frame-time unchanged; per-scheme
bests on result + HUD. Then the OPEN_QUESTIONS 13 defaults (picker gesture, tilt countdown,
picker boards, hold/countdown feel, model size).

## 2026-08-30/31 — improve-ui session: pause/quit/guide, 3-lane steering, camera rework, face overlay

PR #5. **EditMode 166 → 272 green; `RunSeedTests` untouched.** Device-verified on the Nord 2
except the items needing a face (see the protocol in `docs/CAMERA_TUNING.md`). Dev APK 93.5 MiB;
permissions byte-identical to the 26 Aug release.

- `Core/RuntimeUi` replaces three duplicated UGUI copies. Sorting: HUD 0, overlay 50, picker 100,
  guide 120, pause 150, store 200.
- Pause/resume/restart/quit (`RunFlow` + engine-free `PauseState` back-table). Pause = `timeScale
  0` **plus** `RunSession.Frozen`; `RunSession` runs at `[DefaultExecutionOrder(100)]` (after the
  EventSystem) — the pause button, result-screen QUIT and store-tap guard all rest on that
  ordering. Camera released while paused (`Suspend` + `_generation` guard); resume re-stages and
  recalibrates neutral; camera-won't-return → run finishes on tilt+touch (rule 3).
- Quit writes nothing (`Crash()` stays the only best-writer). Result card gained QUIT TO MENU.
- First-run guide: 3 pages, `veyro.seen_guide` (any non-zero = seen), re-openable from the footer.
- **3-lane steering** (owner call): `Track/LaneSelector`, absolute mapping, enter |0.5| / hold
  |0.3|, smoothstep 0.14 s/lane (ceiling 0.152 — full sweep uses 92% of the tightest dodge
  window). Collisions still read `transform.position` per frame.
- **Camera rework** — all constants, units, dials, telemetry format, calibration protocol, open
  decisions and risks live in **`docs/CAMERA_TUNING.md`**: orientation persists across pause
  (mirror-twin inversion fixed); jump = windowed net rise (frame-rate independent); gestures in
  face-widths so they cost the same centimetres at any distance (`BoxWidthsPerFace` 1.35 is an
  **estimate** — calibrate first); loss holds the lane instead of recentring; three score tiers so
  side-lane hops survive blur; `FaceOverlay` stickman panel (run + staging) and 1 Hz `[CAM]
  telemetry` logcat line; back cancels picker staging; mode picks deferred one frame.
- **Store "unavailable" was a doc bug**: `BuildAndroid` is deliberately keyless; device builds are
  `BuildAndroidDev` (AGENTS.md corrected; keyless builds now log it).

Verified on device: full pause/quit/restart/guide loops, store live on Test Store prices, staging
+ overlay + back-cancel, first-frame-jump fix (with positive control), logcat clean. Not verified
(needs a person): every camera centimetre, the resume framing panel, gesture feel.

Needs a human: run the `CAMERA_TUNING.md` calibration protocol (sign check → 25 cm measurement →
distance sweep → false-positive watch), then the open decisions listed there (absolute-vs-latched
lanes, slide no-op, back-at-picker, result hint wording, overlay placement).

## 2026-08-30 — PR #4 review fixes: privacy text now matches the build (pr4-review session)

Copilot raised six findings on PR #4; all six verified against the code, all six real.

**Privacy copy — the one that mattered.** It claimed a player who never buys never contacts
RevenueCat. The build has never worked that way: `GameBootstrap` configures the SDK at boot and
`OnApplicationPause` refetches on every resume. Corrected the **text**, not the behaviour —
`SkinService` needs entitlements at boot and the resume re-read is the judge promo-code path
(REVENUECAT_PLAN §6.3). The claim sat in five places (policy ×2, support ×2, listing); terms §2 and
the home page had softer versions. All fixed, policy and site page re-verified in sync, none deployed.
→ **Data safety must now declare device/other IDs, not just purchase history** (an anonymous app user
ID leaves the device every launch). `STORE_COMPLIANCE.md` + `PLAY_CONSOLE_SETUP.md` updated —
**owner should read that wording before filling the Console form.**

**Three correctness fixes** (owner committed as `b55e03c`): `RevenueCatStore` reported `AlreadyOwned`
(and paywall success) before the entitlement refresh landed, breaking `IStore.Purchase`'s written
guarantee — `RefreshCustomerInfo` now takes a completion callback. `StorePanel` callbacks touched
`_status` with no liveness check → `MissingReferenceException` on close mid-call; now a `StillOpen`
helper, with `Equip` outside it so a paid-for skin is worn even if the panel is gone. `BuildScript`
mutated the project before entering its rollback `try`; all mutations moved inside.

**Verified.** 166/166 EditMode tests, clean compile. Rollback measured with `VEYRO_KEYSTORE` unset:
pre-fix leaked `VEYRO_STORE_BUILD` into the defines (and into the committed `ProjectSettings.asset`)
and left `buildAppBundle` true; fixed leaves both untouched. Device (Nord 2, fresh dev APK): full
store flow clean, zero game-side exceptions — prices, direct Play sheet, valid purchase →
auto-**EQUIPPED** and skin persists into the next run, failed purchase, restore, close, resume re-read.

**Not verified.** The `StillOpen` guard never fired: Test Store answers in ~90 ms, two `adb` taps
can't land closer than ~60 ms, and the purchase dialog is modal — needs a throwaway build with a
delayed callback. `AlreadyOwned` is unreachable via the UI (an owned row equips instead of buying);
fold both into the versionCode-5 internal spot check.

**Also:** T-020's backlog entry still claimed 162/162 tests and pending keys/device test (both done
27–29 Aug) — rewritten so the next agent does not redo finished work.

**Needs a human:** commit the docs/site changes (GPG prompts for a passphrase); then the atomic
versionCode-5 flip — `wrangler deploy` from `site/` in the same window as the .aab upload, so the
corrected policy is live before the build that talks to RevenueCat reaches testers.

## 2026-08-29 — store split in two: skins buy straight on the Google Play sheet, the pass keeps the paywall (t020-direct-purchase session)

> **Owner device verification COMPLETE, 29 Aug evening (dev-store flavour, fresh
> `MotionRunnerDev.apk` built + adb-installed same day): 6/6 checks pass.** Skins raise the
> purchase dialog directly (valid/cancel/failed all correct, instant equip on success);
> season-pass paywall renders (verified on a clean-state reinstall — Android Auto Backup had
> been resurrecting the old anonymous ID and its Test Store entitlements, so the reinstall ran
> with `bmgr` restore off); RESTORE round-trips proven in logcat (two `_restorePurchases`
> responses captured — "does nothing" was correct both times: once everything was already
> owned, once the fresh identity owned nothing). USD prices are Test Store placeholders;
> the Play build localizes via the buyer's Play-account country. Two follow-ups filed for the
> game-UX phase, NOT this release: restore status should say "nothing to restore" when zero
> items return; consider the same wording note for the paywall. Real-Play spot check (license
> tester: buy → uninstall → reinstall from Play → RESTORE returns it) happens on the
> versionCode-5 internal build before the closed submission.

**Headline: the owner's 29 Aug call is implemented — tapping EMBER or FROST now raises the native
Google Play purchase sheet directly, no paywall in between; SEASON 1 PASS still opens the
dashboard-configured RevenueCat paywall.** Both paths are gated identically (a row is only
purchasable while it is locked) and entitlements remain the single source of truth for "owned".
**166/166 EditMode tests pass** (was 162; +4).

**Why so little code moved:** the seam already had the right call. `IStore.Purchase(packageId)` →
`GetOfferings` → `PurchasePackage` existed and was tested from 26 Aug; it was simply never wired
to the UI, where every tap went to `PresentPaywall`. `StorePanel` now routes skin rows to
`Purchase` and the pass row to `PresentPaywall`. `PurchasePackage` is deliberate — the SDK's
`PurchaseProduct` overload defaults to `type: "subs"`, which is wrong for three one-time
non-consumables, and RevenueCat's own guidance is to use the package call whenever the Offerings
system is in play.

Four things the paywall used to handle that the direct path now owns:

- **Cancel is silent.** Backing out of the sheet reports `Cancelled`, prints no status text,
  grants nothing, and leaves the panel usable. Accepted from *either* `PurchaseResult.UserCancelled`
  *or* error code 1 — Android can surface the same event as either.
- **Pending is not failure.** New `PurchaseStatus.Pending`, mapped from error code 20
  (`PaymentPendingError`: cash/voucher payments, parental approval, SCA). Nothing is granted —
  that would hand out an unpaid skin — and the panel says the payment is pending and unlocks
  itself. The existing `OnApplicationPause(false)` cache-drop + re-read is what picks the
  entitlement up when Google confirms.
- **The unlock is immediate.** `ApplyCustomerInfo(result.CustomerInfo)` now runs *before* the
  callback, so the entitlement is live by the time the panel reacts — no restart, no second fetch.
  A skin bought this way also auto-equips: the tap already said "I want this one".
- **The sheet opens on the tap, not after a round trip.** The adapter caches the last `Offerings`
  it fetched (the panel fetches on open for prices), so a tap goes straight to `PurchasePackage`,
  falling back to `GetOfferings` when nothing is cached.

The three branch codes are named constants now (1 cancelled / 6 already-purchased / 20 pending).
The numbering is shared across every RevenueCat SDK; `purchases-ios` `Sources/Generated/ErrorCode.swift`
is the generated source of truth if it ever needs re-checking. Also new:
`Entitlements.PackageFor(entitlementId)`, the inverse of `ForPackage`, living in the engine-free
assembly so the map the direct tap depends on is EditMode-tested — `StorePanel`'s hand-rolled
if-chain is gone.

**Verified:** `Unity 6000.5.9f1 -batchmode -runTests -testPlatform EditMode` against this tree
(owner's editor closed — no scratch copy needed), **166/166, 0 failed**, including the four new
tests: entitlement↔package inverse map, every sellable skin has a package, Pending grants nothing
and is neither success nor failure, a direct skin purchase leaves the pass alone. No new packages,
no new permissions, no SDK type outside `MotionRunner.Commerce.RevenueCat`; EditMode still compiles
and runs with no SDK in sight. Nothing in STORE_COMPLIANCE flips — same Billing surface, same
declarations. 4ab3335 is intact: `StorePanel.IsOpen` is untouched and the panel stays open across
the Play sheet, so the pause/resume the sheet causes cannot restart the run either.

**Needs the owner (~10 min, phone in hand — license tester, `BuildAndroidDev`, Test Store):**
1. Tap **EMBER** → the **Google Play purchase sheet must appear directly**, with no paywall first.
2. Complete the purchase → row turns owned **and the runner turns ember immediately** (auto-equip),
   with no restart.
3. Tap **FROST** → sheet appears → **press back / cancel** → nothing happens: no error text, row
   still shows its price, store still usable, run still frozen behind it.
4. Tap **SEASON 1 PASS** → the **dashboard paywall must still render** (this is the regression to
   watch; it is the only remaining paywall call site).
5. **RESTORE PURCHASES** still returns everything; relaunch → still owned.

One dashboard note, not a code issue: the paywall attached to offering `default` lists all three
packages, so the pass's paywall can still sell a skin. If the owner wants that paywall to show the
pass alone, it is a Paywall Builder edit with no app release (REVENUECAT_PLAN §5).

## 2026-08-27 — T-020 on device: keys in, store live against the Test Store; three bugs found and fixed in the install loop (t020-unity session)

**Headline: the owner pasted both public API keys and the store now runs on the phone — SDK
configured, customer info received, offerings fetched, the store panel shows live Test Store
prices ($2.99 / $2.99 / $4.99) with equip/lock state and Restore.** The chain configure →
entitlements → offerings → UI is device-verified; what remains human is the purchase itself
(paywall render, buy, restore) — steps below still apply, with one command change.

Three real findings from the first installs, all fixed and re-verified on device:

1. **`Purchases.IsConfigured()` NREs before the SDK's `Start()`** binds its platform wrapper —
   and Unity delivers the first `OnApplicationPause(false)` earlier than that, so the resume
   re-read crashed at boot. `RevenueCatStore` now tracks its own `_configured` flag and never
   asks the SDK.
2. **A Test Store key closes a release build.** RevenueCat shows "Wrong API Key … the app will
   close now to protect the security of test purchases" on any **non-debuggable** build using a
   `test_` key. So the key scheme changed: **`BuildAndroidDev`** (new, `BuildOptions.Development`,
   output `builds/MotionRunnerDev.apk`) is the Test-Store flavour; plain `BuildAndroid` release
   APKs now ship **no key** (store disabled, fail-open) instead of a test key that force-closes.
   `StoreBuildGuard` enforces all of it: VEYRO_DEV_STORE requires a Development build + `test_`
   key; .aab still requires VEYRO_STORE_BUILD + `goog_` key and refuses the dev define.
   **Device-test command is therefore `BuildAndroidDev`, not `BuildAndroid`** — the 26 Aug entry's
   step 2 is superseded by this.
3. **Opening the store restarted the run behind it.** `TouchTapInput` reports jump on the same
   `TouchPhase.Ended` that fires a UI button's click, so the tap on SKINS & STORE also hit the
   result screen's "tap anywhere to restart". Restart is now deferred one frame and cancelled if
   that tap opened the store (`StorePanel.IsOpen`); keyboard space and the camera-mode hop
   restart still work.

**Verified on the Nord 2:** dev build installs over the Play-track copy (uninstall needed once —
different signing key, expected), boots with no dialog and no exceptions, `Development Build`
watermark present, logcat shows `_receiveCustomerInfo`/`_getCustomerInfo`, result screen shows
SKINS & STORE, panel opens with live prices and the run stays frozen on the result behind it.
Screenshots in the session log. Keys: OPEN_QUESTIONS 9b marked answered.

**Still needs the owner (~10 min, phone in hand):** in the open store panel — tap EMBER → the
dashboard paywall must render → complete the fake Test Store purchase → row turns owned → equip
→ runner turns ember; relaunch → still owned; RESTORE PURCHASES returns it; background the app,
grant/revoke an entitlement in the dashboard, foreground → updates without restart. (Reinstall →
Restore stays a Play-sandbox-only test — Test Store receipts don't survive a new anonymous id.)

## 2026-08-26 — T-020 code complete: RevenueCat 9.8.1 in, Commerce seam + store UI + key guard; needs keys + device test (t020-unity session)

**Headline: the whole Unity half of T-020 is built and green — SDK 9.8.1 via OpenUPM, the
`MotionRunner.Commerce` seam with 22 new EditMode tests (162/162 total), a store panel wired to
the dashboard paywall, entitlement-driven skins, and a build guard that makes shipping the Test
Store key in an .aab a build failure. Two things stand between this and done: the owner must
paste the two public API keys (OPEN_QUESTIONS 9b — the handoff's key placeholders were never
filled in), then run the Test-Store device test below.**

**Install route: OpenUPM (plan §1.1), EDM4U in mainTemplate-patch mode.** `manifest.json` pins
`com.revenuecat.purchases-unity` + `com.revenuecat.purchases-ui-unity` **9.8.1** exactly and
`com.google.external-dependency-manager` **1.2.188** (latest on OpenUPM; ≥ 1.2.187 keeps the iOS
SPM path open — the plan's 1.2.186 was a verify-at-install placeholder). Instead of letting EDM4U
download AARs into `Assets/Plugins/Android` (Compose has dozens of transitive deps — exactly the
gotcha-11 debris class), the three gradle templates are committed under `Assets/Plugins/Android/`
and EDM4U patches `mainTemplate.gradle` with 4 dependency lines
(`purchases-hybrid-common[-ui] 18.31.0`, androidx.activity/annotation); Gradle pulls transitives
at build time. EDM4U settings are pinned project-side in `ProjectSettings/GvhProjectSettings.xml`
(not per-machine EditorPrefs), resolution record in `ProjectSettings/AndroidResolverDependencies.xml`
— all committed deliberately (plan §2.7). The templates are copies of 6000.5.9f1's defaults: on a
Unity version bump, re-copy them and let EDM4U re-patch.

**APK diff (gotcha 10), release `BuildAndroid`, same tree before/after** (the old 29.6 MB figure
was the pre-camera baseline; `main` ships camera mode since PR #1, so the honest baseline was
rebuilt first):

| | bytes | permissions |
|---|---|---|
| baseline (this tree, pre-RevenueCat) | 47,024,127 (44.8 MB) | INTERNET, CAMERA |
| with RevenueCat 9.8.1 + wrapper + UI | 58,205,601 (55.5 MB) | + `com.android.vending.BILLING`, + `ACCESS_NETWORK_STATE` |

+10.7 MB — the Jetpack-Compose-for-paywalls line item the plan budgeted for (§1.2); acceptable,
UGUI-fallback question closed. BILLING is required; ACCESS_NETWORK_STATE rides in with Play
Billing (normal-level, install-time). Both noted in STORE_COMPLIANCE with the flip table.

**Found and fixed while checking §2.2:** the merged manifest gave `UnityPlayerGameActivity`
`launchMode="singleTask"` (Unity's default) — RevenueCat requires standard/singleTop or a
purchase dies when the player is bounced to a banking/3DS app mid-payment. New
`AndroidLaunchModeFix` (IPostGenerateGradleAndroidProject) patches it to **singleTop** at build
time; verified `launchMode=1` in the rebuilt APK's manifest. No committed AndroidManifest.xml
shadows Unity's template.

**The seam (plan §3):** `MotionRunner.Commerce` (engine-free, `noEngineReferences: true`):
`IStore` (callback-shaped; `PresentPaywall()` takes no entitlement argument — Present, never
PresentIfNeeded), `StoreOffer`, `PurchaseOutcome` (status + optional `StoreError`),
`Entitlements` (`skin_ember`/`skin_frost`/`season1` + the package→entitlement map),
`CosmeticCatalog` with unlock rules **Default | Entitlement(id) | SeasonLevel(track, level)**
per COSMETICS_CATALOG §5 — the SeasonLevel shape exists and is tested, but no season items and
no XP ship: that is T-025, deliberately untouched — and `FakeStore`. 22 new EditMode tests cover
the §3.4 list: locked→purchase→unlocked, restore-after-reinstall shape, same-set re-announce,
cancel ≠ failure, AlreadyOwned (the out-of-app promo path), fail-open (not-ready grants nothing
and throws nothing), catalog integrity, SeasonLevel truth table. **162/162 EditMode tests pass**
(was 140), run in batchmode against this tree (owner's editor closed — no scratch copy needed).

**The adapter:** `MotionRunner.Commerce.RevenueCat` / `RevenueCatStore` — the only assembly that
sees an SDK type. Runtime setup on a persistent GameObject (`useRuntimeSetup` set before the
component's `Start()`; `Configure` deferred one frame because `Purchases` binds its platform
wrapper in `Start()` and configuring earlier NREs), **anonymous app user id** (null → SDK
generates, §3.3), `UpdatedCustomerInfoListener` → `EntitlementsChanged`, purchase error code 6
(ProductAlreadyPurchased) mapped to `AlreadyOwned`, and **entitlements re-read on
`OnApplicationPause(false)`** with `InvalidateCustomerInfoCache` — the §6.3 out-of-app promo
redemption path. Fail-open throughout: no key / no network / no offering → locked entitlements,
playable game, no exceptions. In the Editor (where the SDK NREs, §2.5) GameBootstrap hands the
game a ready `FakeStore`, so the store UI is exercisable in Play mode.

**UI (code-first UGUI, matching RunHud/ModeSelectMenu):** the result screen gained a
**SKINS & STORE** button (RunHud card 780→920) opening `StorePanel`: catalog rows with live lock
state and localized prices from `GetOfferings`, tap-to-equip for owned skins, the dashboard
paywall for locked ones (the call site gates — it only opens for items the player does not own),
a Season 1 pass row honestly labelled "reward ladder arrives with the Season 1 update", a visible
**RESTORE PURCHASES** button (§6.3), and fail-open status text. Skins apply through
`SkinService` → `CosmeticCatalog` → `RuntimeMaterials.Shared` at the GameBootstrap colour site;
the default skin pins the original orange (test-enforced), Ember/Frost are colour swaps until
T-025 builds the full presets. Selection persists in `veyro.skin`; a revoked entitlement falls
back to the default on the next apply.

**Key handling (the door that must not open the wrong way):** `RevenueCatKeys.cs` holds both
**public** SDK keys — currently **empty: the session handoff's key placeholders were never
filled** (→ OPEN_QUESTIONS 9b). Selection is compile-time: `BuildAndroidBundle` sets
`VEYRO_STORE_BUILD` for the duration of the build (restored in `finally`) → `ActiveKey =
PlayStoreKey`; APK builds never set it → `ActiveKey = TestStoreKey`. `ApplyStoreKeyPairing` plus
`StoreBuildGuard` (IPreprocessBuildWithReport — catches editor-GUI builds too) fail the build on
any mismatch: .aab without the define, .aab with a non-`goog_` key, APK with a non-`test_` key.
Consequence today: **dev APKs build with the store disabled (fail-open, loud build-log warning);
`BuildAndroidBundle` refuses to build until the Play key is pasted** — deliberate, an .aab with
a missing/test key would be a dead store build.

**T-031 AAB path:** `BuildAndroidBundle` already existed (24 Aug) and gains only the key guard;
signing unchanged via `VEYRO_KEYSTORE`/`VEYRO_KEYSTORE_PASS` (+ optional `VEYRO_KEYALIAS`,
`VEYRO_KEYALIAS_PASS`, `VEYRO_VERSION_CODE`) env vars. The upload keystore exists — owner created
it 25 Aug (D5 ✅; the keytool runbook stays in PREREQUISITES D5 for recovery). The next .aab
carries BILLING + the purchases SDK, so **every STORE_COMPLIANCE T-020 flip rides that same
release** (note added there).

**Also:** `companyName` DefaultCompany → **Ferrabled** (`productName` was already "Veyro Run") —
plan §7B item 13. Rename in ProjectSettings if a different publisher label is wanted; cheap now,
awkward after store screenshots.

**Needs the owner (in order):**
1. **Paste the two public API keys** into `game/Assets/Scripts/Commerce/RevenueCat/RevenueCatKeys.cs`
   (OPEN_QUESTIONS 9b) and commit — public keys, safe in git; never the `sk_` secret ones.
2. **Test-Store device test** (~15 min). Rebuild + install:
   `Unity -batchmode -quit -projectPath game -buildTarget Android -executeMethod MotionRunner.EditorTools.BuildScript.BuildAndroid`,
   `adb install -r builds/MotionRunner.apk`. Then:
   - crash a run → **SKINS & STORE** → tap Ember → the dashboard paywall renders (it cannot
     render in the Editor) → buy via the Test Store sheet → row turns owned → equip → runner
     turns ember; relaunch → still owned, still equipped.
   - background the app → grant/revoke an entitlement on the customer in the RevenueCat
     dashboard → foreground → state updates without a restart (§6.3 resume re-read).
   - **RESTORE PURCHASES** on the same install returns the entitlements.
   - uninstall → reinstall → Restore: **expect empty on the Test Store** — a fresh anonymous app
     user id has no Test Store receipts behind it. The real reinstall→Restore acceptance test
     (T-020's AC) only means something against Play sandbox (plan §7C item 16), once the Play
     products (§D1) + service-account JSON (§D3) exist.
3. **Play side, unchanged:** create the 3 products (PLAY_CONSOLE_SETUP §D1), upload the
   service-account JSON (§D3), then `BuildAndroidBundle` → internal track (unlocks in-app product
   creation) / closed track as the next update, with the STORE_COMPLIANCE flips in the same
   release.

**Next:** keys + device test → T-020 🔷→✅; after that the paywall is pure dashboard iteration
(plan §5, no store review). T-025 (season pass content) now has its rule shapes and seam waiting.

## 2026-08-26 — PR #2 review pass: entitlement model, judge-code hardening, season-window fix, timeline recomputed (doc session)

Worked the Copilot review on PR #2 (17 inline + 7 suppressed comments — all 24 verified against the
repo, none spurious). No runtime code touched; `game/Assets/Scripts/Commerce/` still does not exist,
which is exactly why these mattered: the T-020 Unity session reads these docs as the spec.

**Design changes, not just wording:**

- **Three entitlements, everywhere.** `REVENUECAT_PLAN.md` still specified a single `cosmetics`
  entitlement in five places (§3.2 const, §3.3 paywall row, §7 checklist steps 1/3, step 11) — dead
  since the 23 Aug catalog redesign. Implementing it would have made *every* entitlement check fail.
  The §7 checklist was the dangerous one: it told the owner to create **two** products and one
  `cosmetics` entitlement in Play Console, where ids are immutable. Now `skin_ember` / `skin_frost` /
  `season1` throughout, matching `PLAY_CONSOLE_SETUP.md` §D (which was already correct).
- **`PresentIfNeeded` → `Present`.** With three independent entitlements no single
  `requiredEntitlementIdentifier` answers "already owned": any one of them shows the paywall to
  someone who owns the other two. `IStore.PresentPaywall` lost its entitlement argument; gate the
  call site instead.
- **`PurchaseOutcome` is a struct, not an enum.** It was sketched as
  `enum { … Failed(reason) }`, which does not compile — a C# enum cannot carry a payload, and the
  reason is needed for §6 diagnostics. Now `PurchaseStatus` enum + optional `StoreError`.
- **Historical judge-login proposal — retired 23 Sep 2026.** This review had proposed random
  identifiers in place of predictable shared identifiers. The owner subsequently chose Google
  Play promo codes as the primary route; no custom judge login is planned. Current instructions:
  REVENUECAT_PLAN §6 and resolved OPEN_QUESTIONS 10.
- **Season 1 earning never closes for pass owners.** The catalog claimed paid items "stay owned"
  after 31 Oct because the entitlement is non-consumable. Not true: pass-track items unlock on
  *level*, level derives from local XP (D10), so reinstall → Restore returns `season1` but resets the
  ladder → after 31 Oct the items are permanently unreachable. v1.0 ships no rollover, expiry or
  Season 2 code, so nothing technical depended on the window closing — 31 Oct is now an *event* date
  only. **T-025 constraint: the earn path must not be date-gated.** T-009 stays post-v1.0 as the fix
  for progress durability itself, and the paywall copy should disclose device-local progress.

**Stale facts corrected:** `AGENTS.md` in four places (adapter names → `GyroTiltInput` /
`TouchTapInput` / `CameraFaceInput`; BlazePose → BlazeFace, since BlazePose missed the gate at 118 ms;
`≥ 8.4.0` → exact `9.8.1` pin; camera "parked pending OPEN_QUESTIONS 7" → ships in v1.0). Five stale
"blocked on P1" markers, incl. `BACKLOG` T-020 which read ⛔ on the same line as "Unblocked 24 Aug".
`CHECKLIST` privacy-policy ref pointed at OPEN_QUESTIONS 12 (the camera decision) instead of 9.

**Camera-in-v1.0 reached the store kit.** `LISTING.md` §4 was a "paste only when the camera update is
LIVE" delta — folded into the shipping copy: short description now `Play hands-free with the camera,
or tilt to steer. A new endless track daily.` (77/80) and the full description carries the
`HANDS-FREE CAMERA MODE (BETA)` block and the FEATURES bullet (2,828/4,000). Verified by
`check-lengths.sh`, which now also measures both §2 alternates. `SCREENSHOTS.md` Shot 5 (mode-select
menu) reversed from "hold for v1.0" to ship — it is the visual half of explaining the CAMERA
permission to a reviewer. Shot 7 likewise. `VIDEO_SCRIPT.md` contingency retired.

**Timeline recomputed — closed testing went LIVE 26 Aug**, five days ahead of the ~1 Sep plan:

| | Was | Now |
|---|---|---|
| 14 days elapse | ~15 Sep | **~9 Sep** (from the day the **12th** tester is opted in, not from upload) |
| Apply for production | ~7–8 Sep (impossible) / ~15 Sep | **~9 Sep** |
| Production live | ~22 Sep | **~16 Sep** |
| Slack before 30 Sep | ~8 days | **~10 days**, all of it after production access |

`PLAY_CONSOLE_SETUP.md` §E had "apply ~7–8 Sep", which was arithmetically impossible against a ~1 Sep
start; gone. BACKLOG T-031 is now 🚧, and the 🔷 marker (code complete, awaiting device test) was
added to the legend — it was in use on T-011/12/13 but undocumented.

**Needs the owner:** create the **3 in-app products in Play Console** — they exist in RevenueCat but
not in Play, so no real purchase can be verified and each package's Play row is empty
(`PLAY_CONSOLE_SETUP.md` §D1) — then the service-account JSON (§D3, ~36 h propagation). And **get to
12 testers opted in**: the 14-day clock starts at the 12th, not at upload, so this is now the single
uncompressible item on the critical path. Recruit 16–18.

**Not addressed (owner's call):** `VIDEO_SCRIPT.md` §0:00–0:18 still says "one continuous take" *and*
"cut in a tight insert at ~0:10" *and* "no cuts inside the lean-swerve beat" — three rules that cannot
all hold. Low stakes, but ambiguous for the shoot.

## 2026-08-25 — T-036 marketing website built; privacy policy gets a real home (veyro-site session)

**Headline: the complete marketing site exists at `site/`, ready to deploy to veyro.ferrabled.com —
which also resolves OPEN_QUESTIONS 9 (privacy-policy hosting + contact email).** Owner decisions
this session: Cloudflare free tier + Wrangler (not GitHub Pages), subdomain `veyro.ferrabled.com`,
contact email `ferrabled+veyro@gmail.com` (temporary alias, will be replaced), and the design —
"Riso Print" (two-ink risograph poster, teal #12454C + fluoro pink #EE3D87 on paper #FBF5E9),
picked from a 2-round pitch (owner explicitly wants nothing that reads AI-generated; camera mode
is the headline feature, tilt secondary). Pitch artifact:
https://claude.ai/code/artifact/c0ca2d3c-4582-4968-8592-8b2a9cfb46be

- `site/public/`: index (7 independent sections: hero / how-it-plays / camera-privacy / daily-ticket
  / cosmetics-pledge / spec-strip / about), `/privacy/` (mirrors `docs/PRIVACY_POLICY.md`, whose two
  email TODOs are now filled), `/terms/` (cosmetic-IAP + physical-safety terms), `/support/`
  (FAQ + data-deletion statement), 404, favicon, `_headers` (CSP), robots.txt. No framework, no
  build step; fonts Anybody / Atkinson Hyperlegible / Fragment Mono. Hero CTA is in pre-launch
  state (closed-test mailto); the Google Play CTA is in the HTML, commented, for launch day.
- `GameLinks.PrivacyPolicyUrl` → `https://veyro.ferrabled.com/privacy/` (was GitHub Pages).
  **The URL 404s until the first deploy — deploy before any build that ships this constant.**
- Copy compliance checked: grep for the ART_DIRECTION banned words (race/speed/drive/circuit/turbo)
  is clean across `site/`; camera copy carries the BETA label + on-device lines matching the policy.
- Verified via local static server + headless DOM checks: all pages/assets 200, unknown path 404s,
  zero console errors, no horizontal overflow at 1280px or 375px (panels stack on mobile), daily
  ticket regenerates from today's UTC date (JS), CSS/fonts load. **Not visually inspected by a
  human yet — needs the owner to look at it** (`npx serve site/public` or `npx wrangler dev`).
- **Needs human:** (1) `cd site && npx wrangler login && npx wrangler deploy` — first deploy
  auto-creates the custom domain in the ferrabled.com zone; (2) eyeball the design on desktop +
  phone; (3) when the Play listing goes live, flip the hero CTA (marked in `site/README.md`).
- Owner reviewed the site 25 Aug: approved except the capsule-person figures — "once we have the
  main model and skins, we will replicate those." Filed as **T-037** (blocked on the character
  model); the exact spots to swap are tabled under "Placeholder figures" in `site/README.md`.

## 2026-08-29 — Play ↔ RevenueCat verified end-to-end; T-020 needs only the flip release (compliance session)

**Headline: a license-test purchase through the real Play Store landed in RevenueCat's Play
Store app — the whole monetization pipeline works.** 3 products live in both consoles
(`veyro.skin.ember`/`.frost` €2.99, `veyro.season1.pass` €4.99), credentials "valid",
license testing on, T-020 AAB (versionCode 4, BILLING) on the internal track. P4 ✅ complete;
P10 merchant profile ✅ (payout bank still unwired — non-blocking, sales accrue).

**Remaining for T-020 (handed off to a t020-release session, brief given to owner 29 Aug):**
1. Store UX decision (owner, 29 Aug): the in-game store currently opens the season-pass
   RevenueCat paywall for every purchase — change to paywall **only for the season pass**;
   the two skins purchase **directly** (`Purchases.PurchasePackage` on their offering
   packages). Research + implement + device-verify.
2. The **atomic closed-track flip release** (STORE_COMPLIANCE T-020 section): versionCode 5
   build + App access "Sí" + Data safety purchase-history + content-rating redo + IAP flag +
   held-back cosmetics paragraph (LISTING.md §3) + privacy-policy purchases section on
   docs + site (redeploy = owner wrangler run).
3. Owner-only, still open: **§D2 15% fee tier**, payout bank, address-publication decision
   (register), OPEN_QUESTIONS 12 → DECISIONS.md. Historical question 10 was subsequently resolved
   on 23 Sep: Google Play promo codes are primary; the judge-login proposal is retired.

## 2026-08-25 — First Play upload done; site live + compliance register created (compliance session)

**Headline: versionCode 1 is on the internal track, the marketing/legal site is live at
veyro.ferrabled.com, and store compliance now has a canonical doc.**

- Owner registered the Play account, created the upload keystore (D5 ✅), built and uploaded
  `MotionRunner.aab` (44.8 MB, target SDK 36, ARM64, jarsigner-verified) to **internal testing**.
- **Rebuild required before closed testing:** versionCode 1 has the dead GitHub Pages privacy
  URL baked in (verified in the AAB's IL2CPP metadata). Rebuild with `VEYRO_VERSION_CODE=2`
  and upload that to the closed track. Recorded as the "URL coupling" rule in STORE_COMPLIANCE.
- Site live and verified (privacy/terms/support): passes the User Data policy checklist; added
  "Your rights" + "About this website" sections (effective date 25 Aug), hedged the support
  purchases FAQ ("coming in a future update"); owner redeployed. `site/.wrangler/` gitignored
  (held the Cloudflare account id). Terms already said "may offer" purchases — left as-is.
- Copilot PR review triaged: kept `APP_UI_EDITOR_ONLY` (it *strips* App UI runtime from player
  builds — asmdef constraint `UNITY_EDITOR || !APP_UI_EDITOR_ONLY`; removing it would bloat the
  APK), reverted the dangling `com.unity.dt.app-ui` EditorBuildSettings entry (GUID exists
  nowhere), documented `AndroidKeystoreName: '{inproject}: '` as the deliberate post-build wipe
  and made only bundle builds touch keystore fields.
- **`docs/STORE_COMPLIANCE.md` created** (owner request): current Console declaration state +
  per-release flip table (T-020 purchases, T-021 push/AD_ID check, Layers, T-009 login/
  leaderboard incl. the account-deletion policy trap) + standing rules. Linked from CLAUDE.md
  canonical docs. Store-listing text drafted in `docs/store-kit/listing.md`.
- Console App content answers agreed (all recorded in STORE_COMPLIANCE): App access "No",
  Data safety "no collection", content rating all-No → PEGI 3, target audience 13+, Arcade.

**Needs a human:** rebuild + upload versionCode 2 to closed testing; finish Console items
(content rating, target audience, Data safety, category/contact, listing); **store graphics
missing** (512×512 icon, 1024×500 feature graphic, ≥2 screenshots — specs in
docs/store-kit/listing.md); recruit 12 testers (H6); commit + merge this branch (PR open).
## 2026-08-24 (merge) — `main` reconciled into `revenueCat-prep`: AAB path + privacy policy + camera-in-v1.0 meet the RevenueCat prep

Merging `main` (camera-feature + the compliance session below) changed facts the RevenueCat-side
docs of this branch assumed; reconciled in this merge, not just spliced:

- **The AAB path already exists** (`BuildScript.BuildAndroidBundle`, keystore runbook
  PREREQUISITES D5) → `PLAY_CONSOLE_SETUP.md` §B updated (the T-020 session only adds the SDK, not
  the build path; item 8 of the T-020 handoff is mostly done already).
- **Camera mode ships in v1.0** (owner call; record in DECISIONS = OPEN_QUESTIONS **12**) →
  PLAY_CONSOLE_SETUP §A updated: Data safety declares camera on-device-only from build one; the
  listing description must mention camera mode (T-030).
- **The privacy policy is drafted** (`docs/PRIVACY_POLICY.md`, linked in-app via `GameLinks`) —
  remaining is hosting + contact email, merged into OPEN_QUESTIONS **9** (was 12 on this branch;
  judge-access question retains 10, resolved 23 Sep in favor of Google Play promo codes).
- **T-031 sequencing improved:** an AAB exists *now*, so the 12×14 clock starts with the current
  build; the RevenueCat build follows as a track update — which is also what unlocks Play in-app
  product creation. Backlog T-030/T-031 rewritten as the union of both branches.

## 2026-08-24 (later) — RevenueCat dashboard COMPLETE for both apps; Play Console checklist written; Unity handoff issued (owner dashboard session)

- Owner finished the RevenueCat side: entitlements, Test Store *and* Play Store products, offering
  `default`, paywall — the dashboard half of T-020 is done. What remains dashboard-side is only
  §D of the new checklist (Play products, service credentials, attach).
- **`docs/PLAY_CONSOLE_SETUP.md` written** (claims verified against live Google/RevenueCat docs
  today): ordered owner checklist — §A (payments profile, app creation, listing + declarations +
  privacy policy, 12 testers) can all happen **now, before any build**; §B is the agent-built
  signed AAB; §C uploads (internal track to unlock product creation instantly, closed track for
  the 12×14 clock); §D products + service-account JSON (~36 h propagation) + attach in RevenueCat.
- Doc sync: PREREQUISITES P1 ✅ / P4 mostly-✅ / P10 urgent; OPEN_QUESTIONS 9 moved to Answered
  (catalog is owner-confirmed; ids now committed in the RevenueCat dashboard); the privacy-policy
  question (now OPEN_QUESTIONS 9) marked urgent — it gates the clock; T-030 asset deadline moved up (rough
  512×512 icon + 1024×500 feature graphic + 2 screenshots gate the track publish); T-031 points at
  the checklist; CLAUDE.md canonical-docs list gained COSMETICS_CATALOG + PLAY_CONSOLE_SETUP.
- **A copy-pasteable T-020 Unity-session handoff was given to the owner in chat** (scope: SDK
  9.8.1 via OpenUPM, APK diff, Commerce asmdefs + tests, RevenueCatStore, store UI + Restore,
  entitlement-applied skins, resume re-read, product/company name fix, AAB build path, Test-Store
  device test). T-025 explicitly out of its scope.

**Needs the owner:** run PLAY_CONSOLE_SETUP §A today (privacy policy first — it gates everything);
recruit the 12 testers; create the upload keystore when the agent posts the command; paste the two
RevenueCat public API keys into the Unity session handoff.

## 2026-08-24 — P1 cleared; RevenueCat project live with Test Store; T-020 Unity half unblocked (owner dashboard session)

- **Owner reports the Play developer account identity verification passed** → P1 no longer blocks.
  The 12-testers × 14-days clock is now the schedule's critical path: it only runs while a closed
  test is live, so T-031 (first closed-testing upload — private, invite-only) should happen as soon
  as a build exists, with polish landing as track updates. Backlog T-031 updated with the three
  ordering traps (AAB required; billing-enabled build must be uploaded before Play products can be
  created; app-content declarations incl. privacy policy needed to publish the track).
- **RevenueCat project "Veyro Run" exists** with the Play Store app + a **Test Store** app. Owner
  created a test product (`test.season1.pass`, wired to an entitlement), an offering with a
  `$rc_lifetime` package, and a paywall. Test Store means the whole Unity integration can be
  device-tested (purchase → entitlement → unlock, paywall render) before Play credentials exist —
  T-020's Unity half is unblocked today; only real-purchase verification still waits on the Play
  side. Convention recorded in `COSMETICS_CATALOG.md` §6 (`test.*` mirror products, same
  entitlements, same packages).
- **Dashboard fix-ups flagged to the owner:** the offering was created with identifier
  `default.current` (immutable; looks like a misreading of "mark it as current") — recreate as
  `default`, mark Current, re-attach the paywall, delete the odd one, while nothing depends on it.
  Also still missing: the two skin entitlements/test products/packages.

**Needs the owner:** offering fix; add `skin_ember`/`skin_frost` entitlements + test products +
packages; then the Play Console sequence (app creation, payments profile, privacy-policy URL,
upload keystore, 12 testers).

**Next:** Unity session for T-020 (SDK 9.8.1 + wrapper + paywall call, tested via Test Store) and
first AAB → closed testing (T-031). T-025/T-006/T-007 are the "improve the app" work that lands as
track updates without touching the clock.

## 2026-08-24 — Play submission prep: AAB build path, upload signing, privacy policy (compliance session)

**Headline: the repo can now produce a Play-uploadable artifact, and the policy paperwork the
Console will demand is drafted.** Owner decided in-session: **closed testing ships the
`camera-feature` branch** (camera in v1.0 — amends D2; recorded as OPEN_QUESTIONS 12 after the
merge renumbering, for the owner to move into DECISIONS.md). Researched against the live Play
policies (24 Aug 2026):

- **`BuildScript.BuildAndroidBundle`** (new): `builds/MotionRunner.aab` — Play only accepts App
  Bundles for new apps. Signs with the upload keystore from `VEYRO_KEYSTORE*` env vars (creation
  runbook: PREREQUISITES **D5**, human-only), optional `VEYRO_VERSION_CODE` override; keystore
  settings wiped in `finally` so no local path/secret lands in ProjectSettings.asset.
- **Target SDK pinned to 36** in `Build()` — Play's floor for new apps from **31 Aug 2026** (was
  "highest installed", which happened to resolve to 36; now it can't drift).
- **Privacy policy**: drafted at `docs/PRIVACY_POLICY.md` (truthful for current state: zero
  collection, camera on-device-only, no network calls). Required because CAMERA is a *sensitive
  permission*. Linked in-app (Play requires in-app reachability): new `GameLinks.cs` +
  "privacy policy" link on `ModeSelectMenu`. Hosting + contact email = **OPEN_QUESTIONS 9**.
- **T-030/T-031 acceptance criteria extended** with the Console App content forms (Data safety
  "no collection" — must change with T-020/T-021; content rating; target audience **13+**; ads
  "no"), and T-031 now says AAB, not APK.
- Checked and fine as-is: camera `uses-feature` entries are all `required=false` in the built APK
  (aapt2 on `VeyroCamDev.apk`) so camera-less devices keep store access; runtime permission is
  requested in-context; the three Console declarations (program policies / Play App Signing ToS /
  US export laws) are all safe to accept — no proprietary crypto, engine-standard TLS only.

**Verified:** EditMode suite in a fresh batchmode scratch copy (owner's editor held the repo
project): **140/140 passed**, sync of edited files into the scratch cmp-verified first. Robocopy
note for the gotcha file: exclude `game/Temp` (`/XD`) — the open editor's `UnityLockfile` lives
there and, if copied, makes the scratch look locked too.

**Needs a human (all gate T-031):** P1 Play account (register + verify — the 12×14 closed-test
clock can't start before it; **done per the entries above, owner-reported same day**), D5 upload
keystore, OPEN_QUESTIONS 9 (privacy hosting + contact email — fill the two `[TODO]`s in
`docs/PRIVACY_POLICY.md`), OPEN_QUESTIONS 12 (owner moves the camera-in-v1.0 decision into
DECISIONS.md). First Console upload: accept Play App Signing ToS, let Google generate the app
signing key, upload `builds/MotionRunner.aab`.

## 2026-08-23 — Catalog redesigned with the owner: 2 skins + Season 1 pass; dashboard setup unblocked (owner dashboard session)

Owner direction during RevenueCat dashboard setup: the two-SKU colour-pack catalog is replaced by
**2 premium skins sold individually + a Season 1 pass (free + paid cosmetic reward tracks, XP from
run score)**. Still cosmetics-only — D6 unchanged; retention was the gap the pass fills. Written up
in **`docs/COSMETICS_CATALOG.md`**: product/entitlement/offering tables (3 products, 3 entitlements,
all NON-CONSUMABLE), the 10-level Season 1 ladder, item feasibility notes (everything composes from
`RuntimeMaterials` + TrailRenderer + ParticleSystem + primitives — no new art tech), and the owner
sign-off checklist (§7). OPEN_QUESTIONS 9 rewritten to point there; REVENUECAT_PLAN §4 marked
superseded (rest of the plan stands); T-020 backlog line updated; **T-025 added** (XP + pass +
locker — pure C#/UGUI, deliberately *not* blocked on P1, the right work while Play Console approval
is pending). Also fixed an accidental paste that had corrupted source [S22] in REVENUECAT_PLAN.md.

Verified: docs only, no Unity, no build. Cross-references checked by hand.

**Needs the owner:** tick `COSMETICS_CATALOG.md` §7 (ids/prices/names are the one-way doors — ids
must match Play Console character-for-character later); then the RevenueCat dashboard can be filled
today without the Play Console (project, Play Store app by package name, API key, 3 entitlements,
3 products *manually by id, non-consumable*, offering `default`, paywall). Service-account JSON,
Play-side products, license testers and promo codes still wait on P1.

**Next:** T-025 can start immediately in Unity; T-020 the day P1 + P4 exist.

## 2026-08-23 — T-020 prep: RevenueCat integration plan written, ready to execute in one session (no-unity-prep session)

No Unity opened, no code written, no build made — by instruction. Produced
**`docs/REVENUECAT_PLAN.md`**: version pin + rationale, install method, Unity/Android gotchas, the
wrapper design, the catalog proposal, Paywall-Builder server-vs-app split, the judge path, and an
ordered checklist. Every external claim carries a link, checked live today.

**The three findings that change the plan:**

1. **Pin `9.8.1`, not `≥ 8.4.0`.** 9.8.1 shipped **20 Aug 2026** — three days ago. 8.4.x is four
   minors behind. 9.x brings Play Billing 8.3.0 (vs 8.0.0), min SDK 23 (ours is 26), multipage
   paywalls, and the ad primitives T-023 needed to evaluate. Pin exactly — RevenueCat ships ~weekly
   and an unpinned bump between the closed-testing build and the production build would move the
   store's Billing dependency underneath us. Backlog AC updated.
2. **One dashboard toggle can permanently break T-020's acceptance criterion.** From SDK 9.0.0
   RevenueCat removed the workaround that allowed restoring consumed one-time products: a one-time
   product misconfigured as **consumable** is consumed and can never be restored. We have no login
   system and our AC is literally *"entitlement survives reinstall"*. **Every SKU must be
   non-consumable**, set before the first purchase.
3. **The Paywall Builder is the best schedule lever in the project.** The entire paywall — layout,
   copy, colours, packages — is server-configured; the app contributes one `PaywallsPresenter`
   call. So the paywall can be redesigned throughout the judging window with no store review. Get
   the *call* into v1.0; do not spend September polishing pixels in Unity.

**Wrapper seam** (CLAUDE.md rule 5), mirroring the existing `MotionRunner.Track`/`.Pose` shape:
`MotionRunner.Commerce` (engine-free, `noEngineReferences: true`) holds `IStore`, `Entitlements`,
`CosmeticCatalog` and a `FakeStore`; `MotionRunner.Commerce.RevenueCat` holds the adapter and is the
only assembly that sees the SDK. The SDK ships its own asmdefs, so — unlike `CameraFaceInput` — the
adapter does **not** have to live in Assembly-CSharp. EditMode tests reference only the engine-free
assembly, so `-runTests` keeps working with or without the package (and the SDK cannot run in the
Editor at all, so this is a requirement, not a nicety).

**Owner must review/decide:** OPEN_QUESTIONS **9** (SKU ids, entitlement name, prices — product ids
are immutable once created). Historical question **10** is now resolved (23 Sep): the judge-login
proposal is retired and Google Play promo codes are the primary route; see REVENUECAT_PLAN §6.

**Two things found while reading the repo, both cheap, both embarrassing if missed:** Player Settings
still say `productName: Motion Runner` / `companyName: DefaultCompany` (that string is the launcher
label), and adding the SDK will pull Play Billing + Jetpack Compose into the APK — per gotcha 10 the
release APK must be size- and permission-diffed immediately after the package lands, before any
paywall work, so the UGUI fallback is still an option if Compose blows the budget.

**Blocked on:** P1 (Play Console — requested, pending) and P4. Nothing else.

## 2026-08-23 — T-023 RevenueCat Ads spike: **works from Unity, still a NO for v1.0** (no-unity-prep session)

Desk research only, no integration, ~2 h of a 1-day timebox. Verdict as required by the AC:

**Technically YES.** Unity is a supported platform for RevenueCat ad monetization —
`purchases-unity` **9.1.0+** for the `AdTracker` API (the docs carry a Unity C# sample), and
**9.8.0+** for reward verification. Confirmed in the source, not just the docs:
`GenerateRewardVerificationToken` and `PollRewardVerification` are public on `Purchases` at
`RevenueCat/Scripts/Purchases.cs:729` and `:746` @ 9.8.1.

**Strategically NO for v1.0**, four reasons:

1. **RevenueCat Ads does not serve ads.** It is impression/revenue *tracking* that sits alongside an
   existing ad stack via ILRD callbacks — "we aren't replacing your mediation platform". Shipping it
   means first shipping a real ad SDK: AdMob, AppLovin MAX, ironSource/LevelPlay or Unity Ads.
2. **Rewards need AdMob SSV, and Unity has no adapter.** Server-verified rewards are verified
   through AdMob Server-Side Verification. RevenueCat's `loadAndTrack` helper exists for Swift and
   Kotlin only — the AdMob integration page has no Unity samples. Unity is the manual path: Google
   Mobile Ads Unity plugin + hand-plumbed `AdTracker` calls + reward tokens threaded through AdMob
   SSV custom data.
3. **Cost against the calendar.** An AdMob/AdSense account (approval takes days), a second ad SDK in
   the APK, an advertising-ID permission, a Data-safety redeclaration, and a UMP consent flow — all
   before ~12 Sep, with the Play Console not yet in existence. The feature is also in **beta** (ad
   revenue does not count toward MTR yet).
4. **It would read as box-checking.** The category rewards "RevenueCat Ads as a monetization
   method"; screeners explicitly penalise category overreach. One rewarded revive bolted on in the
   final week is the canonical example.

**Consequence:** Catvertising is **not** entered (D7 already said "only if the spike passes").
Removed from the target list in `docs/submission/DEVPOST_ANSWERS.md`. The v1.0 listing declares
**no ads**, which is also a positioning asset — "no ads interrupting a run" is in the store copy.

**If it is ever revisited (Update 2, only if T-020 shipped and production is live by ~20 Sep):**
one rewarded placement, on the result screen, offering a single revive per run — never mid-run, never
auto-playing. Behind `IStore`-style seam + a remote feature flag, defaulting off. A revive is the one
ad in a runner that players ask for rather than tolerate; it must never be purchasable, or the
cosmetics-only promise (D6) breaks and the Daily Run stops being comparable between players.

Evidence: RevenueCat ad monetization overview and rewards docs
(https://www.revenuecat.com/docs/ad-monetization, /rewards, /manual-integration, /admob), Unity SDK
9.1.0 and 9.8.0 changelog entries, and `Purchases.cs` @ 9.8.1.

## 2026-08-23 — T-030 store listing kit: copy, shot list, art brief, tester pack (no-unity-prep session)

Copy only, no binary assets — by instruction. New `docs/store-kit/`:

- **`LISTING.md`** — app title, short and full descriptions, plus alternates, Console field values
  (category, content rating, ads=no, target audience) and a camera-mode delta block to paste **only**
  once that update is live. Written around "motion-controlled endless runner" search terms.
- **`check-lengths.sh`** — asserts the copy against Google's 30/80/4000 limits. Currently
  24 / 78 / 2077. Run it before pasting anything into the Console.
- **`SCREENSHOTS.md`** — 7-shot list with the exact game state per shot, the capture commands
  (including the `MSYS_NO_PATHCONV=1` trap), and the arithmetic for the Devpost **1179 × 2556
  frameless** shot: from 1080 × 2400, scale to width 1179 → height 2620, crop 64 px off the *top*.
- **`ART_DIRECTION.md`** — icon (512/1024) and feature-graphic (1024 × 500) brief, three icon
  concepts, the in-game palette as hex (`#FF8C26` runner orange, `#171A29` background — taken from
  the code so the icon and the first screenshot match for free), and three style frames for
  OPEN_QUESTIONS 4. Leads with the non-automotive constraint from the VEYRON note, including the
  subtle version: the three-lane strip must read as a *path*, not a road.
- **`CLOSED_TESTING.md`** — the 12×14 rule re-verified, dates worked backwards from 30 Sep,
  recruitment message, opt-in instructions written for someone who has never done it, and the five
  questions to ask testers. **Recruit 16–18, not 12**, and upload before the kit is finished — the
  clock is the scarce resource, not polish.

**Two overpromises caught by checking the copy against the code, both removed:**

- **Free Run is not reachable.** `RunMode.Free` exists and `RunHud` would render "FREE RUN", but
  `RunSession.Mode` is hard-coded to `Daily` (`RunSession.cs:33`) and nothing sets it. Worth a few
  lines in the T-020 session to expose; until then it is out of the listing.
- **Sliding does not exist.** `IsSlidePressed()` is plumbed through `CompositeInput`,
  `TouchTapInput`, `KeyboardInput` and `FaceSteering` — and `RunnerController.Step()` reads only
  `GetMoveAxis()` and `IsJumpPressed()`, so the input arrives and is dropped. Both "crouch to slide"
  claims are gone from the listing and the camera copy.

**Owner must review** all copy before it is pasted anywhere. **Still open:** the binary assets (icon,
feature graphic, captures) need a device and a designer. **New blocker surfaced:** Play requires a
hosted **privacy-policy URL** even for an app that collects nothing → OPEN_QUESTIONS 9 (renumbered
in the 24 Aug merge).

## 2026-08-23 — T-035 submission prep: video script, Devpost answers, artefact checklist (no-unity-prep session)

New `docs/submission/`:

- **`VIDEO_SCRIPT.md`** — timecoded to 1:50 against a hard 2:00 cap, opening 0:00–0:18 on the
  hands-free shot in **one continuous take** (a cut in the lean-swerve beat reads as a fake, and this
  is the only 18 seconds a screener is guaranteed to watch). Includes a contingency if camera mode
  misses production: open on the tilt shot and demote the camera footage to a labelled BETA segment,
  rather than headlining an unreleased feature.
- **`DEVPOST_ANSWERS.md`** — a category audit first (four solid entries; five conditional on SDKs
  that are not integrated yet; Catvertising dropped per T-023), then the description draft. It leads
  with the CV failure and the pivot, because that is the most interesting true thing about the
  project and every number in it is traceable to this journal. Placeholders are marked `[N]`.
- **`CHECKLIST.md`** — every artefact the form needs, with producer, status and blocker, plus a
  dated timeline. **Slack in the whole chain is about 8 days, all of it in Play's production
  review.**

**Rule found in the rules text and worth flagging loudly:** *"A Project may have existed before the
Submission Period, but it must not have been publicly released on any eligible store before the
Submission Period. Updates to previously released apps are not eligible."* We are clear — Veyro Run
has never been released — but this is why nothing gets pushed to **production** to "test the
pipeline". Closed testing is not a public release; a production rollout is, and it would consume the
one first-release event the submission depends on.

**Blocked on P1** for everything downstream (store URL, promo codes, real numbers).

## 2026-08-23 — T-040: first three #BuildInPublic posts drafted (no-unity-prep session)

`docs/posts/2026-08-camera-mode.md` — three posts, X and LinkedIn variants each, covering the CV
pivot, the hands-free demo, and the input-seam engineering story. **Nothing posted; nothing will be
by an agent.**

Every number is traced to a STATUS entry in a table at the bottom of the file, along with an explicit
do-not-claim-yet list (monetization, install numbers, leaderboards, sliding, Free Run). One
discrepancy resolved there so it never leaks into two different posts: the 22 Aug feasibility sweep
measured **4.2 ms**, the shipping in-game gate reads **3.7 ms** — posts use 3.7 and say what it is.

The lead post is the failure: BlazePose at 118 ms against a 30 ms gate, the surprise that CPU beat
GPUCompute by 2.5×, and the pivot to a 3.7 ms face detector — "a much worse model of a human and a
much better input device". The negative result is the strongest content this project has.

**Blocked on P9** — the social account does not exist yet. That is minutes of work and it is the
actual constraint on a category worth $30k that costs no engineering time.

## 2026-08-23 — T-011/T-012/T-013: camera mode is in the game, on the `camera-feature` branch (camera-feature session)

**Headline: the game now boots to a mode menu, and camera mode drives it end-to-end on the
device.** Owner chose OPEN_QUESTIONS 7(d) on 22 Aug; this implements it. On the Nord 2 the full
chain ran unattended: speed gate **3.7 ms blocking median → PASS**, camera permission, orientation
probe settled at score 0.89 (again overruling the device's wrong rotation report, 270 → 90+flip),
face acquired, run started hands-free. `main` still holds v1.0 untouched; this branch's APK
installs as **`com.ferrabled.veyro.camdev`** ("Veyro Cam Dev", `builds/VeyroCamDev.apk`, 44.8 MB)
so it can never replace the working game on a phone.

**How it fits together** (all new code except the adapter lives outside Assembly-CSharp):
- `MotionRunner.Pose.FaceSteering` (engine-free, clock-free): face position → LEFT/RIGHT (neutral
  band + dead zone + low-pass), JUMP (upward velocity + refractory), SLIDE (sustained crouch +
  hold time), loss decay, slow neutral drift. 13 EditMode tests drive synthetic 30 Hz
  trajectories; writing them caught a real design bug — standing up from a crouch is a jump's
  exact velocity signature, so ending a slide now arms the jump refractory.
- `MotionRunner.CameraInput` (new ungated asmdef): `CameraFeed` + `BlazeAffine` moved here from
  the spike (spike asmdef now references this one), plus `FaceAnchors` (the 896 BlazeFace anchors
  generated in code), `FaceDetector` (BlazeFace short-range, CPU backend, argmax folded into the
  graph, startup self-check on the box tensor width), `FaceTrackingRig` (state machine:
  gate → permission → camera → probe → track; the gate runs *before* the permission ask so a
  too-slow device never gets prompted).
- `CameraFaceInput : IGameInput` (in Assembly-CSharp with the other adapters — asmdefs cannot
  reference Assembly-CSharp, so the adapter lives beside GyroTiltInput). Stale observations
  (>0.35 s) read as loss.
- `ModeSelectMenu` (code-first UGUI): TILT & TOUCH vs CAMERA (BETA), rig states narrated
  on-screen, every camera failure falls back to the menu with the reason. Camera mode keeps
  touch+keyboard in the composite as backup. Last choice remembered (`veyro.input_mode`), menu
  shown every launch.
- **No downloads, ever**: the 418 KB BlazeFace ONNX is committed at
  `Assets/Scripts/CameraInput/Resources/CameraInput/` and ships in the APK. `docs/cv-spike.sh off`
  no longer removes `com.unity.ai.inference` — on this branch it is a shipping dependency
  (committed in the manifest; the APK carries its ~8.8 MB and CAMERA by design).
- `BuildScript.BuildAndroidCamDev` builds the dev-flavoured APK; `BuildAndroid` is untouched and
  still produces the real package name for the eventual post-release update (D2).

**Verified:** 140 EditMode tests green (127 + 13 new). On device: menu renders; TILT & TOUCH
starts a normal run (screenshot); CAMERA (BETA) ran the full pipeline unattended — `[CAM]` logcat
lines show gate PASS at 3.7 ms, orientation settled, and the run started from a face sighting.
No runtime exceptions; memory 332 MB PSS with camera+inference live (game alone was ~231 MB).
One build-loop trap hit: launching an APK build seconds after a `-runTests` run on the same
scratch copy deadlocks Unity ("More than one copy of bee_backend running" → build waits forever
at Compiling Scripts). Leave a gap or check for lingering `bee_backend` before batchmode builds.

**Needs a human (the fun part)**
- **Play it.** `builds/VeyroCamDev.apk` is installed. Prop the phone up, pick CAMERA (BETA), step
  back 1.5–2.5 m: lean to steer, hop to jump, crouch to slide. The verdict this needs: does
  steering feel 1:1, do jumps fire when you hop (and only then), does the short-range model still
  see you at play distance in your room's light?
- Tuning knobs live in `FaceSteering` (all public fields, defaults are first guesses):
  `HalfRangeX` (lean sensitivity), `JumpVelocity`, `SlideDrop`. Report what feels wrong.
- T-012's false-positive AC (<1/min standing still) needs a few timed minutes in frame.

**Next:** owner playtest → tune → then T-020 RevenueCat (unstarted, still the critical path;
Play Console account requested and pending). Do not merge to `main` until v1.0 has shipped —
this branch deliberately trades the clean 29.6 MB/INTERNET-only baseline for camera mode.

## 2026-08-22 — T-010b feasibility sweep: **the 30 ms gate is beatable — 4.2 ms, but not with BlazePose** (track-b/cv-feasibility)

**Headline: YES, camera control clears 30 ms on the Nord 2 — by switching models, not backends.**
BlazeFace short-range (418 KB, HF `unity/inference-engine-blaze-face`, Apache-2.0) on the **CPU
(Burst) backend with a blocking readback medians 4.2 ms** isolated (p95 8.3), and **9.1 ms inside a
live 60 FPS loop at 59.4 Hz with 0 of 359 frames over 20 ms**. Face centre-x is LEFT/RIGHT, face-y
velocity is JUMP, sustained face-y drop is SLIDE — all three signals the game needs, from one model
36x smaller than the pose detector. Measured on-device over three runs with the same harness as
T-010 (`CvFeasibilitySweep`, logged as `[CV] FEAS` lines; T-010's own benchmark re-ran alongside as
the control and reproduced within run-to-run variance).

**The one table that matters** ("isolated" = uncapped, like T-010's table; "live" = running
continuously while the app renders under a 60 FPS cap, empty scene + camera feed):

| model | backend | readback | isolated med | live: inference / rate / frames >20 ms |
|---|---|---|---|---|
| **face** | **CPU** | **blocking** | **4.2 ms** (p95 8.3) | **9.1 ms / 59.4 Hz / 0/359** |
| face | CPU | async await | 33.6 ms (raw 8.4) | 17.0 ms / 50.4 Hz / 0/360, main thread 1.1 ms |
| face | GPUCompute | async | 100.8 ms (raw 23.5) | 66.9 ms / 15.7 Hz / 3/357 |
| face | GPUPixel | async | 58.3 ms (raw 71.9) | — |
| lite landmarker | CPU | blocking | 31.0 ms (p95 45.6) | — |
| lite landmarker | CPU | async | 65.7–67.3 ms | 50.7 ms / **18.3 Hz** / 3/356 |
| lite landmarker | GPUCompute | async | 184–211 ms | 150.4 ms / 6.8 Hz / **43/165** |
| lite landmarker | GPUCompute, ScheduleIterable | async | — | 151.9 ms / 6.4 Hz / 87/240 — slicing does not help |
| no-ML motion centroid 160×120 | CPU | — | 0.05 ms (+1.4 ms sync GPU readback) | — |

**Why T-010's numbers overstated the cost:** an awaited `ReadbackAndCloneAsync` resumes on a frame
tick, so async latency is *quantized to multiples of ~16.7 ms*. T-010's "67.9 ms lite on CPU" was
~43 ms of Burst work plus readback rounding; face-on-CPU reads 33.6 ms async but 4.2 ms blocking.
**T-013's startup gate must measure the blocking CPU cost, not awaited latency**, or it will fail
devices that are actually fine. (This also closes T-010's 118-vs-170 ms puzzle: GPU numbers swing
~±20% run to run with queue warmth and DVFS — the end-of-sweep repeat read 190 vs 210 at start,
same harness. Nothing was wrong with either measurement; the GPU path is just dead anyway.)

**Hypotheses killed, with evidence** (each cost minutes on device; none need revisiting):
- **Quantization (H2): broken in Inference Engine 2.6.1 for these models.** Every attempt threw —
  face+u8 `NullReferenceException`, lite+fp16/u8 `KeyNotFoundException '362'`. The docs also state
  quantization reduces size, not inference time, so this lever was doubly dead.
- **GPUPixel (H3): no rescue.** face 58.3 ms, lite 137.9 ms (raw 461 — worse than its own latency).
- **GPU anything, live (H4): unplayable.** GPUCompute inference alongside rendering hitches badly
  (frame p95 100.8 ms, 43/165 frames over 20 ms) and `ScheduleIterable` slicing makes it *worse*
  (87/240) — dispatch is spread but the GPU is still saturated.
- **The pose detector must never run on the CPU backend.** Creating that worker OOM-killed the
  whole process twice (`ApplicationExitInfo reason=3 LOW_MEMORY` at `importance=100` — foreground —
  with 5 GB free, no crash log, signal 9). A gate/adapter that ever tries it takes the game down
  with it. The face-based scheme doesn't need the detector at all.

**The three answers for OPEN_QUESTIONS 7:**
1. *Is there a configuration that clears 30 ms?* **Yes: BlazeFace short-range, CPU backend.**
   4.2 ms blocking / 17 ms async, measured by `CvFeasibilitySweep` on the device, three runs.
2. *Cheapest usable LEFT/RIGHT/JUMP, at what Hz, leaving 60 FPS?* Same configuration: 30–60 Hz with
   the game's frame rate intact (0 frames over 20 ms in 6 s soaks; async mode costs the main thread
   1.1 ms per inference). Full-pose (lite landmarker, CPU, async) coexists with 60 FPS at 18.3 Hz —
   fine for a skeleton *overlay demo*, still 50 ms latency as an *input*.
3. *Park / invest / drop?* **A fourth option now exists and is the recommendation: build camera
   mode on BlazeFace-on-CPU** (updated in OPEN_QUESTIONS 7). Path B (MediaPipe plugin, H7) is no
   longer needed for performance and was not tested. No-ML optical flow (H6) is nearly free
   (1.5 ms) but unnecessary given the above; signal quality untested.

**Not tested, and why:** H7 MediaPipeUnityPlugin (obsolete — the need it addressed is gone);
BlazeFace *detection reliability at 1.5–3 m play distance* — the short-range variant is trained for
arm's-length faces, and **this is the one open risk in the recommendation**; it needs a person in
frame (below). The full-range BlazeFace variant is not in Unity's HF repo and would need ONNX
conversion — only worth it if the range test fails.

**Changed** (spike-side only; the release surface is verified below):
- `Assets/CV/CvFeasibilitySweep.cs` (new): the H1/H2/H3/H4/H6 harness, kept for re-running on other
  devices. Wired into `CvSpikeController` after the T-010 benchmark.
- `CvSpikeBuild` stages `blaze_face_short_range.onnx` (gitignored in `game/CvModels/`), and
  `docs/cv-spike.sh on` now fetches it alongside the pose models, so the sweep is reproducible.
- Main `Packages/manifest.json` was never touched: the inference package was enabled only in the
  batchmode scratch copy. The committed tree stays in the "spike off" state throughout.

**Correction to T-010's "release untouched" claim, found by the verification diff:**
`Assets/CV/Resources/Cv/` (81 KB anchors.csv + the affine compute shader) was shipping in every
release APK — Unity includes all `Resources/` content regardless of the asmdef gate. Fixed by
moving them to `Assets/CV/Data/` and having `CvSpikeBuild` stage them per spike build exactly like
the models (unstaged in the same `finally`); `.gitignore` now blocks the whole staged tree.

**Verified:** release APK rebuilt from this tree (clean scratch): permissions INTERNET-only,
launcher activity `UnityPlayerGameActivity`, 31,027,111 bytes vs the 21 Aug baseline's 31,007,023 —
the **only** delta is `MotionRunner.Pose.dll` (+20 KB, the ungated engine-free Pose assembly, which
is intended to ship with T-011+; gate it too if zero footprint matters before then). A control
rebuild of the pure 21 Aug tree came out byte-identical to the baseline, so the toolchain itself is
stable. Three instrumented device runs; runs 1–2 died to the detector-on-CPU OOM (that's how it was
found), run 3 completed the full sweep in 53 s with logcat streamed to disk. The verification also
caught a polluted scratch copy masquerading as a tree change (a leftover
`Assets/Plugins/Android/AndroidManifest.xml` flipping the launcher activity and adding VIBRATE, and
a `SENTIS_ANALYTICS_ENABLED` define the inference package wrote into ProjectSettings that outlived
its removal) — now CLAUDE.md gotcha 11.

**Needs a human**
- **OPEN_QUESTIONS 7, updated in place** — the options changed materially in camera mode's favour.
- **The range test (10 min, needs a body):** install `builds/VeyroCvSpike.apk`, stand 1.5–3 m from
  the front camera in game-typical light, and check the detector score stays high — the spike's
  overlay currently tracks *pose*, so treat this as approximate until a face overlay exists
  (~half a day, only worth it if OPEN_QUESTIONS 7 resolves to "build it").
- Sanity-check the SLIDE mapping (sustained face-y drop = crouch) feels acceptable before T-012
  encodes it.

**Next:** nothing in Track B until OPEN_QUESTIONS 7 is answered. T-020 RevenueCat remains the
critical path and is untouched by all of this.

## 2026-08-22 — T-010 done: BlazePose runs on device, and **misses the 30 ms gate by 4x** (track-b/cv-spike)

**Headline: NO.** On the OnePlus Nord 2 (Dimensity 1200, Mali-G77 MC9, Vulkan), the BlazePose
**lite landmarker medians 118 ms** per inference — the T-013 gate is 30 ms. The detector medians
217 ms, so a full detect+track cycle is ~334 ms (**~3 Hz**). Camera mode as a real-time input is
not viable on this hardware over Path A. Measured over 57 warm inferences; warm-up discarded.

AC met: 33 landmarks drawn over the live front-camera feed, tracking a person, verified by
screenshot. `[CV] STATS` / `[CV] VERDICT` lines are in logcat under tag `Unity`.

**Isolated benchmark** (no body needed — this is the same measurement T-013's startup gate will
run). "latency" = schedule → await readback, one at a time, which is what a frame pays. "raw" =
20 scheduled back to back with one readback, which is the GPU cost with await and frame-boundary
overhead removed. Frame rate uncapped during the sweep, or every number would round up to a
multiple of 16.7 ms:

| model | backend | latency median | p95 | raw per inference |
|---|---|---|---|---|
| pose detector | GPUCompute | 183.8 ms | 187.0 | 145.7 |
| landmarker lite | GPUCompute | 169.9 ms | 205.7 | 110.9 |
| landmarker full | GPUCompute | 233.9 ms | 253.2 | 123.2 |
| landmarker lite | **CPU (Burst)** | **67.9 ms** | 93.5 | **43.6** |

Two things fall out of that table, and both matter more than the headline number:

1. **Raw ≈ latency.** Removing per-await overhead saves ~35 %, not 4x. The model really is this
   expensive here; no amount of pipelining or frame-skipping rescues it.
2. **The CPU backend is 2.5x faster than GPUCompute** on the same model. Inference Engine's generic
   compute shaders are badly suited to this Mali GPU — exactly the risk handoff 8.2 flagged. Worth
   knowing before anyone spends a day optimising the GPU path. Even so, CPU at 43.6 ms raw is still
   above 30 ms, so switching backend does not by itself clear the gate.

**Changed** (nothing in Track A, C or D; deleting `Assets/CV`, `Assets/Scripts/Pose` and
`Assets/Editor/CvSpikeBuild.cs` would leave the game exactly as it was):
- New engine-free assembly **`MotionRunner.Pose`** (`noEngineReferences: true`, same pattern as
  `MotionRunner.Track`): `PoseJoint`/`PoseLandmark`/`PoseFrame`, `PoseSkeleton`, `PoseGeometry`
  (torso centre, shoulder width — T-012's seam), `LatencyStats` (median/p95 + the `<30 ms` gate),
  `PoseAffine2x3`/`FrameOrientation`, `OrientationProbe`. Clock-free and injected-input, like
  `DailySeed`, so T-012 can state a false-positive rate and measure it headlessly.
- New Unity-side `MotionRunner.Cv` (`Assets/CV/`): `BlazeAffine` (ported from Unity's Apache-2.0
  `BlazeDetectionSample/Pose`), `BlazePoseRunner`, `CameraFeed`, `PoseOverlay` (UGUI), `CvBenchmark`,
  `CvSpikeBootstrap`/`CvSpikeController`.
- `com.unity.ai.inference` **2.6.1** (sample targets 2.4.0; no API drift, and 2.6.0 fixes a
  GPU-compute convolution-padding crash worth having). **Not left in the manifest** —
  `docs/cv-spike.sh on|off` toggles it, for the reason under "keeping the spike out of the game".
- `Assets/Editor/CvSpikeBuild.cs` builds a **separate** `builds/VeyroCvSpike.apk`, package
  `com.ferrabled.veyro.cvspike`, so it installs beside the game instead of replacing it.

**Keeping the spike out of the game** needed all four of the measures below, and the last two were
found only by rebuilding the release APK and diffing it against the 21 Aug baseline. Guarding the
entry point was *not* enough, and nothing in the editor warned about it:

| release APK | size | CAMERA permission |
|---|---|---|
| 21 Aug baseline, pre-CV | 29.6 MB | no |
| CV code compiled in | 44.6 MB | **yes** |
| + `MotionRunner.Cv` behind `defineConstraints` | 38.0 MB | no |
| + package out of the manifest | 29.6 MB | no |

1. Its own package name, so the spike installs beside the game.
2. The `RuntimeInitializeOnLoadMethod` hook is behind `VEYRO_CV_SPIKE`.
3. **`MotionRunner.Cv` is gated on that define via `defineConstraints`.** Unity scans compiled
   assemblies for `WebCamTexture` and had added CAMERA to the manifest of a game that does not use
   a camera, plus 15 MB. Cost: the editor no longer type-checks the CV code by default — a spike
   build is what compiles it, and it fails loudly.
4. **The package is not in the committed manifest.** Inference Engine ships a large compute-shader
   library in its own Resources, which Unity puts in the APK whenever the package is present,
   referenced or not — 8.8 MB for nothing. `docs/cv-spike.sh on|off` toggles the package and
   fetches the weights, and is how anyone reproduces this spike.

The 20 MB of weights are separately staged into `Resources` by the spike build and unstaged in a
`finally`, so they never reach a release APK either.

**Verified:** 127 EditMode tests green (81 before, 46 new). Spike APK built headless in a scratch
copy and run on the phone. Release APK rebuilt from this tree is byte-for-byte the baseline size
with no extra permissions, so Track A is untouched.

**Two bugs the device caught**
- *The orientation probe trusted noise.* With nobody in frame all eight candidates scored ~0.14 and
  it locked onto `rot=0`, overruling the device's `rot=270`, because 0.147 beat 0.146. It now needs
  the winner to clear a confidence floor, otherwise it keeps the device report and re-probes.
  Three tests cover it, one replaying that exact noise pattern.
- *The device's reported rotation is wrong here anyway.* With a person in frame the probe settled
  confidently (score 0.75) on `rot=90` while `videoRotationAngle` reports 270. Building the probe
  instead of trusting the API was the right call, and T-011 should keep it.

**Needs a human**
- **The scope call this number forces** — OPEN_QUESTIONS 7. Recommended default: park camera mode
  as a *demo* (a menu toggle at ~3 Hz, hands-free but not playable) and do not put it in the pitch
  as an input mode. T-011–T-013 stay unstarted until that is answered.
- Where the BlazePose weights should live once camera mode ships — OPEN_QUESTIONS 8.
- A device with a different GPU would be worth 10 minutes: this verdict is one SoC wide.

**Next:** nothing in Track B until OPEN_QUESTIONS 7 is answered. T-020 RevenueCat remains the
critical path.

## 2026-08-21 — T-003, T-005, T-008 done; named "Veyro Run" (track-a/chunks-session)

**T-003 chunk system + T-005 obstacles/scoring/result screen.**
- New engine-free assembly `MotionRunner.Track` (`Assets/Scripts/Track/`, `noEngineReferences: true`):
  chunk metadata + library, generator, seeded PRNG, difficulty curve, collision geometry, scoring.
  No Unity dependency, so it is testable headlessly and identical on Mono and IL2CPP.
- Unity side: `TrackDirector`, `ChunkView`, `RunSession`, `RunHud` (UGUI from code). `RoadScroller`
  deleted. `RunnerController` is now driven by `RunSession.Step(dt, input)` for a fixed frame order;
  its tuning constants are untouched, so the T-002 feel is preserved.
- `com.unity.ugui` 2.5.0 added to the manifest. `RuntimeMaterials.Shared(color)` added (cached).
- AC amendment: chunk metadata is plain C# data, not ScriptableObjects — nothing visual to author
  yet. SOs land with T-006. Noted in BACKLOG.

**Verified:** 69 EditMode tests green; APK builds clean; full loop confirmed on the OnePlus Nord 2
by screenshot. Memory over a 10-min tap soak plus a 5-min confirmation soak was flat-to-declining
(231.3 → 231.2 MB PSS on the last two samples), no exceptions. Baseline for future comparison.

**Two bugs the device caught, both fixed:** a visible hole under the player at run start (track began
at z=0; the camera sees back to ≈ -3.4, hence `TrackDirector.FirstChunkZ`), and the result-screen
hint rendering under the RUN AGAIN button.

**Same day:** Unity splash screen removed (`PlayerSettings.SplashScreen.show/showUnityLogo = false`
in `BuildScript`, mirrored in `ProjectSettings.asset`; permitted for Unity 6 Personal per
LICENSING_REVENUE §2.12 — the attribution line is required only if a credits screen is ever added).
Name settled as **Veyro Run**, package **`com.ferrabled.veyro.run`** (reverse-DNS of ferrabled.com,
which the owner holds); verified via `aapt2 dump badging`. Old `com.motionrunner.game` uninstalled.

**T-008 Daily Run (same day).** `RunMode.Daily` is now the default: the seed is the UTC date
(`DailySeed`, clock-free — the caller passes the date in, so generation stays deterministic).
Best scores split into all-time and today-only under `veyro.*` PlayerPrefs keys; the daily bucket
self-resets on date rollover, so it stays one key rather than one per day for ever.

Found and fixed while doing it: the seed mixed in `Application.version`, so **a bugfix release would
have forked the day's players onto different tracks**. The seed now carries
`ChunkLibrary.ContentVersion` instead — bump that, and only that, when the chunk library changes.
`RunSeed.GameVersion` was renamed `ContentVersion` so nobody wires the app version back in.

**Verified:** 81 EditMode tests green (12 new, incl. pinned RNG states for known dates). On device,
two cold launches produced byte-identical runs — same seed `20260821/greybox-1/greybox`, score,
distance and chunk count — the AC demonstrated on real hardware. HUD shows `DAILY · <date>`.

**Needs a human**
- A continuous 10-minute *played* run. The soak taps rather than steers, so its runs are short.
  Install `builds/MotionRunner.apk`, play 10 min watching for hitches at chunk boundaries, then
  compare `adb shell dumpsys meminfo com.ferrabled.veyro.run` against the numbers above.
- Feel of obstacle spacing and the difficulty ramp under tilt. Both are first guesses; T-007 tunes
  them against real play.
- Move the settled app name into DECISIONS.md (owner-only). Answer OPEN_QUESTIONS 6 (score freeze).
- Whether to rename the `MotionRunner.*` C# namespace to match the product. Cosmetic and invisible
  to players; safe while everything is code-first; cheapest before T-006/T-020 grow the surface.
  (The old `motionrunner.best_score` key is gone — T-008 restructured best-score storage while there
  are still no players, which was the free moment to do it.)

**Next:** T-020 RevenueCat as soon as P4 exists (it is the contest eligibility gate), then T-007
(curve + difficulty bands — scaffolding and constraint tests exist, needs the owner's played-run
feedback) and T-006 (art pass, and where chunk data becomes ScriptableObjects).

## 2026-08-20 — T-001 + T-002 done; prototype live on device (orchestrator session)

- Unity 6000.5.9f1 (owner-installed, Android + iOS modules). Project created at `game/` in batchmode:
  URP 17.5.0, Input System 1.20.0, Test Framework 1.7.0; `com.unity.multiplayer.center` removed (D9).
- T-002 prototype, fully code-first: `IGameInput` + Gyro/Touch/Keyboard adapters + `CompositeInput`,
  `RunnerController` (3-lane steer + jump), `GameBootstrap` via `RuntimeInitializeOnLoadMethod`.
  `BuildScript.BuildAndroid` for headless APKs (IL2CPP/ARM64, portrait).
- APK installed on the owner's OnePlus Nord 2 (DN2103, the mid-tier perf target), renders correctly,
  no runtime errors. Headless loop proven end-to-end: `ProjectSetup.SetupUrp` →
  `BuildScript.BuildAndroid` → adb install/launch/screencap. Rebuild cycle ≈ 3–5 min.
- Three rendering traps found and fixed here; they are now gotchas 1–3 in CLAUDE.md.
- **Owner verdict 20 Aug on tilt feel: "feels fine for now" → GO**, which cleared the T-002 gate.

## 2026-08-20 — project scaffolding + owner answers (orchestrator session)

- Shipaton 2026 facts verified against live Devpost/rules/docs; spec is
  `shipaton_motion_runner_handoff_v2.md`. Agent workspace created (CLAUDE.md + docs/).
- Feasibility: Inference Engine + BlazePose confirmed as the primary CV path (official Unity sample,
  Apache-2.0 models); Layers has a Unity UPM SDK; RevenueCat Unity SDK has Paywalls but **not**
  Galaxy Store billing.
- Owner answers: no existing Play *developer* account → 12-testers × 14-days closed testing applies
  (D11; P1 + H6 are the urgent human actions). No Mac — a friend's Apple Developer account is the
  iOS path. Android phone available.
