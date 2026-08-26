# Store compliance register — Google Play

The invariant this file protects: **every release is one atomic story — binary ↔ Console
declarations ↔ privacy policy ↔ store listing all describe the same build.** Mismatches
between any two of these are the #1 cause of Play rejections/removals. Update this file in
the same change that ships a feature listed below.

## Declared state (Console, as of 25 Aug 2026 — versionCode 1/2, no SDKs)

| Declaration | Current answer | True because |
|---|---|---|
| App access (Datos de inicio de sesión) | No — nothing restricted | no login, no paid content |
| Data safety (Seguridad de los datos) | **No data collected or shared** | zero network code in the binary (verified via AAB string/permission dump); camera frames processed on-device in memory = not "collected" per Google's definition (only off-device transmission counts) |
| Content rating | Everything "No" (incl. digital purchases) → PEGI 3 / ESRB E | no violence/gambling/UGC/IAP in build |
| Target audience | **13-15, 16-17, 18+ only**; does not appeal to children | never tick an under-13 bracket — that triggers the Families Policy. Keep listing art non-childlike |
| Ads | No | none |
| Advertising ID (ID de publicidad — mandatory for API 33+ targets) | **No** | manifest verified: no `com.google.android.gms.permission.AD_ID` (Play machine-checks this declaration against the manifest — declaring No with the permission present blocks the release). Flips with any SDK that injects AD_ID via manifest merge: see T-021 entry |
| Government / Financial / Health / News | No | n/a |
| Privacy policy URL | https://veyro.ferrabled.com/privacy/ | must equal `GameLinks.PrivacyPolicyUrl` baked into the binary — see "URL coupling" below |
| Category / contact | Juego → Arcade · ferrabled+veyro@gmail.com · veyro.ferrabled.com | |
| AI-asset declaration (listing graphics) | **"Etiquetar"** — icon + feature graphic tagged as AI-created (owner generated both with an image model, 25 Aug 2026) | screenshots stay UNtagged: they are real gameplay captures. The two tagged assets carry a user-visible AI label on the listing. Editable per-asset in the Asset Library — if the graphics are ever redone as human-directed design work, untag them then. Rule stays: AI-made-but-unlabeled is the only wrong state |
| Binary permissions | INTERNET + CAMERA (+ camera uses-features, all `required=false`) | any new permission in an upload = explain it in the listing + re-check Data safety |

## Per-feature flip table (do ALL flips in the SAME release as the feature)

### T-020 — RevenueCat cosmetic purchases
- App access → **Sí** (the form lists "pagos" explicitly). Instruction entry: "Optional
  cosmetic IAP via Google Play billing; no account/code exists; all gameplay accessible
  without purchase."
- Data safety → Financial info / purchase history = collected (RevenueCat is a service
  provider processing purchase tokens).
- Content rating questionnaire → redo; "digital purchases" = yes.
- Listing → "contains in-app purchases" flag + a cosmetics paragraph. The exact paragraph to paste
  is held ready in `docs/store-kit/LISTING.md` §3 under "HELD BACK — the COSMETICS ONLY paragraph",
  together with the §5 Console rows (IAP flag, content rating) that flip in the same release.
  (Path updated 26 Aug: the old `listing.md` was merged into `LISTING.md`.)
- Privacy policy → add purchases section, bump effective date, redeploy site
  (docs/PRIVACY_POLICY.md header comment marks this).
- Console → set up **License Testing** emails so testers/judges buy without real money.
- Terms already cover purchases ("may offer") — no site change needed beyond privacy.

### T-021 — OneSignal push notifications
- Data safety → device/push identifiers = collected.
- **Check the merged manifest for `com.google.android.gms.permission.AD_ID`** after adding
  the SDK — if present, the separate Advertising ID declaration becomes mandatory (targeting
  API 33+). Strip the permission if unused.
- POST_NOTIFICATIONS runtime permission → request in context, explain in listing if asked.
- Privacy policy → push section; redeploy.

### Layers analytics
- Data safety → app interactions / diagnostics = collected. Privacy policy section. Same
  AD_ID check as OneSignal.

### T-009 — Play Games sign-in, leaderboard, Sidekick (post-v1.0)
- App access → **Sí**, note: "optional Google Play Games sign-in; any Google account works;
  no demo credentials required." (Custom email/password accounts would instead require a
  permanent non-expiring demo account for review — avoid.)
- Data safety → account identifiers + gameplay scores = collected.
- **Account-deletion policy activates** the moment server-side accounts/profiles exist
  (Supabase leaderboard): in-app account deletion + a public web deletion URL declared in
  Data safety. Build the delete endpoint with the leaderboard, not after. The site's
  /support/ "Data deletion" section flips from "nothing to delete" to the real mechanism.
- Privacy policy → accounts/leaderboard section; redeploy.

### If camera data EVER leaves the device (any form, any reason)
- Prominent disclosure + runtime consent before the transmission, Data safety camera
  collection, privacy policy rewrite. Today's "never transmitted" claims are load-bearing
  in the policy, the listing draft, the support page, and the Data safety form — all four
  change together or none.

## Standing rules (every release)

1. versionCode strictly increments (`VEYRO_VERSION_CODE` env var); Play permanently burns
   used codes.
2. Target API floor rises every Aug 31 (36 since 31 Aug 2026) — `BuildScript` pins it; bump
   the pin each summer.
3. New SDK/UPM package → CLAUDE.md gotcha #10: rebuild, `aapt2 dump permissions` diff vs the
   previous release, size diff. A package can add permissions while unreferenced.
4. Policy text changes → bump effective date in BOTH docs/PRIVACY_POLICY.md and
   site/public/privacy/index.html (kept in sync by hand), then `npx wrangler deploy` from site/.
5. Listing describes shipped features only — never roadmap.
6. Content-rating questionnaire must be redone whenever a feature changes any answer.

## URL coupling (learned the hard way, 25 Aug 2026)

`GameLinks.PrivacyPolicyUrl` is compiled into the binary. Changing it requires a REBUILD —
versionCode 1 shipped with a dead GitHub Pages URL baked in and had to be replaced by
versionCode 2. The constant, the Console privacy-policy field, and the live site must always
be the same URL.

## Closed testing / production access (first-app rules, new personal account)

- 12+ testers opted in continuously for 14 days on a CLOSED track (internal testing does not
  count), with genuine usage — then "Apply for production access."
- App updates during the window are fine and do NOT reset the clock; dropping below 12
  opted-in testers or pausing the track does hurt it.
