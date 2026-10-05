import test from "node:test";
import assert from "node:assert/strict";
import Module, { createRequire } from "node:module";
import harness from "./provider-harness.cjs";
const require = createRequire(import.meta.url);
const { createAuthSession } = require("../../src/session/auth-session.ts");
const {
  renewProviderSession,
  verifyProviderSession,
} = require("../../src/session/provider-session.ts");
const { requestTimeoutMs } = require("../../src/config/client.ts");
const { account, createHarness } = harness;
const deferred = () => {
  let resolve;
  const promise = new Promise((done) => {
    resolve = done;
  });
  return { promise, resolve };
};
const config = {
  authority: "https://issuer.example.com",
  clientId: "mobile",
  scope: "quests",
};
const saved = {
  ...config,
  userId: "owner",
  token: "old",
  refreshToken: "refresh-1",
  expiresAt: 0,
};
const replacement = () => ({
  accessToken: "new",
  refreshToken: "refresh-2",
  issuedAt: Date.now() / 1000,
  expiresIn: 3600,
});
const discovery = { tokenEndpoint: "https://issuer.example.com/token" };
let discover = async () => discovery;
let refresh = async () => replacement();
const original = Module._load;
Module._load = function (name, ...rest) {
  if (name === "expo-auth-session")
    return {
      fetchDiscoveryAsync: (...args) => discover(...args),
      refreshAsync: (...args) => refresh(...args),
    };
  return original.call(this, name, ...rest);
};
requestTimeoutMs.signInConfiguration = 60;
requestTimeoutMs.renewal = 100;
requestTimeoutMs.api = 60;

function rotationFixture() {
  let persisted = saved;
  let grants = 0;
  let online = false;
  let owner = "owner";
  discover = async () => discovery;
  refresh = async (input) => {
    grants++;
    if (grants > 1 && input.refreshToken === "refresh-1")
      throw { code: "invalid_grant" };
    return replacement();
  };
  global.fetch = async (url) =>
    url.endsWith("/client-configuration")
      ? { ok: true, json: async () => config }
      : online
        ? { ok: true, json: async () => ({ userId: owner, state: "Active" }) }
        : { ok: false, status: 503 };
  const make = () => {
    const auth = createAuthSession({
      changed() {},
      save: async (value) => {
        persisted = structuredClone(value);
      },
      renew: renewProviderSession,
      verify: verifyProviderSession,
    });
    if (persisted) auth.restore(structuredClone(persisted));
    return auth;
  };
  return {
    make,
    get persisted() {
      return persisted;
    },
    get grants() {
      return grants;
    },
    recover: (id = "owner") => {
      online = true;
      owner = id;
    },
  };
}

test("rotated credential survives Identity 503 and restart without resending a consumed token", async () => {
  const host = rotationFixture();
  const auth = host.make();
  let sent = 0;
  await assert.rejects(
    auth.run(async () => sent++),
    (error) => error.status === 503,
  );
  assert.equal(sent, 0);
  assert.equal(host.persisted.token, "old");
  assert.equal(host.persisted.renewal.credentials.refreshToken, "refresh-2");
  host.recover();
  const restarted = host.make();
  assert.equal(await restarted.run(async (token) => token), "new");
  assert.equal(host.grants, 1);
  assert.equal(host.persisted.refreshToken, "refresh-2");
  assert.equal(host.persisted.renewal, undefined);
});

test("an unverified candidate cannot authorize a different owner and is removed by logout or reauth", async () => {
  for (const replacementSession of [
    null,
    { userId: "owner", token: "interactive" },
  ]) {
    const host = rotationFixture();
    const auth = host.make();
    await assert.rejects(
      auth.run(async () => assert.fail("unverified request")),
    );
    host.recover("other");
    await assert.rejects(
      auth.run(async () => assert.fail("wrong-owner request")),
      /active account/,
    );
    await auth.install(replacementSession);
    assert.deepEqual(host.persisted, replacementSession);
    assert.equal(host.grants, 1);
  }
});

