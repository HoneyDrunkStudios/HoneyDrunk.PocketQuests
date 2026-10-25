import { StyleSheet } from "react-native";
import { createStyles } from "@honeydrunk/ui-native";
import { pocketQuestsTheme } from "./theme";
export { Button, Label, Input, Card, Progress } from "@honeydrunk/ui-native";
export const colors = {
  ink: pocketQuestsTheme.colors.text,
  muted: pocketQuestsTheme.colors.muted,
  paper: pocketQuestsTheme.colors.background,
  card: pocketQuestsTheme.colors.surface,
  green: pocketQuestsTheme.colors.primary,
  line: pocketQuestsTheme.colors.border,
  gold: pocketQuestsTheme.colors.accent,
  onGold: pocketQuestsTheme.colors.onPrimary,
  danger: pocketQuestsTheme.colors.danger,
};
const base = createStyles(pocketQuestsTheme);
export const styles = StyleSheet.create({
  ...base,
  // Expo registers a runtime face in Android's normal slot. Asking for a bold
  // weight selects an unregistered variant and falls back to the system font.
  title: {
    ...base.title,
    fontFamily: "JournalSerifBold",
    fontWeight: "normal",
  },
  subtitle: {
    ...base.subtitle,
    fontFamily: "JournalSerifBold",
    fontWeight: "normal",
  },
});
