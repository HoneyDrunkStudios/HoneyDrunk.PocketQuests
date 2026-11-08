import { useState } from "react";
import { Link } from "expo-router";
import { Text, View } from "react-native";
import { useAccountSnapshot } from "../../session/session";
import type { State } from "../../shared/contracts";
import { Button, Card, Label, Progress, useAppTheme } from "../../shared/ui";
import { rewardCosmetics, roomStyles } from "../profile/avatar-look";
import {
  milestoneKind,
  rewardProgress,
  type Reward,
} from "./reward-milestones";

function RewardCard({
  reward,
  state,
}: Readonly<{ reward: Reward; state: State }>) {
  const { colors, styles } = useAppTheme();
  const progress = rewardProgress(reward, state.rank.current);
  const cosmetics = rewardCosmetics(reward.id);
  const milestone = milestoneKind(reward.id);
  const selected =
    reward.earned &&
    ((reward.kind === "Badge" && state.profile.badgeId === reward.id) ||
      (reward.kind === "Frame" && state.profile.frameId === reward.id));
  return (
    <Card
      style={{
        borderColor: reward.earned ? colors.gold : colors.line,
        gap: 12,
      }}
    >
      <View style={{ flexDirection: "row", gap: 12, alignItems: "center" }}>
        <View
          accessible={false}
          style={{
            width: 48,
            height: 48,
            borderRadius: 24,
            borderWidth: 1,
            borderColor: reward.earned ? colors.gold : colors.line,
            alignItems: "center",
            justifyContent: "center",
            backgroundColor: colors.paper,
          }}
        >
          <Text
            accessible={false}
            style={{
              fontSize: 26,
              color: reward.earned ? colors.gold : colors.muted,
            }}
          >
            {reward.earned ? "✓" : "✦"}
          </Text>
        </View>
        <Text accessibilityRole="header" style={[styles.subtitle, { flex: 1 }]}>
          {reward.name}
        </Text>
      </View>
      <Label>
        {milestone ? `${milestone} milestone` : reward.kind} ·{" "}
        {reward.earned ? "Earned" : "In progress"}
        {selected ? " · Selected" : ""}
      </Label>
      <Label>{progress.requirement}</Label>
      {cosmetics.length > 0 && <Label>Unlocks {cosmetics.join(", ")}</Label>}
      {milestone !== "Rank" && (
        <Progress
          label={
            milestone === "Level"
              ? "Overall level"
              : `${reward.name} completions`
          }
          value={Math.min(reward.count, reward.requiredCount)}
          max={reward.requiredCount}
          valueText={
            milestone === "Level"
              ? `Level ${reward.count}. Unlocks at level ${reward.requiredCount}.`
              : `${reward.count} of ${reward.requiredCount} qualifying completions`
          }
        />
      )}
      {!milestone && reward.requiredRank !== "F" && (
        <Label>
          Current rank {reward.requiredRank} or higher:{" "}
          {progress.rankMet ? "met" : "not yet met"}. Your rank:{" "}
          {state.rank.current}.
        </Label>
      )}
      <Label>{progress.guidance}</Label>
      {cosmetics.length > 0 && reward.earned && (
        <Link
          href={milestone === "Level" ? "/room" : "/wardrobe"}
          style={[
            styles.text,
            { color: colors.gold, minHeight: 48, paddingVertical: 12 },
          ]}
        >
          {milestone === "Level"
            ? "Decorate your room"
            : "Try this in your wardrobe"}
        </Link>
      )}
      {reward.earned &&
        roomStyles.some((room) => room.rewardId === reward.id) && (
          <Link
            href="/room"
            style={[
              styles.text,
              { color: colors.gold, minHeight: 48, paddingVertical: 12 },
            ]}
          >
            Visit your unlocked room
          </Link>
        )}
    </Card>
  );
}

function LoadedJournal({ state }: Readonly<{ state: State }>) {
  const { styles, colors } = useAppTheme();
  const [filter, setFilter] = useState("All rewards");
  const rewards = state.entitlements
    .filter(
      (reward) =>
        filter === "All rewards" ||
        (filter === "Earned" ? reward.earned : !reward.earned),
    )
    .sort(
      (a, b) => Number(!!milestoneKind(b.id)) - Number(!!milestoneKind(a.id)),
    );
  const earned = state.entitlements.filter((reward) => reward.earned).length;
  return (
    <View testID="reward-journal" style={{ gap: 12 }}>
      <Text accessibilityRole="header" style={styles.subtitle}>
        Your reward journal
      </Text>
      <Label>
        {earned} of {state.entitlements.length} rewards earned
      </Label>
      <Link
        href="/wardrobe"
        style={[
          styles.text,
          { color: colors.gold, minHeight: 48, paddingVertical: 12 },
        ]}
      >
        Open your wardrobe
      </Link>
      <Label>
        Level up to earn room decorations. Rank up to unlock outfits and rooms.
        Your custom quests earn XP toward both.
      </Label>
      <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 8 }}>
        {["All rewards", "In progress", "Earned"].map((option) => (
          <Button
            key={option}
            title={option}
            secondary={filter !== option}
            accessibilityLabel={`${filter === option ? "Selected: " : ""}${option}`}
            onPress={() => setFilter(option)}
          />
        ))}
      </View>
      <Text accessibilityLiveRegion="polite" style={styles.muted}>
        {rewards.length} {rewards.length === 1 ? "reward" : "rewards"} shown
      </Text>
      {rewards.map((reward) => (
        <RewardCard key={reward.id} reward={reward} state={state} />
      ))}
      {rewards.length === 0 && (
        <Label>
          {filter === "Earned"
            ? "No rewards earned yet. Explore All rewards to find a milestone you like."
            : "No rewards to show in this view."}
        </Label>
      )}
    </View>
  );
}

export function RewardJournal() {
  const { state } = useAccountSnapshot();
  return state ? <LoadedJournal state={state} /> : null;
}
