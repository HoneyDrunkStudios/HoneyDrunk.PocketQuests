> Historical record: the EF correction supersedes all custom stored-procedure, TVP, read-context and mutation-generator design below. Current structure and permissions are in [runtime-cutover.md](runtime-cutover.md). The old framework is removed from source.

# Relational runtime S2

**Historical S2 record.** See [runtime-cutover.md](runtime-cutover.md) for the completed local API adapter, all-command history, lifecycle and explicit deployment selection. The limitations below describe the preserved S2 review snapshot.

This is an executable, locally staged persistence slice. `RelationalQuestCommands` initializes canonical Identity accounts and runs system-quest acceptance, completion, Undo, durable anchor issuance and source-derived reads against `pocketquests`. At this historical S2 snapshot the API still registered the previous store; current behavior is documented in [runtime-cutover.md](runtime-cutover.md). Unsupported commands and accounts with unsupported profile/recurrence/history features fail explicitly; this class is deliberately not presented as a complete `IQuestStore` implementation.

## Source and integration boundary

- Base: remote main `c88b99b5a38252c1950a6caca2420bad5ef60e1b`, plus the independently reviewed S1 patch `93d362544d478114cac48b1dc3b9190e5fdd39f188ad5ff047fcc39e53d651ad`.
- S1 remains unchanged in its original worktree. S2 is a separate local worktree/patch relative to the frozen S1 tree.
- Reviewed SYNC02 head: `7de99e6db48f18c8d3322da97f814761b6dd2ca7`. Its `SyncClockNotReadyException.cs` is copied unchanged as the one overlapping path. The new lane preserves its retryable temporary clock-ahead versus permanent proof-mismatch distinction. The existing API/store files were not changed or merged from the PR.
- No SYNC01/mobile or messaging worktree changes were integrated. Shared Audit/Outbox table definitions remain unchanged. No Identity source build, live migration, push, PR, merge or deployment occurred.

## Persistence boundary

`InitializeAccount`, `LockAccount`, `IssueSyncAnchor` and `CommitQuestCommand` are in the actual DACPAC. The command procedure receives typed scalar/table-valued inputs, locks the canonical Identity account and checks lifecycle, mutation version, ownership, immutable receipt identity, allocation totals and the write-once Undo boundary. It commits the receipt, accepted terms/allocations, immutable occurrence/event history, completion/Undo, current projections, proof cursor, canonical shared Audit envelope and ownership association in the caller's single SQL transaction. Failure aborts all of them. Accepted source rows are never rewritten by this lane; completion Undo is a one-time annotation accompanied by an immutable event.

`pocketquests_command_runtime` is an internal service role: SELECT and named procedure/type permissions, with direct INSERT/UPDATE/DELETE denied on the product schema and shared Audit table. No login/user is provisioned. This is not per-human SQL authentication: the existing trusted Identity resolver supplies `AccountIdentity.Subject`; no request AccountId is accepted. Calibrated terms, clock proof validation and reward/projection computation remain in trusted application/Domain code. SQL is not a second reward engine or a claim that a compromised service principal cannot submit fabricated business inputs to a permitted procedure.

Canonical `usr_` identity is stable across provider/issuer changes. Lifecycle markers/barriers fence initialization, reads, proof issuance and receipt replay. This slice reads the barriers but does not implement lifecycle message handling, purge, acknowledgment or restore.

## Compact receipts and replay

Digest v1 is an explicit ordered tuple covering every existing `QuestCommand` field and nested terms/proof. It does not hash arbitrary serializer property order. Successful receipt lookup happens before account-version/proof-cursor checks. Same ID/different payload conflicts; rejected actions never consume a receipt or ordinal.

Outcome v1 stores only the original projection instant and optional original completion feedback. `AppliedMutationVersion` selects the accepted revisions and events visible to that response. A later Undo annotation is ignored when its immutable event was not visible at that version. No whole account, command payload, occurrence list, ledger or anchor snapshot is stored in the receipt. Presentation order is retained in the bounded historical display document; typed allocations remain authoritative.

The Domain projector now orders attribute/skill ledger contributions by ordinal target ID. Previously their array order depended on `ImmutableDictionary` string hashing, which changes across OS processes. Field names, numerical allocations and reward rules are unchanged; this explicitly defines deterministic ledger ordering so a fresh process can reconstruct the same original response. The separate test-only `PocketQuests.ReplayProbe` verifies that boundary against a disposable SQL database after a later Undo.

Ruleset/display/outcome/digest versions are checked. Future rule/catalog revisions require retained versioned replay implementations; do not silently reinterpret stored facts with a new numerical or catalog policy.

## Clock and projection behavior

Anchors retain a fixed issuance floor and mutation visibility version. Elapsed time is added to issuance server time, then clamped to that fixed floor; refresh never accumulates clock lead. There is no age cutoff. Wrong account/boot, consumed ordinal, regressing/nonfinite elapsed time and wall/monotonic mismatch reject without consuming state. Temporary server/recorded/floor lead throws the exact SYNC02 exception type, for the future API's existing 503 mapping.

`SyncAnchor.LastElapsedMilliseconds` is corrected from bigint to `float(53)` in S2 because the existing API v1 proof is an IEEE-754 double and valid values include fractional milliseconds. No rounding/truncation or wire change is introduced. The type is used only for that clock proof, never XP/money. The contract validator permits this exact documented exception and rejects other approximate numeric columns. The unchanged SQL checker gains one explicit T03 Review; product results become 0 Fail / 22 Review. S1's 0/21 evidence remains unchanged.

Accepted definitions are adopted once and reused only if immutable terms and allocation pools still match. Projections are recomputed with the existing Domain and replaced transactionally; source facts stay immutable. A delayed completion can reprice later category streak contributions. Undo rebuilds surviving contributions and entitlements. Reads project source rows without account creation, SaveChanges or whole-account writeback. They still acquire the account transaction lock and load account history; paged, lock-free read paths and bounded reconciliation/performance work are S5, not completed here.

## Validation and remaining cutover gates

Run `scripts/Test-RelationalSchema.ps1`. It builds the pinned package-mode lane, deploys to fresh named scratch databases and deletes its unique LocalDB instance. The Microsoft SQL Server 2019 master DACPAC 150.1.2 is a pinned system reference for procedure signature checking and must accompany the product DACPAC for DacFx operations. No master objects are deployed into the product database.

Tests cover all ten catalog quests against existing Domain responses, compact original replay after later mutations, same-ID conflicts/races, one-live-completion races, fractional proofs delivered 400 days later, late Undo and its exact 24-hour boundary, fixed floors, ownership/visibility, read rowversion stability, canonical lifecycle barriers, direct role bypass rejection, direct invalid procedure batches, shared-Audit fault rollback, growing-account receipt size and fresh-process replay. The existing metadata/DDL/EF/catalog/expression/mutation tests remain active. Raw evidence is delivered separately with exact source/artifact hashes.

Before API cutover, complete custom definitions/skills, immutable edits/archives, parent links, profile/preferences, penalty consent, recurrence/pause/zone history and old-visible-terms proof behavior. Preserve the full original response for those histories too: the S1 model does not yet record every mutable profile/parent/series presentation fact at each visibility version. Add the smallest explicit history needed rather than snapshots or a generic command JSON journal. Then complete paged reads/reconciliation, canonical Identity/API integration, shared Audit/Data composition and lifecycle inbox/ack fencing, full erasure/restore/35-day-original-marker tests and full backend/mobile integration. The separately approval-blocked Identity source build was not rerouted. Native clients, live Service Bus/Azure SQL and hosted backup restore are unrun.
