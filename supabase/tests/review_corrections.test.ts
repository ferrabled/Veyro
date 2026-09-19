// Runs the actual migrations and RPCs in a disposable PostgreSQL WASM database.
// deno test --allow-read supabase/tests/review_corrections.test.ts
// PGlite lacks pgcrypto: only extension setup/random-byte issuance is shimmed below.
// PostgreSQL's own sha256 is used; no production database or credentials are involved.
import { PGlite } from "npm:@electric-sql/pglite@0.3.14";

function assert(value: unknown, message: string): asserts value {
  if (!value) throw new Error(message);
}

async function database() {
  const db = new PGlite();
  await db.exec(`
    create role anon; create role authenticated; create role service_role bypassrls;
    create schema auth; create schema extensions;
    create table auth.users(id uuid primary key);
    create function auth.uid() returns uuid language sql as
      'select nullif(current_setting(''request.jwt.claim.sub'', true), '''')::uuid';
    create function extensions.gen_random_bytes(n integer) returns bytea language sql as
      'select decode(substr(repeat(md5(gen_random_uuid()::text), n), 1, n * 2), ''hex'')';
    create function extensions.digest(b bytea, algorithm text) returns bytea language sql as
      'select sha256(b)';
    alter default privileges grant all on tables to anon, authenticated, service_role;
    alter default privileges grant all on sequences to anon, authenticated, service_role;
    alter default privileges grant execute on functions to service_role;
  `);
  const migrations = new URL("../migrations/", import.meta.url);
  const paths: string[] = [];
  for await (const entry of Deno.readDir(migrations)) {
    if (entry.name.endsWith(".sql")) paths.push(entry.name);
  }
  for (const name of paths.sort()) {
    const sql = (await Deno.readTextFile(new URL(name, migrations)))
      .replace("create extension if not exists pgcrypto with schema extensions;", "");
    await db.exec(sql);
  }
  return db;
}

const player = "11111111-1111-4111-8111-111111111111";
const rival = "22222222-2222-4222-8222-222222222222";
const replacement = "33333333-3333-4333-8333-333333333333";

async function submit(db: PGlite, user: string, run: number, score: number,
  version = "test", platform = "android_gp", mode = "daily") {
  const id = `aaaaaaaa-aaaa-4aaa-8aaa-${String(run).padStart(12, "0")}`;
  const { rows } = await db.query<{ result: Record<string, unknown> }>(`
    select public.process_run($1, $2, $6,
      to_char(now() at time zone 'utc', 'YYYYMMDD')::int,
      'greybox-1', 'greybox', 'display-only', 'tilt', $3, 100, 30, 30, 60, $4, $5) as result
  `, [user, id, score, version, platform, mode]);
  return rows[0].result;
}

Deno.test("retained board scores determine new-run and replay ranks; metadata drift is rejected", async () => {
  const db = await database();
  try {
    await db.query("insert into auth.users (id) values ($1), ($2)", [player, rival]);
    assert((await submit(db, player, 1, 100)).accepted, "first run failed");
    assert((await submit(db, rival, 2, 500)).accepted, "rival failed");
    await db.exec("update public.runs set created_at = now() - interval '1 hour'");
    const best = await submit(db, player, 3, 1000);
    assert(best.daily_rank === 1 && best.alltime_rank === 1, "new best must lead");
    await db.exec("update public.runs set created_at = now() - interval '1 hour'");
    const worse = await submit(db, player, 4, 200);
    assert(worse.accepted && worse.daily_rank === 1 && worse.alltime_rank === 1,
      "lower follow-up must report retained best rank");
    const xpBefore = await db.query("select xp from profiles where user_id = $1", [player]);
    const replay = await submit(db, player, 1, 100);
    assert(replay.duplicate && replay.daily_rank === 1 && replay.alltime_rank === 1,
      "historical replay must report current board ranks");
    const xpAfter = await db.query("select xp from profiles where user_id = $1", [player]);
    assert(JSON.stringify(xpBefore.rows) === JSON.stringify(xpAfter.rows), "replay granted XP twice");
    assert((await submit(db, player, 1, 100, "changed")).status === "mismatch", "app version drift accepted");
    assert((await submit(db, player, 1, 100, "test", "ios")).status === "mismatch", "platform drift accepted");
    await db.exec("update public.runs set created_at = now() - interval '1 hour'");
    const tied = await submit(db, rival, 5, 1000);
    assert(tied.daily_rank === 1 && tied.alltime_rank === 1, "competition ties changed");
  } finally { await db.close(); }
});

