import { questActions } from "../../features/quests/commands/quest-actions";
import { useState } from "react";
import { Link, Redirect, useLocalSearchParams } from "expo-router";
import { Text, TextInput, View } from "react-native";
import { useSession } from "../../session/session";
import { Page, Button, Label, styles } from "../../shared/ui";
import { QuestRewards } from "../../features/quests/quest-rewards";
import { FocusTimer } from "../../features/quests/focus-timer";
import { DefinitionManagement } from "../../features/quests/definition-management";
export default function QuestDetails() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const { state, catalog, command, busy, pending, signedIn } = useSession();
  const [confirmLoss, setConfirmLoss] = useState(false);
  const [focus, setFocus] = useState(false);
  const item = state?.occurrences.find((o) => o.occurrence.id === id);
  if (!signedIn && !busy) return <Redirect href="/" />;
  if (!state && busy)
    return (
      <Page>
        <Label>Loading your quest.</Label>
      </Page>
    );
  if (!item)
    return (
      <Page>
        <Label>Quest unavailable. Sign in to its account and refresh.</Label>
        <Link href="/(tabs)/board" style={styles.text}>
          Back to quests
        </Link>
      </Page>
    );
  return (
    <Page>
      <Text accessibilityRole="header" style={styles.title}>
        {item.occurrence.quest.title}
      </Text>
      <Label>{item.occurrence.quest.criterion}</Label>
      <QuestRewards
        quest={item.occurrence.quest}
        catalog={catalog}
        state={state}
      />
      {item.status === "Active" && (
        <>
          <Button
            title={`Complete: ${item.occurrence.quest.title}`}
            disabled={busy || pending}
            onPress={() =>
              void command({ action: questActions.complete, occurrenceId: id })
            }
          />
          {(item.occurrence.quest.isCustom ||
            [
              "PQ-CAT-Q01",
              "PQ-CAT-Q02",
              "PQ-CAT-Q03",
              "PQ-CAT-Q04",
              "PQ-CAT-Q07",
              "PQ-CAT-Q08",
            ].includes(item.occurrence.quest.id)) && (
            <>
              <Button
                title={
                  focus ? "Close focus timer" : "Do Now with an optional timer"
                }
                secondary
                onPress={() => setFocus(!focus)}
              />
              {focus && <FocusTimer key={id} />}
            </>
          )}
        </>
      )}
      {item.pendingCompletion && (
        <Label>
          Recorded on this device. Rewards will be confirmed when synchronized.
        </Label>
      )}
      <Label>
        {item.status} · {item.occurrence.quest.baseXp} XP
      </Label>
      {item.planned && (
        <Label>
          Planned: {item.planned.resolved}, {item.planned.instant}
          {item.planned.adjusted
            ? " (adjusted to the next valid clock time)"
            : item.planned.repeated
              ? " (first occurrence of the repeated time)"
              : ""}
          .
        </Label>
      )}
      {item.occurrence.lifecycle?.lockedLoss != null && (
        <Label>
          Accepted category loss: {item.occurrence.lifecycle.lockedLoss} XP.
          Actual assessed loss:{" "}
          {state?.penalties.find((p) => p.occurrenceId === id)?.actualLoss ??
            "not assessed"}
          .
        </Label>
      )}
      {item.status === "Frozen" && (
        <Button
          title="Resume this commitment"
          disabled={busy || pending}
          onPress={() =>
            void command({
              action: questActions.resumeOccurrence,
              occurrenceId: id,
            })
          }
        />
      )}
      {item.status === "Offered" && (
        <>
          <Label>
            Unaccepted offer. Acceptance can incur up to{" "}
            {Math.floor(
              (item.occurrence.quest.baseXp *
                (item.occurrence.quest.penaltyPercent ?? 0) +
                50) /
                100,
            )}{" "}
            category XP loss on miss/abandonment, with possible demotion and a
            zero floor. No loss until accepted.
          </Label>
          <Button
            title="Accept this offer and disclosed terms"
            disabled={busy || pending}
            onPress={() =>
              void command({
                action: questActions.acceptOffer,
                occurrenceId: id,
                confirmPenalty: true,
                acceptedQuest: item.occurrence.quest,
                acceptedLoss: Math.floor(
                  (item.occurrence.quest.baseXp *
                    (item.occurrence.quest.penaltyPercent ?? 0) +
                    50) /
                    100,
                ),
              })
            }
          />
        </>
      )}
      {(item.status === "Active" || item.status === "Frozen") && (
        <>
          <Label>
            Abandonment earns nothing. It assesses the accepted loss of{" "}
            {item.occurrence.lifecycle?.lockedLoss ?? 0} category XP once,
            capped at current balance.
          </Label>
          <Button
            title={
              confirmLoss
                ? "Abandonment confirmed"
                : "Confirm abandonment terms"
            }
            secondary
            onPress={() => setConfirmLoss(true)}
          />
          <Button
            title="Abandon this occurrence"
            disabled={busy || pending || !confirmLoss}
            secondary
            onPress={() =>
              void command({
                action: questActions.abandon,
                occurrenceId: id,
                confirmPenalty: confirmLoss,
                acceptedLoss:
                  item.occurrence.lifecycle?.lockedLoss ?? undefined,
              })
            }
          />
        </>
      )}
      {item.canUndo && (
        <Button
          title="Undo this completion"
          secondary
          disabled={busy || pending}
          onPress={() =>
            void command({
              action: questActions.undo,
              occurrenceId: id,
              completionId: item.completion!.id,
            })
          }
        />
      )}
      <DefinitionManagement
        key={item.occurrence.quest.id}
        questId={item.occurrence.quest.id}
      />
      <Planning key={id} id={id} />
      <Text style={styles.subtitle}>Linked goal and steps</Text>
      {item.occurrence.parentId ? (
        <Link
          href={{
            pathname: "/quest/[id]",
            params: { id: item.occurrence.parentId },
          }}
          style={styles.text}
        >
          Parent:{" "}
          {
            state?.occurrences.find(
              (o) => o.occurrence.id === item.occurrence.parentId,
            )?.occurrence.quest.title
          }
        </Link>
      ) : (
        item.occurrence.quest.effort !== "Large" &&
        state?.occurrences
          .filter((o) => o.occurrence.quest.effort === "Large")
          .map((parent) => (
            <Button
              key={parent.occurrence.id}
              title={`Link to ${parent.occurrence.quest.title}`}
              disabled={busy || pending}
              onPress={() =>
                void command({
                  action: questActions.link,
                  occurrenceId: id,
                  parentId: parent.occurrence.id,
                })
              }
            />
          ))
      )}
      {state?.occurrences
        .filter((o) => o.occurrence.parentId === id)
        .map((step) => (
          <Link
            key={step.occurrence.id}
            href={{
              pathname: "/quest/[id]",
              params: { id: step.occurrence.id },
            }}
            style={styles.text}
          >
            {step.occurrence.quest.title}: {step.status}
          </Link>
        ))}
      <Label>
        Steps and Large goals each need their own completion. A link never
        grants XP or automatically completes a parent.
      </Label>
      <Link href="/(tabs)" style={styles.text}>
        Back to Home
      </Link>
    </Page>
  );
}
function Planning({ id }: { id: string }) {
  const { state, command, busy, pending } = useSession();
  const item = state!.occurrences.find((o) => o.occurrence.id === id)!;
  const [date, setDate] = useState(item.occurrence.dueDate ?? "");
  const [time, setTime] = useState(item.occurrence.plannedTime ?? "");
  return (
    <View style={{ gap: 12 }}>
      <Label>Due day (blank means unscheduled)</Label>
      <TextInput
        accessibilityLabel="Quest due date YYYY-MM-DD"
        value={date}
        onChangeText={setDate}
        style={styles.input}
      />
      <Label>
        Optional planned time, HH:mm; the deadline stays at the end of the due
        day.
      </Label>
      <TextInput
        accessibilityLabel="Quest planned time HH:mm"
        value={time}
        onChangeText={setTime}
        style={styles.input}
      />
      <Button
        title="Save plan"
        disabled={
          busy ||
          pending ||
          item.status !== "Active" ||
          !!item.occurrence.lifecycle?.seriesId ||
          item.occurrence.lifecycle?.lockedLoss != null
        }
        onPress={() =>
          void command({
            action: questActions.plan,
            occurrenceId: id,
            dueDate: date || null,
            plannedTime: time || null,
          })
        }
      />
    </View>
  );
}
