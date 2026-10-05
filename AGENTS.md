# Repository instructions

Read [the engineering standards](docs/engineering-standards.md) before changing backend code. They define the layer boundaries, mapping and validation conventions, EF tracking/transaction rules, immutable history, metadata, and required verification. Use [repository structure](docs/repository-structure.md) to place files and [backend services](docs/completion-service.md) to follow the actual execution path.

For persistence changes, also read [database ownership](docs/database-project.md), [the schema contract](docs/schema/README.md), and [testing](docs/testing-contract.md). For API changes, read [the API boundary](docs/api-boundary.md) and [contract verification](docs/api-contract.md). Read [mobile engineering standards](docs/mobile-engineering-standards.md) before mobile changes. The narrower [mobile instructions](apps/mobile/AGENTS.md) apply within that app.

Preserve unrelated edits, staging, worktrees, sessions and databases. Use the existing verification scripts and disposable SQL fixtures; never point destructive test setup at an existing database. Keep shared Audit, Outbox, Identity, Transport and UI ownership explicit; report missing upstream capabilities rather than creating local substitutes.

Follow the user's current scope and publication instructions. Task-specific review or merge holds belong in that task's PR/workflow notes; they are not permanent repository approval gates. Review the exact intended and staged diff, resolve actionable findings, use Conventional Commits, and report the exact tested head and any unrun checks.
