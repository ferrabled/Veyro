// Operator-only retry for a quiet project or after restoring RC_API_KEY. Never run from
// the client. Credentials are supplied through the environment, never command arguments.
import { createClient } from "jsr:@supabase/supabase-js@2";
import { cleanupProviderIdentities } from "../functions/_shared/provider-cleanup.ts";

function required(name: string): string {
  const value = Deno.env.get(name);
  if (!value) throw new Error(`Missing environment variable: ${name}`);
  return value;
}

const admin = createClient(required("SUPABASE_URL"), required("SUPABASE_SERVICE_ROLE_KEY"));
const key = required("RC_API_KEY");
async function pending(): Promise<number> {
  const { count, error } = await admin.from("provider_cleanup")
    .select("user_id", { count: "exact", head: true });
  if (error || count === null) throw new Error("Could not read pending cleanup count");
  return count;
}

let remaining = await pending();
while (remaining > 0) {
  await cleanupProviderIdentities(admin, "00000000-0000-0000-0000-000000000000", key);
  const next = await pending();
  console.info(`Pending provider cleanup jobs: ${next}`);
  if (next >= remaining) throw new Error("Cleanup made no progress; check provider credentials/connectivity and retry");
  remaining = next;
}
console.info("Provider cleanup queue is empty");
