import { test } from "node:test";
import { strict as assert } from "node:assert";
import { warningTimes } from "../../src/features/quests/notification-plan.ts";
const now = Date.parse("2026-09-28T12:00:00Z");
const cutoff = "2026-09-29T00:00:00Z";
function state() {
  return {
    profile: { expiryWarnings: true },
    schedule: { accountPaused: false, pausedCategories: [] },
    occurrences: [
      {
        status: "Active",
        occurrence: { deadline: cutoff, quest: { categoryId: "c01" } },
      },
    ],
    futureWarnings: [],
  };
}
test("off, signed-out and account-paused states suppress warnings", () => {
  assert.deepEqual(warningTimes(null, now), []);
  const value = state();
  value.profile.expiryWarnings = false;
  assert.deepEqual(warningTimes(value, now), []);
  value.profile.expiryWarnings = true;
  value.schedule.accountPaused = true;
  assert.deepEqual(warningTimes(value, now), []);
});
test("only active nonpaused commitments schedule a generic cutoff warning", () => {
  const value = state();
  assert.deepEqual(warningTimes(value, now), [Date.parse(cutoff) - 3_600_000]);
  for (const status of [
    "Completed",
    "Missed",
    "Frozen",
    "Abandoned",
    "Offered",
  ]) {
    value.occurrences[0].status = status;
    assert.deepEqual(warningTimes(value, now), []);
  }
  value.occurrences[0].status = "Active";
  value.schedule.pausedCategories = ["c01"];
  assert.deepEqual(warningTimes(value, now), []);
});
test("groups identical cutoffs, omits expired/invalid dates, and caps pending dates", () => {
  const value = state();
  value.futureWarnings = [
    cutoff,
    "invalid",
    "2026-09-28T12:30:00Z",
    ...Array.from({ length: 100 }, (_, day) =>
      new Date(Date.parse(cutoff) + day * 86_400_000).toISOString(),
    ),
  ];
  const actual = warningTimes(value, now);
  assert.equal(actual.length, 60);
  assert.equal(actual[0], Date.parse(cutoff) - 3_600_000);
  assert.equal(new Set(actual).size, actual.length);
  assert.ok(
    actual.every((at, i) => at > now && (i === 0 || at > actual[i - 1])),
  );
});
