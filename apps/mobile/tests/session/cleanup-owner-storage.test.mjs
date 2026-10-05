import test from "node:test";
import assert from "node:assert/strict";
import Module, { createRequire } from "node:module";
import "./provider-harness.cjs";
const require = createRequire(import.meta.url);
const values = new Map();
const writes = [];
let hold;
const original = Module._load;
Module._load = function (name, ...rest) {
  if (name === "expo-secure-store")
    return {
      WHEN_UNLOCKED_THIS_DEVICE_ONLY: "device-only",
      getItemAsync: async (key) => values.get(key) ?? null,
      setItemAsync: async (key, value, options) => {
        if (hold) await hold;
        values.set(key, value);
        writes.push({ key, value, options });
      },
      deleteItemAsync: async (key) => {
        values.delete(key);
        writes.push({ key, deleted: true });
      },
    };
  return original.call(this, name, ...rest);
};
const { sessionStorage } = require("../../src/session/storage.ts");

test("cleanup ownership uses device-only SecureStore, remains separate from credentials, and clears explicitly", async () => {
  values.set(
    "pocketquests.session",
    JSON.stringify({ userId: "owner", token: "fixture" }),
  );
  await sessionStorage.saveCleanupOwner("owner");
  assert.equal(await sessionStorage.loadCleanupOwner(), "owner");
  assert.deepEqual(writes.at(-1), {
    key: "pocketquests.cleanup-owner",
    value: '"owner"',
    options: { keychainAccessible: "device-only" },
  });
  await sessionStorage.saveCleanupOwner(null);
  assert.equal(await sessionStorage.loadCleanupOwner(), null);
  assert.equal(JSON.parse(values.get("pocketquests.session")).token, "fixture");
});

test("cleanup marker writes are serialized so a pending deletion cannot erase a newer retained owner", async () => {
  let release;
  hold = new Promise((resolve) => (release = resolve));
  const first = sessionStorage.saveCleanupOwner("first");
  const clearing = sessionStorage.saveCleanupOwner(null);
  const retained = sessionStorage.saveCleanupOwner("owner");
  release();
  hold = undefined;
  await Promise.all([first, clearing, retained]);
  assert.equal(await sessionStorage.loadCleanupOwner(), "owner");
});

test("malformed persisted cleanup ownership is rejected instead of silently forgotten", async () => {
  for (const value of ['{"userId":"owner"}', '""', '"   "', "invalid-json"]) {
    values.set("pocketquests.cleanup-owner", value);
    await assert.rejects(sessionStorage.loadCleanupOwner());
    assert.equal(values.get("pocketquests.cleanup-owner"), value);
  }
  await sessionStorage.saveCleanupOwner(null);
});

test("malformed stored credentials fail closed without deleting private pending work", async () => {
  const pending = JSON.stringify({
    userId: "owner",
    command: { operationId: "retained", action: "complete" },
  });
  values.set("pocketquests.pending.owner", pending);
  for (const session of [
    { userId: "owner", token: 123 },
    { userId: 42, token: "token" },
    {
      userId: "owner",
      token: "token",
      renewal: { credentials: { token: null } },
    },
  ]) {
    const serialized = JSON.stringify(session);
    values.set("pocketquests.session", serialized);
    await assert.rejects(
      sessionStorage.load(),
      /Saved sign-in could not be read/,
    );
    assert.equal(values.get("pocketquests.session"), serialized);
    assert.equal(values.get("pocketquests.pending.owner"), pending);
  }
  const valid = {
    userId: "owner",
    token: "old",
    renewal: {
      credentials: {
        token: "replacement",
        refreshToken: "rotated",
        expiresAt: 12345,
      },
    },
  };
  await sessionStorage.save(valid);
  assert.deepEqual(await sessionStorage.load(), valid);
});
