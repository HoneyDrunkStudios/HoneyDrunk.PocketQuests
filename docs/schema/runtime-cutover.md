# Canonical relational persistence and initial schema

The API uses one canonical relational command, read, export, synchronization and private lifecycle path. The user confirmed there are no deployed databases, so this is the initial product schema: no legacy runtime, mode selection, backfill or account conversion is required. No startup path creates, replaces or migrates a database.

The local patch is based on verified main `361adff50f21f5f36022bab77428f98eca8e61c3`, including merged PQ PR3 clock/retry behavior, PR4 messaging safeguards and the PR5 schema baseline. The approved blueprint was checked against Studio `32362fd43b62ce6e2ac2fc446130acec409b3ad0`, the canonical Pocket Quests PRD/catalog and existing Domain behavior. The historical [S1](README.md) and [S2](runtime-s2.md) records describe independently preserved review snapshots, not the current implementation limits.

## Configuration and behavior

`ConnectionStrings__quests` selects the SQL database for both the product and shared infrastructure. Default startup registers the Application `QuestStore`, which delegates commands, anchors and reads to the Domain `IQuestService`, and `AccountLifecycleStateService` for private lifecycle handling. One scoped root `AppDbContext` maps canonical product entities and the unchanged shared Audit/Outbox tables, and supports the Outbox dispatcher and database health check. Domain workflows use ordinary tracked EF data services, explicit transactions and `SaveChanges`; read workflows do not write account state.

POST `/api/profile` explicitly initializes an account idempotently. GET `/api/state` is read-only and returns 404 before initialization. Retention maintenance runs even without a local broker. `Persistence__ReconciliationEnabled` defaults to `true`; disable it only for deterministic tests or an explicitly operated maintenance host. Large GET/export backlogs return 503 while bounded reconciliation advances them. Leaving maintenance disabled can prevent those reads from completing.

Both direct API startup and AppHost accept the connection and reconciliation settings. AppHost forwards the latter from development configuration. Example:

```powershell
$env:Persistence__ReconciliationEnabled = 'true'
$env:ConnectionStrings__quests = 'Server=(localdb)\<new-isolated-instance>;Database=<new-empty-database>;Integrated Security=true;Encrypt=true;TrustServerCertificate=true'
$env:Identity__BaseUrl = 'https://<explicit-identity-service>/'
dotnet run --project HoneyDrunk.PocketQuests/PocketQuests.Api --no-launch-profile
```

Select `ASPNETCORE_ENVIRONMENT=Development` only for a local development host using a loopback HTTP Identity service. Outside Development/Testing, Identity requires an explicit HTTPS URL. This implementation does not provision Identity or create a provider session.

The existing mobile sign-in flow already POSTs `/api/profile` before reading state or issuing anchors. Request bodies, response fields, enum strings, JSON/CSV export schema and numerical reward rules are retained. The deliberate GET-before-initialization difference is covered by HTTP tests. Temporary recurrence and clock lead are 503; recurrence includes `Retry-After: 1`. Retry the unchanged pending payload. Permanent invalid proof is 400, conflicting operation payload is 409, and another owner's occurrence is 404. Rejected/temporarily deferred commands do not consume their receipt or clock ordinal.

## Selecting a clean local database

For initial adoption, select a **new empty database**. There is no deployed-data migration requirement. An arbitrary existing database is never a cleanup or replacement target.

The existing `scripts/Deploy-LocalDatabase.ps1` uses the existing `PocketQuests` profile. It is not the clean-target selection. For a new local target, generate an isolated instance/database name, create only that new instance, then use the DACPAC with an explicit target connection. Example commands below are manual instructions; no application startup runs them:

```powershell
$localSuffix = [guid]::NewGuid().ToString('N')
$localInstance = 'PQRelational_' + $localSuffix.Substring(0,12)
$localDatabase = 'PocketQuests_Relational_' + $localSuffix
if (@(sqllocaldb info) -contains $localInstance) { throw 'Choose a new isolated instance.' }
sqllocaldb create $localInstance -s
if ($LASTEXITCODE -ne 0) { throw 'New local instance creation failed.' }
$localConnection = "Server=(localdb)\$localInstance;Database=$localDatabase;Integrated Security=true;Encrypt=true;TrustServerCertificate=true"
sqlcmd -S "(localdb)\$localInstance" -E -I -b -d master -Q "IF DB_ID(N'$localDatabase') IS NOT NULL THROW 51990,'Clean deployment requires an absent target database.',1;"
if ($LASTEXITCODE -ne 0) { throw 'Refusing an existing database target.' }
dotnet tool restore
if ($LASTEXITCODE -ne 0) { throw 'SqlPackage restore failed.' }
dotnet build HoneyDrunk.PocketQuests/PocketQuests.Database/PocketQuests.Database.sqlproj -c Release
if ($LASTEXITCODE -ne 0) { throw 'DACPAC build failed.' }
New-Item -ItemType Directory -Force .local/database | Out-Null
$deployArguments = @(
  '/SourceFile:HoneyDrunk.PocketQuests/PocketQuests.Database/bin/Release/PocketQuests.Database.dacpac',
  "/TargetConnectionString:$localConnection",
  '/p:BlockOnPossibleDataLoss=True', '/p:DropObjectsNotInSource=False',
  '/p:ScriptDatabaseOptions=False', '/p:IncludeTransactionalScripts=True'
)
dotnet tool run sqlpackage /Action:Script @deployArguments /OutputPath:.local/database/new-relational.sql
if ($LASTEXITCODE -ne 0) { throw 'Clean-target deployment preview failed.' }
# Inspect the generated script and verify the still-absent explicit target before applying:
# dotnet tool run sqlpackage /Action:Publish @deployArguments
# $env:ConnectionStrings__quests = $localConnection
```

