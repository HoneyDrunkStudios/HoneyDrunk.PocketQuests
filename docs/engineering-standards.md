# Engineering standards

These standards describe the repository's implementation contract. Prefer ordinary C# services, explicit models and EF tracking. Keep changes focused: KISS, YAGNI, SOLID and DRY mean removing unnecessary indirection, using clear responsibilities, and sharing actual repeated behavior. Do not add a mutation framework, policy engine, universal entity-change bag, service locator, parallel persistence path or wrapper layer to organize a single operation.

## Layers and file placement

| Project | Owns |
| --- | --- |
| Api | Authentication, HTTP policy, named typed endpoint handlers, hosting and worker/message adapters. Handlers delegate to a named service and return its typed result. |
| Contracts | Wire requests, responses, public models and boundary types. No EF entities or persistence concerns. |
| Services | Use-case orchestration, static validation, pure mapping, historical source selection, lifecycle policy and calls to typed Data services. |
| Domain | Pure product rules, aggregates, calendars and progression. No EF, Data, Identity, HTTP or host I/O. |
| Data | Entities, EF configuration, generic CRUD, purpose-specific queries, locking and transaction execution. No reward or replay decisions. |
| Database | SQL project/DACPAC schema, constraints, query-supported indexes, descriptions, seeds and deployment ownership. EF does not migrate this schema. |
| Tests / SchemaTests | Tests of real rules, services, HTTP contracts and disposable deployed SQL. Fixtures adapt inputs and inject faults; they do not implement an alternative workflow. |

Use matching feature folders across layers: Accounts, Profiles, Quests, Schedules, Progress, Synchronization and Lifecycle where applicable. Put mappers in that feature's `Mapping` folder and validators in `Validators`. Keep types in named files and avoid anonymous, large endpoint lambdas. Do not reintroduce an Application forwarding layer or a persistence-aware Domain service.

## Mapping, validation, IDs and clocks

Services select business outcomes and retained identities, then call dedicated pure mapping methods. Do not scatter entity/contract construction through endpoint or orchestration code. A mapper may copy fields, format/parse a representation, select the corresponding nullable field for an already-selected command kind, or derive an established deterministic reference. It must not perform I/O, read the host clock, generate random IDs, choose reward rules, decide whether history changed, or own retries, transactions, archive/stop policy or audit timing.

Services own random IDs, trusted ownership and insertion/update timestamps. These product POCOs have no generic audit-stamping facility: do not assume an upstream base context supplies it. Preserve CreatedAt on updates and use the existing monotonic ModifiedAt rule. Passing already-selected values to a mapper is fine; audit/identity assignments in the owning service are deliberate, not inline shape mapping. Prefer injected `TimeProvider` at host-facing service boundaries, with the authoritative operation time captured once and passed to pure rules.

Preserve approved identity exceptions: canonical Identity `usr_` keys come from the trusted resolver, client operation/occurrence/series/skill IDs retain their exact established meaning, historical revision/projection IDs use the existing deterministic derivation, and shared audit IDs come from the shared audit API. Never replace these with fresh EF-generated GUIDs. `ErasureMarker.CreatedAt` means the original verified erasure instant, including restore/retry; it is not an insertion timestamp and has no SQL timestamp default.

Validators are static, deterministic and free of I/O. Give limits, supported versions and repeated/error messages meaningful constants. Check null/error cases before mapping; preserve the existing exception categories, HTTP statuses and structured synchronization errors. Domain validation remains the authority for product rules. Do not hide failures with warning suppression, empty catches or success defaults.

## EF persistence and transactions

Register feature services and typed repositories as scoped dependencies sharing one `AppDbContext`. Use inherited generic CRUD to add/remove the relevant entity type and purpose-specific Data methods for queries. Repository-owned method names omit `Async`; inherited framework methods retain their names. There is no universal `QuestChanges` collection or Data `Apply` dispatcher.

Load tracked entities, change the intended properties, and let EF detect ordinary updates. Do not use `Update`/`UpdateRange` on entities already tracked or recreate whole-account snapshots on reads. Query ownership, selected history versions, ordering, bounds and columns before materialization. Do not issue database/network calls inside loops; collect each feature's new rows locally and submit a typed batch. These short-lived collections and historical identity lookups are ordinary business computation, not a second change tracker.

EF cannot decide whether an immutable definition, series configuration, occurrence transition or completion fact should be appended. Focused services own those comparisons, original-version selection, one-time Undo, projection recalculation and source relationships. Replaceable ledger/balance/category/entitlement projections may be reconciled; immutable replay facts may not be rewritten to match a later projection.

The account data service owns one transaction and one SaveChanges for a command, including history, projections, compact receipt, audit association and applicable outbox work. Keep access/lifecycle checks and the account lock before receipt lookup. Capture the original account version/timezone before mutating tracked state. Reject nested transaction ownership and unrelated pending changes. A bounded transient retry before commit recreates the transaction, clears owned tracking, reloads state and reacquires locks. Never automatically retry after commit starts: resolve an uncertain outcome through the exact retained receipt on the caller's retry. Retain rowversion checks, cancellation and SQL ownership/uniqueness constraints.

## EF configuration and SQL parity

Use CLR nullability and provider conventions for ordinary column types. Omit convention-only `IsRequired`, `ValueGeneratedNever` on ordinary non-key fields, nonunique-index flags and references to a principal's normal primary key. Configure what actually differs or expresses the contract: table/schema and key/index names, application-owned key generation, lengths/Unicode, fixed binary storage, precision, collation, defaults, rowversion, alternate/composite ownership keys, relationships/delete behavior, included columns and required index filters.

Retain the structural comparer for digest bytes and `AfterSaveBehavior.Throw` protections. Do not remove them as cosmetic redundancy. A SQL Server unique nullable index may need explicit `HasFilter(null)` to retain an unfiltered DACPAC index; removing it changes behavior. Model simplification must pass the effective EF/source/DACPAC/deployed SQL comparison without weakening its assertions.

Every product table and column needs an accurate description covering meaning and, where relevant, ownership, time semantics, classification and retention. Add indexes for demonstrated queries/constraints, not blanket indexing. Keep the schema contract, generated DDL and EF model aligned. Shared Audit/Outbox schema remains separately owned. Do not add a SQL rank feature or duplicate domain reward formulas as incidental cleanup.

## Replay, API compatibility and verification

Keep immutable command/term/source history and compact receipts. A receipt records the replay version and original projection instant, not a full account/feedback snapshot. Changes must preserve completion/Undo, late offline arrival, ordinary restart, bounded reconciliation, historical terms, exact payload digest/version handling and original reward rules. There is no blanket late-sync cutoff. Preserve the original verified erasure instant and 35-day marker retention through retry and restore.

Do not silently alter routes, JSON shapes, error contracts or reward behavior during persistence refactoring. A wire/ruleset/digest/receipt version change requires its own explicit compatibility and replay treatment. OpenAPI/generated client changes must be intentional and verified against real serialization fixtures.

Run the checks appropriate to the affected behavior, using [testing-contract.md](testing-contract.md) and the checked-in scripts. Persistence changes require deployed disposable SQL parity, metadata and negative writes plus replay, late arrival/Undo, concurrent ownership, transaction rollback/retry, receipt compactness and erasure/restore coverage. Validate both package and pinned Identity-source modes when shared lifecycle behavior is involved. Run API/client and mobile checks for a combined PR. Add meaningful regression cases for changed behavior; do not create tests that only repeat implementation details. Build without warnings, review the exact diff independently, and distinguish executed local/hosted evidence from unrun native, provider, broker and live deployment acceptance.
