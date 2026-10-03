import test from "node:test";
import { strict as assert } from "node:assert";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";

const script = fileURLToPath(
  new URL("../../scripts/test-ci.cjs", import.meta.url),
);
function fixture(source, check) {
  const temporary = path.resolve(os.tmpdir());
  const root = fs.mkdtempSync(path.join(temporary, "pq-test-runner-"));
  try {
    fs.mkdirSync(path.join(root, "scripts"));
    fs.mkdirSync(path.join(root, "tests", "feature", "nested"), {
      recursive: true,
    });
    fs.copyFileSync(script, path.join(root, "scripts/test-ci.cjs"));
    if (source !== null)
      fs.writeFileSync(
        path.join(root, "tests/feature/nested/contract.test.mjs"),
        source,
      );
    // This fixture launches an independent CLI run, not a child of the outer test worker.
    const environment = { ...process.env };
    delete environment.NODE_TEST_CONTEXT;
    delete environment.NODE_TEST_WORKER_ID;
    const result = spawnSync(
      process.execPath,
      [path.join(root, "scripts/test-ci.cjs")],
      {
        encoding: "utf8",
        timeout: 30000,
        env: environment,
      },
    );
    assert.equal(result.error, undefined);
    check(result, root);
  } finally {
    assert.equal(path.dirname(path.resolve(root)), temporary);
    assert.ok(path.basename(root).startsWith("pq-test-runner-"));
    fs.rmSync(root, { recursive: true, force: true });
  }
}

test("test runner discovers nested feature tests and writes nonempty TAP", () =>
  fixture(
    'import test from "node:test"; test("nested contract", () => {});',
    (result, root) => {
      assert.equal(result.status, 0);
      const report = fs.readFileSync(
        path.join(root, "reports/unit.tap"),
        "utf8",
      );
      assert.match(report, /ok 1 - nested contract/);
      assert.match(report, /# tests 1/);
    },
  ));

test("test runner propagates nested test failures", () =>
  fixture(
    'import test from "node:test"; test("broken contract", () => { throw new Error("expected failure"); });',
    (result, root) => {
      assert.equal(result.status, 1);
      assert.match(
        fs.readFileSync(path.join(root, "reports/unit.tap"), "utf8"),
        /not ok 1 - broken contract/,
      );
    },
  ));

test("test runner fails when organization leaves no discoverable tests", () =>
  fixture(null, (result, root) => {
    assert.equal(result.status, 1);
    assert.match(result.stderr, /No mobile contract test files were found/);
    assert.equal(fs.existsSync(path.join(root, "reports/unit.tap")), false);
  }));
