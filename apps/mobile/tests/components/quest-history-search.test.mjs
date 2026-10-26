import test from "node:test";
import assert from "node:assert/strict";
import Module, { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { JSDOM } = require("jsdom");
const dom = new JSDOM("<!doctype html><html><body></body></html>");
global.window = dom.window;
global.document = dom.window.document;
global.ShadowRoot = dom.window.ShadowRoot;
global.HTMLElement = dom.window.HTMLElement;
global.IS_REACT_ACT_ENVIRONMENT = true;
const React = require("react"),
  { act } = React;
const { createRoot } = require("react-dom/client");
const nativeWeb = require("react-native-web");
const ts = require("typescript");
let session, sent;
const load = Module._load;
let renderedSections;
Module._load = function (name, ...rest) {
  if (name === "react-native") return nativeWeb;
  if (name.endsWith("/session")) return { useAccountSnapshot: () => session };
  if (name.endsWith("/session-page"))
    return {
      Page: ({ children }) => React.createElement("main", null, children),
    };
  if (name.endsWith("/journal-heading")) return { JournalHeading: () => null };
  if (name === "./available-quests")
    return {
      AvailableQuests: () =>
        React.createElement("div", null, "Available quest content"),
    };
  if (name === "./quest-history")
    return {
      QuestHistory: ({ children, sections }) => {
        renderedSections = sections;
        return React.createElement(
          "main",
          null,
          React.createElement("div", { key: "controls" }, children),
          ...sections.map((section) =>
            React.createElement(
              "section",
              { key: section.key, "data-group": section.key },
              section.title && React.createElement("h2", null, section.title),
              ...section.data.map((item) =>
                React.createElement(
                  "p",
                  { key: item.occurrence.id, "data-quest": item.occurrence.id },
                  item.occurrence.quest.title,
                ),
              ),
              section.data.length ? null : section.emptyContent,
            ),
          ),
        );
      },
    };
  if (name === "expo-router")
    return {
      Redirect: ({ href }) =>
        React.createElement("span", { "data-redirect": href }),
    };
  return load.call(this, name, ...rest);
};
for (const extension of [".ts", ".tsx"])
  require.extensions[extension] = (module, file) =>
    module._compile(
      ts.transpileModule(require("node:fs").readFileSync(file, "utf8"), {
        fileName: file,
        compilerOptions: {
          inlineSourceMap: true,
          inlineSources: true,
          module: ts.ModuleKind.CommonJS,
          jsx: ts.JsxEmit.ReactJSX,
          target: ts.ScriptTarget.ES2022,
        },
      }).outputText,
      file,
    );
const { ThemeProvider } = require("@honeydrunk/ui-native");
const { pocketQuestsTheme } = require("../../src/shared/theme.ts");
const { QuestBoard } = require("../../src/features/quests/quest-board.tsx");
const basic = {
  id: "q",
  title: "Practice",
  criterion: "Finish one practice",
  description: "A deliberate session",
  categoryId: "c01",
  rank: "F",
  effort: "Small",
  baseXp: 10,
  attributes: [{ id: "a01", basisPoints: 10000 }],
  skills: [],
  isCustom: false,
  penaltyPercent: 0,
};
function reset() {
  sent = [];
  session = {
    signedIn: true,
    busy: false,
    pending: false,
    offline: true,
    command: async (command) => sent.push(command),
    catalog: {
      quests: [{ ...basic }],
      categories: [
        { id: "c01", name: "Health" },
        { id: "c02", name: "Recreation" },
      ],
      attributes: [{ id: "a01", name: "Strength" }],
      skills: [],
    },
    state: {
      today: "2026-10-18",
      zone: "America/New_York",
      profile: { onboardingComplete: true, interests: ["c02"] },
      definitions: [],
      occurrences: [],
      schedule: { series: [] },
      categories: [
        { id: "c01", level: 1 },
        { id: "c02", level: 1 },
      ],
      skills: [],
    },
  };
}
async function mount(Component = QuestBoard, props = {}) {
  const element = document.createElement("div");
  document.body.append(element);
  const root = createRoot(element);
  const view = {
    element,
    async render() {
      // Real account/catalog publications replace their outer snapshot objects.
      if (session.state) session.state = { ...session.state };
      if (session.catalog) session.catalog = { ...session.catalog };
      await act(async () =>
        root.render(
          React.createElement(
            ThemeProvider,
            { theme: pocketQuestsTheme },
            React.createElement(Component, props),
          ),
        ),
      );
    },
    button(name) {
      return [
        ...element.querySelectorAll(
          '[role="button"],button,[role="radio"],[role="checkbox"],[role="tab"]',
        ),
      ].find((item) => item.textContent === name);
    },
    async click(name) {
      const button = view.button(name);
      assert.ok(button, `${name}: ${view.element.textContent}`);
      await act(async () => button.click());
    },
    field(label) {
      return element.querySelector(`[aria-label="${label}"]`);
    },
    async fill(label, value) {
      await act(async () => {
        const field = view.field(label);
        Object.getOwnPropertyDescriptor(
          dom.window.HTMLInputElement.prototype,
          "value",
        ).set.call(field, value);
        field.dispatchEvent(new dom.window.Event("input", { bubbles: true }));
      });
    },
    async close() {
      await act(async () => root.unmount());
      element.remove();
    },
  };
  await view.render();
  return view;
}

function occurrence(id, status = "Active", dueDate = null, quest = {}) {
  return {
    status,
    occurrence: {
      id,
      dueDate,
      acceptedAt: "2026-10-01T12:00:00Z",
      plannedTime: null,
      quest: { ...basic, title: id, ...quest },
    },
  };
}
const ids = () =>
  renderedSections.flatMap((group) =>
    group.data.map((item) => item.occurrence.id),
  );

test("history combines local search/category/effort without writes, and clears to original grouping", async () => {
  reset();
  session.state.occurrences = [
    occurrence("today", "Active", session.state.today, {
      title: "Café practice",
    }),
    occurrence("unscheduled", "Active", null, { effort: "Medium" }),
    occurrence("frozen", "Frozen", "2026-10-17"),
    occurrence("future", "Active", "2026-10-20", { categoryId: "c02" }),
    occurrence("past", "Missed", "2026-10-16"),
    occurrence("done", "Completed"),
    occurrence("offer", "Offered"),
  ];
  const before = structuredClone(session.state);
  const view = await mount();
  try {
    assert.deepEqual(ids(), ["offer"]);
    await view.click("Active");
    assert.deepEqual(ids(), ["today", "unscheduled", "frozen", "future"]);
    assert.match(view.element.textContent, /4 quests found/);
    await view.fill("Search active quests", "  CAFE health  ");
    assert.deepEqual(ids(), ["today"]);
    assert.equal(view.field("Search active quests").value, "  CAFE health  ");
    await view.click("Show filters");
    await view.click("Health");
    await view.click("Small");
    assert.deepEqual(ids(), ["today"]);
    assert.equal(view.field("Health").getAttribute("aria-checked"), "true");
    await view.click("Medium");
    assert.deepEqual(ids(), []);
    assert.match(
      view.element.textContent,
      /No quests match your search and filters/,
    );
    assert.equal(view.element.querySelectorAll("h2").length, 0);
    await view.click("Clear search and filters");
    assert.deepEqual(ids(), ["today", "unscheduled", "frozen", "future"]);
    await view.click("Show past commitments (1)");
    assert.deepEqual(ids(), [
      "today",
      "unscheduled",
      "frozen",
      "future",
      "past",
    ]);
    assert.match(view.element.textContent, /5 quests found/);
    await view.fill("Search active quests", "past");
    assert.deepEqual(ids(), ["past"]);
    await view.click("Hide past commitments (1)");
    assert.deepEqual(ids(), []);
    await view.click("Hide filters");
    assert.equal(
      view.field("Show quest filters").getAttribute("aria-expanded"),
      "false",
    );
    assert.deepEqual(sent, []);
    assert.deepEqual(session.state, before);
  } finally {
    await view.close();
  }
});

test("completed search uses archived accepted snapshots and tab changes reset hidden constraints", async () => {
  reset();
  session.state.occurrences = [
    occurrence("one", "Completed", null, {
      title: "Café reading",
      description: null,
      criterion: "Finish a chapter",
      effort: "Large",
    }),
    occurrence("two", "Completed", null, {
      title: "Evening walk",
      categoryId: "c02",
    }),
    occurrence("active"),
  ];
  session.state.definitions = [
    { quest: { ...basic, title: "Renamed definition" }, archived: true },
  ];
  const view = await mount();
  try {
    await view.click("Completed");
    assert.deepEqual(ids(), ["one", "two"]);
    await view.fill("Search completed quests", "chapter cafe");
    assert.deepEqual(ids(), ["one"]);
    await view.click("Show filters");
    await view.click("Large");
    await view.click("Completed");
    assert.equal(view.field("Search completed quests").value, "chapter cafe");
    await view.click("Active");
    assert.equal(view.field("Search active quests").value, "");
    assert.deepEqual(ids(), ["active"]);
    assert.equal(
      view.field("Show quest filters").getAttribute("aria-expanded"),
      "false",
    );
    await view.click("Completed");
    assert.deepEqual(ids(), ["one", "two"]);
    await view.click("Show filters");
    await view.click("Recreation");
    assert.deepEqual(ids(), ["two"]);
    await view.click("All categories");
    await view.click("Large");
    await view.click("Any effort");
    assert.deepEqual(ids(), ["one", "two"]);
    await view.fill("Search completed quests", "renamed");
    assert.deepEqual(ids(), []);
    assert.deepEqual(sent, []);
  } finally {
    await view.close();
  }
});

test("search refreshes on new snapshots and clears when account data is unloaded", async () => {
  reset();
  session.state.occurrences = [occurrence("old")];
  const view = await mount();
  try {
    await view.click("Active");
    await view.fill("Search active quests", "fresh");
    assert.deepEqual(ids(), []);
    session.state.occurrences = [occurrence("fresh")];
    await view.render();
    assert.deepEqual(ids(), ["fresh"]);
    await view.fill("Search active quests", "recreation");
    assert.deepEqual(ids(), []);
    session.catalog.categories = [{ id: "c01", name: "Recreation" }];
    await view.render();
    assert.deepEqual(ids(), ["fresh"]);
    session.state = null;
    await view.render();
    assert.match(view.element.textContent, /Loading your quests/);
    reset();
    await view.render();
    assert.match(view.element.textContent, /Available quest content/);
    await view.click("Active");
    assert.equal(view.field("Search active quests").value, "");
    assert.match(view.element.textContent, /No active quests yet/);
    await view.click("Completed");
    assert.match(
      view.element.textContent,
      /Your completed quests will appear here/,
    );
    await view.click("Active");
    await view.click("Choose another quest");
    assert.match(view.element.textContent, /Available quest content/);
  } finally {
    await view.close();
  }
});

test("filtering keeps all matching history rows available to the virtualized list", async () => {
  reset();
  session.catalog = null;
  session.state.occurrences = Array.from({ length: 1000 }, (_, index) =>
    occurrence(`q${index}`, "Completed", null, {
      title: `Run ${index}`,
      categoryId: "legacy",
    }),
  );
  const before = structuredClone(session.state.occurrences);
  const view = await mount();
  try {
    await view.click("Completed");
    assert.equal(ids().length, 1000);
    await view.fill("Search completed quests", "legacy 999");
    assert.deepEqual(ids(), ["q999"]);
    await view.click("Clear search and filters");
    assert.equal(ids().length, 1000);
    assert.deepEqual(session.state.occurrences, before);
  } finally {
    await view.close();
  }
});
