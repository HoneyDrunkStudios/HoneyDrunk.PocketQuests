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
  if (name.endsWith("/session")) return { useSession: () => session };
  if (name.endsWith("/session-recovery"))
    return {
      SessionRecovery: () =>
        React.createElement("div", { role: "status" }, session.error),
    };
  if (name === "react-native-safe-area-context")
    return { useSafeAreaInsets: () => ({ top: 24, bottom: 24 }) };
  if (name === "./character-editor")
    return {
      CharacterEditor: ({ onContinue }) =>
        React.createElement(
          "button",
          {
            role: "button",
            "aria-disabled":
              !session.state ||
              session.busy ||
              session.pending ||
              session.offline,
            disabled:
              !session.state ||
              session.busy ||
              session.pending ||
              session.offline,
            onClick: onContinue,
          },
          "Continue with character",
        ),
    };
  if (name === "./avatar-editor")
    return {
      AvatarEditor: ({ onContinue }) =>
        React.createElement(
          "button",
          { role: "button", onClick: onContinue },
          "Continue with avatar",
        ),
    };
  if (name === "./starting-skills")
    return {
      StartingSkills: ({ onContinue }) =>
        React.createElement(
          "button",
          { role: "button", onClick: onContinue },
          "Continue with these skills",
        ),
    };
  if (name === "expo-router")
    return {
      useRouter: () => ({ replace() {} }),
      Redirect: ({ href }) =>
        React.createElement("span", { "data-redirect": href }),
      Link: ({ children }) => children,
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
const { Onboarding } = require("../../src/features/profile/onboarding.tsx");
function reset() {
  sent = [];
  session = {
    state: {
      overallXp: 42,
      profile: {
        onboardingComplete: false,
        interests: ["work"],
        assessments: {},
        characterName: null,
        portraitId: null,
      },
    },
    queuedCount: 0,
    catalog: {
      skills: [
        { id: "s07", name: "Programming" },
        { id: "s01", name: "Strength Training" },
      ],
      categories: [
        { id: "work", name: "Work & Purpose" },
        { id: "rest", name: "Rest & Recreation" },
      ],
    },
    busy: false,
    pending: false,
    offline: false,
    command: async (input) => {
      sent.push(input);
      if (input.action === "interests")
        session.state = {
          ...session.state,
          profile: { ...session.state.profile, interests: input.interests },
        };
    },
    signOut: async () => {},
  };
}
async function mount(Component = Onboarding, props = {}) {
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
            React.createElement(Component, props),
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
    async fill(label, value) {
      await act(async () => {
        const input = element.querySelector(`[aria-label="${label}"]`);
        Object.getOwnPropertyDescriptor(
          dom.window.HTMLInputElement.prototype,
          "value",
        ).set.call(input, value);
        input.dispatchEvent(new dom.window.Event("input", { bubbles: true }));
      });
    },
    async unmount() {
      await act(async () => root.unmount());
      element.remove();
    },
  };
  await view.render();
  return view;
}
test("first-run is a guided flow without profile editing or navigation controls", async () => {
  reset();
  const view = await mount();
  try {
    assert.match(view.element.textContent, /Create your character/);
    assert.doesNotMatch(
      view.element.textContent,
      /Custom skill name|Current assessment|Your quests/,
    );
    assert.equal(
      view.element.querySelector('[aria-label="Settings and profile"]'),
      null,
    );
    assert.equal(view.element.querySelector('[role="tablist"]'), null);
    await view.click("Continue with character");
    if (view.element.textContent.includes("Continue with avatar"))
      await view.click("Continue with avatar");
    const selected = view.element.querySelector(
      '[aria-label="Work & Purpose"]',
    );
    assert.equal(selected.getAttribute("aria-checked"), "true");
    await act(async () => selected.click());
    assert.deepEqual(sent, []);
    assert.equal(selected.getAttribute("aria-checked"), "false");
    assert.deepEqual(session.state.profile.interests, ["work"]);
    // A background refresh must not overwrite choices still being edited.
    session.state.profile.interests = ["rest"];
    await view.render();
    assert.equal(
      view.element
        .querySelector('[aria-label="Work & Purpose"]')
        .getAttribute("aria-checked"),
      "false",
    );
    await view.click("Continue");
    assert.deepEqual(sent, [{ action: "interests", interests: [] }]);
    assert.match(view.element.textContent, /Starting skills/);
    await view.click("Back");
    assert.match(view.element.textContent, /Your interests/);
    assert.equal(sent.length, 1);
  } finally {
    await view.unmount();
  }
});

