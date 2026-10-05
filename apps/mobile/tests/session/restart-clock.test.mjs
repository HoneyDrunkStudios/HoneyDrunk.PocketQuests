import test from "node:test";
import { strict as assert } from "node:assert";
import harness from "./provider-harness.cjs";
const { account, createHarness, createClockSource } = harness;
const quest = {
  id: "custom",
  title: "Practice",
  criterion: "Do one",
  baseXp: 10,
};
async function offline(host) {
  host.online = false;
  await host.render().refresh();
}
async function complete(host, occurrenceId = "one") {
  await host.render().command({ action: "complete", occurrenceId });
}

test("anchor -> offline process restart -> create/accept/complete/Undo keeps clock continuity and exact replay", async () => {
  const first = createHarness();
  await first.mount();
  await offline(first);
  first.advance(1000);
  await complete(first, "old");
  const original = structuredClone(first.disk.queue[0]);
  const anchor = structuredClone(first.disk.anchor);
  const next = createHarness(first.disk, "new-process");
  next.online = false;
  next.advance(3600000);
  await next.mount();
  await next.render().command({ action: "save-definition", definition: quest });
  await next
    .render()
    .command({ action: "accept", questId: quest.id, occurrenceId: "new" });
  await complete(next, "new");
  await next.render().command({
    action: "undo",
    occurrenceId: "new",
    completionId: next.disk.queue.at(-1).operationId,
  });
  const queued = structuredClone(next.disk.queue);
  assert.equal(queued.length, 5);
  assert.deepEqual(queued[0], original);
  assert.deepEqual(
    queued.map((c) => c.recordedTime.ordinal),
    [1, 2, 3, 4, 5],
  );
  for (const c of queued) {
    assert.equal(c.recordedTime.bootId, anchor.bootId);
    assert.equal(c.recordedTime.anchorId, anchor.id);
    assert.equal("epoch" in c.recordedTime, false);
  }
  assert.equal(queued[1].recordedTime.elapsedMilliseconds, 3601000);
  assert.equal(next.disk.unverified.length, 0);
  assert.equal(next.render().state.overallXp, 0);
  next.online = true;
  await next.render().retry();
  assert.deepEqual(next.sent, queued);
  assert.equal(next.disk.queue.length, 0);
});

test("reboot retains new intent through restart/reconnect without rewriting old proofs or promoting intent", async () => {
  const first = createHarness();
  await first.mount();
  await offline(first);
  first.advance(50);
  await complete(first, "pre-boot");
  const proven = structuredClone(first.disk.queue[0]);
  const reboot = createHarness(first.disk, "rebooted", {
    clockEpoch: "8",
    clockMs: 20,
  });
  reboot.online = false;
  await reboot.mount();
  await complete(reboot, "post-boot");
  const intent = structuredClone(reboot.disk.unverified[0]);
  assert.equal("recordedTime" in intent.command, false);
  assert.equal(intent.clock.epoch, "8");
  assert.match(intent.reason, /restarted|baseline changed/);
  assert.deepEqual(reboot.disk.queue, [proven]);
  assert.equal(reboot.disk.anchor.ordinal, 1);
  const restarted = createHarness(reboot.disk, "again", {
    clockEpoch: "8",
    clockMs: 1020,
  });
  restarted.online = false;
  await restarted.mount();
  assert.deepEqual(restarted.disk.unverified, [intent]);
  restarted.online = true;
  await restarted.render().connect("test-token");
  await restarted.render().retry();
  assert.deepEqual(restarted.sent, [proven]);
  assert.deepEqual(restarted.disk.unverified, [intent]);
  await complete(restarted, "independent-new");
  assert.equal(restarted.sent.length, 2);
  assert.ok(restarted.sent[1].recordedTime);
  await complete(restarted, "post-boot");
  assert.equal(restarted.sent.length, 2);
  assert.deepEqual(restarted.disk.unverified[1].blockedBy, [
    intent.command.operationId,
  ]);
});

