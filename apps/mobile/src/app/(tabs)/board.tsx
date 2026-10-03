import { questActions } from "../../commands/quest-actions";
import { useState } from "react";
import { Text, TextInput, View } from "react-native";
import { useSession } from "../../session";
import { Page, Button, Label, styles } from "../../ui";
import { QuestEditor } from "../../quest-editor";
import { ProfileEditor } from "../../profile-editor";
import { eligible } from "../../quest-rules";
import { PlannedPreview } from "../../planned-preview";
import { SeriesEditor } from "../../series-editor";
import type { Quest, Series, Definition } from "../../contracts";
export default function Board() {
  const { catalog, state, command, busy, pending } = useSession();
  const [recurring, setRecurring] = useState<{
    quest: Quest;
    initial?: Series;
  } | null>(null);
  const [penaltyConsent, setPenaltyConsent] = useState<Quest | null>(null);
  const [editing, setEditing] = useState<Definition | "new" | null>(null);
  const [time, setTime] = useState("");
  const [category, setCategory] = useState<string | null>(null);
  const [date, setDate] = useState("");
  const [unscheduled, setUnscheduled] = useState(false);
  if (state && !state.profile.onboardingComplete)
    return (
      <Page>
        <ProfileEditor />
      </Page>
    );
  if (editing)
    return (
      <Page>
        <QuestEditor
          key={editing === "new" ? "new" : editing.quest.id}
          initial={editing === "new" ? undefined : editing}
          onClose={() => setEditing(null)}
        />
      </Page>
    );
  if (recurring)
    return (
      <Page>
        <SeriesEditor
          key={recurring.initial?.id ?? recurring.quest.id}
          {...recurring}
          onClose={() => setRecurring(null)}
        />
      </Page>
    );
  const quests = [
    ...(catalog?.quests ?? []),
    ...(state?.definitions.filter((d) => !d.archived).map((d) => d.quest) ??
      []),
  ].sort(
    (a, b) =>
      Number(state?.profile.interests.includes(b.categoryId)) -
      Number(state?.profile.interests.includes(a.categoryId)),
  );
  return (
    <Page>
      <Text selectable style={styles.title}>
        Pick your next quest
      </Text>
      <Label>
        Ten paths. All worth exploring. Your interests appear first.
      </Label>
      <Button title="Create a custom quest" onPress={() => setEditing("new")} />
      <Text selectable style={styles.subtitle}>
        Plan before accepting
      </Text>
      <TextInput
        accessibilityLabel="Due date in YYYY-MM-DD format"
        placeholder={state?.today ?? "YYYY-MM-DD"}
        value={date}
        onChangeText={(value) => {
          setDate(value);
          setPenaltyConsent(null);
        }}
        style={styles.input}
        editable={!unscheduled}
      />
      {!unscheduled && (
        <TextInput
          accessibilityLabel="Optional planned time HH:mm"
          placeholder="Planned time HH:mm (optional)"
          value={time}
          onChangeText={setTime}
          style={styles.input}
        />
      )}
      {!unscheduled && (
        <PlannedPreview date={date || state?.today || ""} time={time} />
      )}
      <Button
        title={
          unscheduled
            ? "Unscheduled selected — choose a due day"
            : "Leave unscheduled instead"
        }
        onPress={() => {
          setUnscheduled(!unscheduled);
          setPenaltyConsent(null);
        }}
        secondary
      />
      <Label>
        {unscheduled
          ? "No deadline."
          : `Due at the end of ${date || state?.today} in ${state?.zone}.`}{" "}
        Recurrence requires a separate opt-in below.
      </Label>
      <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 8 }}>
        <Button
          title="All categories"
          onPress={() => setCategory(null)}
          secondary={category !== null}
        />
        {catalog?.categories.map((c) => (
          <Button
            key={c.id}
            title={c.name}
            onPress={() => setCategory(c.id)}
            secondary={category !== c.id}
          />
        ))}
      </View>
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
      {quests
        .filter((q) => !category || q.categoryId === category)
        .map((q) => (
          <View key={q.id} style={styles.card}>
            <Text selectable style={styles.muted}>
              {catalog?.categories.find((c) => c.id === q.categoryId)?.name} ·{" "}
              {q.rank} / {q.effort}
            </Text>
            <Text selectable style={styles.subtitle}>
              {q.title}
            </Text>
            <Label>{q.criterion}</Label>
            <Label>
              {q.baseXp} overall XP · {q.baseXp} category XP
            </Label>
            {q.attributes.length > 0 && (
              <Text selectable style={styles.muted}>
                Attribute pool:{" "}
                {q.attributes
                  .map(
                    (s) =>
                      `${catalog?.attributes.find((a) => a.id === s.id)?.name} ${s.basisPoints / 100}%`,
                  )
                  .join(" · ")}
              </Text>
            )}
            {q.skills.length > 0 && (
              <Text selectable style={styles.muted}>
                Skill pool:{" "}
                {q.skills
                  .map(
                    (s) =>
                      `${state?.skills.find((a) => a.id === s.id)?.name} ${s.basisPoints / 100}%`,
                  )
                  .join(" · ")}
              </Text>
            )}
            {(q.penaltyPercent ?? 0) > 0 && (
              <>
                <Label>
                  On a miss or confirmed abandonment: up to{" "}
                  {Math.floor((q.baseXp * q.penaltyPercent! + 50) / 100)}{" "}
                  category XP loss. Category level/global rank may fall. Zero
                  floor, no debt, no other-pool loss. A due day is required.
                </Label>
                <Button
                  title={
                    JSON.stringify(penaltyConsent) === JSON.stringify(q)
                      ? "Penalty terms confirmed"
                      : `Confirm penalty terms: ${q.title}`
                  }
                  secondary
                  onPress={() => setPenaltyConsent(q)}
                />
              </>
            )}
            <Button
              title={`Accept: ${q.title}`}
              onPress={() =>
                void command({
                  action: questActions.accept,
                  questId: q.id,
                  dueDate: unscheduled ? null : date || state?.today,
                  plannedTime: unscheduled ? null : time || null,
                  confirmPenalty:
                    JSON.stringify(penaltyConsent) === JSON.stringify(q),
                  acceptedQuest: penaltyConsent ?? undefined,
                  acceptedLoss:
                    JSON.stringify(penaltyConsent) === JSON.stringify(q)
                      ? Math.floor(
                          (q.baseXp * (q.penaltyPercent ?? 0) + 50) / 100,
                        )
                      : undefined,
                })
              }
              disabled={
                busy ||
                pending ||
                !state ||
                !eligible(q, state) ||
                ((q.penaltyPercent ?? 0) > 0 &&
                  (unscheduled ||
                    JSON.stringify(penaltyConsent) !== JSON.stringify(q)))
              }
            />
            <Button
              title={`Set recurrence: ${q.title}`}
              secondary
              onPress={() => setRecurring({ quest: q })}
            />
            {q.isCustom && (
              <>
                <Button
                  title={`Edit: ${q.title}`}
                  secondary
                  onPress={() =>
                    setEditing(
                      state!.definitions.find((d) => d.quest.id === q.id)!,
                    )
                  }
                />
                <Button
                  title={`Archive definition: ${q.title}`}
                  secondary
                  disabled={busy || pending}
                  onPress={() =>
                    void command({
                      action: questActions.archiveDefinition,
                      questId: q.id,
                      expectedRevision: state!.definitions.find(
                        (d) => d.quest.id === q.id,
                      )!.revision,
                    })
                  }
                />
              </>
            )}
          </View>
        ))}
    </Page>
  );
}
