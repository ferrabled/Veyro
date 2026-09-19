import { cleanupProviderIdentities, deleteProviderCustomer } from "./provider-cleanup.ts";
import type { SupabaseClient } from "jsr:@supabase/supabase-js@2";

Deno.test("provider cleanup treats success and missing customers as completed", async () => {
  for (const status of [200, 404]) {
    let calls = 0;
    const send: typeof fetch = (url, init) => {
      calls++;
      if (!String(url).endsWith("/old-id") || init?.method !== "DELETE") {
        throw new Error("wrong provider deletion request");
      }
      return Promise.resolve(new Response("{}", { status }));
    };
    if (!await deleteProviderCustomer("old-id", "test-secret", send) || calls !== 1) {
      throw new Error("deletion did not complete");
    }
  }
});

Deno.test("provider errors and unavailable credentials remain retryable", async () => {
  for (const status of [401, 429, 500]) {
    const send: typeof fetch = () => Promise.resolve(new Response("", { status }));
    if (await deleteProviderCustomer("old-id", "test-secret", send)) {
      throw new Error(`HTTP ${status} must keep the job`);
    }
  }
  const offline: typeof fetch = () => Promise.reject(new Error("offline"));
  if (await deleteProviderCustomer("old-id", "test-secret", offline)) throw new Error("lost retry");
  let called = false;
  const unexpected: typeof fetch = () => { called = true; throw new Error("must not call"); };
  if (await deleteProviderCustomer("old-id", undefined, unexpected) || called) {
    throw new Error("missing credentials must keep job without issuing request");
  }
});

Deno.test("recovery cleanup retains jobs and auth on outage, then clears both on retry", async () => {
  let pending = true;
  let authDeletes = 0;
  let providerStatus = 503;
  const originalFetch = globalThis.fetch;
  globalThis.fetch = () => Promise.resolve(new Response("{}", { status: providerStatus }));
  const admin = {
    from: () => ({
      select: (_columns: string, options?: unknown) => options
        ? { eq: () => Promise.resolve({ count: pending ? 1 : 0, error: null }) }
        : { order: () => ({ limit: () => Promise.resolve({
          data: pending ? [{ user_id: "old", delete_auth: true }] : [], error: null,
        }) }) },
      delete: () => ({ eq: () => { pending = false; return Promise.resolve({ error: null }); } }),
    }),
    auth: { admin: { deleteUser: () => { authDeletes++; return Promise.resolve({ error: null }); } } },
  } as unknown as SupabaseClient;
  try {
    if (await cleanupProviderIdentities(admin, "new", "test-secret") || !pending || authDeletes !== 0) {
      throw new Error("outage discarded provider identity or old auth user");
    }
    providerStatus = 200;
    if (!await cleanupProviderIdentities(admin, "new", "test-secret") || pending || Number(authDeletes) !== 1) {
      throw new Error("retry did not complete provider/auth cleanup");
    }
  } finally { globalThis.fetch = originalFetch; }
});

Deno.test("cleanup transport exception cannot suppress a committed recovery response", async () => {
  const admin = { from: () => { throw new Error("scripted transport failure"); } } as unknown as SupabaseClient;
  if (await cleanupProviderIdentities(admin, "new", "test-secret")) {
    throw new Error("transport failure incorrectly marked complete");
  }
});
