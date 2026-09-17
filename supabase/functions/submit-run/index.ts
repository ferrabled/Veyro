// submit-run: JWT + shape validation, then ONE transactional RPC (process_run, migration
// 0004) that owns everything stateful — dedupe/idempotency, quotas, plausibility, insert,
// boards, XP, ranks — under a per-user advisory lock. Nothing here can half-complete: a
// downstream failure rolls the whole submission back and the client's retry (same
// client_run_id) reprocesses cleanly.
// Response invariant: numeric fields always present, never null (Unity JsonUtility).

import { createClient } from "jsr:@supabase/supabase-js@2";
import { readJsonBody, RunSubmission, schemaError } from "../_shared/validate.ts";

function json(status: number, body: Record<string, unknown>): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

const EMPTY = { reason: "", daily_rank: 0, alltime_rank: 0 };

Deno.serve(async (req) => {
  if (req.method !== "POST") return json(405, { error: "POST only" });

  const authHeader = req.headers.get("Authorization") ?? "";
  const userClient = createClient(
    Deno.env.get("SUPABASE_URL")!,
    Deno.env.get("SUPABASE_ANON_KEY")!,
    { global: { headers: { Authorization: authHeader } } },
  );
  const { data: userData, error: userError } = await userClient.auth.getUser();
  if (userError || !userData?.user) return json(401, { error: "no user" });

  const body = await readJsonBody(req);
  if (body === null) return json(400, { error: "invalid or oversized body" });
  const schemaProblem = schemaError(body);
  if (schemaProblem) return json(400, { error: schemaProblem });
  const run = body as RunSubmission;

  const admin = createClient(
    Deno.env.get("SUPABASE_URL")!,
    Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!,
  );

  const { data, error } = await admin.rpc("process_run", {
    p_user: userData.user.id,
    p_client_run_id: run.client_run_id,
    p_mode: run.mode,
    p_seed: run.seed,
    p_content_version: run.content_version,
    p_world_id: run.world_id,
    p_day_label: run.day_label,
    p_input_mode: run.input_mode,
    p_score: run.score,
    p_distance_m: run.distance_m,
    p_coins: run.coins,
    p_best_combo: run.best_combo,
    p_duration_s: run.duration_s,
    p_app_version: run.app_version,
    p_platform: run.platform,
  });

  // Fail CLOSED: a database error is never treated as permission to accept.
  if (error || !data) {
    console.error("process_run failed", error);
    return json(500, { error: "processing failed" });
  }

  const result = data as {
    status: string;
    accepted: boolean;
    duplicate: boolean;
    reason: string;
    daily_rank: number;
    alltime_rank: number;
  };

  switch (result.status) {
    case "ok":
    case "flagged":
      return json(200, {
        accepted: result.accepted,
        duplicate: result.duplicate,
        reason: result.reason ?? "",
        daily_rank: result.daily_rank ?? 0,
        alltime_rank: result.alltime_rank ?? 0,
      });
    case "mismatch":
      return json(409, { error: "client_run_id reused with a different payload", ...EMPTY });
    case "too_fast":
    case "daily_limit":
    case "flagged_limit":
      return json(429, { error: result.status, ...EMPTY });
    case "unknown_content":
    case "unknown_world":
    case "bad_seed":
      return json(400, { error: result.status, ...EMPTY });
    default:
      console.error("process_run unexpected status", result.status);
      return json(500, { error: "processing failed" });
  }
});
