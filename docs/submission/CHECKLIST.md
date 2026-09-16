# T-035 — submission artefact checklist

Every artefact the Devpost form needs, with who produces it and what blocks it. Written 23 Aug 2026.

**Deadline: 30 Sep 2026, 11:45pm PDT.** Target the form **complete ≥ 24 h before** (AC in T-035), i.e.
**by 29 Sep**. Judging runs 1–13 Oct; winners 21 Oct [S2].

Legend: ☐ not started · ⏳ blocked · ✅ done. Update in place.

---

## A. Eligibility — no artefact, but the submission is void without them

| | Requirement | Status | Blocked on |
|---|---|---|---|
| A1 | RevenueCat SDK powering ≥ 1 real in-app purchase in the published build | ☐ | **T-020.** P1/P4 cleared 24 Aug — the Unity half is unblocked *now* (Test Store). Real-purchase verification needs the 3 Play products created (`PLAY_CONSOLE_SETUP.md` §D1) + service-account JSON (§D3, ~36 h) |
| A2 | First public release **between 1 Aug and 30 Sep 2026** on App Store or Google Play | ☐ | **T-031.** Closed test live 26 Aug → production ~16 Sep. Remaining: listing assets, and 12 testers opted in *continuously* |
| A3 | App accessible from the US | ☐ | Play Console country settings — do not restrict to Spain |
| A4 | Devpost account registered for Shipaton 2026 | ☐ | **P8** |
| A5 | Entrant not resident in an excluded territory | ✅ | — |

> **A2 has a trap in it.** The rules say a project may have existed before the window *"but it must
> not have been publicly released on any eligible store before the Submission Period. Updates to
> previously released apps are not eligible."* [S2] Veyro Run has never been released, so we are
> clear — but this is the reason the closed-testing track and internal builds must stay private, and
> the reason nothing gets pushed to production "just to test the pipeline" before the listing is
> ready. Closed testing is not a public release; a production rollout is.

## B. Required form fields

| | Artefact | Spec | Producer | Status | Blocked on |
|---|---|---|---|---|---|
| B1 | Project name | — | owner | ✅ `Veyro Run` | — |
| B2 | Text description | see `DEVPOST_ANSWERS.md` §2 | drafted ✅, needs `[N]` fills | ☐ | real numbers |
| B3 | Demo video | **< 2:00**, public on YouTube/Vimeo, app shown functioning on a device, no unauthorised trademarks or copyrighted music [S2] | owner + device | ☐ | script ✅ `VIDEO_SCRIPT.md`; needs the shoot |
| B4 | Live store URL | app publicly listed | owner | ⏳ | **T-031** — closed test live 26 Aug, production ~16 Sep |
| B5 | App icon | **1024 × 1024** [S1] | designer | ☐ | brief ✅ `store-kit/ART_DIRECTION.md` |
| B6 | Screenshot | **≥ 1 at exactly 1179 × 2556, no device frame** [S1] | device owner | ☐ | recipe ✅ `store-kit/SCREENSHOTS.md` §3 |
| B7 | Judge access — free trial **or** promo code | promo codes for the cosmetic SKU | owner, in Play Console | ⏳ | **T-020 + product live**; text ✅ `DEVPOST_ANSWERS.md` §4 |
| B8 | Categories selected | only genuinely targeted ones [S3] | owner | ☐ | the ⚠️ audit in `DEVPOST_ANSWERS.md` §1 |

**Verify B6 with a command, not with an eyeball:**
```bash
python3 -c "from PIL import Image; print(Image.open('shot.png').size)"   # must print (1179, 2556)
```

## C. Per-category artefacts — only for categories actually entered

| | Category | Artefact needed | Status |
|---|---|---|---|
| C1 | Best Game | gameplay/art/monetization description | drafted, art section blocked on T-006 |
| C2 | Design | paywall + UX description | blocked on T-020 |
| C3 | HAMM | pricing rationale + conversion numbers | blocked on T-020 + real traffic |
| C4 | #BuildInPublic | links to profile and specific posts | blocked on **P9** + T-040 |
| C5 | OneSignal | **App ID** + ≥ 1 deployed campaign, described | blocked on **P5** + T-021 |
| C6 | Layers | SDK installed and verifiable + experiment write-up with outcome | blocked on **P6** + T-022 |
| C7 | Noise | live app URL + Noise account email | blocked on **P7** |
| C9 | Catvertising | — | **not entered** (T-023 verdict, STATUS 23 Aug) |

