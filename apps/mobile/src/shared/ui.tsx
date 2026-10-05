import { StyleSheet } from "react-native";
import { createStyles } from "@honeydrunk/ui-native";
import { pocketQuestsTheme } from "./theme";
export { Button, Label, Input, Card } from "@honeydrunk/ui-native";
export const colors = {
  ink: pocketQuestsTheme.colors.text,
  muted: pocketQuestsTheme.colors.muted,
  paper: pocketQuestsTheme.colors.background,
  card: pocketQuestsTheme.colors.surface,
  green: pocketQuestsTheme.colors.primary,
  line: pocketQuestsTheme.colors.border,
  gold: pocketQuestsTheme.colors.accent,
  danger: pocketQuestsTheme.colors.danger,
};
export const styles = StyleSheet.create(createStyles(pocketQuestsTheme));
