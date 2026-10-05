import test from "node:test";
import assert from "node:assert/strict";
import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { JSDOM } = require("jsdom");
const dom = new JSDOM("<!doctype html><html><body></body></html>", {
  url: "https://app.example.test",
});
global.window = dom.window;
global.document = dom.window.document;
global.IS_REACT_ACT_ENVIRONMENT = true;
const React = require("react"),
  { act } = React,
  { createRoot } = require("react-dom/client");
process.env.PQ_REAL_REACT = "1";
const {
  account,
  createHarness,
  SessionProvider,
  useSession,
  useAccountSnapshot,
} = require("./provider-harness.cjs");
const { decodeAccount } = require("../../src/session/cache-format.ts");
const tick = () => new Promise((resolve) => setImmediate(resolve));
function deferred() {
  let resolve;
  const promise = new Promise((done) => {
    resolve = done;
  });
  return { promise, resolve };
}
async function mount(host, { strict = false } = {}) {
  const container = document.createElement("div");
  document.body.append(container);
  const root = createRoot(container);
  let current,
    accountRenders = 0;
  function Reader() {
    current = useSession();
    return null;
  }
  function AccountReader() {
    useAccountSnapshot();
    accountRenders++;
    return null;
  }
  const children = React.createElement(
    React.Fragment,
    null,
    React.createElement(Reader),
    React.createElement(AccountReader),
  );
  const element = React.createElement(
    SessionProvider,
    { clockSource: host.clockSource },
    children,
  );
  await act(async () => {
    root.render(
      strict ? React.createElement(React.StrictMode, null, element) : element,
    );
    await tick();
  });
  return {
    get current() {
      return current;
    },
    get accountRenders() {
      return accountRenders;
    },
    async unmount() {
      await act(async () => root.unmount());
      container.remove();
    },
    async settle() {
      await act(async () => {
        await tick();
        await tick();
      });
    },
  };
}

test("Strict Mode boots once, keeps actions stable, isolates status renders and cleans up native/web subscriptions", async () => {
  const host = createHarness();
  host.platform = "web";
  const view = await mount(host, { strict: true });
  try {
    assert.equal(view.current.signedIn, true);
    assert.equal(
      host.requests.filter((r) => r.url.endsWith("/api/profile")).length,
      1,
    );
    assert.equal(host.appListeners.size, 1);
    const command = view.current.command,
      renders = view.accountRenders,
      warnings = host.warningCalls.length;
    host.online = false;
    await act(async () => {
      window.dispatchEvent(new window.Event("online"));
      await tick();
    });
    assert.equal(view.current.offline, true);
    assert.equal(view.current.command, command);
    assert.equal(
      view.accountRenders,
      renders,
      "status changes must not rerender account-only consumers",
    );
    assert.equal(
      host.warningCalls.length,
      warnings,
      "status changes must not reschedule unchanged warnings",
    );
  } finally {
    await view.unmount();
  }
  assert.equal(host.appListeners.size, 0);
  const requests = host.requests.length;
  window.dispatchEvent(new window.Event("online"));
  await tick();
  assert.equal(host.requests.length, requests);
});

test("unmount during cache opening prevents stale boot publication; remount retains pending work", async () => {
  const pending = {
    operationId: "retained",
    action: "complete",
    occurrenceId: "quest",
  };
  const host = createHarness(account([pending]));
  host.online = false;
  const opening = deferred();
  host.beforeLoad = () => opening.promise;
  const view = await mount(host);
  await view.unmount();
  await act(async () => {
    opening.resolve();
    await tick();
  });
  assert.deepEqual(host.disk.queue, [pending]);
  assert.equal(host.requests.length, 0);
  delete host.beforeLoad;
  const reopened = await mount(host);
  try {
    assert.equal(reopened.current.queuedCount, 1);
    assert.equal(reopened.current.offline, true);
  } finally {
    await reopened.unmount();
  }
});

test("logout cancels a concurrent sign-in before profile response and prevents session resurrection", async () => {
  const host = createHarness();
  const view = await mount(host);
  const gate = deferred();
  let started;
  const entered = new Promise((resolve) => {
    started = resolve;
  });
  host.respond = async (url) => {
    if (url.endsWith("/api/profile")) {
      started();
      await gate.promise;
    }
    return null;
  };
  let connecting, loggingOut;
  try {
    await act(async () => {
      connecting = view.current.connect("replacement").catch((error) => error);
      await entered;
    });
    await act(async () => {
      loggingOut = view.current.signOut(true);
      await tick();
    });
    await act(async () => {
      gate.resolve();
      await connecting;
      await loggingOut;
    });
    assert.equal(view.current.signedIn, false);
    assert.equal(view.current.state, null);
    assert.equal(host.savedSession, null);
    assert.equal(host.disk, null);
  } finally {
    gate.resolve();
    await view.unmount();
  }
});

test("account switch cannot replay another owner's retained queue; malformed success retries the same operation", async () => {
  const command = {
    operationId: "original-operation",
    action: "complete",
    occurrenceId: "quest",
  };
  const host = createHarness(account([command]));
  host.online = false;
  const view = await mount(host);
  try {
    host.online = true;
    host.identityUser = "other-owner";
    await act(async () => {
      await assert.rejects(
        view.current.connect("other-token"),
        /previous account/,
      );
    });
    assert.deepEqual(host.disk.queue, [command]);
    assert.equal(host.sent.length, 0);
    host.identityUser = "owner";
    let malformed = true;
    host.respond = async (url) =>
      url.endsWith("/api/commands") && malformed
        ? { ok: true, json: async () => ({ categories: [] }) }
        : null;
    await act(async () => {
      await assert.rejects(
        view.current.connect("owner-token"),
        /unreadable response/,
      );
    });
    assert.deepEqual(host.disk.queue, [command]);
    malformed = false;
    await act(async () => {
      await view.current.retry();
    });
    const attempts = host.requests.filter((r) =>
      r.url.endsWith("/api/commands"),
    );
    assert.equal(attempts.length, 2);
    assert.deepEqual(attempts[0].body, attempts[1].body);
    assert.equal(host.disk.queue.length, 0);
  } finally {
    await view.unmount();
  }
});

test("damaged display stays hidden on boot while its journal survives until verified synchronization", async () => {
  const saved = account([
    { operationId: "cache-operation", action: "complete" },
  ]);
  delete saved.state.categories;
  const recovered = decodeAccount(saved, "owner");
  assert.equal(recovered.requiresReload, true);
  const host = createHarness(recovered);
  host.online = false;
  host.state = account().state;
  const view = await mount(host);
  try {
    assert.equal(view.current.state, null);
    assert.equal(view.current.queuedCount, 1);
    host.online = true;
    await act(async () => {
      await view.current.connect("owner-token");
    });
    assert.ok(view.current.state.categories);
    assert.equal(view.current.queuedCount, 0);
    assert.equal(host.sent[0].operationId, "cache-operation");
  } finally {
    await view.unmount();
  }
});

test("notification reconciliation ignores a stale completion after logout", async () => {
  const host = createHarness();
  const old = deferred();
  host.syncWarnings = (state) =>
    state ? old.promise : Promise.resolve("cleared");
  const view = await mount(host);
  try {
    await act(async () => {
      await view.current.signOut(true);
    });
    assert.equal(view.current.notificationStatus, "cleared");
    await act(async () => {
      old.resolve("stale scheduled status");
      await tick();
    });
    assert.equal(view.current.notificationStatus, "cleared");
    assert.equal(view.current.state, null);
  } finally {
    old.resolve("done");
    await view.unmount();
  }
});
