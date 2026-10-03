import { questActions } from "./commands/quest-actions";
import { useState } from "react";
import * as Crypto from "expo-crypto";
import { Text, View } from "react-native";
import type { Cadence, Quest, Series } from "./contracts";
import { PlannedPreview } from "./planned-preview";
import { useSession } from "./session";
import { Button, Input, Label, styles } from "./ui";
export function SeriesEditor({
  quest,
  initial,
  onClose,
}: {
  quest: Quest;
  initial?: Series;
  onClose(): void;
}) {
  const { state, command, busy, pending } = useSession();
  const [id] = useState(initial?.id ?? Crypto.randomUUID());
  const [anchor, setAnchor] = useState(state?.today ?? "");
  const [cadence, setCadence] = useState<Cadence>(initial?.cadence ?? "Days");
  const [interval, setInterval] = useState(String(initial?.interval ?? 1));
  const [time, setTime] = useState(initial?.plannedTime ?? "");
  const [consent, setConsent] = useState(false);
  const saved = state?.schedule.series.find((s) => s.id === id);
  const loss = Math.floor(
    (quest.baseXp * (quest.penaltyPercent ?? 0) + 50) / 100,
  );
  const stale = !!saved && saved.version !== (initial?.version ?? 0);
  return (
    <View style={{ gap: 12 }}>
      <Text style={styles.subtitle}>Repeat: {quest.title}</Text>
      <Label>
        Each delivery is due at the end of its own day. Month-end and leap-day
        anchors return when possible. Existing accepted occurrences keep their
        schedule when you edit future deliveries.
      </Label>
      <Label>First delivery date</Label>
      <Input
        accessibilityLabel="First delivery YYYY-MM-DD"
        value={anchor}
        onChangeText={setAnchor}
        style={styles.input}
      />
      <Label>Repeat every 1–999</Label>
      <Input
        accessibilityLabel="Recurrence interval"
        keyboardType="number-pad"
        value={interval}
        onChangeText={setInterval}
        style={styles.input}
      />
      {(["Days", "Weeks", "Months", "Years"] as Cadence[]).map((unit) => (
        <Button
          key={unit}
          title={unit}
          secondary={cadence !== unit}
          onPress={() => setCadence(unit)}
        />
      ))}
      <Input
        accessibilityLabel="Recurring planned time HH:mm"
        placeholder="Optional time HH:mm"
        value={time}
        onChangeText={setTime}
        style={styles.input}
      />
      <PlannedPreview date={anchor} time={time} />
      {loss > 0 && (
        <>
          <Label>
            Each accepted occurrence can lose up to {loss} XP from{" "}
            {state?.categories.find((c) => c.id === quest.categoryId)?.name} on
            a miss or confirmed abandonment. This can lower category
            level/global rank, stops at zero, and does not reduce other pools.
            Without separate consent, deliveries remain offers.
          </Label>
          <Button
            title={
              consent
                ? "Automatic penalty acceptance selected — revoke"
                : `Opt in to automatically accepting ${loss} XP loss per occurrence`
            }
            secondary={!consent}
            onPress={() => setConsent(!consent)}
          />
        </>
      )}
      {stale && (
        <Label>Schedule saved. Return to the board to review it.</Label>
      )}
      <Button
        title={initial ? "Save future schedule" : "Start recurring deliveries"}
        disabled={
          busy ||
          pending ||
          stale ||
          !/^\d{1,3}$/.test(interval) ||
          Number(interval) < 1
        }
        onPress={() =>
          void command({
            action: questActions.saveSeries,
            seriesId: id,
            questId: quest.id,
            dueDate: anchor,
            cadence,
            interval: Number(interval),
            plannedTime: time || null,
            expectedRevision: initial?.version ?? 0,
            confirmPenalty: consent,
            acceptedLoss: consent ? loss : undefined,
            acceptedQuest: consent ? quest : undefined,
          })
        }
      />
      <Button title="Back to quest board" secondary onPress={onClose} />
    </View>
  );
}
