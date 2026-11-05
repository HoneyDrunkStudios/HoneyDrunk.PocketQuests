import { Page } from "../../session/session-page";
import { useMemo } from "react";
import { Text, View } from "react-native";
import { Link, Redirect } from "expo-router";
import { useAccountSnapshot, useSessionStatus } from "../../session/session";
import { Label, Card, colors, styles } from "../../shared/ui";
import { JournalHeading } from "../../shared/journal-heading";
import { ProgressRow, CategoryProgress } from "./character-tracks";
import { RankProgress } from "./rank-progress";
import { CharacterPortrait } from "../profile/character-portrait";
import { LevelProgress } from "./level-progress-view";
import { CharacterSection } from "./character-section";
import { TodayQuests } from "../quests/today-quests";
import { activityRecap } from "./activity-recap";
import { RewardSummary } from "./reward-summary";

export function CharacterHome() {
  const { state, catalog } = useAccountSnapshot();
  const { pending } = useSessionStatus();
  const recap = useMemo(
    () => (state ? activityRecap(state, 7) : null),
    [state],
  );
  if (!state)
    return (
      <Page header={<JournalHeading title="Your character" />}>
        <Label>Loading your character.</Label>
      </Page>
    );
  if (!state.profile.onboardingComplete) return <Redirect href="/onboarding" />;
  const badge = state.entitlements.find(
    (e) => e.id === state.profile.badgeId && e.earned,
  );
  return (
    <Page
      header={
        <JournalHeading
          title={state.profile.characterName ?? "Your character"}
          description="A record of the things you choose to do."
        />
      }
    >
      <Card
        testID="character-sheet"
        style={{ gap: 12, padding: 8, borderColor: colors.gold }}
      >
        <View
          style={{
            flexDirection: "row",
            flexWrap: "wrap",
            gap: 18,
            alignItems: "center",
          }}
        >
          <CharacterPortrait
            portraitId={state.profile.portraitId}
            avatarCode={state.profile.avatarCode}
          />
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
      <Link
        href="/character-setup"
        accessibilityLabel={
          state.profile.characterName
            ? "Edit your character"
            : "Create your character"
        }
        style={[
          styles.text,
          { color: colors.gold, paddingVertical: 12, minHeight: 48 },
        ]}
      >
        {state.profile.characterName
          ? "Edit your character"
          : "Create your character →"}
      </Link>
      <Link
        href="/avatar"
        style={[
          styles.text,
          { color: colors.gold, paddingVertical: 12, minHeight: 48 },
        ]}
      >
        Customize your traveler
      </Link>
      <TodayQuests
        state={state}
        catalog={catalog}
        completedToday={recap?.days.at(-1)?.count ?? 0}
        pending={pending}
      />
      <RewardSummary />
      <RankProgress
        compact
        rank={state.rank}
        categories={state.categories}
        rules={catalog?.rules ?? []}
      />
      <View style={{ gap: 2, paddingHorizontal: 10, paddingTop: 0 }}>
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
