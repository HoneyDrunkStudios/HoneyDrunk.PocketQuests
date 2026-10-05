import test from "node:test";
import assert from "node:assert/strict";
import { configureCrashReporter, reportRenderFailure } from "../../src/shared/crash-reporting.ts";
test("the optional crash adapter is disabled by default, bounded and safe if its sink fails", () => {
  assert.doesNotThrow(reportRenderFailure);
  const events = [];
  configureCrashReporter(event => events.push(event));
  reportRenderFailure();
  assert.deepEqual(Object.keys(events[0]), ["code", "at"]);
  assert.equal(events[0].code, "unexpected_render_error");
  configureCrashReporter(() => { throw new Error("sink failed"); });
  assert.doesNotThrow(reportRenderFailure);
  configureCrashReporter();
});
