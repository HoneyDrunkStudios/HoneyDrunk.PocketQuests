/** Approved v0.6 display curves. XP awards and levels remain server-owned. */
const coefficients = { overall: 100, category: 10, attribute: 25, skill: 5 };
export type ProgressionTrack = keyof typeof coefficients;

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
