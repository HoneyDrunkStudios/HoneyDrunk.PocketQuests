import { resolveTheme, type Theme } from "@honeydrunk/ui-tokens";
import type { TextStyle, ViewStyle } from "react-native";
export function createStyles(theme: Theme) {
  const resolved = resolveTheme(theme);
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
      fontWeight: resolved.typography.subtitleWeight,
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
    button: {
      minHeight: theme.controls.minHeight,
      minWidth: resolved.controls.minWidth,
      paddingVertical: s.control,
      paddingHorizontal: resolved.spacing.buttonHorizontal,
      borderRadius: r.controlRadius,
      justifyContent: "center",
      alignItems: "center",
      borderWidth: r.borderWidth,
      borderColor: c.primary,
    } satisfies ViewStyle,
    buttonText: {
      fontSize: t.bodySize,
      lineHeight: t.bodyLineHeight,
      fontWeight: resolved.typography.buttonWeight,
      fontFamily: t.fontFamily,
    } satisfies TextStyle,
    badge: {
      alignSelf: "flex-start",
      backgroundColor: c.surface,
      borderRadius: r.controlRadius,
      borderWidth: r.borderWidth,
      borderColor: c.border,
      padding: s.control,
    } satisfies ViewStyle,
    progressTrack: {
      height: resolved.controls.progressHeight,
      borderRadius: r.controlRadius,
      borderWidth: r.borderWidth,
      borderColor: c.border,
      backgroundColor: resolved.colors.progressTrack,
      overflow: "hidden",
      flexDirection: "row",
    } satisfies ViewStyle,
    progressFill: {
      height: "100%",
      backgroundColor: c.primary,
    } satisfies ViewStyle,
  };
}

/** An outline avoids layout shifts and remains separate from an error border. */
export function focusAppearance(theme: Theme, focused: boolean): ViewStyle {
  const resolved = resolveTheme(theme);
  return {
    outlineColor: resolved.colors.focus,
    outlineWidth: focused ? resolved.controls.focusWidth : 0,
    outlineOffset: resolved.controls.focusWidth,
    outlineStyle: "solid",
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
