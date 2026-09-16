---
title: Veyro Run — Privacy Policy
permalink: /privacy/
---

<!-- SOURCE OF TRUTH for the policy text. Published at https://veyro.ferrabled.com/privacy/
     (site/public/privacy/index.html — keep the two in sync; that URL is in
     GameLinks.PrivacyPolicyUrl and goes in the Play Console privacy-policy field).
     Hosting decided by owner 25 Aug 2026 (OPEN_QUESTIONS 9): Cloudflare Workers static
     assets, deployed with wrangler from site/. Contact email is a temporary alias the
     owner will replace later — grep site/ + this file for it when that happens.
     MUST be updated before shipping T-020 (RevenueCat purchases), T-021 (OneSignal push) or
     the Layers analytics integration — each of those starts real data collection that has to
     be declared here AND in the Play Console Data safety form.
     T-020 DONE 29 Aug 2026: the Purchases section below is written and the absolute
     "no network requests" claim is gone. This text and site/public/privacy/index.html are
     in sync and awaiting `npx wrangler deploy` from site/ — deploy it in the same window as
     the versionCode-5 upload and the Console flips (STORE_COMPLIANCE.md T-020).
     CORRECTED 30 Aug 2026 (PR #4 review), before any of it was deployed: the first draft
     said a player who never buys never contacts RevenueCat. That was never true of the
     build — GameBootstrap configures the SDK at boot and OnApplicationPause refreshes on
     every resume, so RevenueCat is contacted on every launch by every player, buyer or not.
     The section now describes launch, resume and store-browsing, and the Data safety form
     must therefore declare device/other IDs as well as purchase history. If the app ever
     defers SDK configuration to an explicit store action, this text has to move back.
     REVISED 15 Sep 2026 (release/revenue-cat), still before deployment, because the 29 Aug
     text described a purchases launch that never reached testers (the closed track still
     runs the pre-RevenueCat beta) and made four claims the implementation cannot back:
     (1) it dated the purchases section to a "ship" that did not happen — the policy now
     distinguishes the no-shop beta versions from versions with the shop; (2) "uninstalling
     deletes everything" / "reinstalling produces a new identifier" — Android Auto Backup
     was observed (29 Aug device test) restoring the RevenueCat anonymous ID across a
     reinstall; (3) "deleting the RevenueCat record means the purchase can no longer be
     restored" — the Play purchase is a separate Google record we cannot delete, and a later
     restore can recreate the RevenueCat data; (4) the blanket "no analytics" claim — the
     Data safety form declares Analytics as a purpose for purchase history per RevenueCat's
     own guidance, so the policy now says what we do see (aggregate purchase statistics)
     and what we do not (gameplay). Effective date moved to the actual revision date. -->


# Veyro Run — Privacy Policy

**Effective date:** 15 September 2026
**App:** Veyro Run (`com.ferrabled.veyro.run`), published by Fernando Rabasco ("ferrabled")
**Contact:** ferrabled+veyro@gmail.com

## The short version

Veyro Run plays entirely on your device — every run, every score, every camera frame. There are no user accounts, no advertising, no gameplay analytics, and no tracking of what you do in the game. Versions that include the in-game shop use the internet for one purpose only: optional cosmetic items. The game asks our purchase provider which ones your device owns when it launches, and it goes online again if you buy or restore one. Nothing else you do in the game ever leaves your phone.

**Which version do you have?** Closed-beta versions distributed before the shop update (September 2026) contain no purchases and make no network requests at all — for those versions, the Purchases section below simply does not apply. Everything else on this page applies to every version.

## Camera (optional game mode)

Veyro Run has an optional "camera" control mode that uses your device's front camera to detect your head position so you can steer by moving your body.

- The camera is used **only** if you choose camera mode, and only after you grant the Android camera permission.
- Camera images are processed **entirely on your device, in memory**, by a local machine-learning model. They are **never recorded, never saved to storage, and never transmitted anywhere**. Each frame is discarded as soon as the next one is processed.
- Declining the camera permission loses nothing: the tilt & touch mode is the full game.

## Motion sensors

Tilt mode reads your device's gyroscope/accelerometer to steer. Sensor readings are used in the moment, on the device, and are not stored or transmitted.

## Data stored on your device

Your local game data — best scores, daily streak, recent run history, your equipped cosmetic, and preferences such as your last-used control mode — is saved in the app's local storage on your device only. Uninstalling the app removes it from the device; note that Android's automatic backup service may restore app data when you reinstall, depending on your device's backup settings — that is Android behaviour, not something the game controls.

## Purchases (optional)

*This section applies to versions that include the in-game shop (the September 2026 update onward). Earlier closed-beta versions contain no shop and contact no one.*

Veyro Run is free, and the whole game is in the free download. You can optionally buy cosmetic items for your runner, which change how your runner looks and nothing else. Buying is optional; the check described in the first point below is not, because the game has to know what you already own.

- **RevenueCat is contacted when the game starts, whether or not you ever buy.** The game uses RevenueCat, a purchase-management service acting as our processor, to answer one question: which cosmetics does this device own? It asks that question when the game launches, when you return to it after switching away, and when you open the store screen — not only at the moment of a purchase. That is what makes a cosmetic you already own appear straight away instead of after a restart. On first launch RevenueCat generates an **anonymous identifier** for your installation, and each request carries basic technical details: app version, platform, and store country. If you never buy anything, that identifier simply has no purchases attached to it. Its policy is at [revenuecat.com/privacy](https://www.revenuecat.com/privacy).
- **Google Play handles the payment.** Purchases are made through your own Google account under Google Play's terms. We never see or receive your payment details — no card number, no billing address, no name. Google's handling of the transaction is covered by [Google's privacy policy](https://policies.google.com/privacy).
- **Buying is what adds the purchase itself.** When you buy or restore, RevenueCat also receives the **purchase token and purchase history** that Google Play issues for the transaction, and keeps them so your purchase can be given back to you when you reinstall or switch phones.
- **That identifier is not you.** It is generated for the installation, is not linked to your name or email, and there is no Veyro Run account to link it to — none exists. Reinstalling normally produces a new one, though if Android's backup service restores the app's data, the previous identifier can come back with it.
- **Purchase records double as our sales statistics.** RevenueCat shows us aggregate numbers derived from purchases — how many of each cosmetic have been bought, revenue totals. That is the extent of the "analytics" in this game: it comes from the purchase records themselves, not from any tracking of you or your play. No advertising, no ad identifiers, no separate analytics SDK, no profiling, and no gameplay data is sent anywhere. Buying a skin does not start any other collection.

Camera mode is unaffected by all of this: camera frames are never part of a purchase, never sent to Google Play or RevenueCat, and never transmitted anywhere at all.

## Children

Veyro Run is not directed at children under 13.

## Your rights

The game itself holds no data about you, so for the game there is nothing to request, export, correct, or delete — uninstalling removes the local scores and settings from the device (subject to Android's backup behaviour, above).

In shop-enabled versions, RevenueCat holds the anonymous identifier for your installation from the first launch onwards, and — if you have bought a cosmetic — the record of that purchase, which it keeps so the purchase can be restored. A purchase also appears in your own Google Play order history, which you see and manage in your Google account.

To have the RevenueCat record deleted, email ferrabled+veyro@gmail.com:

- **If you have bought something**, include your Google Play order number (from your Google Play order history) so we can locate the record. Deleting it removes what RevenueCat holds, but the purchase itself remains in your Google Play account — that is Google's record, not ours to delete — so restoring purchases on a later install can create a fresh RevenueCat record containing that purchase again.
- **If you have never bought anything**, the record is a random identifier that neither we nor RevenueCat can connect to you — the app does not display it, and it is not linked to your name, email, or Google account. It contains nothing about you beyond app version, platform, and store country. We will still help with any request, but we cannot single out which anonymous record is yours.

Any other privacy question or concern: email the same address, and a human will answer.

## About this website

This website (veyro.ferrabled.com) sets no cookies and runs no analytics or trackers. Two things happen at the infrastructure level when you visit: the site is served by Cloudflare, which processes visitor IP addresses to deliver pages (as any web host does), and pages load their fonts from Google Fonts, which means your browser requests the font files from Google's servers. We never see or store any of this ourselves.

## Changes

The Purchases section was added on 29 August 2026 and revised on 15 September 2026, ahead of the first distributed version that includes the in-game shop. Closed-beta versions distributed before that update contain no purchases and collect nothing. If a future version of the game starts any further data collection (for example push notifications), this policy will be updated here first, and the app's store listing will reflect it.

## Contact

Questions about this policy: ferrabled+veyro@gmail.com.
