import test from "node:test";
import assert from "node:assert/strict";
import harness from "./provider-harness.cjs";
const { account, createHarness } = harness;
const deferred = () => {
  let resolve;
  const promise = new Promise((done) => {
    resolve = done;
  });
  return { promise, resolve };
};
function ownerHost() {
  const host = createHarness(account());
  host.accounts = {
    owner: account(),
    other: {
      ...account(),
      userId: "other",
      queue: [{ operationId: "other-private" }],
    },
  };
  host.recoveryCopies = {};
  host.storageEvents = [];
  return host;
}
async function retainedAfterLogout() {
  const host = ownerHost();
  await host.mount();
  host.storageEvents = [];
  const entered = deferred(),
    gate = deferred();
  host.beforeSave = async () => {
    entered.resolve();
    await gate.promise;
  };
  const pending = host.render().refresh();
  await entered.promise;
  const logout = host.render().signOut();
  host.recoveryCopies.owner = "Unreadable owner A actions retained";
  gate.resolve();
  await Promise.all([pending, logout]);
  host.beforeSave = undefined;
  assert.equal(host.savedSession, null);
  assert.equal(host.cleanupOwner, "owner");
  assert.equal(host.render().recoveryRequired, true);
  assert.equal(host.render().queuedCount, 0);
  return host;
}
async function assertOtherBlocked(host) {
  host.identityUser = "other";
  const original = structuredClone(host.accounts);
  const before = host.requests.length;
  await assert.rejects(
    host.render().connect("other-token"),
    /recovery copy|private storage cleanup/,
  );
  assert.deepEqual(host.accounts, original);
  assert.equal(host.savedSession?.userId === "other", false);
  assert.equal(host.render().recoveryRequired, true);
  assert.equal(host.render().busy, false);
  assert.ok(
    host.requests
      .slice(before)
      .every(
        (request) =>
          request.url.endsWith("/users/me") ||
          request.url.endsWith("/users/me/status"),
      ),
  );
}

test("retained owner blocks another account after ordinary logout without hiding recovery controls", async () => {
  const host = await retainedAfterLogout();
  await assertOtherBlocked(host);
  assert.equal(
    host.recoveryCopies.owner,
    "Unreadable owner A actions retained",
  );
  await host.render().signOut();
  assert.equal(host.render().recoveryRequired, true);
  await host.render().signOut(true);
  assert.equal(host.cleanupOwner, null);
  assert.equal(host.recoveryCopies.owner, undefined);
  assert.equal(host.accounts.other.queue[0].operationId, "other-private");
});

test("same-owner reconnect keeps recovery visible and hands ownership back to the durable session", async () => {
  const host = await retainedAfterLogout();
  await host.render().connect("owner-token");
  assert.equal(host.savedSession.userId, "owner");
  assert.equal(host.cleanupOwner, null);
  assert.equal(host.render().recoveryRequired, true);
  assert.ok(host.recoveryCopies.owner);
  await assertOtherBlocked(host);
});

for (const action of ["discardPending", "signOut"])
  test(`explicit ${action} resolves retained ownership and permits another account`, async () => {
    const host = await retainedAfterLogout();
    await host.render()[action](true);
    assert.equal(host.cleanupOwner, null);
    assert.equal(host.recoveryCopies.owner, undefined);
    host.identityUser = "other";
    // This control's target has no pending commands to drain.
    host.accounts.other.queue = [];
    await host.render().connect("other-token");
    assert.equal(host.savedSession.userId, "other");
    assert.equal(host.render().recoveryRequired, false);
  });

for (const status of [200, 401])
  test(`inactive different-owner response (${status}) cannot bypass retained ownership`, async () => {
    const host = await retainedAfterLogout();
    host.respond = async (url) => {
      if (url.endsWith("/users/me"))
        return status === 401
          ? { ok: false, status: 401 }
          : {
              ok: true,
              json: async () => ({ userId: "other", state: "Inactive" }),
            };
      if (url.endsWith("/users/me/status"))
        return {
          ok: true,
          json: async () => ({ userId: "other", state: "PendingDeletion" }),
        };
    };
    await assertOtherBlocked(host);
    assert.equal(host.render().inactiveAccount, null);
  });