test("multiple interest taps stay local and Continue submits their final selection once", async () => {
  reset();
  let finish;
  session.command = (input) => {
    sent.push(input);
    return new Promise((resolve) => {
      finish = () => {
        session.state.profile.interests = [...input.interests];
        resolve();
      };
    });
  };
  const view = await mount();
  try {
    await view.click("Continue with character");
    if (view.element.textContent.includes("Continue with avatar"))
      await view.click("Continue with avatar");
    const work = view.element.querySelector('[aria-label="Work & Purpose"]');
    const rest = view.element.querySelector('[aria-label="Rest & Recreation"]');
    await act(async () => {
      work.click();
      rest.click();
      work.click();
    });
    assert.equal(work.getAttribute("aria-checked"), "true");
    assert.equal(rest.getAttribute("aria-checked"), "true");
    assert.notEqual(rest.getAttribute("aria-disabled"), "true");
    assert.deepEqual(sent, []);
    const next = view.button("Continue");
    await act(async () => {
      next.click();
      next.click();
    });
    assert.deepEqual(sent, [
      { action: "interests", interests: ["rest", "work"] },
    ]);
    assert.match(view.element.textContent, /Your interests/);
    assert.equal(
      view.button("Saving choices…").getAttribute("aria-disabled"),
      "true",
    );
    await act(async () => finish());
    assert.match(view.element.textContent, /Starting skills/);
    await view.click("Back");
    await view.click("Continue");
    assert.equal(sent.length, 1);
  } finally {
    await view.unmount();
  }
});

test("confirmed interests advance permanently while a later skill command is pending", async () => {
  reset();
  const view = await mount();
  try {
    await view.click("Continue with character");
    await view.click("Continue with avatar");
    await act(async () =>
      view.element.querySelector('[aria-label="Rest & Recreation"]').click(),
    );
    await view.click("Continue");
    assert.match(view.element.textContent, /Starting skills/);
    session.busy = true;
    session.pending = true;
    session.queuedCount = 1;
    await view.render();
    assert.match(view.element.textContent, /Starting skills/);
    assert.ok(view.button("Continue with these skills"));
    assert.equal(
      view.element.querySelector('[aria-label="Rest & Recreation"]'),
      null,
    );
    session.busy = false;
    session.pending = false;
    session.queuedCount = 0;
    await view.render();
    await view.click("Continue with these skills");
    assert.ok(view.button("Choose my first quest"));
    assert.equal(sent.length, 1);
  } finally {
    await view.unmount();
  }
});

test("failed interest saves retain the draft and stay on the selection step for retry", async () => {
  reset();
  session.command = async (input) => {
    sent.push(input);
    session.error = "Please try saving again";
  };
  const view = await mount();
  try {
    await view.click("Continue with character");
    if (view.element.textContent.includes("Continue with avatar"))
      await view.click("Continue with avatar");
    await act(async () =>
      view.element.querySelector('[aria-label="Work & Purpose"]').click(),
    );
    await view.click("Continue");
    assert.match(view.element.textContent, /Your interests/);
    assert.match(view.element.textContent, /Please try saving again/);
    assert.equal(
      view.element
        .querySelector('[aria-label="Work & Purpose"]')
        .getAttribute("aria-checked"),
      "false",
    );
    session.command = async (input) => {
      sent.push(input);
      session.error = null;
      session.state.profile.interests = input.interests;
    };
    await view.render();
    await view.click("Continue");
    assert.equal(sent.length, 2);
    assert.deepEqual(sent[1], { action: "interests", interests: [] });
    assert.match(view.element.textContent, /Starting skills/);
  } finally {
    await view.unmount();
  }
});

