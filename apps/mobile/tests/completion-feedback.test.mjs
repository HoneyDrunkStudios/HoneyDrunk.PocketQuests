import test from "node:test";
import { strict as assert } from "node:assert";
import {
  completionFeedback,
  completionAnnouncement,
  survivingFeedback,
} from "../src/completion-feedback.ts";
import { rewardPreview, allocatedPreview } from "../src/reward-preview.ts";
import { createRequire } from "node:module";
import { remainingFocus, pauseFocus } from "../src/focus-clock.ts";
const require = createRequire(import.meta.url);
const ts = require("typescript");
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
const { pendingProjection } = require("../src/offline-projection.ts");

const quest = {
  id: "q",
  title: "Practice",
  categoryId: "c01",
  baseXp: 10,
  attributes: [],
  skills: [],
};
const occurrence = {
  occurrence: { id: "o", quest },
  status: "Active",
  completion: null,
  canUndo: false,
};
const command = {
  operationId: "completion",
  action: "complete",
  occurrenceId: "o",
};
function before() {
  return {
    overallXp: 90,
    overallLevel: 1,
    categories: [{ id: "c01", name: "Health", xp: 0, level: 1 }],
    attributes: [],
    skills: [{ id: "s01", name: "Strength Training", xp: 0, level: 1 }],
    rank: { current: "F" },
    entitlements: [],
    occurrences: [structuredClone(occurrence)],
    definitions: [],
    ledger: [],
  };
}
function confirmed() {
  const state = before();
  state.overallXp = 100;
  state.overallLevel = 2;
  state.categories[0] = { ...state.categories[0], xp: 10, level: 2 };
  state.skills[0] = { ...state.skills[0], xp: 10, level: 2 };
  state.occurrences[0] = {
    ...state.occurrences[0],
    status: "Completed",
    completion: { id: "completion" },
  };
  state.ledger = [
    {
      eventId: "completion",
      occurrenceId: "o",
      track: "Overall",
      trackId: "overall",
      amount: 10,
    },
    {
      eventId: "completion",
      occurrenceId: "o",
      track: "Category",
      trackId: "c01",
      amount: 10,
    },
    {
      eventId: "completion",
      occurrenceId: "o",
      track: "Skill",
      trackId: "s01",
      amount: 10,
    },
    {
      eventId: "unrelated",
      occurrenceId: "other",
      track: "Category",
      trackId: "c01",
      amount: 999,
    },
  ];
  state.completionOutcome = {
    completionId: "completion",
    occurrenceId: "o",
    levelUps: [
      { track: "Overall", trackId: "overall", name: "Overall", from: 1, to: 2 },
      { track: "Category", trackId: "c01", name: "Health", from: 1, to: 2 },
      {
        track: "Skill",
        trackId: "s01",
        name: "Strength Training",
        from: 1,
        to: 2,
      },
    ],
    rankUp: null,
    unlocks: [],
  };
  return state;
}
test("confirmed feedback uses only the matching reward ledger and announces every level-up", () => {
  const result = completionFeedback(before(), confirmed(), command);
  assert.deepEqual(
    result.rewards.map((r) => r.xp),
    [10, 10, 10],
  );
  assert.deepEqual(
    result.levelUps.map((r) => r.name),
    ["Overall", "Health", "Strength Training"],
  );
  assert.match(
    completionAnnouncement(result),
    /Level up! Overall reached level 2/,
  );
  assert.match(completionAnnouncement(result), /Health reached level 2/);
  assert.match(
    completionAnnouncement(result),
    /Strength Training reached level 2/,
  );
});
test("a tolerated future timestamp remains celebratable through receipt replay and current-state reconciliation", () => {
  const receipt = confirmed();
  receipt.occurrences[0].completion.recordedAt = "2026-10-03T12:00:05Z";
  receipt.ledger = receipt.ledger.map((entry) => ({
    ...entry,
    at: "2026-10-03T12:00:05Z",
  }));
  const notice = completionFeedback(before(), receipt, command);
  const current = { ...structuredClone(receipt), completionOutcome: null };
  assert.equal(notice.completionId, command.operationId);
  assert.match(completionAnnouncement(notice), /Overall reached level 2/);
  const replayed = completionFeedback(current, receipt, command);
  assert.deepEqual(survivingFeedback([notice, replayed], current), [notice]);
  const undone = before();
  assert.deepEqual(survivingFeedback([replayed], undone), []);
});

