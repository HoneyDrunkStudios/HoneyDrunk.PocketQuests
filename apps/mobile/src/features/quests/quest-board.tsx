import { Page } from "../../session/session-page";
import { QuestHistory, type QuestHistorySection } from "./quest-history";
import { useMemo, useState } from "react";
import { JournalHeading } from "../../shared/journal-heading";
import { useAccountSnapshot } from "../../session/session";
import { Button, Label } from "../../shared/ui";
import { AvailableQuests } from "./available-quests";
import { Redirect } from "expo-router";
import { activePlan } from "./active-plan";
import type { Catalog, State } from "../../shared/contracts";
import { QuestHistoryControls } from "./quest-history-controls";
import { indexQuestHistory, questHistoryMatcher } from "./quest-history-search";
import {
  defaultQuestSearchFilters,
  type QuestSearchFilters,
} from "./quest-search";
import { QuestViewTabs } from "./quest-view-tabs";

const boardViews = ["Available", "Active", "Completed"] as const;
type BoardView = (typeof boardViews)[number];

export function QuestBoard() {
  const { state, catalog } = useAccountSnapshot();
  if (!state)
    return (
      <Page header={<JournalHeading title="Your quests" compact />}>
        <Label>Loading your quests.</Label>
      </Page>
    );
  if (!state.profile.onboardingComplete) return <Redirect href="/onboarding" />;
  return <QuestBoardContent state={state} catalog={catalog} />;
}

function QuestBoardContent({
  state,
  catalog,
}: Readonly<{ state: State; catalog: Catalog | null }>) {
  const [filters, setFilters] = useState<QuestSearchFilters>(
    defaultQuestSearchFilters,
  );
  const [section, setSection] = useState<BoardView>("Available");
  const [showPast, setShowPast] = useState(false);
  const plan = useMemo(
    () => activePlan(state.occurrences, state.today),
    [state.occurrences, state.today],
  );
  const index = useMemo(
    () => indexQuestHistory(state.occurrences, catalog?.categories ?? []),
    [state.occurrences, catalog?.categories],
  );
  const matches = useMemo(
    () => questHistoryMatcher(index, filters),
    [index, filters],
  );
  const filtering =
    !!filters.query.trim() || !!filters.categoryId || !!filters.effort;
  const selectView = (view: BoardView) => {
    if (view === section) return;
    setSection(view);
    setFilters(defaultQuestSearchFilters);
    setShowPast(false);
  };
  const items = state.occurrences.filter((o) => {
    if (section === "Completed") return o.status === "Completed";
    if (section === "Active") return ["Active", "Frozen"].includes(o.status);
    return o.status === "Offered";
  });
  const offeredTitle =
    section === "Available" && items.length > 0
      ? "Recurring quest offers"
      : undefined;
  const sections: QuestHistorySection[] =
    section === "Active"
      ? [
          {
            key: "today",
            title: `Today · ${state.today}`,
            data: plan.current,
            emptyContent: <Label>No quests planned for today.</Label>,
          },
          {
            key: "unscheduled",
            title: "Unscheduled",
            data: plan.unscheduled,
            emptyContent: <Label>No unscheduled quests.</Label>,
          },
          ...(plan.earlier.length
            ? [
                {
                  key: "earlier",
                  title: "Earlier commitments",
                  data: plan.earlier,
                },
              ]
            : []),
          ...(plan.upcoming.length
            ? [{ key: "upcoming", title: "Upcoming", data: plan.upcoming }]
            : []),
          ...(showPast && plan.past.length
            ? [{ key: "past", title: "Past commitments", data: plan.past }]
            : []),
        ]
      : [
          {
            key: section,
            title: offeredTitle,
            data: items,
          },
        ];
  const visibleSections =
    section === "Available"
      ? sections
      : sections
          .map((group) => ({ ...group, data: group.data.filter(matches) }))
          .filter((group) => !filtering || group.data.length > 0);
  const totalCount = sections.reduce(
    (total, group) => total + group.data.length,
    0,
  );
  const resultCount = visibleSections.reduce(
    (total, group) => total + group.data.length,
    0,
  );
  return (
    <QuestHistory
      sections={visibleSections}
      header={<JournalHeading title="Your quests" compact />}
    >
      <QuestViewTabs<BoardView>
        options={boardViews}
        selected={section}
        onSelect={selectView}
      />
      {section === "Available" && (
        <>
          <Label style={{ fontSize: 14, lineHeight: 20 }}>
            Start picks up a quest. Open it from Active when you are ready to
            complete it.
          </Label>
          <AvailableQuests />
        </>
      )}
      {section !== "Available" && (
        <QuestHistoryControls
          key={section}
          view={section}
          categories={catalog?.categories ?? []}
          filters={filters}
          resultCount={resultCount}
          onChange={(change) =>
            setFilters((current) => ({ ...current, ...change }))
          }
          onReset={() => setFilters(defaultQuestSearchFilters)}
        />
      )}
      {section === "Active" && (
        <>
          <Label style={{ fontSize: 14, lineHeight: 20 }}>
            Your day in {state.zone}. Planned times come first, then acceptance
            order. Unscheduled quests have no deadline.
          </Label>
          {plan.past.length > 0 && (
            <Button
              title={`${showPast ? "Hide" : "Show"} past commitments (${plan.past.length})`}
              secondary
              onPress={() => setShowPast((shown) => !shown)}
            />
          )}
          <Button
            title="Choose another quest"
            onPress={() => selectView("Available")}
          />
        </>
      )}
      {section !== "Available" && totalCount > 0 && resultCount === 0 && (
        <Label>No quests match your search and filters.</Label>
      )}
      {section !== "Available" && totalCount === 0 && (
        <Label>
          {section === "Active"
            ? "No active quests yet. Choose one from Available."
            : "Your completed quests will appear here."}
        </Label>
      )}
    </QuestHistory>
  );
}
