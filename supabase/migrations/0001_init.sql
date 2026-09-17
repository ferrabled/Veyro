-- Veyro Run profile/leaderboard backend — initial schema.
-- Security model: the anon key can NEVER write a table directly. Reads of shared data go
-- through security-definer RPCs that expose handle+score+rank only. All writes happen in
-- Edge Functions (service role, after JWT verification) or auth.uid()-keyed RPCs.
-- Spec: docs/PROFILE_LEADERBOARD_PLAN.md

-- Hosted Supabase installs extensions into the `extensions` schema; match that locally so
-- the explicit `extensions.` qualifications below work in both places.
create extension if not exists pgcrypto with schema extensions;

-- ============================================================ profiles

create table public.profiles (
  user_id           uuid primary key references auth.users (id)
                      on delete cascade on update cascade,
  handle            text not null unique,
  rerolls_left      smallint not null default 3 check (rerolls_left >= 0),
  -- SHA-256 of the reinstall recovery key. The key itself exists only on the device.
  recovery_key_hash bytea,
  -- Server-authoritative: incremented by submit-run from accepted runs, never set by a client.
  xp                integer not null default 0 check (xp >= 0),
  created_at        timestamptz not null default now(),
  updated_at        timestamptz not null default now()
);

-- ============================================================ runs (personal history)

create table public.runs (
  id               bigint generated always as identity primary key,
  user_id          uuid not null references public.profiles (user_id)
                     on delete cascade on update cascade,
  -- Client-generated idempotency key: a retry after a network timeout must not double-insert.
  client_run_id    uuid not null,
  mode             text not null check (mode in ('daily', 'free')),
  seed             integer not null,
  content_version  text not null,
  world_id         text not null,
  -- Derived by submit-run from the seed (daily) or the server clock (free) — never client-set.
  run_date         date not null,
  input_mode       text not null check (input_mode in ('tilt', 'camera', 'camera_fallback')),
  -- Board grouping enforced in the database: a camera run that degraded to tilt
  -- (camera_fallback) competes on the standard board, never the camera board.
  input_group      text generated always as
                     (case when input_mode = 'camera' then 'camera' else 'standard' end) stored,
  score            integer not null check (score >= 0),
  distance_m       real not null check (distance_m >= 0),
  coins            integer not null check (coins >= 0),
  best_combo       integer not null check (best_combo >= 0),
  duration_s       real not null check (duration_s > 0),
  app_version      text not null,
  platform         text not null check (platform in ('android_gp', 'android_galaxy', 'ios')),
  -- Failed a plausibility bound: kept for tuning/forensics, excluded from boards and XP.
  flagged          boolean not null default false,
  created_at       timestamptz not null default now(),
  unique (user_id, client_run_id)
);

create index runs_user_history_idx on public.runs (user_id, created_at desc);
create index runs_user_day_idx     on public.runs (user_id, run_date);

-- ============================================================ board_scores
-- One row per user per board (the conditional upsert in submit-run is the anti-spam measure).
-- scope='alltime' uses the sentinel date 'epoch' (1970-01-01) so one PK shape serves both.

create table public.board_scores (
  scope            text not null check (scope in ('daily', 'alltime')),
  run_date         date not null,
  content_version  text not null,
  world_id         text not null,
  input_group      text not null check (input_group in ('standard', 'camera')),
  user_id          uuid not null references public.profiles (user_id)
                     on delete cascade on update cascade,
  score            integer not null check (score >= 0),
  run_id           bigint not null references public.runs (id) on delete cascade,
  achieved_at      timestamptz not null default now(),
  check (scope = 'daily' or run_date = 'epoch'::date),
  primary key (scope, run_date, content_version, world_id, input_group, user_id)
);

create index board_scores_topn_idx on public.board_scores
  (scope, run_date, content_version, world_id, input_group, score desc);

-- ============================================================ game_config
-- Plausibility bounds per content version, tunable without a redeploy.

