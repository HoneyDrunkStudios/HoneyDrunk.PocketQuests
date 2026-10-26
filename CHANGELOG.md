# Changelog

## Optional Expo Go development pairing — October 25, 2026

- Add an explicitly enabled local development host for the actual Identity/API services and dedicated retained SQL databases, with a six-digit pairing code and restart-scoped signing keys.
- Gate the mobile development screen behind a development build and explicit flag; preserve provider sign-in for ordinary and release builds.
- Add a launcher and disposable SQL verification for startup gates, invalid/forged credentials, rate limits and progress retained across backend restart; run that verification in hosted browser CI.
- Cover launcher and cleanup failure paths with measured PowerShell tests, restore absent environment variables correctly, and keep uploaded pairing evidence limited to the safe check summary.
- Keep recurring-quest maintenance enabled so retained development accounts can recover a large delivery backlog.

## Quest discovery — October 25, 2026

- Search available quests by title, topic, category, criterion, attribute and built-in or custom skill, with case and accent matching.
- Combine category, effort, starter/custom, readiness and penalty-free filters; sort by recommendations, XP or title and clear filters in one action.
- Index results locally and show 20 cards at a time, keeping search responsive without network writes and preserving existing start/reward gates.

## Profile preference drafts — October 25, 2026

- Let interest, badge and frame choices stay local until their Save button is pressed; reset drafts without sending a command.
- Keep choices responsive offline, retain failed saves for retry and preserve newer edits when an earlier queued save is confirmed.
- Keep skill correction confirmation open until the matching account update arrives.

## Quiet saves and understandable recovery — October 25, 2026

- Keep ordinary queued saves quiet; show recovery only when offline or changes need review, recovery or an explicit retry.
- Explain unsaved and timing-unverified activities in plain language, with clear discard consequences and optional support details.
- Use readable action names while preserving retry, rejection, dependency and saved-progress behavior.

## Guided onboarding and journal entry — October 25, 2026

- Compose rank decorations with supported Image/View primitives while preserving the shield, backdrop and disclosure control.
- Add an illustrated welcome and dedicated three-step onboarding flow with local interest drafts saved once on Continue.
- Route incomplete profiles into onboarding and completed profiles into the app with explicit, cycle-free route guards.
- Bundle the licensed renamed bold serif face, improve small-screen journal/card readability, and keep sign-in forms above the iOS keyboard.
- Keep provider setup/recovery failures readable without echoing private authentication errors, and document remaining native/provider acceptance.

## Expo SDK 58 compatibility — October 25, 2026

- Install the declared .NET SDK baseline in CI and allow the legacy onboarding save assertions the same bounded wait as browser actions.
- Align the mobile client to Expo SDK 58, React 19.3 and React Native 0.88 for the current iPhone Expo Go host.
- Import the exact reviewed shared UI revision that retains React Native 0.86 support and permits 0.88 hosts.
- Exercise Router's current query parser and retire the decoder dependency/patch it no longer uses; retain the other dependency mitigations and release audit.
- Give the protected root stack sole ownership of sign-in transitions and a real touch target to the settings shortcut, retaining stable navigation through account updates on SDK 58.
- Wait for the complete saved-profile restoration before applying route guards; configure the Home tab anchor using SDK 58 layout settings and retain paused quest routes when opening Home.
- Render the journal's decorative background with supported Image/View primitives instead of deprecated ImageBackground.
- Keep web routes synchronous so development navigation can read the Home anchor; native route loading defaults stay intact.

## Illustrated adventure journal — October 18, 2026

- Apply the approved navy, ivory and gold design with bundled lake scenery, a traveler portrait, serif headings and illustrated path rows.
- Add native navigation icons, a compact expandable rank summary and refreshed quest cards while retaining real balances, details, pending warnings and Undo.
- Keep due days separate from planned times, and preserve virtualized history and explicit recovery actions.


## Compact quest journal — October 18, 2026

- Follow the approved prototype's compact Active/Completed cards, with named detail links, readable headings and one base-XP summary; retain schedules, frozen/pending warnings, accepted losses and current-completion Undo.
- Put the completion criterion and explicit completion action before the reward preview in quest details, preserving optional focus time and existing commands.
- Verify navigation, command guards and warnings in rendered component tests and retain synthetic browser evidence of Active, Completed and detail views.
- Keep timing-unverified completion guidance distinct from other pending actions, and disable destructive recovery sign-out during ordinary in-flight writes while retaining the unreadable-cache recovery escape.

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
