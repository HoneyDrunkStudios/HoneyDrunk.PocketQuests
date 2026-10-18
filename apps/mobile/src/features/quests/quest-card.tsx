import { Text, View } from "react-native";
import Ionicons from "@expo/vector-icons/Ionicons";
import { QuestCardArtwork } from "./quest-card-artwork";
import { Link } from "expo-router";
import { questActions } from "./commands/quest-actions";
import {
  useSessionActions,
  useSessionStatus,
  useAccountSnapshot,
} from "../../session/session";
import type { OccurrenceView } from "../../shared/contracts";
import { Card, Button, Label, colors, styles } from "../../shared/ui";
const categoryIcons: Record<
  string,
  "color-palette-outline" | "briefcase-outline"
> = {
  c03: "color-palette-outline",
  c04: "briefcase-outline",
};
export function QuestCard({ item }: { item: OccurrenceView }) {
  const { command } = useSessionActions();
  const { busy, pending } = useSessionStatus();
  const { catalog, state } = useAccountSnapshot();
  const q = item.occurrence.quest;
  const creative = q.categoryId === "c03";
  const accent = creative ? "#BB91F4" : colors.gold;
  const dueDate =
    item.occurrence.dueDate === state?.today
      ? "today"
      : item.occurrence.dueDate;
  const pendingStatus = item.pendingTimingVerification
    ? "Timing unverified"
    : "Pending sync";
  return (
    <Card
      style={{
        borderColor: creative ? "#9274B9" : colors.gold,
        gap: 8,
        padding: 16,
      }}
    >
      <QuestCardArtwork creative={creative} />
      <View
        style={{
          flexDirection: "row",
          alignItems: "center",
          gap: 8,
          flexWrap: "wrap",
        }}
      >
        <Ionicons
          name={categoryIcons[q.categoryId] ?? "compass-outline"}
          size={20}
          color={accent}
          accessible={false}
        />
        <Text
          selectable
          style={[styles.muted, { color: accent, fontSize: 14 }]}
        >
          {catalog?.categories.find((c) => c.id === q.categoryId)?.name}
        </Text>
        <Text
          style={[
            styles.muted,
            {
              borderWidth: 1,
              borderColor: accent,
              borderRadius: 10,
              paddingHorizontal: 8,
              paddingVertical: 2,
              fontSize: 12,
            },
          ]}
        >
          {item.pendingCompletion ? pendingStatus : item.status}
        </Text>
      </View>
      <Text accessibilityRole="header" selectable style={styles.subtitle}>
        {q.title}
      </Text>
      <View style={{ flexDirection: "row", gap: 8, alignItems: "center" }}>
        <View style={{ flex: 1, gap: 10, minHeight: 62 }}>
          <Text
            style={[
              styles.muted,
              { color: colors.gold, fontSize: 13, lineHeight: 19 },
            ]}
          >
            {q.baseXp} base XP · Rank {q.rank} · {q.effort}
          </Text>
          <Text
            selectable
            style={[styles.muted, { fontSize: 13, lineHeight: 19 }]}
          >
            {dueDate ? `Due ${dueDate}` : "Unscheduled"}
            {item.occurrence.plannedTime
              ? ` · Planned ${item.occurrence.plannedTime}`
              : ""}
          </Text>
        </View>
      </View>
      {item.pendingCompletion && (
        <View
          style={{
            borderWidth: 1,
            borderColor: colors.line,
            borderRadius: 10,
            padding: 10,
            backgroundColor: colors.card,
          }}
        >
          <Label style={styles.muted}>
            {item.pendingTimingVerification
              ? "Completion pending timing verification — rewards unconfirmed"
              : "Completion awaiting sync · rewards unconfirmed"}
          </Label>
        </View>
      )}
      <Link
        href={{ pathname: "/quest/[id]", params: { id: item.occurrence.id } }}
        accessibilityLabel={`View quest & rewards: ${q.title}`}
        style={[
          styles.text,
          {
            color: accent,
            paddingVertical: 12,
            minHeight: 48,
            borderTopWidth: 1,
            borderTopColor: colors.line,
            fontWeight: "600",
            backgroundColor: "rgba(9, 26, 41, 0.85)",
          },
        ]}
      >
        View quest & rewards
      </Link>
      {item.occurrence.lifecycle?.lockedLoss != null && (
        <Label style={{ backgroundColor: colors.card }}>
          Accepted loss: up to {item.occurrence.lifecycle.lockedLoss} category
          XP.
        </Label>
      )}
      {item.status === "Frozen" && (
        <Label style={{ backgroundColor: colors.card }}>
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
