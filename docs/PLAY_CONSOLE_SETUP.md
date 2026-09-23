# Play Console setup — ordered human checklist (feeds T-031, completes P4/P10)

Written 24 Aug 2026, claims verified against live Google/RevenueCat docs that day. These are
**owner-only** actions (agents have no console access). Work top to bottom; the ordering is not
optional — see §0.

Play-side state when written: developer account registered + identity verified (P1 ✅).
RevenueCat-side state: project fully configured for both the Test Store and the Play Store app
(`docs/COSMETICS_CATALOG.md`); only the service credentials and the Play-side products are missing.

---

## 0. The two ordering constraints

1. **In-app products can only be created after a build carrying `com.android.vending.BILLING` has
   been uploaded** to some track. The RevenueCat SDK adds that permission automatically — so §D1
   unlocks only once a T-020 build is uploaded. **Don't hold the clock for it** (since 24 Aug an
   AAB already exists — `BuildScript.BuildAndroidBundle`, STATUS 24 Aug compliance entry): upload
   the *current* AAB to **Closed testing** now to start the 12×14 clock (new-account closed
   releases go through review, which can take days); when the T-020 build lands, push it to
   **Internal testing** (instant, no review) to unlock product creation the same day, and to the
   closed track as a normal update.
2. **The closed-testing track cannot publish until the store listing and App content declarations
   are complete** — including the privacy policy URL, content rating, icon, feature graphic and
   screenshots. Everything in §A can and should be done *now*, before any build exists.

The 12-testers × 14-days clock (personal accounts created after 13 Nov 2023; reduced from 20 in
Dec 2024) starts only when testers are opted in to a live closed test — every day §A slips, the
production date slips.

---

## A. Now — no build needed

1. **Payments profile (P10).** Play Console → Setup → *Payments profile* — create/link the Google
   payments merchant account. Products cannot be created without it; approval can take days.
   Form guidance (researched 26 Aug, owner = individual in Spain):
   - Public merchant name: `ferrabled` (trade name, no registration needed for a persona física;
     legal name stays in the legal section). Buyer card-statement name: `VEYRO RUN`. Website
     veyro.ferrabled.com. Support email is MERCHANT-level (appears on receipts for every future
     app): use a studio-generic one — ideally `support@ferrabled.com` via free Cloudflare Email
     Routing, else `ferrabled+play@gmail.com`; editable later. The `ferrabled+veyro` alias stays
     as the per-app listing/site/privacy contact.
   - EU VAT: Google is merchant of record — charges and remits it; no VAT invoicing/filing for
     Play sales. Spain side is separate and independent of Google's forms: income is actividad
     económica from €1 (IRPF), alta censal 036/037 + the autónomo question → gestor, can run in
     parallel (not tax advice).
2. **Create the app.** All apps → *Create app*: name **Veyro Run**, default language, type
   **Game**, **Free** (⚠️ irreversible once published — correct for us, free + IAP), accept the
   declarations. The **package name is not asked here** — it binds permanently from the first
   uploaded build, which must be `com.ferrabled.veyro.run`.
3. **Store listing.** Grow → Store presence → *Main store listing*. Copy from
   `docs/store-kit/LISTING.md` (verified under the length limits) — **give it the camera-mode
   pass first**: camera ships in v1.0 (OPEN_QUESTIONS 12), and an unexplained CAMERA permission
   invites review questions. Binary assets required before the track can publish — rough versions
   are fine, finals replace them later:
   - app icon **512×512** PNG
   - feature graphic **1024×500**
   - **≥ 2 phone screenshots** (briefs in `store-kit/SCREENSHOTS.md`, `ART_DIRECTION.md`)
4. **App content declarations** (Policy → App content) — the closed-track gate:
   - **Privacy policy URL** — ✅ resolved 25 Aug:
     the policy is live at `https://veyro.ferrabled.com/privacy/` (Cloudflare, OPEN_QUESTIONS 9
     answered), `GameLinks.cs` and the Console field both point there, contact email filled.
   - App access: *all functionality available without special access* (no login exists).
   - Ads: **No** (v1.0 has none).
   - Content rating questionnaire (IARC): category Game; honest answers should land Everyone /
     PEGI 3.
   - Target audience: **13+** — do **not** tick any under-13 age band, that opts into the Families
     policy and its extra requirements.
   - Data safety: for the first closed build (camera in v1.0, on-device-only, no RevenueCat yet):
     **no data collected**, camera frames never leave the device. From the T-020 build onward:
     purchases/purchase history **and device or other IDs** are collected — the RevenueCat SDK
     is configured at boot and refreshed on resume, so an anonymous app user ID leaves the
     device on every launch whether or not the player ever buys. Follow RevenueCat's "Google
     Play data safety" guide when updating the form; revisit again when OneSignal/Layers land
     (T-021/T-022).
   - News / government / financial / health: No.
5. **Recruit the 12 testers (H6).** Email list or Google Group in Testing → Closed testing →
   Testers tab. The recruiting pack is `docs/store-kit/CLOSED_TESTING.md`. They must be real
   devices + real Google accounts; emulators and duplicates don't count.

## B. Build

The AAB path **already exists** (24 Aug compliance session): `BuildScript.BuildAndroidBundle` →
`builds/MotionRunner.aab`, signed from the `VEYRO_KEYSTORE*` env vars; the upload keystore itself
is owner-created (runbook: PREREQUISITES **D5**), passwords owner-held. The first upload (§C) uses
this build as-is. The T-020 Unity session then adds the RevenueCat SDK — *that* build carries the
BILLING permission §D1 waits for, and ships as a track update.

