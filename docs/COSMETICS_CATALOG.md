# Cosmetics & catalog — products, entitlements, and the Season 1 pass

Written 23 Aug 2026 (owner dashboard session). Supersedes the two-SKU proposal in
`REVENUECAT_PLAN.md` §4 / old OPEN_QUESTIONS 9, after the owner's direction: **two premium skins
sold directly, plus a Season 1 pass with a free and a paid reward track**. D6 still binds —
everything here is cosmetic; nothing affects gameplay, physics, or track generation.

**Status: proposal awaiting owner sign-off on the ★ fields in §7.** Product ids are immutable once
created in Play Console and entitlement ids are referenced from code — those are the one-way doors.
Display names, prices (until first publish), item art, and the paywall are all changeable later.

---

## 1. The vocabulary — one rule, four layers

The confusion this section settles: *"should every pass item be a product?"* **No.** The layers:

| Layer | Lives in | What it is | We have |
|---|---|---|---|
| **Product** | Play Console, mirrored in RevenueCat | The thing money buys. Has a price, an immutable id, and promo codes are generated per-product. | **3** |
| **Entitlement** | RevenueCat | An unlock flag on a *customer*. Buying any product attached to it grants it. The app reads **only** these. | **3** |
| **Offering / package** | RevenueCat | Presentation: which products the paywall shows, in what order. Pure display layer. | 1 offering, 3 packages |
| **Cosmetic item** | Game code (`CosmeticCatalog`, pure C#) | A colour, trail, hat, aura, or skin. Unlocked by a *rule*: default, owns-entitlement, or reached-level-on-track. | ~25 |

**The rule: money buys products → products grant entitlements → the app checks entitlements →
items are game data with unlock rules.** Play and RevenueCat never hear about individual pass
items, because:

1. They are not individually sellable — a Play product with no price and no purchase path is
   meaningless, and each product created is permanent id surface plus store-review surface.
2. Level-gating is game logic. Play knows nothing about XP; a store cannot express "unlocks at
   level 7 if you own the pass". The pass *product* buys the `season1` entitlement once; which
   items that exposes at which level is entirely `CosmeticCatalog`'s business.
3. It keeps T-020 small: the `IStore` seam and its tests don't grow with the item count.

### "Lifetime" packages — not a lifetime subscription

RevenueCat packages carry a *duration label* used by paywall templates: `$rc_monthly`,
`$rc_annual`, `$rc_lifetime`, or custom. **`Lifetime` is simply RevenueCat's word for a one-time
purchase that never expires** — exactly what a non-consumable is. It creates no subscription and
changes no billing. To avoid the ambiguity entirely we use **custom package identifiers** (below);
they are presentation-only strings.

---

## 2. Store catalog (what gets typed into the dashboards)

### Products — all three one-time, **NON-CONSUMABLE** (see `REVENUECAT_PLAN.md` §2.1; not optional)

| Product id (immutable ★) | Display name (mutable) | Price ★ | Grants entitlement(s) |
|---|---|---|---|
| `veyro.skin.ember` | Ember Skin | €2.99 | `skin_ember` |
| `veyro.skin.frost` | Frost Skin | €2.99 | `skin_frost` |
| `veyro.season1.pass` | Season 1 Pass | €4.99 | `season1` |

Skins sold **individually** rather than as a pack ★: a buyer who wants one isn't forced to buy
both, the €2.99 → €4.99 ladder reads as deliberate pricing (HAMM judges strategy), and a
"Supporter Bundle" that grants **all three entitlements at once** can be added later as a fourth
product **with zero app-code change** — products attach to multiple entitlements dashboard-side.
That is also why entitlements are per-skin rather than one `cosmetics` flag: packaging stays a
dashboard decision forever.

Season 2, if it ever happens, is a **new product** `veyro.season2.pass` → entitlement `season2`.
No rollover or expiry code ships in v1.0; Season 1 just has an end date (§3).

### Entitlements

| Entitlement id (code-referenced ★) | Unlocks |
|---|---|
| `skin_ember` | the Ember skin preset |
| `skin_frost` | the Frost skin preset |
| `season1` | the paid track of the Season 1 reward ladder |

### Offering

One offering, id `default`, current. Three packages with **custom identifiers**
`skin_ember`, `skin_frost`, `season1_pass`, one product each. The Paywall Builder paywall attaches
to this offering; per `REVENUECAT_PLAN.md` §5 the paywall itself is server-side and endlessly
revisable after the store build freezes.

### Judge promo codes (updates `REVENUECAT_PLAN.md` §6)

Codes are per-product: generate **10 for `veyro.season1.pass` and 5 per skin** (well inside the
500/quarter budget). The pass code is the one that goes in the Devpost description — it demos the
richest premium surface (paid track + instant level-1 unlock, §3).

---

## 3. Season 1 pass design

- **XP** is earned from finished runs, derived from the final score (formula and level thresholds
  are a pure C# table in `MotionRunner.Commerce` — deterministic, EditMode-tested, tuned in T-025
  and frozen before the production release, same discipline as OPEN_QUESTIONS 6).
- **10 levels ★**, each with one **free-track** and one **pass-track** reward. Everyone sees the
  whole ladder — the paid column shows a lock icon without `season1`, which *is* the pass's
  in-game advertisement.
- **Level 1 is the starting level**, so both level-1 rewards unlock the moment the track is first
  shown — and buying the pass grants its level-1 item **instantly**. A judge redeeming a promo
  code sees immediate payoff without grinding.
- **Season 1 window ★:** production launch (~22 Sep per D11) → **31 Oct 2026**. After that the
  paid items stay owned (non-consumable entitlement; nothing is ever taken away) — only the
  *earning* window ends. Post-hackathon problem regardless.
- Progress is stored locally (no backend, D10); T-009 later syncs it via Play Games Saved Games.
  The entitlement itself always survives reinstall via the store account + Restore.

### Season 1 reward ladder (proposal ★)

| Lv | Free track | Pass track (`season1`) |
|---|---|---|
| 1 | **Mint** body colour | **Neon Lime** glow body |
| 2 | **White** trail | **Cyan Pulse** glow trail |
| 3 | **Sunset Orange** body colour | **Top Hat** (headwear) |
| 4 | **Sky Blue** body colour | **Firefly** aura |
| 5 | **Disc Cap** (headwear) | **Chrome** body finish |
| 6 | **Plum** body colour | **Twin** trail |
| 7 | **Shadow** trail | **Crown** (headwear) |
| 8 | **Charcoal** body colour | **Aurora** trail (colour-cycling) |
| 9 | **Confetti** crash burst | **Comet** aura |
| 10 | **Matte Gold** body colour | ⭐ **Prism skin** (season exclusive) |

Free track is deliberately generous (colour-heavy, one headwear, one trail, one FX) so the ladder
is worth engaging with unpaid; the pass column is where glow, metal, layered trails, auras and the
exclusive skin live. **Prism** = chrome body + colour-cycling emission + Aurora trail + Crown +
Comet aura; its combination is season-exclusive.

### Item slots

An equipped loadout is one choice per slot, selectable independently in the locker UI:
**Body** (colour/finish) · **Trail** · **Headwear** · **Aura** · **Crash FX**. A **skin** is a
preset that fills every slot at once (and is the only way to wear its exclusive parts in v1.0 —
no per-part unbundling, keeps the locker simple).

---

## 4. Direct-purchase skins (the two products)

| Skin | Preset |
|---|---|
| **Ember** | Charcoal body + orange emissive glow + fire-gradient trail + rising-spark aura |
| **Frost** | Ice-white body + pale-blue emissive glow + crystal-fade trail + drifting-snow aura |

Names ★ are non-automotive on purpose (the VEYRON caution in OPEN_QUESTIONS "App name"). The ids
bake the names in (`veyro.skin.ember`) — acceptable because *display* names stay mutable; renaming
"Ember" to anything on the store page never requires touching the id.

---

## 5. Engineering notes (why every item above is cheap)

All items compose from capabilities the project already ships — no new art tech, no new shaders
(gotchas 1/3: everything derives from the shipped `PrimitiveLit` base material):

- **Body colours** — `RuntimeMaterials.Lit(color)`, exists today (`GameBootstrap.cs:44`).
- **Glow bodies / emissive** — small `RuntimeMaterials` extension: clone base, enable
  `_EMISSION`, set `_EmissionColor`. URP Lit supports it natively.
- **Metal finishes (Chrome, Matte Gold)** — set `_Metallic`/`_Smoothness` on the clone.
- **Trails** — `TrailRenderer` with a `RuntimeMaterials` material (staying in the Lit family
  avoids shipping a second base material). Twin = two offset emitters; Aurora = colour gradient
  cycled over time.
- **Headwear** — primitives parented above the head (`Top Hat` = cylinder + disc, `Crown` = cube
  ring, `Disc Cap` = flattened cylinder). **Destroy the collider `CreatePrimitive` adds** —
  cosmetics must never change the player's collision profile.
- **Auras / crash FX** — `ParticleSystem` (`com.unity.modules.particlesystem` is already in the
  manifest); keep alive-particle counts small for the 60 FPS budget.
- **Determinism (rule 4)** — cosmetics are visual-only. Particle randomness is fine (it is not
  gameplay RNG); nothing here may read or seed track generation.
- **Unlock rules** — `CosmeticCatalog` rows: `slot`, `id`, `rule` ∈ { Default,
  Entitlement(id), SeasonLevel(track, level) }. Pure C#, EditMode-tested against `FakeStore`
  (locked/unlocked/restore/cancel cases per `REVENUECAT_PLAN.md` §3.4).

---

## 6. What this changes elsewhere

- `REVENUECAT_PLAN.md` **§4 is superseded** by §2 here (note added there). §§1–3 and 5–7 stand;
  in the §7 checklist read "two products" as "three" and add the second/third entitlement.
- **Dashboard session (today, no Play Console needed):** create the 3 entitlements, the 3 products
  (ids above, non-consumable — screenshot the config), attach, offering `default` with the 3
  custom packages, paywall on `default`.
- **Test Store convention (24 Aug):** the project's RevenueCat **Test Store** app carries mirror
  products `test.season1.pass`, `test.skin.ember`, `test.skin.frost`, attached to the *same*
  entitlements and sitting in the *same* packages as the future Play products (a package holds one
  product per app — that is its purpose). The Unity build is device-tested against the Test Store
  app's API key first; swapping to the Play Store app's key later changes nothing else. The test
  key must never ship in a store build.
- **Play Console (when P1 lands):** create the same 3 product ids, price, activate; promo codes
  per §2.
- **Backlog:** new task **T-025** (XP + season pass + locker) — pure C#/UGUI, *not* blocked on
  Play Console, ideal work while P1 is pending; must not delay T-020 the day the console arrives.
- **DECISIONS.md** (owner-only): D6 stands unchanged; the pass is still cosmetics-only.

---

## 7. Owner sign-off checklist (★ items)

- [ ] Product ids: `veyro.skin.ember`, `veyro.skin.frost`, `veyro.season1.pass` — **immutable**
- [ ] Entitlement ids: `skin_ember`, `skin_frost`, `season1` — referenced from code
- [ ] Prices: €2.99 / €2.99 / €4.99
- [ ] Skins sold individually (vs. one duo pack)
- [ ] Skin names Ember & Frost; pass exclusive named Prism
- [ ] 10 levels for Season 1, ladder as in §3 (item mix freely editable until T-025 lands)
- [ ] Season 1 window: launch → 31 Oct 2026
