# Profile + leaderboard + run history plan (Supabase, cross-store)

> Written 3 Sep 2026 (profile-integration session). This is the execution spec for the feature
> that triggers D10's condition ("Supabase only when the shared leaderboard ships"). It follows
> the REVENUECAT_PLAN format: decisions first, then schema, functions, Unity seam, UI, release
> flips, owner checklist. Owner decisions taken 3 Sep are marked **[OWNER 3 Sep]**; all of them
> are now recorded — D13–D16 (19 Sep), with D17 the score freeze and D18 CAPTCHA-off.

## 0. What ships and why Supabase

One shared backend for **profiles (generated handle, XP), leaderboards, and run history** that
behaves identically on Google Play, the App Store, and — when it was still a target — the Galaxy
Store *(dropped 16 Sep, D12: no Samsung commercial seller account; the schema keeps the
`android_galaxy` platform value as a harmless reserved tag)*. Platform game services cannot span
stores (Play Games has no iOS, Game Center no Android), and the Daily Run pitch — "same world
every day, who's #1?" — needs one board. Costs **$0**:
Supabase Free (500 MB Postgres, 50K MAU, anonymous sign-ins, social OAuth, 500K Edge Function
invocations/mo) + the existing Cloudflare free plan. Free-tier trap: projects pause after 7 days
with zero DB traffic → mitigated by a Cloudflare Worker cron (§7).

**Ships as Update 1 (post-v1.0)** — never in the versionCode-5 T-020 flip release; the 12×14
tester clock is untouchable. Same atomic-release rule as T-020: binary + Console flips + policy
text go out together (§8).

## 1. Identity: anonymous-first, recovery key, no login UI

- **Phase 1 (this feature): Supabase anonymous sign-in only.** Created silently on first online
  use. The Supabase user UUID is the stable player ID. No login screen exists.
- **`Purchases.LogIn(supabaseUserId)`** aliases RevenueCat to the same ID (new
  `IStore.Identify(string)`; RevenueCatStore calls `LogIn` once configured). This supersedes
  REVENUECAT_PLAN §3.3's "Play Games player ID becomes the app user ID" — proposal D14.
  Purchases are never at risk from profile loss: entitlements re-derive from store receipts via
  RESTORE, and RevenueCat transfers them to the new app user ID.
- **Reinstall restore without login — the recovery key.** At profile creation the server
  generates a random 256-bit key, returns it **once**, and stores only its SHA-256 hash
  (`profiles.recovery_key_hash`). The client keeps the key in storage that survives reinstall:
  - Android: a small file under `Application.persistentDataPath` covered by **Auto Backup**
    (proven live in this app — STATUS 29 Aug caught Auto Backup resurrecting the RevenueCat
    anonymous ID; PlayerPrefs/files restore on reinstall when the user has Google backup on).
  - iOS: **the same plaintext file**, in the app container's `Documents/`. The Keychain is
    **deferred** (owner decision, 28 Sep; reason and consequences in "iOS storage" at the end).
    Deleting the app deletes the file, so on iPhone the rescue is COPY the recovery code →
    IMPORT PROFILE, and purchases come back through RESTORE PURCHASES.
  - Boot order: valid session → use it; else recovery key present → `recover-session` Edge
    Function → same profile/XP; else → new anonymous profile. Purchases restore in the same
    boot (receipts + LogIn). **Why not persist the refresh token:** rotation makes a
    backup-restored token stale, and reuse detection can revoke the whole session family; the
    recovery key never rotates.
  - Honest caveat for policy/support copy: backup off, or a new device with no backup restore →
    the anonymous profile is unrecoverable. On iOS a plain delete + reinstall is enough to lose
    it, whatever the backup settings, unless the player copied the code first. Phase 2 fixes
    this properly.
- **Phase 2 (later release, designed-in now):** optional Google + Sign in with Apple linking
  onto the same user (`linkIdentity`). Apple guideline 4.8 (offer SIWA if Google login exists)
  triggers only then, and only on iOS. No schema change needed.
