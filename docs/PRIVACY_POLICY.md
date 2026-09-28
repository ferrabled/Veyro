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
     and what we do not (gameplay). Effective date moved to the actual revision date.
     T-009 ADDED 18 Sep 2026 (profile-integration): the "Player profile and leaderboards"
     section, recovery-code paragraph, updated short version/rights/changes — all VERSION-
     SCOPED ("the leaderboard update onward") so the page stays truthful whenever it deploys.
     Deploy WITH or BEFORE the first build that carries profiles; the Data safety flips ride
     the same release (STORE_COMPLIANCE.md T-009). The old "no accounts / nothing else leaves
     your phone" absolutes are gone from every page (18 Sep review R3).
     T-022 REVISED 21 Sep 2026 (same day, before any analytics build is distributed): the
     locally patched Layers SDK was replaced by the official unmodified package, so the
     "Turning it off" bullet no longer promises that unsent events are discarded at that
     moment — with the stock SDK they stay on the device, can never be sent while analytics
     is off, and are deleted on the next enable or when app data is cleared. The delivery-
     diagnostics sentence now also names the initialization-timing event the stock SDK always
     emits, and no page claims a dedicated/isolated SDK storage folder any more (the SDK's
     files live in the app-specific storage folder; device test 22 Sep found them under Android/data/<pkg>/files/layers_sdk). Effective date unchanged: same-day revision.
     T-024 ADDED 28 Sep 2026 (feat-share-run, PR #13 review): the "Sharing a run, and challenge
     links" section, a short-version sentence, the challenge-page paragraph under "About this
     website" and a Changes line — text from docs/SHARE_COMPLIANCE.md §8, version-scoped to
     versions with the SHARE button. Nothing new is collected: the share is a user-initiated
     hand-off to the system share sheet. Deploy WITH or BEFORE the first build with SHARE, in the
     same `wrangler deploy` as /challenge/. Effective date moved to the revision date.
     iOS ADDED 28 Sep 2026 (feat/iOS-implementation, Phase 2c; same-day revision, so the
     effective date is unchanged): short iPhone clauses beside each Android-only claim — Apple
     handles App Store payments; SHARE copies to the clipboard on iPhone (owner decision 4);
     deleting the app deletes the recovery code on iPhone, which is what makes the Android-only
     "survives uninstalling" sentence false there (owner decision 3: plaintext file kept,
     Keychain deferred — docs/PROFILE_LEADERBOARD_PLAN.md "iOS storage"); APNs delivers iPhone
     notifications; no tracking prompt on iPhone. Nothing new is collected. Deploy before the
     iOS build is submitted for App Review (App Store Connect links this page). The support
     page, the terms and the site footer are still Android-only. -->



# Veyro Run — Privacy Policy

**Effective date:** 28 September 2026
**App:** Veyro Run (`com.ferrabled.veyro.run`), published by Fernando Rabasco ("ferrabled")
**Contact:** ferrabled+veyro@gmail.com

## The short version

The game itself plays entirely on your device — every camera frame is processed and discarded in memory, and the game serves no ads. Online services support purchases, leaderboards and, in notification-enabled versions, notification registration and delivery. Versions with GAMEPLAY ANALYTICS offer a separate, optional analytics service that stays off until you enable it. Versions with the in-game shop ask our purchase provider which cosmetics you own. Versions with the shared leaderboard (the **leaderboard update** onward) additionally create an **anonymous player profile** — a random ID and a generated name like SWIFT-FOX-42, no email, no sign-in — and upload your **finished run results** so the daily and all-time leaderboards can exist. Your name and score on those boards are visible to other players; you can rename the profile at any time and delete it, with everything it holds, from inside the game. Versions with the SHARE button hand a single line of text to your phone's own share sheet (on iPhone, SHARE copies it to your clipboard instead): the game itself sends nothing, and the message carries a score and a track seed, never your name or your ID.

**Which version do you have?** Closed-beta versions before the shop update (September 2026) make no network requests at all. A SHOP tab indicates purchase services; a generated player name indicates online profiles. Versions showing a player name on the PROFILE tab have both — and the profile section below applies. Versions with PROFILE → NOTIFICATIONS also include the notification service described below. PROFILE → GAMEPLAY ANALYTICS identifies versions offering the optional Layers service described below.

## Camera (optional game mode)

Veyro Run has an optional "camera" control mode that uses your device's front camera to detect your head position so you can steer by moving your body.

- The camera is used **only** if you choose camera mode, and only after you grant the Android camera permission (on iPhone, camera access when iOS asks).
- Camera images are processed **entirely on your device, in memory**, by a local machine-learning model. They are **never recorded, never saved to storage, and never transmitted anywhere**. Each frame is discarded as soon as the next one is processed.
- Declining the camera permission loses nothing: the tilt & touch mode is the full game.

## Motion sensors

Tilt mode reads your device's gyroscope/accelerometer to steer. Sensor readings are used in the moment, on the device, and are not stored or transmitted.

## Data stored on your device

Your local game data — best scores, daily streak, recent run history, your equipped cosmetic, and preferences such as your last-used control mode — is saved in the app's local storage on your device only. Uninstalling the app removes it from the device; note that Android's automatic backup service may restore app data when you reinstall, depending on your device's backup settings — that is Android behaviour, not something the game controls. On iPhone, deleting the app removes this data too. It comes back only if you restore the whole iPhone from an iCloud or computer backup made while the app was installed, and offloading the app (in the iPhone Storage settings) keeps it.

In versions with online profiles, the device additionally stores the profile's session and a **recovery code**. The recovery code exists so that the same profile — name, progress, leaderboard entries — can come back after you reinstall: Android's backup carries it across, and on the next launch the game exchanges it for your profile. On iPhone, deleting the app deletes the code too. After a delete and reinstall the profile comes back only if you copied its code first (PROFILE → RECOVERY CODE → COPY) and use IMPORT PROFILE on the new install, or if you restore the whole iPhone from a backup that contains it. It is a random secret that unlocks only this game profile; the server keeps only a scrambled fingerprint of it, never the code itself.

The recovery code is written to the app's own storage folder, and on some Android versions and devices that folder — and therefore the code — **survives uninstalling the app**. That is what lets a profile come back on a reinstall even when device backup is off, but it also means uninstalling is not a reliable way to erase it. On iPhone the code does not survive: deleting the app deletes it from the device, although an iCloud or computer backup made earlier still contains it until that backup is replaced or deleted. To remove the code deliberately: use **PROFILE → DELETE ONLINE PROFILE** inside the game (which deletes the code along with the profile it unlocks), or clear the app's data from Android's app settings (on iPhone, delete the app). Treat the code like a password while it exists: anyone who has it can claim the profile. If automatic recovery cannot replace a profile you have already played, the older code is kept separately on this device for support, while the current profile receives its own code. Deleting the online profile or clearing app data (on iPhone, deleting the app) also removes these saved codes; removing a saved code does not delete the separate older profile.

## Player profile and leaderboards

*This section applies to versions with the shared leaderboard (the leaderboard update onward). If your PROFILE tab shows a generated player name, it applies to you.*

- **A profile is created automatically** the first time the game goes online: a random player ID and a generated display name (like SWIFT-FOX-42). There is no sign-in, and no email, phone number, or real name is asked for or stored. You can generate a new name whenever you like; you can never type one, so no player's text is ever shown to others.
- **Finished runs are uploaded automatically.** When a run ends, the game sends its results — score, distance, coins, best combo, duration, the day's track seed, which control mode you used (tilt or camera — a yes/no fact, never any camera imagery), app version and platform — to our database provider **Supabase**, acting as our processor, so the daily and all-time leaderboards and your run history can exist. The device keeps up to eight of the most recent pending runs and retries uploading them when a connection returns; older pending runs are discarded when the queue is full. Uploads remain subject to server validation and submission limits. This upload is required for the leaderboard feature to work; it is not optional within these versions, and deleting the profile is the way to stop it.
- **What other players see:** your generated name and your scores, on the public leaderboards. Nothing else — never your player ID, and never anything from the camera or sensors.
- **The profile ID is also your purchase identifier.** The same random ID is used with RevenueCat (see Purchases below) so that your cosmetics and your profile belong to one identity. It remains a pseudonymous identifier: it is not linked to your name, email, or Google or Apple account by us, but records attached to it are your records, and we treat them that way — they are deletable, not "anonymous and unaccountable".
- **Retention and deletion:** profile and runs are kept while the profile exists. **PROFILE → DELETE ONLINE PROFILE** deletes the profile, every run, and every leaderboard entry immediately and permanently, and asks RevenueCat to delete its customer record for the same ID. Local device stats stay on the device (they are yours, on your hardware) until you clear the app's data (on iPhone, until you delete the app). Without the app, see [data deletion on the support page](https://veyro.ferrabled.com/support/#delete).
- **Fair-play and security processing:** submitted runs are checked server-side against what the game can physically produce, and submission is rate-limited per profile. Implausible submissions are stored flagged (never shown on boards; entries over 30 days old are removed when the same profile submits another flagged run, and otherwise may remain until profile deletion). This processing exists to keep the leaderboard honest and the service available. For provider-deletion retries, former Player IDs may remain in a restricted cleanup queue until cleanup succeeds.

## Sharing a run, and challenge links

*This section applies to versions with a SHARE button on the run-over card (the challenge update onward).*

- **Sharing is your action, and the game sends nothing.** Tapping SHARE opens Android's own share sheet — your phone's standard list of apps. The game hands that sheet one line of text and stops there (if the sheet cannot open, the same text is copied to your clipboard instead). On iPhone, SHARE copies that same line to your clipboard, and you paste it wherever you choose. Whether the message goes anywhere, and to whom, happens inside the app you pick, in your hands. We never see the message, the recipient, or which app you chose, and the game does not read your contacts or check which messaging apps you have installed.
- **What the message contains.** Your score, the distance, whether it was a new personal best, whether it was that day's Daily Run, and a link like `https://veyro.ferrabled.com/challenge/?s=987654&v=greybox-1&w=greybox&p=4210&m=free`. Those values are the track's random seed, the content version, the world name, your score, the run mode and, for a Daily Run, the date — they describe *a run*, so your friend plays exactly the track you played. Your player name, your Player ID, your device, and anything from the camera or sensors are **not** in it, and there is no box for you to type your own text.
- **Opening someone's challenge link.** The link opens the game directly where Android has verified it for Veyro Run; otherwise it opens a page on our website showing the score to beat, with a button that opens the game on the same track with the same seed, or takes you to the Google Play listing if you do not have the game. On iPhone the link always opens that page first, and its button opens the game if it is installed. That page sets no cookies, runs no analytics, and does not know who you are — it reads the score and the seed out of the link's own address in your browser. See "About this website" below for what happens at the infrastructure level.

## Purchases (optional)

*This section applies to versions that include the in-game shop (the September 2026 update onward). Earlier closed-beta versions contain no shop and contact no one.*

Veyro Run is free, and the whole game is in the free download. You can optionally buy cosmetic items for your runner, which change how your runner looks and nothing else. Buying is optional; the check described in the first point below is not, because the game has to know what you already own.

- **RevenueCat is contacted when the game starts, whether or not you ever buy.** The game uses RevenueCat, a purchase-management service acting as our processor, to answer one question: which cosmetics does this device own? It asks that question when the game launches, when you return to it after switching away, and when you open the store screen — not only at the moment of a purchase. That is what makes a cosmetic you already own appear straight away instead of after a restart. On first launch RevenueCat generates an **anonymous identifier** for your installation, and each request carries basic technical details: app version, platform, and store country. If you never buy anything, that identifier simply has no purchases attached to it. Its policy is at [revenuecat.com/privacy](https://www.revenuecat.com/privacy).
- **Google Play handles the payment.** Purchases are made through your own Google account under Google Play's terms. We never see or receive your payment details — no card number, no billing address, no name. Google's handling of the transaction is covered by [Google's privacy policy](https://policies.google.com/privacy).
- **On iPhone, Apple handles the payment instead.** Purchases are made through your own Apple Account under Apple's terms, and the same rule holds: we never see or receive your payment details. Apple's handling of the transaction is covered by [Apple's privacy policy](https://www.apple.com/legal/privacy/).
- **Buying is what adds the purchase itself.** When you buy or restore, RevenueCat also receives the **purchase token and purchase history** that Google Play issues for the transaction (on iPhone, the App Store's transaction and receipt details), and keeps them so your purchase can be given back to you when you reinstall or switch phones.
- **That identifier is pseudonymous.** It is not linked to your name or email. In shop-only versions it is generated per installation; in versions with online profiles it is your player profile's random ID (see the profile section above), so cosmetics and profile belong to one identity. Reinstalling normally produces a new one, though Android's backup service — or, in profile versions, the recovery code — can bring the previous identity back.
- **Purchase records double as our sales statistics.** RevenueCat shows us aggregate numbers derived from purchases — how many of each cosmetic have been bought, revenue totals. These statistics come from purchase records. Profile-enabled versions additionally send finished-run results as described above; notification-enabled versions also process sessions and notification interactions as described below. We do not serve ads or collect advertising identifiers. Buying a skin does not enable additional gameplay tracking.

Camera mode is unaffected by all of this: camera frames are never part of a purchase, never sent to Google Play, Apple or RevenueCat, and never transmitted anywhere at all.

## Notifications (versions with PROFILE → NOTIFICATIONS)

The game uses **OneSignal**, with **Firebase Cloud Messaging (FCM)** for Android delivery and the **Apple Push Notification service (APNs)** on iPhone. Firebase and APNs are delivery services here; the profile database remains Supabase.

- **Registration starts when the app starts**, even before you allow notifications. OneSignal processes an installation/user identifier, push subscription identifier and device push token when available, notification permission/subscription state, app/device/OS information, language/time zone, sessions and notification interactions. Network requests expose your IP address to the service. In an app with purchases, OneSignal may also process purchase history for messaging statistics. This processing supports notifications and their delivery/engagement measurements.
- **Receiving notifications is optional.** A menu explanation lets you choose; Android (on iPhone, iOS) asks for permission when needed. PROFILE → NOTIFICATIONS → TURN OFF stops push delivery, and Android settings (on iPhone, the Settings app) also control permission. These actions do not delete the provider record or stop SDK registration/session processing while the app is used. Every run remains playable without notification permission or a network connection.
- **No camera frames, motion readings or precise location are sent to OneSignal.** Location sharing is disabled. This first notification version does not send your Supabase player ID, email, phone number or custom run-result tags to OneSignal. Its installation record is separate from your online profile.
- **Retention and deletion:** the notification record remains with OneSignal until deleted. To request deletion, copy PROFILE → NOTIFICATIONS → COPY NOTIFICATION SUPPORT ID and email [ferrabled+veyro@gmail.com](mailto:ferrabled+veyro@gmail.com). The ID locates the record; we verify control before deleting. We handle requests within 30 days, normally sooner. DELETE ONLINE PROFILE only deletes the profile-related records described above, not this separate notification record. Using the app after deletion can create a new notification record; uninstalling stops further SDK activity but is not itself a provider-deletion request.

## Gameplay analytics (optional, versions with PROFILE → GAMEPLAY ANALYTICS)

We use Layers to understand how players use the game and whether a notification leads to a played Daily Run. This is separate from notification permission and from the leaderboard service.

- **Off until you choose.** The Layers SDK does not start or contact Layers until you enable PROFILE → GAMEPLAY ANALYTICS. Your choice is remembered between launches. Declining has no effect on gameplay, purchases, leaderboards or notification delivery.
- **What is sent after enabling.** A random analytics support ID and SDK installation/device and session identifiers; app version and build type; device model, operating system, language, time zone and screen information; app visits; notification opens; and real run starts and results, including mode, control choice, track/day/version, score, coins, distance and duration. A notification may carry campaign and variant labels so we can connect that click to a later run. The SDK also reports delivery diagnostics, such as queued/delivered/dropped event counts, retry status, SDK version and consent state, and one measurement of how long the SDK took to start, to help detect delivery problems. We do not send the notification text or arbitrary notification payloads to Layers.
- **Approximate location.** Network requests expose your IP address to Layers. Layers states that it derives country and region from the address and then discards the raw IP. This is approximate location processing; the game does not request GPS/location permission.
- **What is excluded.** We disable advertising-ID and install-referrer collection, advertising consent, and automatic application crash/error reports and gameplay performance traces in this integration. SDK delivery diagnostics described above remain enabled while analytics is on. No camera frames, motion-sensor readings, recovery codes, email addresses or phone numbers are sent. Choosing camera controls sends only the control-mode label, never what the camera sees. This version uses a separate analytics ID rather than your Supabase player ID. On iPhone the game never asks for tracking permission (App Tracking Transparency).
- **Turning it off.** PROFILE → GAMEPLAY ANALYTICS → TURN ANALYTICS OFF withdraws consent and shuts the SDK down, so nothing further is sent. While enabled, up to 200 pending events may be stored in the app's own storage area on your device (an app-specific folder that other apps cannot read) for delivery when connected. Events still unsent when you turn analytics off stay on the device and can never be sent while analytics is off; they are deleted the next time you enable analytics, or when you clear the app's data (on iPhone, delete the app). Events already delivered cannot be recalled by the switch. The support ID remains on the device so you can request deletion of earlier records.
- **Retention, access and deletion.** Previously delivered analytics remains with Layers until deleted under its retention arrangements or a verified deletion request; turning off or uninstalling does not itself delete provider records. Copy PROFILE → GAMEPLAY ANALYTICS → COPY ANALYTICS SUPPORT ID, then email ferrabled+veyro@gmail.com to request access or deletion. The ID locates records; we verify control before acting. Requests are handled within 30 days, normally sooner. DELETE ONLINE PROFILE does not delete this separate analytics record. Enable analytics again after deletion and new records can be created.
- **Provider and international processing.** Layers processes analytics for us and states that its primary processing region is the United States. Its data-protection documentation describes its processing, international-transfer arrangements and deletion process. Contact us with questions about our use of the service.

Provider information: [Layers data protection](https://layers.com/docs/api/operational/data-protection).

## Children

Veyro Run is not directed at children under 13.

## Your rights

In versions with online profiles, your profile and runs are yours to manage directly: rename in the PROFILE tab, delete profile-related records with DELETE ONLINE PROFILE (immediate and permanent), or email us. The PROFILE tab shows a **Player ID** (locates your records) and a **recovery code** (proves control of the profile — only your device holds it; the server keeps a fingerprint). An ID alone locates a record but does not authorize: before acting on an emailed request we verify control, and the recovery code is the way to demonstrate it. The same code also re-imports your profile on another install (IMPORT PROFILE on the PROFILE tab).

For versions without online profiles, purchases, notifications or gameplay analytics, no data leaves the game, so there is nothing held by our providers to request, export, correct, or delete — uninstalling removes the local scores and settings from the device (subject to Android's backup behaviour, or an iPhone restore from backup, above).

In shop-enabled versions, RevenueCat holds the anonymous identifier for your installation from the first launch onwards, and — if you have bought a cosmetic — the record of that purchase, which it keeps so the purchase can be restored. A purchase also appears in your own Google Play order history, which you see and manage in your Google account. On iPhone it appears in your Apple Account's purchase history instead.

To have the RevenueCat record deleted, email ferrabled+veyro@gmail.com:

- **If you have bought something**, include your Google Play order number (from your Google Play order history; on iPhone, the order ID from Apple's receipt email) so we can locate the record. Deleting it removes what RevenueCat holds, but the purchase itself remains in your Google Play account (on iPhone, your Apple Account) — that is Google's (or Apple's) record, not ours to delete — so restoring purchases on a later install can create a fresh RevenueCat record containing that purchase again.
- **If you have never bought anything**, the record is a random identifier that neither we nor RevenueCat can connect to you — the app does not display it, and it is not linked to your name, email, or Google or Apple account. It contains nothing about you beyond app version, platform, and store country. We will still help with any request, but we cannot single out which anonymous record is yours.

Any other privacy question or concern: email the same address, and a human will answer.

## About this website

This website (veyro.ferrabled.com) sets no cookies and runs no analytics or trackers. Two things happen at the infrastructure level when you visit: the site is served by Cloudflare, which processes visitor IP addresses to deliver pages (as any web host does), and pages load their fonts from Google Fonts, which means your browser requests the font files from Google's servers. We never see or store any of this ourselves.

The challenge page (veyro.ferrabled.com/challenge/) works the same way: it reads the score and the track from the link's own address, in your browser, and stores nothing. As with every page here, the request itself passes through Cloudflare — which means the address you asked for, including the score and seed inside it, is handled by Cloudflare's infrastructure like any other URL. We add no tracking of our own and keep no logs.

## Changes

The Purchases section was added on 29 August 2026 and revised on 15 September 2026, ahead of the first distributed version that includes the in-game shop. The Player profile and leaderboards section was added on 18 September 2026 and clarified on 19 September 2026, ahead of the leaderboard update. Closed-beta versions distributed before the shop update contain no purchases and collect nothing. The Notifications section was added on 20 September 2026, ahead of the notification-enabled update. The optional Gameplay analytics section was added on 21 September 2026, ahead of the analytics-enabled update, and its description of turning analytics off was corrected the same day, before distribution. The "Sharing a run, and challenge links" section was added on 28 September 2026, ahead of the challenge update. iPhone-specific wording (App Store payments, clipboard sharing, what deleting the app does to the recovery code, APNs delivery) was added the same day, ahead of the first iPhone version; it adds no new data collection. Future collection changes will be described here before distribution and reflected in the store listing.

## Contact

Questions about this policy: ferrabled+veyro@gmail.com.
