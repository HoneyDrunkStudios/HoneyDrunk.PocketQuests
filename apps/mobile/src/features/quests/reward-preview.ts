import type { Quest, Share } from "../../shared/contracts";

/** Display-only mirror of Progression.Allocate: one fixed pool, stable-ID ties. */
export function allocatedPreview(xp: number, shares: Share[]) {
  if (!shares.length) return [];
  const amounts = shares.map((s) => ({
    id: s.id,
    xp: Math.floor((xp * s.basisPoints) / 10_000),
    remainder: (xp * s.basisPoints) % 10_000,
  }));
  const remaining = xp - amounts.reduce((sum, s) => sum + s.xp, 0);
  const ordered = [...amounts].sort(
    (a, b) =>
      b.remainder - a.remainder || (a.id < b.id ? -1 : a.id > b.id ? 1 : 0),
  );
  for (const entry of ordered.slice(0, remaining)) entry.xp++;
  return amounts.map(({ id, xp }) => ({ id, xp }));
}

export function rewardPreview(quest: Quest) {
  return {
    overall: quest.baseXp,
    category: quest.baseXp,
    attributes: allocatedPreview(quest.baseXp, quest.attributes),
    skills: allocatedPreview(quest.baseXp, quest.skills),
  };
}