- **XP is server-authoritative.** `submit-run` increments `profiles.xp` from accepted runs; the
  client never posts an XP total. First sync after T-025 lands locally: server takes
  `max(local, server)` once via a dedicated migration path, then local becomes a cache.
  **T-025 constraint carried over:** the earn path is not date-gated. Accepted Daily and Free
  runs both earn `min(score / 100, 500)` XP (integer division); flagged runs and duplicate
  retries earn none. Free runs remain history-only, with no board rows or ranks (0008).

## 2. Owner decisions [OWNER 3 Sep] + proposals

| Decided 3 Sep | Value |
|---|---|
| Display names | **Generated handles only** (e.g. `SWIFT-FOX-42`), rerolls **unlimited** (owner amendment 17 Sep, migration 0005; was 3 lifetime), never free text → no UGC, no moderation surface, no content-rating change |
| Boards | **Daily** (per date/content_version/world_id) **and All-time**, each split by input group |
| Input split | `standard` (tilt/touch, incl. camera_fallback) vs `camera`. A camera run that degrades to tilt (`camera_fallback`) counts on the standard board + history, **never** the camera board |
| Free-mode runs | Personal history only, no board |
| Reinstall | Profile + XP + purchases must all return without login (recovery key, §1) |

**Recorded in DECISIONS.md, 19 Sep** (numbers shifted 17 Sep — D12 is the Samsung drop):
**D13** Supabase backend now, **D14** Supabase UUID = RevenueCat app user ID, **D15–16** the
table above, **D17** score formula frozen (the shared board plus the server validator make it
practically immutable — the formula, `game_config` and `validate.ts` now move together or not
at all), **D18** CAPTCHA deliberately off. Do not relitigate; propose changes via
OPEN_QUESTIONS.md. Attestation / replay validation is deliberately NOT a decision — it is
tracked as work in **T-041** (post-hackathon by default), so T-009 has no open questions left.

## 3. Schema (migration `supabase/migrations/0001_init.sql`)

Four tables. Full DDL lives in the migration; shape:

- **`profiles`** — `user_id uuid PK → auth.users on delete cascade`, `handle text unique`,
  `rerolls_left smallint default 3`, `recovery_key_hash bytea`, `xp integer default 0`,
  timestamps. Created by an `after insert on auth.users` trigger (security definer
  `generate_handle()`: adjective×animal×number ≈ 405K combos, retry loop on collision, numbers
  widen 1–999 after 5 attempts). Profile exists before the client's first read — no race.
- **`runs`** — full personal history. `client_run_id uuid` + `UNIQUE(user_id, client_run_id)`
  makes submits idempotent/retry-safe. `run_date` is **server-derived from the seed** for daily
  runs; `input_group` is a **generated column** (`camera` → `'camera'`, else `'standard'`) so
  the fallback rule is enforced in the database, not trusted from the client. `flagged bool`
  keeps implausible submissions out of boards without punishing honest clients.
  `platform ∈ (android_gp, android_galaxy, ios)`; `mode ∈ (daily, free)`;
  `input_mode ∈ (tilt, camera, camera_fallback)`.
- **`board_scores`** — one row per user per board:
  `PK (scope, run_date, content_version, world_id, input_group, user_id)`; `scope ∈ (daily,
  alltime)` with `run_date = 'epoch'` for alltime. Conditional upsert
  (`do update … where excluded.score > board_scores.score`) is the anti-row-spam measure and
  makes top-N/rank tiny index scans. No materialized views.
- **`game_config`** — per `content_version`: `max_speed_mps`, `max_coins_per_s`,
  `min/max_duration_s`, allow-listed `worlds[]`. Plausibility bounds tunable without redeploy.

