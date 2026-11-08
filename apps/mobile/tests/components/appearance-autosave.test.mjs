import test, { mock } from "node:test";
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
const React = require("react"),
  { act } = React;
const { createRoot } = require("react-dom/client");
const nativeWeb = require("react-native-web"),
  ts = require("typescript");
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
mock.timers.enable({ apis: ["setTimeout"] });
let session, sent, probe, root, page;
const load = Module._load;
Module._load = function (name, ...rest) {
  if (name === "react-native") return nativeWeb;
  if (name.endsWith("/session"))
    return {
      useAccountSnapshot: () => session,
      useSessionStatus: () => session,
      useSessionActions: () => session,
    };
  return load.call(this, name, ...rest);
};
const {
  AppearanceAutosaveProvider,
  useAppearanceAutosave,
} = require("../../src/features/profile/appearance-autosave.tsx");
function Probe() {
  probe = useAppearanceAutosave();
  return React.createElement("div", null, page);
}
function render() {
  root.render(
    React.createElement(
      AppearanceAutosaveProvider,
      null,
      React.createElement(Probe),
    ),
  );
}
async function mount() {
  sent = [];
  page = "wardrobe";
  session = {
    appearanceOwnerId: "one",
    signedIn: true,
    offline: false,
    busy: false,
    pending: false,
    queuedCount: 0,
    state: {
      profile: {
        avatarCode: "v1:2:0:0:0:0",
        badgeId: null,
        frameId: null,
        onboardingComplete: true,
      },
      entitlements: [],
    },
    command: async (input) => {
      sent.push(input);
    },
  };
  const element = document.createElement("div");
  document.body.append(element);
  root = createRoot(element);
  await act(async () => render());
  return {
    async render() {
      await act(async () => render());
    },
    async edit(code) {
      await act(async () => probe.changeAvatar(code));
    },
    async flush() {
      await act(async () => mock.timers.tick(700));
    },
    async ack(field, value) {
      session.state = {
        ...session.state,
        profile: { ...session.state.profile, [field]: value },
      };
      await act(async () => render());
    },
    async close() {
      await act(async () => root.unmount());
      element.remove();
    },
  };
}
const deferred = () => {
  let resolve, reject;
  const promise = new Promise((yes, no) => {
    resolve = yes;
    reject = no;
  });
  return { promise, resolve, reject };
};
test("rapid taps coalesce and navigation retains the latest choice without loading feedback", async () => {
  const v = await mount();
  try {
    await v.edit("v1:3:0:0:0:0");
    await v.edit("v1:4:0:0:0:0");
    page = "home";
    await v.render();
    assert.equal(probe.previewAvatar, "v1:4:0:0:0:0");
    assert.equal(probe.notice, "");
    assert.equal(sent.length, 0);
    await v.flush();
    assert.deepEqual(sent, [
      { action: "save-avatar", avatarCode: "v1:4:0:0:0:0" },
    ]);
    await v.ack("avatarCode", sent[0].avatarCode);
    assert.equal(probe.notice, "");
    await v.flush();
    assert.equal(sent.length, 1);
    await v.ack("avatarCode", "v1:1:0:0:0:0");
    assert.equal(probe.avatarCode, "v1:1:0:0:0:0");
    assert.equal(probe.previewAvatar, undefined);
  } finally {
    await v.close();
  }
});
test("an edit during a delayed save is preserved and sent after the first acknowledgement", async () => {
  const v = await mount();
  try {
    const first = deferred();
    session.command = (input) => {
      sent.push(input);
      return sent.length === 1 ? first.promise : Promise.resolve();
    };
    await v.render();
    await v.edit("v1:3:0:0:0:0");
    await v.flush();
    await v.edit("v1:5:0:0:0:0");
    await v.flush();
    assert.equal(sent.length, 1);
    await v.ack("avatarCode", sent[0].avatarCode);
    assert.equal(probe.avatarCode, "v1:5:0:0:0:0");
    await act(async () => first.resolve());
    await v.flush();
    assert.equal(sent[1].avatarCode, "v1:5:0:0:0:0");
  } finally {
    await v.close();
  }
});
test("offline and queued commands retain cosmetic drafts until safe to send", async () => {
  const v = await mount();
  try {
    session.offline = true;
    await v.render();
    await v.edit("v1:5:0:0:0:0");
    await v.flush();
    assert.equal(sent.length, 0);
    assert.match(probe.notice, /Reconnect/);
    session.offline = false;
    session.queuedCount = 1;
    await v.render();
    await v.flush();
    assert.equal(sent.length, 0);
    session.queuedCount = 0;
    session.pending = true;
    await v.render();
    await v.flush();
    assert.equal(sent.length, 0);
    session.pending = false;
    await v.render();
    await v.flush();
    assert.equal(sent.length, 1);
  } finally {
    await v.close();
  }
});
test("a failed or unacknowledged save shows retry and never loops automatically", async () => {
  const v = await mount();
  try {
    session.command = async (input) => {
      sent.push(input);
      throw Error("offline");
    };
    await v.render();
    await v.edit("v1:5:0:0:0:0");
    await v.flush();
    assert.equal(probe.failed, true);
    await v.flush();
    assert.equal(sent.length, 1);
    session.command = async (input) => {
      sent.push(input);
    };
    await v.render();
    await act(async () => probe.retry());
    await v.flush();
    assert.equal(sent.length, 2);
    assert.equal(probe.failed, true);
    await v.ack("avatarCode", sent[1].avatarCode);
    assert.equal(probe.notice, "");
  } finally {
    await v.close();
  }
});
test("switching accounts cancels the old timer and ignores an old flight's failure", async () => {
  const v = await mount();
  try {
    await v.edit("v1:5:0:0:0:0");
    session.appearanceOwnerId = "two";
    await v.render();
    await v.flush();
    assert.equal(sent.length, 0);
    assert.equal(probe.avatarCode, "v1:2:0:0:0:0");
    const old = deferred();
    session.command = (input) => {
      sent.push(input);
      return old.promise;
    };
    await v.render();
    await v.edit("v1:4:0:0:0:0");
    await v.flush();
    session.appearanceOwnerId = "three";
    await v.render();
    await act(async () => old.reject(Error("old")));
    assert.equal(probe.notice, "");
    assert.equal(probe.avatarCode, "v1:2:0:0:0:0");
  } finally {
    await v.close();
  }
});
test("badge and frame writes serialize with the avatar; unearned rewards never send", async () => {
  const v = await mount();
  try {
    session.state.entitlements = [
      { id: "badge", kind: "Badge", earned: true },
      { id: "frame", kind: "Frame", earned: true },
    ];
    await v.render();
    await act(async () => {
      probe.changeAvatar("v1:5:0:0:0:0");
      probe.changeReward("Badge", "badge");
      probe.changeReward("Frame", "frame");
    });
    for (const field of ["avatarCode", "badgeId", "frameId"]) {
      await v.flush();
      const input = sent.at(-1);
      await v.ack(field, input.avatarCode ?? input.rewardId);
    }
    assert.deepEqual(
      sent.map((x) => x.action),
      ["save-avatar", "select-badge", "select-frame"],
    );
    assert.equal(probe.notice, "");
    await act(async () => probe.changeReward("Badge", "locked"));
    await v.flush();
    assert.equal(sent.length, 3);
    assert.match(probe.notice, /no longer available/);
  } finally {
    await v.close();
  }
});
test("revoking clothing before debounce prevents the write", async () => {
  const v = await mount();
  try {
    session.state.entitlements = [{ id: "PQ-RANK-C", earned: true }];
    await v.render();
    await v.edit("v1:2:0:0:3:0");
    session.state.entitlements = [];
    await v.render();
    await v.flush();
    assert.equal(sent.length, 0);
    assert.match(probe.notice, /no longer available/);
  } finally {
    await v.close();
  }
});

test("returning to the original choice during a flight survives its late acknowledgement", async () => {
  const v = await mount();
  try {
    const first = deferred();
    session.command = (input) => {
      sent.push(input);
      return sent.length === 1 ? first.promise : Promise.resolve();
    };
    await v.render();
    await v.edit("v1:5:0:0:0:0");
    await v.flush();
    await v.edit("v1:2:0:0:0:0");
    await v.render();
    await v.ack("avatarCode", "v1:5:0:0:0:0");
    await act(async () => first.resolve());
    assert.equal(probe.avatarCode, "v1:2:0:0:0:0");
    await v.flush();
    assert.equal(sent[1].avatarCode, "v1:2:0:0:0:0");
  } finally {
    await v.close();
  }
});
