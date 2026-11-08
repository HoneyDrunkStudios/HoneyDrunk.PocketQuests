import test from "node:test";
import assert from "node:assert/strict";
import {
  calendarDateInput,
  displayDate,
} from "../../src/shared/date-format.ts";
import { isCalendarDate } from "../../src/features/quests/calendar-plan.ts";

test("month/day/year dates round-trip calendar boundaries without a timezone shift", () => {
  for (const date of [
    "2024-02-29",
    "2026-01-01",
    "2026-12-31",
    "0001-01-01",
    "9998-12-31",
  ])
    assert.equal(calendarDateInput(displayDate(date)), date);
  assert.equal(displayDate("2026-11-06"), "11/06/2026");
  assert.equal(calendarDateInput("1/2/2026"), "2026-01-02");
});

test("partial, impossible and blank dates never silently become valid plans", () => {
  for (const text of ["02/30/2026", "13/01/2026", "11/", "11/06/", "words"])
    assert.equal(isCalendarDate(calendarDateInput(text)), false);
  assert.equal(calendarDateInput(""), "");
});
