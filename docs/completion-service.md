# Completion service exemplar

This slice establishes `Contracts` and `Services` and moves only quest completion onto the approved service pattern. Other commands, profile initialization, reads, anchors and private lifecycle handling retain their existing workflows until a separate slice is approved.

The public route remains `POST /api/commands`. Its named handler in `Api/Endpoints/Quests/QuestEndpoints.cs` accepts a Contracts request and returns a typed Contracts response. `Services/Quests/QuestService.cs` owns orchestration, invokes static validators and the existing pure Domain aggregate, and chooses its atomic scope through `IBaseDataService.ExecuteInTransaction`.

The endpoint-derived OpenAPI snapshot is refreshed only for JSON member ordering after registration moved. Parsed content is identical to the preceding contract, and the strict snapshot and HTTP compatibility checks remain in place.

`Data/Queries/Quests` applies account ownership, joins, ordering and current-head selection in SQL before materialization. Missing current revisions remain visible through left joins so source validation fails instead of silently omitting a quest or series. Retained definition terms and command history are needed for reward recalculation and exact old-receipt replay. This slice still loads that account history; it does not claim bounded memory or introduce a history cutoff or pagination that changes replay.

Mappings under `Services/Quests/Mapping` convert entities to Domain inputs and stage explicit entity changes. Mapping loops perform no storage calls. Completion updates changed occurrences, recurrence cursors, reward projections, the account cursor and any consumed anchor. It does not rewrite interests, skill definitions, assessments or time-zone history. `QuestCompletionChanges` groups the entity additions and projection removals; Data applies them through EF `AddRange`/`RemoveRange`, without invoking business rules.

The base transaction executor rejects nested transactions and unrelated staged changes before taking ownership. It saves and commits once, rolls back on failure through transaction disposal, and clears only the context work whose ownership it established. It does not automatically repeat a command after an uncertain commit; a subsequent request resolves the stable operation ID against its retained receipt. Account application locks and rowversion checks continue to protect concurrent writes.

Receipts remain compact: only the original projection instant and a null feedback placeholder are stored. Typed immutable source history reconstructs completion feedback, including after Undo or later term edits. Late arrival retains the existing anchor proof rules, recurrence continuation and ordinary restart behavior with no blanket age cutoff.

`AppDbContext` discovers the explicit per-entity configurations under `Data/Configurations`. The DACPAC remains the schema owner. Immutable scalar fields, source history and receipt byte arrays use EF save metadata and a deep digest comparer, without a runtime mutation-policy engine. Shared Audit/Outbox ownership and DDL remain unchanged.

Validation lives in `SchemaTests/Quests/CompletionServiceTests.cs`, the real-host `RelationalApiTests`, compact-receipt regression and existing schema parity/negative-write suite. The test harness requires a newly generated disposable LocalDB instance. Native clock/device, provider sign-in, live broker and deployment checks are separate release work.
