// delete-account: true, immediate deletion (Google Play forbids deactivation-as-deletion).
//
// Scope (matches the support/privacy copy): the Supabase auth user + profile + runs + board
// rows (ON DELETE CASCADE) AND — when the owner has configured a RevenueCat secret key as a
// function secret (`supabase secrets set RC_API_KEY=sk_...`) — the RevenueCat customer for
// this app user id, per Google's requirement to extend deletion to processing service
// providers. RevenueCat deletion is best-effort: a provider outage must not leave the player
// unable to delete their profile, so the Supabase deletion proceeds and the response says
// whether the provider record went too (the support email is the documented fallback).
// The Play purchase itself is Google's record and can re-create provider data on a later
// RESTORE — disclosed in the policy copy.
//
// The client then discards its local session + recovery key and resets the commerce identity
// (IStore.ResetIdentity -> Purchases.LogOut). Spec: docs/PROFILE_LEADERBOARD_PLAN.md §5/§8.

import { createClient } from "jsr:@supabase/supabase-js@2";
import { cleanupProviderIdentities } from "../_shared/provider-cleanup.ts";

function json(status: number, body: Record<string, unknown>): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
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
  const userId = userData.user.id;

  const admin = createClient(
    Deno.env.get("SUPABASE_URL")!,
    Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!,
  );
  // Delete under the same transactional lock as submissions and recovery. This also
  // durably queues auth/provider cleanup, so a crash here cannot resurrect the profile.
  const { data, error: profileError } = await admin.rpc("delete_profile", { p_user: userId });
  if (profileError || !data) return json(500, { error: "profile deletion failed" });
  if (data.status === "profile_moved") {
    return json(409, { error: "profile moved; delete from the recovered session" });
  }
  if (data.status !== "ok") return json(500, { error: "profile deletion failed" });
  const { error } = await admin.auth.admin.deleteUser(userId);
  if (error && error.status !== 404) {
    console.error("delete failed", error);
    return json(500, { error: "delete failed" });
  }
  const providerDeleted = await cleanupProviderIdentities(
    admin, userId, Deno.env.get("RC_API_KEY"),
  );
  return json(200, { deleted: true, provider_deleted: providerDeleted });
});
