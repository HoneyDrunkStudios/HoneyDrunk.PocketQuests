import test from "node:test";
import assert from "node:assert/strict";
import Module, { createRequire } from "node:module";
const require = createRequire(import.meta.url);
require("./provider-harness.cjs");
const load = Module._load;
let signedIn = false;
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
      useSessionStatus: () => ({ signedIn }),
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

function routePolicy(authenticated, onboardingComplete) {
  signedIn = authenticated;
  state =
    onboardingComplete === null ? null : { profile: { onboardingComplete } };
  const provider = Layout().props.children.find(
    (child) => child.type === "SessionProvider",
  );
  const navigator = provider.props.children[0];
  const stack = navigator.type(navigator.props);
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
