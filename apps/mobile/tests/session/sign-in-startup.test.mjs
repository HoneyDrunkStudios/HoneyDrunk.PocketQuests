import test from "node:test";
import assert from "node:assert/strict";
import Module, { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { JSDOM } = require("jsdom");
const dom = new JSDOM("<!doctype html><html><body></body></html>");
global.window = dom.window;
global.document = dom.window.document;
global.IS_REACT_ACT_ENVIRONMENT = true;
const React = require("react"),
  { act } = React;
const { createRoot } = require("react-dom/client");
process.env.PQ_REAL_REACT = "1";
require("./provider-harness.cjs");
const originalLoad = Module._load;
const config = {
  authority: "https://issuer.example.test",
  clientId: "mobile",
  scope: "quests",
};
const discovery = {
  authorizationEndpoint: "https://issuer.example.test/authorize",
  tokenEndpoint: "https://issuer.example.test/token",
};
let discover, prompt, promptCalls, response, exchange;
const request = { codeVerifier: "test-verifier" };
const connect = async () => {};
const container = ({ children }) => React.createElement("div", null, children);
Module._load = function (name, ...rest) {
  if (name === "expo-auth-session")
    return {
      fetchDiscoveryAsync: (...args) => discover(...args),
      exchangeCodeAsync: (...args) => exchange(...args),
      useAuthRequest: () => [
        request,
        response,
        async () => {
          promptCalls++;
          return prompt();
        },
      ],
      makeRedirectUri: () => "pocketquests://callback",
      ResponseType: { Code: "code" },
    };
  if (name === "expo-web-browser") return { maybeCompleteAuthSession() {} };
  if (name === "react-native") return { Text: container, View: container };
  if (name === "expo-router") return { Redirect: () => null };
  if (name === "../session/session")
    return { useSession: () => ({ busy: false, signedIn: false, connect }) };
  if (name === "../session/session-page") return { Page: container };
  if (name === "../shared/journal-entry") return { JournalEntry: container };
  if (name === "../session/session-recovery")
    return { SessionRecovery: () => null };
  if (name === "../features/profile/account-settings")
    return { RecoveryPanel: () => null };
  if (name === "@honeydrunk/ui-native") return { Loading: () => null };
  if (name === "../shared/ui")
    return {
      Button: ({ title, onPress, disabled }) =>
        React.createElement("button", { onClick: onPress, disabled }, title),
      Label: container,
      Card: container,
      colors: {},
      styles: {},
    };
  return originalLoad.call(this, name, ...rest);
};
const { useProviderSetup } = require("../../src/session/use-provider-setup.ts");
const Welcome = require("../../src/app/index.tsx").default;
const { ProviderSignIn } = require("../../src/session/provider-sign-in.tsx");
const tick = () => new Promise((resolve) => setImmediate(resolve));
function deferred() {
  let resolve;
  const promise = new Promise((done) => {
    resolve = done;
  });
  return { promise, resolve };
}
function reset() {
  discover = async () => discovery;
  prompt = async () => ({ type: "cancel" });
  promptCalls = 0;
  response = null;
  exchange = async () => ({ accessToken: "test-access-token" });
  global.fetch = async () => ({ ok: true, json: async () => config });
}
async function mount(Component, strict = false) {
  const element = document.createElement("div");
  document.body.append(element);
  const root = createRoot(element);
  await act(async () => {
    const child = React.createElement(Component);
    root.render(
      strict ? React.createElement(React.StrictMode, null, child) : child,
    );
    await tick();
  });
  return {
    element,
    button: (title) =>
      [...element.querySelectorAll("button")].find(
        (button) => button.textContent === title,
      ),
    async click(button) {
      await act(async () => {
        button.click();
        await tick();
      });
    },
    async unmount() {
      await act(async () => root.unmount());
      element.remove();
    },
  };
}

for (const [name, Component, title] of [
  ["welcome", Welcome, "Sign in or create an account"],
  [
    "account proof",
    () =>
      React.createElement(ProviderSignIn, {
        title: "Verify account",
        onToken: connect,
      }),
    "Verify account",
  ],
]) {
  test(`${name}: a failed configuration can be retried without remounting`, async () => {
    reset();
    global.fetch = async () => ({ ok: false });
    const view = await mount(Component);
    try {
      assert.match(view.element.textContent, /Check your connection/);
      assert.equal(view.button(title), undefined);
      global.fetch = async () => ({ ok: true, json: async () => config });
      await view.click(view.button("Retry sign-in setup"));
      assert.equal(view.button(title).disabled, false);
      assert.equal(view.button("Retry sign-in setup"), undefined);
    } finally {
      await view.unmount();
    }
  });
  test(`${name}: failed discovery is handled and retry loads fresh endpoints`, async () => {
    reset();
    discover = async () => {
      throw new Error("private provider details");
    };
    const view = await mount(Component);
    try {
      assert.match(view.element.textContent, /Sign-in is unavailable/);
      assert.doesNotMatch(view.element.textContent, /private provider/);
      discover = async () => discovery;
      await view.click(view.button("Retry sign-in setup"));
      assert.equal(view.button(title).disabled, false);
    } finally {
      await view.unmount();
    }
  });
}

test("a stalled discovery times out; its late result cannot replace a successful retry", async (t) => {
  reset();
  const stalled = deferred();
  discover = () => stalled.promise;
  let current;
  function Reader() {
    current = useProviderSetup();
    return null;
  }
  t.mock.timers.enable({ apis: ["setTimeout"] });
  const view = await mount(Reader);
  try {
    await act(async () => {
      t.mock.timers.tick(10_001);
      await tick();
    });
    assert.match(current.error, /Sign-in is unavailable/);
    const fresh = {
      ...discovery,
      tokenEndpoint: "https://issuer.example.test/fresh-token",
    };
    discover = async () => fresh;
    await act(async () => {
      current.retry();
      await tick();
    });
    assert.equal(current.setup.discovery.tokenEndpoint, fresh.tokenEndpoint);
    await act(async () => {
      stalled.resolve(discovery);
      await tick();
    });
    assert.equal(current.setup.discovery.tokenEndpoint, fresh.tokenEndpoint);
    assert.equal(current.error, null);
  } finally {
    await view.unmount();
    t.mock.timers.reset();
  }
});

test("Strict Mode and unmount abort obsolete configuration loads without publishing them", async () => {
  reset();
  const first = deferred();
  const calls = [];
  global.fetch = async (_url, { signal }) => {
    calls.push(signal);
    if (calls.length === 1) return first.promise;
    return { ok: true, json: async () => config };
  };
  let current;
  let discoveryCalls = 0;
  discover = async () => {
    discoveryCalls++;
    return discovery;
  };
  function Reader() {
    current = useProviderSetup();
    return null;
  }
  const view = await mount(Reader, true);
  assert.equal(calls[0].aborted, true);
  assert.equal(current.setup.config.clientId, config.clientId);
  await view.unmount();
  await act(async () => {
    first.resolve({
      ok: true,
      json: async () => ({ ...config, clientId: "obsolete" }),
    });
    await tick();
  });
  assert.equal(discoveryCalls, 1);
  assert.equal(current.setup.config.clientId, config.clientId);
});

test("missing or insecure discovery endpoints never enable sign-in", async () => {
  for (const invalid of [
    {},
    { ...discovery, tokenEndpoint: "http://issuer.example.test/token" },
  ]) {
    reset();
    discover = async () => invalid;
    const view = await mount(Welcome);
    try {
      assert.match(view.element.textContent, /Sign-in is unavailable/);
      assert.equal(view.button("Sign in or create an account"), undefined);
    } finally {
      await view.unmount();
    }
  }
});

test("welcome handles browser rejection and cancellation and blocks a second prompt while opening", async () => {
  reset();
  const opening = deferred();
  prompt = () => opening.promise;
  const view = await mount(Welcome);
  try {
    const button = view.button("Sign in or create an account");
    await view.click(button);
    assert.equal(button.disabled, true);
    await view.click(button);
    assert.equal(promptCalls, 1);
    await act(async () => {
      opening.resolve({ type: "cancel" });
      await tick();
    });
    assert.equal(button.disabled, false);
    prompt = async () => {
      throw new Error("private browser details");
    };
    await view.click(button);
    assert.equal(button.disabled, false);
    assert.match(view.element.textContent, /Sign-in was interrupted/);
    assert.doesNotMatch(view.element.textContent, /private browser/);
    prompt = async () => ({ type: "error" });
    await view.click(button);
    assert.match(view.element.textContent, /Sign-in did not finish/);
  } finally {
    await view.unmount();
  }
});

test("account verification shows provider rejection and allows another attempt", async () => {
  reset();
  prompt = async () => ({
    type: "error",
    params: { error_description: "private provider details" },
  });
  const view = await mount(() =>
    React.createElement(ProviderSignIn, {
      title: "Verify account",
      onToken: connect,
    }),
  );
  try {
    await view.click(view.button("Verify account"));
    assert.match(view.element.textContent, /Sign-in did not finish/);
    assert.doesNotMatch(view.element.textContent, /private provider/);
    assert.equal(view.button("Verify account").disabled, false);
    prompt = async () => ({ type: "cancel" });
    await view.click(view.button("Verify account"));
    assert.doesNotMatch(view.element.textContent, /Sign-in did not finish/);
    assert.equal(view.button("Verify account").disabled, false);
  } finally {
    await view.unmount();
  }
});

test("account verification hides exchange details and does not submit a failed proof", async () => {
  reset();
  response = { type: "success", params: { code: "test-code" } };
  exchange = async () => {
    throw new Error("private exchange token or endpoint");
  };
  let proofs = 0;
  const view = await mount(() =>
    React.createElement(ProviderSignIn, {
      title: "Verify account",
      onToken: async () => {
        proofs++;
      },
    }),
  );
  try {
    assert.match(view.element.textContent, /Sign-in did not finish/);
    assert.doesNotMatch(view.element.textContent, /private exchange/);
    assert.equal(proofs, 0);
    assert.equal(view.button("Verify account").disabled, false);
  } finally {
    await view.unmount();
  }
});
