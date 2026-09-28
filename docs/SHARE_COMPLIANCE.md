# Share & challenge-link compliance (T-024) — Google Play

Research date: **21 September 2026**. Scope: Android. **iOS / App Store: see §12, the addendum
added 28 September 2026.** Companion to `docs/STORE_COMPLIANCE.md` — the flip table there stays
the register of record; this file is the evidence behind the T-024 row.

Feature under review, as implemented on `feat/share-run`:

| Piece | Where | What it does |
|---|---|---|
| SHARE button | `game/Assets/Scripts/Core/RunShare.cs` → `ShareSheet.cs` | `Intent.ACTION_SEND`, `type=text/plain`, wrapped in `Intent.createChooser`. Text only, no attachment. Falls back to the clipboard when the intent fails |
| Share text | `game/Assets/Scripts/Track/ChallengeMessage.cs` | Fully app-generated. `"🏆 new personal best! I scored 4 210 in Veyro Run (1 820 m) — can you beat me? https://veyro.ferrabled.com/challenge/?s=1234&v=1&w=greybox&p=4210&m=free"` |
| Link payload | same | `s` seed, `v` content version, `w` worldId, `p` score, `m` mode, `d` daily label. **No player name, no Player ID, no device identifier, no free text** |
| Receive side | `game/Assets/Scripts/Core/DeepLinks.cs` + `game/Assets/Editor/AndroidChallengeLinks.cs` | `veyro://challenge?…` custom scheme + `https://veyro.ferrabled.com/challenge/` App Link with `autoVerify` |
| Landing page | `site/public/challenge/index.html` + `challenge.js` (undeployed) | Static, Cloudflare Workers. Shows the score; on Android opens `intent://challenge?…#Intent;scheme=veyro;package=…;S.browser_fallback_url=<Play listing>;end` (§3.5), elsewhere tries `veyro://challenge?…` and falls back to the Play listing on a timer. Carries its own no-cookies/no-analytics line (§8.5). No analytics |

---

## 0. Verdict at a glance

