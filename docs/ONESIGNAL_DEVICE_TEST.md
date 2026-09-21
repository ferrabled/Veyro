# OneSignal Android device verification — 20 September 2026

Task: T-021, `feat-implement-tracks`. Device: OnePlus Nord 2 / DN2103, Android 13.
Package: `com.ferrabled.veyro.run`. Public OneSignal App ID:
`1f6ba056-efe3-4bfe-a0cd-a9a26150720a`.

**Later owner confirmation:** received the test notification and tapping it opened the game.
Basic delivery/open is now owner-verified. The exact message ID, installed build and warm/cold
state were not captured, so the lifecycle-specific checks below remain open. The notification
showed OneSignal branding; replace the default icon resources and verify the next build. The
permission-denial/retry defect remains unresolved. Next plan: `REMINDER_AUTOMATION_PLAN.md`.

## Scope and preservation

Owner authorized installation and testing of the game only, with no uninstall and no changes
to other apps or phone settings. Installed with `adb install -r`; the game's original install
date remains 18 Sep 2026, 13:16:09. The player's name, best score, streak and run history were
visible after the update. No app data was cleared. Game-only process restarts were used to
diagnose permission retry and verify persistence; each was followed immediately by launch
and none is a cold-push delivery test.

The other active game agent was sent a device reservation before installation. Each test tap
checks that the game (or its expected Android permission dialog) is foreground and that the
installed APK timestamp has not changed. Screenshots are streamed directly to local `builds/`;
no screenshot files are written to the phone. No notification shade or system Settings browsing.

## Release artifact

`builds/MotionRunner-OneSignal.apk`, versionName 1.0.0 / versionCode 5, 72,496,925 bytes.
SHA-256: `5c9e8a3ab6ec21858bae79eb80f750438e6d6cf9d52f9adc7a5f9e14c9b60da9`.
Installed at 20:50:17 local device time. This local release flavour has no RevenueCat key;
purchase checks require the development/Test Store flavour or a properly signed Play build.

The subsequent development build passed and was installed at 21:12:33, preserving data:
`builds/MotionRunner-OneSignalDev.apk`, 88,380,680 bytes, versionName
`1.0.0-dev.20260920-2051.nogit`, versionCode 5. SHA-256:
`ac79659175f38d8a6f2502d242f3b4590d6cbbd0b9f8f34b826c8d07b8de2ac1`.
The `nogit` stamp reflects the isolated build directory; 183 source/assembly/package/player
configuration inputs were checked against the workspace before building. The development flag
and Test Store public SDK key were verified in the APK. No source code changed during this test.

## Results

| Check | Result | Evidence / limits |
|---|---|---|
| Compatible update preserving data | Pass | Same signing certificate; unchanged firstInstallTime; profile/streak/history retained |
| Native SDK registration | Pass | Contextual panel appeared only after a real server subscription ID; no `local-` placeholder accepted |
| OS prompt requires a tap | Pass | POST_NOTIFICATIONS initially false; Android prompt appeared after Enable |
| Deny notification permission | Pass | Returned to the game; permission remained false; UI settled |
| Gameplay after denial | Pass | Daily tilt run completed: score 153, 6 coins, 63 m; returned to menu |
| Retry permission in same session | **Fail — unresolved** | Second Enable stayed on PLEASE WAIT without another native permission activity; close remained usable; root cause not confirmed |
| Retry after game-only restart | Pass | Android prompt appeared; Allow granted POST_NOTIFICATIONS |
| Push-ready state | Pass | UI showed NOTIFICATIONS ON, requiring real subscription ID + permission + opt-in + token |
| In-game off / on | Pass | TURN OFF returned to enable state; enable restored ready state without another OS prompt |
| Offer remains dismissed after restart | Pass | Relaunch returned to menu with no repeated offer |
| Subscription/opt-in survives restart | Pass | Same subscription ID, one PUSH record, SUBSCRIBED, optedIn true and token present; final UI NOTIFICATIONS ON |
| Profile/leaderboard | Pass, smoke check | Existing profile and online score loaded; identity/delete/import controls were not changed |
| Camera staging/cancel | Pass, smoke check | Camera started, no-face staging shown, Back returned to menu and stopped camera; physical steering not tested |
| Push delivery / notification tap | Pass, owner-observed | Owner received the message and tapping opened the game; exact build/message ID and warm/cold state not captured |
| Foreground suppression | Pending | Requires actual incoming test push |
| Background / cold delivery | Partially verified | Owner confirmed visible receipt/open; exact app state was not recorded, so distinct lifecycle cases remain pending |
| RevenueCat Test Store | Pass | Restore, simulated failed purchase, simulated valid Ember purchase, unlock/equip and restart persistence; Frost selected again afterward; no real payment |
| Live Play purchase | Not tested | Requires the signed Play build; Test Store does not substitute for it |

Local evidence is under `builds/onesignal-device-*`; screenshots/logs are test artifacts, not
submission-ready public assets. No game crash was observed. The development startup logged an
optional Play AssetPackManager class lookup error; the game, store and notification registration
continued. An OEM camera statistics-provider warning also appeared without a crash.

The old shop UI displays stale "store unavailable" text on opening even though its Test Store
purchase/restore actions work. This is a separate presentation finding for the ongoing shop
redesign, not evidence that the purchase failed.

## Reproduced retry defect

1. From the contextual offer, Enable → Android Don't allow.
2. Wait for the panel to settle, close it, complete a Daily Run and return to the menu.
3. Profile → Notifications → Enable.
4. The primary button remains PLEASE WAIT; no second PermissionsActivity starts.
5. Close remains usable. Restarting only the game restores the permission request path.

This is a release gate until fixed or explicitly accepted by the owner. Do not describe the
permission flow as fully verified based solely on the successful first request or restart.
The development build is installed for native SDK diagnostics. Attempting to revoke only the
game's POST_NOTIFICATIONS permission through ADB was rejected by the phone with a
REVOKE_RUNTIME_PERMISSIONS SecurityException; the permission remained granted. No workaround,
global developer setting, app uninstall, data clear or system Settings navigation was attempted.
Further negative permission diagnosis needs the owner to control the game's notification
permission in Android Settings. Keep the same installation and subscription.

## Handoff

- Final phone state: development build above, Frost equipped, notifications enabled, notification
  settings panel open. The sandbox account now also owns Ember from the simulated test purchase.
- Exact subscription is in ignored `builds/onesignal-device-subscription.json` and was supplied
  to the owner for a targeted OneSignal Send test. No private Firebase/REST key was requested.
- No test push was observed or sent by the agent. The owner subsequently confirmed delivery and
  opening the game. Foreground suppression and separate warm/cold cases still need verification.
- Device testing ended and a release/results message was queued to the other active game
  agent. No background device monitor remains running. Re-check which APK is installed before
  the next push test because that agent may resume installation work.
- Coordinate the next device window with the other game agent. Fix/retest the permission retry
  before distributing this SDK-enabled build; campaign/tag/Daily routing and release paperwork
  remain separate T-021 requirements.