## D. Things that are easy to forget

- ☐ **YouTube video set to Public or Unlisted, and embeddable.** A private video is a filtered
  submission. Check it from a logged-out browser.
- ☐ **Promo codes generated and written down somewhere that is not this repository.** They are
  bearer tokens; do not commit them.
- ☐ **Promo code tested end-to-end on a second Google account** before it goes in the form
  (`REVENUECAT_PLAN.md` §6.2, Route A). An untested code in a submission is a coin flip.
- ☐ **Store listing screenshots not stale** — recapture if the visible Daily Run date is more than
  ~2 weeks old (`SCREENSHOTS.md`, Shot 3).
- ☐ **Privacy policy URL live** — required by Play regardless of how little data is collected
  (`store-kit/LISTING.md` §5; OPEN_QUESTIONS **9** — renumbered in the 24 Aug merge; 12 is the camera-in-v1.0 decision).
- ☐ **Play Data safety form matches reality** — camera is in the build **from v1.0**, not a later update, so this must declare camera access as on-device-only *before* the closed track publishes.
- ☐ **`productName` reads "Veyro Run"** in Player Settings, not `Motion Runner`.
- ☐ **RevenueCat bundle ID matches the store listing** — screeners verify the SDK against it [S3].
- ☐ **No self-purchases.** Revenue must be organic; artificial transactions violate store policy and
  poison the submission (`docs/LICENSING_REVENUE.md` §5).
- ☐ **Tax forms.** Devpost typically requires W-8BEN (non-US) from winners
  (`docs/LICENSING_REVENUE.md` §3) — worth having the details to hand, not worth chasing now.

## E. Timeline

Recomputed 26 Aug: P1 cleared 24 Aug and closed testing went live 26 Aug, five days early.

| Date | Milestone | Blocked on |
|---|---|---|
| 24 Aug ✅ | Play Console approved + verified | — |
| 26 Aug ✅ | Closed-testing build up on the closed track | — |
| **now** | **12+ testers opted in** — the clock only starts at the 12th | H6 (recruit 16–18) |
| ~2 Sep | 3 Play products created + service-account JSON uploaded (~36 h propagation) | `PLAY_CONSOLE_SETUP.md` §D |
| ~5 Sep | T-020 done: real sandbox purchase + entitlement survives reinstall | §D above |
| ~8 Sep | Store listing complete (copy ✅ incl. camera, icon, screenshots) | T-006, designer |
| ~9 Sep | 14 days elapsed → apply for production | 12 testers continuous from 26 Aug |
| ~16 Sep | Production live → **B4 store URL exists** | Play review, ≤ ~7 days |
| ~16–20 Sep | Video shot (camera mode is already **in** v1.0, not an update) | T-011/12/13 playtest |
| ~20 Sep | Promo codes generated and tested | B7 |
| **29 Sep** | **Devpost form complete** | everything above |
| 30 Sep 23:45 PDT | Deadline | — |

**The single point of failure is now tester retention, not P1.** P1 cleared and the build is up, so
the one date in this chain that cannot be compressed by working harder is the 14 continuous days —
and it does not even start until the 12th tester is opted in. Twelve is a floor; recruit 16–18, and
check the opted-in count in Console rather than assuming (you cannot discover you are at eleven on
day 13).

There is now ~10 days of slack, all of it *after* production access. Google Play is the only release
path, with iOS via P2 as the sole backup if Play review rejects — treat the Play dates as having no
second store behind them.

---

## Sources

- **[S1]** Shipaton 2026 overview — https://revenuecat-shipaton-2026.devpost.com/
- **[S2]** Shipaton 2026 rules — dates, eligibility, "updates to previously released apps are not eligible" — https://revenuecat-shipaton-2026.devpost.com/rules
- **[S3]** How we judge Shipaton — https://www.shipaton.com/blog/how-we-judge-shipaton
