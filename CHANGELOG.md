# Changelog

## [Unreleased]

- Require manual lifecycle settlement and reject automatic-completion/Blob-fallback overrides. Keep acknowledgment publishing broker-only so failed sends remain retryable in the SQL outbox; add local composition regression tests.
