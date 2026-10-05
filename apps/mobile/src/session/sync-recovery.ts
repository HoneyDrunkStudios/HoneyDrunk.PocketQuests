import {
  offlineQuestActions,
  questActions,
} from "../features/quests/commands/quest-actions";
import type { Command, State, Catalog } from "../shared/contracts";

export type RejectedCommand = {
  command: Command;
  kind: "rejected" | "blocked";
  reason: string;
  httpStatus?: number;
  blockedBy?: string[];
};

// Only the four bounded offline actions can be proven independent here. Other
// commands can change account-wide terms and must remain an ordering barrier.
export function dependsOn(command: Command, earlier: Command): boolean {
  if (
    !offlineQuestActions.has(command.action) ||
    !offlineQuestActions.has(earlier.action)
  )
    return true;
  if (command.completionId === earlier.operationId) return true;
  const occurrences = [earlier.occurrenceId, earlier.parentId].filter(Boolean);
  if (
    [command.occurrenceId, command.parentId].some(
      (id) => id && occurrences.includes(id),
    )
  )
    return true;
  if (earlier.action === questActions.saveDefinition) {
    const id = earlier.definition?.id;
    return !!id && (command.questId === id || command.definition?.id === id);
  }
  return false;
}

export function rejectionBlockers(
  command: Command,
  rejected: RejectedCommand[],
) {
  return rejected.filter((entry) => dependsOn(command, entry.command));
}

// Quarantine descendants as well as the rejected root. They remain individually
// visible and persisted, and discarding a prerequisite never releases them.
export function isolateRejection(
  queue: Command[],
  rejected: RejectedCommand[],
  failed: Command,
  status: number,
  reason: string,
) {
  const retained: Command[] = [];
  const next: RejectedCommand[] = [
    ...rejected,
    { command: failed, kind: "rejected", httpStatus: status, reason },
  ];
  for (const command of queue) {
    const blockers = rejectionBlockers(command, next);
    if (!blockers.length) retained.push(command);
    else
      next.push({
        command,
        kind: "blocked",
        reason:
          "Not sent because an earlier related action was rejected. Review and discard this action individually before recording a replacement.",
        blockedBy: blockers.map((entry) => entry.command.operationId),
      });
  }
  return { queue: retained, rejected: next };
}

export function rejectedActionLabel(
  command: Command,
  state: State,
  catalog: Catalog,
) {
  const title =
    command.definition?.title ??
    state.occurrences.find(
      (item) => item.occurrence.id === command.occurrenceId,
    )?.occurrence.quest.title ??
    state.definitions.find((item) => item.quest.id === command.questId)?.quest
      .title ??
    catalog.quests.find((quest) => quest.id === command.questId)?.title ??
    command.occurrenceId ??
    command.questId ??
    "Account";
  return `${command.action}: ${title}`;
}
