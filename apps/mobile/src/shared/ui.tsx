import { questActions } from "../features/quests/commands/quest-actions";
import { ScrollView, Text, View, StyleSheet } from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";
import { Link } from "expo-router";
import { useSession } from "../session/session";
import type { OccurrenceView } from "./contracts";

import {
  createStyles,
  Card,
  Button,
  Label,
  Notice,
} from "@honeydrunk/ui-native";
import { pocketQuestsTheme } from "./theme";
export { Button, Label, Input, Card } from "@honeydrunk/ui-native";
export const colors = {
  ink: pocketQuestsTheme.colors.text,
  muted: pocketQuestsTheme.colors.muted,
  paper: pocketQuestsTheme.colors.background,
  card: pocketQuestsTheme.colors.surface,
  green: pocketQuestsTheme.colors.primary,
  line: pocketQuestsTheme.colors.border,
  gold: pocketQuestsTheme.colors.accent,
  danger: pocketQuestsTheme.colors.danger,
};
export const styles = StyleSheet.create(createStyles(pocketQuestsTheme));
export function Page({ children }: { children: React.ReactNode }) {
  const {
    error,
    pending,
    busy,
    retry,
    offline,
    queuedCount,
    rejected = [],
    discardRejected,
    discardPending,
    signOut,
  } = useSession();
  const insets = useSafeAreaInsets();
  return (
    <ScrollView
      contentInsetAdjustmentBehavior="automatic"
      style={{ flex: 1, backgroundColor: colors.paper }}
      contentContainerStyle={{
        padding: 20,
        paddingBottom: Math.max(insets.bottom, 20) + 24,
        gap: 20,
        width: "100%",
        maxWidth: 780,
        alignSelf: "center",
      }}
    >
      {(offline || queuedCount > 0 || rejected.length > 0) && (
        <View style={styles.card} accessibilityLiveRegion="polite">
          <Label>
            {offline
              ? "Offline - showing your private device cache."
              : rejected.length > 0
                ? "Recorded changes need review."
                : "Recorded changes awaiting confirmation."}{" "}
            {queuedCount} pending. Displayed XP remains server-confirmed.
          </Label>
          <Button
            title="Synchronize recorded changes"
            disabled={busy}
            onPress={() => void retry()}
          />
          {rejected.map((entry) => (
            <View key={entry.command.operationId} style={{ gap: 8 }}>
              <Label>
                {entry.kind === "blocked" ? "Blocked" : "Rejected"}:{" "}
                {entry.label}
              </Label>
              <Notice>{entry.reason}</Notice>
              <Text selectable style={styles.muted}>
                Action ID: {entry.command.operationId}
              </Text>
              {entry.blockedBy?.length ? (
                <Text selectable style={styles.muted}>
                  Related action IDs: {entry.blockedBy.join(", ")}
                </Text>
              ) : null}
              <Button
                title={`Discard this action: ${entry.label}`}
                secondary
                disabled={busy}
                onPress={() => void discardRejected(entry.command.operationId)}
              />
            </View>
          ))}
          {(queuedCount > 0 || rejected.length > 0) && (
            <>
              <Label>
                Discard pending changes removes all pending, rejected and
                blocked actions on this device.
              </Label>
              <Button
                title="Discard pending changes"
                secondary
                disabled={busy}
                onPress={() => void discardPending()}
              />
              <Button
                title="Discard pending changes and sign out"
                secondary
                disabled={busy}
                onPress={() => void signOut(true)}
              />
            </>
          )}
        </View>
      )}
      {error && (
        <View accessibilityLiveRegion="polite" style={styles.card}>
          <Notice>{error}</Notice>
          {pending && (
            <Button
              title="Retry pending action"
              onPress={() => void retry()}
              disabled={busy}
            />
          )}
        </View>
      )}
      {children}
    </ScrollView>
  );
}
export function QuestCard({ item }: { item: OccurrenceView }) {
  const { command, busy, pending, catalog } = useSession();
  const q = item.occurrence.quest;
  return (
    <Card style={{ borderLeftWidth: 4, borderLeftColor: colors.green }}>
      <Text selectable style={styles.muted}>
        {catalog?.categories.find((c) => c.id === q.categoryId)?.name} ·{" "}
        {item.status}
      </Text>
      <Text selectable style={styles.subtitle}>
        {q.title}
      </Text>
      <View
        style={{
          alignSelf: "flex-start",
          borderColor: colors.gold,
          borderWidth: 1,
          borderRadius: 8,
          paddingHorizontal: 10,
          paddingVertical: 5,
        }}
      >
        <Text
          style={{ ...styles.muted, color: colors.gold, fontWeight: "700" }}
        >
          {q.baseXp} XP · RANK {q.rank}
        </Text>
      </View>
      <Label>{q.criterion}</Label>
      {item.pendingCompletion && (
        <Label>Completion awaiting sync · rewards unconfirmed</Label>
      )}
      <Text selectable style={styles.muted}>
        {item.occurrence.dueDate
          ? `Due ${item.occurrence.dueDate}`
          : "Unscheduled"}{" "}
        {item.occurrence.plannedTime
          ? ` at ${item.occurrence.plannedTime} (planned only)`
          : ""}{" "}
        · {q.baseXp} base XP
      </Text>
      <Link
        href={{ pathname: "/quest/[id]", params: { id: item.occurrence.id } }}
        style={styles.text}
      >
        Open quest
      </Link>
      {item.occurrence.lifecycle?.lockedLoss != null && (
        <Label>
          Accepted loss: up to {item.occurrence.lifecycle.lockedLoss} category
          XP. Reward: {q.baseXp} XP.
        </Label>
      )}
      {item.status === "Frozen" && (
        <Label>
          Frozen: resume its category/account pause first. Stopped-series
          commitments can then be resumed in quest details.
        </Label>
      )}
      {item.canUndo && (
        <Button
          title={`Undo: ${q.title}`}
          secondary
          onPress={() =>
            void command({
              action: questActions.undo,
              occurrenceId: item.occurrence.id,
              completionId: item.completion!.id,
            })
          }
          disabled={busy || pending}
        />
      )}
    </Card>
  );
}
