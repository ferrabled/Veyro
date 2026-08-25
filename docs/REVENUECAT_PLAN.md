# T-020 — RevenueCat integration plan (design doc, no code)

Written 23 Aug 2026 (no-unity-prep session) so that **T-020 is executable in one Unity session** the
moment the Play Console exists. Nothing here has been compiled or run; every external claim is cited
and was checked against the live page on 23 Aug 2026.

T-020 is the **contest eligibility gate**: without a working RevenueCat-powered purchase in the
published build, the Shipaton submission is filtered out before a judge sees it ([S2]).

**Blocked on:** P1 (Play Console account — requested, approval pending) and P4 (RevenueCat account).
Everything in §1–§7 can be done the day both exist.

---

## 1. Version to pin: `9.8.1` — not 8.4.0

The handoff and backlog say "≥ 8.4.0". That was correct when written (8.4.0 is the release that
added Paywalls + Customer Center to Unity, Oct 2025 [S12]) but it is now **four minor versions
behind**, and the gap matters.

| | 8.4.x | **9.8.1 (recommended)** |
|---|---|---|
| Released | Oct 2025 | **20 Aug 2026** (3 days ago) [S19] |
| Play Billing Library | 8.0.0 | **8.3.0** [S20] |
| Android min SDK | 21 | **23** (ours is 26 — fine) |
| purchases-android / -ios | 9.11.0 / 5.44.1 | 10.17.0 / 5.84.0 |
| Paywalls V2 | yes | yes, **+ multipage paywalls** (9.7.0) |
| Ad tracking API | no | yes (9.1.0) |
| Ad reward verification | no | yes (9.8.0) — see `docs/STATUS.md` T-023 |
| iOS dependency via SPM | no | yes (9.1.0+, needs EDM4U 1.2.187+) [S21] |

Two reasons to take 9.x rather than the minimum that satisfies the backlog text:

1. **Play Billing deadline.** From **31 Aug 2026** all new apps and updates must ship Billing Library
   8 or later (extension available to 1 Nov 2026) [S20]. Both 8.4.x and 9.x clear it, so this is not
   a forcing function — but it means there is no "safe old version" to retreat to, and 9.x is the
   line RevenueCat is actively shipping into.
2. **T-023 depends on it.** The ad-reward primitives only exist from 9.8.0. Picking 9.8.1 now keeps
   the Catvertising option open for Update 2 at zero cost today.

**Pin exactly `9.8.1`.** Do not use a range. RevenueCat ships roughly weekly (9.5.0 → 9.8.1 in about
six weeks), and an unpinned bump between the closed-testing build and the production build would
change the store's Billing dependency under us.

### 1.1 Install method: UPM via OpenUPM (with a documented fallback)

RevenueCat supports two routes [S21]:

