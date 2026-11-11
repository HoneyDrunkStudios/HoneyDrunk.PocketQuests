import test, { mock } from "node:test";
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
const React = require("react"),
  { act } = React;
const { createRoot } = require("react-dom/client");
const nativeWeb = require("react-native-web"),
  ts = require("typescript");
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
let system = "light",
  probe,
  theme,
  mounts = 0,
  loads,
  writes,
  save,
  root;
const load = Module._load;
Module._load = function (name, ...rest) {
  if (name === "react-native")
    return { ...nativeWeb, useColorScheme: () => system };
  if (name === "./storage")
    return {
      displayPreferences: {
        load: () => loads.promise,
        save: (mode) => save(mode),
      },
    };
  return load.call(this, name, ...rest);
};
const {
  DisplayModeProvider,
  useDisplayMode,
} = require("../../src/session/display-mode.tsx");
const { useTheme } = require("@honeydrunk/ui-native");
const {
  pocketQuestsTheme,
  pocketQuestsDarkTheme,
} = require("../../src/shared/theme.ts");
const deferred = () => {
  let resolve, reject;
  const promise = new Promise((yes, no) => {
    resolve = yes;
    reject = no;
  });
  return { promise, resolve, reject };
};
function Probe() {
  probe = useDisplayMode();
  theme = useTheme();
  React.useEffect(() => {
    mounts++;
  }, []);
  return null;
}
function render() {
  root.render(
    React.createElement(DisplayModeProvider, null, React.createElement(Probe)),
  );
}
async function mount() {
  system = "light";
  mounts = 0;
  loads = deferred();
  writes = [];
  save = async (mode) => {
    writes.push(mode);
  };
  const element = document.createElement("div");
  document.body.append(element);
  root = createRoot(element);
  await act(async () => render());
  return {
    async render() {
      await act(async () => render());
    },
    async select(mode) {
      await act(async () => probe.selectMode(mode));
    },
    async close() {
      await act(async () => root.unmount());
      element.remove();
    },
  };
}
test("saved display choice restores; System follows the OS and overrides change live without remounting", async () => {
  const v = await mount();
  try {
    await act(async () => loads.resolve("dark"));
    assert.equal(probe.ready, true);
    assert.equal(theme, pocketQuestsDarkTheme);
    await v.select("system");
    assert.equal(theme, pocketQuestsTheme);
    system = "dark";
    await v.render();
    assert.equal(theme, pocketQuestsDarkTheme);
    await v.select("light");
    assert.equal(theme, pocketQuestsTheme);
    assert.equal(mounts, 1);
    assert.deepEqual(writes, ["system", "light"]);
  } finally {
    await v.close();
  }
});
test("a late preference load cannot overwrite a choice made on screen", async () => {
  const v = await mount();
  try {
    await v.select("dark");
    await act(async () => loads.resolve("light"));
    assert.equal(probe.mode, "dark");
    assert.equal(theme, pocketQuestsDarkTheme);
  } finally {
    await v.close();
  }
});
test("preference writes serialize; stale errors do not replace the newest successful choice", async () => {
  const v = await mount();
  try {
    const first = deferred();
    save = (mode) => {
      writes.push(mode);
      return writes.length === 1 ? first.promise : Promise.resolve();
    };
    await v.select("dark");
    await v.select("light");
    assert.deepEqual(writes, ["dark"]);
    await act(async () => first.reject(Error("storage")));
    assert.deepEqual(writes, ["dark", "light"]);
    assert.equal(probe.error, "");
    assert.equal(probe.mode, "light");
    save = async () => {
      throw Error("storage");
    };
    await v.select("dark");
    assert.match(probe.error, /could not be saved/);
    assert.equal(theme, pocketQuestsDarkTheme);
    await act(async () => loads.resolve("system"));
  } finally {
    await v.close();
  }
});
function luminance(hex) {
  const rgb = hex
    .slice(1)
    .match(/../g)
    .map((x) => parseInt(x, 16) / 255)
    .map((x) => (x <= 0.04045 ? x / 12.92 : ((x + 0.055) / 1.055) ** 2.4));
  return rgb[0] * 0.2126 + rgb[1] * 0.7152 + rgb[2] * 0.0722;
}
function contrast(a, b) {
  const x = luminance(a),
    y = luminance(b);
  return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05);
}
test("both themes maintain readable text and distinct interactive controls", () => {
  for (const theme of [pocketQuestsTheme, pocketQuestsDarkTheme]) {
    const c = theme.colors;
    for (const fg of [c.text, c.muted, c.accent, c.danger])
      for (const bg of [c.background, c.surface, c.input])
        assert.ok(contrast(fg, bg) >= 4.5, `${fg} / ${bg}`);
    assert.ok(contrast(c.primary, c.onPrimary) >= 4.5);
    for (const bg of [c.background, c.surface, c.input])
      assert.ok(contrast(c.border, bg) >= 3, `${c.border} / ${bg}`);
  }
});
