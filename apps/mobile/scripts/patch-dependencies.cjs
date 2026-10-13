// App-owned, hash-pinned mitigations until compatible upstream fixes ship.
// Package identities and audit findings intentionally remain unchanged.
const fs = require("node:fs");
const path = require("node:path");
const { createHash } = require("node:crypto");
const patches = require("./dependency-patches.json");
const hash = (text) => createHash("sha256").update(text).digest("hex");
function preparePatch(root, patch, checkOnly) {
  const packagePath = path.join(
    root,
    "node_modules",
    patch.package,
    "package.json",
  );
  const installed = JSON.parse(fs.readFileSync(packagePath, "utf8"));
  if (installed.version !== patch.version)
    throw new Error(
      `Review ${patch.package} ${installed.version} before changing its security patch`,
    );
  const file = path.join(path.dirname(packagePath), patch.file);
  const code = fs.readFileSync(file, "utf8").replaceAll("\r\n", "\n");
  if (hash(code) === patch.patchedSha256) return null;
  if (hash(code) !== patch.originalSha256)
    throw new Error(
      `Unexpected dependency source: ${patch.package}/${patch.file}`,
    );
  if (checkOnly)
    throw new Error(`Missing security patch: ${patch.package}/${patch.file}`);
  let patched = code;
  for (const edit of patch.replacements) {
    if (patched.split(edit.before).length - 1 !== edit.count)
      throw new Error(`Patch context changed: ${patch.package}/${patch.file}`);
    patched = patched.replaceAll(edit.before, edit.after);
  }
  if (hash(patched) !== patch.patchedSha256)
    throw new Error(`Patch output mismatch: ${patch.package}/${patch.file}`);
  return { file, patched };
}
function applyPatches(root, checkOnly = false) {
  const lock = JSON.parse(
    fs.readFileSync(path.join(root, "package-lock.json"), "utf8"),
  );
  for (const name of new Set(patches.map((patch) => patch.package))) {
    const copies = Object.keys(lock.packages).filter((entry) =>
      entry.endsWith("node_modules/" + name),
    );
    if (copies.length !== 1 || copies[0] !== "node_modules/" + name)
      throw new Error(
        `Review additional or relocated copies of ${name} before applying security patches`,
      );
  }
  // Validate every input before writing any patch. Unexpected upgrades fail closed.
  const changes = patches.map((patch) => preparePatch(root, patch, checkOnly));
  for (const change of changes) {
    if (change) fs.writeFileSync(change.file, change.patched);
  }
}
module.exports = { applyPatches };
if (require.main === module) {
  applyPatches(path.resolve(__dirname, ".."), process.argv.includes("--check"));
  console.log(
    "Pinned braces depth guards and forge DigestAlgorithm validation verified; registry advisories remain visible.",
  );
}