create table public.game_config (
  content_version  text primary key,
  max_speed_mps    real not null check (max_speed_mps > 0),
  max_coins_per_s  real not null check (max_coins_per_s > 0),
  min_duration_s   real not null default 3 check (min_duration_s > 0),
  max_duration_s   real not null default 3600,
  worlds           text[] not null
);

-- ============================================================ handle generation
-- Server-generated only (no free text = no UGC): ADJECTIVE-ANIMAL-N, ~405K combos.

create or replace function public.generate_handle()
returns text
language plpgsql
security definer
set search_path = public
as $$
declare
  adjectives text[] := array[
    'SWIFT','BRAVE','LUCKY','SOLAR','LUNAR','RAPID','QUIET','WILD','NOBLE','AMBER',
    'AZURE','CORAL','CRISP','DUSTY','EARLY','FUZZY','GOLD','HAPPY','IRON','JOLLY',
    'KEEN','LIGHT','MINTY','NIFTY','OPAL','PLUCKY','QUICK','ROYAL','SUNNY','TIDAL',
    'ULTRA','VIVID','WITTY','ZESTY','BOLD','CALM','DAWN','EMBER','FROST','GALE',
    'HAZEL','INDIGO','JADE','KIND','LOFTY','MELLOW','NIMBLE','OCEAN','PRIME','RUSTY',
    'SABLE','TRUE','URBAN','VELVET','WARM','YOUNG','ZIPPY','ARCTIC','BREEZY','CEDAR',
    'DAPPER','ELDER','FABLE','GRAND'
  ];
  animals text[] := array[
    'FOX','WREN','OTTER','LYNX','HERON','KOALA','PANDA','RAVEN','TIGER','WHALE',
    'BISON','CAMEL','DINGO','EAGLE','FINCH','GECKO','HYENA','IBIS','JAGUAR','KIWI',
    'LEMUR','MOOSE','NEWT','ORCA','PUMA','QUAIL','ROBIN','SEAL','TAPIR','URCHIN',
    'VIPER','WOLF','YAK','ZEBRA','BADGER','CIVET','DONKEY','EGRET','FALCON','GOOSE',
    'HARE','IMPALA','JERBOA','KESTREL','LLAMA','MARTEN','NARWHAL','OCELOT','PARROT','RACCOON',
    'SHRIKE','TOUCAN','UAKARI','VOLE','WALRUS','XERUS','YABBY','ZORILLA','BEAVER','CONDOR',
    'DUGONG','ERMINE','FERRET','GIBBON'
  ];
  candidate text;
  attempt int := 0;
  max_number int;
begin
  loop
    attempt := attempt + 1;
    -- Widen the number space if the early tries keep colliding.
    max_number := case when attempt <= 5 then 99 else 999 end;
    candidate := adjectives[1 + floor(random() * array_length(adjectives, 1))::int]
                 || '-' ||
                 animals[1 + floor(random() * array_length(animals, 1))::int]
                 || '-' ||
                 (1 + floor(random() * max_number))::text;
    if not exists (select 1 from public.profiles where handle = candidate) then
      return candidate;
    end if;
    if attempt >= 10 then
      -- Last resort stays unique and still looks like a handle.
      return 'RUNNER-' || substr(replace(gen_random_uuid()::text, '-', ''), 1, 8);
    end if;
  end loop;
end;
$$;

-- Profile row exists before the client's first read: created with the auth user itself.
create or replace function public.handle_new_user()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
begin
  insert into public.profiles (user_id, handle) values (new.id, public.generate_handle());
  return new;
end;
$$;

create trigger on_auth_user_created
  after insert on auth.users
  for each row execute function public.handle_new_user();

-- ============================================================ RPCs

