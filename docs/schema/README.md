# Relational schema implementation - S1

**Historical S1 record.** The current implementation, activation rules, test coverage and remaining external dependencies are in [runtime-cutover.md](runtime-cutover.md). Counts and open implementation gates below describe the frozen S1 slice, not the current tree.

This page records the reviewed S1 baseline. The separately staged executable persistence work is documented in [runtime-s2.md](runtime-s2.md). The API continues to use legacy persistence until the remaining cutover gates pass.

This slice adds the approved relational model to the product DACPAC and a separately staged EF read model. **The running API still uses the existing `dbo` persistence tables.** No account conversion, new writer, API contract change or live deployment occurs here. This keeps the current app coherent while the controlled write/replay boundary is implemented in subsequent slices.

The local source base is Pocket Quests remote `main` at `c88b99b5a38252c1950a6caca2420bad5ef60e1b`. The design comes from the approved October 4 schema blueprint; canonical requirements were checked in Studio at `32362fd43b62ce6e2ac2fc446130acec409b3ad0`, including `prds/PocketQuests/mvp-prd.md`, `starter-catalog.md`, `xp-balance-proposal.md`, and the existing Domain implementation. ADR-0030 and ADR-0082 are Accepted; ADR-0048 and ADR-0049 remain Proposed. The founder's metadata, singular-name and relational-history direction is independently approved.

## Review surface

- [schema-contract.json](schema-contract.json) is the reviewed typed/constraint/metadata contract. It describes 31 product tables, 313 columns, 76 foreign keys and 65 additional indexes, plus exact checker-review dispositions. Counts describe the current result; they are not design targets.
- [reference-data.json](reference-data.json) contains the five public catalogs. Tests compare their persisted IDs, order, names and reward requirements against the existing Domain catalog. System templates and reward policy stay in the current versioned domain code.
- `PocketQuests.Database/Tables/pocketquests.Table.sql` has one table per file with its keys, constraints, indexes and table/column/index `MS_Description` properties. All 409 descriptions are checked after actual DACPAC deployment.
- `PocketQuests.Data/Relational/Entities` and `RelationalQuestModel.g.cs` map the full product model. Shared Audit/Outbox mappings describe referenced keys only; their owning tables remain unchanged.
- `RelationalQuestReadContext` uses no tracking and rejects `SaveChanges`. It is deliberately not registered in the API. This application guard is **not** a database permission boundary: privileged SQL, raw commands and bulk APIs are not claimed to be blocked.
- `PocketQuests.SchemaTests` builds with the pinned Identity Abstractions candidate package, without Identity API source. It includes the existing Domain-only tests by reference, plus the new real-SQL schema tests.

Run `python scripts/schema/generate_schema.py` after an intentional contract/seed edit. `--check` fails if generated table SQL, EF mappings or seed SQL drift. The SQL project includes every table explicitly, so a successful source generation alone cannot hide an omitted DACPAC object: deployed catalog tests compare the exact table and column sets.

## Corrections made during implementation review

1. `CategoryProgress.BonusRatePercent` now enforces **0..20**, matching PQ-MVP-013 and `Progression.Bonus` instead of accepting any nonnegative percentage.
2. `XpBalance.Level` now starts at **1**, matching the approved level curve and `Progression.Level`, including zero XP.
3. `AccountLifecycleState`, `LifecycleMessage` and `ErasureMarker` validate the same canonical `usr_` identifier shape as `Account`, including when no Account FK can exist. These are storage checks, not an invented Identity authorization service.
4. EF maps nullable unique target tuples as unique indexes, not alternate identity keys, because EF alternate keys would incorrectly force optional target columns to NOT NULL. SQL still owns their named uniqueness constraints.

`ErasureMarker` remains exactly **Id + CreatedAt**. There is no timestamp default. CreatedAt denotes the original verified-erasure instant; restore/retry must preserve it, and retention is measured from that instant plus 35 elapsed days. The tests prove column shape, storage, rejected duplicate insertion and cutoff predicates. They do **not** implement a purge/restore writer or claim privileged UPDATE is forbidden.

No numerical reward rules, recurrence interval, zero-share allocation behavior, public catalog IDs, wire names or late-sync cutoff changed. The seeded system skill names are ASCII and use their existing uppercase keys; general Unicode/OrdinalIgnoreCase normalization remains a controlled-writer validation gate.

## Validation and reproduction

From the repository root, with .NET 10.0.401, Python and LocalDB installed:

```powershell
python scripts/schema/validate_contract.py
python scripts/schema/test_contract.py
python scripts/schema/generate_schema.py --check
dotnet restore HoneyDrunk.PocketQuests/PocketQuests.SchemaTests --locked-mode
./scripts/Test-RelationalSchema.ps1
```

The current Identity 0.1.0-alpha.4 candidates must exist in the authorized `.packages/feed` or package cache; this slice does not publish them or prove a public clean-machine restore. Existing repository source mode also works in CI. No source-mode Identity build is necessary for this schema lane.

