import { View } from "react-native";
import { Label, colors, styles } from "../../shared/ui";
import type { Balance, State } from "../../shared/contracts";
import { LevelProgress } from "./level-progress-view";
import type { ProgressionTrack } from "./level-progress";
export function ProgressRow({
  item,
  track,
}: Readonly<{ item: Balance; track: ProgressionTrack }>) {
  return (
    <View
      style={{
        gap: 8,
        paddingVertical: 14,
        borderBottomWidth: 1,
        borderBottomColor: colors.line,
      }}
    >
      <View
        style={{
          flexDirection: "row",
          flexWrap: "wrap",
          justifyContent: "space-between",
          gap: 8,
        }}
      >
        <Label style={{ fontWeight: "600", flexShrink: 1 }}>{item.name}</Label>
        <Label>
          Level {item.level} · {item.xp} XP
        </Label>
      </View>
      <LevelProgress
        compact
        name={item.name}
        track={track}
        xp={item.xp}
        level={item.level}
      />
    </View>
  );
}
export function CategoryProgress({
  item,
  streak,
}: Readonly<{
  item: Balance;
  streak?: State["streaks"][number];
}>) {
  let guidance = "Complete a quest to start a streak.";
  if (streak?.qualifiedToday) guidance = "Done today.";
  else if (streak && streak.days > 0) guidance = "Complete today to continue.";
  return (
    <View style={{ gap: 4, paddingBottom: 8 }}>
      <ProgressRow item={item} track="category" />
      {streak && (
        <Label style={[styles.muted, { fontSize: 13, lineHeight: 19 }]}>
          {streak.days > 0 ? `${streak.days}-day streak. ` : ""}
          {guidance}
        </Label>
      )}
    </View>
  );
}
