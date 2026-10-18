import { Page } from "../../session/session-page";
import { Text, View } from "react-native";
import { Redirect } from "expo-router";
import { useAccountSnapshot } from "../../session/session";
import { Label, Card, colors, styles } from "../../shared/ui";
import { JournalHeading } from "../../shared/journal-heading";
import { ProgressRow, CategoryProgress } from "./character-tracks";
import { RankProgress } from "./rank-progress";
import { CharacterPortrait } from "../profile/character-portrait";
import { LevelProgress } from "./level-progress-view";
import { CharacterSection } from "./character-section";

export function CharacterHome() {
  const { state, catalog } = useAccountSnapshot();
  if (!state)
    return (
      <Page header={<JournalHeading title="Your character" />}>
        <Label>Loading your character.</Label>
      </Page>
    );
  if (!state.profile.onboardingComplete)
    return <Redirect href="/(tabs)/board" />;
  const badge = state.entitlements.find(
    (e) => e.id === state.profile.badgeId && e.earned,
  );
  return (
    <Page
      header={
        <JournalHeading
          title="Your character"
          description="A record of the things you choose to do."
        />
      }
    >
      <Card
        testID="character-sheet"
        style={{ gap: 12, padding: 12, borderColor: colors.gold }}
      >
        <View
          style={{
            flexDirection: "row",
            flexWrap: "wrap",
            gap: 18,
            alignItems: "center",
          }}
        >
          <CharacterPortrait />
          <View
            style={{
              flexBasis: 140,
              flexGrow: 1,
              flexShrink: 1,
              minWidth: 140,
              gap: 6,
            }}
          >
            <Text
              accessibilityRole="header"
              style={[styles.title, { fontSize: 28 }]}
            >
              Level {state.overallLevel}
            </Text>
            <Label style={styles.muted}>
              {state.overallXp.toLocaleString()} overall XP
            </Label>
            <LevelProgress
              compact
              name="Overall"
              track="overall"
              xp={state.overallXp}
              level={state.overallLevel}
            />
            {badge && <Label>{badge.name}</Label>}
          </View>
        </View>
      </Card>
      <RankProgress
        compact
        rank={state.rank}
        categories={state.categories}
        rules={catalog?.rules ?? []}
      />
      <View style={{ gap: 6, paddingHorizontal: 10, paddingTop: 8 }}>
        <Text
          accessibilityRole="header"
          style={[styles.title, { fontSize: 26 }]}
        >
          Your paths
        </Text>
        <Text style={styles.muted}>
          Explore and grow different parts of your life.
        </Text>
      </View>
      <CharacterSection
        title="Categories"
        description="The purpose of your adventures"
        count={state.categories.length}
      >
        {state.categories.map((item) => (
          <CategoryProgress
            key={item.id}
            item={item}
            streak={state.streaks.find((s) => s.categoryId === item.id)}
          />
        ))}
      </CharacterSection>
      <CharacterSection
        title="Attributes"
        description="Your core strengths"
        count={state.attributes.length}
      >
        {state.attributes.map((item) => (
          <ProgressRow key={item.id} item={item} track="attribute" />
        ))}
      </CharacterSection>
      <CharacterSection
        title="Skills"
        description="Your specific practices"
        count={state.skills.length}
      >
        {state.skills.map((item) => (
          <ProgressRow key={item.id} item={item} track="skill" />
        ))}
      </CharacterSection>
    </Page>
  );
}
