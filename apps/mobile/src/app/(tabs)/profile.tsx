import { RankProgress } from "../../features/progression/rank-progress";
import { Text, View } from "react-native";
import { Link } from "expo-router";
import { Page } from "../../session/session-page";
import { useAccountSnapshot, useSessionStatus } from "../../session/session";
import { Card, Label, useAppTheme } from "../../shared/ui";
import { CharacterSection } from "../../features/progression/character-section";
import {
  ProgressRow,
  CategoryProgress,
} from "../../features/progression/character-tracks";
import { LevelProgress } from "../../features/progression/level-progress-view";
import { ActivityRecap } from "../../features/progression/activity-recap-view";
import { RewardSummary } from "../../features/progression/reward-summary";

export default function Profile() {
  const { styles, colors } = useAppTheme();
  const { state, catalog } = useAccountSnapshot();
  const { pending, offline } = useSessionStatus();
  if (!state) return null;
  return (
    <Page>
      <View style={{ flexDirection: "row", gap: 12, alignItems: "center" }}>
        <Text accessibilityRole="header" style={[styles.title, { flex: 1 }]}>
          Your story so far
        </Text>
        <Link
          href="/settings"
          accessibilityLabel="Settings and profile"
          style={[
            styles.text,
            { color: colors.gold, padding: 12, minHeight: 48 },
          ]}
        >
          Settings
        </Link>
      </View>
      <Card style={{ gap: 12, borderColor: colors.gold }}>
        <Text style={styles.subtitle}>Level {state.overallLevel}</Text>
        <Label>{state.overallXp} overall XP</Label>
        <LevelProgress
          compact
          name="Overall"
          track="overall"
          xp={state.overallXp}
          level={state.overallLevel}
        />
      </Card>
      <RankProgress
        rank={state.rank}
        categories={state.categories}
        rules={catalog?.rules ?? []}
        compact
      />
      <Link
        href="/wardrobe"
        style={[
          styles.text,
          { color: colors.green, minHeight: 48, paddingVertical: 14 },
        ]}
      >
        Your wardrobe - view and change equipped items
      </Link>
      <Link
        href="/avatar"
        style={[
          styles.text,
          { color: colors.gold, paddingVertical: 12, minHeight: 48 },
        ]}
      >
        Your traveler · Customize appearance
      </Link>
      <ActivityRecap state={state} pending={pending} offline={offline} />
      <RewardSummary />
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
