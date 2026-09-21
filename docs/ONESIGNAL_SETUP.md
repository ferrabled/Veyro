# OneSignal Android setup and verification

Implementation: 20 September 2026, `feat-implement-tracks`. This is the initial SDK and
permission increment of T-021, not a completed hackathon campaign.

**21 Sep implementation update:** Layers App ID received and a consent-gated adapter,
campaign-to-run events and safe Daily-menu routing are implemented. The OneSignal sample
icons are replaced by a monochrome Veyro V; Unity's bounded Android permission callbacks
replace the previously hanging request path. These changes still require physical
verification on the combined release snapshot. The 20 Sep evidence below is historical.
Current release procedure and gaps: `SDK_PRIVACY_RELEASE.md`.

**Device update (20 Sep evening):** installed release and development builds over the existing
Nord 2 game without clearing data. Registration, token readiness, allow/deny, in-game off/on,
restart persistence, gameplay/camera smoke checks and RevenueCat Test Store transactions were
checked. **A same-session deny → enable retry can hang; it is not fixed.** The owner subsequently
confirmed receiving the test push and tapping it to open the game. Separate foreground/warm/cold
cases remain unverified; OneSignal branding needs replacing with Veyro notification icons.
See `ONESIGNAL_DEVICE_TEST.md` for evidence and `REMINDER_AUTOMATION_PLAN.md` for the next increment.

**Verified:** 451/451 EditMode tests and an Android ARM64 release build passed. The APK is
`builds/MotionRunner-OneSignal.apk` (69.14 MiB, versionCode 5), with native/IL2CPP OneSignal
code, your App ID and notification resources verified. No advertising-ID/location permission
was added. This is the existing keyless-shop release flavour for local checks, **not a new
Play upload**; it does not increment the already-used versionCode. Notification lifecycle cases
beyond the owner's successful receipt/open remain pending. The owner uploaded the Firebase service-account JSON
on 20 Sep; OneSignal's public Android configuration now returns `android_sender_id`.
The sender configuration and subsequent owner-observed delivery are separate verification results.
Detailed build evidence: `builds/onesignal-verification.json`.

## What is configured

