import test from "node:test";
import { strict as assert } from "node:assert";
import fs from "node:fs";
import Module, { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
const require = createRequire(import.meta.url);
const ts = require("typescript");
const wire = JSON.parse(
  fs.readFileSync(
    new URL("../../../../contracts/wire-fixtures.json", import.meta.url),
    "utf8",
  ),
);
const pointers = new Map();
const files = new Map();
let sequence = 0;
let writeFailure = null;
let platform = "android";
let readFailure = false;
class File {
  constructor(_root, name) {
    this.name = name;
  }
  create() {
    files.set(this.name, new Uint8Array());
  }
  write(bytes) {
    files.set(this.name, bytes.slice());
  }
  async bytes() {
    if (readFailure) throw new Error("Device storage locked");
    assert.ok(
      files.has(this.name),
      "committed ciphertext must remain readable",
    );
    return files.get(this.name);
  }
  delete() {
    files.delete(this.name);
  }
  get exists() {
    return files.has(this.name);
  }
}
const originalLoad = Module._load;
Module._load = function (name, ...rest) {
  if (name === "react-native")
    return {
      Platform: {
        get OS() {
          return platform;
        },
      },
    };
  if (name === "expo-file-system")
    return { File, Paths: { document: "private" } };
  if (name === "expo-secure-store")
    return {
      WHEN_UNLOCKED_THIS_DEVICE_ONLY: "this-device",
      getItemAsync: async (key) => pointers.get(key) ?? null,
      deleteItemAsync: async (key) => pointers.delete(key),
      setItemAsync: async (key, value) => {
        if (writeFailure === "before")
          throw new Error("Write failed before commit");
        pointers.set(key, value);
        if (writeFailure === "after")
          throw new Error("Response lost after commit");
      },
    };
  // Encryption is a transport stub here. This test exercises the production
  // file/pointer generation protocol, not native encryption or SecureStore.
  if (name === "expo-crypto")
    return {
      randomUUID: () =>
        `00000000-0000-4000-8000-${(++sequence).toString(16).padStart(12, "0")}`,
      CryptoDigestAlgorithm: { SHA256: "sha256" },
      digestStringAsync: async (_algorithm, user) => user,
      AESEncryptionKey: {
        generate: async () => ({ encoded: async () => "abcd" }),
        import: async () => ({}),
      },
      aesEncryptAsync: async (bytes) => ({ combined: async () => bytes }),
      AESSealedData: { fromCombined: (bytes) => bytes },
      aesDecryptAsync: async (sealed) => sealed,
    };
  return originalLoad.call(this, name, ...rest);
};
require.extensions[".ts"] = (module, filename) =>
  module._compile(
    ts.transpileModule(fs.readFileSync(filename, "utf8"), {
      compilerOptions: {
        module: ts.ModuleKind.CommonJS,
        target: ts.ScriptTarget.ES2022,
        esModuleInterop: true,
      },
    }).outputText,
    filename,
  );
const source = fileURLToPath(
  new URL("../../src/session/offline-store.ts", import.meta.url),
);
const compiled = new Module(source);
compiled.filename = source;
compiled.paths = Module._nodeModulePaths(source);
compiled._compile(
  ts.transpileModule(fs.readFileSync(source, "utf8"), {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022,
    },
  }).outputText,
  source,
);
const {
  saveAccount,
  loadAccount,
  deviceId,
  clearAccount,
  cacheRecoveryNotice,
} = compiled.exports;
const account = (ordinal) => ({
  userId: "owner",
  requiresReload: false,
  state: structuredClone(wire.state),
  catalog: structuredClone(wire.catalog),
  queue: [{ operationId: `op-${ordinal}`, action: "complete" }],
  unverified: [
    {
      command: { operationId: `pending-${ordinal}`, action: "undo" },
      reason: "Timing unavailable",
      observedUtc: "2026-10-05T12:00:00Z",
      clock: null,
      blockedBy: [],
    },
  ],
  anchor: { ordinal },
});

