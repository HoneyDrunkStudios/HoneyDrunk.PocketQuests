const fs = require("node:fs");
const path = require("node:path");
const { createHash } = require("node:crypto");
const source = require.resolve("query-string");
const packageFile = path.join(path.dirname(source), "package.json");
if (JSON.parse(fs.readFileSync(packageFile, "utf8")).version !== "7.1.3")
  throw new Error(
    "Review the query-string compatibility patch for the new version before installing.",
  );
const before = "const decodeComponent = require('decode-uri-component');";
const after =
  "const decodeComponent = require('decode-uri-component').default;";
const code = fs.readFileSync(source, "utf8");
const original = code.replace(after, before);
const hash = createHash("sha256").update(original).digest("hex");
const expected = require("./query-decoder-patch.json").sourceSha256;
if (hash !== expected || !original.includes(before))
  throw new Error(
    "query-string source changed; review the decoder patch before installing.",
  );
if (process.argv.includes("--check")) {
  if (!code.includes(after))
    throw new Error(
      "The decoder compatibility patch was not applied. Run npm ci with install scripts.",
    );
} else if (code !== original.replace(before, after)) {
  fs.writeFileSync(source, original.replace(before, after));
}
const decoder = require("decode-uri-component").default;
if (typeof decoder !== "function" || decoder("%E2%9C%93") !== "✓")
  throw new Error("The patched decoder's module contract is incompatible.");
console.log(
  "query-string 7.1.3 uses the patched decoder's ESM default export.",
);
