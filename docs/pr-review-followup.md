# PR #1 review follow-up

This follow-up addresses the three independently reproduced P2 findings against `251c77d6cfd45120c90006a53bb8b9bb95187d94`, plus the requested folder organization. It does not change canonical rewards, authentication, deadline/Undo validation, dependency versions or the reviewed UI snapshot.

## Confirmed completion time and immutable receipts

The anchor contract already accepts a recorded time up to five seconds ahead of server receipt time. Previously, the store applied the event at that verified time but projected its receipt earlier, so the receipt permanently omitted its own completion and level-up. Live SQL projections now use the latest committed completion/Undo time or current server time, whichever is later. The before-completion projection uses the same logical point as the new event. Immediate reads therefore agree with the receipt and the client's current-state reconciliation retains its celebration.

Recorded event timestamps remain unchanged. The existing account/anchor/ordinal checks and domain command validation still run; a timestamp at or beyond a deadline remains invalid. This does not backdate a late event to receipt time. Historical command receipts remain immutable, and ordinary reads/no-op repeated completions do not create new outcomes. General domain historical projections are unchanged; the live SQL boundary includes events it has already committed.

SQL regressions cover 0, +1 and +5-second lead at the 90-to-100 overall-XP threshold, immediate current state, exact immutable replay after a new DbContext, payload mismatch, no-op repeats and matching future-skew Undo. Additional boundary cases reject deadline/excessive-skew attempts without completion rows or receipts. Client regressions verify level-up feedback, duplicate receipt reconciliation and post-Undo suppression.

## Definition management and streaks

Quest details expose the existing custom editor, recurrence editor and archive command even when Active/Frozen filtering removes a definition from Available. The controls use the current definition revision and disappear for archived/noncustom definitions. Busy/pending controls preserve disabled semantics. Editors return to quest details in this context; existing Available navigation is unchanged. Completed snapshots, unfinished/future edit eligibility, rank requirements, locked penalty terms and archive/series rules remain server-owned.

Home category rows once again show the category's streak length and whether today qualified, with readable text for one day, multiple days and no current streak. The character-sheet landing and separate Quests views remain intact; no Today quest feed was restored.

SQL regressions exercise Active/Frozen edits, stale revisions, ineligible rank rejection and archive preserving commitments while preventing new use/editing. Component checks cover labeled controls, pending disabled state, archived/system definitions and changing streak data. The browser journey now edits an active custom quest, opens/closes its recurrence editor, archives it without losing the occurrence, completes it and checks the Home streak.

## Folder and tooling organization

Functional corrections are isolated in commit `38529b710f0bffb5c001990eb1d687fbdc3ad8dc`. A separate organization change moves 29 mobile source files and six existing test modules into the folders described in [repository-structure.md](repository-structure.md). Expo route names, backend project/solution paths, generic UI source, fixture ports and artifact paths stay unchanged. A content comparison verified that the moved modules change only relative paths/imports (plus the snapshot test's app-root traversal).

Both test commands discover nested modules using the existing Node runner and emit TAP. Three isolated tooling tests exercise recursive discovery, failure propagation and empty-suite rejection. CommonJS script globals fix the three expanded-lint diagnostics without disabling `no-undef`; the normal lint command now includes those scripts.

## Verification

- Complete solution build: passed, zero warnings/errors in the local build.
- Full .NET unit/API/SQL suite: **74 passed**, zero failed/skipped; `PocketQuests.Tests/TestResults/review-final.trx`. Seven new focused SQL cases also passed separately.
- Mobile suite: **34 passed**, zero failed/skipped, including component/accessibility contracts and tooling cases; `apps/mobile/reports/unit.tap`.
- Lint including scripts, expanded ESLint, TypeScript and twelve-file UI provenance: passed. UI remains exactly `213f3bed78e6609dbc6d900610a5d90c16e55b8c`.
- Android/iOS/web production JavaScript export: passed; `artifacts/export-review/` and `artifacts/export-review.log`. This is not an installed/signed native build.
- Expanded browser journey: passed, including active editing/recurrence/archive, visible streak state, existing response-loss/offline/Undo/persistence checks, and verified fixture cleanup. Inspected 320/430-width Home and completion screenshots remain browser-adapter evidence only.
- Hosted validation: results for the replacement head are recorded in the PR description after publication.

An initial sandbox run could not spawn Node workers and a .NET build shell stalled; the owned shell was stopped and a bounded single-worker build rerun with process access. Enforced C# formatting diagnostics were corrected. Component tests gained an Expo crypto adapter, and runner-fixture subprocesses use an independent Node test environment. A first cold Metro bundle took 16.7 seconds against the old 15-second navigation limit; navigation now has a separate bounded 60-second limit while action timeouts remain at 15 seconds and the wrapper retains its six-minute limit. The response-loss test now awaits the final synchronization-anchor response before checking the celebration, avoiding a race with its asynchronous commit confirmation. No test, lint or security gate was suppressed.

All original scoped file hashes and the original index are preserved in the user's untouched checkout. Inherited dependency audit findings and native/device/provider/reference-image acceptance gaps remain documented in [production-slice-delivery.md](production-slice-delivery.md) and [dependency-security.md](dependency-security.md). Browser/component checks do not close those release gates. No merge, deployment, resource provisioning, credential change or real-user-data operation is included.
