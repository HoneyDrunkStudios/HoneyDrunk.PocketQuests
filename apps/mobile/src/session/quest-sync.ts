import * as Crypto from "expo-crypto";
import { completionNoticeDurationMs } from "../config/client";
import {
  completionFeedback,
  survivingFeedback,
} from "../features/progression/completion-feedback";
import {
  offlineQuestActions,
  questActions,
} from "../features/quests/commands/quest-actions";
import type { Anchor, Command, State } from "../shared/contracts";
import { recordedProof } from "./clock-proof";
import { deviceId } from "./offline-store";
import { RequestError, isDefinitiveRejection } from "./request-error";
import type { SessionInternals } from "./session-runtime";
import { isolateRejection, rejectionBlockers } from "./sync-recovery";
import { unverifiedBlockers } from "./unverified-actions";
type Dependencies = Pick<
  SessionInternals,
  | "clockSource"
  | "client"
  | "storageReady"
  | "auth"
  | "localRef"
  | "publish"
  | "awaitingConfirmation"
  | "setCompletionNotices"
  | "setOffline"
  | "setNeedsSignIn"
  | "inFlight"
  | "setError"
  | "setBusy"
  | "failure"
  | "finishWork"
  | "signingOut"
  | "getOffline"
>;
export function createQuestSync({
  clockSource,
  client,
  storageReady,
  auth,
  localRef,
  publish,
  awaitingConfirmation,
  setCompletionNotices,
  setOffline,
  setNeedsSignIn,
  inFlight,
  setError,
  setBusy,
  failure,
  finishWork,
  signingOut,
  getOffline,
}: Dependencies) {
  const anchorFor = async (): Promise<Anchor> => {
    const device = await deviceId();
    const reading = clockSource.read();
    const anchor = await client(
      "POST /api/sync-anchor",
      {
        deviceId: device,
        bootId: Crypto.randomUUID(),
        deviceUtc: reading.observedUtc,
      },
      undefined,
    );
    return {
      ...anchor,
      monotonic: reading.sample?.elapsedMilliseconds ?? 0,
      clock: reading.sample ?? undefined,
      ordinal: 0,
      lastElapsedMilliseconds: 0,
    };
  };
  const drain = async () => {
    if (!storageReady.current)
      throw new Error(
        "Private storage must be reopened. Restart the app before synchronizing.",
      );
    const active = auth.session;
    if (!active || !localRef.current) return;
    if (localRef.current.requiresReload) {
      const [state, catalog] = await Promise.all([
        client("GET /api/state", undefined, undefined),
        client("GET /api/catalog", undefined, undefined),
      ]);
      await publish({
        ...localRef.current,
        state,
        catalog,
        requiresReload: false,
      });
    }
    while (localRef.current.queue.length) {
      const current = localRef.current;
      if (current.requiresReload)
        throw new Error("Reload history before synchronizing.");
      if (current.userId !== active.userId)
        throw new Error("Pending actions belong to another account.");
      const action = current.queue[0];
      let result: State;
      try {
        result = await client("POST /api/commands", action, undefined);
      } catch (value) {
        if (!isDefinitiveRejection(value)) throw value;
        const rejection = value as RequestError;
        await publish({
          ...current,
          ...isolateRejection(
            current.queue.slice(1),
            current.rejected ?? [],
            action,
            rejection.status,
            rejection.message,
          ),
        });
        continue;
      }
      const feedback = completionFeedback(current.state, result, action);
      await publish({
        ...current,
        state: result,
        queue: current.queue.slice(1),
      });
      if (
        feedback &&
        ![
          ...current.queue,
          ...(current.unverified ?? []).map((entry) => entry.command),
        ].some(
          (c) =>
            c.action === questActions.undo &&
            c.completionId === feedback.completionId,
        )
      ) {
        awaitingConfirmation.current.push(feedback);
      }
      if (action.action === questActions.undo)
        setCompletionNotices((notices) =>
          notices.filter(
            (notice) => notice.completionId !== action.completionId,
          ),
        );
    }
    const state = await client("GET /api/state", undefined, undefined);
    const anchor = await anchorFor();
    const current = localRef.current;
    if (current.requiresReload)
      throw new Error("Reload history before synchronizing.");
    await publish({ ...current, state, anchor });
    // An Undo can arrive after its completion was acknowledged but before a
    // failed refresh delivered the feedback. Check durable pending intent at
    // delivery too, including writes that committed before their response failed.
    const pendingUndos = new Set(
      [
        ...localRef.current.queue,
        ...(localRef.current.unverified ?? []).map((entry) => entry.command),
      ]
        .filter((command) => command.action === questActions.undo)
        .map((command) => command.completionId),
    );
    // A replayed receipt is historical. Confirm its completion still survives before displaying it.
    const acknowledged = awaitingConfirmation.current;
    awaitingConfirmation.current = [];
    setCompletionNotices((notices) =>
      survivingFeedback([...notices, ...acknowledged], state)
        .filter((notice) => !pendingUndos.has(notice.completionId))
        .map((notice) => ({
          ...notice,
          until:
            notices.find((prior) => prior.completionId === notice.completionId)
              ?.until ?? Date.now() + completionNoticeDurationMs,
        })),
    );
    setOffline(false);
    setNeedsSignIn(false);
  };
  async function command(input: Omit<Command, "operationId">) {
    const current = localRef.current;
    if (!auth.session || !current || inFlight.current) return;
    if (current.requiresReload) {
      setError(
        "Reconnect to reload your history before recording another action. Existing pending actions are retained.",
      );
      return;
    }
    if (!storageReady.current) {
      setError(
        "Private storage must be reopened. Restart the app before recording another action.",
      );
      return;
    }
    if (!getOffline() && current.queue.length) {
      setError(
        "Synchronize or review the recorded actions before adding another change.",
      );
      return;
    }
    if (
      rejectionBlockers({ ...input, operationId: "" }, current.rejected ?? [])
        .length
    ) {
      setError(
        "Review the rejected actions for this quest before recording another related change. Each retained action can be discarded individually.",
      );
      return;
    }
    if (getOffline() && !offlineQuestActions.has(input.action)) {
      setError(
        "Reconnect to change schedules, profile settings or existing plans.",
      );
      return;
    }
    inFlight.current = true;
    setBusy(true);
    setError(null);
    try {
      const anchor = current.anchor;
      const device = await deviceId();
      const reading = clockSource.read();
      const timing = recordedProof(anchor, reading, device);
      const item: Omit<Command, "recordedTime"> = {
        ...input,
        operationId: Crypto.randomUUID(),
        occurrenceId:
          input.action === questActions.accept
            ? (input.occurrenceId ?? Crypto.randomUUID())
            : input.occurrenceId,
      };
      // Input is not a trusted source of recordedTime, including for unverified
      // intents. Only recordedProof may create a wire proof for a fresh action.
      delete (item as Command).recordedTime;
      const blockers = unverifiedBlockers(item, current.unverified ?? []);
      if (!timing.proof || blockers.length) {
        const reason = blockers.length
          ? "An earlier related action is still awaiting timing verification. This dependent action is also retained pending verification."
          : timing.reason!;
        await publish({
          ...current,
          unverified: [
            ...(current.unverified ?? []),
            {
              command: item,
              observedUtc: reading.observedUtc,
              clock: reading.sample,
              reason,
              blockedBy: blockers.map((entry) => entry.command.operationId),
            },
          ],
        });
        if (item.action === questActions.undo)
          setCompletionNotices((notices) =>
            notices.filter(
              (notice) => notice.completionId !== item.completionId,
            ),
          );
        setError(reason);
        return;
      }
      const proven: Command = { ...item, recordedTime: timing.proof };
      await publish({
        ...current,
        queue: [...current.queue, proven],
        anchor: {
          ...anchor!,
          ordinal: timing.proof.ordinal,
          lastElapsedMilliseconds: timing.proof.elapsedMilliseconds,
        },
      });
      if (item.action === questActions.undo)
        setCompletionNotices((notices) =>
          notices.filter((notice) => notice.completionId !== item.completionId),
        );
      if (!getOffline()) await drain();
      else
        setError(
          "Recorded on this device; progression will be confirmed when synchronized. Clock changes may require reconciliation.",
        );
    } catch (value) {
      failure(value);
    } finally {
      finishWork();
      setBusy(false);
    }
  }
  async function discardUnverified(operationId: string) {
    if (inFlight.current || signingOut.current || !localRef.current) return;
    inFlight.current = true;
    setBusy(true);
    try {
      await publish({
        ...localRef.current,
        unverified: (localRef.current.unverified ?? []).filter(
          (entry) => entry.command.operationId !== operationId,
        ),
      });
      setError(
        "Selected action discarded. Dependent actions remain pending verification; unrelated work is preserved.",
      );
    } catch (value) {
      failure(value);
    } finally {
      finishWork();
      setBusy(false);
    }
  }
  async function discardRejected(operationId: string) {
    if (inFlight.current || signingOut.current || !localRef.current) return;
    inFlight.current = true;
    setBusy(true);
    try {
      await publish({
        ...localRef.current,
        rejected: (localRef.current.rejected ?? []).filter(
          (entry) => entry.command.operationId !== operationId,
        ),
      });
      setError(
        "Selected action discarded. Other recorded and blocked actions remain on this device.",
      );
    } catch (value) {
      failure(value);
    } finally {
      finishWork();
      setBusy(false);
    }
  }
  return { anchorFor, drain, command, discardUnverified, discardRejected };
}
