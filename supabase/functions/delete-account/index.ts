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

  // Provider record first, while the id is still meaningful. RC's app user id IS the
  // Supabase UUID (Purchases.LogIn wiring), so one DELETE covers the linked customer.
  let providerDeleted = false;
  const rcKey = Deno.env.get("RC_API_KEY");
  if (rcKey) {
    try {
      const rc = await fetch(
        `https://api.revenuecat.com/v1/subscribers/${encodeURIComponent(userId)}`,
        {
          method: "DELETE",
          headers: { Authorization: `Bearer ${rcKey}` },
          signal: AbortSignal.timeout(10_000),
        },
      );
      // 404 = no customer record ever existed for this id — that is also "nothing retained".
      providerDeleted = rc.ok || rc.status === 404;
      if (!providerDeleted) {
        console.error(`revenuecat deletion failed: HTTP ${rc.status}`);
      }
    } catch (e) {
      console.error("revenuecat deletion failed", e);
    }
  } else {
    console.warn("RC_API_KEY not configured - provider deletion skipped (support email fallback)");
  }

  const admin = createClient(
    Deno.env.get("SUPABASE_URL")!,
    Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!,
  );
  const { error } = await admin.auth.admin.deleteUser(userId);
  if (error) {
    console.error("delete failed", error);
    return json(500, { error: "delete failed" });
  }
  return json(200, { deleted: true, provider_deleted: providerDeleted });
});
