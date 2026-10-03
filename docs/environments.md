# Switching environments and authoritative storage

September 28, 2026. SQL Server is authoritative for both services. LocalDB is the Windows development instance of SQL Server, not a separate file-storage implementation. Switching to another SQL Server or Azure SQL host changes configuration and deploys the matching DACPAC; it does not require domain, API, or repository code edits. Switching to another database engine is different: EF mappings, SQL application locks and SQL project would need work.

## The small configuration surface

| Process | Setting | Purpose |
| --- | --- | --- |
| Pocket Quests API | `ConnectionStrings__quests` | Product SQL connection |
| Identity API | `ConnectionStrings__identity` | Separate Identity SQL connection |
| Pocket Quests API | `Identity__BaseUrl` | Shared Identity HTTPS endpoint |
| Expo build/dev server | `EXPO_PUBLIC_API_URL`, `EXPO_PUBLIC_IDENTITY_URL` | Public service URLs embedded in the mobile bundle; never database credentials |
| Development AppHost | `Identity__ServiceUrl` or explicit `Identity__SourceRoot` | External service or local source process |
| Both APIs | `Lifecycle__ServiceBusNamespace`, `Lifecycle__AcknowledgmentQueue` | Private Transport broker and receipt destination |
| Pocket Quests API | `Lifecycle__ConsumerQueue` | Identity-only sender queue for product lifecycle instructions |
| Identity API | `Lifecycle__Consumers__pocketquests` | Registered product destination; user requests cannot edit this registry |

For the normal database-host switch, only the two SQL connection strings change. Keep public API endpoints stable and the app does not need rebuilding. If a public endpoint changes, supply the new `EXPO_PUBLIC_*` values when building the app. AppHost intentionally starts local development tools only; hosted services run the API/Identity executables directly.

Development `appsettings.Development.json` files supply LocalDB convenience values. Standard .NET environment variables override them. Other environments have no automatic LocalDB connection. SQL projects own schema deployment. There is no startup `EnsureCreated`, database deletion, or automatic production schema update.

## SQL Server example

Use process/service configuration (or the environment's secret provider), not committed files. With integrated authentication, a non-secret example is:

```text
ConnectionStrings__quests=Server=tcp:YOUR_SQL_HOST,1433;Database=PocketQuests;Integrated Security=true;Encrypt=true;TrustServerCertificate=false
ConnectionStrings__identity=Server=tcp:YOUR_SQL_HOST,1433;Database=HoneyDrunkIdentity;Integrated Security=true;Encrypt=true;TrustServerCertificate=false
```

Give each service access only to its own database. Use a separate schema-deployment identity for DDL; runtime identities need the scoped data and lifecycle operations. Do not paste SQL passwords, client secrets or access tokens into this document, shell transcripts or the mobile bundle. For SQL authentication, inject the complete secret connection string through the deployment's protected settings/Vault integration.

Azure SQL managed-identity configuration uses the same keys, with:

```text
Server=tcp:YOUR_SERVER.database.windows.net,1433;Database=PocketQuests;Authentication=Active Directory Managed Identity;Encrypt=true;TrustServerCertificate=false
```

Use `Database=HoneyDrunkIdentity` for the Identity service and its separate managed identity. Database creation, identity grants, private networking/firewall, backup policy and any billing remain explicit provisioning work; none has been executed here. Graph credential lifecycle uses Identity's managed identity; mobile clients never receive that credential.

## Explicit schema rollout

1. Build the SQL project in each repository to produce its DACPAC. EF handles queries and saves; SQL projects own schema changes.
2. Provision the target databases and separate runtime/deployment access. Supply the target connection through protected deployment configuration.
3. Use SqlPackage Script and DeployReport against the exact target, then review the planned changes. Keep BlockOnPossibleDataLoss enabled and DropObjectsNotInSource disabled. Take a recovery checkpoint first.
4. Publish the reviewed DACPAC using the deployment identity. Start the services with their configured runtime connections and verify authenticated reads and writes before directing traffic to them.

For local development, run `scripts/Deploy-LocalDatabase.ps1` in each repository to generate SQL, then `-Action Publish` to apply it. The checked-in profiles target only the two databases on `(localdb)\PocketQuests`. See [database project](database-project.md) for exact locations, seed scripts, and test coverage. Historical EF migration tables are preserved but no longer used; publishing an older DACPAC is not a safe data rollback.
## Three different kinds of local state

- **Authoritative SQL:** user accounts, quest history, receipts and owned audit records. Configuration selects the SQL host independently for each service.
- **Phone cache and queue:** encrypted per-account data behind `offline-store.ts`, with its key/pointer in SecureStore. It remains on the device by design and is cleared on sign-out/account lifecycle transitions. The web implementation is memory-only. Replacing this storage adapter does not alter authoritative persistence.
- **Transport:** configured private Service Bus uses published HoneyDrunk.Transport and the durable Data outbox. An isolated test broker is a verification adapter, not authoritative storage or an automatic production fallback. No local in-memory production mode is silently selected.

## Deployment proof still required

Local SQL and provider-fixture tests are not proof of Azure configuration. Live sign-in, Graph permissions, broker RBAC/finite TTL, backups capped at 30 days, external deletion-ledger preservation and restore gating must be verified in the approved hosted environment. Restore markers must come from outside the backup being restored and be reapplied before traffic; recovering the marker table from the same old backup is insufficient. See the scoped lifecycle contract and canonical manual-actions ledger for the remaining external dependencies.