- **OpenUPM scoped registry** (RevenueCat's own recommendation), or
- **`Purchases.unitypackage` / `PurchasesUI.unitypackage`** imported from the GitHub release [S19].

**Take OpenUPM.** This repo is already manifest-driven — every dependency lives in
`game/Packages/manifest.json` and nothing third-party sits under `Assets/`. Gotcha 11 in CLAUDE.md
exists precisely because stray files under `Assets/` in a scratch copy silently change builds; the
`.unitypackage` route would dump ~1,500 files (SDK + a bundled copy of EDM4U) into `Assets/` and make
the `diff -rq` verification step meaningless.

Add to `game/Packages/manifest.json`:

```jsonc
{
  "scopedRegistries": [
    {
      "name": "package.openupm.com",
      "url": "https://package.openupm.com",
      "scopes": ["com.revenuecat", "com.google.external-dependency-manager"]
    }
  ],
  "dependencies": {
    "com.revenuecat.purchases-unity": "9.8.1",
    "com.revenuecat.purchases-ui-unity": "9.8.1",
    "com.google.external-dependency-manager": "1.2.186"
  }
}
```

Three things to know about that block:

- **Two packages, not one.** `com.revenuecat.purchases-unity` is the core SDK;
  `com.revenuecat.purchases-ui-unity` is RevenueCatUI (Paywall Builder + Customer Center) and is a
  *separate* package that depends on the core one. Both are versioned in lockstep at 9.8.1 [S22].
  The Paywall Builder paywall that D6 and the HAMM category want lives in the second package.
- **EDM4U is not bundled in the UPM route.** The SDK ships
  `RevenueCat/Plugins/Editor/RevenueCatDependencies.xml` declaring
  `com.revenuecat.purchases:purchases-hybrid-common:18.31.0` and `androidx.annotation:annotation:1.2.0`
  for Android [S23]; the External Dependency Manager is what turns that XML into Gradle
  dependencies. The `.unitypackage` bundles it, UPM does not — install it explicitly. Verify the
  exact current EDM4U version on OpenUPM at install time; if RevenueCat's iOS SPM path is wanted
  later it needs ≥ 1.2.187 [S21].
- **Network at import time.** A scoped registry means the scratch-copy build loop (gotcha 4) needs
  network on its first resolve. Copy `Library/` along as usual and it resolves once.

**Fallback:** if OpenUPM resolution fails or EDM4U misbehaves, import the two `.unitypackage` files
from the 9.8.1 release [S19] and add `Assets/RevenueCat*`, `Assets/PlayServicesResolver`,
`Assets/ExternalDependencyManager` to the whitelist used by the gotcha-11 `diff -rq` check. Record
which route was actually used in STATUS.md — the two produce different APK contents.

### 1.2 What this does to the APK — measure it, do not assume

CLAUDE.md gotcha 10: adding a UPM package changed the release APK even when nothing referenced it.
Expect, and **verify by diffing against the previous release APK**:

- `com.android.vending.BILLING` appears in the merged manifest (this is correct and required).
- Google Play Billing 8.3.0 + `purchases-hybrid-common` 18.31.0 AARs land in the APK. Budget a few MB
  on top of the current baseline (29.6 MB on `main`).
- RevenueCatUI pulls Jetpack Compose in on Android for the paywall UI — this is the single biggest
  size line item. If the size is unacceptable, the fallback is a hand-built UGUI paywall using
  `GetOfferings` + `PurchasePackage`, at the cost of the Paywall-Builder story that the HAMM and
  Design categories reward. **Measure before deciding.**
- Run `aapt2 dump permissions` on both APKs and record the delta in STATUS.md, per gotcha 10.

---

## 2. Unity 6 / IL2CPP / ARM64 gotchas to expect

Checked against RevenueCat's Unity docs, changelog and open issue tracker on 23 Aug 2026. There is
**no Unity-6-specific incompatibility reported** — the open-issue list is short (8 issues) and none
of them name Unity 6, IL2CPP, or ARM64 [S24]. What follows is the set of traps that *are* documented,
filtered to what applies to this project.

Our current Android settings (`game/ProjectSettings/ProjectSettings.asset`): `AndroidMinSdkVersion:
26`, `AndroidTargetArchitectures: 2` (ARM64 only), `scriptingBackend: 1` (IL2CPP), target SDK
automatic. All compatible.

1. **Consumable vs non-consumable is a one-way door.** From SDK 9.0.0 RevenueCat removed the
   workaround that allowed restoring consumed one-time products. If a one-time product is
   misconfigured as **consumable** in the RevenueCat dashboard, RevenueCat consumes it and the
   customer **can never restore it** [S25]. Our cosmetic SKUs are one-time, we have no login system,
   and T-020's acceptance criterion is literally *"entitlement survives reinstall"* — so this single
   dashboard toggle is the highest-risk item in the whole task. **Set every SKU to non-consumable and
   screenshot the dashboard before the first purchase.**
2. **Activity `launchMode` must be `standard` or `singleTop`.** Otherwise a purchase is cancelled when
   the user is bounced out to a banking app for verification [S21]. Our activity is
   `com.unity3d.player.UnityPlayerGameActivity` (gotcha 5); check its `launchMode` in the merged
   manifest, do not assume.
3. **Minify.** If code shrinking is ever enabled, add `-keep class com.revenuecat.** { *; }` to the
   proguard rules [S21]. Relevant because `Assets/link.xml` already exists for the physics module —
   note that link.xml is IL2CPP managed stripping and proguard is Java shrinking; they are different
   files solving different problems, and RevenueCat needs the Java one.
4. **Uncheck "Symlink Sources"** in Build Settings or Android throws `ClassNotFoundException` at
   runtime [S21].
5. **The SDK does not work in the Editor.** Running it in Play mode produces `NullReferenceException`
   [S21]; paywall UI is device-only [S26]. This is not a bug to work around — it is the reason §3
   exists. Nothing in EditMode tests may touch the SDK.
6. **Unity IAP is not installed here and must not be.** Both would ship BillingClient and Gradle
   fails on duplicate classes [S21]. Our manifest has no `com.unity.purchasing`; keep it that way.
7. **Scratch-copy discipline still applies.** Adding EDM4U means an editor plugin that *writes into
   the project* (`Assets/Plugins/Android/*.aar`, gradle templates) on resolve. That is exactly the
   class of debris gotcha 11 describes. After the first resolve, `diff -rq` the scratch copy's
   `Assets/` against the repo, decide deliberately which generated files get committed, and commit
   them — do not leave them only in the scratch.

---

## 3. The wrapper seam (CLAUDE.md rule 5)

> "Keep each behind a thin wrapper so tests run without them."

The existing project already has the right shape to copy: `MotionRunner.Track` and
`MotionRunner.Pose` are asmdefs with `noEngineReferences: true`, pure C#, driven by 140 EditMode
tests; the engine-touching adapters (`CameraFaceInput`, `GyroTiltInput`) sit outside them. Commerce
follows the same split.

Unlike the CV code, the RevenueCat SDK **ships its own asmdefs**
(`revenuecat.purchases-unity`, `revenuecat.purchases-unity-ui`) [S22], so the adapter does *not* have
to live in Assembly-CSharp the way `CameraFaceInput` did.

### 3.1 Two assemblies

```
game/Assets/Scripts/Commerce/
  MotionRunner.Commerce.asmdef            noEngineReferences: true, references: []
    IStore.cs            the seam
    StoreOffer.cs        plain data: id, title, localized price string, entitlement id
    PurchaseOutcome.cs   enum: Purchased | Cancelled | AlreadyOwned | Failed(reason)
    Entitlements.cs      pure logic: is X unlocked, given a set of active entitlement ids
    CosmeticCatalog.cs   SKU id -> what it changes in game (colour set, trail, …)
    FakeStore.cs         in-memory IStore for tests and for the Editor

game/Assets/Scripts/Commerce/RevenueCat/
  MotionRunner.Commerce.RevenueCat.asmdef references: MotionRunner.Commerce,
                                          revenuecat.purchases-unity,
                                          revenuecat.purchases-unity-ui
    RevenueCatStore.cs   IStore implemented over Purchases / PaywallsPresenter
```

`MotionRunner.Tests.EditMode` adds **`MotionRunner.Commerce` only** to its references. It never sees
the SDK, so `Unity -batchmode -runTests` keeps working with or without the package installed, and it
keeps working in the Editor where the SDK cannot run at all (§2.5).

### 3.2 `IStore` — the interface sketch

Callback-shaped, not `async`, to match the SDK's own delegate style (`GetOfferingsFunc`,
`MakePurchaseFunc`, `CustomerInfoFunc` [S27]) and to stay engine-free without pulling in
`UnityEngine.Awaitable` or a Task scheduler.

