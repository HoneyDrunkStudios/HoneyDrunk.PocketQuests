# Mobile engineering standards

These standards apply to `apps/mobile`. Read its [Expo instructions](../apps/mobile/AGENTS.md) for SDK documentation, package installation and native configuration rules. Backend conventions remain in [engineering standards](engineering-standards.md); they are not a template for React component layers.

## Boundaries and state

- Expo Router files in `src/app` compose screens. Put quest, profile and progression behavior in their feature folders; keep form drafts in their owning component.
- `shared/ui.tsx` adapts the app theme and reviewed HoneyDrunk.UI primitives. Product controls belong in features: `quest-card` owns quest commands/navigation; `session-recovery` owns synchronization and discard presentation. Do not edit vendored UI snapshots to add product behavior.
- The session provider is a React adapter over ordinary typed workflows. `session-runtime` composes a session-local store using React's `useSyncExternalStore`. It owns one authenticated coordinator and one operation gate per provider. Do not generalize this into a workflow/state framework.
- `private-account` owns durable publication and owner guards; `quest-sync` owns clock capture, replay, rejection and confirmed feedback; `account-lifecycle` owns reconnect, explicit discard and account cleanup; `session-requests` owns refresh/previews/export. Each receives explicit typed dependencies. Keep the queue commit protocol in one place.
- `use-session-lifecycle` owns boot cancellation and AppState/web subscriptions. `use-expiry-warnings` owns notification reconciliation and ignores stale results. Every asynchronous effect needs an instance-specific cleanup guard; a shared mounted flag alone cannot distinguish Strict Mode setups.
- Prefer `useAccountSnapshot`, `useSessionActions` or `useSessionStatus` for the data a consumer needs. Actions stay stable; projections are memoized by the local account generation. The compatibility `useSession` hook subscribes to all three, so use it only when all are relevant.
- Use functional components, typed props and ordinary functions/hooks. Avoid interface-per-class layers, a service locator, a generic command engine or a global state library without a demonstrated need.

## API and storage boundaries

- Receive JSON as `unknown`. Generated TypeScript types do not validate runtime data. `api/decode.ts` validates the current OpenAPI subset using the schema artifact emitted by the same generator as the types. The drift check covers both artifacts. Unknown additive properties are accepted; missing required/nested fields, incompatible primitive types and enum values are rejected without coercion.
- Keep input and output shapes distinct, including required nullable output and accepted numeric-string inputs. The real .NET [wire fixtures](../contracts/wire-fixtures.json) are regression evidence, not a parallel handwritten contract. Use [contract verification](api-contract.md) when endpoints change.
- A malformed successful command response is uncertain. Retain the original command and retry its exact operation ID/body/proof; never classify it as a definitive rejection or grant local XP.
- Native credentials and cache keys stay in device-only SecureStore. Cache ciphertext is authenticated to its owner. Browser credentials/cache remain page-memory only. Do not log tokens, export private cache keys or add unconfigured telemetry sinks.
- The encrypted cache envelope is `{ version: 1, account }`. The reader supports legacy flat accounts. Future/unknown versions and unreadable records are retained in private quarantine until explicit discard. Format changes require compatibility and pending-work preservation tests.
- Validate the complete display state/catalog before publication. If the display part is incomplete but the journal is readable, expose `state: null`, `catalog: null`, `requiresReload: true`, retaining queued/rejected/unverified work. Reconnect loads display data before replay. Do not render partial snapshots as valid domain state.
- Save ciphertext first, then commit its SecureStore key/pointer. An ambiguous pointer-write failure must reload the committed generation before another ordinal/queue update. Keep uncertain ciphertext; never erase valid pending work during automatic recovery.

## Offline and account safety

Preserve the existing clock/proof rules and [offline acceptance checks](mobile-release.md). Clock observations without sufficient continuity remain unverified intent; reconnect cannot invent earlier timestamps or rewards. Do not backdate quests. Recurring-series edits preserve an existing future anchor; an already-past anchor requires an explicit valid date today or later.

Capture one operation ID and immutable payload before transmission. Persist queue removal before showing confirmed feedback. A pending Undo suppresses matching feedback even after a lost storage/network response. Isolate definitive rejections without releasing dependent actions. Do not infer independence for account-wide changes.

Single-flight token renewal verifies the same active owner before authorizing requests. Logout invalidates the auth epoch before waiting for in-flight work, then drains local writes before private cleanup. Preserve a durable cleanup-owner marker if cleanup is interrupted. Account switching must not take over retained actions or recovery copies. No new cache/persistence abstraction may bypass these invariants.

## Components, accessibility and performance

Use the app theme, labeled controls, explicit selected/disabled states, readable errors and existing large touch targets. Keep destructive discard wording explicit. Markup/React tests do not establish VoiceOver/TalkBack, focus or device acceptance.

Board/calendar history uses one `SectionList` scroll owner with recovery/header content composed into it. Do not nest a vertical virtualized list in `Page`'s ScrollView. Keep durable/draft data outside recycled rows, use stable occurrence keys and avoid fixed row heights for variable text/font scaling. The automated 1,000-row fixture verifies a bounded initial render; native frame rate/memory still require measurement on a representative device before tuning list windows or adding pagination.

## Verification and release evidence

The jsdom 30 lifecycle test dependency requires Node `^22.22.2 || ^24.15.0 || >=26.0.0`; keep the CI Node 22 installation current.

Run mobile lint, strict TypeScript, `check:ui`, `check:api`, `check:security-patch`, complete `test:ci` and an Expo export/build appropriate to the change. Preserve the deterministic fault-injection suite: it tests workflow races and durable failures but intentionally replaces React hooks. Real React DOM/`act` tests separately cover Strict Mode, cleanup, subscription identity, account switching, logout/sign-in races, stale notifications and cache recovery. Neither suite proves native storage/clock/provider behavior.

Before a release, use [mobile release checks](mobile-release.md) for signed iOS/Android builds, real provider refresh/rotation, device restart/offline continuity, native storage, accessibility, distribution and performance. Record exact head/toolchain and passed, failed and unrun checks separately. Do not substitute a web export for a signed native build or claim live crash reporting from an adapter alone.

Dependency remediation must use compatible supported versions and regression evidence. Keep audit gates active; isolate unqualified transitive patches. Do not suppress advisories or publish packages merely to obtain green checks.

Review the exact intended/staged diff, use Conventional Commits and the task's publication authorization. PR/merge/release holds belong in task evidence, not permanent source rules.