| # | Requirement | Source | What we must do | Status |
|---|---|---|---|---|
| 1 | Data safety: text share is **not** "collected" and **not** "shared" | Play Console Help 10787469 / Play Help 11416267 | Nothing. Form unchanged | ✅ no action |
| 2 | Seed + score in the URL are **not** personal or sensitive user data | Play User Data policy 10144311 | Keep handle/Player ID out of the link (it already is) | ✅ enforced in code |
| 3 | `ACTION_SEND` needs **no permission** | developer.android.com/training/sharing/send | Nothing | ✅ no action |
| 4 | App Link intent-filter: VIEW + BROWSABLE + DEFAULT + http/https + `autoVerify` | developer.android.com/training/app-links/verify-android-applinks | Add to the GameActivity entry at build time | ✅ built (`AndroidChallengeLinks.cs`; filters seen in the device build's manifest, STATUS 21 Sep) |
| 5 | `android:exported="true"` on an activity with intent filters (API 31+) | developer.android.com/guide/topics/manifest/activity-element | Already true for the launcher activity; keep it | ✅ no Play declaration exists for this |
| 6 | `assetlinks.json` must carry the **Play-managed app signing** SHA-256 | developer.android.com/training/app-links/configure-assetlinks | Owner copies the fingerprint from Play Console | ⬜ **owner action** |
| 7 | IARC "Online interaction or content exchange" answer stays **No** | Play Console Help 7021383 | Nothing — sharing via secondary apps is explicitly excluded | ✅ no re-questionnaire |
| 8 | Families policy does not apply (13+ target audience) | Play Console Help 9285070 / 9893335 | Nothing; keep listing art non-childlike | ✅ no action |
| 9 | Listing must describe shipped functionality only | Play Deceptive Behavior / Misleading Claims 9888077 | Do not advertise "challenge your friends" until the receive side actually works on a released build | ⬜ **owner action** |
| 10 | Privacy policy must describe the share + the challenge page | Play User Data policy 10144311 | New section below + effective-date bump + redeploy | ✅ text applied to both copies 28 Sep · ⬜ **owner: redeploy** |

**Net effect on the Console: nothing changes.** No new Data safety category, no content-rating
re-submission, no new permission, no new app-access answer. The only mandatory owner work is the
`assetlinks.json` fingerprint, the privacy-policy redeploy, and listing discipline.

---

## 1. Data safety form

### 1.1 Is a text share "collected"?

No. Play defines collection as transmission **by the app**:

> "**Collect**" means transmitting data from your app off a user's device.
> — [Provide information for Google Play's Data safety section](https://support.google.com/googleplay/android-developer/answer/10787469?hl=en)

`ShareSheet.Send` performs no network I/O. It builds an `Intent` and calls `startActivity`. The
string crosses a process boundary **on the device**, into an app the user picked; any transmission
after that is performed by WhatsApp/Gmail/Signal/etc., not by Veyro Run. The Play help page for
users lists the matching exemption:

> Data collection exceptions include when the app "accesses data only on-device without
> transmission".
> — [Understand app privacy & security practices with Google Play's Data safety section](https://support.google.com/googleplay/answer/11416267?hl=en&co=GENIE.Platform%3DDesktop)

### 1.2 Is it "shared"?

No, on two independent grounds. First the definition does not bite (nothing is transferred by us to
a third party). Second, even if you read the share sheet as a transfer, Google's exemption is
written for exactly this case:

> "Transferring user data to a third party based on a specific user-initiated action, where the
> user reasonably expects the data to be shared."
> — [Provide information for Google Play's Data safety section](https://support.google.com/googleplay/android-developer/answer/10787469?hl=en) (exceptions to declaring data as *shared*)

The user-facing wording of the same rule:

> "the data is transferred to a third party based on a specific action that you initiate, where you
> reasonably expect the data to be shared, for example, when you send an email to or share a
> document with another person."
> — [Play Help 11416267](https://support.google.com/googleplay/answer/11416267?hl=en&co=GENIE.Platform%3DDesktop)

And the parallel carve-out in the binding policy, which also settles the "is this a sale?" question:

> "User-initiated transfer of personal and sensitive user data (for example, when the user is using
> a feature of the app to transfer a file to a third party, or when the user chooses to use a
> dedicated purpose research study app), is not regarded as sale."
> — [User Data policy](https://support.google.com/googleplay/android-developer/answer/10144311?hl=en)

The Android share sheet is the textbook "specific user-initiated action": the player taps SHARE,
then picks a target app from a system-drawn chooser. Nothing is pre-selected and no target list is
compiled by us — `createChooser` is used precisely so no default silently swallows the pick.

### 1.3 Is the seed/score personal data?

No. Play's definition is an enumeration of person-linked categories:

> "Personal and sensitive user data includes, but isn't limited to, personally identifiable
> information, financial and payment information, authentication information, phonebook, contacts,
> device location, SMS and call-related data, health data, Health Connect data, inventory of other
> apps on the device, microphone, camera, and other sensitive device or usage data."
> — [User Data policy](https://support.google.com/googleplay/android-developer/answer/10144311?hl=en)

A track seed, a content version, a world id, a score and a run-mode flag describe **a run**, not a
person. They are generated by the game, carry no account, device or advertising identifier, and are
already public-by-design (the daily seed is the same for every player).

**Load-bearing constraint, same shape as the T-009 "no free text" rule:** the challenge URL must
never gain the player's generated handle, the Supabase Player ID, the RevenueCat identifier, or the
recovery code. The handle is a pseudonymous account identifier that Google's *User IDs* definition
covers (see `STORE_COMPLIANCE.md` T-009); putting it in a link would not by itself force a "shared"
declaration — the user-initiated exemption still applies — but it would put a profile identifier
into arbitrary chat histories for no gameplay benefit, and it would make the screenshot variant
(§9.1) materially harder to reason about. `ChallengeMessage.BuildUrl` writes six fixed keys and
nothing else; keep it that way.

### 1.4 Does the challenge page change the form?

No. The Data safety form describes **the app**, not the developer's website — and the page is
reached from the user's browser, not from inside the binary. The app never fetches
`veyro.ferrabled.com/challenge/`; it only *receives* an intent carrying the URL.

Two tripwires that would change this:

- If the **app** ever calls the page or a counter endpoint (e.g. "report that this challenge was
  accepted"), that is app-initiated transmission off the device ⇒ collection ⇒ a Data safety row.
- If the page ever gets analytics, see §9.2 — the *form* still doesn't change, but the privacy
  policy's "runs no analytics or trackers" sentence does.

---

## 2. Permissions and manifest

| Item | Verdict | Source |
|---|---|---|
| `ACTION_SEND` (text) | **No permission required.** Sending is an intent, not an API call | [Sending simple data to other apps](https://developer.android.com/training/sharing/send) — permissions are only discussed for the *receiving* app's access to a shared URI |
| `INTERNET` | Already declared (RevenueCat/Supabase). Share and deep links add nothing | — |
| `CAMERA` | Untouched | — |
| New permissions | **None.** Rebuild + `aapt2 dump permissions` diff anyway (CLAUDE.md gotcha #10 / STORE_COMPLIANCE standing rule 3) | — |
| `android:exported="true"` | Required by the platform, not by Play. "If an activity in your app includes intent filters, set this element to `"true"` to let other apps start it." Veyro's launcher activity already has `LAUNCHER`, so it is already exported; the new intent-filter changes nothing about the declaration | [`<activity>` manifest element](https://developer.android.com/guide/topics/manifest/activity-element) |
| Play declaration triggered by the intent-filter | **None.** There is no Console field for exported components or deep links | — |

### 2.1 Play policy on deep links

There is no dedicated "deep links" policy. The relevant surfaces are:

- **Deceptive Behavior / Misleading Claims** — "We don't allow apps that contain false or misleading
  information or claims, including in the description, title, icon, and screenshots", and apps must
  not "misrepresent or … not accurately and clearly describe their functionality"
  ([Deceptive Behavior](https://support.google.com/googleplay/android-developer/answer/9888077?hl=en)).
  Our link handler opens *our own* domain to start *our own* game — no interception of anyone
  else's traffic, no redirection of the user somewhere they didn't ask to go.
- **Link hijacking** is policed structurally rather than by policy text: Android App Links can only
  be verified for a domain whose `assetlinks.json` names our package and signing key, so we cannot
  claim someone else's host. Registering `veyro://` is uncontested and app-specific.
- Keep the intent-filter **narrow**: host `veyro.ferrabled.com`, path prefix `/challenge`. A
  wildcard host or a bare `https` filter is the shape that attracts "intent redirection" review
  attention and would also hijack our own privacy/terms/support pages away from the browser.
- `ChallengeMessage.TryParse` already refuses any URL whose path does not contain `challenge`, so a
  future `veyro://` link for some other feature cannot accidentally start a run.

### 2.2 Unity / GameActivity notes

- Unity 6 runs `com.unity3d.player.UnityPlayerGameActivity` (CLAUDE.md gotcha #5). A VIEW
  intent-filter on a GameActivity is ordinary Android and raises no Play issue — GameActivity is
  just an `Activity` subclass.
- Unity's own documentation prescribes the same mechanism:
  [Deep linking on Android](https://docs.unity3d.com/6000.1/Documentation/Manual/deep-linking-android.html)
  — enable **Custom Main Manifest**, add the `<intent-filter>` with `VIEW` + `DEFAULT` +
  `BROWSABLE` + `<data>`, then read
  [`Application.absoluteURL`](https://docs.unity3d.com/ScriptReference/Application-absoluteURL.html)
  on cold start and subscribe to
  [`Application.deepLinkActivated`](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Application-deepLinkActivated.html)
  while running. `DeepLinks.cs` already covers both cases.
- **Do not hand-commit `Assets/Plugins/Android/AndroidManifest.xml`** as Unity's doc suggests —
  CLAUDE.md gotcha #11 records a stray file of exactly that name silently changing the launcher
  activity and adding VIBRATE. Patch the merged manifest at build time, the way
  `AndroidLaunchModeFix.cs` already does. (That fix is also load-bearing here: `singleTop` is what
  makes a warm-start link raise `deepLinkActivated` instead of relaunching the process.)

---

## 3. App Links: `assetlinks.json` and verification

### 3.1 Intent filter (required shape)

> ```xml
> <intent-filter android:autoVerify="true">
>     <action android:name="android.intent.action.VIEW" />
>     <category android:name="android.intent.category.BROWSABLE" />
>     <category android:name="android.intent.category.DEFAULT" />
>     <data android:scheme="http" />
>     <data android:scheme="https" />
>     <data android:host="example.com" />
> </intent-filter>
> ```
> — [Verify Android App Links](https://developer.android.com/training/app-links/verify-android-applinks)

Ours: `android:host="veyro.ferrabled.com"`, `android:pathPrefix="/challenge"`, `https` only
(the site is HTTPS-only). The `veyro://` scheme goes in a **separate** intent-filter without
`autoVerify` — mixing a custom scheme into the auto-verified filter is a classic cause of
verification failure.

### 3.2 `assetlinks.json`

Exact shape, from the official docs:

```json
[{
  "relation": ["delegate_permission/common.handle_all_urls"],
  "target": {
    "namespace": "android_app",
    "package_name": "com.ferrabled.veyro.run",
    "sha256_cert_fingerprints":
    ["14:6D:E9:83:C5:73:06:50:D8:EE:B9:95:2F:34:FC:64:16:A0:83:42:E6:1D:BE:A8:8A:04:96:B2:3F:CF:44:E5"]
  }
}]
```
— [Configure website associations](https://developer.android.com/training/app-links/configure-assetlinks)

Hosting requirements from the same page — all four are hard failures if missed:

| Requirement | Detail | Our situation |
|---|---|---|
| Path | `https://veyro.ferrabled.com/.well-known/assetlinks.json` | new file `site/public/.well-known/assetlinks.json` |
| Content type | served as `application/json` | add a rule in `site/public/_headers` — Workers static assets will otherwise guess from the extension, and `.json` normally maps correctly, but verify with `curl -I` |
| HTTPS | must be reachable over HTTPS | ✅ |
| No redirects | "must be accessible without any redirects (no 301 or 302 redirects)" | watch the Workers asset router; a trailing-slash or 404-fallback rule that rewrites unknown paths would break verification |

### 3.3 Which SHA-256 — **the Play app signing key, not the upload key**

> "If you're using Play App Signing for your app, then the certificate fingerprint produced by
> running `keytool` locally will usually not match the one on users' devices. … You can verify
> whether you're using Play App Signing for your app in your Play Console developer account under
> `Release > Setup > App signing`; if you do, then you'll also find the correct Digital Asset Links
> JSON snippet for your app on the same page."
> — [Configure website associations](https://developer.android.com/training/app-links/configure-assetlinks)

Play Console's own page says the same and gives the current navigation (the UI moved in 2026):

> Navigate to **Protected with Play → Play Store protection → Manage Play app signing**, where the
> SHA-256 certificate fingerprint appears under **App signing key certificate**.
> — [Use Play App Signing](https://support.google.com/googleplay/android-developer/answer/9842756?hl=en);
> methods for reading a fingerprint out of an APK/keystore directly are in
> [Obtaining your app's SHA-256 certificate fingerprint](https://support.google.com/googleplay/android-developer/answer/16641489?hl=en)
> (`apksigner verify --print-certs <apk>`).

**Practical note for our build loop:** `sha256_cert_fingerprints` is an array. Put **three**
fingerprints in it during development —

1. the Play **app signing** key (what real users run; without this, verification fails on Play installs);
2. the **upload** key (so an AAB-derived local install verifies);
3. the debug keystore used by `BuildAndroidDev` (so the sideloaded dev APK verifies).

Drop (3) before v1.0 if you want the file tidy; leaving it is harmless but it is a public statement
that a debug-signed build may act for the domain.

### 3.4 What happens when verification fails (Android 12+)

> "Starting in Android 12 (API level 31), a generic web intent resolves to an activity in your app
> only if your app is approved for the specific domain contained in that web intent. If your app
> isn't approved for the domain, the web intent resolves to the user's default browser app instead."
> — [Behavior changes: all apps (Android 12)](https://developer.android.com/about/versions/12/behavior-changes-all)

So the failure mode is **silent and benign**: the `https://veyro.ferrabled.com/challenge/?…` link
opens the web page in Chrome instead of the game. No crash, no disambiguation dialog, no user-facing
error. Which is exactly why the landing page must be able to carry the whole experience on its own.

The user can still grant the association manually (Settings → Apps → Veyro Run → Open by default →
Add link), and `adb shell pm get-app-links com.ferrabled.veyro.run` reports the verification state
for debugging (`verified` / `legacy_failure` / `none`), per
[Verify Android App Links](https://developer.android.com/training/app-links/verify-android-applinks).
Allow ~20 s after install for the asynchronous check.

### 3.5 Custom-scheme fallback

`veyro://challenge?…` requires **no** domain verification and works on every Android version. It is
subject to the disambiguation dialog only if another app registers the same scheme, which for a
made-up scheme is effectively never. Its two limits:

- It is inert for anyone who does not have the game installed — a dead string in a chat app. That is
  why `GameLinks.ChallengeBaseUrl` is the https page and the scheme is only used *by the page*.
- Navigating to `veyro://…` from a web page is a browser-mediated action; on Chrome for Android an
  `intent://challenge/?…#Intent;scheme=veyro;package=com.ferrabled.veyro.run;S.browser_fallback_url=<play url>;end`
  URL is the reliable form, because it carries the Play-listing fallback in the same navigation. The
  plain `veyro://` href fails visibly (error page) when the app is absent. `challenge.js` therefore
  uses the intent form whenever the user agent says Android, and keeps the bare scheme + timer only
  for everything else.

This is the layered design already implied by `GameLinks.cs`: verified App Link → custom scheme via
the page → Play listing.

---

## 4. Content: no UGC obligations

The share text is built entirely by `ChallengeMessage.BuildText` from numeric run state. There is no
text field anywhere in the flow, and the six URL keys are written by the game. Consequences:

- Nothing the player authors ever leaves the app, so there is no content for us to host, moderate,
  report on, or take down. Play's UGC obligations (in-app reporting, moderation, blocking) are not
  engaged.
- The store-listing / Console answer for user-generated content stays **No**, consistent with the
  T-009 rule already recorded in `STORE_COMPLIANCE.md` ("handles are server-generated (no free
  text), so there is no UGC to declare. This is load-bearing").

What changes if we ever add a typed message or name: §9.3.

---

## 5. Content rating / IARC — the "do users interact?" question

**This is the question most likely to be answered wrongly, and Google has published the answer.**

The IARC questionnaire item is *Online Interaction or Content Exchange*. Play Console Help explains
exactly how to answer it:

> "Submitters should answer 'Yes' to this question if users can freely exchange content they have
> created. This includes the ability to communicate between users, comment on provided content,
> share photos, or exchange any other type of content created by users."
>
> Developers should **"only consider their app's native services and not consider sharing that is
> accomplished by using secondary apps (such as Facebook or Twitter)."**
>
> "Multiplayer functionality by itself (with no means for communication or sharing) would not
> require indicating 'Yes' to this question."
> — [Online Interaction or Content Exchange](https://support.google.com/googleplay/android-developer/answer/7021383?hl=en)

Applied to us:

| Test | Veyro Run with SHARE | Answer |
|---|---|---|
| Users exchange content **they created**? | The message is app-generated; the player writes nothing | No |
| Exchange happens in our **native services**? | No — it happens in WhatsApp/Gmail/whatever the chooser opens, i.e. a secondary app, explicitly excluded | No |
| Communication between users inside the app? | None. No chat, no comments, no inbox | No |
| Leaderboard/handle visibility (already shipped in T-009) | Server-generated handles + scores; no free text | No (already assessed) |

**Verdict: the answer stays "No". The share sheet does not flip the IARC interaction question and
the rating label does not change.** The Android share sheet is the canonical "secondary app" case
the carve-out was written for.

Because no answer changes, no re-submission is triggered:

> "All app updates where there has been a change to your content or features that would affect the
> responses to the questionnaire."
> — [Content rating requirements](https://support.google.com/googleplay/android-developer/answer/9859655?hl=en) (when you must retake the questionnaire)

`STORE_COMPLIANCE.md` standing rule 6 says the same thing in the negative — redo it *when an answer
changes*. Record the assessment; do not re-open the questionnaire "to be safe", because a
carelessly re-answered questionnaire is how a PEGI 3 game becomes PEGI 12 with a "Users Interact"
interactive-element badge it did not need.

One adjacent note, unchanged by this feature: Play surfaces interactive-element labels for apps
where "an app shares a user's location, or … allows users to interact with each other"
([Apps & Games content ratings](https://support.google.com/googleplay/answer/6209544?hl=en)).
Neither applies.

---

## 6. Families policy / target audience

Veyro Run declares **13-15, 16-17, 18+** and is not in Designed for Families. Google's target
audience page:

> "Your app is not designed for children. You must still meet the requirements outlined in Google
> Play Developer Program policies and Developer Distribution Agreement."
>
> "If your app is not primarily designed for children under 13 but your listing contains marketing
> elements that suggest otherwise (such as youthful animation or young characters in the graphic
> assets), Google Play may reject your app."
> — [Target audience and content](https://support.google.com/googleplay/android-developer/answer/9285070?hl=en)

The Families "social feature" requirements (safety reminders before children exchange media,
adult-manageable controls, adult verification before exchanging personal information) are defined as
applying to apps that include children in the target audience:

> "A social feature is any additional app functionality that enables users to share freeform content
> or communicate with large groups of people."
> — [Google Play Families Policies](https://support.google.com/googleplay/android-developer/answer/9893335?hl=en)

Two reasons they do not bite: (a) we exclude under-13 brackets, so the policy does not apply at all;
(b) even on its own terms the share sheet is not a social feature — there is no freeform content and
no broadcast to a group, only a fixed line handed to one app the user chose.

**Action: none.** Keep the under-13 brackets unticked and the listing art non-childlike, exactly as
`STORE_COMPLIANCE.md` already requires. If the target audience is ever widened to under-13, revisit
this section *before* the change — the Families social-feature requirements would then need a real
analysis, and "share to any installed app" is hard to reconcile with adult-managed exchange.

---

## 7. Store listing

**Can the listing say "challenge your friends" before assetlinks is verified?**

Strictly: only if the sentence is true of the build that is live.

> "We don't allow apps that contain false or misleading information or claims, including in the
> description, title, icon, and screenshots." … "Apps that misrepresent or do not accurately and
> clearly describe their functionality."
> — [Deceptive Behavior](https://support.google.com/googleplay/android-developer/answer/9888077?hl=en)

`STORE_COMPLIANCE.md` standing rule 5 already says "Listing describes shipped features only — never
roadmap", and the same discipline is what held the cosmetics paragraph back until T-020 shipped.

The nuance that makes this answerable rather than paralysing: **the send side and the receive side
are separable claims.**

| Claim | True when | Safe to publish |
|---|---|---|
| "Share your score and challenge a friend to beat it" | The SHARE button exists in the released build | As soon as that build is live — the send side works with zero verification |
| "Tap a friend's challenge and play their exact run" | The receive side works on a released build | Only after the App Link verifies **or** the page's `veyro://` fallback is confirmed working on a released build |
| "Beat my score" landing page | The page is deployed | After `wrangler deploy` |

Practical recommendation: ship the listing sentence in the *send* register ("send anyone your run
and dare them to beat it") with the first release that has the button, and only add "tap a
challenge link to play the exact same track" once verification is confirmed on a production install
(`adb shell pm get-app-links` reporting `verified`). An unverified link still lands the recipient on
a working page that offers the install — so the user-visible promise is never broken, which is the
thing Misleading Claims actually polices.

---

## 8. Privacy policy — text to add

Add to **both** `docs/PRIVACY_POLICY.md` and `site/public/privacy/index.html`, keep them in sync by
hand, bump the effective date in both, then `npx wrangler deploy` from `site/`
(`STORE_COMPLIANCE.md` standing rule 4). Version-scope the new section the way the T-009 and T-020
sections are scoped, so the page stays truthful whenever it deploys.

### 8.1 New section — place after "Player profile and leaderboards", before "Purchases"

```markdown
## Sharing a run, and challenge links

*This section applies to versions with a SHARE button on the run-over card (the challenge update
onward).*

- **Sharing is your action, and the game sends nothing.** Tapping SHARE opens Android's own share
  sheet — your phone's standard list of apps. The game hands that sheet one line of text and stops
  there. Whether the message goes anywhere, and to whom, happens inside the app you pick, in your
  hands. We never see the message, the recipient, or which app you chose, and the game does not read
  your contacts or check which messaging apps you have installed.
- **What the message contains.** Your score, the distance, whether it was that day's Daily Run, and
  a link like `https://veyro.ferrabled.com/challenge/?s=1234&v=1&w=greybox&p=4210&m=free`. Those
  values are the track's random seed, the content version, the world name, your score and the run
  mode — they describe *a run*, so your friend plays exactly the track you played. Your player name,
  your Player ID, your device, and anything from the camera or sensors are **not** in it, and there
  is no box for you to type your own text.
- **Opening someone's challenge link.** If you have the game, the link opens it and sets up the same
  track with the same seed. If you do not, the link opens a page on our website showing the score to
  beat, with a link to the Google Play listing. That page sets no cookies, runs no analytics, and
  does not know who you are — it reads the score and the seed out of the link's own address in your
  browser. See "About this website" below for what happens at the infrastructure level.
```

### 8.2 Amend "The short version" — add one sentence at the end of the first paragraph

```markdown
Versions with the SHARE button hand a single line of text to your phone's own share sheet: the game
itself sends nothing, and the message carries a score and a track seed, never your name or your ID.
```

### 8.3 Amend "About this website" — append to the existing paragraph

```markdown
The challenge page (veyro.ferrabled.com/challenge/) works the same way: it reads the score and the
track from the link's own address, in your browser, and stores nothing. As with every page here, the
request itself passes through Cloudflare — which means the address you asked for, including the
score and seed inside it, is handled by Cloudflare's infrastructure like any other URL. We add no
tracking of our own and keep no logs.
```

> The last clause ("we … keep no logs") is only true while we do not enable Workers/Logpush logging.
> If a click counter or log sink is ever added, §9.2 applies and this sentence must change.

### 8.4 Amend "Changes" — append

```markdown
The "Sharing a run, and challenge links" section was added on 21 September 2026, ahead of the
challenge update.
```

### 8.5 Also add to the challenge page itself

The landing page needs one line of its own, because a visitor there may never see the policy:

```html
<p class="fine">This page shows a score and a track seed taken from the link you followed. It sets
no cookies and runs no analytics. <a href="/privacy/">Privacy policy</a></p>
```

---

## 9. What would change if…

### 9.1 We share a PNG of the result card (FileProvider)

| Area | Change |
|---|---|
| Permission | **Still none**, provided the PNG is written to the app's own `cacheDir`/`getExternalFilesDir` and shared via a `content://` URI. Never `MediaStore`, never `WRITE_EXTERNAL_STORAGE` |
| Manifest | Adds `<provider android:name="androidx.core.content.FileProvider" android:authorities="com.ferrabled.veyro.run.fileprovider" android:exported="false" android:grantUriPermissions="true">` + a `file_paths.xml` resource. That is a real manifest/resource addition ⇒ CLAUDE.md gotcha #10 applies: rebuild the release APK, `aapt2 dump permissions` diff, size diff |
| Intent | `type="image/png"`, `EXTRA_STREAM` = the content URI, plus `Intent.FLAG_GRANT_READ_URI_PERMISSION` — "Always grant read URI permissions and use FileProvider for temporary, secure access" ([Sending simple data](https://developer.android.com/training/sharing/send)) |
| Data safety | **Unchanged.** Same user-initiated exemption; the image is generated by the app from its own UI and still never transmitted by us |
| Privacy policy | §8.1 bullet 2 must change: the message now also carries an image of your result card, **which will show your generated player name if the card shows it**. Say so plainly |
| Content rating | Still No — the image is app-rendered, and the exchange still happens in a secondary app (§5) |
| **Hard rule** | The rendered image must never contain camera imagery or a camera preview. "Camera frames are never transmitted anywhere" is load-bearing in the policy, the listing, the support page and the Data safety form (`STORE_COMPLIANCE.md`, "If camera data EVER leaves the device"). Render the card from game state, never from a framebuffer that could include the camera feed |
| Unity note | Unity's `ScreenCapture.CaptureScreenshotAsTexture` on a URP frame is the usual trap; compose the card explicitly instead |

### 9.2 The challenge page gets a click counter

| Area | Change |
|---|---|
| Play Data safety | **Unchanged** — the form covers the app, and the app does not call the page. This holds *only* while the counter is a web-page thing. If the game ever pings the endpoint, that is app collection and a Data safety row |
| Privacy policy | **Must change.** "This website … runs no analytics or trackers" and the drafted "we keep no logs" become false. Rewrite, bump the effective date, redeploy (standing rule 4) |
| Cookie consent | Use a cookieless counter (Cloudflare Web Analytics, or a Worker increment into KV keyed by nothing but the day). Any cookie or device fingerprint pulls in the EU ePrivacy consent-banner obligation, which this site currently avoids entirely |
| Data minimisation | Count, do not log. Storing `(timestamp, IP, full query string)` per visit creates a record linking an IP to a specific person's score and seed — still not "personal and sensitive user data" in Play's enumeration, but a needless retention problem. Aggregate at write time; keep nothing per-visit |
| Listing/Console | No change |

### 9.3 The player can type a message or a name

This is the one that actually moves things, and it is the same tripwire `STORE_COMPLIANCE.md`
already flags for T-009 handles.

| Case | Consequence |
|---|---|
| Free text goes **only** into the player's own outgoing message (never stored by us, never shown to other Veyro users) | IARC answer arguably still No, because the exchange is through a secondary app and we host nothing — but the carve-out is written about *sharing mechanisms*, not about authoring. Treat as amber: get it in writing from review, or don't ship it |
| Free text is **carried in the challenge URL** and rendered on our page | Now we publish user-authored content. IARC **Yes** to Online Interaction or Content Exchange ⇒ rating label gains "Users Interact" and likely rises above PEGI 3; Play UGC expectations (moderation, reporting, takedown) attach to the page; the page needs at minimum server-side escaping and a profanity filter, and a way to report abuse. Also a trivially abusable open text-rendering endpoint |
| Free text shown to other players in-app (leaderboard handle, challenge card) | Same as above plus: content rating questionnaire **must** be re-submitted ([9859655](https://support.google.com/googleplay/android-developer/answer/9859655?hl=en)); the T-009 "no UGC to declare" line in `STORE_COMPLIANCE.md` stops being true; if the target audience ever included under-13, the Families social-feature requirements engage in full |
| Privacy policy | The "there is no box for you to type your own text" sentence must go, replaced by an honest description of what we store and for how long |

**Recommendation: keep every shared string app-generated.** The cost of typed text is a rating
change, a moderation obligation and a new data category; the benefit is a nicety.

---

## 10. Owner-only checklist

Ordered. Nothing here can be done by an agent.

- [ ] **Fingerprint.** Play Console → *Protected with Play → Play Store protection → Manage Play app
      signing* (older UI: *Release → Setup → App signing*). Copy the **App signing key certificate**
      SHA-256. Also copy the **Upload key certificate** SHA-256. Paste both into
      `site/public/.well-known/assetlinks.json` (array of fingerprints, §3.2/§3.3). Do **not** use a
      local `keytool` fingerprint of the upload keystore as the only entry.
- [ ] **Deploy the site before the build ships.** `npx wrangler deploy` from `site/` — this must
      publish `/challenge/`, `/.well-known/assetlinks.json` and the updated `/privacy/` page
      *before* any tester gets the build with `autoVerify`. Verification runs at install time; a
      missing file means an unverified state that persists until the next re-verify.
- [ ] **Verify the asset file by hand:** `curl -I https://veyro.ferrabled.com/.well-known/assetlinks.json`
      must return `200`, `content-type: application/json`, and **no** 301/302 anywhere in the chain.
- [ ] **Privacy policy.** §8.1–§8.4 are applied to `docs/PRIVACY_POLICY.md` and
      `site/public/privacy/index.html` with the effective date bumped to 28 September 2026 in both
      (done by an agent, 28 Sep PR review). Owner: read it, then redeploy (standing rule 4).
- [ ] **Rebuild + diff.** Release APK/AAB, `aapt2 dump permissions` against the previous release,
      plus a size diff. Expect **no** permission change (standing rule 3, CLAUDE.md gotcha #10).
- [ ] **Device check (needs human device test).** On a Play-installed build:
      `adb shell pm get-app-links com.ferrabled.veyro.run` → expect `verified` for
      `veyro.ferrabled.com`. Then tap a challenge link from a chat app (cold start and warm start),
      and tap one on a phone **without** the game to confirm the page → Play listing path.
- [ ] **Listing.** Only add challenge wording that matches the shipped build (§7). Send-side wording
      is safe immediately; receive-side wording waits for a `verified` production install.
- [ ] **Do NOT touch:** the Data safety form, the content rating questionnaire, the App access
      answer, the target audience brackets, or the Advertising ID declaration. None of them change
      (§0). Record in `STORE_COMPLIANCE.md` that the assessment was made and the answers stand.

---

## 11. Sources

- [Provide information for Google Play's Data safety section](https://support.google.com/googleplay/android-developer/answer/10787469?hl=en) — "collect" definition, shared-data exceptions, user-initiated action
- [Understand app privacy & security practices with Google Play's Data safety section](https://support.google.com/googleplay/answer/11416267?hl=en&co=GENIE.Platform%3DDesktop) — user-facing wording of the same exemptions
- [User Data policy](https://support.google.com/googleplay/android-developer/answer/10144311?hl=en) — personal & sensitive user data definition, prominent disclosure, user-initiated transfer is not sale, privacy-policy requirement
- [Deceptive Behavior](https://support.google.com/googleplay/android-developer/answer/9888077?hl=en) — misleading claims, misrepresenting functionality
- [Online Interaction or Content Exchange](https://support.google.com/googleplay/android-developer/answer/7021383?hl=en) — **the IARC answer**: secondary apps excluded
- [Content rating requirements for apps, games, and the ads served on both](https://support.google.com/googleplay/android-developer/answer/9859655?hl=en) — when the questionnaire must be retaken
- [Apps & Games content ratings on Google Play](https://support.google.com/googleplay/answer/6209544?hl=en) — interactive-element labels
- [Google Play Families Policies](https://support.google.com/googleplay/android-developer/answer/9893335?hl=en) — "social feature" definition
- [Target audience and content](https://support.google.com/googleplay/android-developer/answer/9285070?hl=en) — 13+ selection means Families policy does not apply
- [Use Play App Signing](https://support.google.com/googleplay/android-developer/answer/9842756?hl=en) and [Obtaining your app's SHA-256 certificate fingerprint](https://support.google.com/googleplay/android-developer/answer/16641489?hl=en)
- [Sending simple data to other apps](https://developer.android.com/training/sharing/send) — ACTION_SEND, createChooser, FileProvider
- [Handling Android App Links (overview)](https://developer.android.com/training/app-links) — deep link vs web link vs App Link
- [Verify Android App Links](https://developer.android.com/training/app-links/verify-android-applinks) — intent-filter shape, `pm get-app-links`
- [Configure website associations](https://developer.android.com/training/app-links/configure-assetlinks) — assetlinks.json shape, hosting rules, Play App Signing fingerprint
- [Behavior changes: all apps (Android 12)](https://developer.android.com/about/versions/12/behavior-changes-all) — unverified web intents resolve to the default browser
- [`<activity>` manifest element](https://developer.android.com/guide/topics/manifest/activity-element) — `android:exported`
- [Unity Manual: Deep linking on Android](https://docs.unity3d.com/6000.1/Documentation/Manual/deep-linking-android.html) and [`Application.deepLinkActivated`](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Application-deepLinkActivated.html)

---

## 12. iOS / App Store addendum (28 September 2026)

Written for the iOS build (`feat/iOS-implementation`, `docs/IOS_HANDOFF.md` Phase 2c). The
Android sections above are unchanged and still describe the Android build.

### 12.1 What ships on iPhone

| Piece | iOS behaviour | Evidence |
|---|---|---|
| SHARE button | **Clipboard only.** `ShareSheet.Send` takes its `#else` branch and writes the text to `GUIUtility.systemCopyBuffer`, then returns `false`. There is no `UIActivityViewController` and no native plugin (owner decision 4, 28 Sep) | `game/Assets/Scripts/Core/ShareSheet.cs` |
| Share text | The same text as on Android: `ChallengeMessage.Build` with the same https link (`GameLinks.ChallengeBaseUrl`) | `Core/RunShare.cs` |
| `veyro://challenge?…` | Registered as `CFBundleURLTypes`: `BuildScript.BuildIOS` sets `PlayerSettings.iOS.iOSUrlSchemes = ["veyro"]` (the constant is `AndroidChallengeLinks.Scheme`), and `IosBuildPostProcess` **fails the build** if the exported Info.plist lacks it | `game/Assets/Editor/BuildScript.cs`, `IosBuildPostProcess.cs` |
| Cold and warm start | **No code change.** `DeepLinks.Begin()` reads `Application.absoluteURL` for the cold start and subscribes to `Application.deepLinkActivated` for the warm one. Unity's iOS app controller fills both ([Unity: deep linking on iOS](https://docs.unity3d.com/6000.0/Documentation/Manual/deep-linking-ios.html)). `DeepLinks`, the `GameBootstrap` call site and `RunFlow.TryStartPendingChallenge` (polled from `Update`) have no platform guard. The Android `singleTop` concern (§2.2) does not exist, because an iOS app is always a single instance | `Core/DeepLinks.cs`, `Core/GameBootstrap.cs:126`, `Gameplay/RunFlow.cs:404–441` |
| Parser | `ChallengeMessage.TryParse` is engine-free and platform-free. It accepts `veyro://challenge?…` and `https://veyro.ferrabled.com/challenge/?…`, and `ChallengeMessageTests` pin both shapes | `Track/ChallengeMessage.cs` |
| https challenge link | **The app does not handle it on iPhone.** There are no Universal Links: no Associated Domains entitlement and no `apple-app-site-association`. A tap on the shared https link opens Safari and the landing page | deferred, §12.3 |
| Landing page on iPhone | `challenge.js` treats an iPhone like any other non-Android visitor (L113–134). OPEN sets `location = veyro://challenge?<query>` with a 1.5 s timer that goes to the **Play listing** if the page is still visible. The "get the game" link (`ch-store`, L139) also goes to the Play listing | deferred, §12.3 |

### 12.2 App Store privacy and review

- **App Privacy ("nutrition label"): no row.** Apple's definition:

  > "Collect" refers to transmitting data off the device in a way that allows you and/or your
  > third-party partners to access it for a period longer than what is necessary to service the
  > transmitted request in real time. … Data that is processed only on device is not "collected"
  > and does not need to be disclosed in your answers.
  > — [App privacy details on the App Store](https://developer.apple.com/app-store/app-privacy-details/)

  A clipboard write never leaves the device through us. Whatever the player pastes afterwards
  goes out through the app they paste into. The received link is parsed on the device. This
  matches `docs/store-kit/APP_STORE_LISTING.md` (age-rating row: "challenge links go out through
  the system share sheet (or clipboard), not an in-app feed"). That file belongs to the Layers /
  privacy track, and nothing in it changes.
- **No permission, no purpose string, no privacy-manifest entry.** Writing to the general
  pasteboard needs no entitlement and shows no prompt. SHARE never *reads* the pasteboard, so
  Apple's paste prompt never appears for SHARE. (IMPORT PROFILE does read it; see
  `PROFILE_LEADERBOARD_PLAN.md` "iOS storage".) The pasteboard is not a required-reason API
  category. A custom URL scheme needs no entitlement.
- **Guideline 2.3.10 (no other platforms named).** The iOS build never names Android or Google in
  the shop (`Menu/StoreCatalogView.cs`, split with `#if UNITY_IOS`). The notification panel is
  split by the OneSignal track, and the camera strings by the camera track. The landing page is a
  website, not app metadata. It still sends iPhones to Google Play, though, which is one more
  reason to fix it once the app is live (§12.3).
- **Link-payload rule (§1.3) is unchanged:** the text and URL carry no handle, Player ID,
  RevenueCat ID or recovery code on either platform.

### 12.3 Deferred, and why

1. **Universal Links.** They need the `applinks:veyro.ferrabled.com` Associated Domains
   capability on the App ID and in the entitlements, plus
   `site/public/.well-known/apple-app-site-association` (appID
   `69J7W5URX7.com.ferrabled.veyro.run`, paths `/challenge*`, served as JSON with no redirects,
   the same hosting rules as §3.2). They are not built for v1.0: they need owner console work and
   a device to verify, and the custom scheme already covers "the game is installed" via the page.
2. **The landing page's App Store fallback.** `challenge.js` sends every non-Android visitor,
   iPhones included, to `PLAY_URL` (L22, L132, L139), and its own comment (L114–115) says an iOS
   build will want a Universal Link and an App Store fallback. No App Store URL
   (`https://apps.apple.com/app/id<Apple ID>`) exists until the app is live. **Owner call: do not
   change it before then.** It lands together with the site's App Store button
   (`IOS_HANDOFF.md` §10), with a site deploy.
3. **Known iPhone risk on the page (unverified; needs the device).** Safari asks "Open in
   'Veyro Run'?" before it hands a custom scheme to an app, and the page stays visible while it
   asks. Chrome has the same trait, which is why the Android branch has no timer (L108–111). The
   1.5 s timer may therefore move a player who *has* the game to the Play listing while the
   prompt is up. Without the game, Safari shows an "address is invalid" alert and then the timer
   goes to Play. That is accepted until item 2. If the device shows the first case, fix it in the
   same change as item 2: no timer on iOS, the scheme as a plain href, and the store behind its
   own link.
4. **A native share sheet** (owner decision 4). It would need an iOS plugin and a TestFlight
   loop. Data-wise it is the same user-initiated hand-off as Android's chooser (§1.2), so
   building it later changes no privacy answer.

### 12.4 Device checks (TestFlight; `IOS_HANDOFF.md` §9 items 7–8)

- [ ] SHARE on the result card: the button reads **COPIED** for about 2 s, then SHARE again
      (`RunShare.Share` returns `ShareSheet.Send`'s result; `RunHud` + `Track/CopiedFlash`, iOS
      only). Paste into Notes or Messages: the full line and the https link arrive intact. RUN
      AGAIN → the next card starts on SHARE.
- [ ] Take a `veyro://challenge?…` link from a real share (take the query from a SHARE on the
      same build, so `v`/`w` match; otherwise RunFlow refuses the challenge, by design) and open
      it from Notes or Safari **cold** (app swiped away) and **warm** (app in the background). The
      challenge run starts from the menu both times.
- [ ] Tap an https challenge link in Messages: Safari opens the page, and OPEN opens the game on
      that track. Record whether the 1.5 s timer jumps to the Play listing while Safari's prompt
      is showing (§12.3 item 3).
