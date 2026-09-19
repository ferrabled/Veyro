-- Fixes from the 18 Sep independent review (docs/T009_SECURITY_PRIVACY_REVIEW.md):
--   R6: 0004's default-privilege revokes were per-schema only — PostgreSQL's GLOBAL implicit
--       PUBLIC EXECUTE on new functions and authenticated's default SELECT on new tables
--       survived. Fixed globally for the migration role, with a self-test.
--   R7: unlimited rerolls (0005) gain a burst throttle; recovery-key rotation too.
--   idempotency: duplicate detection now compares the FULL payload and returns the original
--       outcome including the stored flag reason (new runs.flag_reason column) and fresh ranks.
--   race: recover_profile now takes the same per-user submission locks process_run uses (both
--       users, sorted order, BEFORE the profiles row lock) so a claim cannot interleave with a
--       submission — and the fresh-destination check no longer leans on rerolls_left, which
--       0005 froze at 3.

-- ============================================================ R6: default privileges

-- Global (no IN SCHEMA): kills the implicit PUBLIC EXECUTE on future functions and any
-- default grants to the API roles on future tables/sequences created by the migration role.
alter default privileges revoke execute on functions from public, anon, authenticated;
alter default privileges revoke select, insert, update, delete, truncate, references, trigger
  on tables from anon, authenticated;
alter default privileges revoke usage, select, update on sequences from anon, authenticated;

-- Close 0004's per-schema gaps too (it revoked writes but not authenticated's SELECT,
-- and functions only from anon/authenticated, not PUBLIC).
alter default privileges in schema public revoke select on tables from authenticated;
alter default privileges in schema public revoke execute on functions from public;

-- Self-test with disposable objects: a future table/function must be born unreachable.
do $$
begin
  create table public._priv_probe (id int);
  create function public._priv_probe_fn() returns int language sql as 'select 1';

  if has_table_privilege('anon', 'public._priv_probe', 'select')
     or has_table_privilege('authenticated', 'public._priv_probe', 'select')
     or has_table_privilege('authenticated', 'public._priv_probe', 'insert') then
    raise exception 'default table privileges still leak to API roles';
  end if;
  if has_function_privilege('anon', 'public._priv_probe_fn()', 'execute')
     or has_function_privilege('authenticated', 'public._priv_probe_fn()', 'execute') then
    raise exception 'default function privileges still leak to API roles';
  end if;

  drop function public._priv_probe_fn();
  drop table public._priv_probe;
end;
$$;

-- ============================================================ idempotency + flag reason

alter table public.runs add column if not exists flag_reason text;

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
  -- recover_profile takes the SAME lock for both sides of a claim, so a submission can
  -- never interleave with a profile transfer.
  perform pg_advisory_xact_lock(hashtext('veyro-run'), hashtext(p_user::text));

  -- Idempotent replay FIRST, before any quota: a retry of a completed request must succeed
  -- even if the quota has since filled. The FULL payload must match — any drifted field is a
  -- client bug or an attack, answered with 'mismatch' rather than a reply about another run.
  select * into existing from public.runs
    where user_id = p_user and client_run_id = p_client_run_id;
  if found then
    if existing.seed <> p_seed
       or existing.mode <> p_mode
       or existing.content_version <> p_content_version
       or existing.world_id <> p_world_id
       or existing.input_mode <> p_input_mode
       or existing.score <> p_score
       or existing.coins <> p_coins
       or existing.best_combo <> p_best_combo
       or abs(existing.distance_m - p_distance_m) > 0.01
       or abs(existing.duration_s - p_duration_s) > 0.01 then
      return json_build_object('status', 'mismatch', 'accepted', false, 'duplicate', true,
        'reason', 'client_run_id reused with a different payload',
        'daily_rank', 0, 'alltime_rank', 0);
    end if;
    -- Original outcome, restated: stored flag reason, and current ranks for accepted dailies.
    if not existing.flagged and existing.mode = 'daily' then
      select 1 + count(*) into daily_rank from public.board_scores b
        where b.scope = 'daily' and b.run_date = existing.run_date
          and b.content_version = existing.content_version and b.world_id = existing.world_id
          and b.input_group = existing.input_group and b.score > existing.score;
      select 1 + count(*) into alltime_rank from public.board_scores b
        where b.scope = 'alltime' and b.run_date = 'epoch'::date
          and b.content_version = existing.content_version and b.world_id = existing.world_id
          and b.input_group = existing.input_group and b.score > existing.score;
    end if;
    return json_build_object('status', 'ok', 'accepted', not existing.flagged,
      'duplicate', true, 'reason', coalesce(existing.flag_reason, ''),
      'daily_rank', daily_rank, 'alltime_rank', alltime_rank);
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
  -- current. Free: today — Free seeds are an arbitrary signed int by contract (R8) and get
  -- no date interpretation at all.
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

  -- Quotas at SUBMISSION time, counting EVERY stored attempt.
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

  if is_flagged then
    select count(*) into flagged_24h from public.runs
      where user_id = p_user and flagged and created_at > now() - interval '24 hours';
    if flagged_24h >= 10 then
      return json_build_object('status', 'flagged_limit', 'accepted', false,
        'duplicate', false, 'reason', reason, 'daily_rank', 0, 'alltime_rank', 0);
    end if;
    delete from public.runs
      where user_id = p_user and flagged and created_at < now() - interval '30 days';
  end if;

  insert into public.runs
    (user_id, client_run_id, mode, seed, content_version, world_id, run_date, input_mode,
     score, distance_m, coins, best_combo, duration_s, app_version, platform, flagged,
     flag_reason)
  values
    (p_user, p_client_run_id, p_mode, p_seed, p_content_version, p_world_id, run_day,
     p_input_mode, p_score, p_distance_m, p_coins, p_best_combo, p_duration_s,
     p_app_version, p_platform, is_flagged, reason)
  returning id, input_group into new_run_id, new_group;

  if is_flagged then
    return json_build_object('status', 'flagged', 'accepted', false, 'duplicate', false,
      'reason', reason, 'daily_rank', 0, 'alltime_rank', 0);
  end if;

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

