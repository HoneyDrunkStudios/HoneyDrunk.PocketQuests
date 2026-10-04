# Relational persistence and explicit local cutover

The API now implements the complete relational command, read, export, synchronization and private lifecycle paths. Selection is explicit configuration. No startup path creates, replaces, converts or migrates a database. The same DACPAC supports the retained Legacy mode and the new Relational mode; changing mode is not a data migration.

The local patch is based on verified main `c452d1becdb90b5e2a86bc6831da2a53818ba014`, including merged PQ PR3 clock/retry behavior and PR4 messaging safeguards. The approved blueprint was checked against Studio `32362fd43b62ce6e2ac2fc446130acec409b3ad0`, the canonical Pocket Quests PRD/catalog and existing Domain behavior. The historical [S1](README.md) and [S2](runtime-s2.md) records describe independently preserved review snapshots, not the current implementation limits.

## Configuration and behavior

| Setting | Legacy | Relational |
|---|---|---|
| `Persistence__Mode` | `Legacy`, also the default when absent | Exactly `Relational` |
| Product store and anchors | Existing `SqlQuestStore` / `dbo` tables | `RelationalQuestStore` / `pocketquests` tables and named procedures |
| Lifecycle | Existing `SqlQuestLifecycle` | `RelationalQuestLifecycle`, selected with the store |
| POST `/api/profile` | Existing initialization behavior | Explicit idempotent account initialization; does not reset an existing zone/profile |
| GET `/api/state` | Existing legacy creation/reconciliation behavior | Read-only; an uninitialized profile returns 404; it never creates or writes account rows |
| Retention | Existing worker when messaging is configured | Retention worker even without a configured local broker |
| Recurrence | Existing legacy path | Bounded request continuation plus background maintenance |

Unknown mode values fail startup. There is no automatic fallback to a different mode if SQL is unavailable. `ConnectionStrings__quests` selects the database for both the product and its shared Outbox; there is no second implicit database. `QuestDbContext` remains registered for shared Outbox composition and database health checks. Relational account writes do not use its legacy mappings. `Persistence__ReconciliationEnabled` defaults to `true`; disabling it is intended for deterministic tests or an explicitly operated maintenance host. Large GET/export backlogs return 503 while maintenance advances them; leaving maintenance disabled indefinitely can prevent those reads from completing.

Both direct API startup and AppHost accept these environment settings. AppHost also forwards `Persistence:Mode` and `Persistence:ReconciliationEnabled` from its explicit development configuration to the API. Set the mode and connection before starting the process; they are not a live mode toggle:

```powershell
$env:Persistence__Mode = 'Relational'
$env:Persistence__ReconciliationEnabled = 'true'
$env:ConnectionStrings__quests = 'Server=(localdb)\<new-isolated-instance>;Database=<new-empty-database>;Integrated Security=true;Encrypt=true;TrustServerCertificate=true'
$env:Identity__BaseUrl = 'https://<explicit-identity-service>/'
dotnet run --project HoneyDrunk.PocketQuests/PocketQuests.Api --no-launch-profile
```

Select `ASPNETCORE_ENVIRONMENT=Development` only for a local development host using a loopback HTTP Identity service. Outside Development/Testing, Identity requires an explicit HTTPS URL. This implementation does not provision Identity or create a provider session.

The existing mobile sign-in flow already POSTs `/api/profile` before reading state or issuing anchors. Request bodies, response fields, enum strings, JSON/CSV export schema and numerical reward rules are retained. The deliberate GET-before-initialization difference is covered by HTTP tests. Temporary recurrence and clock lead are 503; recurrence includes `Retry-After: 1`. Retry the unchanged pending payload. Permanent invalid proof is 400, conflicting operation payload is 409, and another owner's occurrence is 404. Rejected/temporarily deferred commands do not consume their receipt or clock ordinal.

## Selecting a clean local database

