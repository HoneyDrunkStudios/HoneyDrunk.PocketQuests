# Local development

Work in the primary Pocket Quests checkout. Run `scripts/Initialize-Local.ps1 -IdentitySourceRoot PATH` before opening the solution on a fresh machine.

## Visual Studio

Open `HoneyDrunk.PocketQuests/HoneyDrunk.PocketQuests.slnx` from the repository root. Set `PocketQuests.AppHost` as the startup project, choose the `local` launch profile and press F5. The authenticated dashboard URL appears in AppHost output. AppHost launches the API, the configured Identity service, and Expo together.

The solution folder contains the application, domain, data, API, AppHost, service defaults, tests and isolated browser test host as peer project folders. There are no .NET `src` or `tests` wrapper folders. The browser test host is an isolated acceptance fixture, not the normal startup project.

## Mobile editing and preview

Open `apps/mobile` in VS Code for the React Native app. Its `src/app` directory is the existing Expo Router route convention and is separate from the removed .NET wrapper folders. The browser preview is available at `http://localhost:8081` while Aspire's mobile resource is running. Do not run a second Expo server on that port.

For Android use the installed development build/emulator with a reachable Metro endpoint; iOS native builds require the corresponding Apple build environment and signing. Live sign-in still requires provider setup. The browser and Android acceptance fixtures use isolated test identities and databases.

## Terminal alternative

From the Pocket Quests repository root:

```powershell
dotnet run --project HoneyDrunk.PocketQuests/PocketQuests.AppHost
```

Stop the AppHost before starting Expo separately. For a frontend-only browser preview, from `apps/mobile` run `npm run web`; API and Identity must already be running for connected features.

## Current local wiring

Ignored `.local/Identity.props` enables the explicit `UseIdentitySource=true` switch for current joint service development, and `.local/development.json` locates the Identity checkout. No client secrets belong in the mobile app. See `environments.md` for SQL and endpoint configuration.

The Identity solution is `HoneyDrunk.Identity/HoneyDrunk.Identity.slnx` inside the separate Identity checkout. The Pocket Quests solution also loads the Identity API and its dependencies under an `Identity` solution folder, referencing their existing checkout. Visual Studio requires the API project to be loaded to launch it through Aspire. `Initialize-Local.ps1` reconciles all configured Identity projects to the solution. A second Visual Studio instance is optional.

If the AppHost dashboard opens but Expo does not start, check the Identity resource first: the API waits for Identity, and mobile waits for the API. After changing the solution, stop debugging, accept Visual Studio's solution reload prompt, and start AppHost again.

## Database projects

The solution includes PocketQuests.Database and Identity/HoneyDrunk.Identity.Database. Expand Tables for SQL definitions and Data for seed/ad-hoc scripts. Reload the solution when prompted. Deploy each database using that repository's scripts/Deploy-LocalDatabase.ps1; see [DACPAC workflow](database-project.md).
