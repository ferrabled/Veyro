// recover-session: reinstall restore without login.
// The caller is a FRESH anonymous user (JWT required) presenting the recovery key its device
// backup preserved. Since migration 0004 the whole claim is ONE transactional RPC
// (recover_profile): hash validated with the source row locked, attempt throttling atomic,
// destination must be a fresh profile (a played profile is never silently destroyed), and the
// key rotates in the same transaction. This function only verifies the caller, hashes the
// key, calls the RPC, cleans up the orphaned auth user, and never leaks which step failed
// for an unknown key.

import { createClient } from "jsr:@supabase/supabase-js@2";
import { readJsonBody } from "../_shared/validate.ts";
import { cleanupProviderIdentities } from "../_shared/provider-cleanup.ts";

const KEY_RE = /^[0-9a-f]{64}$/;

function json(status: number, body: Record<string, unknown>): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

// Unknown key and deleted-source look identical from outside; empty string, never null
// (Unity JsonUtility flat-object contract).
const failure = () => json(200, { recovered: false, recovery_key: "", reason: "" });

function toHex(bytes: Uint8Array): string {
  return Array.from(bytes, (b) => b.toString(16).padStart(2, "0")).join("");
}

async function sha256Hex(hexKey: string): Promise<string> {
  const bytes = new Uint8Array(hexKey.length / 2);
  for (let i = 0; i < bytes.length; i++) {
    bytes[i] = parseInt(hexKey.substr(i * 2, 2), 16);
  }
  return toHex(new Uint8Array(await crypto.subtle.digest("SHA-256", bytes)));
}

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
  const newUserId = userData.user.id;

  const body = await readJsonBody(req) as { recovery_key?: unknown } | null;
  if (body === null) return json(400, { error: "invalid or oversized body" });
  const key = body.recovery_key;
  if (typeof key !== "string" || !KEY_RE.test(key)) {
    return json(400, { error: "recovery_key must be 64 hex chars" });
  }

  const admin = createClient(
    Deno.env.get("SUPABASE_URL")!,
    Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!,
  );

  const { data, error } = await admin.rpc("recover_profile", {
    p_new_user: newUserId,
    p_key_hash: "\\x" + await sha256Hex(key),
  });
  if (error || !data) {
    console.error("recover_profile failed", error);
    return json(500, { error: "recovery failed" });
  }

  const result = data as {
    status: string;
    recovered: boolean;
    recovery_key: string;
    old_user: string | null;
  };

  switch (result.status) {
    case "throttled":
      return json(429, { error: "too many attempts" });
    case "not_found":
      return failure();
    case "destination_not_empty":
      // The CALLER's own state — telling them is safe and the client needs it: this install
      // has already played as its own profile, so recovery must not destroy it.
      return json(409, { recovered: false, recovery_key: "", reason: "destination_not_empty" });
    case "ok": {
      // Migration 0007 preserves old_user in a cleanup job in the claim transaction.
      // Never forget the provider identity just because the profile's UUID changed.
      const providerDeleted = await cleanupProviderIdentities(
        admin, newUserId, Deno.env.get("RC_API_KEY"),
      );
      return json(200, {
        recovered: true,
        recovery_key: result.recovery_key ?? "",
        reason: "",
        provider_deleted: providerDeleted,
      });
    }
    default:
      console.error("recover_profile unexpected status", result.status);
      return json(500, { error: "recovery failed" });
  }
});