```csharp
namespace MotionRunner.Commerce
{
    /// Everything gameplay is allowed to know about money.
    /// No RevenueCat type crosses this boundary — that is the whole point.
    public interface IStore
    {
        /// True once the backend has answered at least once. Until then, treat
        /// every entitlement as locked but never block the player from running.
        bool IsReady { get; }

        /// Active entitlement ids, e.g. { "cosmetics" }. Empty when unknown.
        IReadOnlyCollection<string> ActiveEntitlements { get; }

        /// Raised whenever ActiveEntitlements changes, including at startup and
        /// after a promo-code redemption made outside the app.
        event Action EntitlementsChanged;

        /// The current offering, flattened. Empty if offerings have not loaded.
        void FetchOffers(Action<IReadOnlyList<StoreOffer>, StoreError> done);

        void Purchase(string offerId, Action<PurchaseOutcome> done);

        void Restore(Action<PurchaseOutcome> done);

        /// Presents the dashboard-configured paywall. On platforms/builds where
        /// no native paywall exists (Editor), completes immediately as Cancelled.
        void PresentPaywall(string requiredEntitlementId, Action<PurchaseOutcome> done);
    }
}
```

`Entitlements` is where the game asks its questions, and it is pure:

```csharp
public static class Entitlements
{
    public const string Cosmetics = "cosmetics";
    public static bool Has(IReadOnlyCollection<string> active, string id) => active.Contains(id);
}
```

