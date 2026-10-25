# Expo Go with a persistent development account

The optional `tools/PocketQuests.DevelopmentHost` runs the actual Pocket Quests API and actual HoneyDrunk Identity API with a local development provider. The mobile app exchanges a private pairing code directly for a signed eight-hour bearer token. There is no browser redirect, mocked quest API, guest ownership or change to production authentication. The mobile `connect` workflow still verifies the canonical Identity user and loads the normal profile/catalog before publishing a session.

The provider has one fixed issuer/subject. Identity maps it to a stable `usr_` account in `PocketQuests_ExpoGo_Identity`; the product uses `PocketQuests_ExpoGo`. Both are dedicated databases on `(localdb)\PocketQuests`. First startup deploys the existing DACPACs only if these databases do not exist. Shutdown never removes databases or saved progress; subsequent startup neither resets nor upgrades them. Schema upgrades need an explicit reviewed deployment. These records are development data and are separate from any real provider account.

## First run on an iPhone

1. Use PowerShell 7, the supported .NET/Node versions, SQL Server LocalDB with the existing `PocketQuests` instance, and a configured local Identity source checkout. Install mobile dependencies with `npm ci` if needed. No schema changes or Apple signing are performed by this workflow.
2. Configure a trusted HTTPS reverse proxy from a device-reachable root URL to `http://127.0.0.1:15219`. This is a separate networking step: the launcher does not create tunnels, certificates, firewall rules or cloud resources. A Metro tunnel exposes JavaScript, not the API. Keep pairing codes and tokens private; never place them in `EXPO_PUBLIC_*` variables or source.
3. Stop the existing AppHost/Metro session explicitly if it owns port 8081. The launcher refuses occupied ports rather than stopping another session. From the repository root, run:

   ```powershell
   ./scripts/Start-ExpoGo.ps1 -BackendUrl https://YOUR-TRUSTED-DEVICE-ENDPOINT
   ```

4. The launcher builds/starts the opt-in host, checks both local and HTTPS readiness, selects Expo Go with `--go`, clears Metro's environment cache, and prints a QR code plus a pairing code. Keep the terminal open. The phone and computer need a working LAN route to Metro on 8081; allow it through the existing private-network firewall policy if necessary.
5. Open the project in an Expo Go installation compatible with Expo SDK 58. Sign in to the same Expo account in Expo Go and the CLI (`npx expo login`); this is separate from Pocket Quests authentication. Choose **Use development account** after entering the pairing code. The UI uses real API requests and SQL persistence. Create/complete a quest, close/reopen the project, and verify the saved progress. Stopping/restarting the backend invalidates old tokens, so pair again; the SQL account and progress remain the same.

The pairing code is a randomly generated six-digit numeric PIN (including leading zeros), generated per launcher run and entered by the user; it is never bundled in JavaScript. Signed tokens use process-local random keys. The issuer only runs when the separate tooling executable has both `ASPNETCORE_ENVIRONMENT=Development` and `DevelopmentSignIn__Enabled=true`. It binds only to IPv4 loopback on 15219 and limits pairing attempts. The public development screen/request also requires `__DEV__` and `EXPO_PUBLIC_DEVELOPMENT_SIGN_IN=1`; release bundles ignore that flag. Neither deployed API references the development host or its signing key. This tool must not be deployed.

The proxy exposes the existing authenticated quest routes and read-only Identity user/status routes. Provider linking, account erasure/recovery, real OAuth, notifications and native-device acceptance require their own verification through the real provider/native build. An expired token requests sign-in again rather than refreshing through OAuth. Unsynced actions retain the existing same-account guards.

For normal development, leave the launcher and return to the existing AppHost. It restores the three process environment variables and stops only the host it started; its databases remain. The launcher requires a trusted HTTPS endpoint and checks it before starting Metro.

Run `./scripts/Test-ExpoGo.ps1` for the HTTP sign-in, rejection, throttling, production/disabled startup, real SQL completion and backend restart checks. It creates a unique `PQExpoGo_<hex>` LocalDB instance, removes only its own databases and instance, and records token-free evidence under `artifacts/`. It never uses the normal development databases. `-NoBuild` reuses the current Release build.

For fast launcher and cleanup regression tests without a backend, run `./scripts/tests/Invoke-DevelopmentToolingTests.ps1 -PesterModulePath <path-to-Pester-6.2.0/Pester.psd1>`. CI downloads that pinned module into its temporary workspace. These tests mock process, network and SQL boundaries, cover failure/cleanup paths, and export measured line coverage to Sonar. They do not replace the real HTTP/SQL checks above or prove physical-device behavior. The coverage converter preserves missed lines and rejects empty or foreign-file reports; the existing quality threshold remains unchanged.

See [Expo CLI launch targets](https://docs.expo.dev/more/expo-cli/#launch-target), [Expo authentication](https://docs.expo.dev/guides/authentication/), [SDK 57 Expo Go login requirements](https://expo.dev/changelog/expo-go-57-login) and [local wiring](local-run.md#current-local-wiring). iPhone/Expo Go installation availability and SDK compatibility are separate prerequisites; adding this sign-in path is not physical-iPhone acceptance.
