import test from "node:test";
import { strict as assert } from "node:assert";
import harness from "./provider-harness.cjs";
const { account, createHarness, RequestError } = harness;

test("an action recorded before restart remains intact while offline and replays on reconnection", async () => {
  const first = createHarness();
  await first.mount();
  first.online = false;
  await first.render().refresh();
  await first
    .render()
    .command({ action: "complete", occurrenceId: "before-restart" });
  const existing = structuredClone(first.disk.queue[0]);
  assert.ok(existing.recordedTime);
  const restarted = createHarness(first.disk, "restarted");
  restarted.online = false;
  await restarted.mount();
  assert.deepEqual(restarted.disk.queue, [existing]);
  restarted.online = true;
  await restarted.render().refresh();
  assert.deepEqual(restarted.sent, [existing]);
  assert.equal(restarted.disk.queue.length, 0);
  assert.notEqual(restarted.disk.anchor.bootId, first.disk.anchor.bootId);
});

test("SYNC-02: one rejected command does not block two independent commands", async () => {
  const queue = ["bad", "good-1", "good-2"].map((id) => ({
    operationId: id,
    action: "complete",
    occurrenceId: id,
  }));
  const host = createHarness(account(queue));
  host.execute = async (command) => {
    if (command.operationId === "bad")
      throw new RequestError(400, "This occurrence is unavailable.");
    return host.state;
  };
  await host.mount();
  assert.deepEqual(
    host.sent.map((c) => c.operationId),
    ["bad", "good-1", "good-2"],
  );
  assert.equal(host.disk.queue.length, 0);
  assert.equal(host.disk.rejected.length, 1);
  assert.match(host.disk.rejected[0].reason, /unavailable/);
});

function proof(ordinal) {
  return {
    anchorId: "original-anchor",
    bootId: "original-process",
    ordinal,
    elapsedMilliseconds: ordinal * 100,
    deviceUtc: "2026-10-04T12:00:01.000Z",
  };
}
const queued = (id, action, extra = {}, ordinal = 1) => ({
  operationId: id,
  action,
  ...extra,
  recordedTime: proof(ordinal),
});

test("rejection preserves a dependent create/accept/complete/Undo chain while another occurrence drains", async () => {
  const queue = [
    queued("definition", "save-definition", {
      definition: { id: "q", title: "Practice" },
    }),
    queued("accept", "accept", { questId: "q", occurrenceId: "o" }, 2),
    queued("complete", "complete", { occurrenceId: "o" }, 3),
    queued("undo", "undo", { occurrenceId: "o", completionId: "complete" }, 4),
    queued("independent", "complete", { occurrenceId: "other" }, 5),
  ];
  const host = createHarness(account(queue));
  host.execute = async (c) => {
    if (c.operationId === "definition")
      throw new RequestError(409, "Revision conflict");
    return host.state;
  };
  await host.mount();
  assert.deepEqual(host.sent, [queue[0], queue[4]]);
  assert.deepEqual(
    host.disk.rejected.map((r) => r.command),
    queue.slice(0, 4),
  );
  assert.deepEqual(
    host.disk.rejected.map((r) => r.kind),
    ["rejected", "blocked", "blocked", "blocked"],
  );
  assert.equal(
    host.render().state.definitions.length,
    0,
    "rejected edits must not remain projected",
  );
  const restarted = createHarness(host.disk, "restarted");
  await restarted.mount();
  assert.equal(restarted.disk.rejected.length, 4);
  await restarted.render().discardRejected("definition");
  assert.deepEqual(
    restarted.disk.rejected.map((r) => r.command),
    queue.slice(1, 4),
  );
  await restarted.render().refresh();
  assert.deepEqual(
    restarted.sent,
    [],
    "discarding a prerequisite cannot release descendants",
  );
  await restarted.render().command({ action: "complete", occurrenceId: "o" });
  assert.deepEqual(restarted.sent, []);
  assert.match(restarted.render().error, /review.*rejected/i);
  await restarted.render().discardRejected("undo");
  assert.equal(restarted.disk.rejected.length, 2);
});

