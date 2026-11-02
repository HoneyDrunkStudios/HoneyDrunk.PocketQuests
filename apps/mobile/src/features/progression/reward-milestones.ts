import type { Rank, State } from "../../shared/contracts";
import { ranks } from "../quests/quest-rules";

export type Reward = State["entitlements"][number];

// Presentation copy for the six approved starter triggers. Counts, thresholds,
// rank requirements and earned state always come from the account snapshot.
const completionKinds: Record<string, string> = {
  "PQ-CAT-A01": "custom quest",
  "PQ-CAT-A02": "Rest & Recreation quest",
  "PQ-CAT-B01": "Exploration & Adventure quest",
  "PQ-CAT-B02": "Everyday Life quest",
  "PQ-CAT-F01": "Learning quest",
  "PQ-CAT-F02": "Rest & Recreation quest",
};

export function rewardProgress(reward: Reward, currentRank: Rank) {
  const kind = completionKinds[reward.id] ?? "qualifying quest";
  const remaining = Math.max(0, reward.requiredCount - reward.count);
  const rankMet =
    ranks.indexOf(currentRank) >= ranks.indexOf(reward.requiredRank);
  const requirement = `Complete ${reward.requiredCount} ${kind}${reward.requiredCount === 1 ? "" : "s"}.`;
  let guidance = "Completion requirement met.";
  if (remaining > 0)
    guidance = `${remaining} more ${kind}${remaining === 1 ? "" : "s"} to complete.`;
  if (!rankMet)
    guidance += ` Reach current rank ${reward.requiredRank} or higher.`;
  if (reward.earned) guidance = "You meet this reward’s requirements.";
  return { requirement, guidance, rankMet };
}

/** A browsing suggestion only; never computes or grants an entitlement. */
export function nextReward(rewards: Reward[]) {
  return rewards
    .filter((reward) => !reward.earned)
    .sort((a, b) => {
      const fraction = (reward: Reward) =>
        reward.requiredCount > 0
          ? Math.min(1, reward.count / reward.requiredCount)
          : 1;
      return fraction(b) - fraction(a) || a.id.localeCompare(b.id);
    })[0];
}
