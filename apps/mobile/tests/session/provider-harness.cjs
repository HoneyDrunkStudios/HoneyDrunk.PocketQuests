// Runs the real provider's async workflows with deterministic hooks, transport and
// durable-storage adapters. This is not a native renderer or a SecureStore test.
const { createRequire } = require("node:module");
const Module = require("node:module");
const fs = require("node:fs");
const ts = require("typescript");
const localRequire = createRequire(__filename);
const originalLoad = Module._load;
let active;
const realReact = process.env.PQ_REAL_REACT === "1" ? require("react") : null;
const realJsx = realReact ? require("react/jsx-runtime") : null;
const wire = require("../../../../contracts/wire-fixtures.json");
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
  useMemo(factory) {
    return factory();
  },
  useSyncExternalStore(_subscribe, getSnapshot) {
    return getSnapshot();
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
  if (name === "react") return realReact ?? { ...hooks, default: hooks };
  if (name === "react/jsx-runtime")
    return realJsx ?? { jsx: (type, props) => ({ type, props }) };
  if (name === "react-native")
    return {
      Platform: {
        get OS() {
          return active?.platform ?? "android";
        },
      },
      AppState: {
        addEventListener: (_event, listener) => {
          const host = active;
          host.appListeners ??= new Set();
          host.appListeners.add(listener);
          return {
            remove() {
              host.appListeners.delete(listener);
            },
          };
        },
      },
    };
  if (name === "expo-crypto")
    return { randomUUID: () => `${active.boot}-${++active.sequence}` };
  if (name === "expo")
    return { requireOptionalNativeModule: () => active?.nativeModule ?? null };
  if (name === "./storage")
    return {
      sessionStorage: {
        load: async () => active.savedSession,
        save: async (value) => {
          active.savedSession = value;
          active.storageEvents?.push({ kind: "session", value });
        },
        loadCleanupOwner: async () => {
          if (active.failCleanupRead)
            throw new Error("Private cleanup read failed");
          return active.cleanupOwner ?? null;
        },
        saveCleanupOwner: (value) => {
          const host = active;
          const next = (host.cleanupWrites ?? Promise.resolve())
            .catch(() => {})
            .then(async () => {
              if (host.beforeCleanupSave) await host.beforeCleanupSave(value);
              if (host.failCleanupSave)
                throw new Error("Private cleanup write failed");
              host.cleanupOwner = value;
              host.storageEvents?.push({ kind: "cleanup-owner", value });
            });
          host.cleanupWrites = next;
          return next;
        },
        loadPending: async () => null,
        savePending: async () => {},
      },
    };
  if (name === "./offline-store")
    return {
      cacheRecoveryNotice: (owner) =>
        active.recoveryCopies
          ? active.hiddenNotices?.has(owner)
            ? null
            : (active.recoveryCopies[owner] ?? null)
          : (active.cacheNotice ?? null),
      discardCacheRecovery: async (owner) => {
        if (active.failDiscardRecovery)
          throw new Error("Recovery discard failed");
        if (active.recoveryCopies) delete active.recoveryCopies[owner];
        else active.cacheNotice = null;
        active.hiddenNotices?.delete(owner);
      },
      loadAccount: async (owner) => {
        if (active.beforeLoad) await active.beforeLoad(owner);
        if (active.failLoad) throw new Error("Private storage read failed");
        return structuredClone(
          active.accounts ? (active.accounts[owner] ?? null) : active.disk,
        );
      },
      saveAccount: async (value) => {
        if (active.failSave) throw new Error("Private storage write failed");
        if (active.beforeSave) await active.beforeSave();
        active.disk = JSON.parse(JSON.stringify(value));
        if (active.accounts)
          active.accounts[value.userId] = structuredClone(active.disk);
        if (active.failSaveAfterCommit)
          throw new Error("Private storage response lost after commit");
      },
      clearAccount: async (owner) => {
        if (active.failClear) {
          if (active.hiddenNotices) active.hiddenNotices.add(owner);
          throw new Error("Private storage cleanup failed");
        }
        if (active.accounts) delete active.accounts[owner];
        if (!active.accounts || active.disk?.userId === owner)
          active.disk = null;
        if (active.recoveryCopies) delete active.recoveryCopies[owner];
        else active.cacheNotice = null;
        active.hiddenNotices?.delete(owner);
      },
      deviceId: async () => "device",
    };
  if (name.endsWith("/export-download")) return { saveExport: async () => {} };
  if (name.endsWith("/notifications"))
    return {
      syncWarnings: async (state) => {
        const host = active;
        host.warningCalls ??= [];
        host.warningCalls.push(state);
        return host.syncWarnings ? host.syncWarnings(state) : "off";
      },
    };
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
const { SessionProvider, useSession, useAccountSnapshot, useSessionActions } =
  localRequire("../../src/session/session.tsx");
const { RequestError } = localRequire("../../src/session/request-error.ts");
const { createClockSource } = localRequire("../../src/session/clock-source.ts");

function account(queue = []) {
  return {
    userId: "owner",
    catalog: {
      quests: [],
      categories: [],
      attributes: [],
      skills: [],
      rules: [],
    },
    state: {
      ...structuredClone(wire.state),
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
function createHarness(
  disk = account(),
  boot = "process",
  clockOverrides = {},
) {
  const host = {
    disk: structuredClone(disk),
    boot,
    sequence: 0,
    cursor: 0,
    slots: [],
    effects: [],
    savedSession: { userId: "owner", token: "test-token" },
    sent: [],
    requests: [],
    online: true,
    state: structuredClone(disk.state),
    clockEpoch: "7",
    clockMs:
      (disk.anchor?.clock?.elapsedMilliseconds ?? 1000000) +
      (disk.anchor?.lastElapsedMilliseconds ?? 0),
    wallMs:
      Date.parse(disk.anchor?.deviceUtc ?? "2026-10-04T12:00:00.000Z") +
      (disk.anchor?.lastElapsedMilliseconds ?? 0),
    ...clockOverrides,
  };
  host.clockSource = {
    read: () => ({
      observedUtc: new Date(host.wallMs).toISOString(),
      sample:
        host.clockEpoch === null
          ? null
          : {
              kind: "android-elapsed-realtime-v1",
              epoch: host.clockEpoch,
              elapsedMilliseconds: host.clockMs,
              deviceUtc: new Date(host.wallMs).toISOString(),
            },
      reason:
        host.clockEpoch === null
          ? "Device timing is unavailable; pending timing verification."
          : undefined,
    }),
  };
  host.advance = (milliseconds) => {
    host.clockMs += milliseconds;
    host.wallMs += milliseconds;
  };
  active = host;
  global.fetch = async (url, options = {}) => {
    if (!host.online) throw new TypeError("Network unavailable");
    const body = options.body && JSON.parse(options.body);
    host.requests.push({
      url,
      headers: options.headers,
      body: structuredClone(body),
    });
    if (host.respond) {
      const response = await host.respond(url, options, body);
      if (response) return response;
    }
    let value;
    if (url.endsWith("/users/me"))
      value = { userId: host.identityUser ?? "owner", state: "Active" };
    else if (url.endsWith("/api/catalog"))
      value = host.disk?.catalog ?? account().catalog;
    else if (url.endsWith("/api/sync-anchor"))
      value = {
        id: `anchor-${host.sequence}`,
        ...body,
        serverUtc: body.deviceUtc,
        recordedTimeFloor: null,
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
    let element = SessionProvider({
      children: null,
      clockSource: host.clockSource,
    });
    const value = {};
    while (element?.props?.value) {
      Object.assign(value, element.props.value);
      element = element.props.children;
    }
    return value;
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
module.exports = {
  account,
  createHarness,
  RequestError,
  createClockSource,
  SessionProvider,
  useSession,
  useAccountSnapshot,
  useSessionActions,
  activate: (host) => {
    active = host;
  },
};
