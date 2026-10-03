const fs = require("node:fs");
const path = require("node:path");
const crypto = require("node:crypto");
const { execFileSync } = require("node:child_process");
const root = path.resolve(__dirname, "..");
const manifestPath = path.join(root, "packages", "upstream.json");
const canonical = (bytes) => bytes.toString("utf8").replace(/\r\n/g, "\n");
const digest = (bytes) =>
  crypto.createHash("sha256").update(canonical(bytes)).digest("hex");
const validPath = (file) =>
  /^packages\/ui-(native|tokens)\/[A-Za-z0-9_./-]+$/.test(file) &&
  !file.split("/").includes("..");
function inventory(directory, prefix) {
  return fs.readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    if (entry.isSymbolicLink())
      throw new Error("Snapshot cannot contain symbolic links");
    const file = prefix + "/" + entry.name;
    return entry.isDirectory()
      ? inventory(path.join(directory, entry.name), file)
      : [file];
  });
}
function check(manifest) {
  if (!manifest.files || !/^[a-f0-9]{40}$/.test(manifest.revision))
    throw new Error("Missing exact UI provenance");
  const expected = Object.keys(manifest.files).sort();
  const actual = ["ui-native", "ui-tokens"]
    .flatMap((name) =>
      inventory(path.join(root, "packages", name), "packages/" + name),
    )
    .sort();
  if (JSON.stringify(expected) !== JSON.stringify(actual))
    throw new Error(
      "UI snapshot file inventory diverged from upstream manifest",
    );
  for (const file of expected) {
    if (
      !validPath(file) ||
      digest(fs.readFileSync(path.join(root, file))) !== manifest.files[file]
    )
      throw new Error("UI snapshot diverged: " + file);
  }
}
try {
  const args = process.argv.slice(2);
  let manifest = JSON.parse(fs.readFileSync(manifestPath, "utf8"));
  check(manifest); // Never overwrite unreviewed local UI changes.
  if (args.length === 0) {
    console.log(
      "UI snapshot matches " +
        manifest.revision +
        " (" +
        Object.keys(manifest.files).length +
        " files)",
    );
  } else {
    if (
      (args.length !== 2 && args.length !== 4) ||
      args[0] !== "--source" ||
      (args.length === 4 && args[2] !== "--revision")
    )
      throw new Error(
        "Usage: ui-snapshot.cjs [--source CHECKOUT [--revision EXACT_SHA]]",
      );
    const source = path.resolve(args[1]);
    const revision = args[3] || manifest.revision;
    if (!/^[a-f0-9]{40}$/.test(revision))
      throw new Error("Provide an exact reviewed commit SHA");
    const git = (parameters) =>
      execFileSync("git", ["-C", source, ...parameters], { encoding: "utf8" });
    const remote = git(["remote", "get-url", "origin"])
      .trim()
      .replace(/\.git$/, "");
    if (
      remote !== manifest.repository &&
      remote !== "git@github.com:HoneyDrunkStudios/HoneyDrunk.UI"
    )
      throw new Error("Source origin differs from approved UI repository");
    if (git(["rev-parse", revision + "^{commit}"]).trim() !== revision)
      throw new Error("Source revision unavailable");
    const names = git([
      "ls-tree",
      "-r",
      "--name-only",
      revision,
      "--",
      "packages/ui-native",
      "packages/ui-tokens",
    ])
      .trim()
      .split("\n");
    const content = new Map();
    for (const file of names) {
      if (!validPath(file)) throw new Error("Unsafe upstream snapshot path");
      content.set(file, git(["show", revision + ":" + file]));
    }
    if (content.size < 5) throw new Error("Incomplete upstream UI packages");
    for (const [file, bytes] of content) {
      fs.mkdirSync(path.dirname(path.join(root, file)), { recursive: true });
      fs.writeFileSync(path.join(root, file), bytes);
    }
    for (const file of Object.keys(manifest.files))
      if (!content.has(file)) fs.unlinkSync(path.join(root, file));
    manifest = {
      ...manifest,
      revision,
      files: Object.fromEntries(
        [...content].map(([file, bytes]) => [file, digest(Buffer.from(bytes))]),
      ),
    };
    fs.writeFileSync(manifestPath, JSON.stringify(manifest, null, 2) + "\n");
    check(manifest);
    console.log(
      "Synced committed UI source at " +
        revision +
        "; run typecheck, lint and tests before accepting the update.",
    );
  }
} catch (error) {
  console.error(error.message);
  process.exitCode = 1;
}