async function providerHost() {
  discover = async () => discovery;
  refresh = async () => replacement();
  const cached = account();
  cached.unverified = [
    {
      command: {
        operationId: "pending",
        action: "complete",
        occurrenceId: "o",
      },
      reason: "Unknown timing",
      blockedBy: [],
    },
  ];
  const host = createHarness(cached);
  await host.mount();
  await host.render().connect({ ...saved, expiresAt: Date.now() + 3600000 });
  host.respond = async (url, options) => {
    if (url.endsWith("/client-configuration"))
      return { ok: true, json: async () => config };
    if (
      url.endsWith("/api/state") &&
      options.headers.Authorization === "Bearer old"
    )
      return { ok: false, status: 401, json: async () => ({}) };
  };
  return host;
}

test("stalled discovery releases busy state, preserves pending intent, and permits a successful retry", async () => {
  const host = await providerHost();
  const gate = deferred();
  const pending = structuredClone(host.disk.unverified);
  let grants = 0;
  discover = () => gate.promise;
  refresh = async () => {
    grants++;
    return replacement();
  };
  await host.render().refresh();
  assert.equal(host.render().busy, false);
  assert.match(host.render().error, /timed out/);
  assert.deepEqual(host.disk.unverified, pending);
  discover = async () => discovery;
  await host.render().retry();
  assert.equal(host.savedSession.token, "new");
  gate.resolve(discovery);
  await new Promise(setImmediate);
  assert.equal(grants, 1, "late discovery cannot start another exchange");
});

test("stalled exchange times out; retries join one exchange and late credentials stay unverified", async () => {
  const host = await providerHost();
  const gate = deferred();
  let grants = 0;
  refresh = async () => {
    grants++;
    return gate.promise;
  };
  await host.render().refresh();
  assert.equal(host.render().busy, false);
  await host.render().retry();
  assert.equal(grants, 1);
  const calls = host.requests.length;
  gate.resolve(replacement());
  await new Promise(setImmediate);
  assert.equal(host.requests.length, calls);
  assert.equal(host.savedSession.token, "old");
  assert.equal(host.savedSession.renewal.credentials.token, "new");
  await host.render().retry();
  assert.equal(host.savedSession.token, "new");
  assert.equal(grants, 1);
});

for (const phase of ["discovery", "exchange"])
  test(`explicit logout interrupts stalled ${phase} and rejects late completion`, async () => {
    const host = await providerHost();
    const gate = deferred();
    const entered = deferred();
    if (phase === "discovery")
      discover = async () => {
        entered.resolve();
        return gate.promise;
      };
    else
      refresh = async () => {
        entered.resolve();
        return gate.promise;
      };
    const pending = host.render().refresh();
    await entered.promise;
    assert.equal(host.render().busy, true);
    await host.render().signOut(true);
    await pending;
    assert.equal(host.savedSession, null);
    assert.equal(host.disk, null);
    assert.equal(host.render().busy, false);
    const calls = host.requests.length;
    gate.resolve(phase === "discovery" ? discovery : replacement());
    await new Promise(setImmediate);
    assert.equal(host.savedSession, null);
    assert.equal(host.requests.length, calls);
    assert.equal(host.render().signedIn, false);
  });

test("logout drains an already-started cache write before clearing private storage", async () => {
  const host = await providerHost();
  host.respond = undefined;
  const entered = deferred();
  const gate = deferred();
  host.beforeSave = async () => {
    entered.resolve();
    await gate.promise;
  };
  const pending = host.render().refresh();
  await entered.promise;
  const logout = host.render().signOut(true);
  gate.resolve();
  await Promise.all([pending, logout]);
  assert.equal(host.savedSession, null);
  assert.equal(host.disk, null);
});

