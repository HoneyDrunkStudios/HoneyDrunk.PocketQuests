import test from "node:test";
import assert from "node:assert/strict";
import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const ts = require("typescript");
require.extensions[".ts"] = (module, file) =>
  module._compile(
    ts.transpileModule(require("node:fs").readFileSync(file, "utf8"), {
      compilerOptions: {
        module: ts.ModuleKind.CommonJS,
        target: ts.ScriptTarget.ES2022,
      },
    }).outputText,
    file,
  );
const { searchable } = require("../../src/features/quests/quest-search.ts");
const {
  indexDiscoveryQuests,
  discoverQuests,
  defaultDiscoveryFilters,
} = require("../../src/features/quests/quest-discovery.ts");
const {
  indexQuestHistory,
  questHistoryMatcher,
} = require("../../src/features/quests/quest-history-search.ts");

for (const apostrophe of ["'", "‘", "’", "ʼ"]) {
  test(`quest search folds apostrophe ${apostrophe} without removing word boundaries`, () => {
    assert.equal(
      searchable(`CAFÉ ${apostrophe}reader${apostrophe}s`),
      "cafe 'reader's",
    );
    assert.notEqual(searchable("we're"), searchable("were"));
  });
}
for (const hyphen of ["-", "‐", "‑"]) {
  test(`quest search folds hyphen ${hyphen} without joining unrelated words`, () => {
    assert.equal(searchable(`Ten${hyphen}minute walk`), "ten-minute walk");
    assert.notEqual(searchable("re-sign"), searchable("resign"));
  });
}

const quest = Object.freeze({
  id: "reading",
  title: "Reader’s café",
  description: "Take a ten‑minute reading break",
  criterion: "Finish one session",
  categoryId: "rest",
  effort: "Small",
  rank: "F",
  baseXp: 10,
  skills: [],
  attributes: [],
  isCustom: true,
  penaltyPercent: 0,
});
const categories = [{ id: "rest", name: "Rest & Recreation" }];

test("available quests match smart punctuation in either query or indexed text", () => {
  const ascii = {
    ...quest,
    id: "ascii",
    title: "Reader's cafe",
    description: "A ten-minute break",
  };
  const index = indexDiscoveryQuests(
    [quest, ascii],
    { categories, skills: [], attributes: [] },
    null,
  );
  for (const query of [
    "reader's ten-minute",
    "reader’s ten‑minute",
    "readerʼs ten‐minute",
  ]) {
    assert.deepEqual(
      discoverQuests(index, { ...defaultDiscoveryFilters, query }).map(
        ({ quest }) => quest.id,
      ),
      ["reading", "ascii"],
    );
  }
  assert.deepEqual(
    discoverQuests(index, {
      ...defaultDiscoveryFilters,
      query: "reader's",
      categoryId: "other",
    }),
    [],
  );
  assert.equal(quest.title, "Reader’s café");
  assert.equal(quest.description, "Take a ten‑minute reading break");
});

test("accepted quest history uses the same normalization and retains effort filters", () => {
  const occurrence = { occurrence: { id: "accepted", quest } };
  const index = indexQuestHistory([occurrence], categories);
  const filters = {
    query: "reader's ten-minute",
    categoryId: "rest",
    effort: "Small",
  };
  assert.equal(questHistoryMatcher(index, filters)(occurrence), true);
  assert.equal(
    questHistoryMatcher(index, { ...filters, effort: "Medium" })(occurrence),
    false,
  );
  assert.equal(
    questHistoryMatcher(index, { ...filters, query: "reader's missing" })(
      occurrence,
    ),
    false,
  );
  assert.equal(occurrence.occurrence.quest, quest);
});
