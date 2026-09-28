# Season Pass implementation review — 20 September 2026

## Initial review: existing seams and gaps

- RevenueCat already sells the pass entitlement and both skins. Keep all three products unchanged.
- The backend already awards accepted runs `min(score / 100, 500)` XP. The banner is still a placeholder, and the client currently does not refresh its profile after submission. Fix that refresh, including replayed offline submissions; never locally add the same XP a second time.
- `CosmeticCatalog` only contains three skins. Extend it with the existing 10 × 2 ladder, independent cosmetic slots and explicit collection state.
- `SkinService` only paints the shirt. Extend its role to persisted, ownership-checked loadouts; retain the shop's equip seam and migrate the existing selected skin.
- The main menu has reusable pages/cards, but no detail-screen navigation. Add SeasonPassPage and CosmeticsPage as detail screens, not bottom tabs. Home gets a tappable live character preview.
- The runner stays near world z=0 while the track scrolls. Ordinary stationary TrailRenderer emitters would not leave a forward-running trail. Use a short bounded ribbon that accounts for track motion, and small pooled particle systems.
- Earlier 30 FPS behavior is intermittent. Add target/refresh/render-interval and frame-timing evidence around focus/pause before changing frame-rate policy.

## Delivery sequence

1. Pure progression/claim/loadout rules and tests: earned XP gates, entitlement gates, duplicate claims, maximum level, profile isolation, revocation and restore.
2. Profile XP refresh and on-device cache, scoped by profile. Claims/loadouts remain local and can be rebuilt from the recovered XP + store ownership. Offline runs remain pending until the existing backend accepts them.
3. Full timeline with free/paid columns, total XP, progress to next level and collection; locker categories, preview, equip/unequip and clear locked-state explanations.
4. Import selected CC0 cap/top hat/crown and particle textures with provenance. Fit them to the actual head bone; add body finishes, ribbon variants, auras and crash confetti. Preview and gameplay use the same renderer.
5. Headless rule/integration checks, Android build, permission/size comparison and on-phone run/claim/equip/navigation/FPS checks.

## Temporary testing curve

Development builds use cumulative XP thresholds 0–9 (one XP per level). Actual earned XP is never altered or granted by the UI. Test claims and loadouts use a separate storage version so a later production curve cannot inherit test-speed unlocks. Production thresholds need tuning and owner approval before release; the release build is guarded while that remains pending.

Level 1 starts immediately under the existing catalog, with the owned pass's starter reward instantly available. This preserves the existing catalog decision; all later rewards require both earned XP and Collect.

## Release / recovery boundary

No server migration, new product, SDK or permission is required for this implementation. No production deployment is part of this session. XP comes from the existing server profile; local claims and loadout selections do not yet sync across devices. A restored profile can re-collect rewards justified by its recorded XP and current entitlements. Existing backend rollout requirements (including migration 0008 for Free-run XP) still apply.

## Review findings addressed

- Post-run profile refresh was missing. Accepted and duplicate submissions now refresh XP; a failed refresh retries only the profile read with backoff, never the accepted submission. Older profile responses cannot overwrite newer XP.
- A pass entitlement alone must not unlock high-level rewards. Collection checks both XP and current entitlement; equipment revalidates ownership on restore, revocation and profile changes.
- A model's imported head bone carries an additional FBX scale. Accessories are fitted using its actual world scale, with bounds checks and no colliders.
- Preview cameras render only for visible pages. The hidden attract run is stopped; an inexpensive screen-output camera is retained. Each page has its own Canvas batches and raycaster, inheriting shell scale/sorting; first-display UGUI geometry is rebuilt after layout on Home/tab/detail activation to address the alignment problem observed on Android.
- Aura systems are restarted when a runner returns from a hidden menu state. Trails use fixed-size buffers and an analytic soft edge that remains visible against the pale track; particle counts are bounded at 24 for an aura and 30 for a crash effect. With Confetti equipped, the result card appears after a 0.35-second burst window and 0.18-second fade; score/XP recording still happens at the collision, and invisible result buttons cannot be activated.

## Device verification setup

The regular Development APK uses the actual profile and RevenueCat Test Store. A separate
`BuildAndroidCosmeticQa` app (`com.ferrabled.veyro.cosmeticqa`) uses 9 XP and fake ownership
as an explicit local fixture. It never calls profile or purchase services and does not
write the real application's preferences. Rewards still require Collect. This verifies
paid appearances without buying anything, granting real entitlements or fabricating revenue.

The local collection/loadout cache is profile- and curve-scoped. Recovery restores server
XP and store ownership; rewards can then be collected again. Cross-device collection and
loadout synchronization are not implemented in this version.

## Remaining acceptance and release steps