test("verification timeout retains a candidate; concurrent retry verifies it once and never renews the old token", async () => {
  const gate = deferred();
  let persisted = saved;
  let verifies = 0;
  let grants = 0;
  let healthy = false;
  const auth = createAuthSession({
    changed() {},
    save: async (value) => {
      persisted = value;
    },
    renew: async () => {
      grants++;
      return { ...saved, token: "new", refreshToken: "refresh-2" };
    },
    verify: async () => {
      verifies++;
      if (!healthy) await gate.promise;
    },
  });
  auth.restore(saved);
  await assert.rejects(
    auth.run(async () => assert.fail("unverified request")),
    (error) => error.status === 408,
  );
  assert.equal(persisted.renewal.credentials.refreshToken, "refresh-2");
  healthy = true;
  assert.deepEqual(
    await Promise.all(
      Array.from({ length: 5 }, () => auth.run(async (token) => token)),
    ),
    Array(5).fill("new"),
  );
  gate.resolve();
  assert.equal(grants, 1);
  assert.equal(verifies, 2);
});

test("logout and same-owner reauthentication invalidate a verification still awaiting its response", async () => {
  for (const replacementSession of [
    null,
    { userId: "owner", token: "interactive" },
  ]) {
    const entered = deferred();
    const gate = deferred();
    let persisted;
    const auth = createAuthSession({
      changed() {},
      save: async (value) => {
        persisted = value;
      },
      renew: async () => ({ ...saved, token: "candidate" }),
      verify: async () => {
        entered.resolve();
        await gate.promise;
      },
    });
    auth.restore(saved);
    const pending = assert.rejects(
      auth.run(async () => assert.fail("stale request")),
      /Sign in again/,
    );
    await entered.promise;
    await auth.install(replacementSession);
    await pending;
    gate.resolve();
    await new Promise(setImmediate);
    assert.deepEqual(persisted, replacementSession);
  }
});

test("restart after an unresolved exchange requires reauthentication without replaying a consumed refresh token", async () => {
  const auth = createAuthSession({
    changed() {},
    save: async () => {},
    renew: async () => assert.fail("cannot replay an unresolved exchange"),
    verify: async () => assert.fail("no candidate received"),
  });
  auth.restore({ ...saved, renewal: {} });
  await assert.rejects(
    auth.run(async () => assert.fail("uncertain request")),
    /Sign in again/,
  );
});

test("a candidate secure-write failure cannot restore and replay the consumed refresh token", async () => {
  let persisted;
  let grants = 0;
  const auth = createAuthSession({
    changed() {},
    save: async (value) => {
      if (value?.renewal?.credentials) throw new Error("secure write failed");
      persisted = value;
    },
    renew: async () => {
      grants++;
      return { ...saved, token: "candidate" };
    },
    verify: async () => assert.fail("candidate was not secured"),
  });
  auth.restore(saved);
  await assert.rejects(
    auth.run(async () => {}),
    /secure write failed/,
  );
  assert.deepEqual(persisted.renewal, {});
  await assert.rejects(
    auth.run(async () => {}),
    /Sign in again/,
  );
  assert.equal(grants, 1);
});

test("ordinary logout preserves a recovery copy discovered while an existing local write finishes", async () => {
  const host = createHarness(account());
  await host.mount();
  const entered = deferred();
  const gate = deferred();
  host.beforeSave = async () => {
    entered.resolve();
    await gate.promise;
  };
  const pending = host.render().refresh();
  await entered.promise;
  const logout = host.render().signOut();
  host.cacheNotice = "Unreadable actions discovered during sign-out";
  gate.resolve();
  await Promise.all([pending, logout]);
  assert.equal(host.savedSession, null);
  assert.notEqual(host.disk, null);
  assert.match(host.cacheNotice, /Unreadable actions/);
  assert.equal(host.render().recoveryRequired, true);
  assert.equal(host.render().queuedCount, 0);
  await host.render().signOut(true);
  assert.equal(host.disk, null);
  assert.equal(host.cacheNotice, null);
});
