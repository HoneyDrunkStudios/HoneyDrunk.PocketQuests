import { questActions } from "../features/quests/commands/quest-actions";
import type { Catalog, Command, State } from "../shared/contracts";
import type { UnverifiedAction } from "./unverified-actions";
// Pending UI never grants authoritative XP. The server replaces this projection
// only after an account-bound command receipt is confirmed.
export function pendingProjection(
  base: State,
  queue: Command[],
  catalog: Catalog,
  unverified: UnverifiedAction[] = [],
): State {
  const state = JSON.parse(JSON.stringify(base)) as State;
  const observations = new Map(
    unverified.map((entry) => [entry.command.operationId, entry.observedUtc]),
  );
  for (const command of [
    ...queue,
    ...unverified.map((entry) => entry.command as Command),
  ]) {
    if (command.action === questActions.saveDefinition && command.definition) {
      const index = state.definitions.findIndex(
        (d) => d.quest.id === command.definition!.id,
      );
      const definition = {
        quest: command.definition,
        revision: (command.expectedRevision ?? 0) + 1,
        archived: false,
        pendingSave: true,
      };
      if (index < 0) state.definitions.push(definition);
      else state.definitions[index] = definition;
    }
    if (command.action === questActions.accept && command.occurrenceId) {
      const quest =
        state.definitions.find((d) => d.quest.id === command.questId)?.quest ??
        catalog.quests.find((q) => q.id === command.questId);
      if (
        quest &&
        !state.occurrences.some((o) => o.occurrence.id === command.occurrenceId)
      )
        state.occurrences.push({
          occurrence: {
            id: command.occurrenceId,
            quest,
            dueDate: command.dueDate ?? null,
            plannedTime: command.plannedTime ?? null,
            deadline: null,
            acceptedAt:
              command.recordedTime?.deviceUtc ??
              observations.get(command.operationId) ??
              "",
            parentId: null,
            lifecycle: null,
          },
          status: "Active",
          completion: null,
          canUndo: false,
          planned: null,
        });
    }
    const item = state.occurrences.find(
      (o) => o.occurrence.id === command.occurrenceId,
    );
    if (item && observations.has(command.operationId))
      item.pendingTimingVerification = true;
    if (
      item &&
      command.action === questActions.complete &&
      item.status === "Active"
    ) {
      item.status = "Completed";
      item.completion = {
        id: command.operationId,
        occurrenceId: item.occurrence.id,
        snapshot: item.occurrence.quest,
        recordedAt:
          command.recordedTime?.deviceUtc ??
          observations.get(command.operationId) ??
          "",
      };
      item.canUndo = true;
      item.pendingCompletion = true;
    }
    if (
      item &&
      command.action === questActions.undo &&
      item.completion?.id === command.completionId
    ) {
      item.status = "Active";
      item.completion = null;
      item.canUndo = false;
      item.pendingCompletion = false;
    }
  }
  return state;
}