test("retained ownership restores after restart with no authentication or readable LocalAccount", async () => {
  const previous = await retainedAfterLogout();
  assert.deepEqual(previous.storageEvents.slice(0, 2), [
    { kind: "cleanup-owner", value: "owner" },
    { kind: "session", value: null },
  ]);
  const host = ownerHost();
  host.savedSession = null;
  host.cleanupOwner = previous.cleanupOwner;
  host.recoveryCopies = structuredClone(previous.recoveryCopies);
  delete host.accounts.owner;
  host.disk = null;
  await host.mount();
  assert.equal(host.requests.length, 0, "a retained owner never authenticates");
  assert.equal(host.render().state, null);
  assert.equal(host.render().recoveryRequired, true);
  await assertOtherBlocked(host);
  await host.render().discardPending();
  assert.equal(host.cleanupOwner, null);
  assert.equal(host.recoveryCopies.owner, undefined);
  assert.equal(host.render().recoveryRequired, false);
});

test("failed explicit cleanup retains its owner even if the transient recovery notice is lost", async () => {
  const host = await retainedAfterLogout();
  host.hiddenNotices = new Set();
  host.failClear = true;
  await host.render().signOut(true);
  assert.ok(host.hiddenNotices.has("owner"));
  assert.ok(host.recoveryCopies.owner);
  assert.equal(host.render().recoveryRequired, true);
  await assertOtherBlocked(host);
  host.failClear = false;
  await host.render().signOut(true);
  assert.equal(host.cleanupOwner, null);
  assert.equal(host.recoveryCopies.owner, undefined);
  assert.equal(host.render().recoveryRequired, false);
});

test("failed discard preserves the owner and lifecycle actions cannot bypass recovery", async () => {
  const host = await retainedAfterLogout();
  host.failDiscardRecovery = true;
  await host.render().discardPending();
  assert.ok(host.recoveryCopies.owner);
  await assertOtherBlocked(host);
  const before = host.requests.length;
  await assert.rejects(
    host.render().accountAction("deletion", "other-token"),
    /explicitly discard/,
  );
  assert.equal(host.requests.length, before);
  host.failDiscardRecovery = false;
  await host.render().discardPending();
  assert.equal(host.cleanupOwner, null);
});

for (const state of ["Active", "Inactive"])
  test(`late ${state} sign-in JSON after logout cannot replace or clear retained ownership`, async () => {
    const host = ownerHost();
    await host.mount();
    const entered = deferred(),
      gate = deferred();
    host.respond = async (url) =>
      url.endsWith("/users/me")
        ? {
            ok: true,
            json: async () => {
              entered.resolve();
              await gate.promise;
              return { userId: "other", state };
            },
          }
        : undefined;
    const pending = assert.rejects(
      host.render().connect("other-token"),
      /Sign in again/,
    );
    await entered.promise;
    const logout = host.render().signOut();
    host.recoveryCopies.owner = "Late owner A recovery";
    gate.resolve();
    await Promise.all([pending, logout]);
    assert.equal(host.savedSession, null);
    assert.equal(host.cleanupOwner, "owner");
    assert.equal(host.render().recoveryRequired, true);
    assert.ok(host.recoveryCopies.owner);
    assert.ok(host.accounts.other);
  });

test("logout wins over an in-flight same-owner handoff clearing the cleanup marker", async () => {
  const host = ownerHost();
  await host.mount();
  const entered = deferred(),
    gate = deferred();
  let held = false;
  host.beforeCleanupSave = async (value) => {
    if (value === null && !held) {
      held = true;
      entered.resolve();
      await gate.promise;
    }
  };
  const pending = assert.rejects(
    host.render().connect("owner-token"),
    /Sign in again/,
  );
  await entered.promise;
  const logout = host.render().signOut();
  host.recoveryCopies.owner = "Late owner A recovery";
  gate.resolve();
  await Promise.all([pending, logout]);
  assert.equal(host.savedSession, null);
  assert.equal(host.cleanupOwner, "owner");
  assert.equal(host.render().recoveryRequired, true);
  await assertOtherBlocked(host);
});

