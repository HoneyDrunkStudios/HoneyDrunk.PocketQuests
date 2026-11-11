import "./artwork-fixture.mjs";
import test, { mock } from "node:test";
import assert from "node:assert/strict";
import Module, { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { JSDOM } = require("jsdom");
const dom = new JSDOM("<!doctype html><html><body></body></html>");
global.window = dom.window;
global.document = dom.window.document;
global.HTMLElement = dom.window.HTMLElement;
global.Node = dom.window.Node;
global.ShadowRoot = dom.window.ShadowRoot;
global.IS_REACT_ACT_ENVIRONMENT = true;
const React = require("react");
const { act } = React;
const { createRoot } = require("react-dom/client");
const nativeWeb = require("react-native-web");
const ts = require("typescript");
mock.timers.enable({ apis: ["setTimeout"] });
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
const {
  AppearanceAutosaveProvider,
  AppearanceSaveNotice,
} = require("../../src/features/profile/appearance-autosave.tsx");
const { ThemeProvider } = require("@honeydrunk/ui-native");
const {
  pocketQuestsTheme,
  pocketQuestsDarkTheme,
} = require("../../src/shared/theme.ts");
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
  outfitPreview,
  randomAvatar,
  hairNames,
  hairStyles,
  faceNames,
  buildNames,
  outfits,
  accessories,
} = require("../../src/features/profile/avatar-look.ts");
const {
  avatarSectionSlots,
  randomizeAvatarSection,
  restoreAvatarSection,
} = require("../../src/features/profile/avatar-customization.ts");

