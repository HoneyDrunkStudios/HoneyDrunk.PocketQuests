import { RankInsignia } from "../../shared/rank-insignia";
import { Pressable, Text, View } from "react-native";
import Ionicons from "@expo/vector-icons/Ionicons";
import type { Quest } from "../../shared/contracts";
import { Card, Label, useAppTheme } from "../../shared/ui";
import { QuestCardArtwork } from "./quest-card-artwork";

export function AvailableQuestCard({
  quest,
  categoryName,
  locked,
  onDetails,
}: Readonly<{
  quest: Quest;
  categoryName: string;
  disabled: boolean;
  locked: boolean;
  onStart(): void;
  onDetails(): void;
}>) {
  const { styles, colors } = useAppTheme();
  const hasPenalty = (quest.penaltyPercent ?? 0) > 0;
  return (
    <Card style={{ padding: 0 }}>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={`Quest details: ${quest.title}`}
        accessibilityHint="Read the quest and plan your next step"
        onPress={onDetails}
        style={({ pressed }) => ({
          padding: 16,
          gap: 12,
          opacity: pressed ? 0.8 : 1,
        })}
      >
        <View
          style={{ flexDirection: "row", alignItems: "flex-start", gap: 12 }}
        >
          <View style={{ flex: 1, gap: 8 }}>
            <Text style={[styles.muted, { color: colors.gold, fontSize: 13 }]}>
              {categoryName}
            </Text>
            <Text accessibilityRole="header" style={styles.subtitle}>
              {quest.title}
            </Text>
          </View>
          <QuestCardArtwork categoryId={quest.categoryId} />
        </View>
        <Text numberOfLines={2} style={styles.muted}>
          {quest.criterion}
        </Text>
        <View
          style={{
            flexDirection: "row",
            flexWrap: "wrap",
            alignItems: "center",
            gap: 10,
          }}
        >
          <Text
            style={[styles.text, { color: colors.gold, fontWeight: "700" }]}
          >
            +{quest.baseXp} XP
          </Text>
          <RankInsignia rank={quest.rank} size={24} />
          <Text style={styles.muted}>{quest.effort}</Text>
        </View>
        {locked && <Label>Requires higher progression. Open to review.</Label>}
        {hasPenalty && (
          <Label>
            Penalty quest · review and confirm terms before starting.
          </Label>
        )}
        <View
          style={{
            flexDirection: "row",
            justifyContent: "space-between",
            alignItems: "center",
          }}
        >
          <Text style={[styles.muted, { fontSize: 13 }]}>
            Plan your next step
          </Text>
          <Ionicons
            name="arrow-forward"
            size={20}
            color={colors.gold}
            accessible={false}
          />
        </View>
      </Pressable>
    </Card>
  );
}
