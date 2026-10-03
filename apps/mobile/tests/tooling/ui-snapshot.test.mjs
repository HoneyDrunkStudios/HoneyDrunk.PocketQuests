import test from "node:test";
import { strict as assert } from "node:assert";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";
const app = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
function fixture(action) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "ui-snapshot-test-"));
  try {
    fs.cpSync(path.join(app, "packages"), path.join(root, "packages"), {
      recursive: true,
    });
    fs.mkdirSync(path.join(root, "scripts"));
    fs.copyFileSync(
      path.join(app, "scripts/ui-snapshot.cjs"),
      path.join(root, "scripts/ui-snapshot.cjs"),
    );
    action(root);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
}
const run = (root) =>
  spawnSync(process.execPath, [path.join(root, "scripts/ui-snapshot.cjs")], {
    encoding: "utf8",
  });
test("snapshot check accepts exact committed files and Windows line endings", () =>
  fixture((root) => {
    assert.equal(run(root).status, 0);
    const file = path.join(root, "packages/ui-native/src/styles.ts");
    fs.writeFileSync(
      file,
      fs.readFileSync(file, "utf8").replace(/\r?\n/g, "\r\n"),
    );
    assert.equal(run(root).status, 0);
  }));
test("snapshot check rejects edited generic source", () =>
  fixture((root) => {
    fs.appendFileSync(
      path.join(root, "packages/ui-native/src/styles.ts"),
      "// local divergence\n",
    );
    const result = run(root);
    assert.equal(result.status, 1);
    assert.match(result.stderr, /diverged/);
  }));
test("snapshot check rejects unexpected generic files", () =>
  fixture((root) => {
    fs.writeFileSync(
      path.join(root, "packages/ui-native/src/extra.ts"),
      "export {};\n",
    );
    const result = run(root);
    assert.equal(result.status, 1);
    assert.match(result.stderr, /inventory diverged/);
  }));