function reset() {
  sent = [];
  session = {
    signedIn: true,
    appearanceOwnerId: "traveler",
    busy: false,
    pending: false,
    offline: false,
    queuedCount: 0,
    state: {
      profile: { avatarCode: null },
      entitlements: [
        { id: "PQ-CAT-A01", name: "A Quest of My Own", earned: false },
        { id: "PQ-RANK-C", name: "Rank C", earned: false },
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
    theme: pocketQuestsTheme,
    async render() {
      await act(async () =>
        root.render(
          React.createElement(
            ThemeProvider,
            { theme: view.theme },
            React.createElement(
              AppearanceAutosaveProvider,
              null,
              React.createElement(Component, props),
              React.createElement(AppearanceSaveNotice),
            ),
          ),
        ),
      );
    },
    button(name) {
      return [
        ...element.querySelectorAll('[role="button"], [role="radio"]'),
        ...document.querySelectorAll(
          '[data-testid="room-options"] [role="button"], [data-testid="room-options"] [role="radio"]',
        ),
      ].find(
        (button) =>
          button.textContent === name ||
          button.getAttribute("aria-label") === name ||
          button.getAttribute("aria-label")?.endsWith(": " + name) ||
          (name.startsWith("Wear: ") && button.textContent === name.slice(6)),
      );
    },
    async tab(name) {
      if (
        ["Accessories", "Looks"].includes(name) &&
        view.button("More editing options")
      )
        await act(async () => view.button("More editing options").click());
      name =
        name === "Identity" ? "Body" : name === "Clothing" ? "Clothes" : name;
      const tab = [...element.querySelectorAll('[role="tab"]')].find(
        (node) => node.textContent === name,
      );
      assert.ok(tab, name);
      await act(async () => tab.click());
    },
    async click(name) {
      if (
        /^(Keep |Randomize |Restore saved |Reset avatar preview|Surprise me|Undo preview|Redo preview|Compare saved|Return to preview)/.test(
          name,
        ) &&
        view.button("More editing options")
      )
        await act(async () => view.button("More editing options").click());
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
      if (
        !view.button(name) &&
        (faceNames.includes(name) ||
          name === "Zoom in on face" ||
          name === "Show full traveler") &&
        view.button("More editing options")
      )
        await act(async () => view.button("More editing options").click());
      const button = view.button(name);
      assert.ok(button, name);
      await act(async () => button.click());
    },
    async flush() {
      await act(async () => {
        mock.timers.tick(700);
      });
    },
    async close() {
      await act(async () => root.unmount());
      element.remove();
    },
  };
  await view.render();
  return view;
}

test("rapid look edits coalesce into one quiet automatic save", async () => {
  reset();
  const view = await mount(AvatarEditor);
  try {
    await view.click("Tone 6");
    await view.click("Silver");
    await view.click("Curls");
    assert.equal(sent.length, 0);
    assert.match(view.element.textContent, /Changes save automatically/);
    assert.ok(view.element.querySelector("svg"));
    await view.click("Reset avatar preview");
    assert.equal(view.button("Save avatar"), undefined);
    await view.click("Tone 6");
    await view.flush();
    assert.deepEqual(sent, [
      { action: "save-avatar", avatarCode: "v1:5:0:0:0:0" },
    ]);
    session.state.profile.avatarCode = sent[0].avatarCode;
    await view.render();
    assert.match(view.element.textContent, /Changes save automatically/);
  } finally {
    await view.close();
  }
});

test("only earned cosmetics are shown; revocation blocks an already edited draft", async () => {
  reset();
  const view = await mount(AvatarEditor);
  try {
    await view.tab("Clothing");
    assert.doesNotMatch(
      view.element.textContent,
      /Preview locked|Scholar’s coat/,
    );
    assert.equal(sent.length, 0);
    session.state.entitlements[1].earned = true;
    await view.render();
    await view.click("Wear: Scholar’s coat");
    session.state.entitlements[1].earned = false;
    await view.render();
    assert.equal(view.button("Wear: Scholar’s coat"), undefined);
    assert.equal(view.button("Save avatar"), undefined);
    await view.flush();
    assert.equal(sent.length, 0);
    await view.click("Wear: Trail tunic");
    session.state.entitlements[1].earned = true;
    await view.render();
    await view.click("Wear: Scholar’s coat");
    await view.flush();
    assert.equal(sent[0].avatarCode, "v1:2:0:0:3:0");
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
    await view.flush();
    await view.flush();
    assert.equal(sent.length, 1);
    await act(async () => finish(new Error("unavailable")));
    assert.match(view.element.textContent, /have not saved/);
    assert.ok(
      view.element.querySelector(
        '[aria-label="Selected: Hair color: Chestnut"]',
      ),
    );
    session.offline = true;
    await view.render();
    await view.click("Silver");
    await view.flush();
    assert.equal(sent.length, 1);
    assert.match(view.element.textContent, /Reconnect to save/);
    session.offline = false;
    session.pending = true;
    await view.render();
    assert.equal(view.button("Save avatar"), undefined);
    session.pending = false;
    session.command = async (input) => sent.push(input);
    await view.render();
    await view.flush();
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
    await view.flush();
    session.queuedCount = 1;
    await view.click("Silver");
    session.state.profile.avatarCode = "v1:2:1:0:0:0";
    session.queuedCount = 0;
    await view.render();
    assert.ok(
      view.element.querySelector('[aria-label="Selected: Hair color: Silver"]'),
    );
    assert.match(view.element.textContent, /Changes save automatically/);
    session.state = null;
    session.appearanceOwnerId = null;
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
    bodyType: 0,
    extra: 0,
    scarfColor: 0,
    pose: 0,
    backdrop: 0,
    room: 0,
    upperLeft: 0,
    upperRight: 0,
    lowerLeft: 0,
    lowerRight: 0,
  };
  assert.deepEqual(decodeAvatar(encodeAvatar(look)), look);
  assert.equal(encodeAvatar(decodeAvatar("v2:5:3:2:3:3")), "v1:2:0:0:0:0");
  const outfitRewards = [null, "PQ-RANK-E", "PQ-RANK-D", "PQ-RANK-C"];
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
  assert.equal(starterLooks.length, 3);
  assert.equal(decodeAvatar(randomAvatar([], () => 0.999)).face, 6);
  for (const preset of starterLooks) {
    assert.equal(encodeAvatar(decodeAvatar(preset.code)), preset.code);
    assert.equal(canWear(decodeAvatar(preset.code), []), true);
  }
  const rewards = [
    { id: "PQ-RANK-E", earned: true },
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

test("new identities and gear save automatically; outfit presets preserve identity", async () => {
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
    assert.ok(view.element.querySelector('image[href="faces.png"]'));
    assert.ok(view.element.querySelector('image[href="hair-5.png"]'));
    assert.ok(view.element.querySelector('image[href="outfit-5.png"]'));
    assert.ok(
      view.element.querySelector('image[href="accessories-starters.png"]'),
    );
    await view.flush();
    assert.deepEqual(sent, [
      { action: "save-avatar", avatarCode: "v2:2:7:5:5:4:1:2" },
    ]);
    session.state.profile.avatarCode = sent[0].avatarCode;
    await view.render();
    await view.click("Choose a starting look");
    await view.click("Trail tunic");
    await view.tab("Identity");
    assert.ok(view.element.querySelector('image[href="hair-5.png"]'));
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
    await view.flush();
    assert.equal(sent.length, 1);
    assert.match(view.element.textContent, /Reconnect to save/);
  } finally {
    await view.close();
  }
});

test("wardrobe swaps independent kit parts while skin changes only palette", async () => {
  reset();
  const view = await mount(AvatarEditor);
  try {
    const image = (part) =>
      view.element.querySelector(`g[id$="-${part}"] image`);
    const faceCrop = image("face").parentElement.getAttribute("transform");
    const bodySource = image("body").getAttribute("href");
    const skin = () =>
      view.element
        .querySelector('filter[id*="-skin-face-"] feColorMatrix')
        .getAttribute("values");
    const initial = skin();
    await view.click("Tone 6");
    assert.notEqual(skin(), initial);
    assert.equal(
      image("face").parentElement.getAttribute("transform"),
      faceCrop,
    );
    await view.click("Long");
    assert.equal(image("hair-layer").getAttribute("href"), "hair-2.png");
    assert.equal(image("body").getAttribute("href"), bodySource);
    session.state.entitlements[1].earned = true;
    await view.render();
    await view.click("Wear: " + outfits[3].name);
    assert.equal(image("body").getAttribute("href"), "outfit-3.png");
    assert.equal(
      image("face").parentElement.getAttribute("transform"),
      faceCrop,
    );
    assert.equal(sent.length, 0);
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
    assert.equal(filters.length, 6);
    for (const filter of filters) {
      assert.equal(
        filter.getAttribute("filterUnits"),
        "objectBoundingBox",
        "web filters must cover the atlas before its placement transform",
      );
      assert.equal(filter.getAttribute("x"), "-10%");
      assert.equal(filter.getAttribute("y"), "-10%");
      assert.equal(filter.getAttribute("width"), "120%");
      assert.equal(filter.getAttribute("height"), "120%");
    }
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

test("all heads and hair use one skull registration for every skin choice", () => {
  const {
    kitFace,
    kitHair,
    avatarRig,
  } = require("../../src/features/profile/avatar-kit.ts");
  const {
    avatarKitData,
  } = require("../../src/features/profile/avatar-kit-data.ts");
  for (let face = 0; face < 7; face++) {
    const part = kitFace(face);
    assert.ok(
      Math.abs(part.placement[2] / part.region[2] - avatarRig.headScale) <
        1e-12,
    );
    assert.ok(
      Math.abs(part.placement[3] / part.region[3] - avatarRig.headScale) <
        1e-12,
    );
    assert.ok(part.placement[1] + part.placement[3] >= 180);
  }
  for (let hair = 0; hair < 7; hair++) assert.ok(kitHair(hair));
  assert.equal(kitHair(7), null);
  for (const outfits of avatarKitData.bodies)
    for (const body of outfits) {
      assert.equal(body.placement[1], avatarRig.bodyTop);
      assert.equal(body.placement[1] + body.placement[3], avatarRig.soleY);
      assert.ok(body.skinMask.length > 100);
      assert.ok(body.skinReference.every((v) => v > 40 && v < 255));
    }
});

test("every outfit build and pose retains face and hair and contains its own neck and hands", async () => {
  reset();
  const props = { code: "v1:2:0:0:0:0" };
  const view = await mount(AvatarFigure, props);
  try {
    const part = (name) =>
      view.element.querySelector(`g[id$="-${name}"] image`);
    const face = part("face").parentElement.getAttribute("transform");
    const hair = part("hair-layer").parentElement.getAttribute("transform");
    for (let pose = 0; pose < 2; pose++)
      for (let build = 0; build < 3; build++)
        for (let outfit = 0; outfit < 6; outfit++) {
          props.code = encodeAvatar({
            ...decodeAvatar(props.code),
            pose,
            build,
            outfit,
          });
          await view.render();
          assert.equal(
            part("body").getAttribute("href"),
            `outfit-${outfit}.png`,
          );
          assert.equal(
            part("face").parentElement.getAttribute("transform"),
            face,
          );
          assert.equal(
            part("hair-layer").parentElement.getAttribute("transform"),
            hair,
          );
          assert.equal(
            view.element.querySelectorAll('image[href^="hands-"]').length,
            0,
          );
          assert.equal(
            view.element.querySelectorAll('image[href^="undershirt-"]').length,
            0,
          );
        }
    assert.deepEqual(sent, []);
  } finally {
    await view.close();
  }
});

test("the starter scene switches day/night with the theme without saving an appearance", async () => {
  reset();
  const {
    TravelerBackdrop,
  } = require("../../src/features/profile/avatar-backdrop.tsx");
  const props = { avatarCode: "v1:2:0:0:0:0", width: 390, height: 356 };
  const view = await mount(TravelerBackdrop, props);
  try {
    assert.match(view.element.innerHTML, /home-scene-day-v1/);
    assert.doesNotMatch(view.element.innerHTML, /home-scene-night/);
    view.theme = pocketQuestsDarkTheme;
    await view.render();
    assert.match(view.element.innerHTML, /home-scene-night-v1/);
    assert.doesNotMatch(view.element.innerHTML, /home-scene-day-v1/);
    props.avatarCode = "v4:2:0:0:0:0:0:0:0:0:0:1";
    await view.render();
    assert.match(view.element.innerHTML, /unlockable-stages-v1/);
    assert.doesNotMatch(view.element.innerHTML, /home-scene-night/);
    view.theme = pocketQuestsTheme;
    props.avatarCode = "v1:2:0:0:0:0";
    await view.render();
    assert.match(view.element.innerHTML, /home-scene-day-v1/);
    assert.deepEqual(sent, []);
  } finally {
    await view.close();
  }
});

test("changing face never refits or replaces the shared hairstyle", async () => {
  reset();
  const props = { code: "v1:2:0:0:0:0" };
  const view = await mount(AvatarFigure, props);
  try {
    let fixed;
    for (let face = 0; face < 7; face++) {
      props.code = encodeAvatar({ ...decodeAvatar(props.code), face });
      await view.render();
      const hair = view.element.querySelector('g[id$="-hair-layer"] image');
      const placement = hair.parentElement.getAttribute("transform");
      fixed ??= placement;
      assert.equal(placement, fixed);
      assert.equal(hair.getAttribute("href"), "hair-0.png");
    }
  } finally {
    await view.close();
  }
});

test("browser hair coloring preserves the palette's sRGB color values", async () => {
  reset();
  const view = await mount(AvatarFigure, { code: "v1:2:1:0:0:0" });
  try {
    assert.equal(
      view.element
        .querySelector("filter")
        .getAttribute("color-interpolation-filters"),
      "sRGB",
    );
  } finally {
    await view.close();
  }
});

test("every hair color preserves visible root-to-highlight contrast and transparency", () => {
  const {
    hairColorMatrix,
  } = require("../../src/features/profile/avatar-art.ts");
  for (let color = 0; color < hairNames.length; color++) {
    const matrix = hairColorMatrix(color);
    assert.equal(matrix.length, 20);
    assert.ok(matrix.every(Number.isFinite));
    const luminance = (gray) => {
      const rgb = [0, 1, 2].map((row) =>
        Math.min(
          1,
          Math.max(0, matrix[row * 5 + row] * gray + matrix[row * 5 + 4]),
        ),
      );
      return rgb[0] * 0.2126 + rgb[1] * 0.7152 + rgb[2] * 0.0722;
    };
    assert.ok(
      luminance(0.85) - luminance(0.15) > 0.3,
      `${hairNames[color]} must retain painted strand detail rather than flatten it`,
    );
    assert.deepEqual(matrix.slice(15), [0, 0, 0, 1, 0]);
  }
});

test("hair and scarf filters cover the canvas independently of transformed atlas bounds", async () => {
  reset();
  const platform = nativeWeb.Platform.OS;
  nativeWeb.Platform.OS = "android";
  const view = await mount(() =>
    React.createElement(AvatarFigure, {
      code: "v3:2:1:4:1:1:2:1:2:1",
      portrait: true,
    }),
  );
  try {
    const filters = [...view.element.querySelectorAll("filter")];
    assert.equal(filters.length, 4);
    for (const filter of filters) {
      assert.equal(filter.getAttribute("filterUnits"), "userSpaceOnUse");
      assert.equal(filter.getAttribute("x"), "-10%");
      assert.equal(filter.getAttribute("y"), "-10%");
      assert.equal(filter.getAttribute("width"), "120%");
      assert.equal(filter.getAttribute("height"), "120%");
      const matrix = filter
        .querySelector("feColorMatrix")
        .getAttribute("values")
        .trim()
        .split(/[\s,]+/)
        .map(Number);
      assert.equal(matrix.length, 20);
      assert.deepEqual(
        matrix.slice(15),
        [0, 0, 0, 1, 0],
        "transparent artwork must retain its alpha",
      );
    }
  } finally {
    await view.close();
    nativeWeb.Platform.OS = platform;
  }
});

test("Android swaps source artwork and invalidates palette definitions without nested SVG scopes", async () => {
  reset();
  const platform = nativeWeb.Platform.OS;
  nativeWeb.Platform.OS = "android";
  const props = { code: "v1:2:0:0:0:0" };
  const view = await mount(AvatarFigure, props);
  try {
    const hair = () => view.element.querySelector('g[id$="-hair-layer"] image');
    const filter = hair().getAttribute("filter");
    props.code = "v2:5:3:4:1:4:2:1";
    await view.render();
    assert.equal(hair().getAttribute("href"), "hair-4.png");
    assert.notEqual(hair().getAttribute("filter"), filter);
    assert.equal(
      view.element.querySelector("filter").getAttribute("filterUnits"),
      "userSpaceOnUse",
    );
    assert.equal(view.element.querySelectorAll("svg svg").length, 0);
    assert.deepEqual(sent, []);
  } finally {
    await view.close();
    nativeWeb.Platform.OS = platform;
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
      .querySelector('image[href="hair-1.png"]')
      .parentElement.getAttribute("clip-path");
    const faces = ["Gentle", "Defined", "Heart", "Oval"];
    const transforms = [];
    for (let i = 0; i < faces.length; i++) {
      await view.click(faces[i]);
      const atlas = "faces.png";
      transforms.push(
        view.element
          .querySelector(`image[href="${atlas}"]`)
          .parentElement.getAttribute("clip-path"),
      );
      assert.equal(
        view.element
          .querySelector('image[href="hair-1.png"]')
          .parentElement.getAttribute("clip-path"),
        hair,
      );
      await view.flush();
      assert.equal(sent[i].avatarCode, `v2:5:7:1:0:4:${i + 3}:2`);
      session.state.profile.avatarCode = sent[i].avatarCode;
      await view.render();
    }
    assert.equal(new Set(transforms).size, 4);
    await view.click("Choose a starting look");
    await view.click("City jacket");
    await view.tab("Identity");
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
    await view.flush();
    assert.equal(sent[0].avatarCode, "v2:2:3:0:0:0:6:0");
    await view.click("Return to preview");
    await view.click("Undo preview edit");
    await view.tab("Identity");
    assert.ok(
      view.element.querySelector('[aria-label="Selected: Face: Oval"]'),
    );
    await view.flush();
    assert.equal(sent[1].avatarCode, "v2:2:0:0:0:0:6:0");
    await view.click("Undo preview edit");
    assert.match(view.element.textContent, /Changes save automatically/);
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
    session.state.entitlements[1].earned = true;
    await view.render();
    await view.click("Wear: Scholar’s coat");
    await view.click("Keep clothing when randomizing");
    session.state.entitlements[1].earned = false;
    await view.render();
    await view.click("Surprise me");
    await view.flush();
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
    await view.flush();
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
    await view.flush();
    session.state.profile.avatarCode = sent[0].avatarCode;
    await view.render();
    assert.match(view.element.textContent, /Changes save automatically/);
    assert.doesNotMatch(view.element.textContent, /Showing saved look/);
    assert.equal(view.button("Return to preview"), undefined);
    await view.click("Oval");
    assert.match(view.element.textContent, /Changes save automatically/);
    assert.ok(view.button("Compare saved look"));
  } finally {
    await view.close();
  }
});

test("onboarding Continue waits for the confirmed saved appearance", async () => {
  reset();
  let continued = 0;
  const view = await mount(AvatarEditor, {
    onContinue: () => {
      continued++;
    },
  });
  try {
    await view.click("Continue");
    await view.flush();
    assert.equal(sent.length, 1);
    assert.equal(continued, 0);
    session.state.profile.avatarCode = sent[0].avatarCode;
    await view.render();
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
      view.element
        .querySelector(
          'svg[viewBox="0 -20 400 840"], svg[viewBox="128 64 144 164"]',
        )
        .getAttribute("viewBox"),
      "128 64 144 164",
    );
    assert.equal(sent.length, 0);
    await view.click("Show full traveler");
    assert.equal(
      view.element
        .querySelector(
          'svg[viewBox="0 -20 400 840"], svg[viewBox="128 64 144 164"]',
        )
        .getAttribute("viewBox"),
      "0 -20 400 840",
    );
    await view.flush();
    assert.equal(decodeAvatar(sent[0].avatarCode).face, 6);
  } finally {
    await view.close();
  }
});

test("focused styling changes only the selected group and preserves kept traits", () => {
  const code = "v2:1:1:1:4:4:3:1";
  const before = decodeAvatar(code);
  for (const [section, slots] of Object.entries(avatarSectionSlots)) {
    const next = decodeAvatar(
      randomizeAvatarSection(code, section, [], {}, () => 0.99),
    );
    for (const slot of Object.keys(before))
      if (!slots.includes(slot))
        assert.equal(next[slot], before[slot], `${section}/${slot}`);
    assert.equal(canWear(next, []), true);
  }
  const kept = decodeAvatar(
    randomizeAvatarSection(code, "Hair", [], { hairStyle: true }, () => 0.99),
  );
  assert.equal(kept.hairStyle, before.hairStyle);
  assert.equal(kept.hairColor, 7);
  const locked = decodeAvatar(
    randomizeAvatarSection(
      "v2:1:1:1:3:4:3:1",
      "Clothing",
      [],
      { outfit: true },
      () => 0.99,
    ),
  );
  assert.equal(locked.outfit, 5);
  assert.equal(locked.face, 3);
  const unrelatedLocked = decodeAvatar(
    randomizeAvatarSection("v2:1:1:1:3:4:3:1", "Hair", [], {}, () => 0.99),
  );
  assert.equal(
    unrelatedLocked.outfit,
    3,
    "editing hair never silently changes an unrelated clothing preview",
  );
  assert.equal(canWear(unrelatedLocked, []), false);
});

test("restoring a saved group keeps every other draft choice and both inputs unchanged", () => {
  const saved = "v1:2:0:0:0:0";
  const draft = "v2:5:7:6:5:4:6:2";
  for (const [section, slots] of Object.entries(avatarSectionSlots)) {
    const restored = decodeAvatar(restoreAvatarSection(draft, saved, section));
    const expected = decodeAvatar(draft);
    for (const slot of slots) expected[slot] = decodeAvatar(saved)[slot];
    assert.deepEqual(restored, expected);
  }
  assert.equal(saved, "v1:2:0:0:0:0");
  assert.equal(draft, "v2:5:7:6:5:4:6:2");
});

test("redo restores the undone preview, while a new edit and reset clear the redo branch", async () => {
  reset();
  session.state.profile.avatarCode = "v1:2:0:0:0:0";
  const view = await mount(AvatarEditor);
  try {
    await view.click("Oval");
    await view.click("Silver");
    await view.click("Undo preview edit");
    assert.ok(view.button("Redo preview edit"));
    await view.click("Compare saved look");
    await view.click("Redo preview edit");
    assert.doesNotMatch(view.element.textContent, /Showing saved look/);
    assert.equal(view.button("Redo preview edit"), undefined);
    await view.flush();
    assert.equal(sent[0].avatarCode, "v2:2:3:0:0:0:6:0");
    await view.click("Undo preview edit");
    await view.click("Copper");
    assert.equal(view.button("Redo preview edit"), undefined);
    await view.click("Undo preview edit");
    assert.ok(view.button("Redo preview edit"));
    await view.click("Reset avatar preview");
    assert.equal(view.button("Redo preview edit"), undefined);
    assert.equal(view.button("Undo preview edit"), undefined);
  } finally {
    await view.close();
  }
});

test("focused styling previews offline, preserves identity, and restores only saved hair", async () => {
  reset();
  session.state.profile.avatarCode = "v2:1:1:1:4:4:3:1";
  session.offline = true;
  const view = await mount(AvatarEditor);
  const original = Math.random;
  Math.random = () => 0.99;
  try {
    await view.tab("Hair");
    await view.click("Randomize hair only");
    assert.ok(
      view.element.querySelector('[aria-label="Selected: Hair color: Teal"]'),
    );
    await view.click("Oval");
    await view.tab("Hair");
    await view.click("Restore saved hair");
    assert.ok(
      view.element.querySelector(
        '[aria-label="Selected: Hair color: Chestnut"]',
      ),
    );
    await view.tab("Identity");
    assert.ok(
      view.element.querySelector('[aria-label="Selected: Face: Oval"]'),
    );
    assert.equal(view.button("Save avatar"), undefined);
    assert.deepEqual(sent, []);
    session.offline = false;
    await view.render();
    await view.flush();
    assert.equal(sent[0].avatarCode, "v2:1:1:1:4:4:6:1");
    session.state = null;
    session.appearanceOwnerId = null;
    await view.render();
    reset();
    await view.render();
    assert.equal(view.button("Redo preview edit"), undefined);
    assert.equal(view.button("Undo preview edit"), undefined);
  } finally {
    Math.random = original;
    await view.close();
  }
});

test("layered appearances retain legacy meanings and reject unsupported extra/color ordinals", () => {
  const code = "v3:5:7:6:5:1:6:2:2:7";
  const look = decodeAvatar(code);
  assert.equal(encodeAvatar(look), code);
  assert.equal(look.extra, 2);
  assert.equal(look.scarfColor, 7);
  assert.equal(decodeAvatar("v2:5:7:6:5:1:6:2").extra, 0);
  for (const invalid of [
    "v3:5:7:6:5:1:6:2:3:7",
    "v3:5:7:6:5:1:6:2:2:8",
    code + " ",
  ])
    assert.equal(encodeAvatar(decodeAvatar(invalid)), "v1:2:0:0:0:0");
});

test("extra scarf/glasses and color compose with gear, coalesce into one save, and Undo retains each layer", async () => {
  reset();
  session.state.profile.avatarCode = "v2:1:1:1:4:4:3:1";
  const view = await mount(AvatarEditor);
  try {
    await view.tab("Accessories");
    await view.click("Add linen scarf");
    await view.click("Ocean");
    assert.deepEqual(sent, []);
    assert.equal(view.element.querySelectorAll("filter").length, 4);
    await view.flush();
    const layered = decodeAvatar(sent[0].avatarCode);
    assert.equal(layered.accessory, 4);
    assert.equal(layered.extra, 2);
    assert.equal(layered.scarfColor, 2);
    await view.click("Undo preview edit");
    await view.flush();
    assert.equal(decodeAvatar(sent[1].avatarCode).extra, 2);
    assert.equal(decodeAvatar(sent[1].avatarCode).scarfColor, 0);
    session.state.entitlements[2].earned = true;
    await view.render();
    await view.click("Wear: Explorer’s satchel");
    session.state.entitlements[2].earned = false;
    await view.render();
    assert.equal(view.button("Save avatar"), undefined);
    session.state.entitlements[2].earned = true;
    await view.render();
    await view.flush();
    assert.equal(decodeAvatar(sent[2].avatarCode).accessory, 1);
    assert.equal(decodeAvatar(sent[2].avatarCode).extra, 2);
  } finally {
    await view.close();
  }
});

test("kit pose 0 uses integrated anatomy in every outfit and build", async () => {
  reset();
  const props = { code: "v1:2:0:0:0:0" };
  const view = await mount(AvatarFigure, props);
  try {
    for (let build = 0; build < 3; build++)
      for (let outfit = 0; outfit < 6; outfit++) {
        props.code = encodeAvatar({
          ...decodeAvatar("v2:5:7:5:0:4:6:0"),
          build,
          outfit,
          pose: 0,
        });
        await view.render();
        assert.equal(
          view.element
            .querySelector('g[id$="-body"] image')
            .getAttribute("href"),
          `outfit-${outfit}.png`,
        );
        assert.equal(
          view.element
            .querySelector('g[id$="-face"] image')
            .getAttribute("href"),
          "faces.png",
        );
        assert.equal(
          view.element
            .querySelector('g[id$="-hair-layer"] image')
            .getAttribute("href"),
          "hair-5.png",
        );
        assert.equal(
          view.element.querySelectorAll('image[href^="hands-"]').length,
          0,
        );
      }
    assert.deepEqual(sent, []);
  } finally {
    await view.close();
  }
});

test("stage codes retain identity, require exact earned rewards and reject malformed choices", () => {
  const legacy = "v3:5:7:6:5:4:6:2:2:7";
  assert.deepEqual(
    { ...decodeAvatar(legacy), pose: 0, backdrop: 0 },
    decodeAvatar(legacy),
  );
  for (const [pose, backdrop, rewardId] of [
    [1, 0, "PQ-CAT-B01"],
    [0, 1, "PQ-CAT-A01"],
    [0, 2, "PQ-CAT-F02"],
  ]) {
    const code = `v4:5:7:6:5:4:6:2:2:7:${pose}:${backdrop}`;
    const look = decodeAvatar(code);
    assert.equal(encodeAvatar(look), code);
    assert.equal(canWear(look, []), false);
    assert.equal(canWear(look, [{ id: rewardId, earned: true }]), true);
    assert.equal(canWear(look, [{ id: rewardId, earned: false }]), false);
    assert.equal(canWear(look, [{ id: "different", earned: true }]), false);
    assert.equal(
      outfitPreview(code, "v1:2:0:0:0:0").split(":").slice(-2).join(":"),
      `${pose}:${backdrop}`,
    );
  }
  for (const code of [
    "v4:5:7:6:5:4:6:2:2:7:2:0",
    "v4:5:7:6:5:4:6:2:2:7:0:3",
    "v4:5:7:6:5:4:6:2:2:7:0:0 ",
    "v4:5:7:6:5:4:6:2:2:8:0:0",
  ])
    assert.equal(encodeAvatar(decodeAvatar(code)), "v1:2:0:0:0:0");
});

test("Wardrobe hides unearned stage choices and coalesces owned pose/background changes", async () => {
  reset();
  session.state.profile.avatarCode = "v2:2:1:1:4:0:1:0";
  const view = await mount(AvatarEditor, { equipmentOnly: true });
  try {
    await view.click("Change pose, equipped: At ease");
    assert.equal(view.button("Relaxed stance"), undefined);
    await view.click("Change background, equipped: Floating islands");
    assert.equal(view.button("Golden horizon"), undefined);
    assert.equal(view.button("Moonlit sky"), undefined);
    session.state.entitlements[0].earned = true;
    session.state.entitlements[2].earned = true;
    await view.render();
    await view.click("Golden horizon");
    await view.click("Change pose, equipped: At ease");
    await view.click("Relaxed stance");
    assert.equal(
      view.element
        .querySelector('[role="radio"][aria-label="Relaxed stance"]')
        .getAttribute("aria-checked"),
      "true",
    );
    assert.equal(sent.length, 0);
    await view.flush();
    assert.deepEqual(sent, [
      { action: "save-avatar", avatarCode: "v4:2:1:1:4:0:1:0:0:0:1:1" },
    ]);
    assert.equal(view.button("Save avatar"), undefined);
  } finally {
    await view.close();
  }
});

test("kit pose 1 uses integrated anatomy in every outfit and build", async () => {
  reset();
  const props = { code: "v1:2:0:0:0:0" };
  const view = await mount(AvatarFigure, props);
  try {
    for (let build = 0; build < 3; build++)
      for (let outfit = 0; outfit < 6; outfit++) {
        props.code = encodeAvatar({
          ...decodeAvatar("v2:5:7:5:0:4:6:0"),
          build,
          outfit,
          pose: 1,
        });
        await view.render();
        assert.equal(
          view.element
            .querySelector('g[id$="-body"] image')
            .getAttribute("href"),
          `outfit-${outfit}.png`,
        );
        assert.equal(
          view.element
            .querySelector('g[id$="-face"] image')
            .getAttribute("href"),
          "faces.png",
        );
        assert.equal(
          view.element
            .querySelector('g[id$="-hair-layer"] image')
            .getAttribute("href"),
          "hair-5.png",
        );
        assert.equal(
          view.element.querySelectorAll('image[href^="hands-"]').length,
          0,
        );
      }
    assert.deepEqual(sent, []);
  } finally {
    await view.close();
  }
});

test("Wardrobe equips the free front-facing pose and saves its registered layers", async () => {
  reset();
  session.state.profile.avatarCode = "v1:2:0:0:0:0";
  const view = await mount(AvatarEditor, { equipmentOnly: true });
  try {
    await view.click("Change pose, equipped: At ease");
    await view.click("Front-facing");
    assert.equal(
      view.element.querySelector('g[id$="-body"] image').getAttribute("href"),
      "front-male-balanced.png",
    );
    assert.equal(
      view.element.querySelector('g[id$="-face"] image').getAttribute("href"),
      "front-faces.png",
    );
    assert.equal(
      view.element
        .querySelector('g[id$="-hair-layer"] image')
        .getAttribute("href"),
      "front-hair.png",
    );
    await view.flush();
    assert.deepEqual(sent, [
      { action: "save-avatar", avatarCode: "v6:200000000200:00000" },
    ]);
  } finally {
    await view.close();
  }
});

test("Female starts with Petite and body type can change independently of build", async () => {
  reset();
  const view = await mount(AvatarEditor, { onContinue: () => {} });
  try {
    await view.click("Female");
    assert.ok(view.button("Selected: Build: Petite"));
    await view.click("Broad");
    await view.click("Male");
    assert.ok(view.button("Selected: Build: Broad"));
    await view.click("Female");
    assert.ok(view.button("Selected: Build: Broad"));
    await view.flush();
    assert.equal(decodeAvatar(sent.at(-1).avatarCode).bodyType, 1);
    assert.equal(decodeAvatar(sent.at(-1).avatarCode).build, 1);
  } finally {
    await view.close();
  }
});

test("room layout round-trips without losing any avatar choice and obeys exact reward gates", () => {
  const { roomSlots } = require("../../src/features/profile/avatar-look.ts");
  const {
    randomizeAvatarSection,
  } = require("../../src/features/profile/avatar-customization.ts");
  const rewards = [
    "PQ-RANK-E",
    "PQ-LEVEL-02",
    "PQ-LEVEL-04",
    "PQ-LEVEL-05",
    "PQ-LEVEL-07",
    "PQ-CAT-B01",
    "PQ-CAT-B02",
    "PQ-CAT-F02",
  ].map((id) => ({ id, earned: true }));
  const base = decodeAvatar("v4:5:7:6:5:4:6:2:2:7:1:2");
  const look = {
    ...base,
    room: 1,
    upperLeft: 1,
    upperRight: 3,
    lowerLeft: 4,
    lowerRight: 6,
  };
  const code = encodeAvatar(look);
  assert.equal(code.length, 20);
  assert.equal(code.slice(0, 3), "v5:");
  assert.deepEqual(decodeAvatar(code), look);
  assert.equal(canWear(look, rewards), true);
  assert.equal(canWear(look, []), false);
  const edited = decodeAvatar(
    randomizeAvatarSection(code, "All", rewards, {}, () => 0.8),
  );
  for (const slot of ["room", ...roomSlots])
    assert.equal(edited[slot], look[slot]);
  for (const invalid of [
    code + " ",
    code.replace(":1", ":8"),
    "v5:20000000000:07000",
    "v5:20000000000:30000",
  ])
    assert.equal(encodeAvatar(decodeAvatar(invalid)), "v1:2:0:0:0:0");
});

test("room shows owned choices and quietly saves room and placements while preserving identity", async () => {
  reset();
  session.state.profile = {
    avatarCode: "v2:2:0:0:4:0:0:0",
    onboardingComplete: true,
  };
  const {
    TravelerRoom,
  } = require("../../src/features/profile/room-screen.tsx");
  const {
    roomStyles,
    roomDecorations,
  } = require("../../src/features/profile/avatar-look.ts");
  const view = await mount(TravelerRoom);
  try {
    await view.click("Change room");
    assert.equal(view.button(roomStyles[1].name), undefined);
    await view.click("Done with room options");
    await view.click("Decorate room");
    await view.click("Upper left shelf: Empty display");
    assert.match(
      document.querySelector('[data-testid="room-options"]').textContent,
      /Reach level 2 to earn your first room decoration/,
    );
    await view.click("Done with room options");
    session.state.entitlements.push(
      { id: "PQ-LEVEL-02", earned: true },
      { id: "PQ-RANK-E", earned: true },
    );
    await view.render();
    await view.click("Change room");
    await view.click(roomStyles[1].name);
    await view.click("Upper left shelf: Empty display");
    await view.click("Place " + roomDecorations[1].name);
    assert.deepEqual(sent, []);
    await view.flush();
    assert.equal(sent.length, 1);
    const saved = decodeAvatar(sent[0].avatarCode);
    assert.equal(saved.room, 1);
    assert.equal(saved.upperLeft, 1);
    assert.equal(saved.outfit, 4);
    assert.equal(saved.face, 0);
    session.state.profile.avatarCode = sent[0].avatarCode;
    await view.render();
    await view.click("Right display table: Empty display");
    await view.click("Place " + roomDecorations[1].name);
    await view.flush();
    const moved = decodeAvatar(sent[1].avatarCode);
    assert.equal(moved.upperLeft, 0);
    assert.equal(moved.lowerRight, 1);
    assert.equal(moved.room, 1);
  } finally {
    await view.close();
  }
});

test("resetting an avatar cannot discard a pending room change", async () => {
  reset();
  session.state.profile = {
    avatarCode: "v1:2:0:0:0:0",
    onboardingComplete: true,
  };
  session.state.entitlements.push({ id: "PQ-RANK-E", earned: true });
  const {
    TravelerRoom,
  } = require("../../src/features/profile/room-screen.tsx");
  const { roomStyles } = require("../../src/features/profile/avatar-look.ts");
  const view = await mount(() =>
    React.createElement(
      React.Fragment,
      null,
      React.createElement(TravelerRoom),
      React.createElement(AvatarEditor),
    ),
  );
  try {
    await view.click("Change room");
    await view.click(roomStyles[1].name);
    await view.click("Angular");
    await view.click("Reset avatar preview");
    await view.flush();
    assert.equal(sent.length, 1);
    const saved = decodeAvatar(sent[0].avatarCode);
    assert.equal(saved.face, 0);
    assert.equal(saved.room, 1);
  } finally {
    await view.close();
  }
});
