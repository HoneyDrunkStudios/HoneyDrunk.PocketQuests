import test from "node:test";
import assert from "node:assert/strict";
import Module, { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { JSDOM } = require("jsdom");
const dom = new JSDOM("<!doctype html><html><body></body></html>");
global.window = dom.window;
global.document = dom.window.document;
global.IS_REACT_ACT_ENVIRONMENT = true;
global.__DEV__ = true;
process.env.EXPO_PUBLIC_DEVELOPMENT_SIGN_IN = "1";
process.env.PQ_REAL_REACT = "1";
const React = require("react");
const { act } = React;
const { createRoot } = require("react-dom/client");
require("./provider-harness.cjs");
const originalLoad = Module._load;
const container = ({ children }) => React.createElement("div", null, children);
let connectCalls = [];
let providerCalls = 0;
let signedIn = false;
let redirects = 0;
let inputChange;
let inputProps;
Module._load = function (name, ...rest) {
  if (name === "expo-auth-session") return {};
  if (name === "react-native")
    return {
      Text: container,
      View: container,
      TextInput: ({
        accessibilityLabel,
        onChangeText,
        secureTextEntry,
        keyboardType,
        maxLength,
        value,
      }) => {
        inputChange = onChangeText;
        inputProps = { keyboardType, maxLength, value };
        return React.createElement("input", {
          "aria-label": accessibilityLabel,
          type: secureTextEntry ? "password" : "text",
        });
      },
    };
  if (name === "expo-router")
    return {
      Redirect: () => {
        redirects++;
        return null;
      },
    };
  if (name === "expo-web-browser") return { maybeCompleteAuthSession() {} };
  if (name === "../session/session" || name === "./session")
    return {
      useSession: () => ({
        busy: false,
        signedIn,
        connect: async (value) => {
          connectCalls.push(value);
        },
      }),
    };
  if (name === "../session/use-provider-setup")
    return {
      useProviderSetup() {
        providerCalls++;
        throw new Error(
          "OAuth must not initialize in development sign-in mode",
        );
      },
    };
  if (
    [
      "../session/session-recovery",
      "../features/profile/account-settings",
    ].includes(name)
  )
    return { SessionRecovery: () => null, RecoveryPanel: () => null };
  if (name === "../shared/journal-entry") return { JournalEntry: container };
  if (name === "@honeydrunk/ui-native") return { Loading: () => null };
  if (name === "../shared/ui")
    return {
      Label: container,
      styles: {},
      Button: ({ title, onPress, disabled }) =>
        React.createElement("button", { onClick: onPress, disabled }, title),
    };
  return originalLoad.call(this, name, ...rest);
};
const Welcome = require("../../src/app/index.tsx").default;

test("development welcome bypasses provider setup and pairs through the normal session connect action", async () => {
  const element = document.createElement("div");
  document.body.append(element);
  const root = createRoot(element);
  global.fetch = async (_url, options) => {
    assert.equal(JSON.parse(options.body).pairingCode, "001234");
    return {
      ok: true,
      json: async () => ({ token: "development-credential" }),
    };
  };
  try {
    await act(async () => root.render(React.createElement(Welcome)));
    const button = () => element.querySelector("button");
    assert.equal(providerCalls, 0);
    assert.equal(button().disabled, true);
    assert.equal(element.querySelector("input").type, "password");
    assert.equal(inputProps.keyboardType, "number-pad");
    assert.equal(inputProps.maxLength, 6);
    await act(async () => inputChange("00123"));
    assert.equal(button().disabled, true);
    await act(async () => inputChange("00-12 34 extra 99"));
    assert.equal(inputProps.value, "001234");
    assert.equal(button().disabled, false);
    await act(async () => button().click());
    assert.deepEqual(connectCalls, [{ token: "development-credential" }]);
    assert.equal(providerCalls, 0);
    assert.doesNotMatch(element.textContent, /Preparing secure sign-in/);
  } finally {
    await act(async () => root.unmount());
    element.remove();
  }
});

test("rejected pairing displays a retryable error without connecting a session", async () => {
  connectCalls = [];
  const element = document.createElement("div");
  const root = createRoot(element);
  global.fetch = async () => ({ ok: false, status: 401 });
  try {
    await act(async () => root.render(React.createElement(Welcome)));
    await act(async () => inputChange("654321"));
    await act(async () => element.querySelector("button").click());
    assert.deepEqual(connectCalls, []);
    assert.match(element.textContent, /pairing code was not accepted/);
    assert.equal(element.querySelector("button").disabled, false);
  } finally {
    await act(async () => root.unmount());
  }
});

test("a successful sign-in leaves navigation to the root protected stack", async () => {
  const element = document.createElement("div");
  const root = createRoot(element);
  signedIn = true;
  try {
    await act(async () => root.render(React.createElement(Welcome)));
    assert.equal(element.childElementCount, 0);
    assert.equal(redirects, 0);
  } finally {
    await act(async () => root.unmount());
    signedIn = false;
  }
});
