# OneSignal and Layers — implementation and submission review

Reviewed 20 September 2026 by `feat-implement-tracks`. This is a proposed implementation
plan, not a claim that either SDK, campaign, or experiment has shipped. No dashboard or
store state was inspected. Account setup and production publication still need owner confirmation.

**Follow-up:** [SPONSOR_SDK_INTEGRATION_GUIDE.md](SPONSOR_SDK_INTEGRATION_GUIDE.md) contains the
in-depth package/source/native-binary audit and owner setup checklist. It supersedes preliminary
SDK assumptions below, especially Layers initialization/consent defaults and the six-tag design.

**Recommendation:** ship one Daily Run return loop: finish a run, opt into reminders,
receive a useful OneSignal message, return to the Daily Run, and measure the result in
Layers. Use Layers to help formulate the experiment and interpret its results as well as
collect events. Make this the first deliverable; the two-device friend-challenge feature
is a separate T-024 extension.

## What the categories require

| Category | Eligibility and submission evidence | What makes the entry stronger |
|---|---|---|
| OneSignal — Keep Them Coming Back | Live app, OneSignal integration, at least one campaign actually deployed through its dashboard/API/MCP, App ID, description of the deployed experience | Useful timing, reliable routing, relevant audience, respectful frequency, demonstrated return to play |
| Layers — The Growth Loop | SDK installed and verifiable in the submitted app; describe Layers usage, audience, hypothesis, message, channel/surface, experiment, observed response, learning and next iteration | A focused test with credible measurements and a specific change informed by its result |

