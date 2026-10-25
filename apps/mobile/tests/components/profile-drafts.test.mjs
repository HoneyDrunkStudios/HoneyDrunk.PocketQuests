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
let session, sent;
const load = Module._load;
Module._load = function (name, ...rest) {
  if (name === "react-native") return nativeWeb;
  if (name === "expo-crypto") return { randomUUID: () => "new-skill" };
  if (name.endsWith("/session")) {
    const read = () => session;
    return {
      useSession: read,
      useAccountSnapshot: read,
      useSessionStatus: read,
      useSessionActions: read,
    };
  }
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
const {
  ProfileInterests,
} = require("../../src/features/profile/profile-interests.tsx");
const {
  AppearanceEditor,
} = require("../../src/features/profile/appearance-editor.tsx");
const {
  ProfileEditor,
} = require("../../src/features/profile/profile-editor.tsx");
function reset() {
  sent = [];
  session = {
    busy: false,
    pending: false,
    offline: false,
    queuedCount: 0,
    state: {
      profile: {
        onboardingComplete: true,
        interests: ["work"],
        assessments: {},
        customSkills: [],
        badgeId: null,
        frameId: null,
      },
      entitlements: [
        { id: "badge-a", name: "Explorer", kind: "Badge", earned: true },
        { id: "badge-b", name: "Reader", kind: "Badge", earned: true },
        { id: "frame-a", name: "Gold", kind: "Frame", earned: true },
        { id: "locked", name: "Locked reward", kind: "Frame", earned: false },
      ],
    },
    catalog: {
      categories: [
        { id: "work", name: "Work" },
        { id: "rest", name: "Rest" },
      ],
      skills: [{ id: "s", name: "Programming" }],
    },
    command: async (input) => {
      sent.push(input);
    },
  };
}
async function mount(Component) {
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
    button(name) {
      return [...element.querySelectorAll('[role="button"]')].find(
        (button) => button.textContent === name,
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

test("profile interests edit locally and double Save sends one final selection", async () => {
  reset();
  let finish;
  session.command = (input) => {
    sent.push(input);
    return new Promise((resolve) => {
      finish = () => {
        session.state.profile.interests = [...input.interests].reverse();
        resolve();
      };
    });
  };
  const view = await mount(ProfileInterests);
  try {
    assert.equal(
      view.button("Save interests").getAttribute("aria-disabled"),
      "true",
    );
    await view.click("Selected: Work");
    await view.click("Choose: Rest");
    await view.click("Choose: Work");
    assert.deepEqual(sent, []);
    assert.notEqual(
      view.button("Selected: Work").getAttribute("aria-disabled"),
      "true",
    );
    const save = view.button("Save interests");
    await act(async () => {
      save.click();
      save.click();
    });
    assert.deepEqual(sent, [
      { action: "interests", interests: ["rest", "work"] },
    ]);
    await act(async () => finish());
    assert.equal(
      view.button("Save interests").getAttribute("aria-disabled"),
      "true",
    );
    session.state.profile.interests = [];
    await view.render();
    assert.ok(view.button("Choose: Work"));
    assert.ok(view.button("Choose: Rest"));
  } finally {
    await view.close();
  }
});

test("interest drafts survive failure and refresh, and reset does not send a command", async () => {
  reset();
  const view = await mount(ProfileInterests);
  try {
    await view.click("Choose: Rest");
    await view.click("Save interests"); // No receipt in this fixture: preserve the draft.
    assert.match(view.element.textContent, /unsaved interest choices/);
    session.state.profile.interests = [];
    await view.render();
    assert.ok(view.button("Selected: Work"));
    session.offline = true;
    session.queuedCount = 1;
    await view.render();
    assert.equal(
      view.button("Reset interest choices").getAttribute("aria-disabled"),
      "true",
    );
    session.offline = false;
    session.queuedCount = 0;
    await view.render();
    await view.click("Reset interest choices");
    assert.ok(view.button("Choose: Work"));
    assert.equal(sent.length, 1);
    await view.click("Save interests");
    assert.equal(sent.length, 1);
  } finally {
    await view.close();
  }
});

test("badge and frame selections preview locally, saving only the changed field", async () => {
  reset();
  session.command = async (input) => {
    sent.push(input);
    session.state.profile[
      input.action === "select-badge" ? "badgeId" : "frameId"
    ] = input.rewardId;
  };
  const view = await mount(AppearanceEditor);
  try {
    await view.click("Choose badge: Explorer");
    await view.click("Choose badge: Reader");
    await view.click("Choose frame: Gold");
    assert.deepEqual(sent, []);
    assert.match(view.element.textContent, /Preview: Reader/);
    assert.doesNotMatch(view.element.textContent, /Locked reward/);
    await view.click("Save badge");
    assert.deepEqual(sent, [{ action: "select-badge", rewardId: "badge-b" }]);
    assert.equal(
      view.button("Save badge").getAttribute("aria-disabled"),
      "true",
    );
    await view.click("Save frame");
    assert.deepEqual(sent[1], { action: "select-frame", rewardId: "frame-a" });
    await view.click("No badge");
    await view.click("Default frame");
    assert.equal(sent.length, 2);
    await view.click("Save badge");
    await view.click("Save frame");
    assert.deepEqual(sent.slice(2), [
      { action: "select-badge", rewardId: null },
      { action: "select-frame", rewardId: null },
    ]);
  } finally {
    await view.close();
  }
});

test("failed appearance saves retain the preview, guard double clicks and allow local reset", async () => {
  reset();
  let finish;
  session.command = (input) => {
    sent.push(input);
    return new Promise((resolve) => (finish = resolve));
  };
  const view = await mount(AppearanceEditor);
  try {
    await view.click("Choose badge: Explorer");
    const save = view.button("Save badge");
    await act(async () => {
      save.click();
      save.click();
    });
    assert.equal(sent.length, 1);
    await act(async () => finish());
    assert.match(view.element.textContent, /Preview: Explorer/);
    await view.click("Reset badge choice");
    assert.match(view.element.textContent, /Selected: No badge/);
    assert.equal(sent.length, 1);
  } finally {
    await view.close();
  }
});

test("skill choices are local and a failed correction keeps its confirmation for retry", async () => {
  reset();
  session.state.profile.onboardingComplete = false;
  const view = await mount(ProfileEditor);
  try {
    await view.click("Programming: Practiced");
    await view.click("Programming: Expert");
    assert.deepEqual(sent, []);
    await view.click("Confirm experience correction");
    assert.deepEqual(sent, [
      { action: "assess-skill", skillId: "s", experience: "Expert" },
    ]);
    assert.ok(view.button("Confirm experience correction"));
    session.state.profile.assessments.s = "Expert";
    await view.render();
    assert.equal(view.button("Confirm experience correction"), undefined);
    await view.click("Programming: New");
    await view.click("Keep current experience");
    assert.equal(sent.length, 1);
  } finally {
    await view.close();
  }
});

test("newer local preferences survive an older queued save confirming after they were edited", async () => {
  for (const scenario of [
    {
      Component: ProfileInterests,
      choose: "Choose: Rest",
      save: "Save interests",
      edit: "Selected: Rest",
      confirm: () => {
        session.state.profile.interests = ["work", "rest"];
      },
      check: (view) => assert.ok(view.button("Choose: Rest")),
    },
    {
      Component: AppearanceEditor,
      choose: "Choose badge: Explorer",
      save: "Save badge",
      edit: "No badge",
      confirm: () => {
        session.state.profile.badgeId = "badge-a";
      },
      check: (view) =>
        assert.match(view.element.textContent, /Preview: No badge/),
    },
    {
      Component: ProfileEditor,
      choose: "Programming: Expert",
      save: "Confirm experience correction",
      edit: "Programming: New",
      confirm: () => {
        session.state.profile.assessments.s = "Expert";
      },
      check: (view) => {
        assert.ok(view.button("Confirm experience correction"));
        assert.match(view.element.textContent, /change to New/);
      },
    },
  ]) {
    reset();
    const view = await mount(scenario.Component);
    try {
      await view.click(scenario.choose);
      await view.click(scenario.save);
      session.queuedCount = 1;
      session.pending = true;
      await view.render();
      await view.click(scenario.edit);
      scenario.confirm();
      session.queuedCount = 0;
      session.pending = false;
      await view.render();
      scenario.check(view);
      assert.equal(sent.length, 1);
    } finally {
      await view.close();
    }
  }
});

test("local preference choices remain responsive offline while Save is unavailable", async () => {
  reset();
  session.offline = true;
  const view = await mount(ProfileInterests);
  try {
    await view.click("Choose: Rest");
    assert.ok(view.button("Selected: Rest"));
    assert.equal(
      view.button("Save interests").getAttribute("aria-disabled"),
      "true",
    );
    await view.click("Save interests");
    assert.deepEqual(sent, []);
  } finally {
    await view.close();
  }
});
