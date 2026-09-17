// delete-account: true, immediate deletion (Google Play forbids deactivation-as-deletion).
// auth.admin.deleteUser removes the auth user; ON DELETE CASCADE wipes the profile, runs
// and board rows with it. The client then discards its local session + recovery key and
// calls Purchases.logOut(). Spec: docs/PROFILE_LEADERBOARD_PLAN.md §5/§8.

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

  const admin = createClient(
    Deno.env.get("SUPABASE_URL")!,
    Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!,
  );
  const { error } = await admin.auth.admin.deleteUser(userData.user.id);
  if (error) {
    console.error("delete failed", error);
    return json(500, { error: "delete failed" });
  }
  return json(200, { deleted: true });
});
