import test from "node:test";
import { strict as assert } from "node:assert";
import harness from "./provider-harness.cjs";
const { account, createHarness, createClockSource, RequestError } = harness;

function platformHost(platform, disk = account(), process = "initial") {
  const host = createHarness(disk, process, { platform });
  host.clockSource = createClockSource();
  return host;
}
const complete = (host, occurrenceId = "one") =>
  host.render().command({ action: "complete", occurrenceId });

for (const platform of ["ios", "web"]) {
  test(`${platform}: a rejected anchor request cannot manufacture an executable command`, async () => {
    const host = platformHost(platform);
    host.respond = async (url) =>
      url.endsWith("/api/sync-anchor")
        ? {
            ok: false,
            status: 401,
            json: async () => ({ detail: "Sign in again" }),
          }
        : null;
    await host.mount();
    assert.equal(host.disk.anchor, null);
    assert.equal(host.render().signedIn, false);
    await complete(host, "before-reauthentication");
    assert.deepEqual(host.sent, []);
    assert.equal(host.disk.unverified.length, 1);
    const intent = structuredClone(host.disk.unverified[0]);
    assert.equal("recordedTime" in intent.command, false);
    host.respond = null;
    await host.render().connect("verified-again");
    await complete(host, "after-reauthentication");
    assert.equal(host.sent.length, 1);
    assert.ok(host.sent[0].recordedTime);
    assert.deepEqual(host.disk.unverified, [intent]);
  });

  test(`${platform}: online complete and Undo use authenticated server anchors without native boot continuity`, async () => {
    const host = platformHost(platform);
    await host.mount();
    const anchor = structuredClone(host.disk.anchor);
    assert.equal(anchor.clock.kind, "js-process-monotonic-v1");
    await host.render().command({
      action: "complete",
      occurrenceId: "one",
      recordedTime: { anchorId: "forged", bootId: "forged", ordinal: 99 },
    });
    const completion = host.sent[0];
    assert.equal(completion.recordedTime.anchorId, anchor.id);
    assert.equal(completion.recordedTime.bootId, anchor.bootId);
    assert.equal(completion.recordedTime.ordinal, 1);
    assert.ok(completion.recordedTime.elapsedMilliseconds >= 0);
    assert.notEqual(completion.recordedTime.bootId, anchor.clock.epoch);
    const undoAnchor = structuredClone(host.disk.anchor);
    await host.render().command({
      action: "undo",
      occurrenceId: "one",
      completionId: completion.operationId,
    });
    assert.equal(host.sent[1].recordedTime.anchorId, undoAnchor.id);
    assert.equal(host.sent[1].completionId, completion.operationId);
    assert.equal(host.disk.queue.length, 0);
    assert.equal(host.disk.unverified.length, 0);
    assert.ok(
      host.requests.every(
        (r) => r.headers.Authorization === "Bearer test-token",
      ),
    );
    const issued = host.requests.filter((r) =>
      r.url.endsWith("/api/sync-anchor"),
    );
    assert.ok(issued.some((r) => r.body.bootId === anchor.bootId));
    assert.ok(
      issued.every((r) => !("clock" in r.body) && !("epoch" in r.body)),
    );
  });

  test(`${platform}: same-process offline capture works; restart cannot promote uncertain time, and reconnect restores new online work`, async () => {
    const first = platformHost(platform);
    await first.mount();
    first.online = false;
    await first.render().refresh();
    await complete(first, "before-restart");
    const queued = structuredClone(first.disk.queue[0]);
    assert.ok(queued.recordedTime);
    assert.equal(first.disk.unverified.length, 0);
    const next = platformHost(platform, first.disk, "next-process");
    next.online = false;
    await next.mount();
    await complete(next, "unproven-offline");
    const pending = structuredClone(next.disk.unverified);
    assert.equal(pending.length, 1);
    assert.equal("recordedTime" in pending[0].command, false);
    assert.notEqual(pending[0].clock.epoch, first.disk.anchor.clock.epoch);
    assert.deepEqual(next.disk.queue, [queued]);
    next.online = true;
    await next.render().connect("reconnected-token");
    assert.deepEqual(next.sent, [queued]);
    assert.deepEqual(next.disk.unverified, pending);
    await complete(next, "fresh-online");
    assert.equal(next.sent.length, 2);
    assert.ok(next.sent[1].recordedTime);
    assert.notEqual(
      next.sent[1].recordedTime.anchorId,
      queued.recordedTime.anchorId,
    );
    await complete(next, "unproven-offline");
    assert.equal(
      next.sent.length,
      2,
      "related unresolved intent must remain a barrier",
    );
    assert.deepEqual(next.disk.unverified[1].blockedBy, [
      pending[0].command.operationId,
    ]);
  });

  test(`${platform}: expired authentication retains proof and only the original account can retry it`, async () => {
    const host = platformHost(platform);
    await host.mount();
    host.execute = async () => {
      throw new RequestError(401, "Sign in again");
    };
    await complete(host);
    const original = structuredClone(host.sent[0]);
    assert.equal(host.render().signedIn, false);
    assert.deepEqual(host.disk.queue, [original]);
    assert.equal(host.disk.rejected.length, 0);
    host.identityUser = "other-owner";
    await assert.rejects(
      host.render().connect("other-account-token"),
      /previous account/,
    );
    assert.deepEqual(host.sent, [original]);
    assert.deepEqual(host.disk.queue, [original]);
    host.identityUser = "owner";
    host.execute = async () => host.state;
    await host.render().connect("fresh-owner-token");
    assert.deepEqual(host.sent, [original, original]);
    assert.equal(host.disk.queue.length, 0);
    assert.equal(host.render().signedIn, true);
    const replay = host.requests
      .filter((r) => r.url.endsWith("/api/commands"))
      .at(-1);
    assert.equal(replay.headers.Authorization, "Bearer fresh-owner-token");
  });

  test(`${platform}: lost committed online response replays exactly after restart with one effect`, async () => {
    const first = platformHost(platform);
    await first.mount();
    const receipts = new Map();
    let effects = 0;
    const execute = async (command) => {
      if (receipts.has(command.operationId)) {
        assert.deepEqual(command, receipts.get(command.operationId));
        return first.state;
      }
      receipts.set(command.operationId, structuredClone(command));
      effects++;
      throw new TypeError("Response lost after commit");
    };
    first.execute = execute;
    await complete(first);
    const original = structuredClone(first.disk.queue[0]);
    assert.ok(original.recordedTime);
    const next = platformHost(platform, first.disk, "retry-process");
    next.execute = execute;
    await next.mount();
    assert.deepEqual(next.sent, [original]);
    assert.equal(effects, 1);
    assert.equal(next.disk.queue.length, 0);
    assert.equal(next.disk.unverified.length, 0);
  });

  test(`${platform}: ordinary online proofs still receive server deadline and Undo rejections`, async () => {
    const host = platformHost(platform);
    await host.mount();
    host.execute = async (command) => {
      assert.ok(
        command.recordedTime,
        "online path must retain an actual capture proof",
      );
      if (command.action === "complete")
        throw new RequestError(409, "Deadline passed");
      if (command.action === "undo")
        throw new RequestError(400, "Undo window passed");
      return host.state;
    };
    await complete(host, "expired");
    await host.render().command({
      action: "undo",
      occurrenceId: "older",
      completionId: "old-completion",
    });
    assert.deepEqual(
      host.disk.rejected.map((r) => r.reason),
      ["Deadline passed", "Undo window passed"],
    );
    assert.equal(host.disk.unverified.length, 0);
    await host
      .render()
      .command({ action: "accept", occurrenceId: "independent", questId: "q" });
    assert.equal(host.sent.length, 3);
    assert.equal(host.disk.queue.length, 0);
  });
}

test("losing native continuity cannot silently continue its anchor with a process clock", async () => {
  const host = platformHost("android");
  host.nativeModule = {
    sample: () => ({
      epoch: "7",
      elapsedMilliseconds: host.clockMs,
      utcMilliseconds: host.wallMs,
    }),
  };
  await host.mount();
  assert.equal(host.disk.anchor.clock.kind, "android-elapsed-realtime-v1");
  host.nativeModule = null;
  await complete(host, "during-clock-loss");
  assert.deepEqual(host.sent, []);
  const pending = structuredClone(host.disk.unverified);
  assert.equal(pending.length, 1);
  await host.render().connect("new-baseline-token");
  await complete(host, "new-online-action");
  assert.equal(host.sent.length, 1);
  assert.deepEqual(host.disk.unverified, pending);
  assert.equal(host.disk.anchor.clock.kind, "js-process-monotonic-v1");
});
