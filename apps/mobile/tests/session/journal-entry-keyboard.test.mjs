import test from "node:test";
import assert from "node:assert/strict";
import Module, { createRequire } from "node:module";
const require = createRequire(import.meta.url);
require("./provider-harness.cjs");
const load = Module._load;
let platform = "ios";
Module._load = function (name, ...rest) {
  if (name === "react/jsx-runtime") {
    const element = (type, props) => ({ type, props });
    return { jsx: element, jsxs: element };
  }
  if (name === "react-native")
    return {
      Image: "Image",
      KeyboardAvoidingView: "KeyboardAvoidingView",
      ScrollView: "ScrollView",
      Text: "Text",
      View: "View",
      Platform: {
        get OS() {
          return platform;
        },
      },
    };
  if (name === "react-native-safe-area-context")
    return { useSafeAreaInsets: () => ({ top: 24, bottom: 34 }) };
  if (name === "@expo/vector-icons/Ionicons") return "Icon";
  if (name.endsWith("signin-background.jpg")) return "illustration";
  if (name === "./ui") return { colors: {}, styles: {} };
  return load.call(this, name, ...rest);
};
const { JournalEntry } = require("../../src/shared/journal-entry.tsx");
function descendants(element, type) {
  if (!element || typeof element !== "object") return [];
  const children = [element.props?.children].flat();
  return [
    ...(element.type === type ? [element] : []),
    ...children.flatMap((child) => descendants(child, type)),
  ];
}

test("iOS sign-in shell reserves keyboard space around its single bounded scroll owner", () => {
  platform = "ios";
  const form = { type: "PairingForm", props: {} };
  const screen = JournalEntry({ children: form });
  const [avoidance] = descendants(screen, "KeyboardAvoidingView");
  assert.equal(avoidance.props.enabled, true);
  assert.equal(avoidance.props.behavior, "padding");
  assert.equal(avoidance.props.style.flex, 1);
  const [scroll] = descendants(avoidance, "ScrollView");
  assert.equal(descendants(screen, "ScrollView").length, 1);
  assert.equal(scroll.props.style.flex, 1);
  assert.equal(scroll.props.contentContainerStyle.flexGrow, 1);
  assert.equal(descendants(scroll, "PairingForm")[0], form);
  assert.equal(scroll.props.contentContainerStyle.paddingBottom, 66);
});

test("sign-in scroll owner passes button taps through while the keyboard is open", () => {
  platform = "ios";
  const [scroll] = descendants(JournalEntry({ children: null }), "ScrollView");
  assert.equal(scroll.props.keyboardShouldPersistTaps, "handled");
  assert.equal(scroll.props.keyboardDismissMode, "interactive");
});

test("Android and web keep their existing viewport sizing instead of adding iOS keyboard padding", () => {
  for (const os of ["android", "web"]) {
    platform = os;
    const screen = JournalEntry({ children: null });
    const [avoidance] = descendants(screen, "KeyboardAvoidingView");
    assert.equal(avoidance.props.enabled, false);
    assert.equal(avoidance.props.behavior, undefined);
    assert.equal(descendants(screen, "ScrollView").length, 1);
  }
});
