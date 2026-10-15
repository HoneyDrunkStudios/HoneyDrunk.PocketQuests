const path = require("node:path");

const httpOwners = new Set(["api/client.ts", "session/identity-client.ts"]);
const platformOwners = {
  "expo-secure-store": ["session/storage.ts", "session/offline-store.ts"],
  "expo-file-system": [
    "session/offline-store.ts",
    "features/profile/export-download.ts",
  ],
  "expo-sharing": ["features/profile/export-download.ts"],
  "expo-notifications": ["features/quests/notifications.ts"],
  "expo-auth-session": [
    "session/provider-session.ts",
    "session/provider-sign-in.tsx",
    "session/use-provider-setup.ts",
    "app/index.tsx",
  ],
  "expo-web-browser": [
    "session/provider-sign-in.tsx",
    "app/index.tsx",
    "app/callback.tsx",
  ],
};
// Existing narrow leaf modules are deliberate public entry points. A barrel here
// would introduce cycles between profile and quests and eagerly load adapters.
const featureEntries = new Set([
  "features/profile/character-portrait",
  "features/profile/profile-editor",
  "features/quests/quest-rules",
  "features/quests/commands/quest-actions",
  "features/quests/notifications",
]);
const networkPackages = [
  "axios",
  "ky",
  "node-fetch",
  "cross-fetch",
  "undici",
  "http",
  "https",
  "node:http",
  "node:https",
];
const httpGlobals = new Set([
  "fetch",
  "XMLHttpRequest",
  "WebSocket",
  "EventSource",
]);
const storageGlobals = new Set([
  "localStorage",
  "sessionStorage",
  "indexedDB",
  "caches",
]);
const globalObjects = new Set(["globalThis", "window", "global", "self"]);

function staticName(node, computed) {
  if (!computed && node.type === "Identifier") return node.name;
  if (node.type === "Literal") return node.value;
  if (node.type === "TemplateLiteral" && node.expressions.length === 0)
    return node.quasis[0].value.cooked;
  return undefined;
}
function unwrap(node) {
  while (
    node &&
    [
      "TSAsExpression",
      "TSTypeAssertion",
      "TSNonNullExpression",
      "TSSatisfiesExpression",
    ].includes(node.type)
  )
    node = node.expression;
  return node;
}
function isGlobal(sourceCode, node) {
  let scope = sourceCode.getScope(node);
  while (scope) {
    const variable = scope.set.get(node.name);
    if (variable) return variable.defs.length === 0;
    scope = scope.upper;
  }
  return true;
}

module.exports = {
  rules: {
    boundaries: {
      meta: {
        type: "problem",
        schema: [],
        messages: {
          http: "Raw HTTP belongs in api/client.ts or session/identity-client.ts. Keep auth, retry and owner policy in the caller.",
          storage:
            "Browser persistence is forbidden: web credentials/cache must remain page-memory only. Use the owning native storage adapter.",
          platform: "{{module}} belongs in its existing adapter: {{owners}}.",
          ui: "Shared UI must not import product features. Compose product behavior in the owning feature or route.",
          feature:
            "Cross-feature import {{module}} is private. Use a deliberate public leaf entry point listed in architecture-rules.cjs, or review a new public contract.",
          dynamic:
            "Use a static module specifier so architecture boundaries can be checked.",
        },
      },
      create(context) {
        const sourceCode = context.sourceCode;
        const root = path.resolve(__dirname, "../src");
        const filename = path
          .relative(root, context.filename)
          .split(path.sep)
          .join("/");
        const ownFeature = filename.startsWith("features/")
          ? filename.split("/")[1]
          : null;
        const sharedUi = [
          "shared/ui.tsx",
          "shared/theme.ts",
          "shared/error-boundary.tsx",
        ].includes(filename);
        function checkGlobal(name, node) {
          if (httpGlobals.has(name) && !httpOwners.has(filename))
            context.report({ node, messageId: "http" });
          if (storageGlobals.has(name))
            context.report({ node, messageId: "storage" });
        }
        function isGlobalObject(node) {
          const value = unwrap(node);
          return (
            value?.type === "Identifier" &&
            globalObjects.has(value.name) &&
            isGlobal(sourceCode, value)
          );
        }
        function checkPattern(pattern, value) {
          if (pattern.type !== "ObjectPattern" || !isGlobalObject(value))
            return;
          for (const property of pattern.properties) {
            if (property.type === "Property")
              checkGlobal(
                staticName(property.key, property.computed),
                property,
              );
          }
        }
        function checkImport(node, source) {
          if (
            !source ||
            source.type !== "Literal" ||
            typeof source.value !== "string"
          ) {
            context.report({ node, messageId: "dynamic" });
            return;
          }
          const name = source.value;
          for (const [module, owners] of Object.entries(platformOwners)) {
            if (
              (name === module || name.startsWith(module + "/")) &&
              !owners.includes(filename)
            ) {
              context.report({
                node,
                messageId: "platform",
                data: { module, owners: owners.join(", ") },
              });
            }
          }
          if (
            networkPackages.some(
              (module) => name === module || name.startsWith(module + "/"),
            ) &&
            !httpOwners.has(filename)
          )
            context.report({ node, messageId: "http" });
          // Both Metro's root alias and relative imports resolve to the same boundary.
          const resolved = name.startsWith(".")
            ? path.resolve(path.dirname(context.filename), name)
            : name.startsWith("@/")
              ? path.resolve(root, name.slice(2))
              : null;
          if (!resolved) return;
          const target = path
            .relative(root, resolved)
            .split(path.sep)
            .join("/")
            .replace(/\.(?:[cm]?[jt]sx?)$/, "");
          if (!target.startsWith("features/")) return;
          if (sharedUi) context.report({ node, messageId: "ui" });
          if (
            ownFeature &&
            target.split("/")[1] !== ownFeature &&
            !featureEntries.has(target)
          )
            context.report({
              node,
              messageId: "feature",
              data: { module: name },
            });
        }
        return {
          TSExternalModuleReference(node) {
            checkImport(node, node.expression);
          },
          ImportDeclaration(node) {
            checkImport(node, node.source);
          },
          ExportNamedDeclaration(node) {
            if (node.source) checkImport(node, node.source);
          },
          ExportAllDeclaration(node) {
            checkImport(node, node.source);
          },
          ImportExpression(node) {
            checkImport(node, node.source);
          },
          CallExpression(node) {
            if (
              node.callee.type === "Identifier" &&
              node.callee.name === "require" &&
              isGlobal(sourceCode, node.callee)
            )
              checkImport(node, node.arguments[0]);
          },
          MemberExpression(node) {
            if (isGlobalObject(node.object))
              checkGlobal(staticName(node.property, node.computed), node);
          },
          VariableDeclarator(node) {
            checkPattern(node.id, node.init);
          },
          AssignmentExpression(node) {
            checkPattern(node.left, node.right);
          },
          "Program:exit"() {
            // References, rather than identifier spelling, avoid matching properties,
            // type names, and the legitimate local sessionStorage adapter binding.
            for (const scope of sourceCode.scopeManager.scopes) {
              for (const reference of scope.references) {
                if (!reference.resolved || reference.resolved.defs.length === 0)
                  checkGlobal(reference.identifier.name, reference.identifier);
              }
            }
          },
        };
      },
    },
  },
};
