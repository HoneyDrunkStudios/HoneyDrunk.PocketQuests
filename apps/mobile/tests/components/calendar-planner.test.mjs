import test from "node:test";
import assert from "node:assert/strict";
import Module, { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { JSDOM } = require("jsdom");
const dom = new JSDOM("<!doctype html><html><body></body></html>");
global.window = dom.window;
global.document = dom.window.document;
global.IS_REACT_ACT_ENVIRONMENT = true;
const React = require("react"),
  { act } = React;
const { createRoot } = require("react-dom/client");
process.env.PQ_REAL_REACT = "1";
require("../session/provider-harness.cjs");
const originalLoad = Module._load;
let state, sent, inputHandlers;
const container = ({ children, accessibilityRole }) =>
  React.createElement("div", { role: accessibilityRole }, children);
const Button = ({ title, onPress, disabled }) =>
  React.createElement("button", { onClick: onPress, disabled }, title);
const Input = ({ accessibilityLabel, value, onChangeText }) => {
  inputHandlers.set(accessibilityLabel, onChangeText);
  return React.createElement("input", {
    "aria-label": accessibilityLabel,
    value,
    onChange: (e) => onChangeText(e.target.value),
  });
};
const quest = {
  id: "q",
  title: "Practice",
  criterion: "Practice once",
  rank: "F",
  effort: "Small",
  categoryId: "c01",
  skills: [],
  attributes: [],
  baseXp: 10,
};
Module._load = function (name, ...rest) {
  if (name === "react-native")
    return {
      Text: container,
      View: container,
      TextInput: Input,
      Pressable: ({
        children,
        onPress,
        accessibilityRole,
        accessibilityState,
      }) =>
        React.createElement(
          "button",
          {
            onClick: onPress,
            role: accessibilityRole,
            "aria-selected": accessibilityState?.selected,
          },
          children,
        ),
    };
  if (name.endsWith("/session"))
    return {
      useAccountSnapshot: () => ({ state }),
      useSession: () => ({
        state,
        catalog: {
          quests: [quest],
          categories: [],
          attributes: [],
          skills: [],
        },
        command: async (command) => sent.push(command),
        busy: false,
        pending: false,
      }),
    };
  if (name.endsWith("/session-page")) return { Page: container };
  if (name.endsWith("/shared/ui"))
    return { Button, Input, Label: container, colors: {}, styles: {} };
  if (name === "./quest-history")
    return {
      QuestHistory: ({ children, sections }) =>
        React.createElement(
          "div",
          null,
          children,
          sections.map((section) =>
            React.createElement(
              "section",
              { key: section.key, "data-day": section.key },
              section.title,
              section.data.length
                ? section.data.map((item) =>
                    React.createElement(
                      "p",
                      { key: item.occurrence.id },
                      item.occurrence.quest.title,
                    ),
                  )
                : section.emptyContent,
              section.footerContent,
            ),
          ),
        ),
    };
  if (name === "./quest-editor")
    return {
      QuestEditor: ({ onClose }) =>
        React.createElement(
          "div",
          null,
          "Custom quest editor",
          React.createElement(Button, {
            title: "Return to available",
            onPress: onClose,
          }),
        ),
    };
  if (name === "./planned-preview") return { PlannedPreview: () => null };
  return originalLoad.call(this, name, ...rest);
};
const {
  CalendarPlanner,
} = require("../../src/features/quests/calendar-planner.tsx");
const { QuestBoard } = require("../../src/features/quests/quest-board.tsx");
function reset() {
  state = {
    today: "2026-10-12",
    zone: "America/New_York",
    occurrences: [],
    definitions: [],
    schedule: { series: [] },
    profile: { onboardingComplete: true, interests: [] },
    categories: [],
    skills: [],
  };
  sent = [];
  inputHandlers = new Map();
}
async function mount(Component = CalendarPlanner) {
  const element = document.createElement("div");
  document.body.append(element);
  const root = createRoot(element);
  await act(async () => root.render(React.createElement(Component)));
  return {
    element,
    button: (title) =>
      [...element.querySelectorAll("button")].find(
        (button) => button.textContent === title,
      ),
    async click(title) {
      await act(async () => {
        const button = this.button(title);
        assert.ok(button, title);
        button.click();
      });
    },
    async fill(label, value) {
      await act(async () => inputHandlers.get(label)(value));
    },
    async unmount() {
      await act(async () => root.unmount());
      element.remove();
    },
  };
}

test("calendar day/week navigation keeps selected state and rejects impossible date input", async () => {
  reset();
  const view = await mount();
  try {
    assert.equal(view.button("Day").getAttribute("aria-selected"), "true");
    await view.click("Next day");
    assert.ok(view.element.querySelector('[data-day="2026-10-13"]'));
    await view.click("7 days");
    assert.equal(view.button("7 days").getAttribute("aria-selected"), "true");
    assert.ok(view.element.querySelector('[data-day="2026-10-19"]'));
    await view.click("Previous 7 days");
    assert.ok(view.element.querySelector('[data-day="2026-10-06"]'));
    await view.fill("Calendar date YYYY-MM-DD", "2026-02-30");
    await view.click("Go to date");
    assert.match(
      view.element.querySelector('[role="alert"]').textContent,
      /real date/,
    );
    assert.ok(view.element.querySelector('[data-day="2026-10-06"]'));
    await view.fill("Calendar date YYYY-MM-DD", "2024-02-29");
    await view.click("Go to date");
    assert.ok(view.element.querySelector('[data-day="2024-02-29"]'));
    assert.equal(view.element.querySelector('[role="alert"]'), null);
    await view.click("Today");
    assert.ok(view.element.querySelector('[data-day="2026-10-12"]'));
    assert.deepEqual(sent, []);
  } finally {
    await view.unmount();
  }
});

test("Active board separates planning from opt-in past history and retains Completed access", async () => {
  reset();
  state.occurrences = [
    ["current", state.today, "Active"],
    ["unscheduled", null, "Frozen"],
    ["earlier", "2026-10-11", "Frozen"],
    ["future", "2026-10-13", "Active"],
    ["missed", "2026-10-11", "Missed"],
    ["completed", state.today, "Completed"],
  ].map(([id, dueDate, status]) => ({
    occurrence: {
      id,
      dueDate,
      acceptedAt: "2026-10-01T12:00:00Z",
      plannedTime: null,
      quest: { ...quest, title: id },
    },
    status,
  }));
  const view = await mount(QuestBoard);
  try {
    await view.click("Active");
    assert.equal(view.button("Active").getAttribute("aria-selected"), "true");
    assert.deepEqual(
      [...view.element.querySelectorAll("section")].map((s) => s.dataset.day),
      ["today", "unscheduled", "earlier", "upcoming"],
    );
    assert.equal(view.element.querySelector('[data-day="past"]'), null);
    await view.click("Show past commitments (1)");
    assert.match(
      view.element.querySelector('[data-day="past"]').textContent,
      /missed/,
    );
    await view.click("Hide past commitments (1)");
    assert.equal(view.element.querySelector('[data-day="past"]'), null);
    await view.click("Completed");
    assert.match(
      view.element.querySelector('[data-day="Completed"]').textContent,
      /completed/,
    );
    await view.click("Active");
    await view.click("Choose another quest");
    assert.equal(
      view.button("Available").getAttribute("aria-selected"),
      "true",
    );
    assert.deepEqual(sent, []);
  } finally {
    await view.unmount();
  }
});

test("empty-day choose and create keep the selected due date through the real acceptance control", async () => {
  for (const create of [false, true]) {
    reset();
    const view = await mount();
    try {
      await view.click("7 days");
      await view.click(
        `${create ? "Create" : "Choose"} a quest for 2026-10-15`,
      );
      assert.deepEqual(
        sent,
        [],
        "navigation must not accept or create a quest",
      );
      if (create) {
        assert.match(view.element.textContent, /Custom quest editor/);
        await view.click("Return to available");
      }
      assert.equal(
        view.element.querySelector(
          '[aria-label="Due date in YYYY-MM-DD format"]',
        ).value,
        "2026-10-15",
      );
      await view.click("Start: Practice");
      assert.equal(sent.length, 1);
      assert.equal(sent[0].dueDate, "2026-10-15");
      assert.equal(sent[0].questId, "q");
      await view.click("Back to calendar");
      assert.equal(view.button("7 days").getAttribute("aria-selected"), "true");
      assert.ok(view.element.querySelector('[data-day="2026-10-12"]'));
    } finally {
      await view.unmount();
    }
  }
});

test("calendar history remains viewable without offering invalid due dates", async () => {
  reset();
  const view = await mount();
  try {
    for (const date of ["2026-10-11", "9999-01-01"]) {
      await view.fill("Calendar date YYYY-MM-DD", date);
      await view.click("Go to date");
      assert.ok(view.element.querySelector(`[data-day="${date}"]`));
      assert.equal(view.button(`Choose a quest for ${date}`), undefined);
      assert.equal(view.button(`Create a quest for ${date}`), undefined);
      assert.match(view.element.textContent, /view-only/);
    }
    await view.fill("Calendar date YYYY-MM-DD", "2026-10-10");
    await view.click("Go to date");
    await view.click("7 days");
    assert.equal(view.button("Choose a quest for 2026-10-11"), undefined);
    assert.ok(view.button("Choose a quest for 2026-10-12"));
    assert.ok(view.button("Create a quest for 2026-10-16"));
    assert.deepEqual(sent, []);
  } finally {
    await view.unmount();
  }
});

test("populated week days retain one date-specific planning entry point", async () => {
  reset();
  state.occurrences = ["2026-10-11", "2026-10-12", "2026-10-15"].map(
    (dueDate) => ({
      occurrence: {
        id: dueDate,
        dueDate,
        plannedTime: null,
        acceptedAt: "2026-10-01T12:00:00Z",
        quest: { ...quest, id: "existing" },
      },
      status: "Active",
    }),
  );
  const view = await mount();
  try {
    await view.click("7 days");
    for (const date of ["2026-10-12", "2026-10-15"]) {
      const section = view.element.querySelector(`[data-day="${date}"]`);
      assert.match(section.textContent, /Practice/);
      assert.equal(
        [...section.querySelectorAll("button")].filter(
          (b) => b.textContent === `Choose a quest for ${date}`,
        ).length,
        1,
      );
    }
    await view.click("Choose a quest for 2026-10-15");
    await view.click("Start: Practice");
    assert.equal(sent[0].dueDate, "2026-10-15");
    await view.click("Back to calendar");
    await view.fill("Calendar date YYYY-MM-DD", "2026-10-11");
    await view.click("Go to date");
    assert.match(
      view.element.querySelector('[data-day="2026-10-11"]').textContent,
      /Practice/,
    );
    assert.equal(view.button("Choose a quest for 2026-10-11"), undefined);
    assert.ok(view.button("Create a quest for 2026-10-15"));
  } finally {
    await view.unmount();
  }
});
