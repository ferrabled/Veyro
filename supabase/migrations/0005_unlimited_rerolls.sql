-- Owner call, 17 Sep: handle rerolls are UNLIMITED — let players find a name they like
-- (amends the "3 lifetime" half of D15; still server-generated only, so still
-- no UGC and no Play-policy impact). The rerolls_left column stays for wire compatibility
-- (clients in the field read it) but is no longer decremented or checked; the abuse surface
-- is a handle regeneration per tap, which is one indexed UPDATE — throttled, like every
-- authenticated call, only by being an authenticated call.

create or replace function public.reroll_handle()
returns json
language plpgsql
security definer
set search_path = public
as $$
declare
  new_handle text;
begin
  if not exists (select 1 from public.profiles where user_id = auth.uid()) then
    raise exception 'no profile' using errcode = 'P0002';
  end if;

  new_handle := public.generate_handle();
  update public.profiles
    set handle = new_handle,
        updated_at = now()
    where user_id = auth.uid();

  return json_build_object('handle', new_handle, 'rerolls_left', 999);
end;
$$;
