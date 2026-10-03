import type { Command, State } from "./contracts";

export type CompletionFeedback = {
  occurrenceId: string;
  completionId: string;
  title: string;
  rewards: { track: string; name: string; xp: number }[];
  levelUps: { track: string; name: string; from: number; to: number }[];
  rankUp: string | null;
  unlocks: string[];
};

/** Read the confirmed event ledger. Pending projections never award rewards. */
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
    item.completion?.id !== command.operationId ||
    before.occurrences.some((o) => o.completion?.id === item.completion?.id)
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
  const groups = {
    Category: [before.categories, after.categories],
    Attribute: [before.attributes, after.attributes],
    Skill: [before.skills, after.skills],
  } as const;
  const levelUps: CompletionFeedback["levelUps"] = [];
  const rewards = entries.map((e) => {
    if (e.track === "Overall") {
      if (after.overallLevel > before.overallLevel)
        levelUps.push({
          track: e.track,
          name: "Overall",
          from: before.overallLevel,
          to: after.overallLevel,
        });
      return { track: e.track, name: "Overall", xp: e.amount };
    }
    const [previous, next] = groups[e.track];
    const balance = next.find((b) => b.id === e.trackId);
    const prior = previous.find((b) => b.id === e.trackId);
    if (balance && prior && balance.level > prior.level)
      levelUps.push({
        track: e.track,
        name: balance.name,
        from: prior.level,
        to: balance.level,
      });
    return { track: e.track, name: balance?.name ?? e.trackId, xp: e.amount };
  });
  return {
    occurrenceId: item.occurrence.id,
    completionId: item.completion.id,
    title: item.occurrence.quest.title,
    rewards,
    levelUps,
    rankUp:
      after.rank.current !== before.rank.current &&
      "FEDCBAS".indexOf(after.rank.current) >
        "FEDCBAS".indexOf(before.rank.current)
        ? after.rank.current
        : null,
    unlocks: after.entitlements
      .filter(
        (e) =>
          e.earned &&
          !before.entitlements.some((b) => b.id === e.id && b.earned),
      )
      .map((e) => e.name),
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
