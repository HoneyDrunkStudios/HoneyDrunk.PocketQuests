# Pocket Quests

A self-improvement app with an Expo/React Native client, .NET 10 API and SQL Server persistence. This is an implementation foundation under review, not a completed or production-deployed product.

## Structure

Open `HoneyDrunk.PocketQuests/HoneyDrunk.PocketQuests.slnx` in Visual Studio. Projects are peers beside the solution, with internal feature/responsibility folders. Domain types represent rules; persistence types use the `Entity` suffix and Fluent API mappings. `PocketQuests.Database` owns SQL tables, data scripts and DACPAC deployment. The separate Identity checkout appears in the solution's Identity folder.

The application implements the starter catalog, custom quests and revisions, XP/rank/skill progression, completion-specific Undo, planning/recurrence, onboarding, profile rewards, pause/resume rules, local notification planning, bounded offline command replay, export and account lifecycle integration. APIs own mutation validation, time reconciliation and progression. SQL locks, transactional receipts and canonical audit records protect replay and concurrency. Native behavior and production operations still need verification.

## First setup on Windows

Requirements: .NET SDK 10.0.401, Node compatible with Expo SDK 57, SQL Server LocalDB and the matching [Identity implementation](https://github.com/HoneyDrunkStudios/HoneyDrunk.Identity/pull/1). Until that PR merges, use its `feat/identity-foundation` branch. CI pins an exact Identity revision.

From the repository root:

```powershell
./scripts/Initialize-Local.ps1 -IdentitySourceRoot 'C:/path/to/HoneyDrunk.Identity'
dotnet tool restore
# Create the dedicated development instance once, then start it.
sqllocaldb create PocketQuests
sqllocaldb start PocketQuests
./scripts/Deploy-LocalDatabase.ps1
# Review the generated SQL before publishing.
./scripts/Deploy-LocalDatabase.ps1 -Action Publish
Push-Location apps/mobile
npm ci
Pop-Location
```

Run Identity's own `scripts/Deploy-LocalDatabase.ps1` in its checkout, review its script, then publish with `-Action Publish`. Each service owns a separate database on the same local SQL Server instance. See [database deployment](docs/database-project.md).

Initialize-Local writes ignored source configuration, enables source references and reconciles every Identity solution project with the supplied checkout path. No particular sibling layout is required after initialization. To test package mode instead, supply `-ClientPackageDirectory PATH` containing the exact 0.1.0-alpha.4 candidates built by Identity's Pack-Client script. These packages are not on NuGet.org; package release is a separate action. Package mode uses committed lockfiles; source mode has separate ignored locks.

## Run

Set **PocketQuests.AppHost** as the startup project, choose the **local** profile and press F5. Aspire starts Identity on 5218, the Quest API on 5217 and Expo on 8081. Open the mobile web endpoint. Alternatively:

```powershell
dotnet run --project HoneyDrunk.PocketQuests/PocketQuests.AppHost
```

Edit `apps/mobile` in VS Code if preferred. Do not start a second Expo server while Aspire owns port 8081. See [local run guide](docs/local-run.md) and [environment configuration](docs/environments.md).

Live sign-in requires your own Entra customer tenant/user flow, API scope and client redirects; no credentials or tenant configuration ship with the repository. Identity returns 503 for missing public sign-in configuration and rejects unauthorized API access. Local account creation was exercised, but the full signed-in quest flow after the schema upgrade remains unverified.

## Validate

After initialization:

```powershell
dotnet restore HoneyDrunk.PocketQuests/HoneyDrunk.PocketQuests.slnx --locked-mode
dotnet build HoneyDrunk.PocketQuests/HoneyDrunk.PocketQuests.slnx --configuration Release --no-restore
dotnet test HoneyDrunk.PocketQuests/PocketQuests.Tests --configuration Release --no-build
Push-Location apps/mobile
npm run lint
npm run typecheck
npm run test:logic
Pop-Location
```

SQL tests create isolated temporary databases and deploy the actual DACPAC, including an upgrade/backfill regression. See [review and verification](docs/review-and-verification.md).

## Remaining release gates

Production hosting, authenticated end-to-end browser testing, native iPhone/Android development builds, real-device notification/offline recovery, social-provider enrollment, production account erasure/transport, backups/retention, scaling and operational checks remain. Repository publication does not publish a mobile app, package or service. The dependency advisory recorded in the review document is a release blocker.