OneSignal explicitly permits a single deployed message. A draft or a local notification
does not demonstrate that requirement. [Official contest rules](https://revenuecat-shipaton-2026.devpost.com/rules).
Layers emphasizes the quality of experimentation and learning; it does not require a large
audience or positive uplift. [Layers category](https://www.shipaton.com/categories/growth-loop-award).

Both remain conditional entries until evidence exists. The existing RevenueCat purchase,
published store URL and judge access requirements remain prerequisites for the overall
submission. Deadline: 30 September, 11:45 pm PDT; target submitting the package a day earlier.

## Repository findings

- T-021 and T-022 are unstarted; neither SDK appears in the package manifest/lockfile or
  application integration code. The Unity analytics engine module is not the Layers SDK.
- P5 and P6 remain unchecked. Do not infer that the accounts exist from the original handoff.
- Daily seed generation, local bests, streaks and recent-run history exist. These are enough
  to create a useful reminder without a new backend.
- Bootstrap already composes SDK adapters for commerce and profiles. Use that same pattern
  for notifications and analytics, including fake implementations for Editor/tests.
- RunSession has clear start and crash/finish boundaries. Crash updates the streak and history;
  quitting does not count as a finished run. Menu attract gameplay must never count as player activity.
- T-024 sharing/challenge routing is unbuilt. No existing share action or deep-link handler was
  found. Its events cannot honestly be reported until that feature exists.
- The 16 September status says versionCode 5 was submitted for production and reserves the
  remaining update for these sponsor integrations, with profiles deliberately separate. This
  branch already includes profiles and art. Select the intended release baseline explicitly;
  building this branch wholesale would carry additional release obligations.
- Existing acceptance criteria stop too early: T-021 ends at a drafted campaign, and T-022 at
  dashboard events. They have been corrected to include deployment and experiment evidence.

## Implementation order

### 1. Owner setup, while wrapper work proceeds

1. Confirm the current Play production state and the binary intended for the sponsor update.
2. Create/select the Veyro Run OneSignal app and configure its Google Android platform.
   Enable Firebase Cloud Messaging HTTP v1 and upload the appropriate Firebase service-account
   credential directly to OneSignal. Keep the private JSON and any sending API key out of Unity
   and source control. Supply the public OneSignal App ID to the integration.
   [Android credential setup](https://documentation.onesignal.com/docs/en/android-firebase-credentials).
3. Create/select the Layers project. In Connections, select Layers SDK and obtain its App ID.
   Confirm the Events screen can be used to inspect incoming events.
   [Layers SDK setup overview](https://layers.com/docs/sdk).
4. Confirm which real players have opted into the experiment and the available dashboard features.
   Start with a manually scheduled dashboard campaign; paid automation or ad spend is not a dependency.

### 2. Add the two adapters and prove a device build early

Proposed seam names: `INotificationService` and `IGrowthAnalytics`, with null/fake versions,
vendor adapters, and a small coordinator owned by bootstrap. Keep vendor calls outside the
runner, track generator and camera pipeline. Initialize once; failures must leave play usable.
No scene edits are needed.

- **OneSignal:** install through its documented npm scoped registry and pin an exact release.
  Release **5.3.5** is a candidate checked during this review; it is not yet validated with our
  Unity build. Use the existing Android dependency resolver, configure icons, and disable the
  unneeded native location module before dependency resolution.
  [Unity setup](https://documentation.onesignal.com/docs/en/unity-sdk-setup),
  [5.3.5 release](https://github.com/OneSignal/OneSignal-Unity-SDK/releases/tag/5.3.5).
- **Layers:** use the `com.layers.analytics` UPM package with an exact Git tag/commit. The
  official installation page shows **3.3.0**, while the tagged repository provides **3.3.2**;
  evaluate **3.3.2** and record the resolved commit and native dependencies. Avoid an unpinned
  main-branch dependency. [Installation](https://layers.com/docs/sdk/installation),
  [tagged package](https://github.com/layers/layers-sdk-unity/tree/v3.3.2).
- Build Android immediately after installation, before expanding the UI. Preserve RevenueCat
  **9.8.1**, UnityPlayerGameActivity, and the existing billing-compatible `singleTop` patch.
  Audit generated manifests and Gradle changes from both packages.

### 3. Deliver a useful OneSignal experience

After the first completed Daily Run, offer: **“Remind me when the next Daily Run is ready.”**
Include “Not now” and a reminder switch reachable from the menu. Request Android notification
permission only after the player accepts. Denial must not interrupt play or cause repeated prompts.
The app-level data choice and OS notification permission are separate states.

Update a small set of tags after a completed run and on a later online refresh: reminder
preference, last completed UTC day, streak length, preferred control mode and experiment variant.
Read the existing streak/best stores; do not maintain a second scoring system. If best score is
used, include its control mode and date so camera/standard or yesterday/today are not confused.

First campaign proposal: opted-in players who have completed a Daily Run, with a once-daily
frequency limit and an explicit send window. Copy: **“Today's Daily Run is ready. How far can
you go?”** Exclude players already recorded as finishing today's run where the dashboard can
express that filter. Tags can be stale after offline play, so avoid claiming perfect suppression.
Start with a manual dated send; automate only after the first campaign works.

Carry an allowlisted destination plus campaign/variant identifiers in notification data.
Queue a tap until bootstrap and the menu are ready, then show the Daily Run entry with the
normal control selection and camera setup. Never start physical gameplay automatically or
interrupt an active run. Handle cold start, resume and duplicate callbacks. An old notification
should lead to today's run with appropriate wording; UTC controls the actual daily seed.

OneSignal supports consent gating before initialization and click listeners for routing.
Logging out/unsubscribing is not deletion of provider data.
[SDK reference](https://documentation.onesignal.com/docs/en/mobile-sdk-reference).

### 4. Instrument a Layers experiment

Proposed first hypothesis: **a Daily Run reminder framed around the player's existing streak
will produce more completed return runs than a generic daily announcement.** Audience: opted-in
players with a completed Daily Run and a current streak. Channel: OneSignal push; destination:
the game's Daily Run menu. Use Layers to refine the hypothesis/copy and save the plan and later
interpretation in the project. The platform provides planning and results-review workflows;
check account access/credits before using optional tooling.
[Layers growth tools](https://layers.com/docs/mcp).

Use two stable cohorts if the participant count permits: A receives the generic message;
B receives a streak-focused message. Hold destination, send time and frequency constant.
If the audience is too small, run one documented pilot and report descriptive evidence and
feedback; do not claim a statistically established uplift or a comparison that was not run.
Copy variants compare messages, not the causal effect of push versus no push.

| Proposed event | Exact trigger |
|---|---|
| SDK app/session lifecycle | Use vendor lifecycle events; verify foreground-return behavior before adding a separate event |
| `daily_run_started` | An actual player begins a Daily Run, once per run |
| `run_end` | Crash produces a completed result, once per run; mark Daily/Free and actual input/fallback mode |
| `reminder_offer_shown` | Offer becomes visible, once per presentation |
| `reminder_choice` | Player accepts/declines the offer or changes preference |
| `push_permission_result` | OS permission state is observed after the request |
| `notification_opened` | Validated OneSignal click, deduplicated by message ID |
| `experiment_assigned` | Stable cohort assignment first becomes effective |

Common properties: experiment/variant, campaign when applicable, app/build version, UTC day,
content version, world, input mode and an opaque run identifier. Use the same run identifier
for start/end and, when profiles ship, server submission. Keep camera frames, landmarks, recovery
codes, purchase tokens and raw URLs out of analytics. SDK custom events and identification are
documented; batching exists, but offline/retry behavior still needs our own device verification.
[Layers event tracking](https://layers.com/docs/sdk/tracking-events).

Record campaign delivery/click totals in OneSignal and completed return runs in Layers. Carry
campaign context only for a defined window, such as that foreground session capped at 24 hours;
clear it on expiry. Do not attribute every later run to an old click.

Primary comparison: unique eligible players completing a Daily Run in the 24-hour window divided
by players assigned/sent in each cohort; record both denominator definitions explicitly. Report
delivery rate and click-to-completion separately. To call a metric D1 retention, use consecutive
UTC-day cohorts and only include players whose observation window has ended. Separate QA events
and test devices from real participant results. Capture counts, dates, limitations, a learning
and one next change, even if the measured result is zero or inconclusive.

### 5. Privacy, identity and release verification

This is required release work, not post-submission cleanup:

- The Layers Android package declares libraries for install referrer and Advertising ID;
  resolving dependencies can add `AD_ID`. Its README also warns that minification can silently
  strip JNI-accessed attribution dependencies without keep rules. Test the actual release build.
  Recommendation: analytics-only for this pilot, no advertising/third-party-sharing use; verify
  initialization and collection behavior before claiming those are disabled. A later consent
  call or removing a permission alone is not proof that no data was collected.
  [Layers Android and consent documentation](https://raw.githubusercontent.com/layers/layers-sdk-unity/v3.3.2/README.md).
- Offer a separate optional analytics choice; delay Layers initialization until the applicable
  choice is known if its pinned version cannot accept the required initial consent state.
  Verify opt-out stops collection/uploads and handles any queued events. Provider retention and
  deletion need a documented support route. This is a proposed product default, not a legal conclusion.
- Reconcile the actual SDK payloads, purposes and dashboard configuration with Google Play Data
  safety. OneSignal's current guide includes app interactions and purchase-history behavior in
  purchasing apps; the current repo's push-IDs-only note is incomplete. Do not blindly mark Layers
  diagnostics collected without checking what it sends.
  [OneSignal Data safety](https://documentation.onesignal.com/docs/en/google-play-data-safety-requirements),
  [Google's disclosure guidance](https://support.google.com/googleplay/android-developer/answer/10787469).
- Update STORE_COMPLIANCE, the privacy source and deployed policy, support copy, and Console
  declarations before testers receive the collecting build. The current “no gameplay analytics”
  wording cannot remain. Camera processing stays entirely on-device.
- Preserve the existing account-ID decision. Where T-009 is included, identify both providers
  with the authenticated Supabase UUID, and handle recovery, identity changes and provider deletion
  together. Where T-009 is excluded, use SDK anonymous identities; do not invent a competing
  account system or make the reminder depend on leaderboard deployment.

Verification checklist for implementation:

1. EditMode checks for reminder eligibility/UTC rollover, persisted choices, duplicate click
   handling, event cardinality, cohort stability and the selected release's identity lifecycle.
2. Android release build: package/permission/size diff; preserved launch activity and billing mode;
   RevenueCat purchase/restore regression; no new runtime exception or gameplay stall.
3. **Needs human device test:** install the internal-track bundle; finish a run; accept and deny
   the reminder on separate test states; deliver a OneSignal test push while backgrounded and
   after normal cold closure; tap to the menu and play; verify one click and one completed run
   in the dashboards. Also test active gameplay, UTC rollover, offline/resume, opt-out, settings
   revocation, and stale notifications. Android force-stop is a distinct OS delivery restriction,
   not the same case as normal closure.
4. If acquisition attribution is claimed, install from a real Play tracking/referrer link on a
   clean test install. A sideload can verify event delivery but not Play install attribution.
   [Layers attribution](https://layers.com/docs/sdk/attribution).
5. Verify a public production install contains the SDKs and produces real events. Save the
   versionCode, App IDs, campaign evidence and dated results. Neither an Editor test nor a
   synthetic SDK-health probe demonstrates a live growth experiment.

## Schedule and submission artifacts

These are working targets, not store-review guarantees:

| Target | Deliverable |
|---|---|
| 20 September | Account/FCM setup, release baseline confirmed, experiment brief |
| 21–22 September | Pinned adapters, consent/reminder UI, event instrumentation, device proof |
| 22–23 September | Internal-track verification, policy/Console updates, production update submission |
| As soon as update is live | Deploy campaign to opted-in users; collect observations for 48–72 hours where possible |
| 28–29 September | Freeze evidence and write category answers; submit at least a day early |

Do not replace the pending first release casually or assume the update review will finish on
time. Keep integration work moving while the owner checks Play state. If publication slips,
shorten the observation window and report it honestly; do not claim an unshipped integration.

OneSignal evidence pack: public app URL/version, App ID, deployed campaign ID and timestamp,
audience/permission explanation, phone capture of delivery and tap-to-run, delivery/click
counts and any measured return-to-play result.

Layers evidence pack: same public app/version, verified SDK/event view, saved Layers-assisted
experiment brief, audience/message/channel/variant, dated counts and denominators, learning,
and next iteration. Update the placeholders in DEVPOST_ANSWERS and only film sponsor claims
that have actually shipped. Capture optional experiment evidence separately from the sub-two-minute
main demo.

## T-024 extension after the minimum loop works

The original share/challenge idea remains valuable: share a score and reproducible run, let a
friend play it, then compare. It needs a real share surface plus routing for existing installs
and a tested store-install fallback. Preserve seed, content version, world, date and control
mode; reject unsupported payloads and avoid submitting an old shared challenge as today's ranked
Daily Run. Instrument share intent separately from a successful recipient open/completion.
Do not promise referral attribution or “your friend beat you” notifications until that end-to-end
path and its server-side trigger exist. T-024 is not a prerequisite for the proposed reminder test.
