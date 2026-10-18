import "./artwork-fixture.mjs";
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
const load = Module._load;
Module._load = function (name, ...rest) {
  return name === "react-native" ? nativeWeb : load.call(this, name, ...rest);
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
  RankProgress,
} = require("../../src/features/progression/rank-progress.tsx");
const { catalog } = require("../../../../contracts/wire-fixtures.json");

async function mount(initial) {
  const element = document.createElement("div");
  document.body.append(element);
  const root = createRoot(element);
  const view = {
    element,
    async render(props) {
      await act(async () =>
        root.render(
          React.createElement(
            ThemeProvider,
            { theme: pocketQuestsTheme },
            React.createElement(RankProgress, props),
          ),
        ),
      );
    },
    async click(name) {
      const button = [...element.querySelectorAll('[role="button"]')].find(
        (b) => b.getAttribute("aria-label") === name,
      );
      assert.ok(button, name);
      await act(async () => button.click());
    },
    async close() {
      await act(async () => root.unmount());
      element.remove();
    },
  };
  await view.render(initial);
  return view;
}
function props() {
  return {
    rank: {
      current: "F",
      requirement: catalog.rules[1],
      qualifyingCategories: 1,
      total: 500,
    },
    categories: [
      { id: "c01", name: "Work", xp: 500, level: 2 },
      { id: "c02", name: "Care", xp: 0, level: 1 },
    ],
    rules: catalog.rules,
  };
}
test("rank requirements remain independent, disclose each category and use API catalog values", async () => {
  const data = props(),
    original = JSON.stringify(data);
  const view = await mount(data);
  try {
    const bars = [...view.element.querySelectorAll('[role="progressbar"]')];
    assert.equal(bars.length, 2);
    assert.equal(bars[0].getAttribute("aria-valuenow"), "1");
    assert.equal(bars[0].getAttribute("aria-valuemax"), "2");
    assert.equal(bars[1].getAttribute("aria-valuenow"), "300");
    assert.equal(bars[1].getAttribute("aria-valuetext"), "500/300 XP");
    assert.match(view.element.textContent, /Global rank F/);
    await view.click("See category requirements");
    assert.match(view.element.textContent, /500 XP · Floor met/);
    assert.match(view.element.textContent, /0 XP · 70 XP to the floor/);
    await view.click("See the rank ladder");
    for (const rule of catalog.rules.slice(1))
      assert.ok(
        view.element.textContent.includes(
          `${rule.count} categories at ${rule.floor} XP each, and ${rule.total} combined category XP`,
        ),
      );
    assert.match(view.element.textContent, /Rank F · Current rank/);
    await view.click("Hide rank ladder");
    assert.doesNotMatch(view.element.textContent, /Rank S/);
    await view.click("Hide category requirements");
    assert.doesNotMatch(view.element.textContent, /Floor met/);
    assert.equal(JSON.stringify(data), original);
  } finally {
    await view.close();
  }
});
test("confirmed promotion and Undo update open requirements without retaining stale balances", async () => {
  const original = props();
  const view = await mount(original);
  try {
    await view.click("See category requirements");
    await view.click("See the rank ladder");
    await view.render({
      ...original,
      rank: {
        current: "E",
        requirement: catalog.rules[2],
        qualifyingCategories: 1,
        total: 570,
      },
    });
    assert.match(view.element.textContent, /Rank E · Current rank/);
    assert.match(view.element.textContent, /0 XP · 250 XP to the floor/);
    await view.render(original);
    assert.match(view.element.textContent, /Rank F · Current rank/);
    assert.doesNotMatch(view.element.textContent, /Rank E · Current rank/);
    assert.match(view.element.textContent, /0 XP · 70 XP to the floor/);
  } finally {
    await view.close();
  }
});
test("highest rank has no invented next target and missing catalog stays recoverable", async () => {
  const data = props();
  const view = await mount({
    ...data,
    rank: { ...data.rank, current: "S", requirement: catalog.rules[6] },
    rules: [],
  });
  try {
    assert.match(
      view.element.textContent,
      /All ten paths contribute to rank S/,
    );
    assert.equal(view.element.querySelector('[role="progressbar"]'), null);
    assert.match(view.element.textContent, /catalog is available/);
    await view.render(data);
    await view.click("See the rank ladder");
    assert.match(view.element.textContent, /Your starting rank/);
  } finally {
    await view.close();
  }
});

test("compact Home rank summary opens and closes requirements without changing confirmed balances", async () => {
  const data = { ...props(), compact: true };
  const view = await mount(data);
  try {
    assert.equal(
      view.element.querySelectorAll('[role="progressbar"]').length,
      0,
    );
    await view.click("View rank progress");
    assert.equal(
      view.element.querySelectorAll('[role="progressbar"]').length,
      2,
    );
    await view.click("See category requirements");
    assert.match(view.element.textContent, /500 XP · Floor met/);
    await view.click("Hide rank progress");
    assert.equal(
      view.element.querySelectorAll('[role="progressbar"]').length,
      0,
    );
    await view.render({ ...data, rank: { ...data.rank, total: 570 } });
    await view.click("View rank progress");
    assert.match(view.element.textContent, /570\/300 XP/);
  } finally {
    await view.close();
  }
});