-- Leaderboard read: the ONLY way shared data leaves the database. Exposes handle+score+rank,
-- never user ids. LIMIT is capped server-side. Returns a JSON object (not a bare array)
-- because the Unity client parses with JsonUtility, which cannot read top-level arrays.
create or replace function public.get_leaderboard(
  p_scope text,
  p_run_date date,
  p_content_version text,
  p_world_id text,
  p_input_group text,
  p_limit int default 100)
returns json
language sql
stable
security definer
set search_path = public
as $$
  select json_build_object('entries', coalesce(json_agg(e), '[]'::json))
  from (
    select p.handle,
           b.score,
           (rank() over (order by b.score desc))::int as rank
    from public.board_scores b
    join public.profiles p using (user_id)
    where b.scope = p_scope
      and b.run_date = case when p_scope = 'alltime' then 'epoch'::date else p_run_date end
      and b.content_version = p_content_version
      and b.world_id = p_world_id
      and b.input_group = p_input_group
    order by b.score desc, b.achieved_at asc
    limit least(greatest(coalesce(p_limit, 100), 1), 100)
  ) e;
$$;

create or replace function public.get_my_rank(
  p_scope text,
  p_run_date date,
  p_content_version text,
  p_world_id text,
  p_input_group text)
returns json
language sql
stable
security definer
set search_path = public
as $$
  -- 0 = "not on this board": the Unity client parses with JsonUtility, which cannot
  -- represent a null number, so numeric fields are always present and never null.
  select case
    when mine.score is null then json_build_object('rank', 0, 'score', 0)
    else json_build_object(
      'rank',
      1 + (select count(*)
           from public.board_scores b
           where b.scope = p_scope
             and b.run_date = case when p_scope = 'alltime' then 'epoch'::date else p_run_date end
             and b.content_version = p_content_version
             and b.world_id = p_world_id
             and b.input_group = p_input_group
             and b.score > mine.score)::int,
      'score', mine.score)
  end
  from (
    -- Scalar subquery: this outer row always exists, score is null when the player
    -- is not on the board — so the function always returns exactly one JSON object.
    select (
      select score
      from public.board_scores b
      where b.scope = p_scope
        and b.run_date = case when p_scope = 'alltime' then 'epoch'::date else p_run_date end
        and b.content_version = p_content_version
        and b.world_id = p_world_id
        and b.input_group = p_input_group
        and b.user_id = auth.uid()
    ) as score
  ) mine;
$$;

-- Handle reroll: keyed on auth.uid(), 3 lifetime uses (column default).
create or replace function public.reroll_handle()
returns json
language plpgsql
security definer
set search_path = public
as $$
declare
  remaining smallint;
  new_handle text;
begin
  select rerolls_left into remaining
  from public.profiles where user_id = auth.uid() for update;

  if remaining is null then
    raise exception 'no profile' using errcode = 'P0002';
  end if;
  if remaining <= 0 then
    return json_build_object('handle', null, 'rerolls_left', 0);
  end if;

  new_handle := public.generate_handle();
  update public.profiles
    set handle = new_handle,
        rerolls_left = remaining - 1,
        updated_at = now()
    where user_id = auth.uid();

  return json_build_object('handle', new_handle, 'rerolls_left', remaining - 1);
end;
$$;

-- Keep-alive target for the Cloudflare Worker cron: the cheapest request that still counts
-- as database activity against the free-tier 7-day pause counter.
create or replace function public.ping()
returns int
language sql
as $$ select 1 $$;

-- ============================================================ RLS + grants
-- Default posture: deny. No insert/update/delete policy exists anywhere; writes are
-- service-role (Edge Functions) or the security-definer RPCs above.

alter table public.profiles     enable row level security;
alter table public.runs         enable row level security;
alter table public.board_scores enable row level security;
alter table public.game_config  enable row level security;

create policy "own profile" on public.profiles
  for select to authenticated using (user_id = auth.uid());

create policy "own runs" on public.runs
  for select to authenticated using (user_id = auth.uid());

-- board_scores and game_config: no select policy — readable only through the RPCs.

revoke insert, update, delete on all tables in schema public from anon, authenticated;

