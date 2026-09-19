-- Found by the 17 Sep live smoke test: on hosted Supabase, pgcrypto is installed into the
-- `extensions` schema, and issue_recovery_key() pins `search_path = public` (deliberately,
-- it is security definer) — so gen_random_bytes()/digest() were not visible and every call
-- failed with 42883. Qualify them explicitly; keep the pinned search_path.

create extension if not exists pgcrypto with schema extensions;

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
