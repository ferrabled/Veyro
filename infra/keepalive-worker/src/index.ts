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
    event: ScheduledEvent,
    env: Env,
    _ctx: ExecutionContext,
  ): Promise<void> {
    const startedAt = Date.now();
    const details = {
      cron: event.cron,
      scheduledTime: event.scheduledTime,
    };
    let status: number | undefined;
    try {
      const response = await fetch(`${env.SUPABASE_URL}/rest/v1/rpc/ping`, {
        method: "POST",
        headers: {
          apikey: env.SUPABASE_ANON_KEY,
          "Content-Type": "application/json",
        },
        body: "{}",
        signal: AbortSignal.timeout(10_000),
      });
      status = response.status;
      const body = await response.text();
      if (!response.ok) {
        throw new Error(`keepalive ping failed: HTTP ${status}: ${body.slice(0, 300)}`);
      }
      if (body.trim() !== "1") {
        throw new Error(`keepalive ping unexpected body: ${body.slice(0, 100)}`);
      }
      console.info({
        message: "keepalive ping succeeded",
        ...details,
        status,
        durationMs: Date.now() - startedAt,
      });
    } catch (error) {
      console.error({
        message: "keepalive ping failed",
        ...details,
        status,
        durationMs: Date.now() - startedAt,
        error: error instanceof Error ? `${error.name}: ${error.message}` : String(error),
      });
      // Keep the invocation marked as failed in Cloudflare's metrics and cron history.
      throw error;
    }
  },
};