test("pending, missing ledger and no-op repeated completion cannot celebrate XP", () => {
  const prior = before();
  const pending = pendingProjection(prior, [command], { quests: [quest] });
  assert.equal(pending.overallXp, 90);
  assert.equal(pending.occurrences[0].pendingCompletion, true);
  assert.equal(completionFeedback(prior, pending, command), null);
  assert.equal(
    completionFeedback(prior, { ...confirmed(), ledger: undefined }, command),
    null,
  );
  assert.equal(
    completionFeedback(
      confirmed(),
      { ...confirmed(), completionOutcome: null },
      command,
    ),
    null,
  );
  assert.equal(
    completionFeedback(prior, confirmed(), {
      ...command,
      operationId: "double-tap",
    }),
    null,
  );
});
test("Undo and stale Undo never replace a later completion in the device projection", () => {
  const state = confirmed();
  state.occurrences[0].completion.id = "new-completion";
  const stale = pendingProjection(
    state,
    [{ action: "undo", occurrenceId: "o", completionId: "completion" }],
    { quests: [] },
  );
  assert.equal(stale.occurrences[0].status, "Completed");
  const matching = pendingProjection(
    state,
    [{ action: "undo", occurrenceId: "o", completionId: "new-completion" }],
    { quests: [] },
  );
  assert.equal(matching.occurrences[0].status, "Active");
  assert.equal(matching.occurrences[0].completion, null);
  assert.equal(matching.overallXp, 100); // Confirmed totals wait for the server.
  assert.equal(
    completionFeedback(state, matching, { ...command, action: "undo" }),
    null,
  );
});
test("rank promotion and earned rewards appear only when newly confirmed", () => {
  const state = confirmed();
  state.rank.current = "E";
  state.entitlements = [
    { id: "new", name: "New badge", earned: true },
    { id: "old", name: "Old badge", earned: true },
  ];
  const prior = before();
  prior.entitlements = [{ id: "old", earned: true }];
  state.completionOutcome.rankUp = "E";
  state.completionOutcome.unlocks = [{ id: "new", name: "New badge" }];
  const result = completionFeedback(prior, state, command);
  assert.equal(result.rankUp, "E");
  assert.deepEqual(result.unlocks, ["New badge"]);
});
test("lost-response acknowledgement retains exact level changes despite an already refreshed cache", () => {
  const after = confirmed();
  const result = completionFeedback(after, after, command);
  assert.equal(result.levelUps[0].from, 1);
  const stale = before();
  stale.overallLevel = 99;
  assert.deepEqual(completionFeedback(stale, after, command), result);
  const legacy = completionFeedback(
    before(),
    { ...after, completionOutcome: null },
    command,
  );
  assert.equal(legacy.rewards[0].xp, 10);
  assert.deepEqual(legacy.levelUps, []);
});
test("acknowledgements are deduplicated, ordered, and suppressed after matching Undo or recompletion", () => {
  const state = confirmed();
  const feedback = completionFeedback(before(), state, command);
  assert.deepEqual(survivingFeedback([feedback, feedback], state), [feedback]);
  state.occurrences[0].completion.id = "later";
  assert.deepEqual(survivingFeedback([feedback], state), []);
  state.occurrences[0].status = "Active";
  assert.deepEqual(survivingFeedback([feedback], state), []);
  const second = {
    ...feedback,
    completionId: "second",
    occurrenceId: "second-o",
  };
  const current = confirmed();
  current.occurrences.push({
    ...current.occurrences[0],
    occurrence: { ...occurrence.occurrence, id: "second-o" },
    completion: { id: "second" },
  });
  assert.deepEqual(survivingFeedback([feedback, second], current), [
    feedback,
    second,
  ]);
});
test("display previews conserve the canonical pools and break remainder ties by stable ID", () => {
  assert.deepEqual(
    allocatedPreview(10, [
      { id: "a02", basisPoints: 2500 },
      { id: "a01", basisPoints: 7500 },
    ]),
    [
      { id: "a02", xp: 2 },
      { id: "a01", xp: 8 },
    ],
  );
  const preview = rewardPreview(quest);
  assert.deepEqual(preview, {
    overall: 10,
    category: 10,
    attributes: [],
    skills: [],
  });
  for (const xp of [10, 13, 80, 104, 3440]) {
    assert.equal(
      allocatedPreview(xp, [
        { id: "s01", basisPoints: 6000 },
        { id: "s02", basisPoints: 4000 },
      ]).reduce((n, s) => n + s.xp, 0),
      xp,
    );
  }
});
test("focus expiry and pause produce only time values and preserve remaining duration", () => {
  const clock = { remainingMs: 60_000, startedAt: 1_000 };
  assert.equal(remainingFocus(clock, 31_000), 30_000);
  assert.deepEqual(pauseFocus(clock, 31_000), {
    remainingMs: 30_000,
    startedAt: null,
  });
  assert.equal(remainingFocus(pauseFocus(clock, 31_000), 99_000), 30_000);
  assert.equal(remainingFocus(clock, 61_000), 0);
  assert.equal(remainingFocus(clock, 100_000), 0);
});
test("a queued definition stays explicitly unconfirmed while its entered content is preserved", () => {
  const draft = {
    ...quest,
    title: "My entered title",
    criterion: "My entered criterion",
  };
  const state = pendingProjection(
    before(),
    [{ action: "save-definition", definition: draft, expectedRevision: 0 }],
    { quests: [] },
  );
  assert.deepEqual(state.definitions[0], {
    quest: draft,
    revision: 1,
    archived: false,
    pendingSave: true,
  });
  assert.equal(state.overallXp, 90);
});
