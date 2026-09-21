# Daily Run reminders: OneSignal delivery and Layers measurement

20 September 2026 — proposed next increment for T-021/T-022, feat-implement-tracks.
The server sender and schedule remain a plan. **21 Sep update:** the Layers SDK and
consent/campaign/run instrumentation are implemented using `app_4cf32e54fc359326`;
dashboard receipt and physical verification remain pending. Current implementation,
privacy controls and release gates are in `SDK_PRIVACY_RELEASE.md`. No sender is deployed.

## Current checkpoint

The owner confirmed receiving a push and tapping it successfully opened the game. Record this
as owner-observed delivery and app opening; the message ID, precise installed build and warm/cold
state were not captured. This does not close every notification lifecycle test. The owner saw
OneSignal branding in the notification: replace the bundled default icon resources with Veyro
art and verify on Android. The previously reproduced denial/retry hang also remains unresolved.

## The loop

1. A player enables reminders. Optional gameplay analytics remains a separate choice.
2. The game updates reminder eligibility and the last completed Daily Run day.
3. A controlled send, followed later by a scheduled Supabase function, asks OneSignal to send
   the reminder to eligible opted-in players.
4. OneSignal delivers the push and supplies our campaign context when the player taps it.
5. The game presents the Daily Run entry safely. It never starts motion/camera gameplay from
   a notification tap and never interrupts an active run.
6. With analytics permission, the Layers Unity adapter records notification open, actual Daily
   start and completed run, carrying the same experiment/campaign/variant context.
7. Compare message delivery/click counts in OneSignal with completed return runs in Layers;
   use Layers to record the hypothesis, review the results and choose the next message.

The app carries the connection between providers. No direct OneSignal-to-Layers connector or
exchange of push tokens is required. Our custom campaign properties describe returning-player
engagement; SDK installation-attribution features do not automatically attribute this loop.

## Implementation order

### 1. Finish the notification experience

Fix/retest denial followed by retry, replace small and large default icons, and test warm/cold
opening plus foreground suppression. Android small icons need a transparent monochrome
silhouette; a full-colour large Veyro icon is separate. Icons are compiled into the next build.
Reference: [OneSignal icon setup](https://documentation.onesignal.com/docs/en/notification-icons).

Add the six existing proposed tags (qa, daily_reminders, analytics_opt_in, last_daily_day,
streak_length, experiment_variant), explicit QA exclusion, and versioned notification data:
destination=daily, campaign_id, experiment_id, variant and UTC run day. Validate destinations,
deduplicate clicks and expire click context at the next visit or after 24 hours, whichever comes
first. Continue using the source-audited thin wrappers and fake adapters.

### 2. Install Layers and prove the measured loop

Obtain the public Layers SDK App ID from Connections → Layers SDK → Get your App ID. Integrate
the reviewed Unity package and its consent/collection controls as specified in
SPONSOR_SDK_INTEGRATION_GUIDE.md. Initialization must wait for optional analytics acceptance;
verify the advertising-ID suppression, automatic event configuration and opt-out queue handling
before shipping. Do not assume defaults implement our chosen collection policy.

Proposed custom events: experiment_assigned, notification_opened, daily_run_started and
daily_run_completed. Properties include campaign/experiment/variant when applicable, unique run
ID, UTC day, build, world/content version, input mode and QA state. Menu attract runs emit none
of these gameplay events. Verify real phone events in the project's Events screen.

Where the chosen release includes profiles, use its authenticated Supabase UUID for provider
identity, consistent with the existing RevenueCat decision. Handle recovery/deletion and verify
identity-binding protection before enabling alias-targeted sending. A sponsor-only anonymous
release can first use tags and campaign-level measurements; Q19 still governs the release baseline.

References: [Layers setup](https://layers.com/docs/sdk),
[custom events and identity](https://layers.com/docs/sdk/tracking-events).

### 3. Add a protected programmatic sender, then scheduling

Use a Supabase Edge Function to call OneSignal's Create Message API. This uses the existing
backend; Firebase continues to provide Android push transport and Supabase stays the database.

- Store ONESIGNAL_APP_API_KEY in Supabase Edge Function secrets. The public App ID may be
  configuration. No sending key belongs in Unity, a public repository, a log or chat.
- Protect the sender as an internal operation. The game cannot choose arbitrary recipients,
  notification text or campaigns. Validate any player preference updates against authenticated
  identity; never trust a supplied player UUID as ownership proof.
- Derive recipients from reminder opt-in, recent Daily activity and QA/cohort state. For the
  measured pilot, include players who separately accepted analytics. Skip today's known
  completions and re-check eligibility before sending. Offline runs may not yet be synced, so
  avoid copy that claims certainty about a missed run or lost streak.
- Start with a dry run returning counts and intended campaign, then a one-device allowlist test.
  Use a send ledger and a stable retry/deduplication strategy so concurrent or retried jobs cannot
  duplicate the same player's reminder for the same campaign/day. Proposed cap: one Daily reminder
  per player per UTC game day. Send time and time-zone handling need an explicit campaign setting.
- Record the provider message ID and send result separately from actual delivery/open/completion.
  Bound retries, make opt-out/deletion take effect, and include an immediate schedule kill switch.
- Once the complete loop passes, invoke the same function from Supabase Cron. Keep invocation
  credentials private and the schedule disabled until the audience, copy and timing are reviewed.

References: [OneSignal API](https://documentation.onesignal.com/reference/create-message),
[Supabase function secrets](https://supabase.com/docs/guides/functions/secrets),
[Supabase scheduling](https://supabase.com/docs/guides/functions/schedule-functions).

### 4. Run and document the first experiment

Use the existing proposal: A is a generic Daily Run reminder; B is a streak-oriented invitation.
Hold destination and send time constant, persist assignments, and exclude QA traffic. A small
audience may justify a simple pilot with qualitative learning rather than a numerical A/B claim.
Primary measure: unique eligible players completing a Daily Run during a complete 24-hour
observation window, with the assigned cohort denominator recorded. Include organic returns in
that cohort; separately report delivery, clicks and clicked-session completions. Comparing two
messages does not establish push-versus-no-push uplift or automatically measure D1 retention.

Save the brief and interpretation in Layers, provider message IDs/timestamps, the tested public
build/version, dated counts and the next change informed by the result. Update privacy/store
declarations for the actual SDK collection before release. A successful personal test send is
integration evidence; it is not the completed production growth experiment.

## Owner handoff

1. Create/select the Veyro Run Layers project; provide its public SDK App ID and project URL.
   Keep ad forwarding off for this first experiment.
2. When the sender is ready, save the OneSignal App API key directly as the Supabase secret
   ONESIGNAL_APP_API_KEY and confirm it is configured. No private-key sharing is needed.
3. Confirm Q19's release baseline before provider identity/release integration, and select the
   pilot audience, message copy and send time before enabling any recurring sends.
4. Arrange a phone slot after the cosmetic agent releases it. Preserve the current installation;
   the owner controls Android permission changes for negative-path testing because ADB revoke
   is blocked on this phone.

Next engineering milestone: one branded reminder opens the Daily entry and produces exactly one
notification-open event plus the real run events in Layers. Automate only after this is verified.