Deno.test("accepted Free runs earn XP once without entering either board; flagged runs earn none", async () => {
  const db = await database();
  try {
    await db.query("insert into auth.users (id) values ($1)", [player]);
    const free = await submit(db, player, 1, 1000, "test", "ios", "free");
    assert(free.accepted && free.daily_rank === 0 && free.alltime_rank === 0, "Free run was ranked or rejected");
    assert((await submit(db, player, 1, 1000, "test", "ios", "free")).duplicate, "Free replay failed");
    await db.exec("update runs set created_at = now() - interval '1 hour'");
    assert(!(await submit(db, player, 2, 999999, "test", "ios", "free")).accepted, "implausible run accepted");
    const { rows } = await db.query<{ xp: number; boards: number; runs: number }>(`
      select xp, (select count(*)::int from board_scores) boards,
        (select count(*)::int from runs) runs from profiles where user_id = $1`, [player]);
    assert(rows[0].xp === 10, "Free run must grant 10 XP exactly once");
    assert(rows[0].boards === 0 && rows[0].runs === 2, "Free history/board contract changed");
  } finally { await db.close(); }
});

for (const recoveryFirst of [false, true]) {
  Deno.test(`deletion/recovery serialize safely, recovery first = ${recoveryFirst}`, async () => {
    const db = await database();
    try {
      await db.query("insert into auth.users (id) values ($1), ($2)", [player, rival]);
      await submit(db, player, 1, 1000);
      await db.query("update profiles set recovery_key_hash = decode('aabb', 'hex') where user_id = $1", [player]);
      const recover = () => db.query<{ result: { status: string } }>(
        "select recover_profile($1, decode('aabb', 'hex')) result", [rival]);
      const remove = () => db.query<{ result: { status: string } }>(
        "select delete_profile($1) result", [player]);
      if (recoveryFirst) {
        assert((await recover()).rows[0].result.status === "ok", "recovery failed");
        assert((await remove()).rows[0].result.status === "profile_moved", "must not claim transferred data was erased");
        const { rows } = await db.query<{ xp: number }>("select xp from profiles where user_id = $1", [rival]);
        assert(rows[0].xp === 10, "old session deleted recovered profile");
      } else {
        assert((await remove()).rows[0].result.status === "ok", "deletion failed");
        assert((await remove()).rows[0].result.status === "ok", "partial deletion cannot be retried");
        assert((await recover()).rows[0].result.status === "not_found", "deleted profile was recovered");
        const { rows } = await db.query<{ count: number }>(`select
          ((select count(*) from profiles where user_id = $1) +
           (select count(*) from runs) + (select count(*) from board_scores))::int count`, [player]);
        assert(rows[0].count === 0, "personal records survived deletion");
        const jobs = await db.query<{ delete_auth: boolean }>("select delete_auth from provider_cleanup where user_id = $1 and owner_id = $1", [player]);
        assert(jobs.rows[0].delete_auth, "auth/provider deletion must survive endpoint failure");
        // Auth removal may still be pending: a stale JWT must not recover another profile
        // into the now-deleted destination and then lose it to that pending auth cascade.
        await db.query("update profiles set recovery_key_hash = decode('ccdd', 'hex') where user_id = $1", [rival]);
        const retired = await db.query<{ result: { status: string } }>(
          "select recover_profile($1, decode('ccdd', 'hex')) result", [player]);
        assert(retired.rows[0].result.status === "destination_deleted", "retired identity received another profile");
      }
      const { rows } = await db.query<{ exposed: boolean; service: boolean }>(`select
        has_function_privilege('authenticated', 'delete_profile(uuid)', 'execute') or
        has_function_privilege('anon', 'delete_profile(uuid)', 'execute') exposed,
        has_function_privilege('service_role', 'delete_profile(uuid)', 'execute') service`);
      assert(!rows[0].exposed && rows[0].service, "deletion RPC access is incorrect");
    } finally { await db.close(); }
  });
}

Deno.test("recovery cleanup survives repeated transfers and account deletion with API access denied", async () => {
  const db = await database();
  try {
    await db.query("insert into auth.users (id) values ($1), ($2), ($3)", [player, rival, replacement]);
    await db.query("update profiles set recovery_key_hash = decode('aabb', 'hex') where user_id = $1", [player]);
    const claim = await db.query<{ result: { status: string; recovery_key: string } }>(
      "select recover_profile($1, decode('aabb', 'hex')) as result", [rival]);
    assert(claim.rows[0].result.status === "ok", "claim failed");
    const next = await db.query<{ result: { status: string } }>(
      "select recover_profile($1, sha256(decode($2, 'hex'))) as result",
      [replacement, claim.rows[0].result.recovery_key]);
    assert(next.rows[0].result.status === "ok", "second claim failed");
    await db.query("delete from auth.users where id = $1", [replacement]);
    const jobs = await db.query<{ owner_id: string }>("select * from provider_cleanup order by user_id");
    assert(jobs.rows.length === 2 && jobs.rows.every((r) => r.owner_id === replacement),
      "provider ids were forgotten on transfer or deletion");
    const grants = await db.query<{ exposed: boolean }>(`select
      has_table_privilege('anon', 'provider_cleanup', 'select') or
      has_table_privilege('authenticated', 'provider_cleanup', 'select') or
      has_function_privilege('authenticated',
        'board_rank_for_user(uuid,text,date,text,text,text)', 'execute') as exposed`);
    assert(!grants.rows[0].exposed, "new service-only surface leaked to API users");
  } finally { await db.close(); }
});
