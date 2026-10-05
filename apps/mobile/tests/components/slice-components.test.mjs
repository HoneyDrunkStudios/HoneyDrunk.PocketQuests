import test from "node:test";
import { strict as assert } from "node:assert";
import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const ts = require("typescript");
const nativeWeb = require("react-native-web");
const React = require("react");
const { renderToStaticMarkup } = require("react-dom/server");
const Module = require("node:module");
const load = Module._load;
let state;
let signedIn = true;
let pending = false;
let rejected = [];
let unverified = [];
let recoveryRequired = false;
let busy = false;
Module._load = function (name, ...rest) {
  if (name === "react-native") return nativeWeb;
  if (name === "expo-crypto") return { randomUUID: () => "fixture-id" };
  if (name.endsWith("/session")) {
    const read = () => ({
      state,
      catalog: null,
      busy,
      recoveryRequired,
      pending,
      signedIn,
      rejected,
      unverified,
      queuedCount: 0,
    });
    return {
      useSession: read,
      useAccountSnapshot: read,
      useSessionStatus: read,
      useSessionActions: read,
    };
  }
  if (name === "expo-router")
    return {
      Redirect: ({ href }) =>
        React.createElement("span", { "data-redirect": href }),
      Link: ({ children }) => React.createElement("a", null, children),
      useLocalSearchParams: () => ({ id: "o" }),
      useFocusEffect: () => {},
    };
  if (name === "react-native-safe-area-context")
    return { useSafeAreaInsets: () => ({ top: 0, bottom: 0 }) };
  if (name.endsWith("/available-quests"))
    return {
      AvailableQuests: () =>
        React.createElement("div", null, "Available quest content"),
    };
  if (name.endsWith("/profile-editor"))
    return {
      ProfileEditor: () => React.createElement("div", null, "Onboarding"),
    };
  return load.call(this, name, ...rest);
};
for (const extension of [".ts", ".tsx"])
  require.extensions[extension] = (module, file) => {
    module._compile(
      ts.transpileModule(require("node:fs").readFileSync(file, "utf8"), {
        compilerOptions: {
          module: ts.ModuleKind.CommonJS,
          jsx: ts.JsxEmit.ReactJSX,
          target: ts.ScriptTarget.ES2022,
        },
      }).outputText,
      file,
    );
  };
const { ThemeProvider } = require("@honeydrunk/ui-native");
const { pocketQuestsTheme } = require("../../src/shared/theme.ts");
const Home = require("../../src/app/(tabs)/index.tsx").default;
const Quests = require("../../src/app/(tabs)/board.tsx").default;
const {
  CompletionCelebration,
} = require("../../src/features/progression/completion-celebration.tsx");
const { FocusTimer } = require("../../src/features/quests/focus-timer.tsx");
const QuestDetails = require("../../src/app/quest/[id].tsx").default;
const { Page } = require("../../src/session/session-page.tsx");
const { QuestHistory } = require("../../src/features/quests/quest-history.tsx");
const { AppErrorBoundary } = require("../../src/shared/error-boundary.tsx");
const render = (component, props = {}) =>
  renderToStaticMarkup(
    React.createElement(
      ThemeProvider,
      { theme: pocketQuestsTheme },
      React.createElement(component, props),
    ),
  );

test("large history initially mounts a bounded SectionList window with accessible quest links", () => {
  const wire = require("../../../../contracts/wire-fixtures.json");
  const data = Array.from({ length: 1000 }, (_, index) => {
    const item = structuredClone(wire.state.occurrences[0]);
    item.occurrence.id = `history-${index}`;
    item.occurrence.quest.title = `History item ${index}`;
    return item;
  });
  const html = render(QuestHistory, {
    sections: [{ key: "history", title: "Unscheduled history", data }],
  });
  assert.match(html, /Unscheduled history/);
  assert.match(html, /History item 0/);
  assert.doesNotMatch(html, /History item 999/);
  assert.ok((html.match(/Open quest/g) ?? []).length <= 20);
});

