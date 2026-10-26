import { questActions } from "./commands/quest-actions";
import { useMemo, useState } from "react";
import { Text, View } from "react-native";
import { useSession } from "../../session/session";
import { Button, Label, styles } from "../../shared/ui";
import { QuestEditor } from "./quest-editor";
import { SeriesEditor } from "./series-editor";
import { AvailableQuestCard } from "./available-quest-card";
import { availableQuests } from "./quest-availability";
import {
  defaultDiscoveryFilters,
  discoverQuests,
  indexDiscoveryQuests,
  type DiscoveryFilters,
} from "./quest-discovery";
import { QuestDiscoveryControls } from "./quest-discovery-controls";
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
  const [filters, setFilters] = useState<DiscoveryFilters>(
    defaultDiscoveryFilters,
  );
  const [visibleLimit, setVisibleLimit] = useState(20);
  const indexedQuests = useMemo(
    () => indexDiscoveryQuests(availableQuests(catalog, state), catalog, state),
    [catalog, state],
  );
  const matchingQuests = useMemo(
    () => discoverQuests(indexedQuests, filters),
    [indexedQuests, filters],
  );
  const visibleQuests = matchingQuests.slice(0, visibleLimit);
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
  if (!state || !catalog) return <Label>Loading your quests…</Label>;
  function changeFilters(change: Partial<DiscoveryFilters>) {
    setFilters((current) => ({ ...current, ...change }));
    setVisibleLimit(20);
  }
  return (
    <View style={{ gap: 20 }}>
      <Text accessibilityRole="header" style={styles.title}>
        Pick your next quest
      </Text>
      <Label>Search or browse for a quest that fits your day.</Label>
      <QuestDiscoveryControls
        categories={catalog.categories}
        filters={filters}
        resultCount={matchingQuests.length}
        shownCount={visibleQuests.length}
        onChange={changeFilters}
        onReset={() => {
          setFilters(defaultDiscoveryFilters);
          setVisibleLimit(20);
        }}
      />
      <Button
        title="Create a custom quest"
        secondary
        onPress={() => setEditing("new")}
      />
      <Label>
        Start plans a quest for {initialDate || state?.today} in {state?.zone}.
        Open quest details to change its date, leave it unscheduled or set
        recurrence.
      </Label>
      {visibleQuests.length === 0 && (
        <Label>
          No quests match this search and filter combination. Try fewer filters,
          clear your search, or create a custom quest.
        </Label>
      )}
      {visibleQuests.map(({ quest, categoryName, ready }) => (
        <AvailableQuestCard
          key={quest.id}
          quest={quest}
          categoryName={categoryName}
          disabled={busy || pending || !state}
          locked={!ready}
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
      {matchingQuests.length > visibleLimit && (
        <Button
          title="Show more quests"
          secondary
          onPress={() => setVisibleLimit((count) => count + 20)}
        />
      )}
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
