import test from "node:test";
import assert from "node:assert/strict";
import {
  isCalendarDate,
  shiftCalendarDate,
  calendarPlan,
} from "../../src/features/quests/calendar-plan.ts";

function item(
  id,
  dueDate,
  plannedTime = null,
  acceptedAt = "2026-01-01T12:00:00Z",
  status = "Active",
) {
  return { occurrence: { id, dueDate, plannedTime, acceptedAt }, status };
}

test("calendar rejects rolled-over, malformed and out-of-range dates", () => {
  for (const date of [
    "",
    "2026-2-01",
    "2026-02-29",
    "2024-02-30",
    "2026-04-31",
    "2026-13-01",
    "0000-01-01",
    "2026-01-01T00:00:00Z",
    " 2026-01-01",
  ])
    assert.equal(isCalendarDate(date), false, date);
  for (const date of ["2024-02-29", "0001-01-01", "0099-12-31", "9999-12-31"])
    assert.equal(isCalendarDate(date), true, date);
});

test("calendar steps preserve dates across leap years, DST and year boundaries", () => {
  for (const [date, amount, expected] of [
    ["2024-02-28", 1, "2024-02-29"],
    ["2024-02-29", 1, "2024-03-01"],
    ["2026-12-31", 1, "2027-01-01"],
    ["2027-01-01", -7, "2026-12-25"],
    ["2026-03-07", 7, "2026-03-14"],
    ["2026-10-31", 7, "2026-11-07"],
  ])
    assert.equal(shiftCalendarDate(date, amount), expected);
  assert.equal(shiftCalendarDate("0001-01-01", -1), null);
  assert.equal(shiftCalendarDate("9999-12-31", 1), null);
  assert.equal(shiftCalendarDate("2026-02-30", 1), null);
  assert.equal(shiftCalendarDate("2026-01-01", 0.5), null);
});

test("seven days group all statuses, keep empty dates and separate unscheduled history", () => {
  const input = [
    item("before", "2026-03-06"),
    item("first", "2026-03-07"),
    item("completed", "2026-03-07", null, undefined, "Completed"),
    item("missed", "2026-03-08", null, undefined, "Missed"),
    item("last", "2026-03-13"),
    item("after", "2026-03-14"),
    item("unscheduled", null),
  ];
  const result = calendarPlan(input, "2026-03-07", true);
  assert.deepEqual(
    result.days.map((day) => day.date),
    [
      "2026-03-07",
      "2026-03-08",
      "2026-03-09",
      "2026-03-10",
      "2026-03-11",
      "2026-03-12",
      "2026-03-13",
    ],
  );
  assert.deepEqual(
    result.days.flatMap((day) => day.data.map((row) => row.occurrence.id)),
    ["completed", "first", "missed", "last"],
  );
  assert.equal(result.days[2].data.length, 0);
  assert.deepEqual(
    result.unscheduled.map((row) => row.occurrence.id),
    ["unscheduled"],
  );
  assert.equal(calendarPlan(input, "2026-03-07", false).days.length, 1);
});

test("within a date planned time precedes acceptance order without mutating saved state", () => {
  const input = [
    item("late", "2026-01-01", "18:00:00"),
    item("no-time-late", "2026-01-01", null, "2026-01-01T14:00:00Z"),
    item("morning-late", "2026-01-01", "09:00:00", "2026-01-01T13:00:00Z"),
    item("no-time-early", "2026-01-01", null, "2026-01-01T10:00:00Z"),
    item("morning-early", "2026-01-01", "09:00:00", "2026-01-01T11:00:00Z"),
  ];
  const before = structuredClone(input);
  Object.freeze(input);
  assert.deepEqual(
    calendarPlan(input, "2026-01-01", false).days[0].data.map(
      (row) => row.occurrence.id,
    ),
    ["morning-early", "morning-late", "late", "no-time-early", "no-time-late"],
  );
  assert.deepEqual(input, before);
});

test("calendar handles invalid starts and the final representable date without throwing", () => {
  assert.deepEqual(calendarPlan([], "invalid", true).days, []);
  assert.deepEqual(
    calendarPlan([], "9999-12-31", true).days.map((day) => day.date),
    ["9999-12-31"],
  );
});
