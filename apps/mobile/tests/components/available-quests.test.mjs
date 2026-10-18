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
let session, sent, navigation, params, canGoBack;
const load = Module._load;
const editor = ({ onClose, closeLabel }) =>
  React.createElement("button", { onClick: onClose }, closeLabel);
Module._load = function (name, ...rest) {
  if (name === "react-native") return nativeWeb;
  if (name.endsWith("/session")) return { useSession: () => session };
  if (name.endsWith("/session-page"))
    return {
      Page: ({ children }) => React.createElement("main", null, children),
    };
  if (name === "./quest-editor") return { QuestEditor: editor };
  if (name === "./series-editor") return { SeriesEditor: editor };
  if (name.endsWith("/profile-editor"))
    return {
      ProfileEditor: () => React.createElement("span", null, "Onboarding"),
    };
  if (name === "expo-router")
    return {
      useLocalSearchParams: () => params,
      useRouter: () => ({
        push: (href) => navigation.push(href),
        back: () => navigation.push("back"),
        replace: (href) => navigation.push(href),
        canGoBack: () => canGoBack,
      }),
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
const {
  AvailableQuests,
} = require("../../src/features/quests/available-quests.tsx");
const AvailableQuestScreen =
  require("../../src/app/quest/available/[id].tsx").default;
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
  navigation = [];
  params = { id: "q" };
  canGoBack = true;
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
async function mount(Component = AvailableQuests, props = {}) {
  const element = document.createElement("div");
  document.body.append(element);
  const root = createRoot(element);
  const view = {
    element,
    async render() {
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
      return [...element.querySelectorAll('[role="button"],button')].find(
        (item) => item.textContent === name,
      );
    },
    async click(name) {
      const button = view.button(name);
      assert.ok(button, name);
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

test("compact cards keep interest order, filter unavailable definitions, and open details without accepting", async () => {
  reset();
  session.state.definitions = [
    {
      quest: {
        ...basic,
        id: "custom",
        title: "My downtime",
        categoryId: "c02",
        isCustom: true,
      },
      revision: 1,
      archived: false,
    },
    { quest: { ...basic, id: "archive", title: "Archived" }, archived: true },
  ];
  session.catalog.quests.push(
    { ...basic, id: "active", title: "Active elsewhere" },
    { ...basic, id: "frozen", title: "Frozen elsewhere" },
  );
  session.state.occurrences = ["active", "frozen"].map((id) => ({
    status: id === "active" ? "Active" : "Frozen",
    occurrence: { quest: { id } },
  }));
  const view = await mount(AvailableQuests, { initialDate: "2026-10-21" });
  try {
    assert.ok(
      view.element.textContent.indexOf("My downtime") <
        view.element.textContent.indexOf("Practice"),
    );
    assert.doesNotMatch(
      view.element.textContent,
      /Archived|Active elsewhere|Frozen elsewhere|Finish one practice|Attribute pool|Plan before accepting/,
    );
    await view.click("View quest & rewards: Practice");
    assert.deepEqual(navigation, [
      {
        pathname: "/quest/available/[id]",
        params: { id: "q", date: "2026-10-21" },
      },
    ]);
    assert.equal(sent.length, 0);
    await view.click("Start: Practice");
    assert.equal(sent[0].dueDate, "2026-10-21");
    assert.equal(sent[0].plannedTime, null);
    await view.click("Recreation");
    assert.equal(view.button("Start: Practice"), undefined);
    session.state.definitions[0].archived = true;
    await view.render();
    assert.match(
      view.element.textContent,
      /No available quests in this selection/,
    );
  } finally {
    await view.close();
  }
});

test("quick start uses today, respects pending and rank gates, and penalty cards require details", async () => {
  reset();
  const view = await mount();
  try {
    await view.click("Start: Practice");
    assert.equal(sent[0].dueDate, session.state.today);
    for (const flag of ["busy", "pending"]) {
      session[flag] = true;
      await view.render();
      assert.equal(
        view.button("Start: Practice").getAttribute("aria-disabled"),
        "true",
      );
      session[flag] = false;
    }
    session.catalog.quests[0].rank = "E";
    await view.render();
    assert.equal(
      view.button("Start: Practice").getAttribute("aria-disabled"),
      "true",
    );
    assert.ok(view.button("View quest & rewards: Practice"));
    session.catalog.quests[0].rank = "F";
    session.catalog.quests[0].penaltyPercent = 25;
    await view.render();
    assert.equal(view.button("Start: Practice"), undefined);
    await view.click("Review before starting: Practice");
    assert.equal(sent.length, 1);
    assert.equal(navigation.length, 1);
  } finally {
    await view.close();
  }
});

test("details show actual reward pools and submit the reviewed plan without losing drafts in editors", async () => {
  reset();
  params.date = "2026-10-21";
  session.catalog.quests = [];
  session.state.definitions = [
    { quest: { ...basic, isCustom: true }, revision: 2, archived: false },
  ];
  const view = await mount(AvailableQuestScreen);
  try {
    assert.match(view.element.textContent, /Overall \+10 XP/);
    assert.ok(
      view.element.textContent.includes(
        "Available to pick up · Rank F · Small",
      ),
    );
    assert.match(view.element.textContent, /Strength \+10 XP/);
    assert.match(view.element.textContent, /Finish one practice/);
    assert.equal(
      view.field("Due date in YYYY-MM-DD format").value,
      "2026-10-21",
    );
    await view.fill("Due date in YYYY-MM-DD format", "2026-10-22");
    await view.fill("Optional planned time HH:mm", "09:30");
    for (const action of ["Edit: Practice", "Set recurrence: Practice"]) {
      await view.click(action);
      await view.click("Back to quest details");
      assert.equal(
        view.field("Due date in YYYY-MM-DD format").value,
        "2026-10-22",
      );
      assert.equal(view.field("Optional planned time HH:mm").value, "09:30");
    }
    await view.click("Start: Practice");
    assert.equal(sent[0].dueDate, "2026-10-22");
    assert.equal(sent[0].plannedTime, "09:30");
    await view.click("Leave unscheduled instead");
    await view.click("Start: Practice");
    assert.equal(sent[1].dueDate, null);
    assert.equal(sent[1].plannedTime, null);
    session.state.definitions[0].revision = 3;
    await view.render();
    await view.click("Archive definition: Practice");
    assert.equal(sent[2].expectedRevision, 3);
    await view.click("Back to available quests");
    assert.deepEqual(navigation, ["back"]);
  } finally {
    await view.close();
  }
});

test("penalty consent is invalidated by changed terms, due dates and unscheduled plans", async () => {
  reset();
  session.catalog.quests[0].penaltyPercent = 25;
  const view = await mount(AvailableQuestScreen);
  const disabled = () =>
    assert.equal(
      view.button("Start: Practice").getAttribute("aria-disabled"),
      "true",
    );
  try {
    disabled();
    await view.click("Confirm penalty terms: Practice");
    await view.click("Start: Practice");
    assert.equal(sent[0].acceptedLoss, 3);
    assert.deepEqual(sent[0].acceptedQuest, session.catalog.quests[0]);
    session.catalog.quests[0] = { ...basic, penaltyPercent: 50 };
    await view.render();
    disabled();
    await view.click("Confirm penalty terms: Practice");
    await view.fill("Due date in YYYY-MM-DD format", "2026-10-24");
    disabled();
    await view.click("Confirm penalty terms: Practice");
    await view.click("Leave unscheduled instead");
    disabled();
    await view.click("Confirm penalty terms: Practice");
    disabled();
    assert.equal(sent.length, 1);
  } finally {
    await view.close();
  }
});

test("cold detail routes wait for both account and catalog restoration before resolving the quest", async () => {
  reset();
  const loaded = { ...session };
  session = {
    ...loaded,
    signedIn: false,
    busy: true,
    state: null,
    catalog: null,
  };
  const view = await mount(AvailableQuestScreen);
  try {
    assert.match(view.element.textContent, /Loading your quest/);
    assert.doesNotMatch(view.element.textContent, /Quest unavailable/);
    assert.equal(view.button("Back to available quests"), undefined);
    session = { ...loaded, busy: true, catalog: null };
    await view.render();
    assert.match(view.element.textContent, /Loading your quest/);
    session = loaded;
    await view.render();
    assert.ok(view.button("Start: Practice"));
    assert.doesNotMatch(
      view.element.textContent,
      /Loading your quest|Quest unavailable/,
    );
    assert.equal(sent.length, 0);
  } finally {
    await view.close();
  }
});

test("detail routes reject invalid dates, missing or accepted quests and signed-out access", async () => {
  reset();
  const view = await mount(AvailableQuestScreen);
  try {
    session.state.occurrences = [
      { status: "Active", occurrence: { quest: basic } },
    ];
    await view.render();
    assert.match(view.element.textContent, /Quest unavailable/);
    assert.equal(view.button("Start: Practice"), undefined);
    session.state.occurrences = [];
    params.date = "2026-02-30";
    await view.render();
    assert.match(view.element.textContent, /Invalid planning date/);
    delete params.date;
    params.id = "missing";
    await view.render();
    assert.match(view.element.textContent, /Quest unavailable/);
    canGoBack = false;
    await view.click("Back to available quests");
    assert.deepEqual(navigation, ["/(tabs)/board"]);
    session.signedIn = false;
    await view.render();
    assert.ok(view.element.querySelector('[data-redirect="/"]'));
    assert.equal(sent.length, 0);
  } finally {
    await view.close();
  }
});
