import test from "node:test";
import assert from "node:assert/strict";
import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { validateReleaseConfig } = require("../../scripts/release-config.cjs");
const valid = {
  EAS_BUILD_PROFILE: "production",
  EXPO_PUBLIC_API_URL: "https://api.example.com",
  EXPO_PUBLIC_IDENTITY_URL: "https://identity.example.com",
};
test("production and preview require both explicit HTTPS service URLs", () => {
  assert.doesNotThrow(() => validateReleaseConfig(valid));
  for (const EAS_BUILD_PROFILE of ["production", "preview"])
    for (const key of ["EXPO_PUBLIC_API_URL", "EXPO_PUBLIC_IDENTITY_URL"])
      for (const value of [
        undefined,
        "http://api.example.com",
        "https://localhost",
        "https://localhost.",
        "https://127.1",
        "https://[::1]",
        "https://10.0.0.1",
        "https://api.local",
        "https://a:b@api.example.com",
        "https://api.example.com?token=private",
      ])
        assert.throws(
          () =>
            validateReleaseConfig({
              ...valid,
              EAS_BUILD_PROFILE,
              [key]: value,
            }),
          new RegExp(key),
        );
});
test("development remains usable locally; production cannot opt out through a development environment label", () => {
  assert.doesNotThrow(() =>
    validateReleaseConfig({ EXPO_PUBLIC_APP_ENV: "development" }),
  );
  assert.throws(
    () =>
      validateReleaseConfig({
        EAS_BUILD_PROFILE: "production",
        EXPO_PUBLIC_APP_ENV: "development",
      }),
    /EXPO_PUBLIC_API_URL/,
  );
  assert.throws(
    () => validateReleaseConfig({ NODE_ENV: "production" }),
    /EXPO_PUBLIC_API_URL/,
  );
});