-- ============================================================ recovery (reinstall restore)
-- The recovery key never rotates on refresh (unlike session tokens), so a device-backup
-- copy always works. On a successful claim it IS rotated, so a leaked old key dies.

create index profiles_recovery_idx on public.profiles (recovery_key_hash)
  where recovery_key_hash is not null;

-- Defense-in-depth pacing for recover-session (a 256-bit key is not brute-forceable, but
-- there is no reason to allow probing either).
create table public.recovery_attempts (
  user_id      uuid not null,
  attempted_at timestamptz not null default now()
);
create index recovery_attempts_idx on public.recovery_attempts (user_id, attempted_at desc);
alter table public.recovery_attempts enable row level security;

-- One-time issue of the caller's recovery key. The key is returned exactly once (only
-- while no hash is stored); afterwards only its hash exists server-side. Rotation happens
-- solely through a successful claim.
create or replace function public.issue_recovery_key()
returns json
language plpgsql
security definer
set search_path = public
as $$
declare
  raw bytea;
  key_hex text;
  updated int;
begin
  raw := extensions.gen_random_bytes(32);
  key_hex := encode(raw, 'hex');
  update public.profiles
    set recovery_key_hash = extensions.digest(raw, 'sha256'),
        updated_at = now()
    where user_id = auth.uid() and recovery_key_hash is null;
  get diagnostics updated = row_count;
  if updated = 0 then
    return json_build_object('recovery_key', null);
  end if;
  return json_build_object('recovery_key', key_hex);
end;
$$;

-- Atomically move the old profile (and, via ON UPDATE CASCADE, its runs and board rows)
-- onto the caller's fresh anonymous user. The caller's auto-created empty profile dies;
-- the old auth user is deleted afterwards by the Edge Function.
create or replace function public.claim_profile(
  p_old_user uuid,
  p_new_user uuid,
  p_new_hash bytea)
returns void
language plpgsql
as $$
begin
  if p_old_user = p_new_user then
    return;
  end if;
  delete from public.profiles where user_id = p_new_user;
  update public.profiles
    set user_id = p_new_user,
        recovery_key_hash = p_new_hash,
        updated_at = now()
    where user_id = p_old_user;
end;
$$;

-- ============================================================ service-role helpers
-- Called only by the submit-run Edge Function (service role). Keeping them in SQL makes
-- the conditional board upsert atomic and the XP grant a single statement.

create or replace function public.upsert_board_score(
  p_scope text,
  p_run_date date,
  p_content_version text,
  p_world_id text,
  p_input_group text,
  p_user_id uuid,
  p_score int,
  p_run_id bigint)
returns void
language sql
as $$
  insert into public.board_scores
    (scope, run_date, content_version, world_id, input_group, user_id, score, run_id)
  values
    (p_scope, p_run_date, p_content_version, p_world_id, p_input_group, p_user_id,
     p_score, p_run_id)
  on conflict (scope, run_date, content_version, world_id, input_group, user_id)
  do update set score = excluded.score, run_id = excluded.run_id, achieved_at = now()
  where excluded.score > public.board_scores.score;
$$;

create or replace function public.grant_xp(p_user_id uuid, p_amount int)
returns void
language sql
as $$
  update public.profiles
    set xp = xp + greatest(p_amount, 0), updated_at = now()
    where user_id = p_user_id;
$$;

revoke execute on all functions in schema public from public, anon, authenticated;
grant execute on function public.get_leaderboard(text, date, text, text, text, int) to authenticated;
grant execute on function public.get_my_rank(text, date, text, text, text)          to authenticated;
grant execute on function public.reroll_handle()                                    to authenticated;
grant execute on function public.issue_recovery_key()                               to authenticated;
grant execute on function public.ping()                                             to anon, authenticated;
-- upsert_board_score / grant_xp / claim_profile / generate_handle / handle_new_user:
-- service-role only (no grant; default execute revoked above).
