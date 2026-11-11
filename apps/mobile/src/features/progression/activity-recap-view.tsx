import { useMemo, useState } from "react";
import { Link } from "expo-router";
import { Pressable, Text, View } from "react-native";
import { buttonAppearance, focusAppearance } from "@honeydrunk/ui-native";
import type { State } from "../../shared/contracts";
import { Button, Card, Label, useAppTheme } from "../../shared/ui";
import {
  activityRecap,
  recapDateLabel,
  type RecapPeriod,
} from "./activity-recap";

function RecapChoice({
  title,
  detail,
  selected,
  expanded,
  accessibilityLabel = title,
  onPress,
}: Readonly<{
  title: string;
  detail?: string;
  selected: boolean;
  expanded?: boolean;
  accessibilityLabel?: string;
  onPress(): void;
}>) {
  const { styles, colors, theme } = useAppTheme();
  const [focused, setFocused] = useState(false);
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={accessibilityLabel}
      accessibilityState={expanded === undefined ? { selected } : { expanded }}
      aria-pressed={expanded === undefined ? selected : undefined}
      aria-expanded={expanded}
      onPress={onPress}
      onFocus={() => setFocused(true)}
      onBlur={() => setFocused(false)}
      style={({ pressed }) => {
        const appearance = buttonAppearance(theme, !selected, false, pressed);
        return [
          styles.button,
          detail ? { paddingHorizontal: 10, gap: 4 } : null,
          {
            backgroundColor: appearance.backgroundColor,
            opacity: appearance.opacity,
          },
          focusAppearance(theme, focused),
        ];
      }}
    >
      <Text
        style={[
          styles.buttonText,
          {
            color: selected ? theme.colors.onPrimary : colors.green,
          },
        ]}
      >
        {title}
      </Text>
      {detail && (
        <Text
          style={[
            styles.buttonText,
            {
              fontSize: 12,
              color: selected ? theme.colors.onPrimary : colors.green,
            },
          ]}
        >
          {detail}
        </Text>
      )}
    </Pressable>
  );
}