test("quarantine-only recovery displays labeled discard controls even with zero readable actions", () => {
  recoveryRequired = true;
  busy = true;
  const html = render(Page);
  assert.match(html, /unreadable recovery copy/);
  assert.match(html, /Discard pending changes and sign out/);
  const buttons =
    html.match(/<button\b[^>]*>[\s\S]*?<\/button>/g) ??
    html.match(/<div[^>]*role="button"[\s\S]*?<\/div>/g);
  assert.ok(
    buttons?.some(
      (button) =>
        button.includes("Discard pending changes and sign out") &&
        !button.includes('aria-disabled="true"'),
    ),
  );
  recoveryRequired = false;
  busy = false;
});

test("unexpected render failures expose a labeled retry and never echo private error content", () => {
  const html = render(AppErrorBoundary, {
    error: new Error("secret-token and private quest content"),
    retry: async () => {},
  });
  assert.match(html, /Try opening the screen again/);
  assert.match(html, /role="button"/);
  assert.match(html, /role="alert"/);
  assert.doesNotMatch(html, /secret-token|private quest content/);
});

test("sync recovery shows each retained action and reason with individual discard", () => {
  rejected = [
    {
      command: { operationId: "bad-id" },
      kind: "rejected",
      label: "complete: Practice",
      reason: "Deadline passed",
    },
    {
      command: { operationId: "undo-id" },
      kind: "blocked",
      label: "undo: Practice",
      reason: "Earlier completion rejected",
      blockedBy: ["bad-id"],
    },
  ];
  const html = render(Page);
  assert.match(html, /Rejected: complete: Practice/);
  assert.match(html, /Deadline passed/);
  assert.match(html, /Blocked: undo: Practice/);
  assert.match(html, /Discard this action: complete: Practice/);
  assert.match(html, /Discard this action: undo: Practice/);
  assert.match(html, /Related action IDs: bad-id/);
  rejected = [];
});

test("timing-unverified actions are visible without promising reconnect will confirm rewards", () => {
  unverified = [
    {
      command: { operationId: "unverified-id" },
      label: "complete: Practice",
      reason: "Device time could not be verified",
      blockedBy: [],
    },
  ];
  const html = render(Page);
  assert.match(html, /Pending timing verification: complete: Practice/);
  assert.match(html, /Device time could not be verified/);
  assert.match(html, /Reconnecting alone cannot verify/);
  assert.match(html, /Discard this unverified action: complete: Practice/);
  unverified = [];
});