Budget: ~320 B per run all-in → 500 MB ≈ 1.4M runs. Monthly prune (Free runs older than 90 days
beyond each user's most recent 50; daily runs referenced by boards kept via FK) + optional prune
of zero-run anonymous users older than 90 days.

## 4. Access surface (RLS)

- **Zero direct writes from the anon key.** RLS on everything, no insert/update/delete policies,
  plus explicit `revoke insert, update, delete … from anon, authenticated`. All writes go
  through Edge Functions (service role after JWT verification) or `security definer` RPCs keyed
  on `auth.uid()`.
- Reads: own profile + own runs via PostgREST (`user_id = auth.uid()` select policies).
  Leaderboards **only** via `security definer` RPCs `get_leaderboard(scope, run_date,
  content_version, world_id, input_group, limit)` and `get_my_rank(...)` — granted to
  `authenticated` only, expose `handle + score + rank` (never user_id), hard-cap LIMIT 100
  server-side, return a JSON **object** (`{"entries":[…]}`) because JsonUtility cannot parse
  top-level arrays. Every response shape in this system is a flat JSON object for that reason.
- Anonymous sign-up abuse: `config.toml` per-IP rate limit on anonymous sign-ins (20/hr).
- **Column-level SELECT on `profiles`** (0004): authenticated may read only
  `user_id, handle, rerolls_left, xp, created_at` — `select=*` and `recovery_key_hash` are
  denied outright; clients never see the hash, even their own.
- **Default privileges revoked** (0004): Supabase auto-grants new public tables/functions to
  anon/authenticated, which would silently undo the revokes for anything a later migration
  adds. `ALTER DEFAULT PRIVILEGES` now denies writes/execute by default — every future object
  gets an explicit grant or stays backend-only.
- Read-path note: the Auth signup rate limit does NOT cover Data API/RPC/Edge Function calls;
  read abuse is bounded by `authenticated`-only grants + server-side LIMIT caps and accepted
  at this scale (see §5 trade-offs).

## 5. Edge Functions (all `verify_jwt = true` except noted)

**`submit-run`** — since the 17 Sep hardening (migration `0004_hardening.sql`) a thin shell:
JWT → user_id, 4 KB body cap, strict schema/enum/length/numeric-bound validation, then ONE call
to the transactional **`process_run()`** RPC (service-role only), which owns everything stateful
under a per-user advisory lock:
- **Idempotent replay first**, before any quota — a retry of a completed request succeeds even
  after the quota fills; the same `client_run_id` with a *different* payload is a 409, never a
  silent answer for a different run. Duplicates never re-grant XP or re-touch boards.
- Allowlist; `run_date` derived from the seed (`yyyyMMdd` ∈ {today, yesterday} UTC), never the
  client label.
- **Quotas at submission time counting EVERY stored attempt** (flagged and yesterday-seed runs
  included): min-interval ≥ `duration×0.5` since the last stored run; ≤ 30 stored runs/24 h;
  ≤ 10 flagged runs/24 h (garbage floods cap out); the user's >30-day flagged debris is pruned
  opportunistically. All race-free under the lock.
- Plausibility from `game_config` (duration window, `distance ≤ max_speed×duration×1.10+50`,
  coin rate, the exact closed-form coin-economy score ceiling, `best_combo ≤ coins`) — soft
  failure stores `flagged=true`, no board, no XP, HTTP 200 `{"accepted":false}`.
- Board upserts (daily + alltime) + XP grant **in the same transaction** as the insert: a
  downstream failure rolls the whole submission back; a database error anywhere fails CLOSED
  (500), never "accepted".

**`recover-session`** (`verify_jwt = true` — the caller is the *new* anonymous user): no session
minting, no custom JWT crypto. Body `{recovery_key}` (64 hex); the function hashes it and calls
the transactional **`recover_profile()`** RPC: attempt throttling (5/h/caller) atomic under a
per-caller lock; hash validated with the source profile row LOCKED (`for update`), so two
concurrent claims of one key serialize and exactly one wins; **the destination must be a fresh
profile** (no runs, no XP, untouched rerolls) — a recovery key can never silently destroy a
played profile (`destination_not_empty`, 409; merging is an unmade owner decision); transfer +
key rotation happen in the same transaction (handle, XP, runs and board rows follow via
`ON UPDATE CASCADE`). Migration `0007` also queues the old RevenueCat UUID in that same
transaction. The Edge Function requests provider deletion before removing the old auth user;
failed work stays in the service-only `provider_cleanup` table across further recoveries and
account deletion. Every recovery/deletion retries up to eight jobs. Purchases remain restorable
from the store receipt; a device purchase/restore pass is required before release. Unknown key → uniform
`{recovered:false}`. Keys are issued by **`rotate_recovery_key()`** (authenticated): always
rotates and returns a fresh key for the caller — **retry-safe by design**: a lost response just
means the next call issues another valid key, and the superseded key stops working (old
credentials are never accepted indefinitely). Rotating your own key is exactly as privileged as
holding your session.

**`delete-account`** — migration `0008` adds service-only `delete_profile(user_id from JWT)`.
The RPC takes the same per-user `veyro-run` lock as recovery/submission, deletes the profile
(and cascaded runs/boards), and durably queues auth/provider cleanup in one transaction.
The endpoint then removes the auth user and retries provider cleanup. If recovery already
transferred the profile, deletion returns 409 rather than claiming that the moved data was
removed; delete from the recovered session. A retired destination cannot receive a recovery.
Client success drops the local session, active and archived recovery codes, and logs out of
RevenueCat. Commerce waits for identity settlement, and holds that identity through entitlement
application and its completion callback; an identity wait times out after 15 seconds with a
retryable error before any purchase/restore is sent.

**19 Sep review deployment:** apply `0007_review_corrections.sql` and `0008_latest_review.sql` before deploying both
`recover-session` and `delete-account` with their shared provider-cleanup helper. Set the existing
`RC_API_KEY` secret before testing. Cleanup uses RevenueCat's retry-safe DELETE semantics
(HTTP 200 or 404); errors remain pending instead of losing the provider UUID. There is no
scheduled provider worker: on a quiet project, the owner retries with
`deno run --allow-env --allow-net supabase/scripts/retry-provider-cleanup.ts`, supplying
`SUPABASE_URL`, `SUPABASE_SERVICE_ROLE_KEY`, and `RC_API_KEY` through the environment.
Check this queue during deletion support and before the documented 30-day support deadline.
Previously orphaned provider IDs from recoveries before `0007` cannot be reconstructed by
this migration; include them in the existing pre-tester dashboard/test-data cleanup.

Handle reroll and profile creation are **not** Edge Functions: `security definer` RPC + auth
trigger (same security, zero function invocations).

Accepted trade-off — stated plainly: **the leaderboard is not cheat-proof.** Score, duration and
input mode are client claims; plausibility bounds reject the impossible, not the fabricated, and
server-side XP derives from those claims (tracked as T-041). Designed-in future mitigations, no
schema break: `replay_trace bytea` column later (deterministic seeds → server re-simulation of
top-N suspects), Play Integrity / App Attest verdict check inside `submit-run`. CAPTCHA on
anonymous signup is deliberately off (OPEN_QUESTIONS 17): Supabase enforces it at the Auth
endpoint, and the Unity client cannot render a challenge without a WebView dependency — enabling
it in the dashboard would break sign-in for shipped clients. Read-path abuse (Data API/RPC calls
are not covered by the Auth signup rate limit) is bounded only by `authenticated`-only grants and
the LIMIT caps — accepted at Shipaton scale.

Storage rationale (recovery key + refresh token stay in plaintext device storage BY DESIGN): the
key must survive reinstall via device backup, and Android Keystore-backed encryption keys do
**not** survive reinstall — hardware-bound encryption would break exactly the property the key
exists for. A local attacker who can read the key file can equally read the PlayerPrefs session,
which is the same privilege level. The key file is tagged `<userId>:<key>` so stale keys
(rotated away, pre-claim leftovers, lost responses) are detected and replaced by
`rotate_recovery_key` on the next boot. iOS keeps the same file for now; the Keychain is
deferred (see "iOS storage" at the end).

## 6. Unity seam (mirrors Commerce exactly)

- **`game/Assets/Scripts/Social/MotionRunner.Social.asmdef`** — engine-free
  (`noEngineReferences: true`): `IProfileService` (callback-shaped like `IStore`: `IsReady`,
  `CurrentProfile`, `SubmitRun`, `FetchLeaderboard`, `FetchMyRuns`, `RerollHandle`,
  `DeleteAccount`, `event ProfileChanged`), flat `[Serializable]` DTOs (JsonUtility), board
  bucketing rules, pending-queue policy, `FakeProfileService`. Fail-open throughout: no
  network / not ready → local-only game, nothing blocks, nothing throws (rule 3).
- **`Social/Supabase/MotionRunner.Social.Supabase.asmdef`** — the only assembly that knows HTTP:
  `SupabaseProfileService` over `UnityWebRequest` + `JsonUtility` (both modules already in the
  manifest — **no new UPM package**, so gotcha 10 exposure is our own code only),
  `SupabaseKeys.cs` (URL + anon key, committed — public by design, same rationale as
  RevenueCatKeys), session + recovery-key storage (decision logic engine-free, storage calls in
  the adapter, GuideState pattern).
- **Run-end plumbing** (post main-menu merge; re-verify line sites then): `PauseState` gains
  `StartedInCameraMode`/`CameraWasDropped` (today `DropCameraMode()`'s return is discarded in
  `RunFlow` — the degraded-run fact is currently unobservable); `RunSummary` gains input mode,
  seed triple, duration; `RunSession.Crash()` hands the record to `SubmitRun` fire-and-forget
  with an offline queue (encoded like `RunHistory`, capped, `veyro.runs.pending`).
- **`GameBootstrap`**: `CreateProfileService()` beside `CreateStore()` — `FakeProfileService`
  in Editor/keyless builds; wire `store.Identify(profile.UserId)` when both are ready.
- **Joining the board is automatic** (owner call, 17 Sep, reversing the 3 Sep opt-in gate):
  every finished run submits once a session exists. The privacy story moves entirely onto the
  policy/support copy (§8), which already describes exactly this — profile created on first
  online use, run results stored to show shared leaderboards, delete any time. The Play Data
  safety answers are unchanged (scores were declared collected+shared either way).

## 7. UI + repo infra

- Builds on the **feat/main-menu rework** (prerequisite: that work must be committed; this
  branch then fast-forwards onto it). `SupabaseLeaderboardSource : ILeaderboardSource`
  (seam extended with `Refresh()` + `event Changed`; `Top()` stays synchronous over a cache;
  `IsLive` flips true when server rows land). `ProfilePage`: handle + reroll on the player card,
  Daily/All-time × Standard/Camera tabs + my-rank on the board card, delete-profile
  (two-tap confirm) on the links card. `ProgressStore`/`RunHistory` extend for
  the pending queue (their decoders tolerate added fields).
- **`supabase/`** in repo root: `config.toml`, `migrations/`, `functions/` — config-as-code,
  deployed only by the owner (`supabase db push` / `supabase functions deploy`).
- **`infra/keepalive-worker/`**: Cloudflare Worker, cron `0 3 */3 * *`, POSTs
  `rest/v1/rpc/ping` (a `select 1` RPC granted to `anon`) — DB traffic is what resets the
  7-day pause counter; PostgREST calls don't burn the Edge Function quota.
- **Secrets:** repo/client = project URL + anon key only. Owner-only: access token, DB
  password, project ref. Service-role key is platform-injected into Edge Functions — never in
  repo, never in client. Worker secret = anon key.

## 8. Release flips (atomic with the Update-1 binary — STORE_COMPLIANCE has the row)

- Play Data safety: + **App activity → gameplay content/scores = collected** (device/other IDs
  already declared since the T-020 flip). **Account deletion**: in-app deletion + the public
  web deletion URL (`https://veyro.ferrabled.com/support/#delete`) declared in Data safety.
- App access: still no restricted content (no login form; anonymous profile is automatic) —
  update the instruction note only.
- Content rating: **unchanged** (generated handles = no UGC).
- Privacy policy + support page: paste-ready copy below goes into `docs/PRIVACY_POLICY.md` +
  `site/public/privacy|support` **in the release window, not before** (the site currently
  stages the T-020 purchases flip — do not mix; same held-back pattern as LISTING.md §3).
- Galaxy build: same binary/backend, nothing extra. Apple: nutrition labels (identifiers +
  gameplay content); deletion already built (5.1.1(v)).

### Policy copy — WRITTEN INTO THE SITE, 18 Sep (supersedes the held-back drafts)

The full copy now lives in the actual pages — `site/public/privacy/index.html` (new "Player
profile and leaderboards" section, recovery-code paragraph, updated short version and Your
rights), `site/public/support/index.html` (leaderboard FAQ + the `#delete` anchor with in-app
path, Player-ID email fallback, ownership verification, 30-day window), `site/public/terms/`
§2/§6 and the homepage claims — mirrored in `docs/PRIVACY_POLICY.md`. Everything is
**version-scoped** ("the leaderboard update onward"), so the pages stay truthful whenever the
owner deploys; deploy no later than the release window. Two review corrections baked in
(18 Sep, R3): profiles are described as **pseudonymous, deletable records** — never "contain
no personal data" — and the app now shows a copyable **Player ID** on the PROFILE tab so the
email fallback actually works; an ID locates a record, and ownership is demonstrated with the
**player-visible recovery code** (owner call, 18 Sep, replacing the rename idea): the PROFILE
tab exposes it with a tap-to-copy and a warning, and an IMPORT PROFILE row lets the same code
move a profile to a new install manually (backup-off rescue; same recover-session path, fresh
destination required, code rotates on every successful claim).

## 9. Owner checklist (ordered) — connecting the created project (project exists since 9 Sep)

The security split, so nothing lands in the wrong place: the repo/client get ONLY the project
URL and the **anon (publishable)** key — harmless under RLS, like the RevenueCat public keys.
The **service-role key, JWT secret and database password never enter the repo or the game**;
they live in the dashboard and are used implicitly by the CLI/platform. Agents never run these
steps (P11 is human-only).

1. **Paste the coordinates** into
   `game/Assets/Scripts/Social/Supabase/SupabaseKeys.cs`: `Url` =
   `https://<project-ref>.supabase.co`, `AnonKey` = Project Settings → API → anon/public key.
   (Empty values = backend disabled, game fully playable — fail-open.)
2. **Apply the schema + functions** from the repo root (install the CLI with
   `scoop install supabase` or `winget install Supabase.CLI`; login opens a browser, no
   secrets typed into the shell):
   ```
   supabase login
   supabase link --project-ref <project-ref>     # asks for the DB password once, stored locally
   supabase db push                              # applies supabase/migrations/ (schema, RLS, RPCs)
   supabase functions deploy submit-run recover-session delete-account
   ```
3. **Enable anonymous sign-ins**: Dashboard → Authentication → Sign In/Up → "Allow anonymous
   sign-ins" ON (config.toml records the same for local dev; the hosted project needs the
   dashboard toggle). While there, set the anonymous sign-in rate limit
   (Authentication → Rate Limits) to ~20/hour/IP.
