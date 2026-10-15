const path = require("node:path");
const { applyPatches } = require("./dependency-patches.cjs");
applyPatches(path.resolve(__dirname, ".."), process.argv.includes("--check"));
console.log(
  "Pinned braces depth guards and forge DigestAlgorithm validation verified; registry advisories remain visible.",
);
