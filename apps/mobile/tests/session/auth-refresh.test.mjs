import test from "node:test";
import assert from "node:assert/strict";
import { createRequire } from "node:module";
import "./provider-harness.cjs";
const require = createRequire(import.meta.url);
const { createAuthSession } = require("../../src/session/auth-session.ts");
const { RequestError } = require("../../src/session/request-error.ts");
const deferred = () => {
  let resolve;
  const promise = new Promise((r) => {
    resolve = r;
  });
  return { promise, resolve };
};
const old = {
  userId: "owner",
  token: "old",
  refreshToken: "refresh",
  expiresAt: 5000,
};
function fixture(
  renew = async () => ({
    token: "new",
    refreshToken: "rotated",
    expiresAt: 100000,
  }),
) {
  const host = { saved: old, current: old, refreshes: 0 };
  host.auth = createAuthSession({
    verify: async () => {},
    now: () => 5000,
    save: async (value) => {
      host.saved = value;
    },
    changed: (value) => {
      host.current = value;
    },
    renew: async (value) => {
      host.refreshes++;
      return renew(value);
    },
  });
  host.auth.restore(old);
  return host;
}
test("expiry refresh is single-flight across concurrent requests and securely rotates the token", async () => {
  const gate = deferred();
  const host = fixture(async () => {
    await gate.promise;
    return { token: "new", refreshToken: "rotated", expiresAt: 100000 };
  });
  const calls = Array.from({ length: 5 }, () =>
    host.auth.run(async (token) => token),
  );
  gate.resolve();
  assert.deepEqual(await Promise.all(calls), Array(5).fill("new"));
  assert.equal(host.refreshes, 1);
  assert.equal(host.saved.refreshToken, "rotated");
});
test("a 401 refresh retries the identical request once, without looping on a second 401", async () => {
  const host = fixture();
  host.auth.restore({ ...old, expiresAt: undefined });
  const tokens = [];
  await assert.rejects(
    host.auth.run(async (token) => {
      tokens.push(token);
      throw new RequestError(401, "expired");
    }),
    /expired/,
  );
  assert.deepEqual(tokens, ["old", "new"]);
  assert.equal(host.refreshes, 1);
});
test("transient refresh failure preserves the credential for retry", async () => {
  let online = false;
  const host = fixture(async () => {
    if (!online) throw new TypeError("offline");
    return { token: "new", expiresAt: 100000 };
  });
  await assert.rejects(
    host.auth.run(async (token) => token),
    /offline/,
  );
  assert.deepEqual(host.saved, old);
  online = true;
  assert.equal(await host.auth.run(async (token) => token), "new");
});
test("logout during refresh cannot restore a token or send the pending request", async () => {
  const gate = deferred();
  const host = fixture(async () => {
    await gate.promise;
    return { token: "new" };
  });
  let sent = false;
  const call = host.auth.run(async () => {
    sent = true;
  });
  const rejected = assert.rejects(call, /Sign in again/);
  await host.auth.install(null);
  gate.resolve();
  await rejected;
  assert.equal(host.saved, null);
  assert.equal(host.current, null);
  assert.equal(sent, false);
});
test("same-account reauthentication invalidates a stale refresh as well", async () => {
  const gate = deferred();
  const host = fixture(async () => {
    await gate.promise;
    return { token: "stale" };
  });
  const call = host.auth.run(async (token) => token);
  const rejected = assert.rejects(call, /Sign in again/);
  await host.auth.install({ userId: "owner", token: "interactive" });
  gate.resolve();
  await rejected;
  assert.equal(host.saved.token, "interactive");
});
test("a provider without a refresh token requires sign-in without inventing a refresh grant", async () => {
  const host = fixture();
  host.auth.restore({ userId: "owner", token: "old" });
  await assert.rejects(
    host.auth.run(async () => {
      throw new RequestError(401, "expired");
    }),
    /Sign in again/,
  );
  assert.equal(host.refreshes, 0);
});
test("logout waits behind a pending credential write and finishes with cleared storage", async () => {
  const gate = deferred();
  let saved;
  let started;
  const writing = new Promise((r) => {
    started = r;
  });
  const auth = createAuthSession({
    verify: async () => {},
    changed() {},
    renew: async () => ({ token: "new" }),
    save: async (value) => {
      if (value) {
        started();
        await gate.promise;
      }
      saved = value;
    },
  });
  const installing = auth.install(old);
  const rejected = assert.rejects(installing, /Sign in again/);
  await writing;
  const clearing = auth.install(null);
  gate.resolve();
  await rejected;
  await clearing;
  assert.equal(saved, null);
});

test("provider cleanup preserves an unresolved rotation marker and a replacement cannot replay its consumed refresh token", async () => {
  const gate = deferred(),
    entered = deferred();
  const host = fixture(async () => {
    entered.resolve();
    await gate.promise;
    return { token: "late", refreshToken: "rotated" };
  });
  const call = host.auth.run(async (token) => token);
  const rejected = assert.rejects(call, /Sign in again/);
  await entered.promise;
  host.auth.cancel();
  assert.deepEqual(host.saved.renewal, {});
  gate.resolve();
  await rejected;
  await new Promise((resolve) => setImmediate(resolve));
  assert.deepEqual(host.saved.renewal, {});
  const replacement = fixture();
  replacement.auth.restore(host.saved);
  await assert.rejects(
    replacement.auth.run(async (token) => token),
    /Sign in again/,
  );
  assert.equal(replacement.refreshes, 0);
});

test("provider cleanup retains an already-started rotated candidate write for same-owner verification on replacement", async () => {
  const gate = deferred(),
    entered = deferred();
  let saved = old,
    renewals = 0;
  const options = {
    now: () => 5000,
    changed() {},
    verify: async () => {},
    renew: async () => {
      renewals++;
      return { token: "new", refreshToken: "rotated", expiresAt: 100000 };
    },
    save: async (value) => {
      if (value?.renewal?.credentials) {
        entered.resolve();
        await gate.promise;
      }
      saved = value;
    },
  };
  const auth = createAuthSession(options);
  auth.restore(old);
  const call = auth.run(async (token) => token);
  const rejected = assert.rejects(call, /Sign in again/);
  await entered.promise;
  auth.cancel();
  gate.resolve();
  await rejected;
  await new Promise((resolve) => setImmediate(resolve));
  assert.equal(saved.renewal.credentials.refreshToken, "rotated");
  const verified = [];
  const replacement = createAuthSession({
    ...options,
    verify: async (value) => {
      verified.push(value);
    },
  });
  replacement.restore(saved);
  assert.equal(await replacement.run(async (token) => token), "new");
  assert.equal(renewals, 1);
  assert.equal(verified[0].userId, "owner");
  assert.equal(saved.refreshToken, "rotated");
  assert.equal(saved.renewal, undefined);
});
