import test from "node:test";
import assert from "node:assert/strict";
import { createRequire } from "node:module";
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
const {
  monthDates,
  adjacentMonth,
} = require("../../src/features/quests/month-grid.ts");

test("month grids show each date once and align leap days with weekdays", () => {
  const leap = monthDates("2028-02-18");
  assert.equal(leap.length, 35);
  assert.equal(monthDates("2026-08-01").length, 42);
  assert.equal(leap.filter(Boolean).length, 29);
  assert.equal(leap[2], "2028-02-01");
  assert.equal(leap[30], "2028-02-29");
  assert.equal(new Set(leap.filter(Boolean)).size, 29);
  assert.equal(monthDates("2100-02-01").filter(Boolean).length, 28);
});

test("month navigation preserves month starts across years and rejects unsupported years", () => {
  assert.equal(adjacentMonth("2026-12-31", 1), "2027-01-01");
  assert.equal(adjacentMonth("2026-01-31", -1), "2025-12-01");
  assert.equal(adjacentMonth("0001-01-01", -1), null);
  assert.equal(adjacentMonth("9999-12-01", 1), null);
  assert.deepEqual(monthDates("invalid"), []);
});
