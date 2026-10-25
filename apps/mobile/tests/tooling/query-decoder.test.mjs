import test from "node:test";
import assert from "node:assert/strict";
import { createRequire } from "node:module";
import { readFileSync } from "node:fs";
const require = createRequire(import.meta.url);
const {
  parseQueryParams,
} = require("expo-router/build/fork/getStateFromPath-forks.js");
const parse = (query) =>
  parseQueryParams("/callback?" + query, { name: "callback" });

test("SDK 58 Router removes the old query decoder dependencies from the installed graph", () => {
  const lock = JSON.parse(
    readFileSync(new URL("../../package-lock.json", import.meta.url), "utf8"),
  );
  for (const name of ["query-string", "decode-uri-component"])
    assert.equal(
      Object.keys(lock.packages).some((entry) =>
        entry.endsWith("node_modules/" + name),
      ),
      false,
    );
});

test("the actual SDK 58 Router parser preserves callback and quest link parameters", () => {
  assert.deepEqual(
    { ...parse("code=abc%2Bdef%3D&state=nonce%2Fvalue") },
    { code: "abc+def=", state: "nonce/value" },
  );
  assert.deepEqual(
    { ...parse("title=Caf%C3%A9+%E2%9C%93&empty=&tag=one&tag=two") },
    { title: "Café ✓", empty: "", tag: ["one", "two"] },
  );
  const values = { title: "?&=+/#", unicode: "👋", id: "123" };
  assert.deepEqual(
    { ...parse(new URLSearchParams(values).toString()) },
    values,
  );
});

test(
  "malformed percent input completes in the SDK 58 Router without losing valid neighbors",
  { timeout: 2000 },
  () => {
    const result = parse(`title=${"%C0%AF".repeat(10000)}&id=ok`);
    assert.equal(result.id, "ok");
    assert.equal(typeof result.title, "string");
    assert.ok(result.title.length > 0);
  },
);
