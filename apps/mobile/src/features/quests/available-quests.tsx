import { questActions } from "./commands/quest-actions";
import { useState } from "react";
import { Text, View } from "react-native";
import { useSession } from "../../session/session";
import { Button, Label, styles } from "../../shared/ui";
import { QuestEditor } from "./quest-editor";
import { eligible } from "./quest-rules";
import { SeriesEditor } from "./series-editor";
import { AvailableQuestCard } from "./available-quest-card";
import { availableQuests } from "./quest-availability";
import { Redirect, useRouter } from "expo-router";
import type { Quest, Series, Definition } from "../../shared/contracts";

export function AvailableQuests({
  initialDate = "",
  initialCreate = false,
}: Readonly<{ initialDate?: string; initialCreate?: boolean }> = {}) {
  const { catalog, state, command, busy, pending } = useSession();
  const [recurring, setRecurring] = useState<{
    quest: Quest;
    initial?: Series;
  } | null>(null);
  const [editing, setEditing] = useState<Definition | "new" | null>(
    initialCreate ? "new" : null,
  );
  const [category, setCategory] = useState<string | null>(null);
  const router = useRouter();
  if (state && !state.profile.onboardingComplete)
    return <Redirect href="/onboarding" />;
  if (editing)
    return (
      <QuestEditor
        key={editing === "new" ? "new" : editing.quest.id}
        initial={editing === "new" ? undefined : editing}
        onClose={() => setEditing(null)}
      />
    );
  if (recurring)
    return (
      <SeriesEditor
        key={recurring.initial?.id ?? recurring.quest.id}
        {...recurring}
        onClose={() => setRecurring(null)}
      />
    );
  const quests = availableQuests(catalog, state);
  const visibleQuests = quests.filter(
    (quest) => !category || quest.categoryId === category,
  );
  return (
    <View style={{ gap: 20 }}>
      <Text accessibilityRole="header" style={styles.title}>
        Pick your next quest
      </Text>
      <Label>
        Ten paths. All worth exploring. Your interests appear first.
      </Label>
      <Button title="Create a custom quest" onPress={() => setEditing("new")} />
      <Label>
        Start plans a quest for {initialDate || state?.today} in {state?.zone}.
        Open quest details to change its date, leave it unscheduled or set
        recurrence.
      </Label>
      <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 8 }}>
        <Button
          title="All categories"
          onPress={() => setCategory(null)}
          secondary={category !== null}
        />
        {catalog?.categories.map((item) => (
          <Button
            key={item.id}
            title={item.name}
            onPress={() => setCategory(item.id)}
            secondary={category !== item.id}
          />
        ))}
      </View>
      {visibleQuests.length === 0 && (
        <Label>
          No available quests in this selection. Choose another category or
          create a custom quest.
        </Label>
      )}
      {visibleQuests.map((quest) => (
        <AvailableQuestCard
          key={quest.id}
          quest={quest}
          categoryName={
            catalog?.categories.find((item) => item.id === quest.categoryId)
              ?.name ?? quest.categoryId
          }
          disabled={busy || pending || !state}
          locked={!!state && !eligible(quest, state)}
          onDetails={() =>
            router.push({
              pathname: "/quest/available/[id]",
              params: {
                id: quest.id,
                ...(initialDate ? { date: initialDate } : {}),
              },
            })
          }
          onStart={() =>
            void command({
              action: questActions.accept,
              questId: quest.id,
              dueDate: initialDate || state?.today,
              plannedTime: null,
            })
          }
        />
      ))}
      {!!state?.schedule.series.length && (
        <Text accessibilityRole="header" style={styles.subtitle}>
          Recurring plans
        </Text>
      )}
      {state?.schedule.series.map((series) => (
        <View key={series.id} style={styles.card}>
          <Label>
            {series.quest.title}: every {series.interval}{" "}
            {series.cadence.toLowerCase()} from {series.anchor}.{" "}
            {series.stopped ? "Stopped" : "Running"}.{" "}
            {series.autoAcceptPenalty
              ? "Penalty auto-acceptance on"
              : "No automatic penalty acceptance"}
            .
          </Label>
          {!series.stopped && (
            <>
              <Button
                title={`Edit schedule: ${series.quest.title}`}
                secondary
                onPress={() =>
                  setRecurring({ quest: series.quest, initial: series })
                }
              />
              <Label>
                Stopping ends future deliveries and freezes incomplete accepted
                commitments. Resume those individually or abandon them
                explicitly.
              </Label>
              <Button
                title={`Stop series: ${series.quest.title}`}
                secondary
                disabled={busy || pending}
                onPress={() =>
                  void command({
                    action: questActions.stopSeries,
                    seriesId: series.id,
                  })
                }
              />
            </>
          )}
        </View>
      ))}
    </View>
  );
}
