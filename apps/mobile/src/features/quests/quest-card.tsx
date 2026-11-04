import { Pressable, Text, View } from "react-native";
import Ionicons from "@expo/vector-icons/Ionicons";
import { Link } from "expo-router";
import { QuestCardArtwork } from "./quest-card-artwork";
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
  const { catalog, state } = useAccountSnapshot();
  const q = item.occurrence.quest;
  const accent = q.categoryId === "c03" ? "#BB91F4" : colors.gold;
  const due =
    item.occurrence.dueDate === state?.today
      ? "today"
      : item.occurrence.dueDate;
  let status: string = item.status;
  if (item.pendingCompletion)
    status = item.pendingTimingVerification
      ? "Timing unverified"
      : "Pending sync";
  return (
    <Card style={{ padding: 0, borderColor: accent, gap: 0 }}>
      <Link
        href={{ pathname: "/quest/[id]", params: { id: item.occurrence.id } }}
        accessibilityLabel={`Quest details: ${q.title}`}
        asChild
      >
        <Pressable
          accessibilityRole="link"
          accessibilityLabel={`Quest details: ${q.title}`}
          style={({ pressed }) => ({
            padding: 16,
            gap: 12,
            opacity: pressed ? 0.8 : 1,
          })}
        >
          <View
            style={{ flexDirection: "row", alignItems: "flex-start", gap: 12 }}
          >
            <View style={{ flex: 1, gap: 8 }}>
              <Text style={[styles.muted, { color: accent, fontSize: 13 }]}>
                {catalog?.categories.find((c) => c.id === q.categoryId)?.name}
              </Text>
              <Text accessibilityRole="header" style={styles.subtitle}>
                {q.title}
              </Text>
            </View>
            <QuestCardArtwork categoryId={q.categoryId} />
          </View>
          <Text numberOfLines={2} style={styles.muted}>
            {q.criterion}
          </Text>
          <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 10 }}>
            <Text style={[styles.muted, { color: colors.gold }]}>
              {q.baseXp} base XP · Rank {q.rank} · {q.effort}
            </Text>
            <Text style={styles.muted}>{status}</Text>
          </View>
          <View
            style={{
              flexDirection: "row",
              gap: 8,
              alignItems: "center",
              justifyContent: "space-between",
            }}
          >
            <Text style={[styles.muted, { flex: 1 }]}>
              {due ? `Due ${due}` : "Unscheduled"}
              {item.occurrence.plannedTime
                ? ` · Planned ${item.occurrence.plannedTime}`
                : ""}
            </Text>
            <Ionicons
              name="arrow-forward"
              size={20}
              color={accent}
              accessible={false}
            />
          </View>
        </Pressable>
      </Link>
      {(item.pendingCompletion ||
        item.occurrence.lifecycle?.lockedLoss != null ||
        item.status === "Frozen" ||
        item.canUndo) && (
        <View style={{ padding: 16, paddingTop: 0, gap: 10 }}>
          {item.pendingCompletion && (
            <Label>
              {item.pendingTimingVerification
                ? "Completion pending timing verification — rewards unconfirmed"
                : "Completion awaiting sync · rewards unconfirmed"}
            </Label>
          )}
          {item.occurrence.lifecycle?.lockedLoss != null && (
            <Label>
              Accepted loss: up to {item.occurrence.lifecycle.lockedLoss}{" "}
              category XP.
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
              disabled={busy || pending}
              onPress={() =>
                void command({
                  action: questActions.undo,
                  occurrenceId: item.occurrence.id,
                  completionId: item.completion!.id,
                })
              }
            />
          )}
        </View>
      )}
    </Card>
  );
}