### 3.3 How `RevenueCatStore` maps onto the SDK

Signatures verified against `RevenueCat/Scripts/Purchases.cs` @ 9.8.1 [S27]:

| `IStore` member | SDK call |
|---|---|
| configure (ctor) | `Purchases.Configure(PurchasesConfiguration)` — built with `PurchasesConfiguration.Builder.Init(apiKey)`; "Use runtime setup" must be ticked on the `Purchases` MonoBehaviour [S28] |
| `ActiveEntitlements` | `customerInfo.Entitlements.Active.Keys` — `EntitlementInfos.Active` is a `Dictionary<string, EntitlementInfo>` [S29] |
| `EntitlementsChanged` | subclass `Purchases.Listener` → `CustomerInfoReceived`; also `GetCustomerInfo(CustomerInfoFunc)` on resume |
| `FetchOffers` | `GetOfferings((offerings, error) => …)`, read `offerings.Current.AvailablePackages` |
| `Purchase` | `PurchasePackage(package, result => …)`; `PurchaseResult` has `UserCancelled`, `Error`, `CustomerInfo`, `ProductIdentifier` [S30] |
| `Restore` | `RestorePurchases((customerInfo, error) => …)` |
| `PresentPaywall` | `await RevenueCatUI.PaywallsPresenter.PresentIfNeeded(requiredEntitlementIdentifier: "cosmetics")` → `PaywallResultType.Purchased / Restored / Cancelled` [S26] |

Notes that belong in the adapter's header comment when it is written:

- **Anonymous app user IDs are correct for v1.0.** Pass `null` and let RevenueCat generate one [S28].
  D10 says no backend; there is no login. T-009 later adds Play Games sign-in, whose stable player ID
  becomes the app user ID via `LogIn(appUserId, …)` — do not invent an ID scheme before then.
- `SetOnesignalUserID(string)` exists on `Purchases` [S27]. Wire it in T-021, not now, but the seam
  should not make it awkward.
- The whole thing must be **fail-open**. Network down, store unavailable, SDK not configured → the
  player still gets a run. Camera mode already established this pattern (rule 3: never block the
  game on an optional subsystem); commerce is the same shape.

### 3.4 How tests run without the SDK

`FakeStore` implements `IStore` over a `HashSet<string>` and a scripted list of `StoreOffer`. Tests
worth writing in the same session (all EditMode, all engine-free, matching the 140 already green):

- entitlement gate: locked before purchase, unlocked after, still unlocked after a simulated
  restore, still unlocked after `EntitlementsChanged` fires with the same set;
- cancel is not a failure and must not unlock anything;
- `AlreadyOwned` unlocks (that is the promo-code-redeemed-outside-the-app path, §5);
- catalog: every SKU id in `CosmeticCatalog` maps to exactly one cosmetic and no two SKUs collide;
- fail-open: `IsReady == false` never throws and never grants.

The device-only half (a real sandbox purchase, entitlement surviving reinstall) is a
**"needs human device test"** per CLAUDE.md rule 6 — steps in §5.

---

## 4. Catalog proposal

> **Superseded 23 Aug 2026 (owner session):** the catalog is now **2 premium skins + a Season 1
> pass** — 3 products, 3 entitlements — specified in **`docs/COSMETICS_CATALOG.md`**. That file
> wins on any conflict with this section; §§1–3 and 5–7 of this plan stand (read "two products"
> as "three" in the §7 checklist). Kept below for the original reasoning.

Pricing and final SKU naming are the owner's call — **written up as a proposal in
`docs/OPEN_QUESTIONS.md` (item 9), not decided here.** The engineering-relevant shape:

- **One entitlement**, id `cosmetics`. One entitlement keeps `Entitlements.Has` trivial and means a
  second SKU later does not change any gameplay code.
- **Two one-time, non-consumable products** (see §2.1 — non-consumable is not optional):
  a small "support" skin unlock and a larger bundle. Both grant `cosmetics`.
