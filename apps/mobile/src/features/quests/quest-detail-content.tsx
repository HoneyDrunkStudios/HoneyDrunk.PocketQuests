import { useState } from "react";
import { Text } from "react-native";
import type {
  Catalog,
  OccurrenceView,
  Quest,
  State,
} from "../../shared/contracts";
import { useSessionActions, useSessionStatus } from "../../session/session";
import { Button, Card, Label, styles } from "../../shared/ui";
import { questActions } from "./commands/quest-actions";
import { FocusTimer } from "./focus-timer";
import { QuestRewards } from "./quest-rewards";

export function QuestCompletionCriterion({
  quest,
}: Readonly<{ quest: Quest }>) {
  return (
    <Card>
      <Text accessibilityRole="header" style={styles.subtitle}>
        Complete when
      </Text>
      <Label>{quest.criterion}</Label>
      {quest.description ? <Label>{quest.description}</Label> : null}
    </Card>
  );
}

export function QuestCompletionActions({
  item,
}: Readonly<{ item: OccurrenceView }>) {
  const { command } = useSessionActions();
  const { busy, pending } = useSessionStatus();
  const [focus, setFocus] = useState(false);
  if (item.status !== "Active") return null;
  const quest = item.occurrence.quest;
  const supportsFocus =
    quest.isCustom ||
    [
      "PQ-CAT-Q01",
      "PQ-CAT-Q02",
      "PQ-CAT-Q03",
      "PQ-CAT-Q04",
      "PQ-CAT-Q07",
      "PQ-CAT-Q08",
    ].includes(quest.id);
  return (
    <>
      <Button
        title={`Complete: ${quest.title}`}
        disabled={busy || pending}
        onPress={() =>
          void command({
            action: questActions.complete,
            occurrenceId: item.occurrence.id,
          })
        }
      />
      <Label>Already done it? Log it here. No timer required.</Label>
      {supportsFocus && (
        <>
          <Button
            title={
              focus ? "Close focus timer" : "Do Now with an optional timer"
            }
            secondary
            onPress={() => setFocus((shown) => !shown)}
          />
          {focus && <FocusTimer />}
        </>
      )}
    </>
  );
}

export function QuestOccurrenceRewards({
  item,
  catalog,
  state,
}: Readonly<{
  item: OccurrenceView;
  catalog: Catalog | null;
  state: State | null;
}>) {
  return (
    <Card>
      <Text accessibilityRole="header" style={styles.subtitle}>
        Completion rewards
      </Text>
      <QuestRewards
        quest={item.occurrence.quest}
        catalog={catalog}
        state={state}
      />
      <Label>
        Separate progress pools, not extra overall XP. Base reward preview.{" "}
        {item.pendingCompletion && item.pendingTimingVerification
          ? "Timing verification is still required; reconnecting alone cannot confirm rewards."
          : "Confirmed rewards require a verified completion."}
      </Label>
    </Card>
  );
}
