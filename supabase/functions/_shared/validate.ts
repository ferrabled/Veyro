// Schema validation for submit-run. Since migration 0004 everything stateful — allowlist,
// seed/date derivation, plausibility, quotas, board upserts, XP — lives in the transactional
// process_run() RPC, so it is atomic and race-free under the per-user advisory lock. This
// file only rejects requests that are not even shaped like a run, BEFORE any database work.

export type RunSubmission = {
  client_run_id: string;
  mode: "daily" | "free";
  seed: number;
  content_version: string;
  world_id: string;
  day_label: string; // client's claim; the server derives run_date from the seed
  input_mode: "tilt" | "camera" | "camera_fallback";
  score: number;
  distance_m: number;
  coins: number;
  best_combo: number;
  duration_s: number;
  app_version: string;
  platform: "android_gp" | "android_galaxy" | "ios";
};

/** Requests larger than this are rejected before JSON parsing. A run is ~400 bytes. */
export const MAX_BODY_BYTES = 4096;

const UUID_RE =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const MODES = ["daily", "free"];
const INPUT_MODES = ["tilt", "camera", "camera_fallback"];
const PLATFORMS = ["android_gp", "android_galaxy", "ios"];
const MAX_TEXT = 32;

// Hard numeric ceilings: anything beyond these is not a run, it is a probe. The DB has its
// own CHECK constraints; rejecting here costs nothing and keeps garbage out of the RPC.
// Seeds are a SIGNED 32-BIT contract for both modes (Free seeds are tick-count derived and
// can be negative or huge — R8); only Daily mode's seed is date-interpreted, in process_run.
const MIN_SEED = -2147483648;
const MAX_SEED = 2147483647;
const MAX_SCORE = 100_000_000;
const MAX_DISTANCE_M = 1_000_000;
const MAX_COINS = 100_000;
const MAX_DURATION_S = 86_400;

function isInt(v: unknown): v is number {
  return typeof v === "number" && Number.isInteger(v);
}
function isNum(v: unknown): v is number {
  return typeof v === "number" && Number.isFinite(v);
}
function isShortText(v: unknown): v is string {
  return typeof v === "string" && v.length > 0 && v.length <= MAX_TEXT;
}

/** Hard schema check. Returns an error string or null. Anything failing here is dropped. */
export function schemaError(body: unknown): string | null {
  if (typeof body !== "object" || body === null) return "body must be an object";
  const b = body as Record<string, unknown>;
  if (typeof b.client_run_id !== "string" || !UUID_RE.test(b.client_run_id)) {
    return "client_run_id must be a uuid";
  }
  if (!MODES.includes(b.mode as string)) return "bad mode";
  if (!isInt(b.seed) || (b.seed as number) < MIN_SEED || (b.seed as number) > MAX_SEED) {
    return "seed out of range";
  }
  if (!isShortText(b.content_version)) return "bad content_version";
  if (!isShortText(b.world_id)) return "bad world_id";
  if (!isShortText(b.day_label)) return "bad day_label";
  if (!INPUT_MODES.includes(b.input_mode as string)) return "bad input_mode";
  if (!isInt(b.score) || (b.score as number) < 0 || (b.score as number) > MAX_SCORE) {
    return "score out of range";
  }
  if (
    !isNum(b.distance_m) || (b.distance_m as number) < 0 ||
    (b.distance_m as number) > MAX_DISTANCE_M
  ) return "distance out of range";
  if (!isInt(b.coins) || (b.coins as number) < 0 || (b.coins as number) > MAX_COINS) {
    return "coins out of range";
  }
  if (
    !isInt(b.best_combo) || (b.best_combo as number) < 0 ||
    (b.best_combo as number) > MAX_COINS
  ) return "best_combo out of range";
  if (
    !isNum(b.duration_s) || (b.duration_s as number) <= 0 ||
    (b.duration_s as number) > MAX_DURATION_S
  ) return "duration out of range";
  if (!isShortText(b.app_version)) return "bad app_version";
  if (!PLATFORMS.includes(b.platform as string)) return "bad platform";
  return null;
}

/**
 * Shared body reader: a BOUNDED BYTE reader (R9). The stream is cancelled the moment it
 * exceeds MAX_BODY_BYTES, so a missing/lying Content-Length or a chunked body can never
 * make the function allocate an unbounded buffer, and the limit is counted in UTF-8 bytes,
 * not JavaScript UTF-16 characters. Returns null on any problem.
 */
export async function readJsonBody(req: Request): Promise<unknown | null> {
  const declared = Number(req.headers.get("content-length") ?? "0");
  if (declared > MAX_BODY_BYTES) return null;
  if (req.body === null) return null;

  const reader = req.body.getReader();
  const chunks: Uint8Array[] = [];
  let total = 0;
  try {
    for (;;) {
      const { done, value } = await reader.read();
      if (done) break;
      total += value.byteLength;
      if (total > MAX_BODY_BYTES) {
        await reader.cancel();
        return null;
      }
      chunks.push(value);
    }
  } catch {
    return null;
  }

  const bytes = new Uint8Array(total);
  let offset = 0;
  for (const chunk of chunks) {
    bytes.set(chunk, offset);
    offset += chunk.byteLength;
  }
  try {
    return JSON.parse(new TextDecoder("utf-8", { fatal: true }).decode(bytes));
  } catch {
    return null;
  }
}
