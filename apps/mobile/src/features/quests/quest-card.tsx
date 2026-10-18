import { Text } from "react-native";
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
    <Card>
      <Text selectable style={styles.muted}>
        {catalog?.categories.find((c) => c.id === q.categoryId)?.name} ·{" "}
        {item.status}
      </Text>
      <Text accessibilityRole="header" selectable style={styles.subtitle}>
        {q.title}
      </Text>
      <Text style={{ ...styles.muted, color: colors.gold }}>
        {q.baseXp} base XP · Rank {q.rank} · {q.effort}
      </Text>
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
          : ""}
      </Text>
      <Link
        href={{ pathname: "/quest/[id]", params: { id: item.occurrence.id } }}
        accessibilityLabel={`View quest & rewards: ${q.title}`}
        style={[
          styles.text,
          { color: colors.gold, paddingVertical: 12, minHeight: 48 },
        ]}
      >
        View quest & rewards
      </Link>
      {item.occurrence.lifecycle?.lockedLoss != null && (
        <Label>
          Accepted loss: up to {item.occurrence.lifecycle.lockedLoss} category
          XP.
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
