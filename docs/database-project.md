# SQL database project

`HoneyDrunk.PocketQuests/PocketQuests.Database/PocketQuests.Database.sqlproj` owns the database schema and builds `bin/Debug/PocketQuests.Database.dacpac`. It loads as a traditional SSDT project in Visual Studio 2026. Install the normal SQL Server Data Tools component and .NET Framework 4.8 targeting pack; Framework is used only by the SQL build tools, while the APIs remain on .NET 10.

The same project conditionally imports Microsoft.Build.Sql only under `dotnet` MSBuild, preserving cross-platform command-line and CI builds. Visual Studio uses its installed SSDT targets. Both paths share the explicit SQL file list and output a DACPAC; intermediate outputs are separated to avoid conflicting caches. Add new SQL files through Visual Studio or explicitly include them in the project. SDK-style SQL project support is not required in Visual Studio.

- `Tables/dbo` contains only the unchanged shared Audit table.
- `Tables/{domain}/{Table}.sql` contains the canonical product model, with metadata on every table and column. See [initial clean-database selection](schema/runtime-cutover.md).
- `Tables/outbox` and `Schemas` contain the shared outbox schema and table.
- `Data/Seed` contains repeatable data updates explicitly included by `Data/PostDeployment.sql`.
- `Data/AdHoc` holds manually executed SQL, excluded from automatic publish.
- `PublishProfiles/Local.publish.xml` targets only `(localdb)\PocketQuests`, database `PocketQuests`.

Root `AppDbContext` inherits `BaseDbContext` and maps canonical product and shared Audit/Outbox entities. Each entity has one configuration in `Configurations/{domain}`. Scoped Data services reuse HoneyDrunk.Data generic CRUD and own transaction execution. Services orchestrates operations using pure Domain rules and explicit Data queries/mappings. Domain has no persistence dependency. Internal models live under Domain/Models. The SQL project owns metadata, indexes and constraints; no custom SQL write procedures, TVPs, mutation-policy generators or EF migration history remain.

Relational persistence is registered unconditionally. Startup never publishes a DACPAC or migrates accounts. The initial clean-database path and validation boundaries are documented in [runtime-cutover.md](schema/runtime-cutover.md).

## Deploy locally

Stop debugging and ensure the `PocketQuests` LocalDB instance is running. From this repository:

```powershell
./scripts/Deploy-LocalDatabase.ps1                 # Build and generate a reviewable SQL plan
./scripts/Deploy-LocalDatabase.ps1 -Action DeployReport
./scripts/Deploy-LocalDatabase.ps1 -Action Publish # Apply to the named local database
```

Run the equivalent script in the Identity repository for its separate database. Neither service changes schemas on startup. Reload the solution in Visual Studio if the new project is not visible.

The profile blocks possible data loss, preserves objects absent from the project, avoids changing database options, and requests transactional deployment scripts. Back up an existing database before applying changes. The post-deployment script seeds only reference data and is safe to repeat. There are no deployed product databases requiring conversion or historical backfill.

For a hosted target, use SqlPackage with the DACPAC and an explicitly reviewed target profile supplied by deployment configuration. Do not edit the local profile to point at production or commit credentials. Review a target-specific deployment script/report and take a recovery checkpoint before publishing. Do not treat publishing an older DACPAC as a data rollback.

## Validation

SQL integration tests deploy these same DACPACs into unique test databases. They exercise EF mappings against deployed SQL, verify only canonical product and unchanged shared tables, preserve committed history and pending proof through repeat publication, and check repeated publishing. Identity's DACPAC is also copied into the integration test output from its explicit source checkout.

SQL projects use Microsoft's [SDK and post-deployment script support](https://learn.microsoft.com/en-us/sql/tools/sql-database-projects/concepts/pre-post-deployment-scripts).

All product workflows use Services and the Data-owned transaction executor. See [backend services](completion-service.md) for responsibilities and validation boundaries.
