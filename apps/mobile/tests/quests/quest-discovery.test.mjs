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
const {
  indexDiscoveryQuests,
  discoverQuests,
  defaultDiscoveryFilters,
  discoveryFilterCount,
} = require("../../src/features/quests/quest-discovery.ts");
const {
  availableQuests,
} = require("../../src/features/quests/quest-availability.ts");
const base = {
  id: "q",
  title: "Practice",
  description: "Take a deliberate break",
  criterion: "Finish one session",
  categoryId: "health",
  rank: "F",
  effort: "Small",
  baseXp: 10,
  skills: [],
  attributes: [],
  isCustom: false,
  penaltyPercent: 0,
};
function fixture() {
  const quests = [
    {
      ...base,
      id: "reading",
      title: "Café reading",
      categoryId: "rest",
      skills: [{ id: "writing", basisPoints: 10000 }],
      attributes: [{ id: "focus", basisPoints: 10000 }],
    },
    {
      ...base,
      id: "medium",
      title: "Long practice",
      effort: "Medium",
      baseXp: 80,
    },
    {
      ...base,
      id: "locked",
      title: "Advanced practice",
      rank: "E",
      baseXp: 13,
    },
    {
      ...base,
      id: "custom",
      title: "Personal practice",
      isCustom: true,
      penaltyPercent: 25,
    },
  ];
  const catalog = {
    quests,
    categories: [
      { id: "health", name: "Health & Movement" },
      { id: "rest", name: "Rest & Recreation" },
    ],
    skills: [{ id: "writing", name: "Creative writing" }],
    attributes: [{ id: "focus", name: "Concentration" }],
  };
  const state = {
    profile: { interests: ["rest"] },
    definitions: [],
    occurrences: [],
    categories: [
      { id: "health", level: 1 },
      { id: "rest", level: 99 },
    ],
    skills: [{ id: "writing", level: 1 }],
  };
  return { quests, catalog, state };
}
const ids = (results) => results.map((item) => item.quest.id);

test("search folds case and accents and matches every term across actual quest metadata", () => {
  const { quests, catalog, state } = fixture(),
    index = indexDiscoveryQuests(quests, catalog, state);
  for (const query of [
    "  CAFE  reading ",
    "creative recreation",
    "deliberate concentration",
    "finish session cafe",
  ])
    assert.deepEqual(
      ids(discoverQuests(index, { ...defaultDiscoveryFilters, query })),
      ["reading"],
    );
  assert.deepEqual(
    ids(
      discoverQuests(index, { ...defaultDiscoveryFilters, query: "unrelated" }),
    ),
    [],
  );
  assert.equal(
    discoverQuests(index, { ...defaultDiscoveryFilters, query: " \n\t " })
      .length,
    4,
  );
});

test("search includes the player's custom skill names", () => {
  const { quests, catalog, state } = fixture();
  state.profile.customSkills = [
    { id: "custom-skill", name: "Watercolour painting" },
  ];
  quests[3].skills = [{ id: "custom-skill", basisPoints: 10000 }];
  const index = indexDiscoveryQuests(quests, catalog, state);
  assert.deepEqual(
    ids(
      discoverQuests(index, {
        ...defaultDiscoveryFilters,
        query: "watercolour",
      }),
    ),
    ["custom"],
  );
});

test("category, effort, source, readiness and penalty filters intersect without mutating inputs", () => {
  const { quests, catalog, state } = fixture(),
    original = structuredClone(quests),
    index = indexDiscoveryQuests(quests, catalog, state);
  assert.deepEqual(
    ids(
      discoverQuests(index, {
        ...defaultDiscoveryFilters,
        categoryId: "health",
        effort: "Small",
        readyOnly: true,
        penaltyFreeOnly: true,
      }),
    ),
    [],
  );
  assert.deepEqual(
    ids(
      discoverQuests(index, { ...defaultDiscoveryFilters, source: "custom" }),
    ),
    ["custom"],
  );
  assert.deepEqual(
    ids(
      discoverQuests(index, {
        ...defaultDiscoveryFilters,
        source: "starter",
        effort: "Medium",
      }),
    ),
    ["medium"],
  );
  assert.deepEqual(
    ids(discoverQuests(index, { ...defaultDiscoveryFilters, readyOnly: true })),
    ["reading", "medium", "custom"],
  );
  assert.deepEqual(quests, original);
  assert.equal(
    discoveryFilterCount({
      ...defaultDiscoveryFilters,
      categoryId: "rest",
      readyOnly: true,
      sort: "xp",
    }),
    3,
  );
});

test("ready-to-start uses skill eligibility rather than category or global rank alone", () => {
  const { catalog, state } = fixture();
  const quest = {
    ...base,
    id: "skill-gate",
    rank: "E",
    categoryId: "rest",
    skills: [{ id: "writing", basisPoints: 10000 }],
  };
  assert.equal(
    discoverQuests(indexDiscoveryQuests([quest], catalog, state), {
      ...defaultDiscoveryFilters,
      readyOnly: true,
    }).length,
    0,
  );
  state.skills[0].level = 5;
  assert.equal(
    discoverQuests(indexDiscoveryQuests([quest], catalog, state), {
      ...defaultDiscoveryFilters,
      readyOnly: true,
    }).length,
    1,
  );
});

test("sorting is explicit, recommendation order stays stable and locked quests remain discoverable by default", () => {
  const { quests, catalog, state } = fixture(),
    index = indexDiscoveryQuests(quests, catalog, state);
  assert.deepEqual(ids(discoverQuests(index, defaultDiscoveryFilters)), [
    "reading",
    "medium",
    "locked",
    "custom",
  ]);
  assert.deepEqual(
    ids(discoverQuests(index, { ...defaultDiscoveryFilters, sort: "xp" })),
    ["medium", "locked", "reading", "custom"],
  );
  assert.deepEqual(
    ids(discoverQuests(index, { ...defaultDiscoveryFilters, sort: "title" })),
    ["locked", "reading", "medium", "custom"],
  );
  assert.deepEqual(
    index.map((item) => item.quest.id),
    ["reading", "medium", "locked", "custom"],
  );
});

test("discovery preserves availability exclusions and refreshed saved interests", () => {
  const { catalog, state } = fixture();
  state.definitions = [
    { quest: { ...base, id: "archived", isCustom: true }, archived: true },
  ];
  state.occurrences = [
    { status: "Active", occurrence: { quest: { id: "medium" } } },
    { status: "Frozen", occurrence: { quest: { id: "custom" } } },
  ];
  assert.deepEqual(
    ids(indexDiscoveryQuests(availableQuests(catalog, state), catalog, state)),
    ["reading", "locked"],
  );
  assert.deepEqual(discoverQuests([], defaultDiscoveryFilters), []);
  assert.deepEqual(indexDiscoveryQuests([], null, null), []);
});
