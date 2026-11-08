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
let session;
const load = Module._load;
Module._load = function (name, ...rest) {
  if (name === "react-native") return nativeWeb;
  if (name.endsWith("/session")) return { useAccountSnapshot: () => session };
  if (name === "expo-router")
    return {
      Link: ({ href, children, accessibilityLabel }) =>
        React.createElement(
          "a",
          { href, "aria-label": accessibilityLabel },
          children,
        ),
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
const {
  RewardJournal,
} = require("../../src/features/progression/reward-journal.tsx");
const {
  RewardSummary,
} = require("../../src/features/progression/reward-summary.tsx");
const {
  rewardProgress,
  nextReward,
} = require("../../src/features/progression/reward-milestones.ts");
const definitions = [
  ["A01", "Achievement", "A Quest of My Own", 1, "F", "custom quest"],
  ["A02", "Achievement", "Room to Recharge", 5, "F", "Rest & Recreation quest"],
  ["B01", "Badge", "Curious Explorer", 3, "F", "Exploration & Adventure quest"],
  ["B02", "Badge", "Everyday Care", 10, "F", "Everyday Life quest"],
  ["F01", "Frame", "Open Notebook", 10, "E", "Learning quest"],
  ["F02", "Frame", "Quiet Horizon", 10, "D", "Rest & Recreation quest"],
];
function reset() {
  session = {
    state: {
      rank: { current: "F" },
      profile: { badgeId: null, frameId: null },
      entitlements: definitions.map(
        ([id, kind, name, requiredCount, requiredRank]) => ({
          id: `PQ-CAT-${id}`,
          kind,
          name,
          count: 0,
          requiredCount,
          requiredRank,
          earned: false,
        }),
      ),
    },
  };
}
async function mount(Component = RewardJournal) {
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
            React.createElement(Component),
          ),
        ),
      );
    },
    async click(title) {
      const button = [...element.querySelectorAll('[role="button"]')].find(
        (item) => item.textContent === title,
      );
      assert.ok(button, title);
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

test("all six starter conditions explain server thresholds, including rank gates", () => {
  reset();
  for (const [index, reward] of session.state.entitlements.entries()) {
    const copy = rewardProgress(reward, "F");
    assert.ok(copy.requirement.includes(definitions[index][5]));
    assert.ok(copy.requirement.includes(String(reward.requiredCount)));
    assert.equal(copy.rankMet, index < 4);
    for (const count of [
      reward.requiredCount - 1,
      reward.requiredCount,
      reward.requiredCount + 1,
    ]) {
      const progress = rewardProgress({ ...reward, count }, "S");
      assert.ok(!progress.guidance.includes("-1"));
      assert.equal(progress.rankMet, true);
      assert.match(
        progress.guidance,
        count < reward.requiredCount ? /1 more/ : /Completion requirement met/,
      );
    }
  }
  assert.match(
    rewardProgress({ ...session.state.entitlements[0], id: "future" }, "F")
      .requirement,
    /qualifying quest/,
  );
  // Display a changed server threshold, never a second local threshold table.
  assert.match(
    rewardProgress({ ...session.state.entitlements[0], requiredCount: 7 }, "F")
      .requirement,
    /Complete 7 custom quests/,
  );
});

test("filters expose earned and in-progress rewards without commands or mutations", async () => {
  reset();
  Object.assign(session.state.entitlements[2], { count: 4, earned: true });
  session.state.profile.badgeId = "PQ-CAT-B01";
  const before = structuredClone(session.state);
  const view = await mount();
  try {
    assert.equal(
      view.element.querySelectorAll('[role="progressbar"]').length,
      6,
    );
    assert.match(view.element.textContent, /1 of 6 rewards earned/);
    await view.click("Earned");
    assert.equal(
      view.element
        .querySelector('[aria-label="Selected: Earned"]')
        .getAttribute("role"),
      "button",
    );
    assert.match(view.element.textContent, /Curious Explorer/);
    assert.match(view.element.textContent, /Badge · Earned · Selected/);
    assert.doesNotMatch(view.element.textContent, /Open Notebook/);
    const bar = view.element.querySelector('[role="progressbar"]');
    assert.equal(bar.getAttribute("aria-valuenow"), "3");
    assert.equal(
      bar.getAttribute("aria-valuetext"),
      "4 of 3 qualifying completions",
    );
    await view.click("In progress");
    assert.equal(
      view.element.querySelectorAll('[role="progressbar"]').length,
      5,
    );
    assert.doesNotMatch(view.element.textContent, /Curious Explorer/);
    await view.click("All rewards");
    assert.equal(
      view.element.querySelectorAll('[role="progressbar"]').length,
      6,
    );
    assert.deepEqual(session.state, before);
  } finally {
    await view.close();
  }
});

test("Undo, recompletion and rank loss refresh earned state without selecting or granting rewards", async () => {
  reset();
  Object.assign(session.state.entitlements[4], { count: 10, earned: true });
  session.state.rank.current = "E";
  session.state.profile.frameId = "PQ-CAT-F01";
  const view = await mount();
  try {
    await view.click("Earned");
    assert.match(view.element.textContent, /Frame · Earned · Selected/);
    // Rank loss with sufficient completions must not be interpreted as earned.
    session.state = {
      ...session.state,
      rank: { current: "F" },
      profile: { ...session.state.profile, frameId: null },
      entitlements: session.state.entitlements.map((r) => ({
        ...r,
        earned: false,
      })),
    };
    await view.render();
    assert.match(view.element.textContent, /No rewards earned yet/);
    await view.click("In progress");
    assert.match(
      view.element.textContent,
      /Completion requirement met. Reach current rank E or higher/,
    );
    assert.match(
      view.element.textContent,
      /Current rank E or higher: not yet met/,
    );
    session.state.rank.current = "E";
    session.state.entitlements[4] = {
      ...session.state.entitlements[4],
      count: 9,
    };
    await view.render();
    assert.match(view.element.textContent, /1 more Learning quest to complete/);
    assert.match(view.element.textContent, /Current rank E or higher: met/);
    Object.assign(session.state.entitlements[4], { count: 10, earned: true });
    await view.render();
    await view.click("Earned");
    assert.match(view.element.textContent, /Open Notebook/);
    assert.doesNotMatch(view.element.textContent, /Frame · Earned · Selected/);
    assert.equal(session.state.profile.frameId, null);
  } finally {
    await view.close();
  }
});

test("account-data unloading removes private rewards and resets the filter", async () => {
  reset();
  const view = await mount();
  try {
    await view.click("Earned");
    assert.match(view.element.textContent, /No rewards earned yet/);
    session.state = null;
    await view.render();
    assert.equal(view.element.textContent, "");
    reset();
    await view.render();
    assert.equal(
      view.element.querySelectorAll('[role="progressbar"]').length,
      6,
    );
    assert.ok(
      view.element.querySelector('[aria-label="Selected: All rewards"]'),
    );
  } finally {
    await view.close();
  }
});

test("Home summary suggests an unearned reward without mutating state and handles all-earned or missing data", async () => {
  reset();
  Object.assign(session.state.entitlements[4], { count: 10 });
  const before = structuredClone(session.state);
  assert.equal(nextReward(session.state.entitlements).id, "PQ-CAT-F01");
  assert.deepEqual(session.state, before);
  const view = await mount(RewardSummary);
  try {
    assert.match(view.element.textContent, /Keep growing: Open Notebook/);
    assert.match(view.element.textContent, /Reach current rank E/);
    assert.equal(
      view.element.querySelector("a").getAttribute("href"),
      "/rewards",
    );
    session.state.entitlements = session.state.entitlements.map((reward) => ({
      ...reward,
      earned: true,
    }));
    await view.render();
    assert.match(view.element.textContent, /6 of 6 rewards earned/);
    assert.match(view.element.textContent, /every starter reward/);
    session.state.entitlements = [];
    await view.render();
    assert.equal(view.element.textContent, "");
    session.state = null;
    await view.render();
    assert.equal(view.element.textContent, "");
  } finally {
    await view.close();
  }
});

test("reward journal names the clothing and accessories each milestone unlocks", async () => {
  reset();
  for (const rank of ["E", "D", "C"])
    session.state.entitlements.push({
      id: `PQ-RANK-${rank}`,
      kind: "Achievement",
      name: `Rank ${rank}`,
      count: 0,
      requiredCount: 0,
      requiredRank: rank,
      earned: false,
    });
  session.state.entitlements.push({
    id: "PQ-LEVEL-02",
    kind: "Achievement",
    name: "Level 2",
    count: 1,
    requiredCount: 2,
    requiredRank: "F",
    earned: false,
  });
  const view = await mount();
  try {
    for (const name of [
      "Maker\u2019s vest",
      "Cozy knit",
      "Scholar\u2019s coat",
      "Explorer\u2019s satchel",
      "Everyday toolkit",
      "Horizon cape",
    ])
      assert.ok(view.element.textContent.includes(`Unlocks ${name}`));
    assert.match(
      view.element.textContent,
      /Reach overall level 2.*Maker's Crest/,
    );
    assert.match(view.element.textContent, /Reach rank E.*Sunlit Workshop/);
  } finally {
    await view.close();
  }
});