test("editing after a failed save cannot advance when an older selection is later confirmed", async () => {
  reset();
  session.command = async (input) => {
    sent.push(input);
    session.pending = true;
    session.error = "Please retry this save";
  };
  const view = await mount();
  try {
    await view.click("Continue with character");
    if (view.element.textContent.includes("Continue with avatar"))
      await view.click("Continue with avatar");
    await act(async () =>
      view.element.querySelector('[aria-label="Rest & Recreation"]').click(),
    );
    await view.click("Continue");
    await act(async () =>
      view.element.querySelector('[aria-label="Work & Purpose"]').click(),
    );
    session.state.profile.interests = sent[0].interests;
    session.pending = false;
    session.error = null;
    session.command = async (input) => {
      sent.push(input);
      session.state.profile.interests = input.interests;
    };
    await view.render();
    assert.match(view.element.textContent, /Your interests/);
    assert.equal(
      view.element
        .querySelector('[aria-label="Work & Purpose"]')
        .getAttribute("aria-checked"),
      "false",
    );
    await view.click("Continue");
    assert.deepEqual(sent[1], { action: "interests", interests: ["rest"] });
    assert.match(view.element.textContent, /Starting skills/);
  } finally {
    await view.unmount();
  }
});

test("choices remain local across Back and loading account data seeds untouched choices", async () => {
  reset();
  const loadedState = session.state;
  session.state = null;
  const view = await mount();
  try {
    session.state = loadedState;
    await view.render();
    await view.click("Continue with character");
    if (view.element.textContent.includes("Continue with avatar"))
      await view.click("Continue with avatar");
    const work = () =>
      view.element.querySelector('[aria-label="Work & Purpose"]');
    assert.equal(work().getAttribute("aria-checked"), "true");
    await act(async () => work().click());
    await view.click("Back");
    await view.click("Back");
    await view.click("Continue with character");
    if (view.element.textContent.includes("Continue with avatar"))
      await view.click("Continue with avatar");
    assert.equal(work().getAttribute("aria-checked"), "false");
    assert.deepEqual(sent, []);
  } finally {
    await view.unmount();
  }
});
test("finishing waits for confirmed onboarding; errors stay visible and retry remains possible", async () => {
  reset();
  const view = await mount();
  try {
    await view.click("Continue with character");
    if (view.element.textContent.includes("Continue with avatar"))
      await view.click("Continue with avatar");
    await view.click("Continue");
    await view.click("Continue with these skills");
    await view.click("Choose my first quest");
    assert.deepEqual(sent, [{ action: "finish-onboarding" }]);
    assert.equal(view.element.querySelector("[data-redirect]"), null);
    session.pending = true;
    session.error = "Waiting for confirmation";
    await view.render();
    assert.equal(
      view.button("Choose my first quest").getAttribute("aria-disabled"),
      "true",
    );
    assert.match(view.element.textContent, /Waiting for confirmation/);
    session.pending = false;
    session.error = "Please try again";
    await view.render();
    await view.click("Choose my first quest");
    assert.equal(sent.length, 2);
    session.state.profile.onboardingComplete = true;
    await view.render();
    assert.equal(
      view.element
        .querySelector("[data-redirect]")
        .getAttribute("data-redirect"),
      "/(tabs)/board",
    );
  } finally {
    await view.unmount();
  }
});
test("offline, missing account data and in-flight saves cannot complete setup", async () => {
  for (const override of [
    { offline: true },
    { state: null },
    { catalog: null },
    { busy: true },
    { pending: true },
  ]) {
    reset();
    Object.assign(session, override);
    const view = await mount();
    try {
      if (!session.state || !session.catalog) {
        assert.match(view.element.textContent, /Loading your journal/);
        assert.equal(sent.length, 0);
        continue;
      }
      assert.equal(
        view.button("Continue with character").getAttribute("aria-disabled"),
        "true",
      );
      await view.click("Continue with character");
      if (view.element.textContent.includes("Continue with avatar"))
        await view.click("Continue with avatar");
      assert.match(view.element.textContent, /Create your character/);
      assert.equal(sent.length, 0);
    } finally {
      await view.unmount();
    }
  }
});
test("returning accounts bypass setup without issuing commands", async () => {
  reset();
  session.state.profile.onboardingComplete = true;
  const view = await mount();
  try {
    assert.equal(
      view.element
        .querySelector("[data-redirect]")
        .getAttribute("data-redirect"),
      "/(tabs)/board",
    );
    assert.equal(sent.length, 0);
  } finally {
    await view.unmount();
  }
});

const {
  CharacterEditor,
} = require("../../src/features/profile/character-editor.tsx");
const {
  StartingSkills,
} = require("../../src/features/profile/starting-skills.tsx");

