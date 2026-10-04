# Anchored command ordering

A completion may legitimately be recorded up to five seconds ahead of server receipt time. After showing that completion, a refreshed clock anchor must not let the next Undo precede it. The previous live-state fix exposed the completion immediately but refreshed anchors still used physical time alone, producing an immutable pending Undo that could never pass temporal validation.

## Contract

`ServerUtc` remains the physical server baseline at issuance. `RecordedTimeFloor` is a separate, server-owned ordering floor captured under the account transaction lock, together with the immutable occurrence snapshot. It is never added to elapsed time.

1. Validate anchor ownership, process ID, ordinal, finite/nonnegative/monotonic elapsed time, device wall-clock drift and the existing five-second future limit.
2. Calculate physical action time as `ServerUtc + ElapsedMilliseconds`.
3. Calculate effective action time as `max(physical action time, anchor.RecordedTimeFloor)` and enforce the five-second limit on that result too.
4. Apply unchanged domain validation, including acceptance ordering, exact due-day cutoff and the strict 24-hour Undo boundary.

The floor is immutable for an issued anchor. Later commands, synchronization or wall time cannot change a pending proof's interpretation. Delayed offline actions therefore retain their established timestamps instead of being moved to current receipt time. Receipt lookup still returns the exact saved result before clock validation; mismatched payloads remain rejected.

The account persists `LastRecordedAt` for newly issued anchors and live projection. Proof-free online actions use the current account floor, with the same future bound. Historical event times recover the floor for upgraded accounts. Future deadlines are excluded. Repeated anchor refreshes do not compound an offset; once physical time catches up it advances normally.

## Compatibility and recovery

The DACPAC adds nullable `Accounts.LastRecordedAt` and `SyncAnchors.RecordedTimeFloor` columns. Upgrade the database before running the updated server; no deployment is performed by this change. Existing receipts and event rows remain intact.

Old anchors have no stored floor. Their immutable snapshots already contain accepted, completed and lifecycle timestamps, so the server derives their floor from those visible events and their original physical baseline. This allows the reported pending Undo to recover after upgrade with its original operation ID, ordinal and proof. Existing successful receipts replay unchanged. This is not a general clock-tampering recovery mechanism: invalid ownership, process, ordinal, excessive drift/future claims or expired Undo still require explicit reconciliation.

`POST /api/sync-anchor` adds an optional `recordedTimeFloor` response field. Existing clients can ignore it: the server derives effective time from its stored anchor, never a client-supplied floor. The mobile client persists and retries exactly the same queued proof; it does not retimestamp actions, recreate operation IDs or grant optimistic rewards.

## Regression coverage

SQL tests reproduce zero/+1/+5-second completion lead followed by live read, fresh anchor, immediate Undo, lost-response replay after a fresh context and a subsequent queued command. Additional cases cover future acceptance, repeated refreshes, proof-free Undo, raw-proof/ownership/ordinal/future guards, exact deadline and 24-hour boundaries, and repeat-publishing the initial canonical schema with a pending Undo. The original bug was reproduced first: zero lead passed; +1 and +5 failed at Undo.

The browser journey uses the actual session, API and isolated SQL databases. A temporary bounded monotonic-clock lead causes the client itself to persist the completion proof. The harness restores the physical clock before normal anchor refresh, verifies the next Undo's physical proof precedes the completion, loses the committed Undo response, checks byte-equivalent command data on replay, and issues a subsequent completion/Undo without discarding the queue. SQL context/serialized-queue restart and browser-adapter retry are covered; native encrypted-cache process-death execution remains a separate device acceptance gate.

Current test counts and terminal hosted CI belong in the PR description and handoff so they remain tied to the exact published head.