test("failed owner-marker persistence does not erase the only durable account identity", async () => {
  const host = ownerHost();
  await host.mount();
  host.failCleanupSave = true;
  await host.render().signOut(true);
  assert.equal(host.savedSession.userId, "owner");
  assert.equal(host.render().signedIn, false);
  assert.equal(host.render().recoveryRequired, true);
  await assertOtherBlocked(host);
  host.failCleanupSave = false;
  await host.render().signOut(true);
  assert.equal(host.savedSession, null);
  assert.equal(host.cleanupOwner, null);
});

test("a delayed inactive-status response cannot clear recovery discovered during ordinary logout", async () => {
  const host = ownerHost();
  await host.mount();
  const entered = deferred(),
    gate = deferred();
  host.respond = async (url) => {
    if (url.endsWith("/api/state"))
      return { ok: false, status: 401, json: async () => ({}) };
    if (url.endsWith("/users/me/status")) {
      entered.resolve();
      await gate.promise;
      return {
        ok: true,
        json: async () => ({ userId: "owner", state: "PendingDeletion" }),
      };
    }
  };
  const pending = host.render().refresh();
  await entered.promise;
  const logout = host.render().signOut();
  host.recoveryCopies.owner = "Late owner A recovery";
  gate.resolve();
  await Promise.all([pending, logout]);
  assert.equal(host.savedSession, null);
  assert.equal(host.cleanupOwner, "owner");
  assert.ok(host.recoveryCopies.owner);
  assert.equal(host.render().inactiveAccount, null);
});

test("failure opening a known owner's cache retains explicit cleanup without permitting another account", async () => {
  const host = ownerHost();
  host.failLoad = true;
  await host.mount();
  assert.equal(host.render().recoveryRequired, true);
  host.identityUser = "other";
  await assert.rejects(
    host.render().connect("other-token"),
    /Private storage must be reopened/,
  );
  await host.render().signOut(true);
  assert.equal(host.savedSession, null);
  assert.equal(host.accounts.owner, undefined);
  assert.ok(host.accounts.other);
  assert.equal(host.cleanupOwner, null);
});

test("logout while the initial cache read is pending keeps its known owner and ignores a late failure", async () => {
  const host = ownerHost();
  const entered = deferred(),
    gate = deferred();
  host.beforeLoad = async () => {
    entered.resolve();
    await gate.promise;
    throw new Error("Late cache read failure");
  };
  const mount = host.mount();
  await entered.promise;
  await host.render().signOut(true);
  gate.resolve();
  await mount;
  assert.equal(host.savedSession, null);
  assert.equal(host.accounts.owner, undefined);
  assert.equal(host.cleanupOwner, null);
  assert.equal(host.render().recoveryRequired, false);
  assert.equal(host.render().error, null);
});

test("an earlier discard cannot release ownership retained by a newer ordinary logout", async () => {
  const host = ownerHost();
  await host.mount();
  host.recoveryCopies.owner = "Original owner A copy";
  const entered = deferred(),
    gate = deferred();
  host.beforeSave = async () => {
    entered.resolve();
    await gate.promise;
  };
  const discard = host.render().discardPending();
  await entered.promise;
  assert.equal(host.recoveryCopies.owner, undefined);
  const logout = host.render().signOut();
  host.recoveryCopies.owner = "Late owner A recovery";
  gate.resolve();
  await Promise.all([discard, logout]);
  assert.equal(host.savedSession, null);
  assert.equal(host.cleanupOwner, "owner");
  assert.equal(host.render().recoveryRequired, true);
  await assertOtherBlocked(host);
});