The runner creates a random `PQSchema_<hex>` instance, refuses to reuse an existing name and removes only the instance it created. Tests create and drop their own `PocketQuests_SchemaTests_<guid>` databases. Existing `PocketQuests`, `MSSQLLocalDB`, user databases and running application sessions are not targets. Synthetic evidence is saved under ignored `artifacts/schema-validation` (or the explicitly supplied evidence directory).

Validation covers:

- Contract structure plus deliberate missing-description, FK type/collation/index, duplicate-index and duplicate-table mutation failures.
- Warning-clean DACPAC/EF/test compilation, actual DACPAC publication, exact deployed types/nullability/collation/default names, keys/FK order/delete action, index shape, metadata and trusted/enabled constraints.
- EF column facets, nullability, value generation, rowversion concurrency tokens and ownership relationships; actual no-tracking catalog materialization and seed/domain parity.
- Syntax-tree comparison of every CHECK, default and filtered-index predicate, plus DacFx model comparison for the remaining schema objects. Mutation tests prove that changed bounds, function arguments, string case, collation and boolean precedence are not hidden.
- Repeat publication preserves catalog rows and unchanged timestamps. DacFx conservatively re-emits some equivalent `IN`/`BETWEEN` CHECKs after SQL Server expands them; these are accepted only after expression-tree equivalence succeeds. The raw deployment report remains evidence, not a claim of zero DDL statements.
- Declarative cross-account/ownership, malformed receipt JSON and size limit, UTC, schedule/penalty null tuples, penalty choices, allocation bounds, reward kind, exact Undo boundary, live-completion uniqueness and marker probes, all using synthetic rows. A separate two-connection race checks only one live completion can commit, followed by Undo/recompletion.
- The unchanged table-design checker, with every new-schema failure fatal and every Review matched to an exact, non-stale disposition. Shared-owner and still-active legacy findings remain separate and visible.

The existing backend CI caller invokes this additional lane after its current tests. There is no replacement of the existing SQL lane or broad reusable-workflow adoption in this slice.

## Boundaries for S2–S6

The new account-owned tables are staged and unused by the live application. Do not treat their scalar constraints or the read context as a complete persistence boundary. Before runtime cutover, implement and verify:

- Account-scoped controlled procedures with principal-derived canonical ownership, account/lifecycle locking and version comparison. Procedures are the selected next implementation direction; none are deployed by S1. Their runtime role must not have generic table DML. Do not add a second competing trigger mechanism.
- Atomic immutable revisions plus allocation pools, cross-row sums, normalized-name rules, parent validation, revision/state consistency, write-once Undo and historical display labels.
- Compact original command outcomes and a versioned canonical digest, exact lost-response replay, same-ID/different-payload conflicts and receipt/projection/audit/outbox atomicity. The 65,536-byte CHECK bounds documents; it alone does not prove the maximum legal result fits or forbid a whole-state document.
- Revision-based anchor visibility, fixed clock floors, monotonic ordering, lifecycle invalidation and valid arbitrarily late replay. The staged `bigint` elapsed field requires an explicit versioned treatment of existing fractional-millisecond `double` wire values; do not truncate or silently change the public contract. Preserve the approved native-clock and pending-unverified behavior without adopting an unreviewed mobile implementation.
- Current projection rebuild and chronological zero floors, pause intent, date-line behavior, read-only paged queries and bounded due reconciliation. Existing GET account creation/writeback remains unresolved in the legacy runtime.
- Same-transaction Audit/Data composition, personal audit ownership/erasure policy, lifecycle acknowledgment fencing, verified purge plus marker, externally supplied restore evidence and exact retention. Full erasure/restore cannot be certified by marker storage tests.

The pre-cutover API/SQL integration suite, runtime-principal bypass tests, full new-persistence replay/late-arrival/crash/erasure tests, native clients, provider sign-in, live Service Bus, Azure SQL and hosted restore/performance checks remain open. Source-mode API integration also depends on the separately blocked Identity build; this task does not reroute that action.

## Concurrent work and ownership

SYNC02 PR #3 head `7de99e6db48f18c8d3322da97f814761b6dd2ca7` was inspected but not merged. Its temporary clock-ahead conditions return **503** and preserve exact command payloads for retry; permanent proof mismatch remains **400**. No failed command may consume its receipt or ordinal. Later writers must retain those distinctions.

The parent supplied SYNC01's `backend-overlap.json`: it has no incremental backend/schema/EF paths beyond SYNC02. This slice changes none of the eight inherited backend paths listed there and no mobile files. The unreviewed SYNC01 branch is not integrated. Messaging settlement work also remains in its owner's worktrees; shared Audit/Outbox tables and SQL lifecycle handlers are untouched here.

The unchanged shared prerequisites still produce **32 Fail / 19 Review** checker findings. The active legacy product tables produce **72 Fail / 37 Review**. The new `pocketquests` schema produces **0 Fail / 21 exact Review**. Adding clean tables does not resolve legacy runtime debt; those tables are removed only as part of a separately validated persistence cutover. The safe publish settings remain `BlockOnPossibleDataLoss=True` and `DropObjectsNotInSource=False`.
