import test from "node:test";
import assert from "node:assert/strict";
import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
global.__DEV__ = true;
process.env.EXPO_PUBLIC_DEVELOPMENT_SIGN_IN = "1";
require("./provider-harness.cjs");
const configPath = require.resolve("../../src/config/client.ts");
const clientPath = require.resolve("../../src/session/identity-client.ts");
const load = (development, enabled) => {
  global.__DEV__ = development;
  process.env.EXPO_PUBLIC_DEVELOPMENT_SIGN_IN = enabled;
  delete require.cache[configPath];
  delete require.cache[clientPath];
  return require(clientPath).requestDevelopmentSession;
};

test("release bundles and missing opt-in reject development sign-in before making a request", async () => {
  let requests = 0;
  global.fetch = async () => {
    requests++;
    throw new Error("must not send");
  };
  for (const [development, enabled] of [
    [false, "1"],
    [true, "0"],
    [true, ""],
  ]) {
    await assert.rejects(
      load(development, enabled)("private-pairing-code"),
      /disabled/,
    );
  }
  assert.equal(requests, 0);
});

test("pairing posts a private code and accepts only a valid credential response", async () => {
  const signIn = load(true, "1");
  global.fetch = async (url, options) => {
    assert.match(url, /\/development\/sign-in$/);
    assert.equal(options.method, "POST");
    assert.deepEqual(JSON.parse(options.body), {
      pairingCode: "private-pairing-code",
    });
    assert.equal(options.headers.Authorization, undefined);
    return {
      ok: true,
      json: async () => ({ token: "signed-development-token" }),
    };
  };
  assert.deepEqual(await signIn(" private-pairing-code "), {
    token: "signed-development-token",
  });
  for (const value of [null, {}, { token: "" }, { token: 42 }]) {
    global.fetch = async () => ({ ok: true, json: async () => value });
    await assert.rejects(signIn("private-pairing-code"), /invalid session/);
  }
});

test("pairing rejection and throttling never return credentials or expose server response details", async () => {
  const signIn = load(true, "1");
  for (const [status, message] of [
    [401, /not accepted/],
    [429, /Wait a minute/],
    [500, /unavailable/],
  ]) {
    global.fetch = async () => ({
      ok: false,
      status,
      json: async () => ({
        token: "do-not-use",
        detail: "private-server-error",
      }),
    });
    await assert.rejects(signIn("private-pairing-code"), message);
  }
});
