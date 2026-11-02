import { Link } from "expo-router";
import { Text } from "react-native";
import { useAccountSnapshot } from "../../session/session";
import { Card, Label, colors, styles } from "../../shared/ui";
import { nextReward, rewardProgress } from "./reward-milestones";

export function RewardSummary() {
  const { state } = useAccountSnapshot();
  if (!state || state.entitlements.length === 0) return null;
  const earned = state.entitlements.filter((reward) => reward.earned).length;
  const next = nextReward(state.entitlements);
  return (
    <Card>
      <Text accessibilityRole="header" style={styles.subtitle}>
        Your milestones
      </Text>
      <Label>
        {earned} of {state.entitlements.length} rewards earned
      </Label>
      {next ? (
        <Label>
          Keep growing: {next.name}.{" "}
          {rewardProgress(next, state.rank.current).guidance}
        </Label>
      ) : (
        <Label>You’ve earned every starter reward.</Label>
      )}
      <Link
        href="/rewards"
        accessibilityLabel="Open your reward journal"
        style={[
          styles.text,
          {
            color: colors.gold,
            minHeight: 48,
            paddingVertical: 14,
            textDecorationLine: "underline",
          },
        ]}
      >
        Open your reward journal
      </Link>
    </Card>
  );
}
