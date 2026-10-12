import { useState } from "react";
import { Text, View } from "react-native";
import type { OccurrenceView } from "../../shared/contracts";
import { useSession } from "../../session/session";
import { Button, Input, Label, colors, styles } from "../../shared/ui";
import { questActions } from "./commands/quest-actions";
import { PlannedPreview } from "./planned-preview";
import { planInputError, planLockReason } from "./quest-plan";

type PlanDraft = {
  date: string;
  time: string;
  baseDate: string;
  baseTime: string;
};

export function QuestPlanEditor({ item }: Readonly<{ item: OccurrenceView }>) {
  const { state, command, busy, pending } = useSession();
  const savedDate = item.occurrence.dueDate ?? "";
  const savedTime = item.occurrence.plannedTime ?? "";
  const savedZone = item.occurrence.lifecycle?.deadlineZone ?? state?.zone;
  const [draft, setDraft] = useState<PlanDraft | null>(null);
  const date = draft?.date ?? savedDate;
  const time = draft?.time ?? savedTime;
  const nextDate = date.trim();
  const nextTime = time.trim();
  const dirty = nextDate !== savedDate || nextTime !== savedTime;
  const conflict =
    dirty &&
    draft !== null &&
    (savedDate !== draft.baseDate || savedTime !== draft.baseTime);
  const locked = planLockReason(item);
  const error = planInputError(nextDate, nextTime, state?.today ?? "");
  const disabled = busy || pending || !state;
  function edit(change: Partial<Pick<PlanDraft, "date" | "time">>) {
    setDraft({
      date,
      time,
      baseDate: dirty && draft ? draft.baseDate : savedDate,
      baseTime: dirty && draft ? draft.baseTime : savedTime,
      ...change,
    });
  }
  return (
    <View style={{ gap: 12 }}>
      <Text accessibilityRole="header" style={styles.subtitle}>
        Your plan
      </Text>
      <Label>
        {savedDate ? `Due ${savedDate}` : "Unscheduled · no deadline"}
        {savedDate && savedTime ? ` at ${savedTime} (planned only)` : ""}
        {savedZone ? ` · ${savedZone}` : ""}
      </Label>
      {locked ? (
        <Label>{locked}</Label>
      ) : (
        <>
          <Label>Plan changes use your selected zone: {state?.zone}.</Label>
          <Label>Due day (blank means unscheduled)</Label>
          <Input
            accessibilityLabel="Quest due date YYYY-MM-DD"
            value={date}
            onChangeText={(value) => edit({ date: value })}
            editable={!disabled}
            style={styles.input}
          />
          <Label>
            Optional planned time, HH:mm; the deadline stays at the end of the
            due day.
          </Label>
          <Input
            accessibilityLabel="Quest planned time HH:mm"
            value={time}
            onChangeText={(value) => edit({ time: value })}
            editable={!disabled}
            style={styles.input}
          />
          <Button
            title="Plan for today"
            secondary
            disabled={disabled}
            onPress={() => edit({ date: state!.today })}
          />
          <Button
            title="Leave this quest unscheduled"
            secondary
            disabled={disabled}
            onPress={() => edit({ date: "", time: "" })}
          />
          {dirty && error && (
            <Text
              accessibilityRole="alert"
              style={[styles.text, { color: colors.danger }]}
            >
              {error}
            </Text>
          )}
          {conflict && (
            <>
              <Text
                accessibilityRole="alert"
                style={[styles.text, { color: colors.danger }]}
              >
                The saved plan changed while you were editing. Load it before
                making another change.
              </Text>
              <Button
                title="Load saved plan"
                secondary
                disabled={disabled}
                onPress={() => setDraft(null)}
              />
            </>
          )}
          {!error && nextDate && nextTime ? (
            <PlannedPreview date={nextDate} time={nextTime} />
          ) : null}
          <Button
            title="Save plan"
            disabled={disabled || !dirty || !!error || conflict}
            onPress={() =>
              void command({
                action: questActions.plan,
                occurrenceId: item.occurrence.id,
                dueDate: nextDate || null,
                plannedTime: nextTime || null,
              })
            }
          />
        </>
      )}
    </View>
  );
}
