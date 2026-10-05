import test from "node:test";
import assert from "node:assert/strict";
import Module, { createRequire } from "node:module";
import "./provider-harness.cjs";
const require = createRequire(import.meta.url);
const original = Module._load;
let refreshed;
let grantError;
Module._load = function (name, ...rest) {
  if (name === "expo-auth-session")
    return {
      fetchDiscoveryAsync: async () => ({
        tokenEndpoint: "https://issuer.example.com/token",
      }),
      refreshAsync: async (config) => {
        refreshed = config;
        if (grantError) throw grantError;
        return {
          accessToken: "renewed",
          refreshToken: "rotated",
          issuedAt: 1000,
          expiresIn: 3600,
        };
      },
    };
  return original.call(this, name, ...rest);
};
const {
  providerScopes,
  providerCredentials,
  renewProviderSession,
  verifyProviderSession,
  validateProviderConfiguration,
} = require("../../src/session/provider-session.ts");
const config = {
  authority: "https://issuer.example.com",
  clientId: "mobile-client",
  scope: "quests.read quests.write",
};
const saved = {
  ...providerCredentials(
    { accessToken: "old", refreshToken: "refresh", issuedAt: 1, expiresIn: 2 },
    config,
  ),
  userId: "owner",
};
function transport(userId = "owner", state = "Active") {
  grantError = null;
  refreshed = null;
  const requests = [];
  global.fetch = async (url, options) => {
    requests.push({ url, options });
    return {
      ok: true,
      json: async () =>
        url.endsWith("/client-configuration") ? config : { userId, state },
    };
  };
  return requests;
}
test("offline_access is requested only when configured or advertised by the broker", () => {
  assert.deepEqual(providerScopes(config, null), [
    "openid",
    "profile",
    "quests.read",
    "quests.write",
  ]);
  assert.ok(
    providerScopes(config, {
      discoveryDocument: { scopes_supported: ["offline_access"] },
    }).includes("offline_access"),
  );
  assert.throws(
    () =>
      validateProviderConfiguration({
        ...config,
        authority: "http://issuer.example.com",
      }),
    /Secure/,
  );
});
test("provider rotation uses the supported refresh API then verifies the same active Identity account", async () => {
  const calls = transport();
  const next = await renewProviderSession(saved);
  await verifyProviderSession({ ...saved, ...next });
  assert.equal(next.token, "renewed");
  assert.equal(next.refreshToken, "rotated");
  assert.equal(next.expiresAt, 4600000);
  assert.deepEqual(refreshed, {
    clientId: "mobile-client",
    refreshToken: "refresh",
  });
  assert.equal(calls.at(-1).options.headers.Authorization, "Bearer renewed");
});
test("a different or inactive owner and changed issuer cannot take over a stored queue", async () => {
  transport("different");
  await assert.rejects(verifyProviderSession(saved), /active account/);
  transport("owner", "Inactive");
  await assert.rejects(verifyProviderSession(saved), /active account/);
  transport();
  await assert.rejects(
    renewProviderSession({
      ...saved,
      authority: "https://changed.example.com",
    }),
    /configuration changed/,
  );
  assert.equal(refreshed, null);
});
test("invalid_grant requests reauthentication; transient failure is retryable and neither leaks provider details", async () => {
  transport();
  grantError = { code: "invalid_grant", message: "private provider detail" };
  await assert.rejects(
    renewProviderSession(saved),
    (error) => error.status === 401 && !error.message.includes("private"),
  );
  transport();
  grantError = new Error("private provider detail");
  await assert.rejects(
    renewProviderSession(saved),
    (error) => !error.status && !error.message.includes("private"),
  );
});