- Android package: `com.ferrabled.veyro.run`; Unity 6000.5.9f1, ARM64, API 26 minimum / 36 target.
- OneSignal App ID: `1f6ba056-efe3-4bfe-a0cd-a9a26150720a` (public, safe in the app).
- OneSignal Unity **5.1.15 Stable**, selected from the official
  [release index](https://onesignal.github.io/sdk-releases/releases.json) on 20 Sep 2026.
  It resolves Android native **5.1.37**. The earlier feasibility guide reviewed **5.3.5 Current**;
  the owner's subsequently supplied [integration prompt](https://raw.githubusercontent.com/OneSignal/sdk-ai-prompts/main/docs/unity/ai-prompt.md)
  specifically requires the Stable channel. These are deliberately different.
- UPM installs Core + Android from OneSignal's npm registry. Existing EDM4U 1.2.188 and
  RevenueCat 9.8.1 remain pinned. Custom Gradle templates are enabled. No Firebase Unity SDK,
  Google Services Gradle plugin, or `google-services.json` is required.
- IL2CPP linker preservation and the vendor's Android resource library are included. The
  default small icon is now a monochrome Veyro V vector; the vendor large sample is removed.
- All native calls live behind `IPushService`; Editor/unsupported platforms use a fake that
  never claims successful registration. iOS native setup is outside this Android increment.
- A retained subscription observer plus an immediate state read handles both late and cached
  registration. Empty / `local-` IDs never count as successful registration. Registration,
  Android permission, subscription opt-in and push token are separate states.
- Development Android builds show the vendor's once-only verification dialog on the menu,
  after the first-run guide. Player builds use player-facing copy and offer notifications only
  after a completed Daily Run, back on the home menu. Permission is requested only on the
  dialog's button tap. PROFILE → NOTIFICATIONS remains available for enabling/disabling.
- Foreground notification banners are suppressed; remote in-app messages stay paused, so a
  campaign cannot cover an active run or camera staging. Background notifications can display
  after the player enables them. No Daily Run payload routing or scheduled campaign yet.
- Location sharing is disabled. Email/SMS collection, player tags and Supabase identity linking
  are not enabled. The first subscription belongs to the installation; no new database exists.
  The wrapper reserves identity/tag operations for the campaign increment. When identity is
  wired, use the existing Supabase UUID (D14) and extend provider deletion/recovery in the same
  change; `Logout` alone is not deletion.

## What the owner needs to do now

1. Create or choose a **Firebase project for Cloud Messaging only**. Keep Supabase as the
   database and authentication/backend service. You do not need Firestore or Realtime Database.
   Leave Google Analytics disabled; it is optional for this setup. Layers remains the planned
   game analytics integration.
2. In Firebase project settings → Cloud Messaging, ensure **Firebase Cloud Messaging API (V1)**
   is enabled. In Service accounts, generate the private service-account JSON key following
   [OneSignal's Android credential guide](https://documentation.onesignal.com/docs/en/android-firebase-credentials).
3. In the OneSignal app above, open Settings → Push & In-App → Google Android (FCM), upload
   that JSON **directly to OneSignal**, and save/validate. A custom service account needs
   `cloudmessaging.messages.create` and `firebase.projects.get`; the linked guide lists roles.
   Keep the private JSON outside the repository; no REST API key or JSON is needed in chat.
4. Tell the agent **FCM validation succeeded**, and confirm the connected Android test phone
   is available for an install over the existing app. Do not uninstall or clear app data to
   verify OneSignal; that creates another subscription and loses local state. If signatures
   differ, use the owner's Play testing route instead of uninstalling.
5. Publish the staged privacy/support text before distributing the SDK-enabled build and
   complete the T-021 Console flips in STORE_COMPLIANCE. Nothing in this task deploys the site,
   uploads to Play or sends a campaign. Confirm the release baseline from OPEN_QUESTIONS 19.

**Layers is independent.** Its account can wait until push delivery works; no Layers key or
Supabase credential is needed for this step.

### Current onboarding checkpoint (20 Sep)

The owner has completed the credential upload and reached OneSignal's SDK integration screen.
Choosing **Android** for the delivery platform is correct. If the next screen offers an SDK
choice, choose **Unity**. The generated `/docs/android/ai-prompt.md` describes a native
Java/Kotlin application; this game uses the Unity integration, which includes the native Android
SDK. Keep the same OneSignal app and Firebase project. Do not add a second initialization or
native SDK version alongside the Unity package.

The existing APK satisfies the relevant Android checks: INTERNET and POST_NOTIFICATIONS,
API 26/36, OneSignal PermissionsActivity in the merged manifest, native code and IL2CPP bridge.
Credentials are supplied by OneSignal at runtime, so this dashboard change needs no App ID or
code change. The next useful check is registration on the phone, then a dashboard test push to
that exact subscription. Onboarding may remain incomplete until the SDK contacts OneSignal.

The connected Nord 2 runs Android 13. Its installed development APK and our release test APK
have matching signing certificates and versionCode 5, so an install preserving app data is
compatible. The existing release test APK uses PROFILE → NOTIFICATIONS for manual opt-in;
the automatic vendor verification dialog requires a development build. The release test shop
is deliberately unavailable. Do not use that build to verify purchases.

## Device verification — preserve the existing installation

1. Build `BuildAndroidDev` for the vendor verification dialog. Install over the existing
   compatible test app; use the existing `BuildAndroid` release flavour for release checks
   (its shop is deliberately keyless). A signed Play build uses `BuildAndroidBundle` and the
   existing owner-only signing process.
2. Launch online. Finish/skip the existing how-to guide. Wait for a real subscription ID;
   the development dialog appears once. Tap **Got it**, then allow Android notifications.
   On Android 12 and earlier there may be no runtime permission sheet. No launch-time OS prompt.
3. Open PROFILE → NOTIFICATIONS. Check the status, copy the notification support ID, and use
   that exact subscription under OneSignal Audience → Subscriptions. Save it as a **test
   subscription**. A registered ID by itself does not prove FCM delivery.
4. Use the dashboard's **Send test** to that device only. Put the app in the background, verify
   the notification arrives, tap it and confirm the game resumes safely. Record screenshot,
   message ID, subscription ID, build version, device/Android version and timestamp.
5. Repeat with a normally closed app. Do not use Android **Force stop** as the closed-app
   test: Android suppresses push until the app is launched again. Check foreground suppression,
   permission denial, revocation in Android settings, TURN OFF, relaunch and enabling again.
   Gameplay, tilt/camera selection and purchases must still work. Nothing should auto-start a run.
6. Editor tests cannot prove FCM/native registration. Capture diagnostic logs with
   `adb logcat -s OneSignal:V Unity:I` for a development build; do not publish tokens or private
   profile/recovery values from logs. Release logging is warnings only.

PROFILE → NOTIFICATIONS → COPY NOTIFICATION SUPPORT ID supports manual deletion requests.
Turning notifications off stops delivery; it does not delete the OneSignal record. This first
increment does not link it to the online profile, so DELETE ONLINE PROFILE does not delete it.

## Remaining T-021 campaign increment

After device delivery works: implement the agreed tags, QA exclusion, opt-in audience, allowed
Daily Run notification payload and cold/warm routing; connect profile identity and provider
deletion if the profile-enabled baseline is selected. Then configure and deploy the actual
Daily Run campaign, verify it against the public build, and capture judge evidence + App ID.
Only that completes the OneSignal track. Layers can subsequently measure the same return loop.

Build/test evidence is recorded in STATUS.md; human device checks remain explicit until run.