test("new boot with larger uptime cannot masquerade as the old boot", async () => {
  const first = createHarness();
  await first.mount();
  const reboot = createHarness(first.disk, "new-boot", {
    clockEpoch: "8",
    clockMs: 2000000,
  });
  reboot.online = false;
  await reboot.mount();
  await complete(reboot);
  assert.equal(reboot.disk.queue.length, 0);
  assert.equal(reboot.disk.unverified.length, 1);
});

test("legacy JS baseline cannot be converted using device wall time", async () => {
  const first = createHarness();
  await first.mount();
  delete first.disk.anchor.clock;
  const next = createHarness(first.disk, "legacy");
  next.online = false;
  await next.mount();
  await complete(next);
  assert.equal(next.disk.queue.length, 0);
  assert.match(
    next.disk.unverified[0].reason,
    /compatible trusted time baseline/,
  );
});

test("clock jumps and backwards readings preserve observations instead of clamping or backdating", async () => {
  for (const change of ["wall", "monotonic"]) {
    const host = createHarness();
    await host.mount();
    await offline(host);
    if (change === "wall") host.wallMs += 121000;
    else host.clockMs -= 1;
    await complete(host);
    assert.equal(host.disk.queue.length, 0);
    assert.equal(
      host.disk.unverified[0].observedUtc,
      new Date(host.wallMs).toISOString(),
    );
    assert.equal(
      host.disk.unverified[0].clock.elapsedMilliseconds,
      host.clockMs,
    );
    assert.equal("recordedTime" in host.disk.unverified[0].command, false);
  }
});

test("unverified create/accept/complete/Undo chain is visible without XP and discard never releases descendants", async () => {
  const host = createHarness(account(), "unsupported", { clockEpoch: null });
  await host.mount();
  await offline(host);
  await host.render().command({ action: "save-definition", definition: quest });
  await host
    .render()
    .command({ action: "accept", questId: quest.id, occurrenceId: "new" });
  await complete(host, "new");
  const completion = host.disk.unverified.at(-1).command;
  assert.equal(
    host.render().state.occurrences[0].pendingTimingVerification,
    true,
  );
  assert.equal(host.render().state.overallXp, 0);
  await host.render().command({
    action: "undo",
    occurrenceId: "new",
    completionId: completion.operationId,
  });
  const retained = structuredClone(host.disk.unverified);
  assert.equal(retained.length, 4);
  assert.equal(host.disk.queue.length, 0);
  assert.ok(retained[3].blockedBy.includes(completion.operationId));
  assert.equal(host.render().recentCompletion, null);
  host.online = true;
  await host.render().retry();
  await host.render().discardUnverified(retained[0].command.operationId);
  assert.deepEqual(host.disk.unverified, retained.slice(1));
  await host.render().retry();
  assert.deepEqual(host.sent, []);
  assert.equal(host.render().state.overallXp, 0);
});

test("unsupported or unavailable native timing is explicitly process-local", async () => {
  for (const platform of ["ios", "web", "android"]) {
    const host = createHarness(account(), platform, { platform });
    host.clockSource = createClockSource();
    await host.mount();
    await complete(host);
    assert.equal(host.disk.queue.length, 0);
    assert.equal(host.disk.unverified.length, 0);
    assert.equal(host.sent.length, 1);
    assert.equal(host.disk.anchor.clock.kind, "js-process-monotonic-v1");
  }
  const host = createHarness();
  host.nativeModule = {
    sample() {
      throw new Error("Counter inaccessible");
    },
  };
  host.clockSource = createClockSource();
  await host.mount();
  await complete(host);
  assert.equal(host.disk.unverified.length, 0);
  assert.equal(host.disk.anchor.clock.kind, "js-process-monotonic-v1");
});

