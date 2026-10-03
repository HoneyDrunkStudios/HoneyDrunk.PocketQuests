# Repository structure

The root contains standard repository/build configuration and README. Product code, tests, documentation and tooling have explicit homes; the original user checkout is not rearranged by this task worktree.

| Location | Responsibility |
| --- | --- |
| `HoneyDrunk.PocketQuests/` | Existing .NET solution, Domain/Application/API/Data/SQL/Aspire projects and backend test project. |
| `apps/mobile/src/app/` | Expo Router routes and navigation layouts only. Route paths and public navigation stay unchanged. |
| `apps/mobile/src/features/quests/` | Quest discovery, definition/recurrence editors, quest commands/rules, reward previews, optional focus timer and deadline-warning planning. |
| `apps/mobile/src/features/progression/` | Completion feedback reconciliation, celebration presentation and root modal. |
| `apps/mobile/src/features/profile/` | Onboarding, profile/account settings and export presentation. |
| `apps/mobile/src/session/` | Authenticated session coordination, provider sign-in, private cache, offline projections and request errors. |
| `apps/mobile/src/shared/` | App contracts, app-local theme and adapter to the generic UI package. Generic primitives remain upstream-owned. |
| `apps/mobile/src/config/` | Client endpoints, timeout constants and redirect configuration. |
| `apps/mobile/tests/` | Component, quest, progression, notification and tooling contract tests in corresponding folders. |
| `apps/mobile/e2e/`, `apps/mobile/.maestro/` | Browser-adapter journey and prepared native device smoke flow respectively. |
| `apps/mobile/scripts/` | App test runner and exact UI snapshot/provenance tooling. |
| `apps/mobile/packages/` | Reviewed immutable HoneyDrunk.UI consumer snapshot; no local generic-component fork. |
| `scripts/` | Existing repository-level PowerShell setup, SQL deployment and browser fixture runner. |
| `docs/` | Product delivery, testing, environment and integration notes; inactive CI proposal under `docs/ci/`. |
| `.github/workflows/` | Active hosted validation. HoneyDrunk.Actions is the current shared CI owner. |

The organization change moves 29 mobile source files and six existing test modules, rewrites relative imports, and makes both test commands discover nested test files with the existing Node runner. No runtime dependency, route, backend project, solution path, fixture port or output artifact location changes. `npm run lint` now includes the app's CommonJS scripts as well as Expo source; globals are configured for those scripts without disabling `no-undef`.

App manifests/configuration stay at `apps/mobile/` where Expo, npm, TypeScript and ESLint expect them. Root build files (`Directory.Build.props`, `NuGet.Config`, `global.json`, `dotnet-tools.json`) retain their discovery paths. Generated caches, logs, test outputs and private configuration remain ignored rather than becoming repository content.
