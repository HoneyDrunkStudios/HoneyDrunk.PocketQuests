import { useMemo } from "react";
import { Link } from "expo-router";
import { Pressable, Text, View } from "react-native";
import type { Catalog, State } from "../../shared/contracts";
import { Card, Label, colors, styles } from "../../shared/ui";
import { calendarDateLabel } from "./calendar-plan";
import { dailyIdeas, dailyPlan } from "./daily-plan";
import { QuestCardArtwork } from "./quest-card-artwork";

const activeBoard = {
  pathname: "/(tabs)/board",
  params: { view: "Active" },
} as const;
const availableBoard = {
  pathname: "/(tabs)/board",
  params: { view: "Available" },
} as const;

/** Public Home composition: browse current commitments without issuing commands. */
export function TodayQuests({
  state,
  catalog,
  completedToday,
  pending,
}: Readonly<{
  state: State;
  catalog: Catalog | null;
  completedToday: number;
  pending: boolean;
}>) {
  const plan = useMemo(() => dailyPlan(state), [state]);
  const ideas = useMemo(
    () => (plan.next.length ? [] : dailyIdeas(catalog, state)),
    [catalog, state, plan.next.length],
  );
  return (
    <Card
      testID="today-quests"
      style={{ gap: 16, borderTopWidth: 3, borderTopColor: colors.gold }}
    >
      <View style={{ gap: 4 }}>
        <Text accessibilityRole="header" style={styles.subtitle}>
          Your day
        </Text>
        <Label style={styles.muted}>{calendarDateLabel(state.today)}</Label>
      </View>
      <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 18 }}>
        <View style={{ flex: 1, minWidth: 100 }}>
          <Text style={[styles.title, { color: colors.gold }]}>
            {plan.today.length}
          </Text>
          <Label>Planned today</Label>
        </View>
        <View style={{ flex: 1, minWidth: 100 }}>
          <Text style={[styles.title, { color: colors.green }]}>
            {completedToday}
          </Text>
          <Label>Completed today</Label>
        </View>
      </View>
      {pending && (
        <Label style={styles.muted}>
          Your completed count updates after changes finish saving.
        </Label>
      )}
      {plan.next.map((item) => (
        <Link
          key={item.occurrence.id}
          href={{ pathname: "/quest/[id]", params: { id: item.occurrence.id } }}
          asChild
        >
          <Pressable
            accessibilityRole="link"
            accessibilityLabel={`Open planned quest: ${item.occurrence.quest.title}`}
            style={({ pressed }) => ({
              paddingVertical: 12,
              minHeight: 64,
              borderTopWidth: 1,
              borderTopColor: colors.line,
              gap: 6,
              opacity: pressed ? 0.75 : 1,
            })}
          >
            <Text style={[styles.text, { fontWeight: "600" }]}>
              {item.occurrence.quest.title}
            </Text>
            <Text style={styles.muted}>
              {item.occurrence.dueDate
                ? `Today${item.occurrence.plannedTime ? ` · ${item.occurrence.plannedTime}` : ""}`
                : "Unscheduled · whenever it fits"}{" "}
              →
            </Text>
          </Pressable>
        </Link>
      ))}
      {plan.earlier > 0 && (
        <Label>
          {plan.earlier} earlier {plan.earlier === 1 ? "plan is" : "plans are"}{" "}
          ready to review.
        </Label>
      )}
      {plan.paused > 0 && (
        <Label style={styles.muted}>
          {plan.paused} {plan.paused === 1 ? "quest is" : "quests are"} paused.
        </Label>
      )}
      {plan.upcoming > 0 && (
        <Label style={styles.muted}>
          {plan.upcoming} {plan.upcoming === 1 ? "quest is" : "quests are"}{" "}
          planned for later.
        </Label>
      )}
      {(plan.next.length > 0 ||
        plan.earlier > 0 ||
        plan.paused > 0 ||
        plan.upcoming > 0) && (
        <Link
          href={activeBoard}
          style={[
            styles.text,
            { color: colors.gold, minHeight: 48, paddingVertical: 12 },
          ]}
        >
          See your active quests
        </Link>
      )}
      {!plan.next.length && (
        <Label>
          {completedToday
            ? "You’ve made room for progress today. Take a breath, or find another small adventure."
            : "Start with something that fits your day. One small quest is enough."}
        </Label>
      )}
      {!plan.next.length && ideas.length > 0 && (
        <View style={{ gap: 8 }}>
          <Text accessibilityRole="header" style={styles.subtitle}>
            A few ideas
          </Text>
          <Label style={styles.muted}>
            {state.profile.interests.length
              ? "Small quests from your interests and current skills."
              : "A few small quests you can start. Open one to make a plan."}
          </Label>
          {ideas.map((quest) => (
            <Link
              key={quest.id}
              href={{
                pathname: "/quest/available/[id]",
                params: { id: quest.id },
              }}
              asChild
            >
              <Pressable
                accessibilityRole="link"
                accessibilityLabel={`Plan a suggested quest: ${quest.title}`}
                style={({ pressed }) => ({
                  minHeight: 72,
                  paddingVertical: 12,
                  borderTopWidth: 1,
                  borderTopColor: colors.line,
                  flexDirection: "row",
                  alignItems: "center",
                  gap: 12,
                  opacity: pressed ? 0.75 : 1,
                })}
              >
                <View style={{ flex: 1, gap: 6 }}>
                  <Text style={[styles.text, { fontWeight: "600" }]}>
                    {quest.title}
                  </Text>
                  <Text style={styles.muted}>
                    {catalog?.categories.find(
                      (item) => item.id === quest.categoryId,
                    )?.name ?? "A small adventure"}{" "}
                    · +{quest.baseXp} XP
                  </Text>
                </View>
                <QuestCardArtwork categoryId={quest.categoryId} />
              </Pressable>
            </Link>
          ))}
        </View>
      )}
      <Link
        href="/(tabs)/explore"
        style={[
          styles.text,
          { color: colors.gold, minHeight: 48, paddingVertical: 12 },
        ]}
      >
        Find a place to explore
      </Link>
      <Link
        href={availableBoard}
        style={[
          styles.text,
          { color: colors.gold, minHeight: 48, paddingVertical: 12 },
        ]}
      >
        Browse all quests
      </Link>
    </Card>
  );
}
