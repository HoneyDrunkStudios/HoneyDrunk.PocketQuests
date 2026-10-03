import type { Theme } from "@honeydrunk/ui-tokens";
import type { TextStyle, ViewStyle } from "react-native";
export function createStyles(theme: Theme) {
  const c = theme.colors,
    t = theme.typography,
    s = theme.spacing,
    r = theme.shape;
  return {
    title: {
      fontSize: t.titleSize,
      fontWeight: t.titleWeight,
      color: c.text,
      fontFamily: t.fontFamily,
    } satisfies TextStyle,
    subtitle: {
      fontSize: t.subtitleSize,
      fontWeight: "600",
      color: c.text,
      fontFamily: t.fontFamily,
    } satisfies TextStyle,
    text: {
      fontSize: t.bodySize,
      lineHeight: t.bodyLineHeight,
      color: c.text,
      fontFamily: t.fontFamily,
    } satisfies TextStyle,
    muted: {
      fontSize: t.captionSize,
      lineHeight: t.captionLineHeight,
      color: c.muted,
      fontFamily: t.fontFamily,
    } satisfies TextStyle,
    card: {
      backgroundColor: c.surface,
      borderRadius: r.panelRadius,
      padding: s.panel,
      gap: s.gap,
      borderWidth: r.borderWidth,
      borderColor: c.border,
    } satisfies ViewStyle,
    input: {
      minHeight: theme.controls.minHeight,
      borderColor: c.border,
      borderWidth: r.borderWidth,
      borderRadius: r.controlRadius,
      padding: s.control,
      fontSize: t.bodySize,
      color: c.text,
      backgroundColor: c.input,
      fontFamily: t.fontFamily,
    } satisfies TextStyle,
  };
}
export function buttonAppearance(
  theme: Theme,
  secondary: boolean,
  disabled: boolean,
  pressed: boolean,
) {
  return {
    backgroundColor: secondary ? theme.colors.background : theme.colors.primary,
    color: secondary ? theme.colors.primary : theme.colors.onPrimary,
    opacity: disabled
      ? theme.controls.disabledOpacity
      : pressed
        ? theme.controls.pressedOpacity
        : 1,
  };
}
