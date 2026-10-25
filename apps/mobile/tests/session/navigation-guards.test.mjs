import test from "node:test";
import assert from "node:assert/strict";
import Module, { createRequire } from "node:module";
const require = createRequire(import.meta.url);
require("./provider-harness.cjs");
const load = Module._load;
let signedIn = false;
let busy = false;
let restoring = false;
let state = null;
const Stack = Object.assign(() => null, {
  Protected: "Protected",
  Screen: "Screen",
});
Module._load = function (name, ...rest) {
  if (name === "react/jsx-runtime") {
    const element = (type, props) => ({ type, props });
    return { jsx: element, jsxs: element };
  }
  if (name === "expo-router") return { Stack };
  if (name === "expo-font") return { useFonts: () => [true, null] };
  if (name === "@expo/vector-icons/Ionicons") return { font: {} };
  if (name.endsWith("LibreBaskerville.ttf")) return "font";
  if (name === "expo-status-bar") return { StatusBar: "StatusBar" };
  if (name === "../session/session")
    return {
      SessionProvider: "SessionProvider",
      useSessionStatus: () => ({ signedIn, busy, restoring }),
      useAccountSnapshot: () => ({ state }),
    };
  if (name === "@honeydrunk/ui-native") return { ThemeProvider: "Theme" };
  if (name === "../shared/theme") return { pocketQuestsTheme: {} };
  if (name === "../shared/ui") return { colors: {} };
  if (name === "../shared/error-boundary") return { AppErrorBoundary: null };
  if (name === "../features/progression/completion-overlay")
    return { CompletionOverlay: "CompletionOverlay" };
  return load.call(this, name, ...rest);
};
const Layout = require("../../src/app/_layout.tsx").default;

function routePolicy(
  authenticated,
  onboardingComplete,
  booting = false,
  working = false,
) {
  signedIn = authenticated;
  busy = working;
  restoring = booting;
  state =
    onboardingComplete === null ? null : { profile: { onboardingComplete } };
  const provider = Layout().props.children.find(
    (child) => child.type === "SessionProvider",
  );
  const navigator = provider.props.children[0];
  const stack = navigator.type(navigator.props);
  if (stack === null) return null;
  return new Map(
    stack.props.children.flatMap((group) =>
      [group.props.children]
        .flat()
        .map((screen) => [
          screen.props.name,
          { allowed: group.props.guard, redirect: group.props.redirectTo },
        ]),
    ),
  );
}

function destination(policy, route) {
  const visited = new Set();
  while (!policy.get(route).allowed) {
    assert.ok(!visited.has(route), `Protected-route cycle at ${route}`);
    visited.add(route);
    const href = policy.get(route).redirect;
    assert.ok(href, `Blocked ${route} must have an explicit destination`);
    route = href === "/" ? "index" : href.slice(1);
    assert.ok(policy.has(route), `Unknown destination ${href}`);
  }
  return route;
}

test("sign-in removes welcome and callback from the stack so account updates cannot redirect an app screen", () => {
  const policy = routePolicy(true, false);
  for (const route of ["index", "callback"])
    assert.equal(destination(policy, route), "(tabs)");
  assert.equal(policy.get("quest/[id]").allowed, true);
  assert.equal(policy.get("(tabs)").allowed, true);
});
test("signed-out deep links return to welcome and OAuth callbacks remain available", () => {
  const policy = routePolicy(false, null);
  for (const route of ["(tabs)", "quest/[id]", "quest/available/[id]"])
    assert.equal(destination(policy, route), "index");
  assert.equal(policy.get("callback").allowed, true);
});

test("cold quest deep links are not rejected while a saved session restores", () => {
  assert.equal(routePolicy(false, null, true), null);
  assert.equal(routePolicy(true, null, true), null);
  const restored = routePolicy(true, true, false);
  for (const route of ["quest/[id]", "quest/available/[id]"])
    assert.equal(restored.get(route).allowed, true);
  const signedOut = routePolicy(false, null, false);
  assert.equal(destination(signedOut, "quest/[id]"), "index");
});

test("ordinary account work keeps signed-out recovery and signed-in routes mounted after restoration", () => {
  const recovering = routePolicy(false, null, false, true);
  assert.equal(recovering.get("index").allowed, true);
  assert.equal(recovering.get("callback").allowed, true);
  const policy = routePolicy(true, true, false, true);
  assert.equal(policy.get("quest/[id]").allowed, true);
});
