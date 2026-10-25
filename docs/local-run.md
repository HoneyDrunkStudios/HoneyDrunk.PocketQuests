# Local development

Work in the primary Pocket Quests checkout. Run `scripts/Initialize-Local.ps1 -IdentitySourceRoot PATH` before opening the solution on a fresh machine.

## Visual Studio

Open `HoneyDrunk.PocketQuests.slnx` from the repository root. Set `PocketQuests.AppHost` as the startup project, choose the `local` launch profile and press F5. The authenticated dashboard URL appears in AppHost output. AppHost launches the API, the configured Identity service, and Expo together.

The solution folder contains the application, domain, data, API, AppHost, service defaults, tests and isolated browser test host as peer project folders. There are no .NET `src` or `tests` wrapper folders. The browser test host is an isolated acceptance fixture, not the normal startup project.

## Mobile editing and preview

Open `apps/mobile` in VS Code for the React Native app. Its `src/app` directory is the existing Expo Router route convention and is separate from the removed .NET wrapper folders. The browser preview is available at `http://localhost:8081` while Aspire's mobile resource is running. Do not run a second Expo server on that port.

For Android use the installed development build/emulator with a reachable Metro endpoint; iOS native builds require the corresponding Apple build environment and signing. Live sign-in still requires provider setup. The browser and Android acceptance fixtures use isolated test identities and databases.

## Terminal alternative

For Expo Go on a physical iPhone, follow [development pairing](expo-go-development.md). Stop the existing AppHost before using its launcher so it can own Metro 8081 and the opt-in backend. Normal startup does not reset its dedicated saved-progress databases.

From the Pocket Quests repository root:

```powershell
dotnet run --project src/PocketQuests.AppHost
```

Stop the AppHost before starting Expo separately. For a frontend-only browser preview, from `apps/mobile` run `npm run web`; API and Identity must already be running for connected features.

## Current local wiring

AppHost permits its plaintext mobile-service defaults only for localhost, loopback IPs and the explicitly configured `Mobile:TrustedEmulatorHost` (the development settings name Android's `10.0.2.2` host bridge). A physical device needs explicit `Mobile:ApiUrl` and, when starting Identity from source, `Mobile:IdentityUrl` HTTPS URLs with device-trusted certificates; a LAN `Mobile:DeviceHost` alone is rejected. These settings configure the mobile client's route, not TLS termination or trust. The existing loopback-only browser/emulator environment remains usable.

UI snapshot checks do not invoke Git. Importing a new reviewed upstream snapshot uses the installed Git at `C:/Program Files/Git/cmd/git.exe` on Windows or `/usr/bin/git` on Unix, avoiding PATH lookup. For a nonstandard trusted installation, set `PQ_GIT_EXECUTABLE` to its absolute executable path before running the existing `ui-snapshot.cjs --source` workflow.

By default, ignored `.local/Identity.props` enables `UseIdentitySource=true` for joint service development, and `.local/development.json` locates the Identity checkout. To retain that service checkout but use published client contracts in consumers, run `scripts/Initialize-Local.ps1 -IdentitySourceRoot PATH -UsePublishedClientPackages`. This sets `UseIdentitySource=false` and resolves Identity Abstractions/Client `0.1.0-alpha.4` from nuget.org with the committed lockfiles. The retired `-ClientPackageDirectory` option fails before writing configuration or invoking `dotnet`; copied local candidates are not a supported input. No client secrets belong in the mobile app. See `environments.md` for SQL and endpoint configuration.

The Identity solution is `HoneyDrunk.Identity/HoneyDrunk.Identity.slnx` inside the separate Identity checkout. The Pocket Quests solution also loads the Identity API and its dependencies under an `Identity` solution folder, referencing their existing checkout. Visual Studio requires the API project to be loaded to launch it through Aspire. `Initialize-Local.ps1` reconciles all configured Identity projects to the solution. A second Visual Studio instance is optional.

If the AppHost dashboard opens but Expo does not start, check the Identity resource first: the API waits for Identity, and mobile waits for the API. After changing the solution, stop debugging, accept Visual Studio's solution reload prompt, and start AppHost again.

## Published-package validation

The isolated package-mode schema/API/domain lane needs no Identity source checkout or local candidate feed. From the repository root:

```powershell
dotnet restore tests/PocketQuests.SchemaTests/PocketQuests.SchemaTests.csproj --locked-mode -p:UseIdentitySource=false
dotnet build tests/PocketQuests.SchemaTests/PocketQuests.SchemaTests.csproj -c Release --no-restore -p:UseIdentitySource=false
./scripts/Test-RelationalSchema.ps1 -NoBuild
```

The explicit property overrides any source-mode setting in ignored local configuration. Full `PocketQuests.Tests` and browser fixtures require Identity source mode; rerun the initializer without `-UsePublishedClientPackages` before building those lanes. Existing machines that cached historical locally packed `0.1.0-alpha.4` artifacts may receive a lockfile content-hash mismatch: remove only those two Identity package/version cache entries, then restore the published artifacts. Do not edit lockfile hashes to accept a local package. See [dependency provenance](dependency-security.md) and the [testing contract](testing-contract.md).

## Database projects

The solution includes PocketQuests.Database and Identity/HoneyDrunk.Identity.Database. Expand Tables for SQL definitions and Data for seed/ad-hoc scripts. Reload the solution when prompted. Deploy each database using that repository's scripts/Deploy-LocalDatabase.ps1; see [DACPAC workflow](database-project.md).

Identity source mode supports both `src/` and the previous nested Identity layout. `Initialize-Local.ps1` detects the checkout layout and refreshes the root solution references; pinned older Identity revisions remain usable.
