# Changelog

## [Unreleased]

- Remove universal quest write staging in favor of scoped feature services, typed CRUD and ordinary EF tracking. Preserve atomic replay/history writes and service-owned audit clocks; simplify convention-only EF facets and document repository engineering standards in the root agent guidance.
- Replace serialized account persistence with explicit account-owned relational tables, immutable terms/source history, compact receipts, ownership/rowversion constraints, query-justified indexes and table/column metadata. Keep shared Audit/Outbox ownership unchanged.
- Convert every backend command, read, initialization, anchor, export, maintenance and lifecycle operation to Services/Contracts/Data with named typed handlers, pure Domain rules, explicit mappings and Data-owned transactions. Remove Application and persistence-aware Domain wrappers. Preserve reward rules, wire contracts, late replay and Undo; add retained-term and real-command retry regressions.
- Integrate supported mobile refresh/reauthentication/logout/recovery ownership, encrypted cache recovery, release URL/profile guards, error boundary, reviewed Android clock/pending intent handling and an endpoint-derived typed client with serialization/drift gates.
- Require manual lifecycle settlement and reject automatic-completion/Blob-fallback overrides. Keep acknowledgment publishing broker-only so failed sends remain retryable in the SQL outbox; add local composition regression tests.
