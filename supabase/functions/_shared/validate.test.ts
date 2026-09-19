// Regression tests for the shared validator (18 Sep review R8/R9).
// Run with: deno test supabase/functions/_shared/validate.test.ts
// Pure unit tests over the validator's own exports — no network, no Supabase project. The
// DEPLOYED backend cannot run them: Edge Functions execute a request handler, not a test
// runner, so verifying these against production would mean firing probe requests that write
// rows and burn submission quota. Keep them here and run them with a local Deno.
// They cover the two reproduced validator defects (18 Sep review R8/R9).

import { MAX_BODY_BYTES, readJsonBody, schemaError } from "./validate.ts";

function assert(cond: boolean, message: string) {
  if (!cond) throw new Error(message);
}

function baseRun(overrides: Record<string, unknown> = {}) {
  return {
    client_run_id: "11111111-2222-3333-4444-555555555555",
    mode: "free",
    seed: 20260918,
    content_version: "greybox-1",
    world_id: "greybox",
    day_label: "2026-09-18",
    input_mode: "tilt",
    score: 100,
    distance_m: 100,
    coins: 0,
    best_combo: 0,
    duration_s: 30,
    app_version: "test",
    platform: "android_gp",
    ...overrides,
  };
}

Deno.test("R8: Free-mode seeds use the signed 32-bit contract", () => {
  // Reproduced defect: -1 and 100,000,000 were rejected although RunSession's Free seeds
  // are tick-count derived and can be any int32.
  assert(schemaError(baseRun({ seed: -1 })) === null, "-1 must be valid");
  assert(schemaError(baseRun({ seed: 100_000_000 })) === null, "100M must be valid");
  assert(schemaError(baseRun({ seed: -2147483648 })) === null, "int32 min must be valid");
  assert(schemaError(baseRun({ seed: 2147483647 })) === null, "int32 max must be valid");
  assert(schemaError(baseRun({ seed: 2147483648 })) !== null, "beyond int32 is a probe");
  assert(schemaError(baseRun({ seed: 1.5 })) !== null, "non-integer is a probe");
});

Deno.test("R9: the body limit is enforced in bytes, streaming", async () => {
  // Reproduced defect: a 6,300-byte body passed because the check compared UTF-16 string
  // length after reading everything. Multi-byte characters must count as their UTF-8 bytes.
  const fat = JSON.stringify({ ...baseRun(), extra: "\u{1F3C3}".repeat(1200) }); // ~5 KB of runners
  assert(new TextEncoder().encode(fat).byteLength > MAX_BODY_BYTES, "fixture must exceed limit");

  const oversized = new Request("http://localhost/", { method: "POST", body: fat });
  assert((await readJsonBody(oversized)) === null, "oversized unicode body must be rejected");

  const fine = new Request("http://localhost/", {
    method: "POST",
    body: JSON.stringify(baseRun()),
  });
  assert((await readJsonBody(fine)) !== null, "a normal run must parse");
});

Deno.test("lying Content-Length does not bypass the byte cap", async () => {
  const big = "x".repeat(MAX_BODY_BYTES * 3);
  const stream = new ReadableStream<Uint8Array>({
    start(controller) {
      controller.enqueue(new TextEncoder().encode(big));
      controller.close();
    },
  });
  const req = new Request("http://localhost/", {
    method: "POST",
    body: stream,
    headers: { "content-length": "10" },
  });
  assert((await readJsonBody(req)) === null, "streamed oversize must be cancelled");
});
