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
let session, item, sent, currentId;
const load = Module._load;
Module._load = function (name, ...rest) {
  if (name === "react-native") return nativeWeb;
  if (name.endsWith("/session")) {
    const read = () => session;
    return {
      useSession: read,
      useAccountSnapshot: read,
      useSessionStatus: read,
      useSessionActions: read,
    };
  }
  if (name.endsWith("/session-page"))
    return {
      Page: ({ children }) => React.createElement("main", null, children),
    };
  if (name === "expo-crypto") return { randomUUID: () => "fixture-id" };
  if (name.endsWith("/notifications"))
    return { requestWarningPermission: async () => false };
  if (name === "expo-router")
    return {
      Link: ({ children, href, accessibilityLabel }) =>
        React.createElement(
          "a",
          {
            href: typeof href === "string" ? href : `/quest/${href.params.id}`,
            "aria-label": accessibilityLabel,
          },
          children,
        ),
      useLocalSearchParams: () => ({ id: currentId }),
      useFocusEffect: () => {},
      Redirect: ({ href }) =>
        React.createElement("span", { "data-redirect": href }),
    };
  return load.call(this, name, ...rest);
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
const { QuestCard } = require("../../src/features/quests/quest-card.tsx");
const QuestDetails = require("../../src/app/quest/[id].tsx").default;
const { SessionRecovery } = require("../../src/session/session-recovery.tsx");
function reset() {
  sent = [];
  currentId = "o";
  item = {
    occurrence: {
      id: "o",
      quest: {
        id: "q",
        title: "Practice",
        criterion: "Finish one practice",
        description: "Take a deliberate break",
        categoryId: "c01",
        rank: "F",
        effort: "Small",
        baseXp: 10,
        attributes: [],
        skills: [],
        isCustom: true,
      },
      dueDate: "2026-10-18",
      plannedTime: "09:30",
      parentId: null,
      lifecycle: null,
    },
    status: "Active",
    canUndo: false,
    completion: null,
  };
  session = {
    signedIn: true,
    busy: false,
    pending: false,
    command: async (command) => sent.push(command),
    previewClock: async () => ({
      adjusted: false,
      repeated: false,
      resolved: "2026-10-18 09:30",
      instant: "2026-10-18T09:30:00Z",
    }),
    catalog: {
      categories: [{ id: "c01", name: "Health" }],
      skills: [],
      attributes: [],
    },
    state: {
      today: "2026-10-18",
      zone: "UTC",
      occurrences: [item],
      definitions: [],
      penalties: [],
      skills: [],
    },
  };
}
async function mount(Component = QuestCard) {
  const element = document.createElement("div");
  document.body.append(element);
  const root = createRoot(element);
  const view = {
    element,
    async render() {
      await act(async () =>
        root.render(
          React.createElement(
            ThemeProvider,
            { theme: pocketQuestsTheme },
            React.createElement(Component, { item }),
          ),
        ),
      );
    },
    button(name) {
      return [...element.querySelectorAll('[role="button"],button')].find(
        (el) => el.textContent === name,
      );
    },
    async click(name) {
      const button = view.button(name);
      assert.ok(button, name);
      await act(async () => button.click());
    },
    async close() {
      await act(async () => root.unmount());
      element.remove();
    },
  };
  await view.render();
  return view;
}
test("compact journal exposes a unique detail link and heading without repeating criterion or promising earned XP", async () => {
  reset();
  const view = await mount();
  try {
    assert.equal(
      view.element.querySelector('[role="heading"]').textContent,
      "Practice",
    );
    assert.equal(
      view.element
        .querySelector('a[aria-label="View quest & rewards: Practice"]')
        .getAttribute("href"),
      "/quest/o",
    );
    assert.match(view.element.textContent, /Health.*Active/);
    assert.match(view.element.textContent, /10 base XP · Rank F · Small/);
    assert.match(view.element.textContent, /Due today.*Planned 09:30/);
    assert.doesNotMatch(
      view.element.textContent,
      /Finish one practice|Take a deliberate break|Complete:/,
    );
    item.occurrence.dueDate = null;
    item.occurrence.plannedTime = null;
    item.status = "Completed";
    await view.render();
    assert.match(
      view.element.textContent,
      /Completed.*10 base XP.*Unscheduled/,
    );
    assert.deepEqual(sent, []);
  } finally {
    await view.close();
  }
});
test("compact cards retain frozen, accepted-loss and unconfirmed-completion warnings", async () => {
  reset();
  item.status = "Frozen";
  item.occurrence.lifecycle = { lockedLoss: 5 };
  item.pendingCompletion = true;
  const view = await mount();
  try {
    assert.match(
      view.element.textContent,
      /Accepted loss: up to 5 category XP/,
    );
    assert.match(
      view.element.textContent,
      /Frozen: resume its category\/account pause first/,
    );
    assert.match(
      view.element.textContent,
      /Completion awaiting sync · rewards unconfirmed/,
    );
    item.pendingTimingVerification = true;
    await view.render();
    assert.match(
      view.element.textContent,
      /Completion pending timing verification — rewards unconfirmed/,
    );
    assert.match(view.element.textContent, /Timing unverified/);
    assert.doesNotMatch(view.element.textContent, /Pending sync/);
    assert.equal(view.button("Complete: Practice"), undefined);
  } finally {
    await view.close();
  }
});
test("journal Undo uses the current completion and remains separate from navigation", async () => {
  reset();
  item.status = "Completed";
  item.canUndo = true;
  item.completion = { id: "c-original" };
  const view = await mount();
  try {
    assert.equal(view.button("Undo: Practice").closest("a"), null);
    for (const flag of ["busy", "pending"]) {
      session[flag] = true;
      await view.render();
      assert.equal(
        view.button("Undo: Practice").getAttribute("aria-disabled"),
        "true",
      );
      await view.click("Undo: Practice");
      assert.equal(sent.length, 0);
      session[flag] = false;
    }
    item.completion = { id: "c-current" };
    await view.render();
    await view.click("Undo: Practice");
    assert.deepEqual(sent, [
      { action: "undo", occurrenceId: "o", completionId: "c-current" },
    ]);
    item.canUndo = false;
    await view.render();
    assert.equal(view.button("Undo: Practice"), undefined);
  } finally {
    await view.close();
  }
});
test("active details put the criterion and explicit completion before base reward preview and retain optional timer", async () => {
  reset();
  const view = await mount(QuestDetails);
  try {
    const text = view.element.textContent;
    assert.match(text, /Health.*Practice.*Active · Rank F · Small/);
    assert.match(
      text,
      /Complete when.*Finish one practice.*Take a deliberate break/,
    );
    assert.ok(
      text.indexOf("Complete: Practice") < text.indexOf("Completion rewards"),
    );
    assert.match(text, /No timer required/);
    assert.match(text, /Separate progress pools, not extra overall XP/);
    assert.equal(
      view.element.querySelector('[role="heading"]').textContent,
      "Practice",
    );
    await view.click("Do Now with an optional timer");
    assert.ok(view.button("Start focus timer"));
    assert.deepEqual(sent, []);
    await view.click("Close focus timer");
    for (const flag of ["busy", "pending"]) {
      session[flag] = true;
      await view.render();
      await view.click("Complete: Practice");
      assert.equal(sent.length, 0);
      session[flag] = false;
    }
    await view.render();
    await view.click("Complete: Practice");
    assert.deepEqual(sent, [{ action: "complete", occurrenceId: "o" }]);
    item.status = "Completed";
    await view.render();
    assert.equal(view.button("Complete: Practice"), undefined);
    assert.ok(view.element.textContent.includes("Completion rewards"));
  } finally {
    await view.close();
  }
});

test("timing-unverified details keep reward guidance consistent with recovery warnings", async () => {
  reset();
  item.status = "Completed";
  item.pendingCompletion = true;
  item.pendingTimingVerification = true;
  session.pending = true;
  const view = await mount(QuestDetails);
  try {
    assert.match(
      view.element.textContent,
      /Reconnecting alone cannot confirm this completion or its rewards/,
    );
    assert.match(
      view.element.textContent,
      /Timing verification is still required; reconnecting alone cannot confirm rewards/,
    );
    assert.doesNotMatch(
      view.element.textContent,
      /confirmed rewards appear after synchronization/,
    );
    assert.equal(view.button("Complete: Practice"), undefined);
    item.pendingTimingVerification = false;
    await view.render();
    assert.match(
      view.element.textContent,
      /Confirmed rewards require a verified completion/,
    );
    assert.doesNotMatch(
      view.element.textContent,
      /Timing verification is still required/,
    );
    item.pendingTimingVerification = true;
    item.pendingCompletion = false;
    item.status = "Active";
    await view.render();
    assert.match(
      view.element.textContent,
      /Confirmed rewards require a verified completion/,
    );
    assert.doesNotMatch(
      view.element.textContent,
      /Timing verification is still required|Reconnecting alone cannot confirm this completion/,
    );
    assert.deepEqual(sent, []);
  } finally {
    await view.close();
  }
});

test("normal online saves stay quiet while queued, in flight and confirmed", async () => {
  reset();
  const view = await mount(SessionRecovery);
  try {
    for (const busy of [true, false]) {
      session.queuedCount = 1;
      session.pending = true;
      session.busy = busy;
      await view.render();
      assert.equal(view.element.textContent, "");
      assert.equal(view.element.querySelector("[aria-live]"), null);
      assert.equal(view.button("Try saving again"), undefined);
    }
    session.queuedCount = 0;
    session.pending = false;
    await view.render();
    assert.equal(view.element.textContent, "");
  } finally {
    await view.close();
  }
});

test("a failed online save offers retry and escape without waiting for the server", async () => {
  reset();
  session.queuedCount = 1;
  session.pending = true;
  session.error = "This change could not be saved. Try again.";
  session.retry = async () => sent.push("retry");
  session.discardPending = async () => sent.push("discard");
  session.signOut = async (discard) => sent.push({ signOut: discard });
  const view = await mount(SessionRecovery);
  try {
    assert.match(view.element.textContent, /This change could not be saved/);
    assert.doesNotMatch(view.element.textContent, /Displayed XP/);
    assert.match(view.element.textContent, /discard them to continue/);
    await view.click("Try again");
    assert.deepEqual(sent, ["retry"]);
    await view.click("Discard these changes");
    await view.click("Discard changes and sign out");
    assert.deepEqual(sent, ["retry", "discard", { signOut: true }]);
  } finally {
    await view.close();
  }
});

test("offline, rejected and unverified changes retain their recovery controls", async () => {
  const entry = {
    command: { operationId: "saved-action" },
    label: "Choose interests",
    reason: "Review this saved change.",
    blockedBy: [],
  };
  for (const attention of [
    { offline: true },
    { rejected: [{ ...entry, kind: "rejected" }] },
    { unverified: [entry] },
  ]) {
    reset();
    Object.assign(session, attention, {
      queuedCount: 1,
      error: attention.unverified?.[0]?.reason ?? null,
      retry: async () => sent.push("retry"),
      discardRejected: async (id) => sent.push({ rejected: id }),
      discardUnverified: async (id) => sent.push({ unverified: id }),
    });
    const view = await mount(SessionRecovery);
    try {
      assert.ok(view.button("Discard these changes"));
      await view.click("Try saving again");
      assert.deepEqual(sent, ["retry"]);
      if (attention.rejected || attention.unverified) {
        assert.doesNotMatch(view.element.textContent, /saved-action/);
        if (attention.unverified)
          assert.doesNotMatch(
            view.element.textContent,
            /Review this saved change/,
          );
        await view.click("Show details");
        assert.match(
          view.element.textContent,
          /Support reference: saved-action/,
        );
        if (attention.unverified)
          assert.match(view.element.textContent, /Review this saved change/);
        await view.click("Hide details");
        assert.doesNotMatch(view.element.textContent, /saved-action/);
        const unverified = !!attention.unverified;
        await view.click(
          `${unverified ? "Discard activity" : "Discard change"}: Choose interests`,
        );
        assert.deepEqual(sent[1], {
          [unverified ? "unverified" : "rejected"]: "saved-action",
        });
      }
    } finally {
      await view.close();
    }
  }
});

test("an in-flight command disables destructive sign-out when its recovery panel appears, while quarantine keeps its escape", async () => {
  reset();
  session.queuedCount = 1;
  session.busy = true;
  session.offline = true;
  session.recoveryRequired = false;
  session.signOut = async (discard) => sent.push({ signOut: discard });
  const view = await mount(SessionRecovery);
  try {
    const label = "Discard changes and sign out";
    assert.equal(view.button(label).getAttribute("aria-disabled"), "true");
    await view.click(label);
    assert.deepEqual(sent, []);
    session.recoveryRequired = true;
    await view.render();
    assert.notEqual(view.button(label).getAttribute("aria-disabled"), "true");
    await view.click(label);
    assert.deepEqual(sent, [{ signOut: true }]);
    session.recoveryRequired = false;
    session.busy = false;
    await view.render();
    assert.notEqual(view.button(label).getAttribute("aria-disabled"), "true");
  } finally {
    await view.close();
  }
});

test("changing detail occurrences resets optional focus and targets the current occurrence", async () => {
  reset();
  const view = await mount(QuestDetails);
  try {
    await view.click("Do Now with an optional timer");
    assert.ok(view.button("Start focus timer"));
    const next = {
      ...item,
      occurrence: { ...item.occurrence, quest: { ...item.occurrence.quest } },
    };
    next.occurrence.id = "next";
    next.occurrence.quest.id = "PQ-CAT-Q01";
    next.occurrence.quest.isCustom = false;
    session.state.occurrences.push(next);
    currentId = "next";
    await view.render();
    assert.equal(view.button("Start focus timer"), undefined);
    assert.ok(view.button("Do Now with an optional timer"));
    await view.click("Complete: Practice");
    assert.deepEqual(sent, [{ action: "complete", occurrenceId: "next" }]);
    next.occurrence.quest.id = "quest-without-focus";
    await view.render();
    assert.equal(view.button("Do Now with an optional timer"), undefined);
  } finally {
    await view.close();
  }
});
