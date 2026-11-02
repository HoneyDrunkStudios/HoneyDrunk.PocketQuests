/** Approved v0.6 display curves. XP awards and levels remain server-owned. */
const coefficients = { overall: 100, category: 10, attribute: 25, skill: 5 };
export type ProgressionTrack = keyof typeof coefficients;

/** Display-only starting fill for a confirmed XP reward. */
export function rewardProgressStart(
  track: ProgressionTrack,
  totalXp: number,
  award: number,
) {
  const previousXp = totalXp - award;
  if (
    !Number.isSafeInteger(award) ||
    award < 0 ||
    !Number.isSafeInteger(previousXp) ||
    previousXp < 0
  )
    return null;
  const previousLevel =
    Math.floor(Math.sqrt(previousXp / coefficients[track])) + 1;
  return levelProgress(track, previousXp, previousLevel);
}

export function levelProgress(
  track: ProgressionTrack,
  xp: number,
  level: number,
) {
  if (
    !Number.isSafeInteger(xp) ||
    xp < 0 ||
    !Number.isSafeInteger(level) ||
    level < 1
  )
    return null;
  const coefficient = coefficients[track];
  const start = coefficient * (level - 1) ** 2;
  const next = coefficient * level ** 2;
  if (!Number.isSafeInteger(next) || xp < start || xp >= next) return null;
  return {
    earned: xp - start,
    required: next - start,
    remaining: next - xp,
    nextLevel: level + 1,
  };
}