test("character creation waits for confirmation and preserves a failed name/portrait draft", async () => {
  reset();
  let advanced = 0;
  session.command = async (input) => {
    sent.push(input);
    session.error = "Connection interrupted";
  };
  const view = await mount(CharacterEditor, { onContinue: () => advanced++ });
  try {
    await view.fill("Character name", "Rowan");
    await view.click("Save name and continue");
    assert.equal(advanced, 0);
    assert.equal(session.state.profile.characterName, null);
    assert.equal(
      view.element.querySelector('[aria-label="Character name"]').value,
      "Rowan",
    );
    assert.deepEqual(sent[0], {
      action: "save-character",
      characterName: "Rowan",
      portraitId: "portrait-1",
    });
    session.command = async (input) => {
      sent.push(input);
      session.state.profile.characterName = input.characterName;
      session.state.profile.portraitId = input.portraitId;
      session.error = null;
    };
    await view.render();
    await view.click("Save name and continue");
    await view.render();
    assert.equal(advanced, 1);
    assert.equal(session.state.overallXp, 42);
    await view.render();
    assert.equal(advanced, 1);
  } finally {
    await view.unmount();
  }
});

test("starting-skill choices remain drafts and save only changed assessments in confirmed order", async () => {
  reset();
  const renderErrors = [];
  const originalError = console.error;
  console.error = (...args) => renderErrors.push(args.join(" "));
  let advanced = 0;
  session.command = async (input) => {
    sent.push(input);
    session.state.profile.assessments[input.skillId] = input.experience;
  };
  const view = await mount(StartingSkills, { onContinue: () => advanced++ });
  try {
    await act(async () =>
      view.element
        .querySelector('[aria-label="Programming: Practiced"]')
        .click(),
    );
    await act(async () =>
      view.element
        .querySelector('[aria-label="Strength Training: Experienced"]')
        .click(),
    );
    assert.deepEqual(sent, []);
    await view.click("Continue with these skills");
    await view.render();
    await view.render();
    assert.deepEqual(
      sent.map((entry) => entry.skillId),
      ["s07", "s01"],
    );
    assert.equal(advanced, 1);
    assert.equal(session.state.overallXp, 42);
    assert.deepEqual(renderErrors, []);
  } finally {
    console.error = originalError;
    await view.unmount();
  }
});

test("an older character confirmation cannot advance a newer edited draft", async () => {
  reset();
  let advanced = 0;
  session.command = async (input) => {
    sent.push(input);
    session.error = "Save delayed";
  };
  const view = await mount(CharacterEditor, { onContinue: () => advanced++ });
  try {
    await view.fill("Character name", "Rowan");
    await view.click("Save name and continue");
    await view.fill("Character name", "Juniper");
    session.state.profile.characterName = "Rowan";
    session.state.profile.portraitId = "portrait-1";
    await view.render();
    assert.equal(advanced, 0);
    assert.equal(
      view.element.querySelector('[aria-label="Character name"]').value,
      "Juniper",
    );
    assert.equal(sent.length, 1);
  } finally {
    await view.unmount();
  }
});

for (const recovered of ["Timing check discarded", "Rejected action discarded"]) {
  test(`starting-skill setup resumes after ${recovered.toLowerCase()}`, async () => {
    reset();
    let advanced = 0;
    session.command = async (input) => {
      sent.push(input);
      session.error = "Needs recovery";
      session.queuedCount = 1;
    };
    const view = await mount(StartingSkills, { onContinue: () => advanced++ });
    try {
      await act(async () => view.element.querySelector('[aria-label="Programming: Practiced"]').click());
      await view.click("Continue with these skills");
      await view.render();
      assert.equal(advanced, 0);
      session.queuedCount = 0;
      session.error = recovered;
      await view.render();
      await view.click("Review skill choices");
      session.command = async (input) => {
        sent.push(input);
        session.state.profile.assessments[input.skillId] = input.experience;
        session.error = null;
      };
      await view.click("Continue with these skills");
      await view.render();
      assert.equal(advanced, 1);
      assert.equal(sent.length, 2);
    } finally { await view.unmount(); }
  });
}

test("starting-skill setup can continue unchanged after an informational recovery message", async () => {
  reset(); session.error = "Rejected action discarded";
  let advanced = 0;
  const view = await mount(StartingSkills, { onContinue: () => advanced++ });
  try { await view.click("Continue with these skills"); await view.render(); assert.equal(advanced, 1); assert.equal(sent.length, 0); }
  finally { await view.unmount(); }
});
