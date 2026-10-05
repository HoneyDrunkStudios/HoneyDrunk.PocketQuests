import test from "node:test";
import assert from "node:assert/strict";
import harness from "./provider-harness.cjs";
const { account, createHarness } = harness;
test("reauthentication replaces a damaged display snapshot while preserving unverified pending intent", async () => {
  const cached = account();
  cached.requiresReload = true;
  cached.unverified = [
    {
      command: {
        operationId: "unverified",
        action: "complete",
        occurrenceId: "o",
      },
      reason: "Timing uncertain",
      blockedBy: [],
    },
  ];
  const host = createHarness(cached);
  await host.mount();
  assert.equal(host.render().signedIn, true);
  assert.equal(host.disk.requiresReload, undefined);
  assert.deepEqual(host.disk.unverified, cached.unverified);
  assert.deepEqual(host.sent, []);
});
test("a quarantined recovery copy requires explicit discard before account switching or logout", async () => {
  const host = createHarness(account());
  host.cacheNotice = "Unreadable actions retained";
  await host.mount();
  await host.render().signOut();
  assert.equal(host.render().recoveryRequired, true);
  assert.equal(host.render().queuedCount, 0);
  assert.equal(host.render().signedIn, true);
  host.identityUser = "other";
  await assert.rejects(host.render().connect("other-token"), /recovery copy/);
  assert.equal(host.savedSession.userId, "owner");
  await host.render().discardPending();
  assert.equal(host.cacheNotice, null);
  await host.render().signOut();
  assert.equal(host.savedSession, null);
});

test("quarantine-only recovery without a readable account exposes explicit discard and logout", async () => {
  const host = createHarness(account());
  host.disk = null;
  host.online = false;
  host.cacheNotice = "Unreadable actions retained";
  await host.mount();
  assert.equal(host.render().state, null);
  assert.equal(host.render().recoveryRequired, true);
  await host.render().signOut();
  assert.notEqual(host.savedSession, null);
  await host.render().discardPending();
  assert.equal(host.cacheNotice, null);
  assert.equal(host.render().recoveryRequired, false);
  await host.render().signOut();
  assert.equal(host.savedSession, null);
});

test("an inactive different account cannot clear the current account's recovery copy", async () => {
  const host = createHarness(account());
  host.cacheNotice = "Unreadable actions retained";
  await host.mount();
  host.respond = async (url) => {
    if (url.endsWith("/users/me")) return { ok: false, status: 401 };
    if (url.endsWith("/users/me/status"))
      return {
        ok: true,
        json: async () => ({ userId: "other", state: "PendingDeletion" }),
      };
  };
  await assert.rejects(host.render().connect("other-token"), /recovery copy/);
  assert.equal(host.savedSession.userId, "owner");
  assert.equal(host.disk.userId, "owner");
  assert.equal(host.cacheNotice, "Unreadable actions retained");
});