For a fresh relational deployment, select a **new empty database and explicit Relational mode**. Do not point Relational mode at a populated legacy database expecting an account conversion. No legacy reader, migration, dual write, history conversion or mode rollback transfers data between the two stores.

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
# $env:Persistence__Mode = 'Relational'
```

Do not use `CreateNewDatabase=True`, disable data-loss protection or change the existing profile to recreate a database. The DACPAC retains legacy tables for Legacy compatibility; on this new target they are empty and the Relational runtime does not populate them. Shared Audit/Outbox table definitions and ownership are unchanged. Catalog seed publication is repeatable. The existing legacy post-deployment backfill does nothing to empty legacy tables; it is not a relational conversion. Removing legacy objects or migrating a real account set requires a separate reviewed change.

The test runner is the automated clean-deployment selection: `scripts/Test-RelationalSchema.ps1` creates only a fresh random `PQSchema_<hex>` instance and GUID databases, publishes this exact DACPAC with `upgradeExisting: false`, and verifies cleanup. Repeat-publication tests independently verify schema/seed idempotence. A physical restore test overwrites only its own GUID fixture from its own allowlisted temporary backup.

## Persistence and history

The typed [contract](schema-contract.json) currently describes 33 product tables and 372 columns. Every table/column has metadata; query/FK indexes and EF covering columns are compared against deployed SQL. Immutable definition/series/occurrence revisions and typed command inputs retain historical terms, presentation order, pause intent, schedule cursor and proof visibility. Current hydration uses current relational rows; it does not replay every historical command. The internal occurrence reader supports keyset pages of 1-100 rows. The existing public full-state contract remains a full-state response; introducing a public paging contract is separate work.

Named procedures enforce account locking, canonical Identity ownership, expected versions including NULL rejection, compound owner FKs, allocation sums, immutable fields, write-once Undo and source/receipt/proof/Audit atomicity. `RelationalQuestReadContext` is no-tracking and rejects SaveChanges. The SQL roles are trusted application-service capabilities, not per-human SQL authentication or an independent SQL reward engine. Authenticated Identity supplies the canonical `usr_` owner; public account IDs and forwarded Grid headers never select ownership.

V2 receipts contain only the original projection instant and a null completion-outcome field. Typed history recalculates exact original feedback under retained ruleset/digest/display versions. The writer rejects V2 receipt JSON over 256 UTF-16 bytes; a regression produces 71,792 bytes of legal original feedback with a 138-byte receipt. V1 receipt compatibility remains explicit. Future reward/catalog/ruleset changes need their retained replay implementation, not reinterpretation of old facts.

Undo retains the existing strict 24-hour boundary. Clock proofs preserve fractional milliseconds, fixed anchor floors, committed visibility and original offline ordering, including valid one-year/400-day late arrival. There is no blanket age cutoff. Ordinary API restart does not lose pending proof. SYNC01 native boot/clock continuity is a separate unintegrated dependency; backend restart tests do not certify a device's native clock implementation.

Request reconciliation advances at most 100 deliveries and persists internal continuation history before returning retryable 503, without a client receipt/ordinal. Maintenance scans at most 50 accounts per indexed clock/UUID page, with at most 1000 deliveries per account. Historical budgets make receipt replay deterministic. Tests cover multi-series ordering, fairness, concurrent workers, late series creation and no repeated idle writeback. GET and export perform no account writeback or exclusive account application lock; extensive overdue work is completed by maintenance/command continuation.

A lifecycle pause immediately freezes current commitments and retains any undelivered pre-pause cursor segment. Bounded reconciliation can materialize that segment while paused, but stops at the original pause instant. Already-missed deliveries keep their original dates; a still-active delivery at the pause boundary is frozen at that instant. Resume drains the retained segment before shifting future dates. The 400-day regression requires three bounded pending-command retries, preserves all 401 occurrences and exact original receipts, and introduces no deliveries inside the paused interval. Lifecycle replay uses the recorded zero action budget instead of accidentally reconciling unbounded history.

Each new recurring occurrence links the latest applicable internal configuration for its public schedule version. A definition edit may change terms/reset automatic penalty consent without changing that public version. Existing occurrences retain their original configuration; deliveries materialized before the edit in the same transaction also retain the previous configuration. Later deliveries reference the edited configuration. The existing `save-series` command may choose another quest: the current series pointer changes only with a new owned immutable configuration, while earlier revisions/occurrences stay unchanged. SQL rejects pointer changes through another action, without an increased revision or without the matching owned configuration.

## Lifecycle, roles and shared ownership

The host principal needs `pocketquests_command_runtime` and `pocketquests_lifecycle_runtime` for the combined API/authentication/private lifecycle composition. These roles grant SELECT and named procedure/type access and deny direct product/Audit writes. The DACPAC creates no login, user, cloud principal, queue or cloud resource. Separate Outbox dispatcher permissions must come from its shared owner. Product lifecycle grants intentionally contain no Outbox DENY that could override those composed grants.

SQL role probes execute valid initialization, lifecycle state, acknowledgment, retention and purge operations under synthetic unprivileged users. Eight negatives reject direct writes and product access to private lifecycle procedures. A separate synthetic dispatcher role demonstrates that its SELECT/UPDATE grant composes successfully; this is permission-composition evidence, not provisioning or certification of a live dispatcher's full grants.

Private acknowledgments and explicit ownership links commit with the lifecycle effect. Injected Outbox failure rolls back freeze or complete erasure, including linked Audit rows. `LifecycleMessage` selects exactly owned shared envelopes; `AccountAuditRecord` selects personal Audit rows without actor-wide deletion. Unrelated rows and other accounts survive. Merged manual settlement, disabled Blob fallback, startup validation and broker-failure propagation remain in place and have in-process composition tests.

`ErasureMarker` is exactly `Id + CreatedAt`, with no timestamp default. CreatedAt is the original verified actual-erasure instant, preserved through duplicate delivery and physical restore. Retention is exactly 35 elapsed days. After expiry, a renewed old erasing capability for an already absent account/fence is acknowledged without inventing a new marker/time. A delayed first erasure with actual account/fence data still present performs the purge and records its actual time. A current externally retained marker can be reapplied only before product traffic opens; it must carry its original instant, never the restore clock.

Hosted restoration still depends on a trustworthy erasure evidence source outside the restored backup, a restore window compatible with retention and a closed-traffic restore procedure. No external journal, Identity capability, queue topology or hosted restore controller is fabricated here.

## Validation and remaining external gates

Run `dotnet restore HoneyDrunk.PocketQuests/PocketQuests.SchemaTests --locked-mode`, then `./scripts/Test-RelationalSchema.ps1`. The package-mode lane uses pinned Identity 0.1.0-alpha.4 candidates from the authorized local feed/cache and never requires an Identity API source build. Its real API/SQL tests deliberately supply controlled Identity HTTP responses through the actual Identity client; they do not validate provider JWTs or a live Identity service.

The combined gate covers metadata, source/DDL/EF parity, semantic CHECK/default/filter comparison, generator drift, nine contract mutation checks, real role/negative writes, exact replay/Undo/late arrival, concurrency, account isolation, lifecycle failure rollback, retained marker clocks, physical backup/restore, compactness, maintenance and HTTP JSON/CSV behavior. Export tests retain archived revisions, original completion terms, Undo history, formula-safe CSV and private no-store downloads. Both modes retain restart/replay/Undo behavior. A real hosted worker test advances a 400-day backlog without a client mutation and then serves a read-only HTTP projection. Independent fixture deployment is serialized only to avoid LocalDB model-database creation contention; account transaction concurrency remains real.

Still external: native Android/iOS validation; actual Identity provider/source integration; released shared package adoption; live Service Bus/managed identity; hosted Azure SQL, restore orchestration and production-size performance. Transport PR50 was reported merged, but no unreleased package or unverified shared source was substituted. The existing source-mode CI pin/integration must be advanced with its owning Identity work after approval; these package-mode tests do not certify that separate CI dependency. Shared table-design checker findings remain owned by Audit/Data, and retained legacy table findings remain visible in their own category. They are not reclassified as product passes.

No commit, push, PR, merge, deployment, live database replacement or cloud provisioning is performed by this local implementation.
