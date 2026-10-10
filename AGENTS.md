# Repository instructions

Read [the engineering standards](docs/engineering-standards.md) before changing backend code. They define the layer boundaries, mapping and validation conventions, EF tracking/transaction rules, immutable history, metadata, and required verification. Use [repository structure](docs/repository-structure.md) to place files and [backend services](docs/completion-service.md) to follow the actual execution path.

For persistence changes, also read [database ownership](docs/database-project.md), [the schema contract](docs/schema/README.md), and [testing](docs/testing-contract.md). For API changes, read [the API boundary](docs/api-boundary.md) and [contract verification](docs/api-contract.md). Read [mobile engineering standards](docs/mobile-engineering-standards.md) before mobile changes. This root file is the only agent entrypoint, including work inside `apps/mobile`; the linked mobile standards retain its narrower engineering rules.

Preserve unrelated edits, staging, worktrees, sessions and databases. Use the existing verification scripts and disposable SQL fixtures; never point destructive test setup at an existing database. Keep shared Audit, Outbox, Identity, Transport and UI ownership explicit; report missing upstream capabilities rather than creating local substitutes.

Follow the user's current scope and publication instructions. Task-specific review or merge holds belong in that task's PR/workflow notes; they are not permanent repository approval gates. Review the exact intended and staged diff, resolve actionable findings, use Conventional Commits, and report the exact tested head and any unrun checks.

## Verification entrypoints

Use [the testing contract](docs/testing-contract.md) for the backend solution, pinned Identity-source prerequisites and disposable SQL fixtures. For mobile work, run commands from `apps/mobile`: `npm ci`, `npm run lint`, `npm run typecheck`, `npm run check:ui`, `npm run check:api`, `npm run check:security-patch`, `npm run test:ci`, plus the appropriate Expo export/build. Native/device/provider acceptance remains separate.

## Shared conventions and delivery

Read the [shared engineering conventions](https://github.com/HoneyDrunkStudios/HoneyDrunk.Standards/blob/main/HoneyDrunk.Standards/docs/CONVENTIONS.md) and this repository's owning documentation before editing. Apply the parts relevant to this stack; preserve existing public contracts, dependency direction and repository-specific behavior. Verify shared capabilities in current code before reusing them; a catalog entry or scaffold is not an implemented integration.

Work within the selected request. Preserve unrelated changes and use a separate worktree when needed. Review the final diff, use Conventional Commits and ready-for-review PRs with exactly one accurate `Authorship:` line and a `Request:` line; include the authorship in commit trailers. Run meaningful checks for the affected behavior and report the reviewed/tested revision, failures and unrun checks. For documentation-only changes, check links, paths and instruction consistency. Preserve required checks and inspect actual latest-head Sonar new-code findings where analysis applies; do not suppress findings or weaken gates to obtain a pass. Legacy Grid Review is retired; do not restore its workers, queues or bypass labels. A configured replacement reviewer is not evidence of a completed review or enforcing merge check.