- **One offering**, id `default`, with the two products as packages of type `Custom`/`Lifetime`.
  The Paywall Builder needs an offering to attach to [S31].
- **Store product ids** must be created in the Play Console *and* mirrored in RevenueCat; they are
  immutable once created, so the owner should approve the exact strings before anyone types them.

D6 binds this: **cosmetics only, no pay-to-win.** The runner's colour already comes from
`RuntimeMaterials.Lit(color)` at `GameBootstrap.cs:44`, so a skin SKU is a colour/material swap and
needs no new rendering work — the cheapest possible thing that is still a real product.

---

## 5. Paywall Builder: what is server-side, what is in-app

**Configured in the RevenueCat dashboard, no app update needed** [S31]:

- the entire paywall view — template choice, layout, components, copy, colours, images, fonts;
- which offering the paywall belongs to, and therefore which packages are shown and in what order;
- multipage paywalls (needs SDK ≥ 9.7.0);
- the Customer Center (self-service restore / manage / support), also fully dashboard-driven.

**Must be in the app** [S26]:

- the `com.revenuecat.purchases-ui-unity` package;
- one call: `await PaywallsPresenter.Present()` or `PresentIfNeeded(requiredEntitlementIdentifier)`;
- handling of the four `PaywallResultType` values.

**Consequences worth stating plainly:**

- Everything the RevenueCat **Design** category is judged on can be iterated *after* the store build
  is frozen. This is the single best schedule lever in the project: ship the paywall call in v1.0,
  then redesign the paywall as many times as we like during the judging window without a store
  review. Get the *call* in early; do not spend Sep polishing pixels in Unity.
- If no paywall is configured for the offering, the SDK shows a **default paywall listing all
  packages** [S26] — so the build cannot be "broken" by an unconfigured dashboard, it is just ugly.
- Paywall UI does not render in the Unity Editor. Every paywall change is verified on device or not
  at all.

---

## 6. The judge path — concretely, step by step

Devpost requires: *"free trial access OR promo code enabling judge testing of premium features"*
[S1][S2]. Our product is a **one-time cosmetic unlock**, so "free trial" does not apply — a free
trial is a subscription concept. **Promo codes are the path.**

### 6.1 What we generate (owner, in Play Console, once the app is live)

1. Play Console → the app → **Monetize → Promotions → Promotion codes**.
2. Create **one-time-use codes** for the cosmetic one-time product. Budget: Google allows **500
   promo codes per quarter across all one-time products in an app** [S32] — generate **10**, not
   more, and keep the rest in reserve.
3. Codes are per-product, and *"promo codes can't be used for inactive products"* [S32] — so the
   product must be **active** in Play Console, which means the app must have a published release
   (the closed-testing track counts for creating them; production is what judges will use).

### 6.2 What the judge does

Two routes, and **both work for a one-time product** [S33]:

- **Route A (in-app, the one we document):** open Veyro Run → tap the cosmetic → the Google Play
  purchase sheet appears → tap the **down-arrow next to the payment method** → **Redeem code** →
  paste → confirm. Cost: $0.
- **Route B (Play Store app):** Play Store → menu → **Redeem code** → paste. *"Users redeem these
  codes either directly from the Play Store or from within your app"* [S33]. The purchase then
  happens out-of-app.

**Route A is the one to put in the Devpost description**, with the down-arrow step spelled out. The
single most common support thread on this is developers and users looking for a "redeem" field
*inside the app* and not finding it — it is Google's billing sheet that owns the UI, not us [S34].

### 6.3 What the app must do

- **Handle out-of-app purchases.** Route B completes the purchase in the Play Store while our app is
  backgrounded; Play Billing requires apps to process purchases fetched on resume [S33]. In practice
  RevenueCat does this for us — *"RevenueCat will automatically detect and apply the entitlement
  after the transaction is processed"* [S34] — but the app must **re-read entitlements on
  `OnApplicationPause(false)`** (`GetCustomerInfo`, and `InvalidateCustomerInfoCache` if it looks
  stale) rather than only at boot. This is the `AlreadyOwned` test in §3.4.