test("Android adapter persists its local epoch but uploads only the anchor token", async () => {
  const host = createHarness();
  host.nativeModule = {
    sample: () => ({
      epoch: "7",
      elapsedMilliseconds: host.clockMs,
      utcMilliseconds: host.wallMs,
    }),
  };
  host.clockSource = createClockSource();
  await host.mount();
  await offline(host);
  host.advance(10000);
  await complete(host);
  assert.equal(host.disk.queue[0].recordedTime.elapsedMilliseconds, 10000);
  assert.equal(host.disk.anchor.clock.epoch, "7");
  assert.notEqual(host.disk.queue[0].recordedTime.bootId, "7");
});

test("committed write with lost response reloads its ordinal before another capture", async () => {
  const host = createHarness();
  await host.mount();
  await offline(host);
  host.failSaveAfterCommit = true;
  await complete(host, "first");
  const first = structuredClone(host.disk.queue[0]);
  assert.equal(host.render().queuedCount, 1);
  host.failSaveAfterCommit = false;
  await complete(host, "second");
  assert.deepEqual(host.disk.queue[0], first);
  assert.deepEqual(
    host.disk.queue.map((c) => c.recordedTime.ordinal),
    [1, 2],
  );
});

test("uncertain write plus unreadable pointer blocks mutation until committed storage is reopened", async () => {
  const host = createHarness();
  await host.mount();
  await offline(host);
  host.failSaveAfterCommit = true;
  host.failLoad = true;
  await complete(host, "first");
  const committed = structuredClone(host.disk);
  host.failSaveAfterCommit = false;
  host.failLoad = false;
  await complete(host, "must-not-reuse");
  assert.deepEqual(host.disk, committed);
  assert.match(host.render().error, /storage must be reopened/i);
  const next = createHarness(committed, "reopened");
  next.online = false;
  await next.mount();
  await complete(next, "second");
  assert.deepEqual(
    next.disk.queue.map((c) => c.recordedTime.ordinal),
    [1, 2],
  );
});

test("unverified-only work survives failed discard and guards account switching, sign-out and lifecycle actions", async () => {
  const host = createHarness(account(), "unverified", { clockEpoch: null });
  await host.mount();
  await complete(host);
  const saved = structuredClone(host.disk.unverified);
  host.failSave = true;
  await host.render().discardUnverified(saved[0].command.operationId);
  assert.deepEqual(host.disk.unverified, saved);
  host.failSave = false;
  await host.render().signOut();
  assert.ok(host.savedSession);
  await assert.rejects(
    host.render().accountAction("deletion", "token"),
    /pending changes/,
  );
  host.identityUser = "someone-else";
  await assert.rejects(
    host.render().connect("other-token"),
    /previous account/,
  );
  assert.deepEqual(host.disk.unverified, saved);
  assert.equal(host.disk.userId, "owner");
});

test("native-timed lost committed response replays identically after reboot without duplicate effects", async () => {
  const host = createHarness();
  await host.mount();
  const receipts = new Map();
  let effects = 0;
  const execute = async (c) => {
    if (receipts.has(c.operationId)) {
      assert.deepEqual(c, receipts.get(c.operationId));
      return host.state;
    }
    receipts.set(c.operationId, structuredClone(c));
    effects++;
    throw new TypeError("Committed response lost");
  };
  host.execute = execute;
  await complete(host);
  const saved = structuredClone(host.disk.queue[0]);
  const next = createHarness(host.disk, "reboot", {
    clockEpoch: "8",
    clockMs: 50,
  });
  next.execute = execute;
  await next.mount();
  assert.deepEqual(next.sent, [saved]);
  assert.equal(effects, 1);
  assert.equal(next.disk.queue.length, 0);
});

test("simultaneous duplicate taps cannot allocate the same ordinal", async () => {
  const host = createHarness();
  await host.mount();
  await offline(host);
  await Promise.all([complete(host, "same"), complete(host, "same")]);
  assert.equal(host.disk.queue.length, 1);
  assert.equal(host.disk.queue[0].recordedTime.ordinal, 1);
});
