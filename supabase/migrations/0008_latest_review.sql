-- Latest Copilot review: serialize deletion/recovery and grant XP to accepted Free runs.

-- Called only by the authenticated Edge Function using the service role.
-- A recovery that already won retires the old identity: do not delete somebody else's
-- transferred profile or report success for data that still exists.
create or replace function public.delete_profile(p_user uuid)
returns json
language plpgsql
security definer
set search_path = public
as $$
begin
  perform pg_advisory_xact_lock(hashtext('veyro-run'), hashtext(p_user::text));
  if not exists (select 1 from public.profiles where user_id = p_user) then
    if exists (select 1 from public.provider_cleanup
               where user_id = p_user and owner_id = p_user and delete_auth) then
      return json_build_object('status', 'ok'); -- retry after a partial endpoint failure
    end if;
    return json_build_object('status', 'profile_moved');
  end if;

  insert into public.provider_cleanup (user_id, owner_id, delete_auth)
    values (p_user, p_user, true)
    on conflict (user_id) do update set owner_id = excluded.owner_id, delete_auth = true;
  -- Cascades remove runs and board rows before the lock is released. The auth user is
  -- removed by the endpoint (or cleanup retry), with provider work durably queued.
  delete from public.profiles where user_id = p_user;
  return json_build_object('status', 'ok');
end;
$$;
revoke all on function public.delete_profile(uuid) from public, anon, authenticated;
grant execute on function public.delete_profile(uuid) to service_role;

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
  -- even if the quota has since filled. All stored payload fields must match (day_label is display-only) — any drifted field is a
  -- client bug or an attack, answered with 'mismatch' rather than a reply about another run.
  select * into existing from public.runs
    where user_id = p_user and client_run_id = p_client_run_id;
  if found then
    if existing.seed <> p_seed
       or existing.mode <> p_mode
       or existing.content_version <> p_content_version
       or existing.world_id <> p_world_id
       or existing.input_mode <> p_input_mode
       or existing.app_version is distinct from p_app_version
       or existing.platform is distinct from p_platform
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
      daily_rank := public.board_rank_for_user(p_user, 'daily', existing.run_date,
        existing.content_version, existing.world_id, existing.input_group);
      alltime_rank := public.board_rank_for_user(p_user, 'alltime', 'epoch'::date,
        existing.content_version, existing.world_id, existing.input_group);
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

    daily_rank := public.board_rank_for_user(p_user, 'daily', run_day,
      p_content_version, p_world_id, new_group);
    alltime_rank := public.board_rank_for_user(p_user, 'alltime', 'epoch'::date,
      p_content_version, p_world_id, new_group);
  end if;

  -- Every accepted run earns XP; only Daily runs enter the boards above.
  update public.profiles
    set xp = xp + least(p_score / 100, 500), updated_at = now()
    where user_id = p_user;

  return json_build_object('status', 'ok', 'accepted', true, 'duplicate', false,
    'reason', '', 'daily_rank', daily_rank, 'alltime_rank', alltime_rank);
end;
$$;

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

  if old_user = p_new_user then
    return json_build_object('status', 'ok', 'recovered', true,
      'recovery_key', '', 'old_user', null);
  end if;

  -- Deletion may have removed the destination while auth cleanup is still pending.
  -- Never transfer data into that retired identity, even with a still-valid JWT.
  if not exists (select 1 from public.profiles where user_id = p_new_user) then
    return json_build_object('status', 'destination_deleted', 'recovered', false,
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

  -- Persist the provider id in the SAME transaction as the transfer. Cleanup survives
  -- an Edge Function crash, provider outage, another recovery, or account deletion.
  update public.provider_cleanup set owner_id = p_new_user where owner_id = old_user;
  insert into public.provider_cleanup (user_id, owner_id, delete_auth)
    values (old_user, p_new_user, true)
    on conflict (user_id) do update set owner_id = excluded.owner_id, delete_auth = true;

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

