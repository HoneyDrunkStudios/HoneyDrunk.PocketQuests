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
      Link: ({ href, children, accessibilityLabel }) =>
        React.createElement(
          "a",
          {
            href: typeof href === "string" ? href : `/quest/${href.params.id}`,
            "aria-label": accessibilityLabel,
          },
          children,
        ),
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
  activityRecap,
} = require("../../src/features/progression/activity-recap.ts");
const {
  ActivityRecap,
} = require("../../src/features/progression/activity-recap-view.tsx");
function fixture(today = "2026-10-28", zone = "America/New_York") {
  return {
    today,
    zone,
    occurrences: [],
    ledger: [],
    categories: [
      { id: "learn", name: "Learning" },
      { id: "explore", name: "Exploration" },
    ],
  };
}
function complete(state, id, at, category = "learn", xp = 20) {
  const quest = { title: `Adventure ${id}`, categoryId: category, baseXp: 999 };
  const item = {
    status: "Completed",
    occurrence: { id, quest },
    completion: {
      id: `c-${id}`,
      occurrenceId: id,
      recordedAt: at,
      snapshot: { ...quest },
    },
  };
  state.occurrences.push(item);
  state.ledger.push({
    eventId: `c-${id}`,
    occurrenceId: id,
    at,
    track: "Overall",
    trackId: "overall",
    amount: xp,
  });
  return item;
}
test("recap uses surviving server XP once, immutable completion titles and category totals without mutations", () => {
  const state = fixture();
  const first = complete(state, "one", "2026-10-28T12:00:00Z", "learn", 20);
  first.occurrence.quest.title = "Edited later";
  complete(state, "two", "2026-10-27T12:00:00Z", "explore", 30);
  complete(state, "three", "2026-10-27T15:00:00Z", "learn", 10);
  state.ledger.push({
    ...state.ledger[0],
    track: "Category",
    trackId: "learn",
    amount: 500,
  });
  state.ledger.push({ ...state.ledger[0] });
  const before = JSON.stringify(state);
  const recap = activityRecap(state, 7);
  assert.equal(recap.completed, 3);
  assert.equal(recap.xp, 60);
  assert.equal(recap.activeDays, 2);
  assert.deepEqual(
    recap.paths.map((p) => [p.name, p.count]),
    [
      ["Learning", 2],
      ["Exploration", 1],
    ],
  );
  assert.equal(recap.entries[0].title, "Adventure one");
  assert.equal(JSON.stringify(state), before);
});
test("range boundaries use account-zone dates and ignore future and older completions", () => {
  const state = fixture();
  complete(state, "before", "2026-10-22T03:59:59Z");
  complete(state, "start", "2026-10-22T04:00:00Z");
  complete(state, "end", "2026-10-29T03:59:59Z");
  complete(state, "future", "2026-10-29T04:00:00Z");
  const recap = activityRecap(state, 7);
  assert.deepEqual(
    recap.entries.map((e) => e.occurrenceId),
    ["end", "start"],
  );
  assert.equal(recap.days.length, 7);
  assert.equal(recap.start, "2026-10-22");
});
test("day buckets handle DST, month boundaries and leap dates without device-zone arithmetic", () => {
  const state = fixture("2026-11-02");
  complete(state, "before-fallback", "2026-11-01T05:30:00Z");
  complete(state, "after-fallback", "2026-11-01T06:30:00Z");
  assert.equal(
    activityRecap(state, 7).days.find((d) => d.day === "2026-11-01").count,
    2,
  );
  assert.equal(activityRecap(state, 7).activeDays, 1);
  const leap = activityRecap(fixture("2024-03-01", "Pacific/Kiritimati"), 30);
  assert.equal(leap.start, "2024-02-01");
  assert.ok(leap.days.some((d) => d.day === "2024-02-29"));
});
test("pending, timing-unverified and undone completions cannot inflate the recap", () => {
  const state = fixture();
  complete(state, "pending", "2026-10-28T12:00:00Z").pendingCompletion = true;
  complete(
    state,
    "unverified",
    "2026-10-28T12:00:00Z",
  ).pendingTimingVerification = true;
  const undone = complete(state, "undone", "2026-10-28T12:00:00Z");
  undone.status = "Active";
  undone.completion = null;
  complete(state, "no-receipt", "2026-10-28T12:00:00Z");
  state.ledger.pop();
  assert.equal(activityRecap(state, 7).completed, 0);
  assert.equal(activityRecap(state, 7).xp, 0);
});
test("malformed clock data has no invented activity; legacy snapshots still resolve", () => {
  assert.equal(activityRecap(fixture("2026-02-30"), 7), null);
  assert.equal(activityRecap(fixture("invalid"), 7), null);
  assert.equal(activityRecap(fixture("2026-10-28", "Invalid/Zone"), 7), null);
  const state = fixture();
  complete(state, "bad", "not-an-instant");
  complete(state, "legacy", "2026-10-28T12:00:00Z").completion.snapshot = null;
  assert.equal(activityRecap(state, 7).completed, 1);
  assert.equal(activityRecap(state, 7).entries[0].title, "Adventure legacy");
});
async function mount(state, options = {}) {
  const element = document.createElement("div");
  document.body.append(element);
  const root = createRoot(element);
  let current = state;
  const view = {
    element,
    async render(next = current) {
      current = next;
      await act(async () =>
        root.render(
          React.createElement(
            ThemeProvider,
            { theme: pocketQuestsTheme },
            React.createElement(ActivityRecap, {
              state: current,
              pending: false,
              offline: false,
              ...options,
            }),
          ),
        ),
      );
    },
    async click(title) {
      const button = [...element.querySelectorAll('[role="button"]')].find(
        (b) => b.textContent === title,
      );
      assert.ok(button, title);
      await act(async () => button.click());
    },
    links() {
      return [
        ...element.querySelectorAll('a[aria-label^="Open completed quest:"]'),
      ];
    },
    async close() {
      await act(async () => root.unmount());
      element.remove();
    },
  };
  await view.render();
  return view;
}
test("period, day and category controls filter real quest links and clear together", async () => {
  const state = fixture();
  complete(state, "today", "2026-10-28T12:00:00Z");
  complete(state, "walk", "2026-10-27T12:00:00Z", "explore");
  complete(state, "earlier", "2026-10-10T12:00:00Z");
  const before = JSON.stringify(state);
  const view = await mount(state);
  try {
    assert.equal(view.links().length, 0);
    await view.click("Explore activity");
    assert.equal(view.links().length, 2);
    await view.click("Exploration · 1");
    assert.equal(view.links()[0].getAttribute("href"), "/quest/walk");
    await view.click("Oct 281 quest");
    assert.equal(view.links().length, 0);
    assert.match(view.element.textContent, /No completed quests match/);
    await view.click("Show all completed adventures");
    assert.equal(view.links().length, 2);
    await view.click("Last 30 days");
    assert.equal(view.links().length, 3);
    assert.equal(
      [...view.element.querySelectorAll('[role="button"]')]
        .find((b) => b.textContent === "Last 30 days")
        .getAttribute("aria-pressed"),
      "true",
    );
    assert.equal(JSON.stringify(state), before);
  } finally {
    await view.close();
  }
});
test("recap pages results, updates after Undo and never retains another account's entries", async () => {
  const state = fixture();
  for (let i = 0; i < 8; i++)
    complete(state, String(i), "2026-10-28T12:00:00Z");
  const view = await mount(state);
  try {
    await view.click("Explore activity");
    assert.equal(view.links().length, 5);
    await view.click("Show more completed adventures");
    assert.equal(view.links().length, 8);
    const next = structuredClone(state);
    next.occurrences[0].completion = null;
    next.occurrences[0].status = "Active";
    await view.render(next);
    assert.equal(view.links().length, 7);
    await view.render(fixture());
    assert.equal(view.links().length, 0);
    assert.match(
      view.element.textContent,
      /No completed quests in this period/,
    );
  } finally {
    await view.close();
  }
});
test("offline and pending states explain limits; invalid dates offer recovery", async () => {
  const view = await mount(fixture(), { offline: true, pending: true });
  try {
    assert.match(
      view.element.textContent,
      /Offline: showing your saved activity/,
    );
    assert.match(view.element.textContent, /Pending completions are excluded/);
    await view.render(fixture("invalid"));
    assert.match(view.element.textContent, /Activity dates are unavailable/);
  } finally {
    await view.close();
  }
});
