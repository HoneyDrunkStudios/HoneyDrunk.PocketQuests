import { useMemo, useState } from "react";
import { Text, TextInput, View } from "react-native";
import { useAccountSnapshot } from "../../session/session";
import { Page } from "../../session/session-page";
import { Button, Label, colors, styles } from "../../shared/ui";
import { AvailableQuests } from "./available-quests";
import { QuestHistory } from "./quest-history";
import { QuestViewTabs } from "./quest-view-tabs";
import {
  calendarDateLabel,
  canPlanCalendarDate,
  calendarPlan,
  isCalendarDate,
  shiftCalendarDate,
} from "./calendar-plan";

export function CalendarPlanner() {
  const { state } = useAccountSnapshot();
  const [date, setDate] = useState("");
  const [draft, setDraft] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [week, setWeek] = useState(false);
  const [planning, setPlanning] = useState<{
    date: string;
    create: boolean;
  } | null>(null);
  const selected = date || state?.today || "";
  const plan = useMemo(
    () => calendarPlan(state?.occurrences ?? [], selected, week),
    [state?.occurrences, selected, week],
  );
  if (!state)
    return (
      <Page>
        <Label>Loading your calendar.</Label>
      </Page>
    );
  if (planning)
    return (
      <Page>
        <Button
          title="Back to calendar"
          secondary
          onPress={() => setPlanning(null)}
        />
        <Label>
          Planning for {planning.date} · {state.zone}. Choose a quest, then
          review its date and terms before starting.
        </Label>
        <AvailableQuests
          key={`${planning.date}/${planning.create}`}
          initialDate={planning.date}
          initialCreate={planning.create}
        />
      </Page>
    );
  const selectDate = (value: string) => {
    setDate(value);
    setDraft("");
    setError(null);
  };
  const previous = shiftCalendarDate(selected, week ? -7 : -1);
  const next = shiftCalendarDate(selected, week ? 7 : 1);
  const chooseActions = (day: string) =>
    canPlanCalendarDate(day, state.today) ? (
      <View style={{ gap: 8 }}>
        <Button
          title={`Choose a quest for ${day}`}
          onPress={() => setPlanning({ date: day, create: false })}
        />
        <Button
          title={`Create a quest for ${day}`}
          secondary
          onPress={() => setPlanning({ date: day, create: true })}
        />
      </View>
    ) : null;
  return (
    <QuestHistory
      sections={[
        ...plan.days.map((day) => ({
          key: day.key,
          title: `${calendarDateLabel(day.date)} · ${day.date}`,
          data: day.data,
          emptyContent: <Label>No quests planned for this day.</Label>,
          footerContent: chooseActions(day.date),
        })),
        {
          key: "unscheduled",
          title: "Unscheduled",
          data: plan.unscheduled,
          emptyContent: <Label>No unscheduled quests.</Label>,
        },
      ]}
    >
      <Text accessibilityRole="header" style={styles.title}>
        Make space for what matters
      </Text>
      <QuestViewTabs
        options={["Day", "7 days"] as const}
        selected={week ? "7 days" : "Day"}
        onSelect={(value) => setWeek(value === "7 days")}
      />
      <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 8 }}>
        <Button
          title={week ? "Previous 7 days" : "Previous day"}
          secondary
          disabled={!previous}
          onPress={() => previous && selectDate(previous)}
        />
        <Button
          title="Today"
          secondary
          onPress={() => selectDate(state.today)}
        />
        <Button
          title={week ? "Next 7 days" : "Next day"}
          secondary
          disabled={!next}
          onPress={() => next && selectDate(next)}
        />
      </View>
      <Label>
        {selected}
        {week && plan.days.length ? ` – ${plan.days.at(-1)?.date}` : ""} ·{" "}
        {state.zone}
      </Label>
      <TextInput
        accessibilityLabel="Calendar date YYYY-MM-DD"
        placeholder={selected}
        value={draft}
        onChangeText={setDraft}
        style={styles.input}
        autoCapitalize="none"
      />
      <Button
        title="Go to date"
        secondary
        onPress={() => {
          if (!isCalendarDate(draft))
            setError(
              "Enter a real date in YYYY-MM-DD format. Your current calendar is unchanged.",
            );
          else selectDate(draft);
        }}
      />
      {error && (
        <Text
          accessibilityRole="alert"
          style={[styles.text, { color: colors.danger }]}
        >
          {error}
        </Text>
      )}
      <Label>
        Planned times come first, then acceptance order. All statuses remain
        visible; planned times do not change deadlines.
      </Label>
      {plan.days.some((day) => !canPlanCalendarDate(day.date, state.today)) && (
        <Label>
          Past dates and dates in year 9999 are view-only. Choose today or a
          future date before year 9999 to plan quests.
        </Label>
      )}
    </QuestHistory>
  );
}
