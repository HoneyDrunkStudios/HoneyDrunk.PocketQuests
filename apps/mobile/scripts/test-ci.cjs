const fs = require("node:fs");
const path = require("node:path");
const { spawnSync } = require("node:child_process");
const root = path.resolve(__dirname, "..");
function discover(directory) {
  return fs
    .readdirSync(path.join(root, directory), { withFileTypes: true })
    .flatMap((entry) => {
      const name = path.join(directory, entry.name);
      if (entry.isDirectory()) return discover(name);
      return entry.isFile() && entry.name.endsWith(".test.mjs") ? [name] : [];
    });
}
const tests = discover("tests").sort();
if (!tests.length) throw new Error("No mobile contract test files were found.");
fs.mkdirSync(path.join(root, "reports"), { recursive: true });
const result = spawnSync(
  process.execPath,
  [
    "--experimental-strip-types",
    "--test",
    "--test-reporter=spec",
    "--test-reporter-destination=stdout",
    "--test-reporter=tap",
    "--test-reporter-destination=reports/unit.tap",
    ...tests,
  ],
  { cwd: root, stdio: "inherit" },
);
if (result.error) console.error(result.error.message);
process.exitCode = result.status ?? 1;
