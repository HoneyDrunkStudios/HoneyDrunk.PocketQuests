import { Page } from "../../session/session-page";
import { Text, View } from "react-native";
import { Redirect } from "expo-router";
import { useAccountSnapshot } from "../../session/session";
import { Label, Card, colors, styles } from "../../shared/ui";
import { ProgressRow, CategoryProgress } from "./character-tracks";
import { RankProgress } from "./rank-progress";
import { CharacterPortrait } from "../profile/character-portrait";

export function CharacterHome() {
  const { state, catalog } = useAccountSnapshot();
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
          <CharacterPortrait />
        </View>
      </Card>
      <RankProgress
        rank={state.rank}
        categories={state.categories}
        rules={catalog?.rules ?? []}
      />
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
