-- Security/reliability hardening (17 Sep review). Four themes:
--   1. submit-run becomes ONE transactional RPC (process_run): per-user advisory lock,
--      quotas that count every stored attempt at SUBMISSION time (flagged included,
--      yesterday's seeds included), idempotent replay with payload matching, board upserts
--      + XP in the same transaction, opportunistic pruning of flagged debris.
--   2. recovery becomes ONE transactional RPC (recover_profile): hash validated under row
--      lock, destination must be a fresh profile, key rotates atomically, attempts throttled
--      atomically. issue_recovery_key is replaced by rotate_recovery_key (retry-safe: a lost
--      response just means the next call issues another valid key).
--   3. least privilege hardened: column-level SELECT on profiles (clients never see
--      recovery_key_hash), default privileges revoked so FUTURE tables/functions are not
--      auto-granted to anon/authenticated, superseded helpers dropped.
--   4. recovery_attempts gains an FK so account deletion cascades operational records.

-- ============================================================ 1. transactional submit

create or replace function public.process_run(
  p_user uuid,
  p_client_run_id uuid,
  p_mode text,
  p_seed integer,
  p_content_version text,
  p_world_id text,
  p_day_label text,
  p_input_mode text,
  p_score integer,
  p_distance_m real,
  p_coins integer,
  p_best_combo integer,
  p_duration_s real,
  p_app_version text,
  p_platform text)
returns json
language plpgsql
security definer
set search_path = public, extensions
as $$
declare
  cfg public.game_config%rowtype;
  existing public.runs%rowtype;
  run_day date;
  today date := (now() at time zone 'utc')::date;
  is_stale boolean := false;
  reason text := null;
  is_flagged boolean;
  max_coin_points integer;
  full_groups integer;
  remainder integer;
  last_created timestamptz;
  attempts_24h integer;
  flagged_24h integer;
  new_run_id bigint;
  new_group text;
  daily_rank integer := 0;
  alltime_rank integer := 0;
begin
  -- One submission per user at a time: every check below is race-free against this lock.
  perform pg_advisory_xact_lock(hashtext('veyro-run'), hashtext(p_user::text));

  -- Idempotent replay FIRST, before any quota: a retry of a completed request must succeed
  -- even if the quota has since filled. Same key + different payload is a client bug or an
  -- attack — reject it rather than silently answering for a different run.
  select * into existing from public.runs
    where user_id = p_user and client_run_id = p_client_run_id;
  if found then
    if existing.seed <> p_seed or existing.score <> p_score or existing.mode <> p_mode then
      return json_build_object('status', 'mismatch', 'accepted', false, 'duplicate', true,
        'reason', 'client_run_id reused with a different payload',
        'daily_rank', 0, 'alltime_rank', 0);
    end if;
    return json_build_object('status', 'ok', 'accepted', not existing.flagged,
      'duplicate', true, 'reason', '', 'daily_rank', 0, 'alltime_rank', 0);
  end if;

  -- Allowlist.
  select * into cfg from public.game_config where content_version = p_content_version;
  if not found then
    return json_build_object('status', 'unknown_content', 'accepted', false,
      'duplicate', false, 'reason', 'unknown content_version',
      'daily_rank', 0, 'alltime_rank', 0);
  end if;
  if not (p_world_id = any (cfg.worlds)) then
    return json_build_object('status', 'unknown_world', 'accepted', false,
      'duplicate', false, 'reason', 'unknown world_id',
      'daily_rank', 0, 'alltime_rank', 0);
  end if;

  -- run_date is server-derived. Daily: from the seed (yyyyMMdd), must be a real date and
  -- current (today or yesterday UTC — a run finishing just after midnight). Free: today.
  if p_mode = 'daily' then
    begin
      run_day := make_date(p_seed / 10000, (p_seed % 10000) / 100, p_seed % 100);
    exception when others then
      return json_build_object('status', 'bad_seed', 'accepted', false,
        'duplicate', false, 'reason', 'daily seed is not a date',
        'daily_rank', 0, 'alltime_rank', 0);
    end;
    is_stale := run_day <> today and run_day <> today - 1;
  else
    run_day := today;
  end if;

  -- Quotas at SUBMISSION time, counting EVERY stored attempt (flagged included — a client
  -- that only produces flagged runs must not get unmetered storage), independent of which
  -- leaderboard date the run belongs to.
  select max(created_at) into last_created from public.runs where user_id = p_user;
  if last_created is not null
     and now() - last_created < make_interval(secs => greatest(p_duration_s * 0.5, 2)) then
    return json_build_object('status', 'too_fast', 'accepted', false, 'duplicate', false,
      'reason', 'submitted faster than the run could be played',
      'daily_rank', 0, 'alltime_rank', 0);
  end if;

  select count(*) into attempts_24h from public.runs
    where user_id = p_user and created_at > now() - interval '24 hours';
  if attempts_24h >= 30 then
    return json_build_object('status', 'daily_limit', 'accepted', false, 'duplicate', false,
      'reason', 'daily submission limit reached', 'daily_rank', 0, 'alltime_rank', 0);
  end if;

  -- Plausibility (soft: stored flagged, never boarded, no XP).
  if is_stale then
    reason := 'daily seed is not current';
  elsif p_duration_s < cfg.min_duration_s then
    reason := 'duration below minimum';
  elsif p_duration_s > cfg.max_duration_s then
    reason := 'duration above maximum';
  elsif p_distance_m > cfg.max_speed_mps * p_duration_s * 1.1 + 50 then
    reason := 'distance implausible for duration';
  elsif p_coins > cfg.max_coins_per_s * p_duration_s + 10 then
    reason := 'coins implausible for duration';
  elsif p_score < floor(p_distance_m) then
    reason := 'score below distance floor';
  elsif p_best_combo > p_coins then
    reason := 'combo exceeds coins';
  else
    -- Deterministic coin-economy ceiling (ScoreState): a coin is 10 x a multiplier that
    -- steps every 3 coins, capped x5. Closed form, no loop.
    if p_coins <= 12 then
      full_groups := p_coins / 3;
      remainder := p_coins % 3;
      max_coin_points := 10 * (3 * (full_groups * (full_groups + 1) / 2)
                               + remainder * (full_groups + 1));
    else
      max_coin_points := 300 + (p_coins - 12) * 50;
    end if;
    if p_score > floor(p_distance_m) + max_coin_points then
      reason := 'score above coin-economy ceiling';
    end if;
  end if;
  is_flagged := reason is not null;

  -- Flagged storage is itself capped: keep it useful for tuning, useless for flooding.
  if is_flagged then
    select count(*) into flagged_24h from public.runs
      where user_id = p_user and flagged and created_at > now() - interval '24 hours';
    if flagged_24h >= 10 then
      return json_build_object('status', 'flagged_limit', 'accepted', false,
        'duplicate', false, 'reason', reason, 'daily_rank', 0, 'alltime_rank', 0);
    end if;
    -- Opportunistic prune: this user's flagged rows older than 30 days are pure debris.
    delete from public.runs
      where user_id = p_user and flagged and created_at < now() - interval '30 days';
  end if;

  insert into public.runs
    (user_id, client_run_id, mode, seed, content_version, world_id, run_date, input_mode,
     score, distance_m, coins, best_combo, duration_s, app_version, platform, flagged)
  values
    (p_user, p_client_run_id, p_mode, p_seed, p_content_version, p_world_id, run_day,
     p_input_mode, p_score, p_distance_m, p_coins, p_best_combo, p_duration_s,
     p_app_version, p_platform, is_flagged)
  returning id, input_group into new_run_id, new_group;

  if is_flagged then
    return json_build_object('status', 'flagged', 'accepted', false, 'duplicate', false,
      'reason', reason, 'daily_rank', 0, 'alltime_rank', 0);
  end if;

  -- Boards + XP, same transaction as the insert: a failure rolls everything back and the
  -- client's retry (same client_run_id) reprocesses cleanly instead of double-granting.
  if p_mode = 'daily' then
    insert into public.board_scores
      (scope, run_date, content_version, world_id, input_group, user_id, score, run_id)
    values ('daily', run_day, p_content_version, p_world_id, new_group, p_user, p_score, new_run_id)
    on conflict (scope, run_date, content_version, world_id, input_group, user_id)
    do update set score = excluded.score, run_id = excluded.run_id, achieved_at = now()
    where excluded.score > public.board_scores.score;

    insert into public.board_scores
      (scope, run_date, content_version, world_id, input_group, user_id, score, run_id)
    values ('alltime', 'epoch'::date, p_content_version, p_world_id, new_group, p_user, p_score, new_run_id)
    on conflict (scope, run_date, content_version, world_id, input_group, user_id)
    do update set score = excluded.score, run_id = excluded.run_id, achieved_at = now()
    where excluded.score > public.board_scores.score;

    update public.profiles
      set xp = xp + least(p_score / 100, 500), updated_at = now()
      where user_id = p_user;

    select 1 + count(*) into daily_rank from public.board_scores b
      where b.scope = 'daily' and b.run_date = run_day
        and b.content_version = p_content_version and b.world_id = p_world_id
        and b.input_group = new_group and b.score > p_score;
    select 1 + count(*) into alltime_rank from public.board_scores b
      where b.scope = 'alltime' and b.run_date = 'epoch'::date
        and b.content_version = p_content_version and b.world_id = p_world_id
        and b.input_group = new_group and b.score > p_score;
  end if;

  return json_build_object('status', 'ok', 'accepted', true, 'duplicate', false,
    'reason', '', 'daily_rank', daily_rank, 'alltime_rank', alltime_rank);
end;
$$;

-- ============================================================ 2. transactional recovery

alter table public.recovery_attempts
  add constraint recovery_attempts_user_fk
  foreign key (user_id) references auth.users (id) on delete cascade;

create or replace function public.recover_profile(
  p_new_user uuid,
  p_key_hash bytea)
returns json
language plpgsql
security definer
set search_path = public, extensions
as $$
declare
  old_user uuid;
  attempts_hour integer;
  raw bytea;
begin
  -- Attempt throttling is atomic under the same per-caller lock that serializes the claim.
  perform pg_advisory_xact_lock(hashtext('veyro-recover'), hashtext(p_new_user::text));

  delete from public.recovery_attempts
    where user_id = p_new_user and attempted_at < now() - interval '1 day';
  select count(*) into attempts_hour from public.recovery_attempts
    where user_id = p_new_user and attempted_at > now() - interval '1 hour';
  if attempts_hour >= 5 then
    return json_build_object('status', 'throttled', 'recovered', false,
      'recovery_key', '', 'old_user', null);
  end if;
  insert into public.recovery_attempts (user_id) values (p_new_user);

  -- Hash validated and the source row LOCKED in one statement: two concurrent claims of the
  -- same key serialize here, and the second finds the hash already rotated away.
  select user_id into old_user from public.profiles
    where recovery_key_hash = p_key_hash
    for update;
  if not found then
    return json_build_object('status', 'not_found', 'recovered', false,
      'recovery_key', '', 'old_user', null);
  end if;

  if old_user = p_new_user then
    -- Backup restored onto a device that never lost the session; nothing to move and the
    -- presented key remains the current one.
    return json_build_object('status', 'ok', 'recovered', true,
      'recovery_key', '', 'old_user', null);
  end if;

  -- The destination must be a FRESH profile (the reinstall case). A profile that has played
  -- or spent anything is never silently destroyed — that would let a recovery key erase a
  -- real player. No merge policy exists (owner decision if ever wanted).
  if exists (select 1 from public.runs where user_id = p_new_user)
     or exists (select 1 from public.profiles
                where user_id = p_new_user and (xp > 0 or rerolls_left < 3)) then
    return json_build_object('status', 'destination_not_empty', 'recovered', false,
      'recovery_key', '', 'old_user', null);
  end if;

  -- Atomic transfer + rotation: the old key dies with the claim, so a leaked pre-claim key
  -- is worthless afterwards.
  raw := extensions.gen_random_bytes(32);
  delete from public.profiles where user_id = p_new_user;
  update public.profiles
    set user_id = p_new_user,
        recovery_key_hash = extensions.digest(raw, 'sha256'),
        updated_at = now()
    where user_id = old_user;

  return json_build_object('status', 'ok', 'recovered', true,
    'recovery_key', encode(raw, 'hex'), 'old_user', old_user);
end;
$$;

-- Retry-safe key issuance replaces the one-shot issue_recovery_key: rotating your OWN key is
-- exactly as privileged as holding your session, and a lost response simply means the next
-- call returns another valid key (the previous one stops working — old credentials are not
-- accepted indefinitely).
create or replace function public.rotate_recovery_key()
returns json
language plpgsql
security definer
set search_path = public, extensions
as $$
declare
  raw bytea;
begin
  raw := extensions.gen_random_bytes(32);
  update public.profiles
    set recovery_key_hash = extensions.digest(raw, 'sha256'),
        updated_at = now()
    where user_id = auth.uid();
  if not found then
    return json_build_object('recovery_key', '');
  end if;
  return json_build_object('recovery_key', encode(raw, 'hex'));
end;
$$;

-- ============================================================ 3. least privilege

-- Superseded helpers go away entirely (process_run/recover_profile replace them).
drop function if exists public.upsert_board_score(text, date, text, text, text, uuid, int, bigint);
drop function if exists public.grant_xp(uuid, int);
drop function if exists public.claim_profile(uuid, uuid, bytea);
drop function if exists public.issue_recovery_key();

-- Clients never see recovery_key_hash: column-level SELECT on top of the RLS row filter.
revoke select on public.profiles from anon, authenticated;
grant select (user_id, handle, rerolls_left, xp, created_at)
  on public.profiles to authenticated;

-- FUTURE objects must not inherit grants: Supabase's default privileges auto-grant new
-- tables/functions in public to anon/authenticated, which would silently undo the 0001
-- revokes for anything a later migration adds.
alter default privileges in schema public
  revoke insert, update, delete, truncate, references, trigger on tables from anon, authenticated;
alter default privileges in schema public
  revoke select on tables from anon;
alter default privileges in schema public
  revoke execute on functions from anon, authenticated, public;
alter default privileges in schema public
  revoke usage, select, update on sequences from anon, authenticated;

-- Explicit grants for the current API surface (unchanged behaviour, now explicit):
revoke execute on all functions in schema public from public, anon, authenticated;
grant execute on function public.get_leaderboard(text, date, text, text, text, int) to authenticated;
grant execute on function public.get_my_rank(text, date, text, text, text)          to authenticated;
grant execute on function public.reroll_handle()                                    to authenticated;
grant execute on function public.rotate_recovery_key()                              to authenticated;
grant execute on function public.ping()                                             to anon, authenticated;
-- process_run / recover_profile / generate_handle / handle_new_user: service-role only.
