import assert from "node:assert/strict";
import test from "node:test";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { ESLint } from "eslint";

const root = fileURLToPath(new URL("../..", import.meta.url));
const eslint = new ESLint({ cwd: root });
const component = "src/features/quests/new-screen.tsx";
const invalid = [
  [component, 'globalThis[`fetch`]("/x");', "http"],
  [component, '(globalThis as typeof globalThis).fetch("/x");', "http"],
  [component, 'let send; ({ fetch: send } = globalThis); send("/x");', "http"],
  [component, 'window[`localStorage`].getItem("token");', "storage"],
  [component, '(window as Window).localStorage.getItem("token");', "storage"],
  [component, "let store; ({ sessionStorage: store } = window);", "storage"],
  [
    component,
    "const { [`fetch`]: send } = (globalThis as typeof globalThis);",
    "http",
  ],

  [component, 'import Store = require("expo-secure-store");', "platform"],
  ["src/api/client-copy.ts", 'fetch("/api/state");', "http"],
  [
    "src/session/storage-copy.ts",
    'import Store from "expo-secure-store";',
    "platform",
  ],
  [component, 'fetch("/api/state")', "http"],
  [component, 'const send = fetch; send("/api/state")', "http"],
  [component, 'globalThis.fetch("/api/state")', "http"],
  [component, 'window["fetch"]("/api/state")', "http"],
  [component, "const { fetch: send } = globalThis;", "http"],
  [component, "new XMLHttpRequest()", "http"],
  [component, 'new WebSocket("wss://example.com")', "http"],
  [component, 'import axios from "axios";', "http"],
  [component, 'const client = require("node:https");', "http"],
  [component, 'localStorage.setItem("token", "secret");', "storage"],
  [component, 'window.sessionStorage.getItem("token");', "storage"],
  [component, "const { indexedDB: db } = window;", "storage"],
  [component, 'import * as Store from "expo-secure-store";', "platform"],
  [component, 'await import("expo-file-system");', "platform"],
  [component, 'require("expo-notifications");', "platform"],
  [component, 'export * from "expo-sharing";', "platform"],
  [
    component,
    'import Store from "expo-secure-store/build/SecureStore";',
    "platform",
  ],
  [component, "await import(packageName);", "dynamic"],
  [component, "require(packageName);", "dynamic"],
  [
    "src/shared/ui.tsx",
    'export { CharacterHome } from "../features/progression/character-home";',
    "ui",
  ],
  ["src/shared/ui.tsx", 'await import("@/features/quests/quest-board");', "ui"],
  [
    component,
    'import { SettingsEditor } from "../profile/settings-editor";',
    "feature",
  ],
  [
    component,
    'export * from "../profile/../profile/settings-editor.tsx";',
    "feature",
  ],
  [component, 'await import("@/features/profile/settings-editor");', "feature"],
];
const valid = [
  ["src/api/client.ts", 'fetch("/api/state");'],
  ["src/session/identity-client.ts", 'globalThis.fetch("/users/me");'],
  ["src/session/storage.ts", 'import * as Store from "expo-secure-store";'],
  ["src/session/offline-store.ts", 'await import("expo-file-system");'],
  ["src/features/profile/export-download.ts", 'await import("expo-sharing");'],
  [
    "src/features/quests/notifications.ts",
    'await import("expo-notifications");',
  ],
  ["src/session/provider-session.ts", 'await import("expo-auth-session");'],
  [component, 'import { ProfileEditor } from "../profile/profile-editor";'],
  [component, 'import { questLimits } from "./quest-rules";'],
  [
    "src/app/(tabs)/board.tsx",
    'export { QuestBoard as default } from "../../features/quests/quest-board";',
  ],
  ["src/shared/ui.tsx", 'import { View } from "react-native";'],
  [
    "src/session/session-storage-scope.ts",
    'import { sessionStorage } from "./storage"; sessionStorage.load();',
  ],
  [component, "function read(fetch: () => void) { fetch(); }"],
  [component, "const model = { fetch: 1, localStorage: 2 }; model.fetch;"],
];
for (const [file, code, expected] of invalid) {
  test(`architecture rejects ${file}: ${code}`, async () => {
    const [result] = await eslint.lintText(code, {
      filePath: path.join(root, file),
    });
    assert.equal(result.fatalErrorCount, 0);
    assert.ok(
      result.messages.some(
        (message) =>
          message.ruleId === "architecture/boundaries" &&
          message.messageId === expected,
      ),
      JSON.stringify(result.messages),
    );
  });
}
for (const [file, code] of valid) {
  test(`architecture accepts ${file}: ${code}`, async () => {
    const [result] = await eslint.lintText(code, {
      filePath: path.join(root, file),
    });
    assert.equal(result.fatalErrorCount, 0);
    assert.deepEqual(
      result.messages.filter(
        (message) => message.ruleId === "architecture/boundaries",
      ),
      [],
    );
  });
}
test("production source is discovered and every file has the enabled boundary rule", async () => {
  const results = await eslint.lintFiles(["src/**/*.{ts,tsx}"]);
  assert.ok(results.length > 0, "No production files were checked");
  for (const result of results) {
    const config = await eslint.calculateConfigForFile(result.filePath);
    assert.equal(
      config.rules["architecture/boundaries"][0],
      2,
      result.filePath,
    );
    assert.equal(result.fatalErrorCount, 0, result.filePath);
    assert.deepEqual(
      result.messages.filter(
        (message) => message.ruleId === "architecture/boundaries",
      ),
      [],
      result.filePath,
    );
  }
});
