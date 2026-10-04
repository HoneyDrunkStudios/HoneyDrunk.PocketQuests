// Runs the real provider's async workflows with deterministic hooks, transport and
// durable-storage adapters. This is not a native renderer or a SecureStore test.
const { createRequire } = require("node:module");
const Module = require("node:module");
const fs = require("node:fs");
const ts = require("typescript");
const localRequire = createRequire(__filename);
const originalLoad = Module._load;
let active;
const hooks = {
  createContext: () => ({}),
  use: (context) => context,
  useState(initial) {
    const index = active.cursor++;
    const host = active;
    if (!(index in host.slots))
      host.slots[index] = typeof initial === "function" ? initial() : initial;
    return [
      host.slots[index],
      (value) => {
        host.slots[index] =
          typeof value === "function" ? value(host.slots[index]) : value;
      },
    ];
  },
  useRef(initial) {
    return hooks.useState({ current: initial })[0];
  },
  useCallback(callback) {
    return callback;
  },
  useEffect(effect) {
    active.effects.push(effect);
  },
};
Module._load = function (name, ...rest) {
  if (name === "react") return { ...hooks, default: hooks };
  if (name === "react/jsx-runtime")
    return { jsx: (type, props) => ({ type, props }) };
  if (name === "react-native")
    return {
      Platform: { OS: "android" },
      AppState: { addEventListener: () => ({ remove() {} }) },
    };
  if (name === "expo-crypto")
    return { randomUUID: () => `${active.boot}-${++active.sequence}` };
  if (name === "./storage")
    return {
      sessionStorage: {
        load: async () => active.savedSession,
        save: async (value) => {
          active.savedSession = value;
        },
        loadPending: async () => null,
        savePending: async () => {},
      },
    };
  if (name === "./offline-store")
    return {
      loadAccount: async () => structuredClone(active.disk),
      saveAccount: async (value) => {
        if (active.failSave) throw new Error("Private storage write failed");
        active.disk = structuredClone(value);
      },
      clearAccount: async () => {
        active.disk = null;
      },
      deviceId: async () => "device",
    };
  if (name.endsWith("/export-download")) return { saveExport: async () => {} };
  if (name.endsWith("/notifications"))
    return { syncWarnings: async () => "off" };
  return originalLoad.call(this, name, ...rest);
};
for (const extension of [".ts", ".tsx"])
  require.extensions[extension] = (module, file) =>
    module._compile(
      ts.transpileModule(fs.readFileSync(file, "utf8"), {
        compilerOptions: {
          module: ts.ModuleKind.CommonJS,
          jsx: ts.JsxEmit.ReactJSX,
          target: ts.ScriptTarget.ES2022,
        },
      }).outputText,
      file,
    );
const { SessionProvider } = localRequire("../../src/session/session.tsx");
const { RequestError } = localRequire("../../src/session/request-error.ts");

function account(queue = []) {
  return {
    userId: "owner",
    catalog: { quests: [], categories: [], attributes: [], skills: [] },
    state: {
      definitions: [],
      occurrences: [],
      overallXp: 0,
      categories: [],
      attributes: [],
      skills: [],
    },
    queue,
    anchor: null,
  };
}
function createHarness(disk = account(), boot = "process") {
  const host = {
    disk: structuredClone(disk),
    boot,
    sequence: 0,
    cursor: 0,
    slots: [],
    effects: [],
    savedSession: { userId: "owner", token: "test-token" },
    sent: [],
    online: true,
    state: structuredClone(disk.state),
  };
  active = host;
  global.fetch = async (url, options = {}) => {
    if (!host.online) throw new TypeError("Network unavailable");
    const body = options.body && JSON.parse(options.body);
    let value;
    if (url.endsWith("/users/me")) value = { userId: "owner", state: "Active" };
    else if (url.endsWith("/api/catalog")) value = host.disk.catalog;
    else if (url.endsWith("/api/sync-anchor"))
      value = {
        id: `anchor-${host.sequence}`,
        ...body,
        serverUtc: body.deviceUtc,
      };
    else if (url.endsWith("/api/commands")) {
      host.sent.push(structuredClone(body));
      try {
        value = host.execute ? await host.execute(body) : host.state;
      } catch (error) {
        if (!(error instanceof RequestError)) throw error;
        return {
          ok: false,
          status: error.status,
          json: async () => ({
            detail: error.message,
            retryable: error.retryable,
          }),
        };
      }
    } else value = host.state;
    return { ok: true, json: async () => structuredClone(value) };
  };
  host.render = () => {
    active = host;
    host.cursor = 0;
    host.effects = [];
    return SessionProvider({ children: null }).props.value;
  };
  host.mount = async () => {
    host.render();
    host.effects[0]();
    for (let i = 0; i < 30; i++)
      await new Promise((resolve) => setImmediate(resolve));
    return host.render();
  };
  return host;
}
module.exports = { account, createHarness, RequestError };