test("Home exposes the full character sheet with an accessible portrait and no Today quest actions", () => {
  state = {
    profile: { onboardingComplete: true },
    entitlements: [],
    overallXp: 100,
    overallLevel: 2,
    streaks: [{ categoryId: "c0", days: 1, qualifiedToday: true, rate: 0 }],
    categories: Array.from({ length: 10 }, (_, i) => ({
      id: `c${i}`,
      name: `Category ${i}`,
      xp: i,
      level: 1,
    })),
    attributes: Array.from({ length: 8 }, (_, i) => ({
      id: `a${i}`,
      name: `Attribute ${i}`,
      xp: 0,
      level: 1,
    })),
    skills: Array.from({ length: 18 }, (_, i) => ({
      id: `s${i}`,
      name: `Skill ${i}`,
      xp: 0,
      level: 1,
    })),
    rank: {
      current: "F",
      requirement: { rank: "E", count: 2, floor: 70, total: 300 },
      qualifyingCategories: 0,
      total: 45,
    },
  };
  const html = render(Home);
  assert.match(html, /aria-label="Character portrait silhouette"/);
  assert.match(html, /role="img"/);
  assert.match(html, /role="heading"/);
  assert.match(html, /Level 2/);
  for (const name of ["Category 9", "Attribute 7", "Skill 17", "Global rank F"])
    assert.ok(html.includes(name));
  assert.doesNotMatch(html, /Complete:|Today quests|No quests planned/);
  assert.match(html, /Category 0: 1 day in this streak\. Done today\./);
  state.streaks[0] = {
    categoryId: "c0",
    days: 20,
    qualifiedToday: false,
    rate: 19,
  };
  const ongoing = render(Home);
  assert.match(
    ongoing,
    /Category 0: 20 days in this streak\. Complete today to continue\./,
  );
  assert.notEqual(html, ongoing);
  state.streaks[0] = {
    categoryId: "c0",
    days: 0,
    qualifiedToday: false,
    rate: 0,
  };
  assert.match(
    render(Home),
    /Category 0: 0 days in this streak\. Complete a quest to start a streak\./,
  );
});
test("quest sections expose selected tab semantics and onboarding remains reachable", () => {
  state = { profile: { onboardingComplete: true }, occurrences: [] };
  const html = render(Quests);
  assert.match(html, /role="tablist"/);
  assert.equal((html.match(/role="tab"/g) ?? []).length, 3);
  assert.match(html, /aria-selected="true"/);
  assert.match(html, /Available quest content/);
  state.profile.onboardingComplete = false;
  assert.match(render(Quests), /Onboarding/);
});
test("celebration renders named rewards and explicit level-ups after brief Undo expires", () => {
  const html = render(CompletionCelebration, {
    feedback: {
      title: "A test quest",
      completionId: "c",
      occurrenceId: "o",
      until: 0,
      rewards: [{ track: "Overall", name: "Overall", xp: 10 }],
      levelUps: [{ track: "Overall", name: "Overall", from: 1, to: 2 }],
      rankUp: "E",
      unlocks: ["A test badge"],
    },
    canUndo: true,
    onUndo: () => {},
    onDismiss: () => {},
  });
  assert.match(html, /Quest complete!/);
  assert.match(html, /Level up!/);
  assert.match(html, /Level 1/);
  assert.match(html, /Global rank promoted to E/);
  assert.match(html, /A test badge/);
  assert.doesNotMatch(html, /Undo recent completion/);
  assert.match(html, /Undo remains in quest details/);
  assert.match(html, /Keep adventuring/);
});
test("focus timer begins as an optional input without a completion action", () => {
  const html = render(FocusTimer);
  assert.match(html, /aria-label="Focus minutes, 1 to 180"/);
  assert.match(html, /Start focus timer/);
  assert.doesNotMatch(html, /Complete:|Quest complete!/);
});
test("signed-out quest deep links reach sign-in and missing signed-in quests have a recovery route", () => {
  state = null;
  signedIn = false;
  assert.match(render(QuestDetails), /data-redirect="\/"/);
  signedIn = true;
  state = { occurrences: [] };
  assert.match(render(QuestDetails), /Back to quests/);
});

test("custom definition controls stay accessible in active/frozen details and respect archive/pending state", () => {
  const quest = {
    id: "custom",
    title: "My quest",
    criterion: "A clear result",
    categoryId: "c01",
    rank: "F",
    effort: "Small",
    baseXp: 10,
    attributes: [],
    skills: [],
    isCustom: true,
  };
  const definition = { quest, revision: 2, archived: false };
  const item = {
    occurrence: {
      id: "o",
      quest,
      dueDate: null,
      plannedTime: null,
      parentId: null,
      acceptedAt: "2026-10-03T12:00:00Z",
      lifecycle: null,
    },
    status: "Active",
    canUndo: false,
    completion: null,
  };
  state = {
    occurrences: [item],
    definitions: [definition],
    penalties: [],
    skills: [],
  };
  for (const status of ["Active", "Frozen", "Completed"]) {
    item.status = status;
    const html = render(QuestDetails);
    assert.match(html, /role="heading"[^>]*>Manage custom quest/);
    for (const label of [
      "Edit: My quest",
      "Set recurrence: My quest",
      "Archive definition: My quest",
    ])
      assert.ok(html.includes(`aria-label="${label}"`), `${status}: ${label}`);
    assert.match(
      html,
      /Completed snapshots and accepted penalty terms stay preserved/,
    );
  }
  pending = true;
  const disabled = render(QuestDetails);
  assert.match(
    disabled,
    /aria-disabled="true"[^>]*aria-label="Archive definition: My quest"|aria-label="Archive definition: My quest"[^>]*aria-disabled="true"/,
  );
  pending = false;
  definition.archived = true;
  assert.doesNotMatch(
    render(QuestDetails),
    /aria-label="(?:Edit|Archive definition|Set recurrence): My quest"/,
  );
  state.definitions = [];
  quest.isCustom = false;
  assert.doesNotMatch(
    render(QuestDetails),
    /Manage custom quest|aria-label="Edit:/,
  );
});
