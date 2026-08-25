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
   been uploaded** to some track. The RevenueCat SDK adds that permission automatically — so the
   T-020 Unity session must produce the signed AAB *before* the Monetize section unlocks.
   Tactic: upload that AAB to **Internal testing** first (instant, no review) to unlock product
   creation the same day, and to **Closed testing** for the 12×14 clock (new-account closed
   releases go through review, which can take days).
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
2. **Create the app.** All apps → *Create app*: name **Veyro Run**, default language, type
   **Game**, **Free** (⚠️ irreversible once published — correct for us, free + IAP), accept the
   declarations. The **package name is not asked here** — it binds permanently from the first
   uploaded build, which must be `com.ferrabled.veyro.run`.
3. **Store listing.** Grow → Store presence → *Main store listing*. Copy from
   `docs/store-kit/LISTING.md` (verified under the length limits). Binary assets required before
   the track can publish — rough versions are fine, finals replace them later:
   - app icon **512×512** PNG
   - feature graphic **1024×500**
   - **≥ 2 phone screenshots** (briefs in `store-kit/SCREENSHOTS.md`, `ART_DIRECTION.md`)
4. **App content declarations** (Policy → App content) — the closed-track gate:
   - **Privacy policy URL** — OPEN_QUESTIONS 12; the GitHub Pages page is ~30 min of work. Do it
     first, everything else here waits on it.
   - App access: *all functionality available without special access* (no login exists).
   - Ads: **No** (v1.0 has none).
   - Content rating questionnaire (IARC): category Game; honest answers should land Everyone /
     PEGI 3.
   - Target audience: **13+** — do **not** tick any under-13 age band, that opts into the Families
     policy and its extra requirements.
   - Data safety: for the first closed build (RevenueCat only, no analytics): purchases/purchase
     history are collected; follow RevenueCat's "Google Play data safety" guide when filling the
     form, and revisit when OneSignal/Layers land (T-021/T-022) and when camera mode ships.
   - News / government / financial / health: No.
5. **Recruit the 12 testers (H6).** Email list or Google Group in Testing → Closed testing →
   Testers tab. The recruiting pack is `docs/store-kit/CLOSED_TESTING.md`. They must be real
   devices + real Google accounts; emulators and duplicates don't count.

## B. Build — agent work (T-020 Unity session, see the handoff in STATUS 24 Aug)

Deliverable: a **signed AAB** (Play requires AAB, not APK, for new apps) containing the RevenueCat
SDK (which brings the BILLING permission), built with the upload keystore. The keystore itself is
owner-created and its passwords owner-held; the agent writes the exact commands into STATUS.

## C. Upload and start the clock

1. Testing → **Internal testing** → create release → upload the AAB → publish. Instant; unlocks §D1.
2. Testing → **Closed testing** → default track → create release → same AAB → accept **Play App
   Signing** → release notes → select countries → *Save and publish* → submit for review.
3. Once live: share the **opt-in URL** with the testers; they opt in *and install*. The clock
   counts opted-in testers continuously — check the tester panel stays ≥ 12. Pushing updated
   builds to the track is fine and expected; it does not reset the clock.
4. Setup → **License testing**: add the owner's and testers' Gmail addresses (license response
   NORMAL). Required for sandbox purchases (`REVENUECAT_PLAN.md` §6.5).

## D. Monetization + RevenueCat credentials

1. **Create the 3 in-app products.** Monetize → Products → In-app products → *Create product* —
   ids **character-for-character** from `docs/COSMETICS_CATALOG.md` §2 (immutable):
   `veyro.skin.ember` (€2.99) · `veyro.skin.frost` (€2.99) · `veyro.season1.pass` (€4.99).
   Titles/descriptions from the catalog doc. **Activate** each. Products can take a few hours to
   propagate — don't debug empty offerings in the first hour.
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
5. *(Post-release)* Promo codes for judges: `REVENUECAT_PLAN.md` §6 — 10 for the pass, 5 per skin.

## E. After the 14 days — production access

Play Console dashboard → *Apply for production access*. Google reviews the testing story (tester
engagement, feedback acted on, pre-launch report crash-free) — 12×14 is the minimum, not an
auto-approval. Answer honestly: recruited friends/Shipaton peers, what feedback changed. Target
per D11: apply ~7–8 Sep, production release ~22 Sep.

---

## Sources (checked 24 Aug 2026)

- App testing requirements for new personal accounts — https://support.google.com/googleplay/android-developer/answer/14151465
- Set up an open, closed, or internal test — https://support.google.com/googleplay/android-developer/answer/9845334
- Create an in-app product (billing-permission prerequisite) — https://support.google.com/googleplay/android-developer/answer/1153481
- RevenueCat: creating Play service credentials — https://www.revenuecat.com/docs/service-credentials/creating-play-service-credentials
- RevenueCat: Google Play checklists — https://www.revenuecat.com/docs/service-credentials/creating-play-service-credentials/google-play-checklists
- 12-tester requirement community guide — https://support.google.com/googleplay/android-developer/community-guide/255621488
