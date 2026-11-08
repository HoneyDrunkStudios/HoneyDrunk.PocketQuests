import { useMemo } from "react";
import { StyleSheet } from "react-native";
import { createStyles, useTheme } from "@honeydrunk/ui-native";
import type { Theme } from "@honeydrunk/ui-tokens";
import { pocketQuestsTheme, pocketQuestsDarkTheme } from "./theme";
export { Button, Label, Input, Card, Progress } from "@honeydrunk/ui-native";
function presentation(theme: Theme) {
  const base = createStyles(theme);
  return {
    theme,
    isDark: theme === pocketQuestsDarkTheme,
    colors: {
      ink: theme.colors.text,
      muted: theme.colors.muted,
      paper: theme.colors.background,
      card: theme.colors.surface,
      sky: theme === pocketQuestsDarkTheme ? "#302D52" : "#E6EAFE",
      green: theme.colors.primary,
      line: theme === pocketQuestsDarkTheme ? "#50486C" : "#DAD5ED",
      // Legacy product controls use this alias as their interactive accent.
      gold: theme.colors.primary,
      ochre: theme.colors.accent,
      onGold: theme.colors.onPrimary,
      danger: theme.colors.danger,
    },
    styles: StyleSheet.create({
      ...base,
      input: { ...base.input, color: theme.colors.text },
      title: {
        ...base.title,
        fontFamily: "QuestSansBold",
        fontWeight: "normal",
      },
      subtitle: {
        ...base.subtitle,
        fontFamily: "QuestSansBold",
        fontWeight: "normal",
      },
      card: { ...base.card, borderWidth: 0 },
    }),
  };
}
export const { colors, styles } = presentation(pocketQuestsTheme);
export function useAppTheme() {
  const theme = useTheme();
  return useMemo(() => presentation(theme), [theme]);
}
