import "./artwork-fixture.mjs";
import test from "node:test";
import assert from "node:assert/strict";
import Module, { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { JSDOM } = require("jsdom");
const dom = new JSDOM("<!doctype html><html><body></body></html>");
global.window = dom.window;
global.document = dom.window.document;
global.HTMLElement = dom.window.HTMLElement;
global.ShadowRoot = dom.window.ShadowRoot;
global.IS_REACT_ACT_ENVIRONMENT = true;
const React = require("react");
const { act } = React;
const { createRoot } = require("react-dom/client");
const nativeWeb = require("react-native-web");
const ts = require("typescript");
const load = Module._load;
Module._load = function (name, ...rest) {
  if (name === "react-native") return nativeWeb;
  if (name === "expo-router")
    return {
      Link: ({ children, href, asChild }) => {
        const url =
          typeof href === "string"
            ? href
            : href.pathname
                .replace("/(tabs)", "")
                .replace("[id]", href.params?.id ?? "") +
              (href.params?.view ? `?view=${href.params.view}` : "");
        return asChild
          ? React.cloneElement(children, { href: url })
          : React.createElement("a", { href: url }, children);
      },
    };
  return load.call(this, name, ...rest);
};
for (const extension of [".ts", ".tsx"])
  require.extensions[extension] = (module, file) =>
    module._compile(
      ts.transpileModule(require("node:fs").readFileSync(file, "utf8"), {
        fileName: file,
        compilerOptions: {
          module: ts.ModuleKind.CommonJS,
          jsx: ts.JsxEmit.ReactJSX,
          target: ts.ScriptTarget.ES2022,
        },
      }).outputText,
      file,
    );
const { ThemeProvider } = require("@honeydrunk/ui-native");
const { pocketQuestsTheme } = require("../../src/shared/theme.ts");
const {
  dailyPlan,
  dailyIdeas,
} = require("../../src/features/quests/daily-plan.ts");
const { TodayQuests } = require("../../src/features/quests/today-quests.tsx");
function quest(id, categoryId = "c01", changes = {}) {
  return {
    id,
    title: `Quest ${id}`,
    criterion: "Do one small thing",
    categoryId,
    rank: "F",
    effort: "Small",
    baseXp: 10,
    skills: [],
    attributes: [],
    isCustom: false,
    ...changes,
  };
}
function fixture() {
  return {
    today: "2026-11-02",
    zone: "America/New_York",
    occurrences: [],
    definitions: [],
    profile: { interests: ["c02"] },
    skills: [],
    categories: [
      { id: "c01", name: "Health & Fitness", level: 1 },
      { id: "c02", name: "Learning", level: 1 },
    ],
  };
}
function occurrence(id, dueDate, status = "Active", plannedTime = null) {
  return {
    status,
    occurrence: {
      id,
      quest: quest(id),
      dueDate,
      plannedTime,
      acceptedAt: "2026-11-01T12:00:00Z",
    },
  };
}
async function mount(
  state,
  catalog = { quests: [], categories: state.categories },
  pending = false,
) {
  const element = document.createElement("div");
  document.body.append(element);
  const root = createRoot(element);
  await act(async () =>
    root.render(
      React.createElement(
        ThemeProvider,
        { theme: pocketQuestsTheme },
        React.createElement(TodayQuests, {
          state,
          catalog,
          completedToday: 2,
          pending,
        }),
      ),
    ),
  );
  return {
    element,
    async close() {
      await act(async () => root.unmount());
      element.remove();
    },
  };
}
test("Home prioritizes timed plans, then untimed and unscheduled quests without reclassifying earlier or frozen commitments", () => {
  const state = fixture();
  state.occurrences = [
    occurrence("unscheduled", null),
    occurrence("late", state.today, "Active", "17:00"),
    occurrence("untimed", state.today),
    occurrence("early", state.today, "Active", "08:00"),
    occurrence("paused", state.today, "Frozen"),
    occurrence("earlier", "2026-11-01"),
    occurrence("future", "2026-11-04"),
    occurrence("done", state.today, "Completed"),
    occurrence("missed", "2026-11-01", "Missed"),
  ];
  const before = structuredClone(state);
  const result = dailyPlan(state);
  assert.deepEqual(
    result.next.map((item) => item.occurrence.id),
    ["early", "late", "untimed"],
  );
  assert.equal(result.today.length, 3);
  assert.equal(result.unscheduled.length, 1);
  assert.equal(result.earlier, 1);
  assert.equal(result.paused, 1);
  assert.equal(result.upcoming, 1);
  assert.deepEqual(state, before);
});
test("Home ideas respect interests, category variety, skill gates, active commitments and pending drafts", () => {
  const state = fixture();
  state.occurrences = [occurrence("active", state.today)];
  state.definitions = [
    { quest: quest("draft"), pendingSave: true, archived: false },
  ];
  const catalog = {
    categories: state.categories,
    quests: [
      quest("general"),
      quest("interest", "c02"),
      quest("interest-two", "c02"),
      quest("locked", "c03", { rank: "E" }),
      quest("penalty", "c04", { penaltyPercent: 10 }),
      quest("medium", "c05", { effort: "Medium" }),
      quest("active"),
    ],
  };
  const before = structuredClone({ state, catalog });
  assert.deepEqual(
    dailyIdeas(catalog, state).map((item) => item.id),
    ["interest", "general"],
  );
  assert.deepEqual({ state, catalog }, before);
  assert.deepEqual(dailyIdeas(null, state), []);
});
test("Home links to the committed occurrence and correct board section without offering direct completion writes", async () => {
  const state = fixture();
  state.occurrences = [occurrence("practice", state.today, "Active", "08:00")];
  const view = await mount(state, undefined, true);
  try {
    assert.equal(
      view.element
        .querySelector('[aria-label="Open planned quest: Quest practice"]')
        .getAttribute("href"),
      "/quest/practice",
    );
    assert.match(view.element.textContent, /Today · 08:00/);
    assert.match(
      view.element.textContent,
      /count updates after changes finish saving/,
    );
    assert.equal(
      [...view.element.querySelectorAll("a")]
        .find((item) => item.textContent === "See your active quests")
        .getAttribute("href"),
      "/board?view=Active",
    );
    assert.equal(
      [...view.element.querySelectorAll("a")]
        .find((item) => item.textContent === "Browse all quests")
        .getAttribute("href"),
      "/board?view=Available",
    );
    assert.equal(view.element.querySelector('[role="button"]'), null);
    assert.doesNotMatch(view.element.textContent, /America\/New_York/);
  } finally {
    await view.close();
  }
});
test("an empty day offers small illustrated ideas to plan, preserving all account data", async () => {
  const state = fixture();
  const before = structuredClone(state);
  const view = await mount(state, {
    categories: state.categories,
    quests: [quest("read", "c02")],
  });
  try {
    assert.match(view.element.textContent, /A few ideas/);
    assert.equal(
      view.element
        .querySelector('[aria-label="Plan a suggested quest: Quest read"]')
        .getAttribute("href"),
      "/quest/available/read",
    );
    assert.deepEqual(state, before);
    assert.doesNotMatch(view.element.textContent, /No quests|Missed/);
  } finally {
    await view.close();
  }
});
