import { View } from "react-native";
import { Label } from "../../shared/ui";
import type { Balance, State } from "../../shared/contracts";
export function ProgressRow({ item }: Readonly<{ item: Balance }>) {
  return (
    <View
      style={{
        paddingVertical: 8,
        flexDirection: "row",
        flexWrap: "wrap",
        justifyContent: "space-between",
        gap: 8,
      }}
    >
      <Label>{item.name}</Label>
      <Label>
        Level {item.level} · {item.xp} XP
      </Label>
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
    <View style={{ gap: 4, paddingBottom: 12 }}>
      <ProgressRow item={item} />
      {streak && (
        <Label>
          {item.name}: {streak.days} {streak.days === 1 ? "day" : "days"} in
          this streak. {guidance}
        </Label>
      )}
    </View>
  );
}
