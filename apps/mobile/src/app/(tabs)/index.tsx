import { Text, View } from "react-native";
import { Link, Redirect } from "expo-router";
import { useSession } from "../../session";
import { Page, Label, QuestCard, styles } from "../../ui";
export default function Today() {
  const { state } = useSession();
  if (!state)
    return (
      <Page>
        <Label>Loading your quests…</Label>
      </Page>
    );
  if (!state.profile.onboardingComplete)
    return <Redirect href="/(tabs)/board" />;
  const active = state.occurrences.filter((o) => o.status === "Active");
  const today = active
    .filter((o) => o.occurrence.dueDate === state.today)
    .sort(
      (a, b) =>
        (a.occurrence.plannedTime ?? "99:99").localeCompare(
          b.occurrence.plannedTime ?? "99:99",
        ) || a.occurrence.acceptedAt.localeCompare(b.occurrence.acceptedAt),
    );
  const unscheduled = active.filter((o) => !o.occurrence.dueDate);
  return (
    <Page>
      <Text selectable style={styles.title}>
        A little progress, your way.
      </Text>
      <Text selectable style={styles.muted}>
        {state.today} · {state.zone}
      </Text>
      {today.length ? (
        today.map((o) => <QuestCard key={o.occurrence.id} item={o} />)
      ) : (
        <View style={styles.card}>
          <Label>No quests planned for today.</Label>
          <Link href="/(tabs)/board" style={styles.text}>
            Choose a quest →
          </Link>
        </View>
      )}
      {unscheduled.length > 0 && (
        <>
          <Text selectable style={styles.subtitle}>
            Whenever you’re ready
          </Text>
          {unscheduled.map((o) => (
            <QuestCard key={o.occurrence.id} item={o} />
          ))}
        </>
      )}
      <Text selectable style={styles.subtitle}>
        Category streaks
      </Text>
      {state.streaks.filter((s) => s.days > 0).length ? (
        state.streaks
          .filter((s) => s.days > 0)
          .map((s) => (
            <Label key={s.categoryId}>
              {state.categories.find((c) => c.id === s.categoryId)?.name}:{" "}
              {s.days} {s.days === 1 ? "day" : "days"} ·{" "}
              {s.qualifiedToday ? "Done today" : "Complete today to continue"}
            </Label>
          ))
      ) : (
        <Label>Your first completion starts a category streak.</Label>
      )}
      <View style={styles.card}>
        <Text selectable style={styles.subtitle}>
          Rank {state.rank.current}
        </Text>
        <Label>
          {state.rank.qualifyingCategories}/{state.rank.requirement.count}{" "}
          categories at {state.rank.requirement.floor} XP · {state.rank.total}/
          {state.rank.requirement.total} combined XP toward{" "}
          {state.rank.requirement.rank}
        </Label>
      </View>
    </Page>
  );
}