-- ============================================================ recovery: lock ordering + check

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
  verified uuid;
  attempts_hour integer;
  raw bytea;
  lock_a uuid;
  lock_b uuid;
begin
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

  -- Peek WITHOUT locking to learn who the key belongs to, take the submission locks for
  -- BOTH users in sorted order (same 'veyro-run' namespace process_run uses — a submission
  -- and a claim involving the same player serialize instead of deadlocking or interleaving),
  -- then re-verify the hash under the row lock: a claim that rotated the key between the
  -- peek and the lock is caught here.
  select user_id into old_user from public.profiles where recovery_key_hash = p_key_hash;
  if not found then
    return json_build_object('status', 'not_found', 'recovered', false,
      'recovery_key', '', 'old_user', null);
  end if;

  if old_user = p_new_user then
    return json_build_object('status', 'ok', 'recovered', true,
      'recovery_key', '', 'old_user', null);
  end if;

  lock_a := least(old_user, p_new_user);
  lock_b := greatest(old_user, p_new_user);
  perform pg_advisory_xact_lock(hashtext('veyro-run'), hashtext(lock_a::text));
  perform pg_advisory_xact_lock(hashtext('veyro-run'), hashtext(lock_b::text));

  select user_id into verified from public.profiles
    where user_id = old_user and recovery_key_hash = p_key_hash
    for update;
  if not found then
    return json_build_object('status', 'not_found', 'recovered', false,
      'recovery_key', '', 'old_user', null);
  end if;

  -- The destination must be a FRESH profile: no runs, no XP. (rerolls_left is no longer a
  -- signal — 0005 froze it — and a destination that has merely renamed itself holds nothing
  -- worth keeping.) Checked under both submission locks, so a racing submission for the
  -- destination either landed before this check (and blocks the claim) or waits behind it.
  if exists (select 1 from public.runs where user_id = p_new_user)
     or exists (select 1 from public.profiles where user_id = p_new_user and xp > 0) then
    return json_build_object('status', 'destination_not_empty', 'recovered', false,
      'recovery_key', '', 'old_user', null);
  end if;

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

-- ============================================================ R7: burst throttles

alter table public.profiles add column if not exists handle_changed_at timestamptz;
alter table public.profiles add column if not exists recovery_rotated_at timestamptz;

-- Rerolls stay UNLIMITED (owner call) — this only stops automated flooding: one change per
-- 2 seconds is invisible to a person tapping "new name" and a wall to a script.
create or replace function public.reroll_handle()
returns json
language plpgsql
security definer
set search_path = public
as $$
declare
  new_handle text;
  last_change timestamptz;
begin
  select handle_changed_at into last_change
    from public.profiles where user_id = auth.uid() for update;
  if not found then
    raise exception 'no profile' using errcode = 'P0002';
  end if;
  if last_change is not null and now() - last_change < interval '2 seconds' then
    return json_build_object('handle', null, 'rerolls_left', 999, 'throttled', true);
  end if;

  new_handle := public.generate_handle();
  update public.profiles
    set handle = new_handle,
        handle_changed_at = now(),
        updated_at = now()
    where user_id = auth.uid();

  return json_build_object('handle', new_handle, 'rerolls_left', 999, 'throttled', false);
end;
$$;

create or replace function public.rotate_recovery_key()
returns json
language plpgsql
security definer
set search_path = public, extensions
as $$
declare
  raw bytea;
  last_rotate timestamptz;
begin
  select recovery_rotated_at into last_rotate
    from public.profiles where user_id = auth.uid() for update;
  if not found then
    return json_build_object('recovery_key', '');
  end if;
  if last_rotate is not null and now() - last_rotate < interval '2 seconds' then
    return json_build_object('recovery_key', '');
  end if;

  raw := extensions.gen_random_bytes(32);
  update public.profiles
    set recovery_key_hash = extensions.digest(raw, 'sha256'),
        recovery_rotated_at = now(),
        updated_at = now()
    where user_id = auth.uid();
  return json_build_object('recovery_key', encode(raw, 'hex'));
end;
$$;

-- Re-pin the grant surface after the CREATE OR REPLACEs (defaults no longer grant anything).
revoke execute on all functions in schema public from public, anon, authenticated;
grant execute on function public.get_leaderboard(text, date, text, text, text, int) to authenticated;
grant execute on function public.get_my_rank(text, date, text, text, text)          to authenticated;
grant execute on function public.reroll_handle()                                    to authenticated;
grant execute on function public.rotate_recovery_key()                              to authenticated;
grant execute on function public.ping()                                             to anon, authenticated;
