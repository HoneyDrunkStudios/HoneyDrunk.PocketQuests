import { Link, useLocalSearchParams } from "expo-router";
import { useAccountSnapshot } from "../../../session/session";
import { Page } from "../../../session/session-page";
import { Label, colors, styles } from "../../../shared/ui";
import { AvailableQuests } from "../../../features/quests/available-quests";

export default function DestinationQuests() {
  const { categoryId } = useLocalSearchParams<{
    categoryId?: string | string[];
  }>();
  const { catalog, state } = useAccountSnapshot();
  const category =
    typeof categoryId === "string"
      ? catalog?.categories.find((entry) => entry.id === categoryId)
      : undefined;
  return (
    <Page>
      <Link
        href="/(tabs)/explore"
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
        Back to Explore
      </Link>
      {!catalog || !state ? (
        <Label>Loading your quest catalog…</Label>
      ) : category ? (
        <AvailableQuests key={category.id} initialCategoryId={category.id} />
      ) : (
        <Label>
          This category is unavailable. Choose another outing in Explore.
        </Label>
      )}
    </Page>
  );
}
