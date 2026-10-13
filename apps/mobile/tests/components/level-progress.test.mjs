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
  LevelProgress,
} = require("../../src/features/progression/level-progress-view.tsx");
const {
  levelProgress,
} = require("../../src/features/progression/level-progress.ts");
test("approved independent XP tracks reset only at their own next level", () => {
  for (const [track, threshold] of [
    ["overall", 100],
    ["category", 10],
    ["attribute", 25],
    ["skill", 5],
  ]) {
    assert.deepEqual(levelProgress(track, threshold - 1, 1), {
      earned: threshold - 1,
      required: threshold,
      remaining: 1,
      nextLevel: 2,
    });
    assert.deepEqual(levelProgress(track, threshold, 2), {
      earned: 0,
      required: threshold * 3,
      remaining: threshold * 3,
      nextLevel: 3,
    });
  }
  assert.deepEqual(levelProgress("overall", 280, 2), {
    earned: 180,
    required: 300,
    remaining: 120,
    nextLevel: 3,
  });
  assert.deepEqual(levelProgress("skill", 12285, 50), {
    earned: 280,
    required: 495,
    remaining: 215,
    nextLevel: 51,
  });
});
test("unsafe and inconsistent balances cannot invent a level or a full bar", () => {
  for (const [xp, level] of [
    [-1, 1],
    [0.5, 1],
    [NaN, 1],
    [Infinity, 1],
    [0, 0],
    [0, 1.5],
    [0, NaN],
    [0, 2],
    [100, 1],
    [Number.MAX_SAFE_INTEGER + 1, 1],
    [0, Number.MAX_SAFE_INTEGER],
  ])
    assert.equal(levelProgress("overall", xp, level), null);
});
test("accessible progress reflects confirmed completion, level-up and Undo without stale state", async () => {
  const element = document.createElement("div");
  document.body.append(element);
  const root = createRoot(element);
  async function render(xp, level) {
    await act(async () =>
      root.render(
        React.createElement(
          ThemeProvider,
          { theme: pocketQuestsTheme },
          React.createElement(LevelProgress, {
            name: "Overall",
            track: "overall",
            xp,
            level,
          }),
        ),
      ),
    );
  }
  try {
    for (const [xp, level, earned, required, text] of [
      [90, 1, 90, 100, "90/100 XP toward level 2"],
      [100, 2, 0, 300, "0/300 XP toward level 3"],
      [90, 1, 90, 100, "90/100 XP toward level 2"],
    ]) {
      await render(xp, level);
      const bar = element.querySelector('[role="progressbar"]');
      assert.equal(bar.getAttribute("aria-label"), "Overall level progress");
      assert.equal(bar.getAttribute("aria-valuenow"), String(earned));
      assert.equal(bar.getAttribute("aria-valuemax"), String(required));
      assert.equal(bar.getAttribute("aria-valuetext"), text);
      assert.ok(element.textContent.includes(text));
    }
    await render(100, 1);
    assert.equal(element.querySelector('[role="progressbar"]'), null);
    assert.match(element.textContent, /Level progress unavailable/);
  } finally {
    await act(async () => root.unmount());
    element.remove();
  }
});