4. **Keep-alive Worker** (stops the free-tier 7-day pause): in `infra/keepalive-worker/`, put
   the project URL in wrangler.toml, then `npx wrangler secret put SUPABASE_ANON_KEY` and
   `npx wrangler deploy`.
5. ~~Move OPEN_QUESTIONS 15–16 to DECISIONS.md and approve the §8 deletion copy.~~ **Done
   19 Sep:** D13–D18 recorded, deletion/support copy approved as drafted and live in `site/`.
6. Device test per §10 (`BuildAndroidDev` flavour), then, at release time, the atomic §8 flip.
7. Phase 2 (later): Google OAuth client + SIWA config.

## 10. Verification

EditMode suite green incl. new Social tests (`RunSeedTests` untouched); release-APK
permission/size diff (expect no new permissions — INTERNET exists); device pass: profile
creation, submits (tilt/camera/forced fallback), both board splits render, reroll, offline
queue drains, delete wipes rows in Studio and the game stays fully playable; reinstall test
both ways (backup on → handle+XP+purchases all return in one boot; backup off → fresh profile,
no errors); keep-alive cron visible in Supabase logs.


### 19 Sep latest-review recovery handling

If automatic recovery gets `destination_not_empty`, the client first preserves the foreign
code in `veyro-recovery-support.txt`, then removes it from the active code slot and requests a
code for the current profile. Disk failure leaves the original intact and retries later.
A relaunch never retries an archived claim; failed current-code issuance still retries.
Deleting the current online profile removes both local code files, but does not delete the
separate older profile represented by an archived code. Purchases/restore still need the
licensed device check in §10; iOS run submissions now select the `ios` tag at compile time.

