import Ionicons from "@expo/vector-icons/Ionicons";
import { useMemo } from "react";
import { Link } from "expo-router";
import { Pressable, Text, View } from "react-native";
import type { Catalog, State } from "../../shared/contracts";
import { Card, Label, useAppTheme } from "../../shared/ui";
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
  compact = false,
}: Readonly<{
  state: State;
  catalog: Catalog | null;
  completedToday: number;
  pending: boolean;
  compact?: boolean;
}>) {
  const { colors, styles } = useAppTheme();
  const plan = useMemo(() => dailyPlan(state), [state]);
  const ideas = useMemo(
    () => (plan.next.length ? [] : dailyIdeas(catalog, state)),
    [catalog, state, plan.next.length],
  );
  if (compact)
    return (
      <View testID="today-quests" style={{ gap: 0 }}>
        <Text
          accessibilityRole="header"
          style={[styles.subtitle, { fontSize: 25 }]}
        >
          {plan.next.length ? "Today's quests" : "Quest ideas for today"}
        </Text>
        {plan.next.map((item) => (
          <Link
            key={item.occurrence.id}
            href={{
              pathname: "/quest/[id]",
              params: { id: item.occurrence.id },
            }}
            asChild
          >
            <Pressable
              accessibilityRole="link"
              accessibilityLabel={`Open planned quest: ${item.occurrence.quest.title}`}
              style={{
                minHeight: 62,
                paddingVertical: 4,
                flexDirection: "row",
                gap: 10,
                alignItems: "center",
                borderBottomWidth: 1,
                borderBottomColor: colors.line,
              }}
            >
              <QuestCardArtwork
                categoryId={item.occurrence.quest.categoryId}
                size={76}
              />
              <View style={{ flex: 1, gap: 3 }}>
                <Text
                  style={[
                    styles.text,
                    { fontFamily: "QuestSansBold", fontSize: 17 },
                  ]}
                >
                  {item.occurrence.quest.title}
                </Text>
                <Text style={styles.muted}>
                  Rank {item.occurrence.quest.rank} ·{" "}
                  <Text style={{ color: colors.ochre }}>
                    {item.occurrence.quest.baseXp} XP
                  </Text>
                </Text>
                <Text style={styles.muted}>{plannedTimeLabel(item)}</Text>
              </View>
              <Ionicons
                name="chevron-forward"
                size={18}
                color={colors.green}
                accessible={false}
              />
            </Pressable>
          </Link>
        ))}
        {!plan.next.length && ideas.length === 0 && (
          <Label style={styles.muted}>
            {completedToday
              ? "A little progress, already made today."
              : "Nothing planned yet. Here are two places to begin."}
          </Label>
        )}
        {!plan.next.length &&
          ideas.slice(0, 2).map((quest) => (
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
                style={{
                  minHeight: 58,
                  paddingVertical: 4,
                  flexDirection: "row",
                  gap: 10,
                  alignItems: "center",
                  borderBottomWidth: 1,
                  borderBottomColor: colors.line,
                }}
              >
                <QuestCardArtwork categoryId={quest.categoryId} size={76} />
                <View style={{ flex: 1, gap: 3 }}>
                  <Text
                    style={[
                      styles.text,
                      { fontFamily: "QuestSansBold", fontSize: 17 },
                    ]}
                  >
                    {quest.title}
                  </Text>
                  <Text style={styles.muted}>
                    Rank {quest.rank} ·{" "}
                    <Text style={{ color: colors.ochre }}>
                      {quest.baseXp} XP
                    </Text>
                  </Text>
                </View>
                <Ionicons
                  name="chevron-forward"
                  size={18}
                  color={colors.green}
                  accessible={false}
                />
              </Pressable>
            </Link>
          ))}
        {(plan.earlier > 0 || plan.paused > 0) && (
          <Link
            href={activeBoard}
            style={[
              styles.text,
              { color: colors.green, paddingVertical: 12, minHeight: 48 },
            ]}
          >
            Review your active and paused quests
          </Link>
        )}
        {pending && (
          <Label style={styles.muted}>
            Your progress updates when changes finish saving.
          </Label>
        )}
        <Link
          href={availableBoard}
          style={[
            styles.text,
            { color: colors.green, minHeight: 48, paddingVertical: 12 },
          ]}
        >
          Browse all quests
        </Link>
      </View>
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
            <Text style={styles.muted}>{plannedTimeLabel(item)} →</Text>
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

function plannedTimeLabel(item: State["occurrences"][number]) {
  if (!item.occurrence.dueDate) return "Unscheduled · whenever it fits";
  return item.occurrence.plannedTime
    ? `Today · ${item.occurrence.plannedTime}`
    : "Today";
}
