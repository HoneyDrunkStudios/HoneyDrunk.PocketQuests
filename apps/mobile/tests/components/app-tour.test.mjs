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
let signedIn, complete, params, pathname, root, routes;
const router = {
  setParams(value) {
    params = { ...params, ...value };
    render();
  },
  replace(value) {
    pathname = typeof value === "string" ? value : value.pathname;
    if (pathname === "/(tabs)") pathname = "/";
    routes.push(pathname);
    render();
  },
  navigate(value) {
    pathname = value;
    routes.push(value);
    render();
  },
};
const load = Module._load;
Module._load = function (name, ...rest) {
  if (name === "react-native") return nativeWeb;
  if (name === "react-native-safe-area-context")
    return { useSafeAreaInsets: () => ({ bottom: 0 }) };
  if (name === "expo-router")
    return {
      useGlobalSearchParams: () => params,
      usePathname: () => pathname,
      useRouter: () => router,
    };
  if (name.endsWith("/session"))
    return {
      useAccountSnapshot: () => ({
        state: { profile: { onboardingComplete: complete } },
      }),
      useSessionStatus: () => ({ signedIn }),
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
const { AppTour } = require("../../src/features/profile/app-tour.tsx");
function render() {
  root.render(
    React.createElement(
      ThemeProvider,
      { theme: pocketQuestsTheme },
      React.createElement(AppTour),
    ),
  );
}
async function mount() {
  signedIn = true;
  complete = false;
  params = { tour: "start" };
  pathname = "/onboarding";
  routes = [];
  const element = document.createElement("div");
  document.body.append(element);
  root = createRoot(element);
  await act(async () => render());
  return {
    element,
    async render() {
      await act(async () => render());
    },
    async click(name) {
      const button = [...element.querySelectorAll('[role="button"]')].find(
        (b) => b.textContent === name,
      );
      assert.ok(button, name);
      await act(async () => button.click());
    },
    async close() {
      await act(async () => root.unmount());
      element.remove();
    },
  };
}

test("the tour waits for confirmed onboarding, visits each real screen and never sends quest commands", async () => {
  const view = await mount();
  try {
    assert.equal(view.element.textContent, "");
    complete = true;
    await view.render();
    assert.match(view.element.textContent, /Welcome to your journal/);
    for (const title of [
      "Find something that fits",
      "Give it a little space",
      "Make yourself at home",
      "Small steps add up",
    ]) {
      await view.click("Next");
      assert.ok(view.element.textContent.includes(title));
    }
    await view.click("Open settings");
    assert.match(view.element.textContent, /Keep it your own/);
    await view.click("Choose a quest");
    assert.equal(view.element.textContent, "");
    assert.deepEqual(routes, [
      "/",
      "/board",
      "/calendar",
      "/room",
      "/profile",
      "/settings",
      "/(tabs)/board",
    ]);
  } finally {
    await view.close();
  }
});

test("manual tab navigation advances the marker; skipping and signing out dismiss the tour", async () => {
  const view = await mount();
  try {
    complete = true;
    await view.render();
    pathname = "/board";
    await view.render();
    assert.match(view.element.textContent, /Find something that fits/);
    await view.click("Skip tour");
    assert.equal(view.element.textContent, "");
    params = { tour: "start" };
    await view.render();
    assert.match(view.element.textContent, /Welcome to your journal/);
    signedIn = false;
    await view.render();
    assert.equal(view.element.textContent, "");
  } finally {
    await view.close();
  }
});
