// https://docs.expo.dev/guides/using-eslint/
const { defineConfig } = require("eslint/config");
const expoConfig = require("eslint-config-expo/flat");

module.exports = defineConfig([
  expoConfig,
  {
    files: ["src/**/*.{ts,tsx,js,jsx}"],
    plugins: { architecture: require("./scripts/architecture-rules.cjs") },
    rules: { "architecture/boundaries": "error" },
  },
  {
    ignores: ["dist/*"],
  },
  {
    files: ["scripts/*.cjs"],
    languageOptions: {
      globals: { __dirname: "readonly", Buffer: "readonly" },
    },
  },
]);
