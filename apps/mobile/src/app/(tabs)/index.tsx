import { Text, View } from "react-native";
import { Redirect } from "expo-router";
import { useSession } from "../../session";
import { Page, Label, Card, colors, styles } from "../../ui";
import type { Balance, State } from "../../contracts";

function ProgressRow({ item }: { item: Balance }) {
  return (
    <View
      style={{
        paddingVertical: 8,
        flexDirection: "row",
        flexWrap: "wrap",
        justifyContent: "space-between",
        gap: 8,
      }}
    >
      <Label>{item.name}</Label>
      <Label>
        Level {item.level} · {item.xp} XP
      </Label>
    </View>
  );
}
function CategoryProgress({
  item,
  streak,
}: {
  item: Balance;
  streak?: State["streaks"][number];
}) {
  return (
    <View style={{ gap: 4, paddingBottom: 12 }}>
      <ProgressRow item={item} />
      {streak && (
        <Label>
          {item.name}: {streak.days} {streak.days === 1 ? "day" : "days"} in
          this streak.{" "}
          {streak.qualifiedToday
            ? "Done today."
            : streak.days > 0
              ? "Complete today to continue."
              : "Complete a quest to start a streak."}
        </Label>
      )}
    </View>
  );
}
export default function Home() {
  const { state } = useSession();
  if (!state)
    return (
      <Page>
        <Label>Loading your character.</Label>
      </Page>
    );
  if (!state.profile.onboardingComplete)
    return <Redirect href="/(tabs)/board" />;
  const badge = state.entitlements.find(
    (e) => e.id === state.profile.badgeId && e.earned,
  );
  return (
    <Page>
      <Text accessibilityRole="header" selectable style={styles.title}>
        Your character
      </Text>
      <Label>A record of the things you choose to do.</Label>
      <Card testID="character-sheet" style={{ gap: 20 }}>
        <View
          style={{
            flexDirection: "row",
            flexWrap: "wrap",
            gap: 24,
            alignItems: "center",
          }}
        >
          <View
            accessible
            accessibilityRole="image"
            accessibilityLabel="Character portrait silhouette"
            style={{
              width: 112,
              minHeight: 144,
              borderWidth: 2,
              borderColor: colors.gold,
              borderRadius: 56,
              alignItems: "center",
              justifyContent: "center",
              backgroundColor: colors.paper,
            }}
          >
            <View
              accessible={false}
              style={{
                width: 38,
                height: 38,
                borderRadius: 19,
                backgroundColor: colors.ink,
              }}
            />
            <View
              accessible={false}
              style={{
                width: 64,
                height: 56,
                borderTopLeftRadius: 32,
                borderTopRightRadius: 32,
                marginTop: 8,
                backgroundColor: colors.ink,
              }}
            />
          </View>
          <View style={{ flexGrow: 1, flexShrink: 1, minWidth: 140, gap: 10 }}>
            <Text accessibilityRole="header" style={styles.title}>
              Level {state.overallLevel}
            </Text>
            <Label>{state.overallXp} overall XP earned</Label>
            <Text style={[styles.subtitle, { color: colors.gold }]}>
              Global rank {state.rank.current}
            </Text>
            {badge && <Label>{badge.name}</Label>}
          </View>
        </View>
        {state.rank.current === "S" ? (
          <Label>All ten paths contribute to rank S.</Label>
        ) : (
          <Label>
            Toward rank {state.rank.requirement.rank}:{" "}
            {state.rank.qualifyingCategories}/{state.rank.requirement.count}{" "}
            categories at {state.rank.requirement.floor} XP each, and{" "}
            {state.rank.total}/{state.rank.requirement.total} combined category
            XP.
          </Label>
        )}
      </Card>
      <Text accessibilityRole="header" style={styles.subtitle}>
        Ten paths · Categories
      </Text>
      <Card>
        {state.categories.map((item) => (
          <CategoryProgress
            key={item.id}
            item={item}
            streak={state.streaks.find((s) => s.categoryId === item.id)}
          />
        ))}
      </Card>
      <Text accessibilityRole="header" style={styles.subtitle}>
        Attributes
      </Text>
      <Card>
        {state.attributes.map((item) => (
          <ProgressRow key={item.id} item={item} />
        ))}
      </Card>
      <Text accessibilityRole="header" style={styles.subtitle}>
        Skills
      </Text>
      <Card>
        {state.skills.map((item) => (
          <ProgressRow key={item.id} item={item} />
        ))}
      </Card>
    </Page>
  );
}