- **Have a visible restore button.** Judges test on their own devices; a judge who redeems and then
  reinstalls must be able to get the entitlement back. `Restore` → `RestorePurchases`.

### 6.4 The fallback, if a code fails during judging

Do not improvise at 11pm on 30 Sep. RevenueCat lets an entitlement be **granted** to an app user ID
from the dashboard without a store transaction. That is only usable if we can identify the judge's
app user ID — which anonymous IDs make impossible. **Decision needed (OPEN_QUESTIONS 10):** ship a
hidden "judge mode" that calls `LogIn("shipaton-judge-<n>")` behind a code entry on the title screen,
so a specific ID can be granted from the dashboard as a manual override.
Recommended default: **yes, build it** — it is ~20 lines, it costs nothing in the APK, and the
alternative failure mode is an unscoreable submission.

**Compliance line, non-negotiable:** promo-code redemptions are $0 and are the intended judge path.
Buying our own SKU to inflate revenue violates store policy and would poison the submission
(`docs/LICENSING_REVENUE.md` §5). Honest small numbers.

### 6.5 Sandbox testing before any of that (owner + agent, on device)

Prerequisites RevenueCat calls out and that people routinely miss [S35]:

1. Real device, signed into Play, **with a screen lock PIN set**.
2. Play Console → Setup → **License testing** → add the owner's Google account.
3. A **closed testing track with a signed AAB/APK uploaded** — it does not need to be rolled out, but
   it must be uploaded, and the application id must match (`com.ferrabled.veyro.run`).
4. The tester must **open the opt-in URL**. *"If you don't complete this step, products will not
   load."* This is the #1 cause of "offerings are empty".
5. RevenueCat dashboard → enable **View Sandbox Data**.
6. Products can take a few hours to propagate after creation. Do not debug an empty offering in the
   first hour.

---

## 7. Integration checklist — ordered for one Unity session

Prerequisites (human, before the session starts): **P1** Play Console registered *and identity-verified*,
**P4** RevenueCat account + project + Play Store app linked with a service-account credential,
**P10** payments profile (products cannot go live without it), and OPEN_QUESTIONS 9 answered so SKU
ids and prices are fixed.

**A — Dashboard / console (no Unity, can be done while waiting):**

1. Play Console: create the app, application id `com.ferrabled.veyro.run`, then create the two
   one-time products with the owner-approved ids and prices, and **activate** them.
2. Play Console: enrol in the **15% reduced service-fee tier** (`LICENSING_REVENUE.md` §2).
3. RevenueCat: create the project + Play Store app, upload the service-account JSON, import the two
   products, **set both to non-consumable** (§2.1 — this is the one that bites), create entitlement
   `cosmetics`, attach both products to it, create offering `default`.
4. RevenueCat: build a paywall on `default` in the Paywall Builder. Ugly is fine today; §5 means it
   can be redesigned after the store build is frozen.

**B — Unity, in order:**

5. Add the scoped registry + three packages to `game/Packages/manifest.json` (§1.1). Let EDM4U
   resolve. Commit whatever it generates, deliberately.
6. **Immediately** build a release APK and diff it against the last one — size + `aapt2 dump
   permissions` (gotcha 10, §1.2). Record in STATUS.md. If Compose blows the size budget, decide the
   UGUI fallback *now*, not after the paywall is wired.
7. Create `MotionRunner.Commerce` (engine-free) with `IStore`, `StoreOffer`, `PurchaseOutcome`,
   `Entitlements`, `CosmeticCatalog`, `FakeStore` (§3.1).
8. Write the EditMode tests from §3.4 against `FakeStore`. Run
   `Unity -batchmode -projectPath game -runTests -testPlatform EditMode` — 140 existing tests must
   still be green.
9. Create `MotionRunner.Commerce.RevenueCat` with `RevenueCatStore` (§3.3). Configure at boot from
   `GameBootstrap` alongside the other systems, fail-open.
10. UI: a cosmetics entry point on the result screen (`RunHud` builds the result panel in code) and a
    **Restore** button. Both code-first UGUI, matching `ModeSelectMenu`/`RunHud` — no scene content.
11. Apply the entitlement: `Entitlements.Has(store.ActiveEntitlements, Entitlements.Cosmetics)` picks
    the runner colour at `GameBootstrap.cs:44`. This is the smallest honest "premium feature".
