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
  AvatarEditor,
} = require("../../src/features/profile/avatar-editor.tsx");
const {
  AvatarFigure,
} = require("../../src/features/profile/avatar-figure.tsx");
const {
  decodeAvatar,
  encodeAvatar,
  canWear,
  starterLooks,
  randomAvatar,
  hairNames,
  hairStyles,
  faceNames,
  buildNames,
  outfits,
  accessories,
} = require("../../src/features/profile/avatar-look.ts");

function reset() {
  sent = [];
  session = {
    busy: false,
    pending: false,
    offline: false,
    queuedCount: 0,
    state: {
      profile: { avatarCode: null },
      entitlements: [
        { id: "PQ-CAT-A01", name: "A Quest of My Own", earned: false },
        { id: "PQ-CAT-F01", name: "Open Notebook", earned: false },
        { id: "PQ-CAT-B01", name: "Curious Explorer", earned: false },
      ],
    },
    command: async (input) => {
      sent.push(input);
    },
  };
}
async function mount(Component, props = {}) {
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
      return [
        ...element.querySelectorAll('[role="button"], [role="radio"]'),
      ].find((button) => button.textContent === name);
    },
    async tab(name) {
      const tab = [...element.querySelectorAll('[role="tab"]')].find(
        (node) => node.textContent === name,
      );
      assert.ok(tab, name);
      await act(async () => tab.click());
    },
    async click(name) {
      // Existing editing scenarios navigate the same visible sections as a player.
      if (name === "Choose a starting look") return view.tab("Looks");
      if (!view.button(name)) {
        const section =
          hairNames.includes(name) || hairStyles.includes(name)
            ? "Hair"
            : faceNames.includes(name) ||
                buildNames.includes(name) ||
                name.startsWith("Tone ")
              ? "Identity"
              : outfits.some((item) => name.endsWith(": " + item.name))
                ? "Clothing"
                : accessories.some((item) => name.endsWith(": " + item.name))
                  ? "Accessories"
                  : null;
        if (section) await view.tab(section);
      }
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

test("free look edits and reset are local; Save sends one complete appearance", async () => {
  reset();
  const view = await mount(AvatarEditor);
  try {
    await view.click("Tone 6");
    await view.click("Silver");
    await view.click("Curls");
    assert.equal(sent.length, 0);
    assert.match(view.element.textContent, /Preview — not saved/);
    assert.ok(view.element.querySelector("svg"));
    await view.click("Reset avatar preview");
    assert.doesNotMatch(view.element.textContent, /Preview — not saved/);
    await view.click("Tone 6");
    await view.click("Save avatar");
    assert.deepEqual(sent, [
      { action: "save-avatar", avatarCode: "v1:5:0:0:0:0" },
    ]);
    session.state.profile.avatarCode = sent[0].avatarCode;
    await view.render();
    assert.match(view.element.textContent, /Your saved look/);
  } finally {
    await view.close();
  }
});

test("locked cosmetics can be previewed but not saved; unlocks refresh and rank loss blocks stale drafts", async () => {
  reset();
  const view = await mount(AvatarEditor);
  try {
    await view.click("Preview locked: Scholar’s coat");
    assert.match(view.element.textContent, /Previewing a locked item/);
    assert.equal(
      view.button("Save avatar").getAttribute("aria-disabled"),
      "true",
    );
    await view.click("Save avatar");
    assert.equal(sent.length, 0);
    session.state.entitlements[1].earned = true;
    await view.render();
    assert.doesNotMatch(view.element.textContent, /Previewing a locked item/);
    await view.click("Save avatar");
    assert.equal(sent[0].avatarCode, "v1:2:0:0:3:0");
    session.state.profile.avatarCode = sent[0].avatarCode;
    await view.render();
    session.state.entitlements[1].earned = false;
    session.state.profile.avatarCode = "v1:2:0:0:0:0";
    await view.render();
    assert.match(view.element.textContent, /Your saved look/);
    assert.ok(
      view.element.querySelector(
        '[aria-label="Selected preview: Trail tunic"]',
      ),
    );
  } finally {
    await view.close();
  }
});

test("failed saves preserve previews; pending and offline states prevent duplicate writes", async () => {
  reset();
  let finish;
  session.command = (input) => {
    sent.push(input);
    return new Promise((_, reject) => {
      finish = reject;
    });
  };
  const view = await mount(AvatarEditor);
  try {
    await view.click("Chestnut");
    await view.click("Save avatar");
    await view.click("Save avatar");
    assert.equal(sent.length, 1);
    await act(async () => finish(new Error("unavailable")));
    assert.match(view.element.textContent, /couldn’t save your look/);
    assert.ok(
      view.element.querySelector(
        '[aria-label="Selected: Hair color: Chestnut"]',
      ),
    );
    session.offline = true;
    await view.render();
    await view.click("Silver");
    await view.click("Save avatar");
    assert.equal(sent.length, 1);
    assert.match(view.element.textContent, /Reconnect to save/);
    session.offline = false;
    session.pending = true;
    await view.render();
    assert.equal(
      view.button("Save avatar").getAttribute("aria-disabled"),
      "true",
    );
    session.pending = false;
    session.command = async (input) => sent.push(input);
    await view.render();
    await view.click("Save avatar");
    assert.equal(sent[1].avatarCode, "v1:2:3:0:0:0");
  } finally {
    await view.close();
  }
});

test("late save acknowledgement keeps a newer preview and account unloading clears it", async () => {
  reset();
  const view = await mount(AvatarEditor);
  try {
    await view.click("Chestnut");
    await view.click("Save avatar");
    session.queuedCount = 1;
    await view.click("Silver");
    session.state.profile.avatarCode = "v1:2:1:0:0:0";
    session.queuedCount = 0;
    await view.render();
    assert.ok(
      view.element.querySelector('[aria-label="Selected: Hair color: Silver"]'),
    );
    assert.match(view.element.textContent, /Preview — not saved/);
    session.state = null;
    await view.render();
    assert.equal(view.element.textContent, "");
    reset();
    await view.render();
    await view.tab("Hair");
    assert.ok(
      view.element.querySelector(
        '[aria-label="Selected: Hair color: Midnight"]',
      ),
    );
  } finally {
    await view.close();
  }
});

test("versioned appearances round-trip every slot and all six cosmetic gates", () => {
  const look = {
    skin: 5,
    hairColor: 3,
    hairStyle: 2,
    outfit: 3,
    accessory: 3,
    face: 0,
    build: 0,
  };
  assert.deepEqual(decodeAvatar(encodeAvatar(look)), look);
  assert.equal(encodeAvatar(decodeAvatar("v2:5:3:2:3:3")), "v1:2:0:0:0:0");
  const outfitRewards = [null, "PQ-CAT-A01", "PQ-CAT-A02", "PQ-CAT-F01"];
  const accessoryRewards = [null, "PQ-CAT-B01", "PQ-CAT-B02", "PQ-CAT-F02"];
  for (let index = 1; index < 4; index++) {
    assert.equal(canWear({ ...look, outfit: index, accessory: 0 }, []), false);
    assert.equal(
      canWear({ ...look, outfit: index, accessory: 0 }, [
        { id: outfitRewards[index], earned: true },
      ]),
      true,
    );
    assert.equal(
      canWear({ ...look, outfit: 0, accessory: index }, [
        { id: accessoryRewards[index], earned: true },
      ]),
      true,
    );
  }
});

test("expanded appearances validate every boundary and preserve legacy codes", () => {
  for (const code of [
    "v1:0:0:0:0:0",
    "v1:5:3:3:3:3",
    "v2:5:7:7:5:5:2:2",
    "v2:5:7:7:5:5:6:2",
  ])
    assert.equal(encodeAvatar(decodeAvatar(code)), code);
  for (const code of [
    "v2:6:0:0:0:0:0:0",
    "v2:0:8:0:0:0:0:0",
    "v2:0:0:8:0:0:0:0",
    "v2:0:0:0:6:0:0:0",
    "v2:0:0:0:0:6:0:0",
    "v2:0:0:0:0:0:7:0",
    "v2:0:0:0:0:0:0:3",
    "v2:0:0:0:0:0:0:0 ",
    "v2:0:0:0:0:0:0:0\n",
    "v1:0:0:0:0:0\n",
    null,
  ])
    assert.equal(encodeAvatar(decodeAvatar(code)), "v1:2:0:0:0:0");
});

test("all starter presets are free and randomization respects current ownership", () => {
  assert.equal(starterLooks.length, 10);
  assert.equal(decodeAvatar(randomAvatar([], () => 0.999)).face, 6);
  for (const preset of starterLooks) {
    assert.equal(encodeAvatar(decodeAvatar(preset.code)), preset.code);
    assert.equal(canWear(decodeAvatar(preset.code), []), true);
  }
  const rewards = [
    { id: "PQ-CAT-A01", earned: true },
    { id: "PQ-CAT-B01", earned: true },
  ];
  for (const value of [0, 0.1, 0.25, 0.4, 0.6, 0.75, 0.9, 0.999]) {
    const free = decodeAvatar(randomAvatar([], () => value));
    assert.ok([0, 4, 5].includes(free.outfit));
    assert.ok([0, 4, 5].includes(free.accessory));
    assert.equal(canWear(free, []), true);
    assert.equal(
      canWear(decodeAvatar(randomAvatar(rewards, () => value)), rewards),
      true,
    );
  }
  assert.equal(decodeAvatar(randomAvatar(rewards, () => 0.3)).outfit, 1);
  rewards.forEach((reward) => {
    reward.earned = false;
  });
  assert.equal(decodeAvatar(randomAvatar(rewards, () => 0.3)).outfit, 0);
});

test("new identities, starter gear and presets remain local until an explicit save", async () => {
  reset();
  const view = await mount(AvatarEditor);
  try {
    await view.click("Angular");
    await view.click("Full");
    await view.click("Braided");
    await view.click("Teal");
    await view.click("Wear: City jacket");
    await view.click("Wear: Round glasses");
    assert.equal(sent.length, 0);
    assert.ok(view.element.querySelector('image[href="faces-expanded.png"]'));
    assert.ok(view.element.querySelector('image[href="hair-expanded.png"]'));
    assert.ok(view.element.querySelector('image[href="outfits-starters.png"]'));
    assert.ok(
      view.element.querySelector('image[href="accessories-starters.png"]'),
    );
    await view.click("Save avatar");
    assert.deepEqual(sent, [
      { action: "save-avatar", avatarCode: "v2:2:7:5:5:4:1:2" },
    ]);
    session.state.profile.avatarCode = sent[0].avatarCode;
    await view.render();
    await view.click("Choose a starting look");
    await view.click("Fresh start");
    await view.tab("Identity");
    assert.equal(
      view.element.querySelector('image[href="hair-expanded.png"]'),
      null,
    );
    assert.equal(view.element.querySelector('image[href="hair.png"]'), null);
    await view.click("Reset avatar preview");
    assert.ok(
      view.element.querySelector('[aria-label="Selected: Face: Angular"]'),
    );
    assert.ok(
      view.element.querySelector('[aria-label="Selected: Build: Full"]'),
    );
    session.offline = true;
    await view.render();
    await view.click("Surprise me");
    await view.click("Save avatar");
    assert.equal(sent.length, 1);
    assert.match(view.element.textContent, /Reconnect to save/);
  } finally {
    await view.close();
  }
});

test("wardrobe controls independently change painted layers without saving previews", async () => {
  reset();
  const view = await mount(AvatarEditor);
  const region = (asset) =>
    view.element
      .querySelector(`image[href="${asset}.png"]`)
      .parentElement.getAttribute("transform");
  try {
    const starter = {
      face: region("faces"),
      hair: region("hair"),
      outfit: region("outfits"),
    };
    await view.click("Tone 6");
    assert.notEqual(region("faces"), starter.face);
    assert.equal(region("hair"), starter.hair);
    assert.equal(region("outfits"), starter.outfit);
    const complexion = region("faces");
    await view.click("Long");
    assert.notEqual(region("hair"), starter.hair);
    assert.equal(region("faces"), complexion);
    const color = view.element
      .querySelector("feColorMatrix")
      .getAttribute("values");
    await view.click("Silver");
    assert.notEqual(
      view.element.querySelector("feColorMatrix").getAttribute("values"),
      color,
    );
    assert.equal(region("faces"), complexion);
    await view.click("Preview locked: Scholar’s coat");
    assert.notEqual(region("outfits"), starter.outfit);
    await view.click("Preview locked: Explorer’s satchel");
    assert.ok(view.element.querySelector('image[href="accessories.png"]'));
    assert.equal(sent.length, 0);
    await view.click("Reset avatar preview");
    assert.equal(region("faces"), starter.face);
    assert.equal(region("hair"), starter.hair);
    assert.equal(region("outfits"), starter.outfit);
    assert.equal(
      view.element.querySelector('image[href="accessories.png"]'),
      null,
    );
  } finally {
    await view.close();
  }
});

test("multiple avatar previews keep independent color filters and clip references", async () => {
  reset();
  const view = await mount(() =>
    React.createElement(
      React.Fragment,
      null,
      React.createElement(AvatarFigure, { code: "v1:0:0:0:0:0" }),
      React.createElement(AvatarFigure, {
        code: "v1:5:3:3:3:3",
        portrait: true,
      }),
    ),
  );
  try {
    const ids = [...view.element.querySelectorAll("[id]")].map(
      (node) => node.id,
    );
    assert.equal(new Set(ids).size, ids.length);
    const filters = [...view.element.querySelectorAll("filter")];
    assert.equal(filters.length, 2);
    assert.equal(
      view.element.querySelectorAll("svg svg").length,
      0,
      "native filters and clips must share a single SVG definition scope per avatar",
    );
    assert.notEqual(
      filters[0].textContent + filters[0].innerHTML,
      filters[1].textContent + filters[1].innerHTML,
    );
    for (const node of view.element.querySelectorAll("[filter], [clip-path]")) {
      const ref = node.getAttribute("filter") ?? node.getAttribute("clip-path");
      assert.ok(ids.includes(ref.slice(5, -1)));
    }
  } finally {
    await view.close();
  }
});

test("all four new faces save independently of skin, hair, build and free accessories", async () => {
  reset();
  const view = await mount(AvatarEditor);
  try {
    await view.click("Tone 6");
    await view.click("Teal");
    await view.click("Curls");
    await view.click("Full");
    await view.click("Wear: Round glasses");
    const hair = view.element
      .querySelector('image[href="hair.png"]')
      .parentElement.getAttribute("transform");
    const faces = ["Gentle", "Defined", "Heart", "Oval"];
    const transforms = [];
    for (let i = 0; i < faces.length; i++) {
      await view.click(faces[i]);
      const atlas = i < 2 ? "faces-east.png" : "faces-south.png";
      transforms.push(
        view.element
          .querySelector(`image[href="${atlas}"]`)
          .parentElement.getAttribute("transform"),
      );
      assert.equal(
        view.element
          .querySelector('image[href="hair.png"]')
          .parentElement.getAttribute("transform"),
        hair,
      );
      await view.click("Save avatar");
      assert.equal(sent[i].avatarCode, `v2:5:7:1:0:4:${i + 3}:2`);
      session.state.profile.avatarCode = sent[i].avatarCode;
      await view.render();
    }
    assert.equal(new Set(transforms).size, 4);
    await view.click("Choose a starting look");
    await view.click("Morning stroll");
    await view.tab("Identity");
    await view.click("Reset avatar preview");
    assert.ok(
      view.element.querySelector('[aria-label="Selected: Face: Oval"]'),
    );
    assert.equal(sent.length, 4);
  } finally {
    await view.close();
  }
});

test("creator sections preserve edits; comparison is view-only and undo restores the preceding preview", async () => {
  reset();
  session.state.profile.avatarCode = "v1:2:0:0:0:0";
  const view = await mount(AvatarEditor);
  try {
    assert.equal(view.button("Silver"), undefined);
    await view.click("Oval");
    await view.tab("Hair");
    await view.click("Silver");
    await view.click("Compare saved look");
    assert.match(view.element.textContent, /Showing saved look/);
    assert.equal(sent.length, 0);
    await view.click("Save avatar");
    assert.equal(sent[0].avatarCode, "v2:2:3:0:0:0:6:0");
    await view.click("Return to preview");
    await view.click("Undo preview edit");
    await view.tab("Identity");
    assert.ok(
      view.element.querySelector('[aria-label="Selected: Face: Oval"]'),
    );
    await view.click("Save avatar");
    assert.equal(sent[1].avatarCode, "v2:2:0:0:0:0:6:0");
    await view.click("Undo preview edit");
    assert.match(view.element.textContent, /Your saved look/);
    assert.equal(view.button("Undo preview edit"), undefined);
  } finally {
    await view.close();
  }
});

test("randomization keeps chosen traits but cannot keep a locked or revoked cosmetic", async () => {
  reset();
  const view = await mount(AvatarEditor);
  const originalRandom = Math.random;
  Math.random = () => 0.99;
  try {
    await view.click("Gentle");
    await view.click("Keep face when randomizing");
    await view.tab("Hair");
    await view.click("Curls");
    await view.click("Keep hair style when randomizing");
    await view.click("Preview locked: Scholar’s coat");
    await view.click("Keep clothing when randomizing");
    await view.click("Surprise me");
    await view.click("Save avatar");
    const look = decodeAvatar(sent[0].avatarCode);
    assert.equal(look.face, 3);
    assert.equal(look.hairStyle, 1);
    assert.equal(look.outfit, 5);
    assert.equal(canWear(look, session.state.entitlements), true);
    assert.equal(look.skin, 5);
    session.state.entitlements[1].earned = true;
    await view.render();
    await view.click("Wear: Scholar’s coat");
    session.state.entitlements[1].earned = false;
    await view.render();
    await view.click("Surprise me");
    await view.click("Save avatar");
    assert.equal(
      canWear(decodeAvatar(sent[1].avatarCode), session.state.entitlements),
      true,
    );
    await view.click("Reset avatar preview");
    assert.equal(view.button("Undo preview edit"), undefined);
  } finally {
    Math.random = originalRandom;
    await view.close();
  }
});

test("save acknowledgement ends comparison when the preview becomes the saved appearance", async () => {
  reset();
  const view = await mount(AvatarEditor);
  try {
    await view.click("Round");
    await view.click("Compare saved look");
    await view.click("Save avatar");
    session.state.profile.avatarCode = sent[0].avatarCode;
    await view.render();
    assert.match(view.element.textContent, /Your saved look/);
    assert.doesNotMatch(view.element.textContent, /Showing saved look/);
    assert.equal(view.button("Return to preview"), undefined);
    await view.click("Oval");
    assert.match(view.element.textContent, /Preview/);
    assert.ok(view.button("Compare saved look"));
  } finally {
    await view.close();
  }
});

test("onboarding can save the default traveler and cannot continue before acknowledgement", async () => {
  reset();
  session.state.profile.avatarCode = null;
  let continued = 0;
  const view = await mount(AvatarEditor, { onContinue: () => continued++ });
  try {
    session.queuedCount = 1;
    await view.render();
    assert.equal(
      view.button("Save avatar").getAttribute("aria-disabled"),
      "true",
    );
    await view.click("Save avatar");
    assert.equal(sent.length, 0);
    session.queuedCount = 0;
    await view.render();
    assert.equal(
      view.button("Continue with this avatar").getAttribute("aria-disabled"),
      "true",
    );
    await view.click("Save avatar");
    assert.equal(continued, 0);
    const submitted = sent.at(-1);
    session.state.profile.avatarCode = submitted.avatarCode;
    await view.render();
    await view.click("Continue with this avatar");
    assert.equal(continued, 1);
  } finally {
    await view.close();
  }
});

test("face zoom changes only the view and keeps the selected appearance", async () => {
  reset();
  const view = await mount(AvatarEditor);
  try {
    await view.click("Oval");
    await view.click("Zoom in on face");
    assert.equal(
      view.element.querySelector("svg").getAttribute("viewBox"),
      "85 -20 230 240",
    );
    assert.equal(sent.length, 0);
    await view.click("Show full traveler");
    assert.equal(
      view.element.querySelector("svg").getAttribute("viewBox"),
      "0 -20 400 840",
    );
    await view.click("Save avatar");
    assert.equal(decodeAvatar(sent[0].avatarCode).face, 6);
  } finally {
    await view.close();
  }
});
