import { Text, View } from "react-native";
import type { Catalog, Quest, State } from "./contracts";
import { rewardPreview } from "./reward-preview";
import { Label, styles } from "./ui";

export function QuestRewards({
  quest,
  catalog,
  state,
}: {
  quest: Quest;
  catalog: Catalog | null;
  state: State | null;
}) {
  const reward = rewardPreview(quest);
  const names = (
    entries: { id: string; xp: number }[],
    items: { id: string; name: string }[],
  ) =>
    entries
      .map(
        (e) => `${items.find((i) => i.id === e.id)?.name ?? e.id} +${e.xp} XP`,
      )
      .join(" · ");
  return (
    <View style={{ gap: 6 }}>
      <Label>Overall +{reward.overall} XP</Label>
      <Label>
        {catalog?.categories.find((c) => c.id === quest.categoryId)?.name ??
          "Category"}{" "}
        +{reward.category} XP, plus any streak bonus
      </Label>
      <Text style={styles.muted}>
        Attributes:{" "}
        {reward.attributes.length
          ? names(reward.attributes, catalog?.attributes ?? [])
          : "None selected"}
      </Text>
      <Text style={styles.muted}>
        Skills:{" "}
        {reward.skills.length
          ? names(reward.skills, state?.skills ?? catalog?.skills ?? [])
          : "None selected"}
      </Text>
    </View>
  );
}