## C. Upload and start the clock

1. Testing → **Closed testing** → default track → create release → upload the **current** AAB →
   accept **Play App Signing** → release notes → select countries → *Save and publish* → submit
   for review. Don't wait for the T-020 build — the clock outranks it.
2. When the T-020 (RevenueCat) build exists: upload it to **Internal testing** (instant, no
   review — this is what unlocks §D1) and push it to the closed track as a normal update.
3. Once live: share the **opt-in URL** with the testers; they opt in *and install*. The clock
   counts opted-in testers continuously — check the tester panel stays ≥ 12. Pushing updated
   builds to the track is fine and expected; it does not reset the clock.
4. ✅ *(done 29 Aug)* Setup → **License testing**: add the owner's and testers' Gmail addresses
   (license response NORMAL). Required for sandbox purchases (`REVENUECAT_PLAN.md` §6.5).

## D. Monetization + RevenueCat credentials

> **Status 29 Aug 2026: D1 ✅, D3 ✅ (credentials "valid"), D4 ✅ — verified end-to-end: license-test
> purchase through the real Play Store flowed into RevenueCat's Play Store app (earlier Test Store
> purchases confirmed the entitlement wiring separately). The T-020 AAB (versionCode 4, BILLING
> permission) is on the internal track. **D2 (15% fee tier) still pending — owner.**

1. **Create the 3 in-app products.** ✅ Done 27–28 Aug — but with ids **without the `veyro.`
   prefix** this checklist specified: the live, immutable ids are `skin.ember` (€2.99) ·
   `skin.frost` (€2.99) · `season1.pass` (€4.99), verified against the Console 15 Sep and proven
   working by the 29 Aug license purchase. `docs/COSMETICS_CATALOG.md` §2 records the correction.
   Never recreate them. (Products can take a few hours to propagate — don't debug empty offerings
   in the first hour.)
2. **Enrol in the 15% reduced service-fee tier** (`docs/LICENSING_REVENUE.md` §2).
3. **Service credentials (completes P4)** — RevenueCat's current flow:
   1. Google Cloud Console → create/select a project → enable **Google Play Android Developer
      API**, **Google Play Developer Reporting API**, **Cloud Pub/Sub API**.
   2. IAM → Service accounts → create one (e.g. `revenuecat-veyro`) → roles **Pub/Sub Editor** +
      **Monitoring Viewer** → create a **JSON key**, download it. Never commit it.
   3. Play Console → Users and permissions → *Invite new user* → the service account's email →
      app permissions for Veyro Run: view app information, view financial data, manage orders.
   4. RevenueCat dashboard → Veyro Run **(Play Store) app** settings → *Service credentials* →
      upload the JSON. **Propagation takes up to ~36 h** — upload early, expect "invalid
      credentials" style errors to self-resolve; RevenueCat can auto-configure the Pub/Sub
      real-time-notifications topic from the same page.
4. **Attach the Play products in RevenueCat** (they're already created there): each product into
   its entitlement (`skin_ember`, `skin_frost`, `season1`) and into the empty Play row of its
   package in offering `default`.
5. *(Post-release)* **Google Play promo codes are the primary judge-access route**:
   `REVENUECAT_PLAN.md` §6 — 10 for `season1.pass`, 5 each for `skin.ember` and `skin.frost`,
   plus separate test codes. Supply all three product groups and keep promotions active through
   judging. No custom judge login is planned.

## E. After the 14 days — production access

Play Console dashboard → *Apply for production access*. Google reviews the testing story (tester
engagement, feedback acted on, pre-launch report crash-free) — 12×14 is the minimum, not an
auto-approval. Answer honestly: recruited friends/Shipaton peers, what feedback changed.

**Dates, recomputed 26 Aug** (the closed test went live that day, five days ahead of the ~1 Sep plan;
D11's ~22 Sep production target predates this and is now conservative):

| | Date | Note |
|---|---|---|
| Closed track live | **26 Aug** ✅ | |
| 14 continuous days elapse | **~9 Sep** | From the day the **12th** tester is opted in, not from upload. A tester who drops and rejoins restarts *their own* 14 days. |
| Apply for production | **~9 Sep** | |
| Production live | **~16 Sep** | Review is usually ≤7 days, occasionally longer. |
| Devpost deadline | **30 Sep** | ~2 weeks of slack — the first real slack this schedule has had. |

The old "apply ~7–8 Sep" line here was arithmetically impossible (14 days from ~1 Sep is ~15 Sep) and
is gone. Spend the new slack on T-020 and the listing assets, not on delaying the application:
production access is the gate everything else queues behind.

---

## Sources (checked 24 Aug 2026)

- App testing requirements for new personal accounts — https://support.google.com/googleplay/android-developer/answer/14151465
- Set up an open, closed, or internal test — https://support.google.com/googleplay/android-developer/answer/9845334
- Create an in-app product (billing-permission prerequisite) — https://support.google.com/googleplay/android-developer/answer/1153481
- RevenueCat: creating Play service credentials — https://www.revenuecat.com/docs/service-credentials/creating-play-service-credentials
- RevenueCat: Google Play checklists — https://www.revenuecat.com/docs/service-credentials/creating-play-service-credentials/google-play-checklists
- 12-tester requirement community guide — https://support.google.com/googleplay/android-developer/community-guide/255621488