for (const status of [401, 403, 408, 429, 500, 503]) {
  test(`HTTP ${status} retains exact payload and ordering for retry after restart`, async () => {
    const queue = [
      queued("undo", "undo", { occurrenceId: "o", completionId: "completed" }),
      queued("next", "accept", { questId: "q", occurrenceId: "next-o" }, 2),
    ];
    const host = createHarness(account(queue));
    host.execute = async () => {
      throw new RequestError(status, "Retry the same action later");
    };
    await host.mount();
    assert.deepEqual(host.disk.queue, queue);
    assert.equal(host.disk.rejected?.length ?? 0, 0);
    assert.deepEqual(host.sent, [queue[0]]);
    const restarted = createHarness(host.disk, "restarted");
    await restarted.mount();
    assert.deepEqual(restarted.sent, queue);
    assert.deepEqual(restarted.disk.queue, []);
  });
}

for (const status of [400, 404, 409, 422]) {
  test(`HTTP ${status} preserves reason and only rejects that command`, async () => {
    const queue = [
      queued("bad", "complete", { occurrenceId: "bad-o" }),
      queued("good", "complete", { occurrenceId: "good-o" }, 2),
    ];
    const host = createHarness(account(queue));
    host.execute = async (c) => {
      if (c.operationId === "bad")
        throw new RequestError(status, `Reason ${status}`);
      return host.state;
    };
    await host.mount();
    assert.equal(host.disk.queue.length, 0);
    assert.equal(host.disk.rejected[0].httpStatus, status);
    assert.equal(host.disk.rejected[0].reason, `Reason ${status}`);
    assert.deepEqual(host.disk.rejected[0].command, queue[0]);
    await host.render().discardRejected("bad");
    assert.deepEqual(host.disk.rejected, []);
  });
}

test("committed but lost Undo response replays idempotently before later commands after restart", async () => {
  const undo = queued("undo", "undo", {
    occurrenceId: "o",
    completionId: "completion",
  });
  const next = queued(
    "next",
    "accept",
    { occurrenceId: "next-o", questId: "q" },
    2,
  );
  const host = createHarness(account([undo, next]));
  const receipts = new Map();
  let awardsReversed = 0;
  const execute = async (command) => {
    if (receipts.has(command.operationId)) {
      assert.deepEqual(receipts.get(command.operationId), command);
      return host.state;
    }
    receipts.set(command.operationId, structuredClone(command));
    if (command.action === "undo") {
      awardsReversed++;
      throw new TypeError("Response lost after commit");
    }
    return host.state;
  };
  host.execute = execute;
  await host.mount();
  assert.deepEqual(host.disk.queue, [undo, next]);
  const restarted = createHarness(host.disk, "restarted");
  restarted.execute = execute;
  await restarted.mount();
  assert.deepEqual(restarted.sent, [undo, next]);
  assert.equal(awardsReversed, 1);
  assert.equal(restarted.disk.queue.length, 0);
});

test("network failure does not quarantine, skip or rewrite a queued proof", async () => {
  const queue = [
    queued("complete", "complete", { occurrenceId: "o" }),
    queued("undo", "undo", { occurrenceId: "o", completionId: "complete" }, 2),
  ];
  const host = createHarness(account(queue));
  host.execute = async () => {
    throw new TypeError("Network interrupted");
  };
  await host.mount();
  assert.deepEqual(host.sent, [queue[0]]);
  assert.deepEqual(host.disk.queue, queue);
  host.execute = async () => host.state;
  await host.render().retry();
  assert.deepEqual(host.sent, [queue[0], ...queue]);
  assert.deepEqual(host.disk.queue, []);
});