### iOS storage (28 Sep): the Keychain is deferred

**Decision** (owner, 28 Sep planning session; `docs/IOS_HANDOFF.md` decision 3, still to be
moved to DECISIONS.md by the owner): the iOS build stores the recovery key exactly as Android
does, in `Application.persistentDataPath/veyro-recovery.txt` (`<userId>:<key>`, plus
`veyro-recovery-support.txt` for archived codes). The refresh token and the pending-run queue
stay in PlayerPrefs. No Keychain code is written for v1.0.

**Why it is deferred:**
- **The deadline.** The Shipaton submission closes 30 Sep. Keychain storage needs a native plugin
  over `SecItem*` and a new storage seam; today `SupabaseProfileService` reads and writes the
  file directly.
- **It cannot be verified from here.** There is no Mac and no iPhone. Every native fix costs a
  Windows re-export, a cloud-Mac archive and a TestFlight round trip.
- **The riskiest invariant depends on the file.** Review R1 derives the pending-claim state from
  the key file itself ("the file IS the state"). A second backend needs a migration that keeps
  that invariant, and that is the most delicate logic in this system.
- **The benefit is soft.** A Keychain item outliving an app deletion is de facto behaviour, not
  a documented contract.
- **The rescue paths already exist:** COPY on the RECOVERY CODE row → IMPORT PROFILE on the new
  install, and RESTORE PURCHASES from App Store receipts.

