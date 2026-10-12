import test from "node:test";
import assert from "node:assert/strict";
import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
require("../session/provider-harness.cjs");
const { activePlan } = require("../../src/features/quests/active-plan.ts");

const item = (
  id,
  date,
  status = "Active",
  time = null,
  acceptedAt = "2026-10-01T12:00:00Z",
) => ({
  occurrence: { id, dueDate: date, plannedTime: time, acceptedAt },
  status,
});
const ids = (items) => items.map((row) => row.occurrence.id);

test("active planning keeps server statuses and groups the selected-zone day without changing saved state", () => {
  const input = [
    item("today", "2026-10-12"),
    item("unscheduled", null),
    item("frozen-earlier", "2026-10-01", "Frozen"),
    item("cached-earlier", "2026-10-11"),
    item("future", "2026-10-13"),
    item("missed", "2026-10-11", "Missed"),
    item("abandoned", null, "Abandoned"),
    item("completed", "2026-10-12", "Completed"),
    item("offered", "2026-10-12", "Offered"),
  ];
  const before = structuredClone(input);
  Object.freeze(input);
  const result = activePlan(input, "2026-10-12");
  assert.deepEqual(ids(result.current), ["today"]);
  assert.deepEqual(ids(result.unscheduled), ["unscheduled"]);
  assert.deepEqual(ids(result.earlier), ["frozen-earlier", "cached-earlier"]);
  assert.deepEqual(ids(result.upcoming), ["future"]);
  assert.deepEqual(ids(result.past), ["missed", "abandoned"]);
  assert.equal(result.earlier[0].status, "Frozen");
  assert.deepEqual(input, before);
  assert.deepEqual(ids(activePlan(input, "2026-10-13").current), ["future"]);
});

test("active plans order dates, planned times and acceptance while leaving history out of the day", () => {
  const input = [
    item("late", "2026-10-12", "Active", "18:00:00"),
    item("no-time", "2026-10-12"),
    item(
      "morning-new",
      "2026-10-12",
      "Frozen",
      "09:00:00",
      "2026-10-02T12:00:00Z",
    ),
    item("morning-old", "2026-10-12", "Active", "09:00:00"),
    item("future-later", "2026-10-14", "Active", "08:00:00"),
    item("future-nearer", "2026-10-13", "Active", "20:00:00"),
    item("unscheduled-new", null, "Active", null, "2026-10-02T12:00:00Z"),
    item("unscheduled-old", null),
  ];
  const result = activePlan(input, "2026-10-12");
  assert.deepEqual(ids(result.current), [
    "morning-old",
    "morning-new",
    "late",
    "no-time",
  ]);
  assert.deepEqual(ids(result.upcoming), ["future-nearer", "future-later"]);
  assert.deepEqual(ids(result.unscheduled), [
    "unscheduled-old",
    "unscheduled-new",
  ]);
  assert.deepEqual(result.past, []);
});
