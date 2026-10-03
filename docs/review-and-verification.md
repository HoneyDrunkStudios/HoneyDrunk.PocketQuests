# Review and verification

This file records the preserved foundation's earlier review. For the current October 3 app slice and advisory counts, see `production-slice-delivery.md` and `dependency-security.md`; historical passes below do not replace current acceptance evidence.

## Explicit publication review

Reviewed server authority, SQL transaction locks, account ownership, command replay and Undo, bounded offline storage/replay, account lifecycle/erasure boundaries, audit persistence, native credential storage, SQL schema upgrades, source portability, package contracts and CI paths.

Corrected findings include scoped authentication lifetimes, atomic canonical audit writes, rejection of forged context headers, durable command receipt behavior, current Entity/domain separation and SQL database deployment. This publication pass also fixed:

- Fresh setup now enables current Identity source contracts and rewrites all Identity solution entries for the supplied checkout, including unrelated directory layouts.
- Package mode pins the current 0.1.0-alpha.4 contract candidates; the isolated package consumer uses the reorganized Accounts namespace.
- Identity requires exact delegated API scope for owner requests and secondary link/unlink proof. Missing and lookalike scope tests fail closed.
- Public client configuration no longer advertises unconfigured social providers.
- Local setup and review documents describe current SQL/SSDT behavior and distinguish implementation from environment validation.

## Validation

All 65 .NET tests pass with no skipped cases. Tests use actual SQL Server databases and the production DACPACs. The SQL project upgrade regression checks historical completion/revision backfills and repeat publication without data loss. Local native SSDT and CLI SQL builds produce equivalent schemas. A fresh source copy initialized at a different path builds the complete solution with no warnings or errors. Local secrets, private certificates, backups and package candidates are excluded from source publication.

Frontend lint, TypeScript checking and six logic tests pass. Prior bundle checks covered Android, iOS and web; they are not native-device runtime evidence. CI checks the committed revision separately.

## Dependency release blocker

The current `npm audit` reports three moderate entries (zero high/critical), all stemming from [GHSA-vcc3-ghjq-m6fr](https://github.com/advisories/GHSA-vcc3-ghjq-m6fr): exponential URL decoding in `decode-uri-component`, through `query-string` and Expo Router. The old xcode/uuid advisory is addressed by the scoped override.

The installed query-string 7.x expects a CommonJS callable decoder; the patched decoder is ESM. npm's proposed automatic fix downgrades Expo Router and is not an acceptable unattended compatibility change. Resolve with a supported dependency upgrade or a reviewed compatible patch and regression/bundle testing before release. Do not interpret passing application tests as a clean dependency audit.

## Environment limits

Local account creation was exercised before the schema upgrade, and SQL health checks pass afterward. The full live authenticated quest flow has not been reverified. Native device behavior, notification scheduling, encrypted-cache recovery, external-provider linking/key rotation, production transport/erasure, backup restoration, hosting/scaling and operational monitoring remain release gates. Test identities and loopback-only test hosts do not prove production sign-in.