**What an iPhone player actually gets:**

| Event | Recovery file + PlayerPrefs session | Next launch |
|---|---|---|
| Delete the app, reinstall | Deleted with the app container | Fresh anonymous profile. IMPORT PROFILE with a copied code brings the old one back, provided the fresh profile is still untouched: no run played (`destination_not_empty`, §5) |
| Offload App (Settings → iPhone Storage), reinstall | Kept: offloading keeps documents and data | Same profile |
| New or reset iPhone restored from an iCloud/computer backup | Restored: `Documents/` and PlayerPrefs are in the backup, and the game sets no exclude-from-backup flag | Same profile through the §1 boot order (a stale refresh token falls through to the key), unless the code was rotated after that backup |
| Purchases, in every row | Held by the App Store, not the device | RESTORE PURCHASES; RevenueCat moves them to the current app user ID |

- IMPORT PROFILE reads the clipboard in code (`ProfilePage.ImportTapped`). On iOS 16+, reading
  text that was copied in another app (Notes, Messages) shows Apple's "Allow Paste" prompt,
  unless the player set Settings → Veyro Run → Paste from Other Apps to Allow. "Don't Allow"
  reads as empty, and the row shows "import failed: a recovery code is 64 characters". This is
  harmless, but the device pass (IOS_HANDOFF §9 item 9) should expect the prompt.
- **Policy wording:** the privacy policy's "survives uninstalling the app" paragraph is true only
  of Android. The iPhone wording was added to `docs/PRIVACY_POLICY.md` ("Data stored on your
  device") and mirrored into `site/public/privacy/index.html` on 28 Sep. **The owner still has
  to deploy it.** The support page's "Will my profile survive a reinstall?" answer is still
  Android-only (see STATUS 28 Sep, Phase 2c).
- **If this is revisited (post-Shipaton):** add the storage seam first (the decision logic is
  already engine-free in `RecoveryGate`). Then add a Keychain adapter behind `#if UNITY_IOS`,
  with a one-time file → Keychain migration that never deletes the file before the Keychain write
  is confirmed (R1). Phase 2 identity linking (Sign in with Apple) may make it unnecessary.