The DACPAC contains 33 canonical product tables and the unchanged shared Audit/Outbox definitions. The ten obsolete `dbo` product tables and their backfill are absent. Catalog seed publication is repeatable. Publish safeguards remain `BlockOnPossibleDataLoss=True` and `DropObjectsNotInSource=False`; no deletion script or automatic migration is added.

The test runner is the automated clean-deployment selection: `scripts/Test-RelationalSchema.ps1` creates only a fresh random `PQSchema_<hex>` instance and GUID databases, publishes this exact DACPAC with `upgradeExisting: false`, and verifies cleanup. Repeat-publication tests independently verify schema/seed idempotence. A physical restore test overwrites only its own GUID fixture from its own allowlisted temporary backup.

## Persistence and history

The typed [contract](schema-contract.json) currently describes 33 product tables and 372 columns. Every table/column has metadata; query/FK indexes and EF covering columns are compared against deployed SQL. Immutable definition/series/occurrence revisions and typed command inputs retain historical terms, presentation order, pause intent, schedule cursor and proof visibility. Current hydration uses current relational rows; it does not replay every historical command. The internal occurrence reader supports keyset pages of 1-100 rows. The existing public full-state contract remains a full-state response; introducing a public paging contract is separate work.

Ordinary scoped EF data services reuse HoneyDrunk.Data generic CRUD through `BaseDataService`. Root `BaseDbContext` is inheritable; `AppDbContext` tracks canonical product entities and shared Audit/Outbox. Per-entity business services own history and lifecycle invariants, with explicit transactions and SaveChanges in the Domain quest/lifecycle workflows. The dependency direction is Application → Domain → Data. DACPAC owns DDL; each entity has its own mapping under `Configurations/{domain}` and its canonical entity under `Entities/{domain}`. There are no product stored procedures, TVPs, generated mutation policies, custom writer sessions or EF migrations.

Authenticated Identity supplies the canonical `usr_` owner; public account IDs and forwarded Grid headers never select ownership. Domain services enforce account ownership, immutable original values, allocation rules, write-once Undo and source/receipt/proof/Audit atomicity. Built-in `sys.sp_getapplock` serializes account command/lifecycle transactions; SQL rowversions reject stale tracked updates, and compound FKs/checks/unique constraints enforce storage relationships. Ordinary reads use explicit no-tracking queries or read-only repeatable-read workflows without SaveChanges. Account collections load once per entity/account in a transaction and then use the tracked local view, including newly staged rows; a later transaction reloads them.

V2 receipts contain only the original projection instant and a null completion-outcome field. Typed history recalculates exact original feedback under retained ruleset/digest/display versions. The deployed CHECK constrains receipt document shape/size; the application constructs only compact V2 outcomes. A regression produces large legal completion feedback while keeping the receipt independent of account size. The clean-database implementation supports the retained V2 format and rejects unsupported persisted versions; it does not pretend that legacy snapshots are present. Future reward/catalog/ruleset changes need their retained replay implementation, not reinterpretation of old facts.

Undo retains the existing strict 24-hour boundary. Clock proofs preserve fractional milliseconds, fixed anchor floors, committed visibility and original offline ordering, including valid one-year/400-day late arrival. There is no blanket age cutoff. Ordinary API restart does not lose pending proof. SYNC01 native boot/clock continuity is a separate unintegrated dependency; backend restart tests do not certify a device's native clock implementation.

Request reconciliation advances at most 100 deliveries and persists internal continuation history before returning retryable 503, without a client receipt/ordinal. Maintenance scans at most 50 accounts per indexed clock/UUID page, with at most 1000 deliveries per account. Historical budgets make receipt replay deterministic. Tests cover multi-series ordering, fairness, concurrent workers, late series creation and no repeated idle writeback. GET and export perform no account writeback or exclusive account application lock; extensive overdue work is completed by maintenance/command continuation.

A lifecycle pause immediately freezes current commitments and retains any undelivered pre-pause cursor segment. Bounded reconciliation can materialize that segment while paused, but stops at the original pause instant. Already-missed deliveries keep their original dates; a still-active delivery at the pause boundary is frozen at that instant. Resume drains the retained segment before shifting future dates. The 400-day regression requires three bounded pending-command retries, preserves all 401 occurrences and exact original receipts, and introduces no deliveries inside the paused interval. Lifecycle replay uses the recorded zero action budget instead of accidentally reconciling unbounded history.

