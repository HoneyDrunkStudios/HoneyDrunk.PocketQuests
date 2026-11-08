import { displayDate } from "../../shared/date-format";
import { useMemo } from "react";
import { Pressable, ScrollView, Text, View } from "react-native";
import { Button, Card, useAppTheme } from "../../shared/ui";
import type { OccurrenceView } from "../../shared/contracts";
import { monthDates, adjacentMonth } from "./month-grid";

export function MonthCalendar({
  month,
  selected,
  today,
  occurrences,
  onMonth,
  onSelect,
}: Readonly<{
  month: string;
  selected: string;
  today: string;
  occurrences: OccurrenceView[];
  onMonth(month: string): void;
  onSelect(date: string): void;
}>) {
  const { styles, colors } = useAppTheme();
  const dates = monthDates(month);
  const counts = useMemo(() => {
    const result = new Map<string, number>();
    for (const item of occurrences) {
      const date = item.occurrence.dueDate;
      if (date) result.set(date, (result.get(date) ?? 0) + 1);
    }
    return result;
  }, [occurrences]);
  const label = new Intl.DateTimeFormat(undefined, {
    month: "long",
    year: "numeric",
    timeZone: "UTC",
  }).format(new Date(`${month.slice(0, 7)}-01T00:00:00Z`));
  const previous = adjacentMonth(month, -1);
  const next = adjacentMonth(month, 1);
  return (
    <Card style={{ padding: 8, gap: 10 }}>
      <View style={{ flexDirection: "row", alignItems: "center", gap: 6 }}>
        <Button
          title="‹"
          accessibilityLabel="Previous month"
          secondary
          disabled={!previous}
          onPress={() => previous && onMonth(previous)}
        />
        <Text
          accessibilityRole="header"
          style={[
            styles.subtitle,
            { flex: 1, textAlign: "center", fontSize: 19 },
          ]}
        >
          {label}
        </Text>
        <Button
          title="›"
          accessibilityLabel="Next month"
          secondary
          disabled={!next}
          onPress={() => next && onMonth(next)}
        />
      </View>
      <ScrollView
        horizontal
        showsHorizontalScrollIndicator
        contentContainerStyle={{ flexGrow: 1 }}
      >
        <View style={{ width: 336, flexShrink: 0 }}>
          <View style={{ flexDirection: "row" }}>
            {["S", "M", "T", "W", "T", "F", "S"].map((day, index) => (
              <Text
                key={index}
                accessible={false}
                style={[
                  styles.muted,
                  {
                    width: "14.285714%",
                    textAlign: "center",
                    paddingVertical: 8,
                  },
                ]}
              >
                {day}
              </Text>
            ))}
          </View>
          <View style={{ flexDirection: "row", flexWrap: "wrap" }}>
            {dates.map((date, index) => (
              <View
                key={date ?? `empty-${index}`}
                style={{ width: "14.285714%", padding: 1 }}
              >
                {date ? (
                  <Pressable
                    accessibilityRole="button"
                    accessibilityLabel={`${displayDate(date)}${date === today ? ", today" : ""}, ${counts.get(date) ?? 0} quests`}
                    accessibilityState={{ selected: date === selected }}
                    onPress={() => onSelect(date)}
                    style={({ pressed }) => ({
                      minHeight: 48,
                      paddingVertical: 6,
                      alignItems: "center",
                      justifyContent: "center",
                      borderRadius: 10,
                      backgroundColor:
                        date === selected ? colors.gold : colors.card,
                      borderWidth: 1,
                      borderColor: date === today ? colors.gold : "transparent",
                      opacity: pressed ? 0.75 : 1,
                    })}
                  >
                    <Text
                      style={[
                        styles.text,
                        {
                          color: date === selected ? colors.onGold : colors.ink,
                          fontWeight: date === today ? "700" : "400",
                        },
                      ]}
                    >
                      {Number(date.slice(-2))}
                    </Text>
                    <View
                      accessible={false}
                      style={{
                        width: 5,
                        height: 5,
                        marginTop: 3,
                        borderRadius: 3,
                        backgroundColor: !counts.has(date)
                          ? "transparent"
                          : date === selected
                            ? colors.onGold
                            : colors.gold,
                      }}
                    />
                  </Pressable>
                ) : (
                  <View style={{ minHeight: 48 }} />
                )}
              </View>
            ))}
          </View>
        </View>
      </ScrollView>
    </Card>
  );
}
