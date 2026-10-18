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
  title: { ...base.title, fontFamily: "JournalSerif", fontWeight: "700" },
  subtitle: { ...base.subtitle, fontFamily: "JournalSerif", fontWeight: "700" },
});