test("pointer failure after commit cannot delete the committed queue and unverified intent generation", async () => {
  writeFailure = null;
  await saveAccount(account(1));
  writeFailure = "after";
  await assert.rejects(saveAccount(account(2)), /after commit/);
  assert.deepEqual(await loadAccount("owner"), account(2));
  writeFailure = null;
  await saveAccount(account(3));
  assert.deepEqual(await loadAccount("owner"), account(3));
});

test("pointer failure before commit preserves the previous complete generation", async () => {
  writeFailure = null;
  await saveAccount(account(4));
  writeFailure = "before";
  await assert.rejects(saveAccount(account(5)), /before commit/);
  assert.deepEqual(await loadAccount("owner"), account(4));
  writeFailure = null;
});

test("web commands and anchors keep one device identity for the page lifetime", async () => {
  platform = "web";
  try {
    const before = new Map(pointers);
    const first = await deviceId();
    assert.equal(await deviceId(), first);
    assert.deepEqual(
      pointers,
      before,
      "web identity must not write native secure storage",
    );
  } finally {
    platform = "android";
  }
});

test("missing file clears the stale active pointer and permits a fresh signed-in cache", async () => {
  await saveAccount(account(6));
  const pointer = pointers.get("pq.cache.owner");
  files.delete(JSON.parse(pointer).filename);
  assert.equal(await loadAccount("owner"), null);
  assert.equal(pointers.has("pq.cache.owner"), false);
  assert.ok(cacheRecoveryNotice("owner"));
  await saveAccount(account(7));
  assert.deepEqual(await loadAccount("owner"), account(7));
});

test("corrupt JSON preserves ciphertext and its recovery key without exposing a broken account", async () => {
  await saveAccount(account(8));
  const pointer = pointers.get("pq.cache.owner");
  const filename = JSON.parse(pointer).filename;
  files.set(filename, new TextEncoder().encode("{invalid"));
  assert.equal(await loadAccount("owner"), null);
  assert.equal(files.has(filename), true);
  assert.ok(
    JSON.parse(pointers.get("pq.cache.owner.recovery")).includes(pointer),
  );
  await saveAccount(account(9));
  assert.deepEqual(await loadAccount("owner"), account(9));
  assert.equal(
    files.has(filename),
    true,
    "saving online history must not delete the recovery generation",
  );
});

test("a transient file read failure preserves the valid pointer, pending actions and clock proof", async () => {
  await saveAccount(account(10));
  const pointer = pointers.get("pq.cache.owner");
  readFailure = true;
  await assert.rejects(loadAccount("owner"), /locked/);
  assert.equal(pointers.get("pq.cache.owner"), pointer);
  readFailure = false;
  assert.deepEqual(await loadAccount("owner"), account(10));
});

test("malformed pointer cannot select another file and explicit logout revokes recovery keys", async () => {
  const outside = "../private.json";
  files.set(outside, new Uint8Array([1]));
  pointers.set(
    "pq.cache.owner",
    JSON.stringify({ filename: outside, key: "abcd" }),
  );
  assert.equal(await loadAccount("owner"), null);
  assert.equal(files.has(outside), true);
  await clearAccount("owner");
  assert.equal(pointers.has("pq.cache.owner.recovery"), false);
  assert.equal(cacheRecoveryNotice("owner"), null);
  assert.equal(files.has(outside), true);
});

test("a damaged display snapshot preserves its readable pending journal for online reloading", async () => {
  const cached = account(11);
  delete cached.state;
  await saveAccount(cached);
  const loaded = await loadAccount("owner");
  assert.equal(loaded.requiresReload, true);
  assert.deepEqual(loaded.queue, cached.queue);
  assert.deepEqual(loaded.unverified, cached.unverified);
  assert.deepEqual(loaded.anchor, cached.anchor);
  assert.ok(pointers.has("pq.cache.owner"));
});