export function ActivityRecap({
  state,
  pending,
  offline,
}: Readonly<{
  state: State;
  pending: boolean;
  offline: boolean;
}>) {
  const { colors, styles } = useAppTheme();
  const [period, setPeriod] = useState<RecapPeriod>(7);
  const [category, setCategory] = useState<string | null>(null);
  const [day, setDay] = useState<string | null>(null);
  const [limit, setLimit] = useState(5);
  const [expanded, setExpanded] = useState(false);
  const recap = useMemo(() => activityRecap(state, period), [state, period]);
  const selectedCategory = recap?.paths.some((path) => path.id === category)
    ? category
    : null;
  const selectedDay = recap?.days.some((item) => item.day === day) ? day : null;
  const peakCount = Math.max(
    1,
    ...(recap?.days.map((item) => item.count) ?? []),
  );
  const entries =
    recap?.entries.filter(
      (entry) =>
        (!selectedCategory || entry.categoryId === selectedCategory) &&
        (!selectedDay || entry.day === selectedDay),
    ) ?? [];
  return (
    <Card
      testID="activity-recap"
      style={{ gap: 16, borderTopWidth: 3, borderTopColor: colors.green }}
    >
      <Text accessibilityRole="header" style={styles.subtitle}>
        Your recent adventures
      </Text>
      <Label style={styles.muted}>
        Small steps count. Look back at the quests you’ve completed.
      </Label>
      <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 8 }}>
        {([7, 30] as const).map((value) => (
          <RecapChoice
            key={value}
            title={`Last ${value} days`}
            selected={period === value}
            onPress={() => {
              setPeriod(value);
              setCategory(null);
              setDay(null);
              setLimit(5);
            }}
          />
        ))}
      </View>
      {!recap ? (
        <Label>
          Activity dates are unavailable. Refresh your account to try again.
        </Label>
      ) : (
        <>
          <Label style={styles.muted}>
            {recapDateLabel(recap.start)} – {recapDateLabel(recap.end)}
          </Label>
          <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 16 }}>
            {[
              [recap.completed, "Completed quests"],
              [recap.activeDays, "Active days"],
              [recap.xp, "Overall XP"],
              [recap.paths.length, "Categories explored"],
            ].map(([value, label]) => (
              <View key={label} style={{ flexGrow: 1, flexBasis: 120, gap: 4 }}>
                <Text style={[styles.title, { color: colors.green }]}>
                  {value.toLocaleString()}
                </Text>
                <Label>{label}</Label>
              </View>
            ))}
          </View>
          <View
            accessible
            accessibilityRole="image"
            accessibilityLabel={`Activity over the last ${period} days: ${recap.completed} completed quests across ${recap.activeDays} active days`}
            style={{
              flexDirection: "row",
              alignItems: "flex-end",
              height: 54,
              gap: 3,
            }}
          >
            {recap.days.map((item) => (
              <View
                key={item.day}
                accessible={false}
                style={{
                  flex: 1,
                  height: Math.max(4, (48 * item.count) / peakCount),
                  borderRadius: 4,
                  backgroundColor: item.count ? colors.gold : colors.line,
                  opacity: item.count ? 1 : 0.45,
                }}
              />
            ))}
          </View>
          {offline && (
            <Label>
              Offline: showing your saved activity through{" "}
              {recapDateLabel(state.today)}.
            </Label>
          )}
          {pending && (
            <Label>
              Some changes are waiting to sync. Pending completions are
              excluded; Undo is reflected in this preview.
            </Label>
          )}
          <Label style={styles.muted}>
            XP earned from these quests. Category bonuses and penalties are
            tracked separately.
          </Label>
          <RecapChoice
            title={expanded ? "Hide activity details" : "Explore activity"}
            selected={expanded}
            expanded={expanded}
            onPress={() => setExpanded((value) => !value)}
          />
          {expanded && (
            <>
              <Text accessibilityRole="header" style={styles.subtitle}>
                A day at a time
              </Text>
              <Label style={styles.muted}>
                Choose a day to see its quests. Rest days are welcome.
              </Label>
              <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 6 }}>
                {recap.days.map((item) => (
                  <View key={item.day} style={{ flexBasis: 90, flexGrow: 1 }}>
                    <RecapChoice
                      title={recapDateLabel(item.day)}
                      detail={`${item.count} ${item.count === 1 ? "quest" : "quests"}`}
                      accessibilityLabel={`${recapDateLabel(item.day)}, ${item.count} completed ${item.count === 1 ? "quest" : "quests"}`}
                      selected={selectedDay === item.day}
                      onPress={() => {
                        setDay(selectedDay === item.day ? null : item.day);
                        setLimit(5);
                      }}
                    />
                  </View>
                ))}
              </View>
              <Text accessibilityRole="header" style={styles.subtitle}>
                Paths you explored
              </Text>
              {recap.paths.length ? (
                <View style={{ gap: 8 }}>
                  {recap.paths.map((path) => (
                    <RecapChoice
                      key={path.id}
                      title={`${path.name} · ${path.count}`}
                      selected={selectedCategory === path.id}
                      onPress={() => {
                        setCategory(
                          selectedCategory === path.id ? null : path.id,
                        );
                        setLimit(5);
                      }}
                    />
                  ))}
                </View>
              ) : (
                <Label>
                  No completed quests in this period yet. Your next small
                  adventure can start on the quest board.
                </Label>
              )}
              <Text accessibilityRole="header" style={styles.subtitle}>
                Completed adventures
              </Text>
              {(selectedCategory || selectedDay) && (
                <Button
                  title="Show all completed adventures"
                  secondary
                  onPress={() => {
                    setCategory(null);
                    setDay(null);
                    setLimit(5);
                  }}
                />
              )}
              {entries.slice(0, limit).map((entry) => (
                <View
                  key={entry.id}
                  style={{
                    gap: 4,
                    borderTopWidth: 1,
                    borderTopColor: colors.line,
                    paddingTop: 8,
                  }}
                >
                  <Link
                    href={{
                      pathname: "/quest/[id]",
                      params: { id: entry.occurrenceId },
                    }}
                    accessibilityLabel={`Open completed quest: ${entry.title}`}
                    style={[
                      styles.text,
                      {
                        color: colors.green,
                        minHeight: 48,
                        paddingVertical: 12,
                        textDecorationLine: "underline",
                      },
                    ]}
                  >
                    {entry.title}
                  </Link>
                  <Label style={styles.muted}>
                    {recapDateLabel(entry.day)} · {entry.categoryName} ·{" "}
                    {entry.xp.toLocaleString()} overall XP
                  </Label>
                </View>
              ))}
              {!entries.length && recap.completed > 0 && (
                <Label>No completed quests match these filters.</Label>
              )}
              {entries.length > limit && (
                <Button
                  title="Show more completed adventures"
                  secondary
                  onPress={() => setLimit((value) => value + 5)}
                />
              )}
            </>
          )}
          <Link
            href="/(tabs)/board"
            style={[
              styles.text,
              {
                color: colors.green,
                minHeight: 48,
                paddingVertical: 12,
                textDecorationLine: "underline",
              },
            ]}
          >
            Find your next quest
          </Link>
        </>
      )}
    </Card>
  );
}