test("individual discard retains an unrelated network retry and cannot lose siblings on storage failure", async () => {
  const queue = [
    queued("bad", "complete", { occurrenceId: "bad-o" }),
    queued("good", "complete", { occurrenceId: "good-o" }, 2),
  ];
  const host = createHarness(account(queue));
  host.execute = async (c) => {
    if (c.operationId === "bad") throw new RequestError(400, "Invalid action");
    throw new TypeError("Connection lost");
  };
  await host.mount();
  host.failSave = true;
  await host.render().discardRejected("bad");
  assert.equal(host.disk.rejected.length, 1);
  assert.deepEqual(host.disk.queue, [queue[1]]);
  host.failSave = false;
  await host.render().discardRejected("bad");
  assert.equal(host.disk.rejected.length, 0);
  assert.deepEqual(host.disk.queue, [queue[1]]);
});

test("a rejected completion retains its pending Undo without sending it or presenting a reward", async () => {
  const queue = [
    queued("complete", "complete", { occurrenceId: "o" }),
    queued("undo", "undo", { occurrenceId: "o", completionId: "complete" }, 2),
    queued("good", "accept", { occurrenceId: "other", questId: "q" }, 3),
  ];
  const host = createHarness(account(queue));
  host.execute = async (c) => {
    if (c.operationId === "complete")
      throw new RequestError(409, "Deadline passed");
    return host.state;
  };
  await host.mount();
  assert.deepEqual(host.sent, [queue[0], queue[2]]);
  assert.deepEqual(host.disk.rejected[1].command, queue[1]);
  assert.equal(host.render().recentCompletion, null);
  await host.render().signOut();
  assert.ok(
    host.savedSession,
    "rejected actions must also guard against accidental sign-out loss",
  );
});

test("unrecognized account-wide dependencies stay blocked for explicit review", async () => {
  const queue = [
    queued("zone", "zone", { newZone: "UTC" }),
    queued("next", "complete", { occurrenceId: "o" }, 2),
  ];
  const host = createHarness(account(queue));
  host.execute = async () => {
    throw new RequestError(409, "Zone conflict");
  };
  await host.mount();
  assert.deepEqual(host.sent, [queue[0]]);
  assert.equal(host.disk.rejected[1].kind, "blocked");
});

test("a failed durable rejection write cannot advance to the next command", async () => {
  const queue = [
    queued("bad", "complete", { occurrenceId: "o" }),
    queued("good", "complete", { occurrenceId: "other" }, 2),
  ];
  const host = createHarness(account(queue));
  host.execute = async () => {
    host.failSave = true;
    throw new RequestError(400, "Invalid action");
  };
  await host.mount();
  assert.deepEqual(host.disk.queue, queue);
  assert.deepEqual(host.sent, [queue[0]]);
});

test("an unrelated rejection does not change a valid pending Undo or its following proof", async () => {
  const queue = [
    queued("bad", "accept", { questId: "missing", occurrenceId: "bad-o" }),
    queued("undo", "undo", { occurrenceId: "o", completionId: "completed" }, 2),
    queued("next", "accept", { questId: "q", occurrenceId: "next-o" }, 3),
  ];
  const host = createHarness(account(queue));
  host.execute = async (c) => {
    if (c.operationId === "bad") throw new RequestError(400, "Unknown quest");
    return host.state;
  };
  await host.mount();
  assert.deepEqual(host.sent, queue);
  assert.deepEqual(host.disk.queue, []);
  assert.deepEqual(
    host.disk.rejected.map((entry) => entry.command),
    [queue[0]],
  );
});

test("reconnection replaces the old process anchor before the next offline session", async () => {
  const first = createHarness();
  await first.mount();
  const restarted = createHarness(first.disk, "restarted");
  restarted.online = false;
  await restarted.mount();
  restarted.online = true;
  await restarted.render().refresh();
  const fresh = structuredClone(restarted.disk.anchor);
  assert.notEqual(fresh.bootId, first.disk.anchor.bootId);
  restarted.online = false;
  await restarted.render().refresh();
  await restarted.render().command({ action: "complete", occurrenceId: "new" });
  assert.equal(restarted.disk.queue[0].recordedTime.bootId, fresh.bootId);
  assert.equal(restarted.disk.queue[0].recordedTime.anchorId, fresh.id);
  assert.equal(restarted.disk.queue[0].recordedTime.ordinal, 1);
});
