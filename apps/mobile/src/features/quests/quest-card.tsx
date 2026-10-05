import { Text, View } from "react-native";
import { Link } from "expo-router";
import { questActions } from "./commands/quest-actions";
import {
  useSessionActions,
  useSessionStatus,
  useAccountSnapshot,
} from "../../session/session";
import type { OccurrenceView } from "../../shared/contracts";
import { Card, Button, Label, colors, styles } from "../../shared/ui";
export function QuestCard({ item }: { item: OccurrenceView }) {
  const { command } = useSessionActions();
  const { busy, pending } = useSessionStatus();
  const { catalog } = useAccountSnapshot();
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
        <Label>
          {item.pendingTimingVerification
            ? "Completion pending timing verification — rewards unconfirmed"
            : "Completion awaiting sync · rewards unconfirmed"}
        </Label>
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
