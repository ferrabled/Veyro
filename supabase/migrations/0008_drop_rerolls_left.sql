-- Drop the dead `rerolls_left` column (owner call, 19 Sep).
--
-- 0005 made rerolls unlimited and kept the column "for wire compatibility (clients in the
-- field read it)". That rationale was never true: T-009 has not shipped, so there are ZERO
-- clients in the field. 0006 removed its last real use (the fresh-destination check reads
-- runs + xp instead), and nothing in the game reads it — the reroll link says "new name" and
-- never counts down. What remained was a column with `default 3` and `check (>= 0)` that
-- three call sites answered with a hardcoded 999: exactly the shape that makes a future
-- reader believe a reroll budget exists.
--
-- Doing it NOW is the point. Pre-release is the only window where dropping a column is free;
-- once the leaderboard update is on phones, old clients would still be selecting it.
--
-- No behaviour change: rerolls were already unlimited, and the 2-second anti-flood throttle
-- in reroll_handle is untouched.

-- The only function that still named the column. `rerolls_left` leaves the response shape;
-- clients read `handle` and `throttled`, and JsonUtility simply ignores absent fields.
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
    return json_build_object('handle', null, 'throttled', true);
  end if;

  new_handle := public.generate_handle();
  update public.profiles
    set handle = new_handle,
        handle_changed_at = now(),
        updated_at = now()
    where user_id = auth.uid();

  return json_build_object('handle', new_handle, 'throttled', false);
end;
$$;

-- Dropping the column also drops the column-level SELECT grant 0004 gave it; the grants on
-- user_id / handle / xp / created_at are unaffected, so the hash stays unreadable.
alter table public.profiles drop column rerolls_left;