Each new recurring occurrence links the latest applicable internal configuration for its public schedule version. A definition edit may change terms/reset automatic penalty consent without changing that public version. Existing occurrences retain their original configuration; deliveries materialized before the edit in the same transaction also retain the previous configuration. Later deliveries reference the edited configuration. The existing `save-series` command may choose another quest: the current series pointer changes only with a new owned immutable configuration, while earlier revisions/occurrences stay unchanged. The public aggregate restricts edits to the corresponding command. The series business service rejects definition/configuration mismatch, missing owned configuration and revision regression or skipped revision. Earlier sources remain immutable.

## Lifecycle, roles and shared ownership

The combined API/authentication/private lifecycle host principal needs the lifecycle role, which includes command-role membership, plus shared Outbox dispatcher permissions supplied by its owner. The DACPAC creates only the two internal roles; no login, user, queue or cloud resource is provisioned. Catalogs remain read-only. Command permissions allow the ordinary EF writes used by commands; immutable history receives INSERT but no UPDATE/DELETE, and lifecycle fence linking receives only column-level AccountId UPDATE. Lifecycle adds erasure DELETE and private fence/marker/message writes. Product-owned Audit insert/delete and Outbox insert/delete permissions support the existing atomic workflows; shared table definitions remain unchanged. No product DENY overrides independently composed dispatcher UPDATE rights.

These roles represent trusted application services, not per-human database identities. Lifecycle/account authorization runs in Domain services before writes; granting table DML necessarily means privileged SQL or a compromised service principal could bypass those application rules. SQL still enforces row shape and relational constraints. Neither role is exposed as a public CRUD API. Disposable role tests exercise actual EF initialization/command workflows, reject catalog/history/private-fence rewrites from the command role, and verify shared dispatcher permission composition.

Private acknowledgments and explicit ownership links commit with the lifecycle effect. Injected Outbox failure rolls back freeze or complete erasure, including linked Audit rows. `LifecycleMessage` selects exactly owned shared envelopes; `AccountAuditRecord` selects personal Audit rows without actor-wide deletion. Unrelated rows and other accounts survive. Merged manual settlement, disabled Blob fallback, startup validation and broker-failure propagation remain in place and have in-process composition tests.

`ErasureMarker` is exactly `Id + CreatedAt`, with no timestamp default. CreatedAt is the original verified actual-erasure instant, preserved through duplicate delivery and physical restore. Retention is exactly 35 elapsed days. After expiry, a renewed old erasing capability for an already absent account/fence is acknowledged without inventing a new marker/time. A delayed first erasure with actual account/fence data still present performs the purge and records its actual time. A current externally retained marker can be reapplied only before product traffic opens; it must carry its original instant, never the restore clock.

Hosted restoration still depends on a trustworthy erasure evidence source outside the restored backup, a restore window compatible with retention and a closed-traffic restore procedure. No external journal, Identity capability, queue topology or hosted restore controller is fabricated here.

## Validation and remaining external gates

Run `dotnet restore HoneyDrunk.PocketQuests/PocketQuests.SchemaTests --locked-mode`, then `./scripts/Test-RelationalSchema.ps1`. The package-mode lane uses pinned Identity 0.1.0-alpha.4 candidates from the authorized local feed/cache and never requires an Identity API source build. Its real API/SQL tests deliberately supply controlled Identity HTTP responses through the actual Identity client; they do not validate provider JWTs or a live Identity service.

The combined gate covers metadata, source/DDL/EF parity, semantic CHECK/default/filter comparison, generator drift, nine contract mutation checks, real role/negative writes, exact replay/Undo/late arrival, concurrency, account isolation, lifecycle failure rollback, retained marker clocks, physical backup/restore, compactness, maintenance and HTTP JSON/CSV behavior. Export tests retain archived revisions, original completion terms, Undo history, formula-safe CSV and private no-store downloads. The same product regressions now run against canonical persistence in the package-mode lane and in the separate Identity source-integration lane. Obsolete old-schema upgrade probes are replaced by fresh/repeat-publication and compact-receipt replay checks; source-only JWT and cross-service lifecycle coverage is retained. A real hosted worker test advances a 400-day backlog without a client mutation and then serves a read-only HTTP projection. Independent fixture deployment is serialized only to avoid LocalDB model-database creation contention; account transaction concurrency remains real.

Still external: native Android/iOS validation; actual Identity provider/source integration; released shared package adoption; live Service Bus/managed identity; hosted Azure SQL, restore orchestration and production-size performance. Transport PR50 was reported merged, but no unreleased package or unverified shared source was substituted. PR #5 hosted CI verified the pinned-source integration on its original accepted head. This separate local cleanup does not certify future Identity changes or live deployment. Identity fixes/rollout and Greptile are deferred by the user. Shared table-design checker findings remain owned by Audit/Data. Any findings outside product and those two shared tables fail the gate.

The EF correction is local pending final combined verification and independent review. Every further merge requires Oleg’s personal review and explicit approval; no merge, deployment, live database replacement or cloud provisioning is part of this work.

Quest completion now uses the scoped Services exemplar and Data-owned transaction executor; other workflows retain their existing implementation. See [completion service](../completion-service.md) for scope, representative files and validation boundaries.
