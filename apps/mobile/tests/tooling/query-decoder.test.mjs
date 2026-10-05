import test from "node:test";
import assert from "node:assert/strict";
import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const query = require("query-string");
test("the actual CommonJS query-string consumer accepts patched ESM decoder for callback and quest links", () => {
  assert.deepEqual(
    { ...query.parse("code=abc%2Bdef%3D&state=nonce%2Fvalue") },
    { code: "abc+def=", state: "nonce/value" },
  );
  assert.deepEqual(
    { ...query.parse("title=Caf%C3%A9+%E2%9C%93&empty=&tag=one&tag=two") },
    { title: "Café ✓", empty: "", tag: ["one", "two"] },
  );
  const values = { title: "?&=+/#", unicode: "👋", id: "123" };
  assert.deepEqual({ ...query.parse(query.stringify(values)) }, values);
});
test(
  "malformed percent input completes with the patched decoder without losing valid neighbors",
  { timeout: 2000 },
  () => {
    const malformed = "%C0%AF".repeat(10000);
    const result = query.parse(`title=${malformed}&id=ok`);
    assert.equal(result.id, "ok");
    assert.equal(typeof result.title, "string");
    assert.ok(result.title.length > 0);
  },
);
