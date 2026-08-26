# T-030 / T-031 — closed-testing pack: 12 testers × 14 days

Written 23 Aug 2026. **This is the critical path.** Everything else in the store kit can slip a
week; this cannot.

---

## 1. The rule, verified

A Google Play developer account created after **13 Nov 2023** must run a closed test with a minimum
of **12 testers who have been opted in continuously for at least 14 days** before it can apply for
production access [P1]. Then:

- Testers who opt in, test for fewer than 14 days, then opt out **do not count**. If someone leaves
  and rejoins, **their 14-day clock restarts** — only unbroken enrolment counts [P1].
- After the 14 days you *apply* for production, answering a three-part questionnaire about the
  closed test, the app, and production readiness. Review is **"within seven days or less, but can
  occasionally take longer"** [P1].

That is D11's constraint, re-confirmed today. The owner's account is new and personal, so it applies.

## 2. What the dates mean, working backwards from 30 Sep

| | Date | Note |
|---|---|---|
| Play Console account approved + identity verified | **26 Aug** ✅ | Cleared 24 Aug (P1, owner-reported). No longer a blocker. |
| First AAB uploaded to a closed track; 12 testers opted in | **26 Aug** ✅ | Closed testing went live — five days ahead of the ~1 Sep plan. |
| 14 continuous days elapse | **~9 Sep** | Counts from the day the **12th** tester is opted in, not from upload. Any tester who drops out and rejoins resets *their own* clock, and 12 is a floor, not a target. |
| Apply for production | **~9 Sep** | |
| Production review returns | **~16 Sep** (up to 7 days, sometimes longer) | |
| Devpost submission deadline | **30 Sep, 11:45pm PDT** | Needs a **live store URL** [S2] |

**Slack in this plan: about 14 days** (updated 26 Aug — the closed test went live five days early and
P1 cleared ahead of schedule). Still **none of it in the tester step**: the 14 days are a hard
minimum that no amount of slack elsewhere can shorten.

Two consequences worth stating plainly:

1. **Recruit more than 12.** Recruit **16–18**. Some will install and never open it; some will
   uninstall in week one. Twelve opted-in-continuously is the number Google counts, and you cannot
   discover you are at eleven on day 13.
2. **Upload before the store kit is finished.** The closed-testing build does not need the icon, the
   listing copy, the paywall, or RevenueCat. It needs to install and run. If the Console appears and
   the only thing missing is a nice icon, upload anyway — the 14-day clock is the scarce resource,
   not polish. (T-020 must land before *production*, not before the closed test.)

## 3. Where to find 16–18 people

- **Shipaton Discord.** There are mutual-testing channels every year and everyone in them has the
  same problem. Highest-yield source; these people know how to opt in and will actually do it.
- **r/AndroidAppTesting** and similar mutual-testing subreddits — same dynamic, expect to reciprocate.
- Friends and family with Android phones. Lowest technical friction to ask, highest friction to get
  through the opt-in URL. Walk them through it on a call rather than sending a link and hoping.
- Any Android-owning colleagues, with the usual caution about the day job.

Note the reciprocity cost: mutual-testing groups expect the owner to test *their* apps for 14 days
too. Budget ~10 minutes a day for it. That is real, and it is cheaper than missing the gate.

---

## 4. Recruitment message

Short version, for Discord / Reddit / DMs. Owner fills the bracketed parts and posts it — **agents
post nothing.**

```
Looking for 12+ Android testers for Google Play closed testing (14 days, low effort) —
happy to reciprocate.

I'm building Veyro Run, a motion-controlled endless runner for Shipaton 2026: you tilt your
phone to steer instead of using a joystick. It's a new personal Play account, so I need the
12-testers-for-14-days closed test before I can go to production.

What I need from you:
  • A Google account email (the one your Play Store uses) so I can add you to the tester list
  • Tap an opt-in link, install, and stay opted in for 14 days
  • Play it once or twice — 2 minutes is genuinely enough
  • Please don't uninstall or leave the tester list before [DATE], it resets my clock

What you get:
  • I'll test your app for 14 days too, no questions asked
  • Credit in the release post if you want it
  • A free promo code for the cosmetic unlock once it's live

Reply or DM your Play email and I'll send the opt-in link. Thanks 🙏
[YOUR SOCIAL / GITHUB LINK]
```

