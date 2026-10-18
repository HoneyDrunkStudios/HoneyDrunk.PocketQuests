import { Text, View } from "react-native";
import type { Quest } from "../../shared/contracts";
import { Button, Card, Label, colors, styles } from "../../shared/ui";

export function AvailableQuestCard({
  quest,
  categoryName,
  disabled,
  locked,
  onStart,
  onDetails,
}: Readonly<{
  quest: Quest;
  categoryName: string;
  disabled: boolean;
  locked: boolean;
  onStart(): void;
  onDetails(): void;
}>) {
  const hasPenalty = (quest.penaltyPercent ?? 0) > 0;
  return (
    <Card style={{ gap: 12 }}>
      <Text style={styles.muted}>{categoryName}</Text>
      <Text accessibilityRole="header" style={styles.subtitle}>
        {quest.title}
      </Text>
      <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 12 }}>
        <Text style={[styles.text, { color: colors.gold, fontWeight: "700" }]}>
          +{quest.baseXp} XP
        </Text>
        <Text style={styles.muted}>
          Rank {quest.rank} · {quest.effort}
        </Text>
      </View>
      {locked && (
        <Label>Requires higher progression. View details to review.</Label>
      )}
      {hasPenalty && (
        <Label>Penalty quest · review and confirm terms before starting.</Label>
      )}
      <Button
        title={
          hasPenalty
            ? `Review before starting: ${quest.title}`
            : `Start: ${quest.title}`
        }
        disabled={disabled || locked}
        onPress={hasPenalty ? onDetails : onStart}
      />
      <Button
        title={`View quest & rewards: ${quest.title}`}
        secondary
        onPress={onDetails}
      />
    </Card>
  );
}