12. Re-read entitlements on app resume (§6.3).
13. Set `productName` to **Veyro Run** and `companyName` away from `DefaultCompany` in Player
    Settings — currently `Motion Runner` / `DefaultCompany`, which is what the launcher label and the
    APK metadata would show. Cheap, embarrassing if missed.

**C — Device verification (human, ~30 min):**

14. Sandbox purchase completes end-to-end (§6.5 prerequisites first).
15. Paywall renders on device (it cannot render in the Editor).
16. **Uninstall → reinstall → Restore → entitlement returns.** This is T-020's acceptance criterion
    and the thing §2.1 breaks if the product is a consumable.
17. Redeem one real promo code on a *second* Google account, via Route A — this is exactly what a
    judge will do, and it is the only way to know the instructions we ship are correct.
18. STATUS.md entry: APK size + permission delta, which install route was used, sandbox result,
    reinstall result, promo-code result, and the 10 codes' whereabouts (**not** committed to git).

---

## 8. Sources (all checked 23 Aug 2026)

- **[S1]** Shipaton 2026 overview — https://revenuecat-shipaton-2026.devpost.com/
- **[S2]** Shipaton 2026 rules — https://revenuecat-shipaton-2026.devpost.com/rules
- **[S12]** Unity SDK adds Paywalls & Customer Center (8.4.0) — https://www.revenuecat.com/release/unity-sdk-adds-paywalls-and-customer-center-support-2025-10-23
- **[S19]** purchases-unity releases (9.8.1, 20 Aug 2026; assets `Purchases.unitypackage`, `PurchasesUI.unitypackage`) — https://github.com/RevenueCat/purchases-unity/releases
- **[S20]** Play Billing Library deprecation FAQ (v8 required for new apps/updates 31 Aug 2026, extension to 1 Nov 2026) — https://developer.android.com/google/play/billing/deprecation-faq
- **[S21]** RevenueCat Unity installation — https://www.revenuecat.com/docs/getting-started/installation/unity
- **[S22]** `RevenueCat/package.json` + `RevenueCatUI/package.json` @ 9.8.1 — https://github.com/RevenueCat/purchases-unity/tree/main
- **[S23]** `RevenueCatDependencies.xml` — https://github.com/RevenueCat/purchases-unity/blob/main/RevenueCat/Plugins/Editor/RevenueCatDependencies.xml
- **[S24]** purchases-unity open issues — https://github.com/RevenueCat/purchases-unity/issues
- **[S25]** CHANGELOG 9.0.0 breaking changes + consumable warning — https://github.com/RevenueCat/purchases-unity/blob/main/CHANGELOG.md
- **[S26]** Displaying paywalls (Unity `PaywallsPresenter`, device-only) — https://www.revenuecat.com/docs/tools/paywalls/displaying-paywalls
- **[S27]** `Purchases.cs` @ main — https://github.com/RevenueCat/purchases-unity/blob/main/RevenueCat/Scripts/Purchases.cs
- **[S28]** Configuring the SDK (Unity) — https://www.revenuecat.com/docs/getting-started/configuring-sdk
- **[S29]** `EntitlementInfos.cs` — https://github.com/RevenueCat/purchases-unity/blob/main/RevenueCat/Scripts/EntitlementInfos.cs
- **[S30]** `PurchaseResult.cs` — https://github.com/RevenueCat/purchases-unity/blob/main/RevenueCat/Scripts/PurchaseResult.cs
- **[S31]** RevenueCat Paywalls — https://www.revenuecat.com/docs/tools/paywalls
- **[S32]** Play Console: create promotions (500 one-time-product codes/quarter) — https://support.google.com/googleplay/android-developer/answer/6321495
- **[S33]** Play Billing: promo codes (redemption routes, out-of-app purchases) — https://developer.android.com/google/play/billing/promo
- **[S34]** RevenueCat community: Play "Redeem code" is in Google's billing sheet, entitlement applied automatically — https://community.revenuecat.com/general-questions-7/google-play-redeem-code-option-not-showing-in-production-flutter-revenuecat-6420
- **[S35]** RevenueCat sandbox testing, Google Play — https://www.revenuecat.com/docs/test-and-launch/sandbox/google-play-store
