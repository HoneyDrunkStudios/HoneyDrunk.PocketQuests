import test from "node:test";
import assert from "node:assert/strict";
import { createRequire } from "node:module";
import "./provider-harness.cjs";
const require = createRequire(import.meta.url);
const {
  requestIdentityUser,
  requestProviderConfiguration,
} = require("../../src/session/identity-client.ts");
const { requestQuestExport } = require("../../src/api/client.ts");
const { identityUrl, apiUrl } = require("../../src/config/client.ts");

test("transport adapters preserve responses and leave caller status/owner policy intact", async (t) => {
  const response = { ok: false, status: 401 };
  const calls = [];
  t.mock.method(globalThis, "fetch", async (...args) => {
    calls.push(args);
    return response;
  });
  assert.equal(await requestIdentityUser("candidate-token"), response);
  assert.equal(await requestQuestExport("candidate-token", "csv"), response);
  assert.deepEqual(
    calls.map(([url]) => url),
    [identityUrl + "/users/me", apiUrl + "/api/export/csv"],
  );
  for (const [, init] of calls) {
    assert.deepEqual(init.headers, { Authorization: "Bearer candidate-token" });
    assert.ok(init.signal instanceof AbortSignal);
    assert.equal(init.signal.aborted, false);
  }
});

test("configuration adapter keeps the exact setup cancellation signal and no credentials", async (t) => {
  const controller = new AbortController();
  t.mock.method(globalThis, "fetch", async (url, init) => {
    assert.equal(url, identityUrl + "/client-configuration");
    assert.equal(init.signal, controller.signal);
    assert.equal(init.headers, undefined);
    controller.abort();
    init.signal.throwIfAborted();
  });
  await assert.rejects(requestProviderConfiguration(controller.signal), {
    name: "AbortError",
  });
});

test("wire adapters propagate the original network failure without translating it", async (t) => {
  const error = new TypeError("offline");
  t.mock.method(globalThis, "fetch", async () => {
    throw error;
  });
  for (const request of [
    () => requestIdentityUser("token"),
    () => requestQuestExport("token", "json"),
    () => requestProviderConfiguration(),
  ]) {
    await assert.rejects(request(), (actual) => actual === error);
  }
});
