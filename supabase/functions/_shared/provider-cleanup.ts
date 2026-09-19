import type { SupabaseClient } from "jsr:@supabase/supabase-js@2";

// https://www.revenuecat.com/docs/api-v1/customers#delete-customer
// Both 200 and 404 mean the deletion request is complete; an outage stays retryable.
export async function deleteProviderCustomer(
  userId: string,
  apiKey: string | undefined,
  send: typeof fetch = fetch,
): Promise<boolean> {
  if (!apiKey) return false;
  try {
    const response = await send(
      `https://api.revenuecat.com/v1/subscribers/${encodeURIComponent(userId)}`,
      {
        method: "DELETE",
        headers: { Authorization: `Bearer ${apiKey}` },
        signal: AbortSignal.timeout(3_000),
      },
    );
    await response.body?.cancel();
    return response.ok || response.status === 404;
  } catch {
    return false;
  }
}

// Recovery enqueues its old UUID transactionally. Account deletion enqueues its current
// UUID before deleting auth. Work survives both operations, and every recovery/deletion
// request retries a bounded batch, including earlier failures. A quiet project still needs
// the documented operator retry; this is not a promise of time-based deletion.
export async function cleanupProviderIdentities(
  admin: SupabaseClient,
  ownerId: string,
  apiKey: string | undefined,
): Promise<boolean> {
  try {
    return await cleanupBatch(admin, ownerId, apiKey);
  } catch (error) {
    // Recovery has already committed and rotated its key. Cleanup must never hide the
    // successful response (and that new key) behind a transport exception.
    console.error("provider cleanup deferred", error);
    return false;
  }
}

async function cleanupBatch(
  admin: SupabaseClient,
  ownerId: string,
  apiKey: string | undefined,
): Promise<boolean> {
  if (!apiKey) {
    console.warn("RC_API_KEY not configured - provider cleanup remains pending");
    return false;
  }
  const { data, error } = await admin.from("provider_cleanup")
    .select("user_id,delete_auth").order("created_at").limit(8);
  if (error) {
    console.error("provider cleanup read failed", error);
    return false;
  }
  await Promise.all((data ?? []).map(async (row) => {
    if (!await deleteProviderCustomer(row.user_id, apiKey)) return;
    // Remove the old auth user only after requesting provider deletion. A failed auth
    // cleanup keeps the job too; retrying RC deletion is idempotent.
    if (row.delete_auth) {
      const { error: authError } = await admin.auth.admin.deleteUser(row.user_id);
      if (authError && authError.status !== 404) return;
    }
    const { error: removeError } = await admin.from("provider_cleanup")
      .delete().eq("user_id", row.user_id);
    if (removeError) console.error("provider cleanup completion failed", removeError);
  }));
  const { count, error: pendingError } = await admin.from("provider_cleanup")
    .select("user_id", { count: "exact", head: true }).eq("owner_id", ownerId);
  return !pendingError && count === 0;
}
