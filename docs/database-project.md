# SQL database project

`HoneyDrunk.PocketQuests/PocketQuests.Database/PocketQuests.Database.sqlproj` owns the database schema and builds `bin/Debug/PocketQuests.Database.dacpac`. It loads as a traditional SSDT project in Visual Studio 2026. Install the normal SQL Server Data Tools component and .NET Framework 4.8 targeting pack; Framework is used only by the SQL build tools, while the APIs remain on .NET 10.

The same project conditionally imports Microsoft.Build.Sql only under `dotnet` MSBuild, preserving cross-platform command-line and CI builds. Visual Studio uses its installed SSDT targets. Both paths share the explicit SQL file list and output a DACPAC; intermediate outputs are separated to avoid conflicting caches. Add new SQL files through Visual Studio or explicitly include them in the project. SDK-style SQL project support is not required in Visual Studio.

- `Tables/dbo` contains retained Legacy-mode application tables and the unchanged shared Audit table.
- `Tables/pocketquests.Table.sql` contains the Relational-mode model, with metadata on every table and column. See [explicit mode and clean-database selection](schema/runtime-cutover.md).
- `Tables/outbox` and `Schemas` contain the shared outbox schema and table.
- `Data/Seed` contains repeatable data updates explicitly included by `Data/PostDeployment.sql`.
- `Data/AdHoc` holds manually executed SQL, excluded from automatic publish.
- `PublishProfiles/Local.publish.xml` targets only `(localdb)\PocketQuests`, database `PocketQuests`.

Legacy-mode EF in `PocketQuests.Data` handles its existing queries and saves. Relational-mode EF reads explicit tables and named procedures perform controlled writes. Both mappings must match the SQL project. Domain types remain separate. EF migrations and design-time factories have been removed; do not generate a second schema history.

`Persistence:Mode=Relational` selects the relational store, anchors and lifecycle adapter together; an absent setting retains Legacy mode. Startup never publishes a DACPAC or migrates accounts. The clean-database path and validation boundaries are documented in [runtime-cutover.md](schema/runtime-cutover.md). Publishing this combined DACPAC alone does not select the runtime mode.

## Deploy locally

Stop debugging and ensure the `PocketQuests` LocalDB instance is running. From this repository:

```powershell
./scripts/Deploy-LocalDatabase.ps1                 # Build and generate a reviewable SQL plan
./scripts/Deploy-LocalDatabase.ps1 -Action DeployReport
./scripts/Deploy-LocalDatabase.ps1 -Action Publish # Apply to the named local database
```

Run the equivalent script in the Identity repository for its separate database. Neither service changes schemas on startup. Reload the solution in Visual Studio if the new project is not visible.

The profile blocks possible data loss, preserves objects absent from the project, avoids changing database options, and requests transactional deployment scripts. Back up an existing database before applying changes. Existing `__EFMigrationsHistory` tables can remain as historical records; nothing reads or updates them now. The post-deployment backfills preserve legacy reward snapshots and definition revisions and are safe to repeat.

For a hosted target, use SqlPackage with the DACPAC and an explicitly reviewed target profile supplied by deployment configuration. Do not edit the local profile to point at production or commit credentials. Review a target-specific deployment script/report and take a recovery checkpoint before publishing. Do not treat publishing an older DACPAC as a data rollback.

## Validation

SQL integration tests deploy these same DACPACs into unique test databases. They exercise EF mappings against deployed SQL, preserve a populated legacy ledger through upgrade, and check repeated publishing. Identity's DACPAC is also copied into the integration test output from its explicit source checkout.

SQL projects use Microsoft's [SDK and post-deployment script support](https://learn.microsoft.com/en-us/sql/tools/sql-database-projects/concepts/pre-post-deployment-scripts).
