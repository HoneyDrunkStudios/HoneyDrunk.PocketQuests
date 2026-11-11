import { displayDate } from "../../shared/date-format";
import { DateInput } from "../../shared/date-input";
import { Text } from "react-native";
import { Button, Card, Input, Label, useAppTheme } from "../../shared/ui";
import { PlannedPreview } from "./planned-preview";

export type AcceptancePlan = {
  date: string;
  time: string;
  unscheduled: boolean;
};

export function QuestAcceptancePlan({
  plan,
  today,
  onChange,
}: Readonly<{
  plan: AcceptancePlan;
  today: string;
  zone: string;
  onChange(plan: AcceptancePlan): void;
}>) {
  const { styles } = useAppTheme();
  return (
    <Card>
      <Text accessibilityRole="header" style={styles.subtitle}>
        Plan before accepting
      </Text>
      <Label>Due date</Label>
      <DateInput
        accessibilityLabel="Due date in MM/DD/YYYY format"
        placeholder={today || "MM/DD/YYYY"}
        value={plan.date}
        onChangeText={(date) => onChange({ ...plan, date })}
        style={styles.input}
        editable={!plan.unscheduled}
      />
      {!plan.unscheduled && (
        <>
          <Label>Planned time (optional)</Label>
          <Input
            accessibilityLabel="Optional planned time HH:mm"
            placeholder="Planned time HH:mm (optional)"
            value={plan.time}
            onChangeText={(time) => onChange({ ...plan, time })}
            style={styles.input}
          />
          <PlannedPreview date={plan.date || today} time={plan.time} />
        </>
      )}
      <Button
        title={
          plan.unscheduled
            ? "Unscheduled selected — choose a due day"
            : "Leave unscheduled instead"
        }
        onPress={() => onChange({ ...plan, unscheduled: !plan.unscheduled })}
        secondary
      />
      <Label>
        {plan.unscheduled
          ? "No deadline."
          : `Due by the end of ${displayDate(plan.date || today)}.`}{" "}
        Recurrence requires a separate opt-in below.
      </Label>
    </Card>
  );
}
