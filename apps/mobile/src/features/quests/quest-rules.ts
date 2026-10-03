import type { Quest, Rank, State } from "../../shared/contracts";
export const ranks: Rank[] = ["F", "E", "D", "C", "B", "A", "S"];
// Local previews mirror the server's approved progression calibration.
const rankRules: Record<
  Rank,
  { minimumLevel: number; rewardMultiplierTenths: number }
> = {
  F: { minimumLevel: 1, rewardMultiplierTenths: 10 },
  E: { minimumLevel: 5, rewardMultiplierTenths: 13 },
  D: { minimumLevel: 10, rewardMultiplierTenths: 17 },
  C: { minimumLevel: 20, rewardMultiplierTenths: 22 },
  B: { minimumLevel: 35, rewardMultiplierTenths: 28 },
  A: { minimumLevel: 50, rewardMultiplierTenths: 35 },
  S: { minimumLevel: 70, rewardMultiplierTenths: 43 },
};
const baseXpByEffort: Record<string, number> = {
  Small: 10,
  Medium: 80,
  Large: 800,
};
const rankMultiplierScale = 10;
export const fullPoolBasisPoints = 10_000;
export const basisPointsPerPercent = 100;
export const questLimits = {
  titleLength: 120,
  criterionLength: 2_000,
  descriptionLength: 2_000,
  skillNameLength: 80,
} as const;
export function eligible(quest: Quest, state: State) {
  const gate = rankRules[quest.rank].minimumLevel;
  return quest.skills.length
    ? quest.skills.every(
        (s) => (state.skills.find((b) => b.id === s.id)?.level ?? 1) >= gate,
      )
    : (state.categories.find((b) => b.id === quest.categoryId)?.level ?? 1) >=
        gate;
}
export function baseXp(rank: Rank, effort: string) {
  return (
    (baseXpByEffort[effort] * rankRules[rank].rewardMultiplierTenths) /
    rankMultiplierScale
  );
}
