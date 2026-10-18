# Changelog

## Compact quest journal — October 18, 2026

- Follow the approved prototype's compact Active/Completed cards, with named detail links, readable headings and one base-XP summary; retain schedules, frozen/pending warnings, accepted losses and current-completion Undo.
- Put the completion criterion and explicit completion action before the reward preview in quest details, preserving optional focus time and existing commands.
- Verify navigation, command guards and warnings in rendered component tests and retain synthetic browser evidence of Active, Completed and detail views.

## Available quest details — October 18, 2026

- Present available quests as compact cards with category, rank, effort and XP, following the approved prototype's list/detail flow.
- Add a navigable quest-and-rewards screen for planning, explicit penalty consent, recurrence and custom-quest management; preserve calendar dates and editor drafts.
- Resolve detail routes against current account data, retain quick Start for ordinary quests, and verify the flow through component regressions and the real browser/API/SQL journey.

## Security and reliability cleanup — October 13, 2026

- Add hash-pinned local braces depth guards and forge DigestAlgorithm validation with failing-before/passing-after security regressions; retain upstream Dependabot alerts and release audit gates until compatible fixes ship.
- Install CI dependencies without third-party lifecycle execution, explicitly apply reviewed patches, and export through the locked Expo CLI.
- Confine schema-contract input to the checkout, avoid PATH-based Git lookup for UI imports, and require explicit HTTPS endpoints for physical-device AppHost access.
- Preserve quest-history timestamps at creation, scope SQL probes to their fixture rows, clarify clock-fixture branches, and fix the timezone render conditional and progression square-root conversion with boundary regressions.

## [Unreleased]

- Show accessible overall, category, attribute and skill XP progress on the character sheet using approved curves and confirmed server balances; completion and Undo refresh the bars.

- Add day/seven-day calendar navigation, strict date selection, per-day quest groups ordered by planned time, and choose/create actions that retain the selected date.
- Group Active quests into today, unscheduled, earlier and upcoming plans; keep missed/abandoned history behind an explicit show/hide control and preserve server-owned lifecycle status.

- Enable CI Sonar analysis with real source-mapped test coverage, preserving validation and secret boundaries.

- Update @expo/metro-file-map to 57.0.4, whose supported watcher implementation removes its micromatch/braces dependency path. Document the remaining Metro and Expo certificate paths, fresh bundle exposure evidence and upstream remediation options.
- Upgrade locked mobile tooling to shell-quote 1.12.0 and source-map-js 1.2.2, fixing the command-injection and indexed source-map advisories. Record the remaining unpatched braces and node-forge release blockers.
- Add validated quest-plan editing, explicit unscheduled/today actions, saved-plan conflict recovery and explanations for locked plans, using the existing persisted plan command.
- Remove universal quest write staging in favor of scoped feature services, typed CRUD and ordinary EF tracking. Preserve atomic replay/history writes and service-owned audit clocks; simplify convention-only EF facets and document repository engineering standards in the root agent guidance.
- Replace serialized account persistence with explicit account-owned relational tables, immutable terms/source history, compact receipts, ownership/rowversion constraints, query-justified indexes and table/column metadata. Keep shared Audit/Outbox ownership unchanged.
- Convert every backend command, read, initialization, anchor, export, maintenance and lifecycle operation to Services/Contracts/Data with named typed handlers, pure Domain rules, explicit mappings and Data-owned transactions. Remove Application and persistence-aware Domain wrappers. Preserve reward rules, wire contracts, late replay and Undo; add retained-term and real-command retry regressions.
- Integrate supported mobile refresh/reauthentication/logout/recovery ownership, encrypted cache recovery, release URL/profile guards, error boundary, reviewed Android clock/pending intent handling and an endpoint-derived typed client with serialization/drift gates.
- Require manual lifecycle settlement and reject automatic-completion/Blob-fallback overrides. Keep acknowledgment publishing broker-only so failed sends remain retryable in the SQL outbox; add local composition regression tests.
- Make welcome and account-recovery sign-in retryable after configuration or provider-discovery failures; bound setup waits and handle interrupted browser prompts.
