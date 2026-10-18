import { useState } from "react";
import { Text, View } from "react-native";
import type { Quest } from "../../shared/contracts";
import { useSession } from "../../session/session";
import { Button, Card, Label, styles } from "../../shared/ui";
import { questActions } from "./commands/quest-actions";
import { eligible } from "./quest-rules";
import { QuestRewards } from "./quest-rewards";
import {
  QuestAcceptancePlan,
  type AcceptancePlan,
} from "./quest-acceptance-plan";
import { QuestEditor } from "./quest-editor";
import { SeriesEditor } from "./series-editor";

export function AvailableQuestDetails({
  quest,
  initialDate,
  onBack,
}: Readonly<{
  quest: Quest;
  initialDate: string;
  onBack(): void;
}>) {
  const { catalog, state, command, busy, pending } = useSession();
  const [plan, setPlan] = useState<AcceptancePlan>({
    date: initialDate,
    time: "",
    unscheduled: false,
  });
  const [consent, setConsent] = useState<Quest | null>(null);
  const [mode, setMode] = useState<"edit" | "recurrence" | null>(null);
  const confirmed = JSON.stringify(consent) === JSON.stringify(quest);
  const hasPenalty = (quest.penaltyPercent ?? 0) > 0;
  const loss = Math.floor(
    (quest.baseXp * (quest.penaltyPercent ?? 0) + 50) / 100,
  );
  const definition = state?.definitions.find(
    (item) => item.quest.id === quest.id,
  );
  const locked = !state || !eligible(quest, state);
  if (mode === "edit" && definition)
    return (
      <QuestEditor
        initial={definition}
        closeLabel="Back to quest details"
        onClose={() => setMode(null)}
      />
    );
  if (mode === "recurrence")
    return (
      <SeriesEditor
        quest={quest}
        closeLabel="Back to quest details"
        onClose={() => setMode(null)}
      />
    );
  return (
    <View style={{ gap: 20 }}>
      <Button title="Back to available quests" secondary onPress={onBack} />
      <Text style={styles.muted}>
        {catalog?.categories.find((item) => item.id === quest.categoryId)?.name}
      </Text>
      <Text accessibilityRole="header" style={styles.title}>
        {quest.title}
      </Text>
      <Label>
        Available to pick up Â· Rank {quest.rank} Â· {quest.effort}
      </Label>
      <Card>
        <Text accessibilityRole="header" style={styles.subtitle}>
          Complete when
        </Text>
        <Label>{quest.criterion}</Label>
        {quest.description ? <Label>{quest.description}</Label> : null}
      </Card>
      <Card>
        <Text accessibilityRole="header" style={styles.subtitle}>
          Completion rewards
        </Text>
        <QuestRewards quest={quest} catalog={catalog} state={state} />
        <Label>
          Separate progress pools, not extra overall XP. Rewards are confirmed
          after completion.
        </Label>
      </Card>
      <QuestAcceptancePlan
        plan={plan}
        today={state?.today ?? ""}
        zone={state?.zone ?? ""}
        onChange={(next) => {
          if (next.date !== plan.date || next.unscheduled !== plan.unscheduled)
            setConsent(null);
          setPlan(next);
        }}
      />
      {hasPenalty && (
        <Card>
          <Text accessibilityRole="header" style={styles.subtitle}>
            Penalty terms
          </Text>
          <Label>
            On a miss or confirmed abandonment: up to {loss} category XP loss.
            Category level/global rank may fall. Zero floor, no debt, no
            other-pool loss. A due day is required.
          </Label>
          <Button
            title={
              confirmed
                ? "Penalty terms confirmed"
                : `Confirm penalty terms: ${quest.title}`
            }
            secondary
            onPress={() => setConsent(quest)}
          />
        </Card>
      )}
      {locked && (
        <Label>
          This quest requires higher skill or category levels before you can
          start it.
        </Label>
      )}
      <Button
        title={`Start: ${quest.title}`}
        disabled={
          busy ||
          pending ||
          locked ||
          (hasPenalty && (plan.unscheduled || !confirmed))
        }
        onPress={() =>
          void command({
            action: questActions.accept,
            questId: quest.id,
            dueDate: plan.unscheduled ? null : plan.date || state?.today,
            plannedTime: plan.unscheduled ? null : plan.time || null,
            confirmPenalty: confirmed,
            acceptedQuest: confirmed ? quest : undefined,
            acceptedLoss: confirmed ? loss : undefined,
          })
        }
      />
      <Button
        title={`Set recurrence: ${quest.title}`}
        secondary
        onPress={() => setMode("recurrence")}
      />
      {quest.isCustom && definition && (
        <>
          <Button
            title={`Edit: ${quest.title}`}
            secondary
            onPress={() => setMode("edit")}
          />
          <Label>
            Archiving stops new use and recurrence. Existing history remains.
          </Label>
          <Button
            title={`Archive definition: ${quest.title}`}
            secondary
            disabled={busy || pending}
            onPress={() =>
              void command({
                action: questActions.archiveDefinition,
                questId: quest.id,
                expectedRevision: definition.revision,
              })
            }
          />
        </>
      )}
    </View>
  );
}