test("cleanup cannot delete an unrelated file even before a malformed pointer is loaded", async () => {
  const outside = "../private.json";
  files.set(outside, new Uint8Array([1]));
  const pointer = JSON.stringify({ filename: outside, key: "abcd" });
  pointers.set("pq.cache.owner", pointer);
  await saveAccount(account(12));
  assert.equal(files.has(outside), true);
  pointers.set("pq.cache.owner", pointer);
  await clearAccount("owner");
  assert.equal(files.has(outside), true);
});

test("damaged recovery metadata is retained alongside a newly quarantined pointer", async () => {
  await saveAccount(account(13));
  const pointer = pointers.get("pq.cache.owner");
  files.delete(JSON.parse(pointer).filename);
  pointers.set("pq.cache.owner.recovery", "{damaged-recovery-metadata");
  assert.equal(await loadAccount("owner"), null);
  assert.deepEqual(JSON.parse(pointers.get("pq.cache.owner.recovery")), [
    "{damaged-recovery-metadata",
    pointer,
  ]);
});

test("incomplete cached display is withheld without losing a readable pending journal", async () => {
  const saved = {
    ...account(42),
    state: structuredClone(wire.state),
    catalog: structuredClone(wire.catalog),
  };
  delete saved.state.categories;
  await saveAccount(saved);
  const loaded = await loadAccount(saved.userId);
  assert.equal(loaded.requiresReload, true);
  assert.deepEqual(loaded.queue, saved.queue);
  assert.deepEqual(loaded.unverified, saved.unverified);
});

test("nested cache shape failures withhold display and preserve every readable action", async () => {
  for (const corrupt of [
    (saved) => delete saved.state.profile.interests,
    (saved) => delete saved.state.schedule.series,
    (saved) => {
      saved.state.categories[0].xp = "invalid";
    },
    (saved) => delete saved.catalog.quests[0].attributes,
  ]) {
    const saved = account(43);
    corrupt(saved);
    await saveAccount(saved);
    const loaded = await loadAccount("owner");
    assert.equal(loaded.requiresReload, true);
    assert.equal(loaded.state, null);
    assert.equal(loaded.catalog, null);
    assert.deepEqual(loaded.queue, saved.queue);
    assert.deepEqual(loaded.unverified, saved.unverified);
  }
});

test("malformed recovery metadata retains its ciphertext and journal privately instead of crashing recovery UI", async () => {
  const saved = account(45);
  saved.unverified[0].blockedBy = { invalid: true };
  await saveAccount(saved);
  const pointer = pointers.get("pq.cache.owner"),
    filename = JSON.parse(pointer).filename;
  assert.equal(await loadAccount("owner"), null);
  assert.deepEqual(
    JSON.parse(new TextDecoder().decode(files.get(filename))).account.queue,
    saved.queue,
  );
  assert.ok(
    JSON.parse(pointers.get("pq.cache.owner.recovery")).includes(pointer),
  );
});

test("cache envelopes are versioned, legacy flat data remains readable and future versions are retained privately", async () => {
  const saved = account(44);
  await saveAccount(saved);
  const pointer = pointers.get("pq.cache.owner"),
    filename = JSON.parse(pointer).filename;
  const envelope = JSON.parse(new TextDecoder().decode(files.get(filename)));
  assert.equal(envelope.version, 1);
  assert.deepEqual(envelope.account, saved);
  files.set(filename, new TextEncoder().encode(JSON.stringify(saved)));
  assert.deepEqual(await loadAccount("owner"), saved);
  files.set(
    filename,
    new TextEncoder().encode(JSON.stringify({ ...envelope, version: 999 })),
  );
  assert.equal(await loadAccount("owner"), null);
  assert.ok(files.has(filename));
  assert.ok(
    JSON.parse(pointers.get("pq.cache.owner.recovery")).includes(pointer),
  );
});
