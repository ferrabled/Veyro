// Pings public.ping() (a `select 1` RPC) through PostgREST. Real database traffic is what
// resets Supabase's free-tier inactivity counter; a ping every 3 days makes an inactivity
// pause very unlikely, but it is NOT an official guarantee — Supabase does not document
// cron traffic as pause-proof, and only the paid plan removes pausing entirely (owner
// decision, not assumed here).
//
// Least privilege: the publishable key in the `apikey` header is all PostgREST needs for a
// function granted to `anon` — no bearer token. A failed ping THROWS, so the invocation is
// recorded as an error in the Worker's metrics and cron-event log (that is the practical
// failure signal: Cloudflare dashboard -> Worker -> Metrics/Logs shows failed invocations).

export interface Env {
  SUPABASE_URL: string;
  SUPABASE_ANON_KEY: string;
}

export default {
  async scheduled(
    _event: ScheduledEvent,
    env: Env,
    _ctx: ExecutionContext,
  ): Promise<void> {
    const response = await fetch(`${env.SUPABASE_URL}/rest/v1/rpc/ping`, {
      method: "POST",
      headers: {
        apikey: env.SUPABASE_ANON_KEY,
        "Content-Type": "application/json",
      },
      body: "{}",
      signal: AbortSignal.timeout(10_000),
    });
    if (!response.ok) {
      throw new Error(`keepalive ping failed: HTTP ${response.status}`);
    }
    const body = await response.text();
    if (body.trim() !== "1") {
      throw new Error(`keepalive ping unexpected body: ${body.slice(0, 100)}`);
    }
  },
};