1. Owner device acceptance: open the pass from Home, scroll both tracks, collect an earned
   free item, open the locker by tapping the Home character, preview/equip it, and complete
   a Tilt & Touch run. Check hat clearance and trail visibility during jumps/lane changes.
2. Repeat Camera mode with a person in frame, including lost tracking, pause/hop-twice
   resume, countdown and return to Home. Automation cannot judge physical steering feel.
3. Exercise the real Test Store pass purchase/restore/refund path as part of the existing
   commerce acceptance. Pure/service tests and the isolated fixture cover ownership gates;
   this session does not make purchases or revoke the owner's entitlements.
4. Tune and approve production XP thresholds, then enable the production curve explicitly.
   Preserve the versioned storage boundary; never promote development claims into release.
5. Complete the existing backend/store rollout checklist, including Free-run XP migration
   0008 and the T-009 privacy/declaration flip. Local claims/loadouts need to be recollected
   or reselected after recovery; only recorded XP and store ownership recover remotely.
6. Continue the intermittent FPS investigation if it recurs. Capture target FPS, refresh,
   presentation wait, CPU/GPU time, surface and focus/pause transitions together. Do not
   infer a fix from a short 60 FPS sample.

## Verification evidence

- Full EditMode suite: 464/464 passing, including all catalog renderers, claim/equip gates,
  profile isolation/deletion, restore/revocation, XP refresh retry/order and result reveal.
- Real profile: three Daily Tilt runs scoring 153 each synced and changed the timeline
  from 101 to 104 XP. Cap, Shadow and Confetti were collected; Mint + Cap + Shadow +
  Confetti was equipped and checked in a live run. Paid rewards remained locked without
  the pass. No local XP grants were used. Frost was reselected and confirmed after a
  cold relaunch; the regular app is left on Home.
- Isolated QA: collected cap, Chrome, Crown, Aurora, Comet, Confetti and Prism; equipped
  Prism, ran with its crown/outfit, and confirmed its selection survived a cold relaunch.
  A composed Chrome + Crown + Aurora + Comet + Confetti loadout was also verified in the
  saved QA selection and on the running model; burst frames show Confetti before results.
- Final regular APK: `1.0.0-dev.20260920-1748.71e7c7f`, 88,060,034 bytes,
  +919,850 bytes versus the baseline. Permissions match the previous art build exactly.
  Installed over the existing app without clearing data; final cold-launch, timeline,
  collection, category navigation, equipped run and return-to-Home checks passed.
- CPU/GPU/presentation and focus/refresh diagnostics sampled roughly 16.8 ms p50/p95
  across menu, locker, timeline and runs, including background/resume and screenshots.
  The intermittent 30 FPS issue was not reproduced and remains open.
- Ignored evidence: `builds/season-tests-final.xml`, `builds/season-main-verified.log`,
  `builds/season-review/main-final-*.png`, `qa-isolated-*.png`, `veyro-qa-burst02.png`
  (Confetti before results), and `builds/season-review/performance.txt`.


## Owner-directed presentation update — 20 September 2026

The reference direction is Rocket Pass for progression and a Pokemon Go-style wardrobe
for cosmetics, adapted to portrait. Presentation changes retain the earned-XP/entitlement
rules above:

- The pass is a horizontally draggable tier timeline, premium above free. Each item has
  a baked thumbnail of the actual model/effect. The selected reward has one large live
  preview and a clear Collect / In Locker / Get Pass / XP Needed action. My Level recentres
  on recorded progress; the scrollbar and previous/next controls also navigate the track.
- The locker uses a large character stage above a rounded wardrobe sheet: six categories,
  All/Owned filters and a three-column thumbnail grid. Previewing a locked item never equips
  it. View Reward opens that exact tier; a collected reward opens its exact locker category.
- The camera eases to the head for hats, the torso for body finishes, an angled wider view
  for trails, and full-effect framing for auras/confetti. Drag the model to rotate it;
  confetti previews replay automatically. Leaving the page disables its stage/camera.
- Detail pages temporarily use the header/tab space; Back restores the existing three-tab
  menu. No extra tab, purchase product, XP source or unlock rule is introduced.
- UI panels are native rounded canvas geometry. All 23 thumbnails are pre-rendered,
  transparent 256-pixel sprites, not extra live cameras. The actual selected model uses
  one 768-pixel render target. Phone samples stayed near 60 FPS after the change.

Device verification is complete on the Nord 2: horizontal/vertical scrolling, selected-item
navigation, camera categories, drag rotation, Owned/empty filters, cap equip/run, earned Gold
collection, and return to Home. 467/467 tests pass; final installed build is
`1.0.0-dev.20260920-1906.71e7c7f`, with unchanged Android permissions. Real XP is now 105.
The current Runner selection was restored. Screenshots/logs are in `builds/horizontal-review/`;
STATUS records the APK size and remaining owner visual/physical-input acceptance.
