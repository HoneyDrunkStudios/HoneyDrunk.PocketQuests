import test from "node:test";
import assert from "node:assert/strict";
import Module, { createRequire } from "node:module";
const require = createRequire(import.meta.url),
  ts = require("typescript");
const original = Module._load;
Module._load = function (name, ...rest) {
  if (name === "../api/client") return { requestQuestExport: async () => {} };
  if (name === "../features/profile/export-download")
    return { saveExport: async () => {} };
  if (name === "./identity-client") return { identityRequest: async () => {} };
  return original.call(this, name, ...rest);
};
require.extensions[".ts"] = (module, file) =>
  module._compile(
    ts.transpileModule(require("node:fs").readFileSync(file, "utf8"), {
      compilerOptions: {
        module: ts.ModuleKind.CommonJS,
        target: ts.ScriptTarget.ES2022,
      },
    }).outputText,
    file,
  );
const {
  createSessionRequests,
} = require("../../src/session/session-requests.ts");
function fixture(client) {
  const auth = { session: { token: "fixture" }, epoch: 1 };
  const inFlight = { current: false },
    signingOut = { current: false },
    mounted = { current: true },
    busy = [];
  const actions = createSessionRequests({
    auth,
    inFlight,
    signingOut,
    mounted,
    client,
    setBusy: (value) => busy.push(value),
    setError: () => {},
    finishWork: () => {
      inFlight.current = false;
    },
    localRef: { current: null },
    drain: async () => {},
    forgetInactive: async () => {},
    failure: () => {},
  });
  return { auth, inFlight, signingOut, mounted, busy, actions };
}
test("discovery writes share the account operation gate and preserve their exact request", async () => {
  let finish, seen;
  const f = fixture((...args) => {
    seen = args;
    return new Promise((resolve) => {
      finish = resolve;
    });
  });
  const request = {
    id: "fixture-place",
    expectedRevision: 3,
    notes: "Keep these notes",
  };
  const pending = f.actions.saveDiscovery(request);
  assert.equal(f.inFlight.current, true);
  await assert.rejects(
    f.actions.removeDiscovery({ id: "fixture-place", expectedRevision: 3 }),
    /current action/,
  );
  assert.equal(seen[0], "POST /api/discoveries/save");
  assert.equal(seen[1], request);
  finish({ revision: 4 });
  assert.deepEqual(await pending, { revision: 4 });
  assert.equal(f.inFlight.current, false);
  assert.deepEqual(f.busy, [true, false]);
});
test("old-account discovery responses are rejected after an owner change", async () => {
  let finish;
  const f = fixture(
    () =>
      new Promise((resolve) => {
        finish = resolve;
      }),
  );
  const pending = f.actions.readDiscoveries();
  f.auth.epoch++;
  finish([{ notes: "old account private notes" }]);
  await assert.rejects(pending, /session changed/);
  f.signingOut.current = true;
  await assert.rejects(f.actions.saveDiscovery({}), /Connect/);
});
test("failed discovery writes release the operation gate and never silently retry", async () => {
  let calls = 0;
  const f = fixture(async () => {
    calls++;
    throw new Error("Uncertain response");
  });
  await assert.rejects(f.actions.saveDiscovery({}), /Uncertain response/);
  assert.equal(calls, 1);
  assert.equal(f.inFlight.current, false);
  assert.deepEqual(f.busy, [true, false]);
});
