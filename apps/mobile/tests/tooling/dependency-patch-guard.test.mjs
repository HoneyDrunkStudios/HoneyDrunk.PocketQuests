import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";

const app = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const patches = JSON.parse(
  fs.readFileSync(path.join(app, "scripts/dependency-patches.json"), "utf8"),
);
function fixture(action) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "pq-patch-guard-"));
  try {
    fs.mkdirSync(path.join(root, "scripts"));
    for (const file of ["patch-dependencies.cjs", "dependency-patches.json"])
      fs.copyFileSync(
        path.join(app, "scripts", file),
        path.join(root, "scripts", file),
      );
    const packages = {};
    for (const patch of patches) {
      const directory = path.join(root, "node_modules", patch.package);
      packages["node_modules/" + patch.package] = { version: patch.version };
      fs.mkdirSync(path.join(directory, "lib"), { recursive: true });
      fs.writeFileSync(
        path.join(directory, "package.json"),
        JSON.stringify({ name: patch.package, version: patch.version }),
      );
      let original = fs
        .readFileSync(
          path.join(app, "node_modules", patch.package, patch.file),
          "utf8",
        )
        .replaceAll("\r\n", "\n");
      for (const edit of [...patch.replacements].reverse())
        original = original.replaceAll(edit.after, edit.before);
      fs.writeFileSync(path.join(directory, patch.file), original);
    }
    fs.writeFileSync(
      path.join(root, "package-lock.json"),
      JSON.stringify({ packages }),
    );
    const run = (...args) =>
      spawnSync(
        process.execPath,
        [path.join(root, "scripts/patch-dependencies.cjs"), ...args],
        { encoding: "utf8" },
      );
    action(root, run);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
}

test("dependency patches reject absent guards, apply idempotently, and detect tampering", () =>
  fixture((root, run) => {
    assert.equal(run("--check").status, 1);
    assert.equal(run().status, 0);
    assert.equal(run().status, 0);
    assert.equal(run("--check").status, 0);
    fs.appendFileSync(
      path.join(root, "node_modules/node-forge/lib/rsa.js"),
      "// tampered\n",
    );
    assert.match(run("--check").stderr, /Unexpected dependency source/);
  }));

test("unexpected later patch input prevents every earlier patch write", () =>
  fixture((root, run) => {
    const first = path.join(root, "node_modules/braces/lib/parse.js");
    const before = fs.readFileSync(first, "utf8");
    fs.appendFileSync(
      path.join(root, "node_modules/node-forge/lib/rsa.js"),
      "// new upstream source\n",
    );
    assert.equal(run().status, 1);
    assert.equal(fs.readFileSync(first, "utf8"), before);
  }));

test("additional dependency copies require review instead of leaving an unpatched copy", () =>
  fixture((root, run) => {
    const lockPath = path.join(root, "package-lock.json");
    const lock = JSON.parse(fs.readFileSync(lockPath, "utf8"));
    lock.packages["node_modules/consumer/node_modules/braces"] = {
      version: "3.0.3",
    };
    fs.writeFileSync(lockPath, JSON.stringify(lock));
    assert.match(run().stderr, /additional or relocated copies/);
  }));
