import test from "node:test";
import assert from "node:assert/strict";
import { createRequire } from "node:module";
import harness from "./provider-harness.cjs";

const wire = createRequire(import.meta.url)(
  "../../../../contracts/wire-fixtures.json",
);
const { account, createHarness } = harness;

test("a pending server completion does not report offline until its committed response is lost", async (t) => {
  const cached = account();
  cached.state.occurrences = [
    {
      ...structuredClone(wire.state.occurrences[0]),
      occurrence: {
        ...structuredClone(wire.state.occurrences[0].occurrence),
        id: "o",
        quest: { ...wire.catalog.quests[0], id: "q", baseXp: 10 },
      },
      status: "Active",
      completion: null,
      canUndo: false,
    },
  ];
  const host = createHarness(cached);
  await host.mount();
  const entered = Promise.withResolvers();
  const commit = Promise.withResolvers();
  let receipt;
  host.execute = async (command) => {
    if (receipt) return receipt;
    entered.resolve();
    await commit.promise;
    receipt = {
      ...host.state,
      overallXp: 10,
      occurrences: [
        {
          ...host.state.occurrences[0],
          status: "Completed",
          completion: {
            id: command.operationId,
            occurrenceId: "o",
            recordedAt: "2026-10-04T12:00:00Z",
            snapshot: null,
          },
          canUndo: true,
        },
      ],
      ledger: [
        {
          eventId: command.operationId,
          at: "2026-10-04T12:00:00Z",
          occurrenceId: "o",
          track: "Overall",
          trackId: "overall",
          amount: 10,
        },
      ],
      completionOutcome: {
        completionId: command.operationId,
        occurrenceId: "o",
        levelUps: [],
        rankUp: null,
        unlocks: [],
      },
    };
    host.state = receipt;
    throw new TypeError("Committed response was dropped.");
  };

  t.mock.timers.enable({ apis: ["setTimeout"] });
  let sending;
  try {
    sending = host.render().command({ action: "complete", occurrenceId: "o" });
    await entered.promise;
    // The browser's default five-second assertion budget can expire in this
    // legitimate in-flight state. This harness tests session state, not rendering.
    t.mock.timers.tick(6000);
    assert.equal(host.render().busy, true);
    assert.equal(host.render().offline, false);
    assert.equal(host.render().state.occurrences[0].pendingCompletion, true);
    assert.equal(host.render().recentCompletion, null);
    assert.equal(host.disk.queue.length, 1);
    const retained = structuredClone(host.disk.queue[0]);
    await host.render().command({ action: "complete", occurrenceId: "o" });
    assert.deepEqual(host.sent, [retained]);

    commit.resolve();
    await sending;
    assert.equal(host.render().busy, false);
    assert.equal(host.render().offline, true);
    assert.equal(host.render().recentCompletion, null);
    assert.deepEqual(host.disk.queue, [retained]);
    assert.equal(host.state.overallXp, 10);

    await host.render().retry();
    assert.deepEqual(host.sent, [retained, retained]);
    assert.deepEqual(host.disk.queue, []);
    assert.equal(host.render().state.overallXp, 10);
    assert.equal(host.render().state.ledger.length, 1);
    assert.equal(host.render().recentCompletion.completionId, retained.operationId);
  } finally {
    commit.resolve();
    try {
      await sending;
    } finally {
      t.mock.timers.reset();
    }
  }
});
