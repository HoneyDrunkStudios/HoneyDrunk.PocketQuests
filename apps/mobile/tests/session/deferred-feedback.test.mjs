import test from "node:test";
import assert from "node:assert/strict";
import harness from "./provider-harness.cjs";
import { createRequire } from "node:module";
const wire = createRequire(import.meta.url)(
  "../../../../contracts/wire-fixtures.json",
);
const { account, createHarness } = harness;

async function acknowledgedCompletionWithFailedAnchorRefresh() {
  const cached = account();
  cached.state.occurrences = [
    {
      ...structuredClone(wire.state.occurrences[0]),
      occurrence: {
        ...structuredClone(wire.state.occurrences[0].occurrence),
        id: "o",
        quest: {
          ...wire.catalog.quests[0],
          id: "q",
          title: "Practice",
          baseXp: 10,
        },
      },
      status: "Active",
      completion: null,
      canUndo: false,
    },
  ];
  const host = createHarness(cached);
  await host.mount();
  let failAnchor = true;
  host.respond = async (url) => {
    if (failAnchor && url.endsWith("/api/sync-anchor"))
      throw new TypeError("Anchor response unavailable");
    return null;
  };
  host.execute = async (c) => {
    assert.equal(c.action, "complete");
    host.state = {
      ...host.state,
      overallXp: 10,
      occurrences: [
        {
          ...host.state.occurrences[0],
          status: "Completed",
          completion: {
            id: c.operationId,
            occurrenceId: "o",
            recordedAt: "2026-10-04T12:00:00Z",
            snapshot: null,
          },
          canUndo: true,
        },
      ],
      ledger: [
        {
          eventId: c.operationId,
          at: "2026-10-04T12:00:00Z",
          occurrenceId: "o",
          track: "Overall",
          trackId: "overall",
          amount: 10,
        },
      ],
      completionOutcome: {
        completionId: c.operationId,
        occurrenceId: "o",
        levelUps: [],
        rankUp: null,
        unlocks: [],
      },
    };
    return host.state;
  };
  await host.render().command({ action: "complete", occurrenceId: "o" });
  assert.equal(host.disk.queue.length, 0);
  assert.equal(host.render().recentCompletion, null);
  assert.equal(host.render().offline, true);
  return {
    host,
    reconnect: () => {
      failAnchor = false;
    },
  };
}

test("control: refreshing after an anchor failure presents the deferred confirmed completion", async () => {
  const { host, reconnect } =
    await acknowledgedCompletionWithFailedAnchorRefresh();
  reconnect();
  await host.render().retry();
  assert.equal(
    host.render().recentCompletion.completionId,
    host.sent[0].operationId,
  );
});

test("an unverified Undo must also suppress feedback already awaiting confirmation", async () => {
  const { host, reconnect } =
    await acknowledgedCompletionWithFailedAnchorRefresh();
  const completionId = host.sent[0].operationId;
  // A wall clock change is an expressly supported reason to retain an unverified intent.
  host.wallMs += 121000;
  await host
    .render()
    .command({ action: "undo", occurrenceId: "o", completionId });
  assert.equal(host.disk.unverified.length, 1);
  assert.equal(host.disk.unverified[0].command.completionId, completionId);
  assert.equal(host.render().state.occurrences[0].status, "Active");
  assert.equal(host.render().recentCompletion, null);
  reconnect();
  await host.render().retry();
  assert.equal(host.sent.length, 1, "the unverified Undo is never sent");
  assert.equal(host.disk.unverified.length, 1);
  assert.equal(host.render().state.occurrences[0].status, "Active");
  assert.equal(
    host.render().recentCompletion,
    null,
    "the pending Undo must suppress the delayed completion celebration",
  );
});

test("an Undo persisted before a lost storage response still suppresses deferred feedback", async () => {
  const { host, reconnect } =
    await acknowledgedCompletionWithFailedAnchorRefresh();
  host.wallMs += 121000;
  host.failSaveAfterCommit = true;
  await host.render().command({
    action: "undo",
    occurrenceId: "o",
    completionId: host.sent[0].operationId,
  });
  assert.equal(host.disk.unverified.length, 1);
  host.failSaveAfterCommit = false;
  reconnect();
  await host.render().retry();
  assert.equal(host.sent.length, 1);
  assert.equal(host.disk.unverified.length, 1);
  assert.equal(host.render().recentCompletion, null);
});

test("an Undo that was never persisted does not erase confirmed deferred feedback", async () => {
  const { host, reconnect } =
    await acknowledgedCompletionWithFailedAnchorRefresh();
  host.wallMs += 121000;
  host.failSave = true;
  await host.render().command({
    action: "undo",
    occurrenceId: "o",
    completionId: host.sent[0].operationId,
  });
  assert.equal(host.disk.unverified.length, 0);
  host.failSave = false;
  reconnect();
  await host.render().retry();
  assert.equal(
    host.render().recentCompletion.completionId,
    host.sent[0].operationId,
  );
});