## 5. Opt-in instructions to send each tester

Owner: paste the real URL and date. This is the part where people fall off, so it is written for
someone who has never done it.

```
Thanks for testing Veyro Run! Three steps, about two minutes.

1. On your Android phone, make sure the Play Store is signed in to the SAME Google account
   you sent me. (Play Store → tap your profile picture, top right, to check.)

2. Open this link on that phone:
   [OPT-IN URL FROM PLAY CONSOLE]
   Tap "Become a tester". You should see "You're a tester" confirmed.

3. Same page, tap "Download it on Google Play" and install.
   If the Play Store says the app doesn't exist, wait a few hours — new tracks take a while
   to propagate — then try again.

Then just play it once or twice whenever you like.

The one thing that matters: please stay opted in until [DATE, = opt-in + 14 days].
Google only counts testers who are opted in continuously for 14 days, and if you leave and
come back your clock restarts. Uninstalling the app is fine; leaving the tester list is not.

Any problem, message me — I'd rather fix it than lose a tester.
```

**Owner-side gotchas:**

- Testers must be added by the Google account email their Play Store actually uses. A different
  address on the list silently means they are not a tester.
- Use an **email list** (or a Google Group) in Play Console. A Google Group is easier to manage for
  16+ people than pasting addresses.
- The Play Store can take hours to show a newly uploaded track. Warn people in advance; it is the #1
  source of "the link is broken" messages.

## 6. What to ask them for

Twelve people is a real playtest — the biggest one this project will get before release. Ask for
few, specific things; a generic "any feedback?" returns nothing.

**Ask everyone (one message on day 2–3):**

1. Does the tilt steering feel immediate, or is there a lag between moving the phone and the runner
   moving?
2. Did the difficulty feel fair? Roughly how long did your best run last?
3. What confused you in the first 30 seconds?
4. Phone model + Android version, and did it feel smooth or choppy?
5. Anything crash, freeze, or look visually broken (magenta surfaces, black screen)?

Question 5 is not filler — magenta materials and black screens are the two failure modes this
project has already hit twice on device (CLAUDE.md gotchas 1–3), and they are device-specific.
Question 1 is the one that decides whether the whole premise works, and it is still marked
"owner verdict: feels fine for now" in T-002 from a sample size of one.

**Ask a subset (day 7+, only people who are still playing):** would you have come back on day 2 if a
notification had told you the Daily Run was up? — this shapes the T-021 OneSignal campaign.

**Do not ask:** for reviews or ratings. Closed-test feedback is private, and soliciting ratings is a
separate policy area.

## 7. Feedback handling

- Collect in one place — a text file, a Notion page, whatever the owner will actually use. Do **not**
  route it into `docs/` unstructured.
- Anything reproducible becomes a backlog entry with the device model in it.
- Anything about *feel* (steering sensitivity, obstacle spacing, difficulty curve) goes to the T-007
  tuning pass, which is explicitly waiting for exactly this data.
- Testers who gave good feedback are the first people to tell when it goes live, and the first
  candidates for the #BuildInPublic story (T-040). Track who they are.

---

## Sources

- **[P1]** Google Play — app testing requirements for new personal developer accounts (12 testers, 14 continuous days, production review ≤ ~7 days) — https://support.google.com/googleplay/android-developer/answer/14151465
- **[S2]** Shipaton 2026 rules — live store URL required at submission, deadline 30 Sep 2026 11:45pm PDT — https://revenuecat-shipaton-2026.devpost.com/rules
