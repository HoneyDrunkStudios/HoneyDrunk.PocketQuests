import type { Command, State } from "./contracts";

export type CompletionFeedback = {
  occurrenceId: string;
  completionId: string;
  title: string;
  rewards: { track: string; trackId: string; name: string; xp: number }[];
  levelUps: {
    track: string;
    trackId: string;
    name: string;
    from: number;
    to: number;
  }[];
  rankUp: string | null;
  unlocks: string[];
};

/** Called only when acknowledging a queued command, never on a general state refresh. */
export function completionFeedback(
  before: State,
  after: State,
  command: Command,
): CompletionFeedback | null {
  if (command.action !== "complete" || !command.occurrenceId) return null;
  const item = after.occurrences.find(
    (o) => o.occurrence.id === command.occurrenceId,
  );
  if (
    item?.status !== "Completed" ||
    item.completion?.id !== command.operationId
  )
    return null;
  const entries =
    after.ledger?.filter(
      (e) =>
        e.eventId === item.completion!.id &&
        e.occurrenceId === item.occurrence.id &&
        e.amount > 0,
    ) ?? [];
  if (!entries.some((e) => e.track === "Overall")) return null;
  const outcome = after.completionOutcome;
  if (
    outcome &&
    (outcome.completionId !== command.operationId ||
      outcome.occurrenceId !== command.occurrenceId)
  )
    return null;
  // Older receipts can still confirm XP, but cannot establish transaction-local level changes.
  if (
    !outcome &&
    before.occurrences.some((o) => o.completion?.id === command.operationId)
  )
    return null;
  const groups = {
    Category: after.categories,
    Attribute: after.attributes,
    Skill: after.skills,
  } as const;
  const rewards = entries.map((e) => {
    const name =
      e.track === "Overall"
        ? "Overall"
        : (groups[e.track].find((b) => b.id === e.trackId)?.name ?? e.trackId);
    return { track: e.track, trackId: e.trackId, name, xp: e.amount };
  });
  return {
    occurrenceId: item.occurrence.id,
    completionId: item.completion.id,
    title: item.occurrence.quest.title,
    rewards,
    levelUps: outcome?.levelUps ?? [],
    rankUp: outcome?.rankUp ?? null,
    unlocks: outcome?.unlocks.map((e) => e.name) ?? [],
  };
}

export function completionAnnouncement(feedback: CompletionFeedback) {
  return [
    `Quest complete. ${feedback.title}.`,
    ...feedback.rewards.map(
      (r) => `${r.name}: ${r.xp} ${r.track.toLowerCase()} XP earned.`,
    ),
    ...feedback.levelUps.map(
      (l) => `Level up! ${l.name} reached level ${l.to}.`,
    ),
    feedback.rankUp ? `Global rank promoted to ${feedback.rankUp}.` : "",
    ...feedback.unlocks.map((name) => `${name} unlocked.`),
  ]
    .filter(Boolean)
    .join(" ");
}

/** Reconcile acknowledged receipts with current truth before showing a celebration. */
export function survivingFeedback(notices: CompletionFeedback[], state: State) {
  const seen = new Set<string>();
  return notices.filter((notice) => {
    if (
      seen.has(notice.completionId) ||
      !state.occurrences.some(
        (o) =>
          o.status === "Completed" &&
          o.occurrence.id === notice.occurrenceId &&
          o.completion?.id === notice.completionId,
      )
    )
      return false;
    seen.add(notice.completionId);
    return true;
  });
}
