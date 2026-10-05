import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";
import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { type, generate } = require("../../scripts/generate-api.cjs");
const ts = require("typescript");
const root = fileURLToPath(new URL("../../../../", import.meta.url));
const document = JSON.parse(
  fs.readFileSync(
    path.join(root, "contracts/pocketquests-v1.openapi.json"),
    "utf8",
  ),
);
const fixtures = JSON.parse(
  fs.readFileSync(path.join(root, "contracts/wire-fixtures.json"), "utf8"),
);
function matches(value, schema) {
  if (typeof schema === "boolean") return schema;
  if (schema.nullable && value === null) return true;
  if (
    typeof value === "string" &&
    schema.pattern &&
    !new RegExp(schema.pattern).test(value)
  )
    return false;
  if (schema.$ref)
    return matches(
      value,
      document.components.schemas[schema.$ref.split("/").at(-1)],
    );
  if (schema.anyOf)
    return schema.anyOf.some((candidate) => matches(value, candidate));
  if (schema.oneOf)
    return (
      schema.oneOf.filter((candidate) => matches(value, candidate)).length === 1
    );
  if (schema.allOf)
    return schema.allOf.every((candidate) => matches(value, candidate));
  if (schema.enum) return schema.enum.includes(value);
  if (schema.type === "null") return value === null;
  if (schema.type === "array")
    return (
      Array.isArray(value) && value.every((item) => matches(item, schema.items))
    );
  if (schema.type === "object")
    return (
      value !== null &&
      typeof value === "object" &&
      !Array.isArray(value) &&
      (schema.required ?? []).every((name) => Object.hasOwn(value, name)) &&
      Object.entries(value).every(([name, child]) =>
        schema.properties?.[name]
          ? matches(child, schema.properties[name])
          : matches(child, schema.additionalProperties ?? true),
      )
    );
  if (schema.type === "integer") return Number.isInteger(value);
  return typeof value === schema.type;
}
test("real .NET serialized completion and catalog conform to the committed wire schemas", () => {
  assert.ok(matches(fixtures.state, document.components.schemas.QuestState));
  assert.ok(
    matches(fixtures.catalog, document.components.schemas.CatalogResponse),
  );
  assert.equal(fixtures.state.occurrences[0].status, "Completed");
  assert.ok(
    fixtures.catalog.rules.length > 0,
    "the client includes rules omitted by its prior handwritten contract",
  );
  const corrupted = structuredClone(fixtures.state);
  corrupted.overallXp = "wrong shape";
  assert.equal(
    matches(corrupted, document.components.schemas.QuestState),
    false,
  );
});
test("endpoint schemas preserve required nullable output and accepted numeric-string inputs", () => {
  const schemas = document.components.schemas;
  const missing = structuredClone(fixtures.state);
  delete missing.completionOutcome;
  assert.equal(matches(missing, schemas.QuestState), false);
  const nullable = structuredClone(fixtures.state);
  nullable.futureWarnings = null;
  nullable.completionOutcome = null;
  assert.equal(matches(nullable, schemas.QuestState), true);
  assert.equal(
    matches({ id: "skill", basisPoints: "10000" }, schemas.ShareInput),
    true,
  );
  assert.equal(
    matches({ id: "skill", basisPoints: "invalid" }, schemas.ShareInput),
    false,
  );
  assert.equal(
    matches({ id: "skill", basisPoints: "10000" }, schemas.Share),
    false,
  );
  const input = structuredClone(fixtures.catalog.quests[0]);
  for (const optional of [
    "description",
    "baseXp",
    "isCustom",
    "penaltyPercent",
  ])
    delete input[optional];
  assert.equal(matches(input, schemas.QuestInput), true);
  assert.equal(matches(input, schemas.Quest), false);
});
test("emitter preserves unions in arrays, nullable references, dictionaries and boolean schemas", () => {
  assert.equal(
    type({
      type: "array",
      items: { anyOf: [{ type: "number" }, { type: "string" }] },
      nullable: true,
    }),
    "(number | string)[] | null",
  );
  assert.equal(
    type({
      oneOf: [
        { enum: [null], nullable: true },
        { $ref: "#/components/schemas/QuestInput" },
      ],
    }),
    "null | QuestInput",
  );
  assert.equal(
    type({ type: "object", additionalProperties: { type: "number" } }),
    "Record<string, number>",
  );
  assert.equal(
    type({ type: "object", additionalProperties: true }),
    "Record<string, unknown>",
  );
  assert.equal(type(false), "never");
  assert.throws(() => type({ type: "unexpected" }), /Unsupported/);
});
test("JSON operation queries respect locations, types and requiredness; binary paths stay separate", () => {
  const changed = structuredClone(document);
  changed.paths["/api/planning/zone"].get.parameters.push({
    name: "preview",
    in: "query",
    schema: { type: "boolean" },
  });
  const emitted = generate(changed);
  assert.match(emitted, /"zone": string; "preview"\?: boolean/);
  assert.doesNotMatch(emitted, /GET \/api\/export/);
  changed.paths["/api/planning/zone"].get.parameters[0].in = "path";
  assert.throws(
    () => generate(changed),
    /Unsupported JSON operation parameters/,
  );
});
test("generated types compile real request/output assignments and reject narrowed or optional output", () => {
  const filename = path.join(
    root,
    "apps/mobile/src/api/contract-type-probe.ts",
  );
  const source = `import type { Quest, QuestInput, Share, ShareInput, QuestState, QuestCommand, ApiOperations } from './generated';
const input: ShareInput = { id: 'skill', basisPoints: '10000' };
// @ts-expect-error Responses have JSON numbers.
const output: Share = input;
declare const quest: Quest;
const accepted: QuestInput = quest;
declare const optional: QuestInput;
// @ts-expect-error Responses include every serialized property.
const missing: Quest = optional;
declare const state: QuestState;
const xp: number = state.overallXp;
const outcome: QuestState['completionOutcome'] = null;
const command: QuestCommand = { operationId: 'id', action: 'Accept', expectedRevision: '1', acceptedQuest: optional };
const body: ApiOperations['POST /api/commands']['body'] = command;
// @ts-expect-error Planning query requires zone.
const query: ApiOperations['GET /api/planning/clock']['query'] = { date: '2026-10-04', time: '12:00' };
// @ts-expect-error Nullable response fields are still required.
const invalid: QuestState = {} as Omit<QuestState, 'completionOutcome'> & { completionOutcome?: null };
`;
  const options = {
    strict: true,
    noEmit: true,
    skipLibCheck: true,
    types: [],
    target: ts.ScriptTarget.ES2022,
    module: ts.ModuleKind.CommonJS,
    ignoreDeprecations: "6.0",
  };
  const host = ts.createCompilerHost(options);
  const read = host.readFile.bind(host);
  const exists = host.fileExists.bind(host);
  host.readFile = (name) =>
    path.resolve(name) === filename ? source : read(name);
  host.fileExists = (name) => path.resolve(name) === filename || exists(name);
  const diagnostics = ts.getPreEmitDiagnostics(
    ts.createProgram([filename], options, host),
  );
  assert.deepEqual(
    diagnostics.map((item) =>
      ts.flattenDiagnosticMessageText(item.messageText, "\n"),
    ),
    [],
  );
});
test("generated client drift fails when OpenAPI changes without regeneration", () => {
  const temp = fs.mkdtempSync(path.join(os.tmpdir(), "pq-api-contract-"));
  try {
    fs.mkdirSync(path.join(temp, "apps/mobile/scripts"), { recursive: true });
    fs.mkdirSync(path.join(temp, "apps/mobile/src/api"), { recursive: true });
    fs.mkdirSync(path.join(temp, "contracts"));
    for (const file of [
      "apps/mobile/scripts/generate-api.cjs",
      "apps/mobile/src/api/generated.ts",
      "contracts/pocketquests-v1.openapi.json",
    ])
      fs.copyFileSync(path.join(root, file), path.join(temp, file));
    const run = () =>
      spawnSync(
        process.execPath,
        [path.join(temp, "apps/mobile/scripts/generate-api.cjs"), "--check"],
        { encoding: "utf8" },
      );
    assert.equal(run().status, 0);
    const changed = structuredClone(document);
    changed.components.schemas.Rank.enum.push("FutureRank");
    fs.writeFileSync(
      path.join(temp, "contracts/pocketquests-v1.openapi.json"),
      JSON.stringify(changed),
    );
    const failed = run();
    assert.notEqual(failed.status, 0);
    assert.match(failed.stderr, /drifted/);
  } finally {
    fs.rmSync(temp, { recursive: true, force: true });
  }
});
