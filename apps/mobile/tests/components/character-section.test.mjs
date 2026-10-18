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
  CharacterSection,
} = require("../../src/features/progression/character-section.tsx");
const {
  ProgressRow,
  CategoryProgress,
} = require("../../src/features/progression/character-tracks.tsx");

async function mount(initial) {
  const element = document.createElement("div");
  document.body.append(element);
  const root = createRoot(element);
  const view = {
    element,
    async render(groups) {
      await act(async () =>
        root.render(
          React.createElement(
            ThemeProvider,
            { theme: pocketQuestsTheme },
            groups.map(({ title, track, items, streak }) =>
              React.createElement(
                CharacterSection,
                {
                  key: title,
                  title,
                  description: `${title} description`,
                  count: items.length,
                },
                items.map((item) =>
                  React.createElement(
                    track === "category" ? CategoryProgress : ProgressRow,
                    { key: item.id, item, track, streak },
                  ),
                ),
              ),
            ),
          ),
        ),
      );
    },
    button(title) {
      return element.querySelector(`[aria-label^="${title}, "]`);
    },
    async click(title) {
      await act(async () => view.button(title).click());
    },
    async close() {
      await act(async () => root.unmount());
      element.remove();
    },
  };
  await view.render(initial);
  return view;
}
const group = (title, track, xp = 0, level = 1) => ({
  title,
  track,
  items: [{ id: track, name: `${title} example`, xp, level }],
});

test("character sections start collapsed, open independently and remove hidden details from accessibility", async () => {
  const data = [
    group("Categories", "category"),
    group("Attributes", "attribute"),
    group("Skills", "skill"),
  ];
  const view = await mount(data);
  try {
    for (const { title } of data) {
      const heading = [
        ...view.element.querySelectorAll('[role="heading"]'),
      ].find((item) => item.textContent === title);
      assert.ok(heading, `${title} remains reachable by heading navigation`);
      assert.equal(view.button(title).contains(heading), false);
      assert.equal(view.button(title).getAttribute("role"), "button");
      assert.equal(view.button(title).getAttribute("tabindex"), "0");
      assert.equal(view.button(title).getAttribute("aria-expanded"), "false");
    }
    assert.equal(
      view.element.querySelectorAll('[role="progressbar"]').length,
      0,
    );
    await view.click("Categories");
    await view.click("Skills");
    assert.equal(
      view.button("Categories").getAttribute("aria-expanded"),
      "true",
    );
    assert.equal(
      view.button("Attributes").getAttribute("aria-expanded"),
      "false",
    );
    assert.equal(
      view.element.querySelectorAll('[role="progressbar"]').length,
      2,
    );
    await view.click("Categories");
    assert.doesNotMatch(view.element.textContent, /Categories example/);
    assert.match(view.element.textContent, /Skills example/);
    await act(async () => view.button("Skills").focus());
    assert.equal(document.activeElement, view.button("Skills"));
    await act(async () => view.button("Skills").blur());
  } finally {
    await view.close();
  }
});

test("open and reopened character groups use current completion, streak and Undo balances", async () => {
  const before = {
    ...group("Categories", "category", 9),
    streak: { days: 0, qualifiedToday: false },
  };
  const after = {
    ...group("Categories", "category", 10, 2),
    streak: { days: 1, qualifiedToday: true },
  };
  const view = await mount([before]);
  try {
    await view.click("Categories");
    assert.match(
      view.element.textContent,
      /Complete a quest to start a streak/,
    );
    await view.render([after]);
    assert.equal(
      view.button("Categories").getAttribute("aria-expanded"),
      "true",
    );
    assert.match(view.element.textContent, /Level 2 · 10 XP/);
    assert.match(view.element.textContent, /1 day in this streak. Done today/);
    await view.render([before]);
    assert.match(view.element.textContent, /Level 1 · 9 XP/);
    await view.click("Categories");
    await view.render([after]);
    await view.click("Categories");
    assert.match(view.element.textContent, /Level 2 · 10 XP/);
  } finally {
    await view.close();
  }
});

test("empty skills remain usable and count changes reflect newly available custom skills", async () => {
  const view = await mount([{ title: "Skills", track: "skill", items: [] }]);
  try {
    assert.equal(view.button("Skills").getAttribute("aria-label"), "Skills, 0");
    await view.click("Skills");
    assert.match(view.element.textContent, /No skills to show yet/);
    await view.render([group("Skills", "skill")]);
    assert.equal(view.button("Skills").getAttribute("aria-label"), "Skills, 1");
    assert.doesNotMatch(view.element.textContent, /No skills/);
    assert.match(view.element.textContent, /Skills example/);
  } finally {
    await view.close();
  }
});
