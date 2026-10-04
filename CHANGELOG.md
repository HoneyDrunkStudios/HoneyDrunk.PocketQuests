# Changelog

## [Unreleased]

- Add explicit Relational persistence mode with typed account-owned schema/history, compact replay receipts, bounded reconciliation, lifecycle erasure/restore and read-only state/export paths. Preserve Legacy as the default; database deployment and clean-target selection remain explicit. Add real SQL/API, permission, metadata/parity and recovery regression gates.
- Require manual lifecycle settlement and reject automatic-completion/Blob-fallback overrides. Keep acknowledgment publishing broker-only so failed sends remain retryable in the SQL outbox; add local composition regression tests.
